// tests/deeplynx-config.ts
import type { Page } from '@playwright/test';
import fs from 'fs';
import { loadEnvConfig } from '@next/env';

loadEnvConfig(process.cwd());

const FRONTEND_URL = process.env.NEXTAUTH_URL ?? 'http://localhost:3000';

// Orgs & Projects- configure any additional orgs and projects that are needed for testing here
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

// Accounts and Roles- configure any additional test accounts with their roles here

export const DEFAULT_ROLE_NAME = 'User';

export type TestAccountTitle =
  | 'sysAdmin'
  | 'orgAdminA'
  | 'orgAdminB'
  | 'projectAdminX'
  | 'standardUserX';

export interface TestAccount {
  name: TestAccountTitle;
  provision?: {
    role: 'org_admin' | 'project_admin' | 'user'; // currently setup for only assigning the available default roles
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
  provision: { role: 'user', org: ORGS.orgA.name, project: PROJECTS.projectX.name },
};

export const ACTINGUSERS: TestAccount[] = [
  sysAdmin, orgAdminA, orgAdminB, projectAdminX, standardUserX,
];

export const authFile = (name: TestAccountTitle) => `playwright/.auth/${name}.json`;
export const testUserCacheFile = 'playwright/.auth/testUserCache.json';

// TestUserCache — resolved IDs written by auth.setup.ts, read by tests
export interface TestUserCacheEntry {
  email: string;
  organizationId?: string;
  projectId?: string;
  userId?: string;
  apiKey?: string;
  apiSecret?: string;
}

type TestUserCache = Record<TestAccountTitle, TestUserCacheEntry>;

let cachedTestUsers: TestUserCache;
function readTestUserCache(): TestUserCache {
  if (!cachedTestUsers) cachedTestUsers = JSON.parse(fs.readFileSync(testUserCacheFile, 'utf8'));
  return cachedTestUsers;
}

export function accountMeta(name: TestAccountTitle) {
  return readTestUserCache()[name];
}

// UI selection helpers.
// Auth comes from storageState (see fixtures.ts). These only control UI state:
// Determines which org/project is active in the browser

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

export function projectIdByName(projectName: string): number {
  const owner = ACTINGUSERS.find((a) => a.provision?.project === projectName);
  if (!owner) {
    throw new Error(`No account is provisioned into project "${projectName}" — can't resolve its projectId.`);
  }
  const meta = accountMeta(owner.name);
  if (!meta?.projectId) {
    throw new Error(`No projectId cached for "${owner.name}" (project "${projectName}") — did auth.setup.ts run?`);
  }
  return Number(meta.projectId);
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

export async function selectProject(page: Page, account: TestAccount, projectNameOverride?: string) {
  const projectName = projectNameOverride ?? account.provision?.project;
  if (!projectName) {
    throw new Error(
      `No project to select for "${account.name}" — either provision it with a ` +
      `project, or pass an explicit project name (e.g. via test.use({ actingProject: ... })).`,
    );
  }

  // When overriding, resolve the projectId from whichever account actually owns that project
  const projectId = projectNameOverride
    ? projectIdByName(projectNameOverride)
    : Number(accountMeta(account.name)?.projectId);

  if (!projectId) {
    throw new Error(`No projectId resolved for "${account.name}" / project "${projectName}".`);
  }

  const serialized = JSON.stringify({ projectId, projectName });

  // ProjectSessionProvider.setProject writes to both localStorage AND a cookie
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
}