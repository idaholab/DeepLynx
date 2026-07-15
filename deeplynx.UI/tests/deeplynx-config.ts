// tests/deeplynx-config.ts
import type { Page } from '@playwright/test';
import fs from 'fs';
import { loadEnvConfig } from '@next/env';

loadEnvConfig(process.cwd());

const FRONTEND_URL = process.env.NEXTAUTH_URL ?? 'http://localhost:3000';

// ============================================================================
// Orgs & Projects
// ============================================================================

export interface TestOrg {
  name: string;
}
export interface TestProject {
  name: string;
  org: string; // references TestOrg.name
}

export const ORGS = {
  orgA: { name: 'PW Org A' },
  orgB: { name: 'PW Org B' },
} as const satisfies Record<string, TestOrg>;

export const PROJECTS = {
  projectX: { name: 'PW Project X', org: ORGS.orgA.name },
} as const satisfies Record<string, TestProject>;

// ============================================================================
// Roles
// ============================================================================
// Org-level admin status is a boolean (isAdmin) — there is no named org role.
// Project-level roles are named and fetched from
// GET /organizations/{orgId}/projects/{projectId}/roles.
// "standard" accounts are assigned this built-in project role by name.
// Custom roles are out of scope for test provisioning.

export const DEFAULT_PROJECT_ROLE_NAME = 'User';

// ============================================================================
// Accounts & Roles
// ============================================================================

export type TestAccountTitle =
  | 'sysAdmin'
  | 'orgAdminA'
  | 'orgAdminB'
  | 'projectAdminX'
  | 'standardUserX';

export interface TestAccount {
  name: TestAccountTitle;
  provision?: {
    role: 'org_admin' | 'project_admin' | 'standard';
    org?: string;     // an ORGS[...].name
    project?: string; // a PROJECTS[...].name
  };
}

export const sysAdmin: TestAccount = { name: 'sysAdmin' };
export const orgAdminA: TestAccount = { name: 'orgAdminA', provision: { role: 'org_admin', org: ORGS.orgA.name } };
export const orgAdminB: TestAccount = { name: 'orgAdminB', provision: { role: 'org_admin', org: ORGS.orgB.name } };
export const projectAdminX: TestAccount = {
  name: 'projectAdminX',
  provision: { role: 'project_admin', org: ORGS.orgA.name, project: PROJECTS.projectX.name },
};
export const standardUserX: TestAccount = {
  name: 'standardUserX',
  provision: { role: 'standard', org: ORGS.orgA.name, project: PROJECTS.projectX.name },
};

// Developers: to add a new account, declare it above and add it here.
// To add a new org/project for a boundary test, add it to ORGS/PROJECTS above
// and reference it from an account's `provision`. Nothing else needs to change.
export const ACTINGUSERS: TestAccount[] = [
  sysAdmin, orgAdminA, orgAdminB, projectAdminX, standardUserX,
];

export const authFile = (name: TestAccountTitle) => `playwright/.auth/${name}.json`;
export const testUserCacheFile = 'playwright/.auth/testUserCache.json';

// ============================================================================
// TestUserCache — resolved IDs written by auth.setup.ts, read by tests
// ============================================================================

type TestUserCache = Record<TestAccountTitle, { email: string; organizationId?: string; projectId?: string }>;

let cachedTestUsers: TestUserCache;
function readTestUserCache(): TestUserCache {
  if (!cachedTestUsers) cachedTestUsers = JSON.parse(fs.readFileSync(testUserCacheFile, 'utf8'));
  return cachedTestUsers;
}

export function accountMeta(name: TestAccountTitle) {
  return readTestUserCache()[name];
}

// ============================================================================
// UI selection helpers — which org/project is "active" in the browser
// ============================================================================
// Auth comes from storageState (see fixtures.ts). These only control UI state:
// which org/project the app treats as currently selected.

export function orgIdByName(orgName: string): number {
  const owner = ACTINGUSERS.find((a) => a.provision?.org === orgName);
  if (!owner) {
    throw new Error(`No account is provisioned into org "${orgName}" — can't resolve its organizationId.`);
  }
  const meta = accountMeta(owner.name);
  if (!meta?.organizationId) {
    throw new Error(`No organizationId cached for "${owner.name}" (org "${orgName}") — did auth.setup.ts run?`);
  }
  return Number(meta.organizationId);
}

export async function selectOrganization(page: Page, account: TestAccount, orgNameOverride?: string) {
  const orgName = orgNameOverride ?? account.provision?.org;
  if (!orgName) {
    throw new Error(
      `No org to select for "${account.name}" — either provision it with an org, ` +
      `or pass an explicit org name (e.g. via test.use({ actingOrg: ... })).`,
    );
  }
  const orgId = orgIdByName(orgName);

  await page.addInitScript(([id, name]) => {
    localStorage.setItem('organizationSession', JSON.stringify({ organizationId: id, organizationName: name }));
    localStorage.setItem('dashboard-tour-completed', 'true');
    localStorage.setItem('project-tour-completed', 'true');
  }, [orgId, orgName] as const);

  await page.context().addCookies([
    {
      name: 'organizationSession',
      value: encodeURIComponent(JSON.stringify({ organizationId: orgId, organizationName: orgName })),
      url: FRONTEND_URL,
    },
  ]);
}
export async function selectProject(page: Page, account: TestAccount) {
  const meta = accountMeta(account.name);
  if (!meta?.projectId) {
    throw new Error(`No projectId in registry for "${account.name}" — does this account provision a project?`);
  }
  const projectName = account.provision?.project ?? '';
  const serialized = JSON.stringify({ projectId: meta.projectId, projectName });

  // ProjectSessionProvider.setProject writes to both localStorage AND a
  // cookie (see src/app/contexts/ProjectSessionProvider.tsx) — mirror both,
  // even though today's middleware only reads organizationSession for
  // redirects. Other server-side code may still read the project cookie.
  await page.addInitScript((serialized) => {
    localStorage.setItem('projectSession', serialized);
  }, serialized);

  await page.context().addCookies([
    {
      name: 'projectSession',
      value: encodeURIComponent(serialized),
      url: FRONTEND_URL,
    },
  ]);

  await page.waitForURL(`**/project/${meta.projectId}`).catch(() => {});
}