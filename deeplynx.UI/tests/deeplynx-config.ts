import type { Page } from '@playwright/test';
import { createHash } from 'crypto';

// ----------------------------------------
// Configure Global Orgs & Projects
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
// Configure Global Roles
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
  readonly kind: 'custom';
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

  const hash = createHash('sha1').update(stableStringify(expanded)).digest('hex').slice(0, 10);
  return { kind: 'custom', name: `PW Custom Role ${hash}`, permissions: expanded };
}

export const ROLES = {
  allPermissions: defineRole('all'),
} as const satisfies Record<string, CustomRole>;

// ---------------------------------------------------------------------
// Roles — permission-bearing roles ONLY.
// ---------------------------------------------------------------------
export const Roles = {
  user: 'user',
} as const;
export type BuiltInRoleName = typeof Roles[keyof typeof Roles];
export type RoleSpec = BuiltInRoleName | CustomRole;

export const DEFAULT_ROLE_PERMISSIONS: RolePermissions = {
  [PermissionResource.Project]: [PermissionAction.Read],
  [PermissionResource.Organization]: [PermissionAction.Read],
  [PermissionResource.Record]: [PermissionAction.Read, PermissionAction.Write],
  [PermissionResource.File]: [PermissionAction.Read, PermissionAction.Write],
  [PermissionResource.Edge]: [PermissionAction.Read, PermissionAction.Write],
  [PermissionResource.Tag]: [PermissionAction.Read],
};

// ---------------------------------------------------------------------------
// Configure Global Accounts
// ---------------------------------------------------------------------------
export interface Provision {
  org?: TestOrg;
  project?: TestProject;
  isOrgAdmin?: boolean;     // grants org-admin via PUT /organizations/{orgId}/admin
  isProjectAdmin?: boolean; // grants project-admin via PUT .../projects/{id}/members
  role?: RoleSpec;          // baseline role — still assigned even if isProjectAdmin is true (see provisioning.ts)
}

export interface TestAccount {
  readonly name: string;
  readonly isSysAdmin?: boolean; // grants sysAdmin via PATCH /users/{userId}/admin — global, not org/project-scoped
  readonly provision?: Provision;
}

function provisionFingerprint(p: Provision): string {
  const identity = p.isProjectAdmin ? 'project_admin'
    : p.isOrgAdmin ? 'org_admin'
    : p.role ? (typeof p.role === 'string' ? p.role : p.role.name)
    : 'no-role';
  return [identity, p.org?.name ?? 'no-org', p.project?.name ?? 'no-project'].join('__').replace(/\s+/g, '-');
}

export function defineTestAccount(provision: Provision, name?: string): TestAccount {
  return { name: name ?? `auto-${provisionFingerprint(provision)}`, provision };
}

export function defineSysAdmin(name: string): TestAccount {
  return { name, isSysAdmin: true };
}

export function defineOrgAdmin(org: TestOrg, name?: string): TestAccount {
  return { name: name ?? `auto-orgAdmin-${org.name}`.replace(/\s+/g, '-'), provision: { org, isOrgAdmin: true } };
}

export function defineProjectAdmin(project: TestProject, name?: string): TestAccount {
  return {
    name: name ?? `auto-projectAdmin-${project.name}`.replace(/\s+/g, '-'),
    provision: { org: project.org, project, isProjectAdmin: true },
  };
}

// ---------------------------------------------------------------------
// Built-in accounts
// ---------------------------------------------------------------------
export const sysAdmin: TestAccount = { name: 'sysAdmin', isSysAdmin: true }; // env-var creds — see provisioning.ts special case
export const orgAdminA: TestAccount = defineOrgAdmin(ORGS.orgA, 'orgAdminA');
export const orgAdminB: TestAccount = defineOrgAdmin(ORGS.orgB, 'orgAdminB');
export const projectAdminX: TestAccount = defineProjectAdmin(PROJECTS.projectX, 'projectAdminX');
export const standardUserX: TestAccount = defineTestAccount({ role: Roles.user, org: ORGS.orgA, project: PROJECTS.projectX }, 'standardUserX');
export const fullPermissionUserX: TestAccount = defineTestAccount({ role: ROLES.allPermissions, org: ORGS.orgA, project: PROJECTS.projectX }, 'fullPermissionUserX');

export const authFile = (name: string) => `playwright/.auth/${name}.json`;

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