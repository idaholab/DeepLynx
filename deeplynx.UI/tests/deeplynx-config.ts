import type { Page } from '@playwright/test';
import { createHash } from 'crypto';
import fs from 'fs';

// ----------------------------------------
// Configure Orgs & Projects
// ----------------------------------------
export interface TestOrg {
  readonly name: string;
}
export interface TestProject {
  readonly name: string;
  readonly org: TestOrg;
}

export const ORGS = {
  orgA: { name: 'PW Org A' },
  orgB: { name: 'PW Org B' },
} as const satisfies Record<string, TestOrg>;

export const PROJECTS = {
  projectX: { name: 'PW Project X', org: ORGS.orgA },
} as const satisfies Record<string, TestProject>;

// ---------------------------------------------------------------------------
// Configure Roles
// ---------------------------------------------------------------------------
export const DEFAULT_ROLE_NAME = 'User';

export const PermissionResource = {
  Project: 'Project',
  ObjectStorage: 'Object Storage',
  DataSource: 'Data Source',
  Record: 'Record',
  Edge: 'Edge',
  File: 'File',
  Tag: 'Tag',
  Class: 'Class',
  Relationship: 'Relationship',
  User: 'User',
  Group: 'Group',
  Organization: 'Organization',
  Role: 'Role',
  Permission: 'Permission',
  SensitivityLabel: 'Sensitivity Label',
  RecordCollection: 'Record Collection',
  Insight: 'Insight',
} as const;
export type PermissionResource = typeof PermissionResource[keyof typeof PermissionResource];

export const PermissionAction = {
  Read: 'Read',
  Write: 'Write',
  Update: 'Update',
  All: 'All',
} as const;

export type PermissionAction =
  Exclude<typeof PermissionAction[keyof typeof PermissionAction], typeof PermissionAction['All']>;

export type RolePermissions = Partial<Record<PermissionResource, PermissionAction[]>>;

export type RolePermissionsInput = Partial<Record<PermissionResource, (PermissionAction | typeof PermissionAction['All'])[]>>;
export interface CustomRole {
  readonly name: string;
  readonly permissions: RolePermissions;
}

function stableStringify(perms: RolePermissions): string {
  const sorted = Object.keys(perms).sort().reduce((acc, key) => {
    acc[key] = [...(perms as Record<string, string[]>)[key]].sort();
    return acc;
  }, {} as Record<string, string[]>);
  return JSON.stringify(sorted);
}

const ALL_ACTIONS: PermissionAction[] = [PermissionAction.Read, PermissionAction.Write, PermissionAction.Update];

function expandActions(spec: (PermissionAction | typeof PermissionAction['All'])[]): PermissionAction[] {
  if (spec.includes(PermissionAction.All)) return ALL_ACTIONS;
  return [...new Set(spec as PermissionAction[])];
}

export function defineRole(input: RolePermissionsInput | 'all'): CustomRole {
  const resolvedInput: RolePermissionsInput = input === 'all'
    ? Object.fromEntries(Object.values(PermissionResource).map((r) => [r, [PermissionAction.All]]))
    : input;

  const expanded = Object.fromEntries(
    Object.entries(resolvedInput).map(([resource, spec]) => [resource, expandActions(spec!)]),
  ) as RolePermissions;

  // Create Hash to limit creating new roles every run. 
  const hash = createHash('sha1').update(stableStringify(expanded)).digest('hex').slice(0, 10);
  return { name: `PW Custom Role ${hash}`, permissions: expanded };
}

export const ROLES = {
  allPermissions: defineRole('all'),
} as const satisfies Record<string, CustomRole>;

// ---------------------------------------------------------------------
// Roles
// ---------------------------------------------------------------------
export const Roles = {
  user: 'user',
} as const;
export type BuiltInRoleName = typeof Roles[keyof typeof Roles];
export type RoleSpec = BuiltInRoleName | CustomRole;

// ---------------------------------------------------------------------------
// Configure Accounts
// ---------------------------------------------------------------------------
export interface Provision {
  org?: TestOrg;
  project?: TestProject;
  isOrgAdmin?: boolean;
  isProjectAdmin?: boolean;
  role?: RoleSpec;
}

export interface TestAccount {
  readonly name: string;
  readonly isSysAdmin?: boolean;
  readonly provision?: Provision;
}

export function defineTestAccount(provision: Provision, name: string): TestAccount {
  return { name, provision };
}

export function defineSysAdmin(name: string): TestAccount {
  return { name, isSysAdmin: true };
}

export function defineOrgAdmin(org: TestOrg, name: string): TestAccount {
  return { name, provision: { org, isOrgAdmin: true } };
}

export function defineProjectAdmin(project: TestProject, name: string): TestAccount {
  return { name, provision: { org: project.org, project, isProjectAdmin: true } };
}

export const sysAdmin: TestAccount = { name: 'sysAdmin', isSysAdmin: true }; // Uses env-var creds
export const orgAdminA: TestAccount = defineOrgAdmin(ORGS.orgA, 'orgAdminA');
export const orgAdminB: TestAccount = defineOrgAdmin(ORGS.orgB, 'orgAdminB');
export const projectAdminX: TestAccount = defineProjectAdmin(PROJECTS.projectX, 'projectAdminX');
export const standardUserX: TestAccount = defineTestAccount({ role: Roles.user, org: ORGS.orgA, project: PROJECTS.projectX }, 'standardUserX');
export const fullPermissionUserX: TestAccount = defineTestAccount({ role: ROLES.allPermissions, org: ORGS.orgA, project: PROJECTS.projectX }, 'fullPermissionUserX');

export const TEST_ACCOUNTS: TestAccount[] = [
  sysAdmin,
  orgAdminA,
  orgAdminB,
  projectAdminX,
  standardUserX,
  fullPermissionUserX,
  // Add new accounts here.
];

export const authFile = (name: string) => `playwright/.auth/${name}.json`;

// ---------------------------------------------------------------------------
// Cache files
// ---------------------------------------------------------------------------
export const scopeCacheFile = 'playwright/.auth/scopeCache.json';
export const roleCacheFile = 'playwright/.auth/roleCache.json';
export const testUserCacheFile = 'playwright/.auth/testUserCache.json';

export interface TestUserCacheEntry {
  email: string;
  organizationId?: string;
  projectId?: string;
  userId?: string;
  apiKey?: string;
  apiSecret?: string;
}

export function readJsonCache<T>(file: string): Record<string, T> {
  try { return JSON.parse(fs.readFileSync(file, 'utf8')); } catch { return {}; }
}

// ---------------------------------------------------------------------------
// UI selection helpers
// ---------------------------------------------------------------------------
const FRONTEND_URL = process.env.NEXTAUTH_URL ?? 'http://localhost:3000';

export async function selectOrganization(page: Page, orgId: string, orgName: string) {
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

export async function selectProject(page: Page, projectId: string, projectName: string) {
  const serialized = JSON.stringify({ projectId, projectName });
  await page.addInitScript((serialized) => {
    localStorage.setItem('projectSession', serialized);
  }, serialized);
  await page.context().addCookies([
    { name: 'projectSession', value: encodeURIComponent(serialized), url: FRONTEND_URL },
  ]);
}