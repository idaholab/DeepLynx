
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

type NonUpdateAction = Exclude<PermissionAction, typeof PermissionAction['Update']>;

export type RolePermissions =
  Partial<Record<Exclude<PermissionResource, typeof PermissionResource['Insight']>, PermissionAction[]>> & {
    [PermissionResource.Insight]?: NonUpdateAction[];
  };

type ActionsAllowedFor<R extends PermissionResource> =
  R extends typeof PermissionResource['Insight']
    ? (NonUpdateAction | typeof PermissionAction['All'])[]
    : (PermissionAction | typeof PermissionAction['All'])[];

export type RolePermissionsInput = Partial<{ [R in PermissionResource]: ActionsAllowedFor<R> }>;

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
const INSIGHT_ACTIONS: PermissionAction[] = [PermissionAction.Read, PermissionAction.Write];

function expandActions(
  resource: PermissionResource,
  spec: (PermissionAction | typeof PermissionAction['All'])[],
): PermissionAction[] {
  const fullSet = resource === PermissionResource.Insight ? INSIGHT_ACTIONS : ALL_ACTIONS;
  if (spec.includes(PermissionAction.All)) return fullSet;
  const explicit = spec as PermissionAction[];
  const invalid = explicit.filter((a) => !fullSet.includes(a));
  if (invalid.length) throw new Error(`${resource} does not support: ${invalid.join(', ')}`);
  return [...new Set(explicit)];
}

export function defineRole(input: RolePermissionsInput | 'all'): CustomRole {
  const resolvedInput: RolePermissionsInput = input === 'all'
    ? Object.fromEntries(Object.values(PermissionResource).map((r) => [r, [PermissionAction.All]]))
    : input;

  const expanded = Object.fromEntries(
    Object.entries(resolvedInput).map(([resource, spec]) => [
      resource,
      expandActions(resource as PermissionResource, spec!),
    ]),
  ) as RolePermissions;

  const hash = createHash('sha1').update(stableStringify(expanded)).digest('hex').slice(0, 10);
  return { kind: 'custom', name: `PW Custom Role ${hash}`, permissions: expanded };
}

export const ROLES = {
  allPermissions: defineRole('all'),
} as const satisfies Record<string, CustomRole>;

// ---------------------------------------------------------------------
// Roles — permission-bearing roles ONLY. sysAdmin/orgAdmin/projectAdmin
// are NOT roles — they're booleans (users.IsSysAdmin, the org-admin
// endpoint, the project-member isProjectAdmin flag).
// ---------------------------------------------------------------------
export const Roles = {
  user: 'user',
} as const;
export type BuiltInRoleName = typeof Roles[keyof typeof Roles];
export type RoleSpec = BuiltInRoleName | CustomRole;

export const DEFAULT_ROLE_PERMISSIONS: RolePermissions = {
  [PermissionResource.Project]: [PermissionAction.Read],
  [PermissionResource.Organization]: [PermissionAction.Read], // ceiling — see SPECIAL CASE below
  [PermissionResource.Record]: [PermissionAction.Read, PermissionAction.Write],
  [PermissionResource.File]: [PermissionAction.Read, PermissionAction.Write],
  [PermissionResource.Edge]: [PermissionAction.Read, PermissionAction.Write],
  [PermissionResource.Tag]: [PermissionAction.Read],
};

// ---------------------------------------------------------------------------
// Configure Global Accounts
// ---------------------------------------------------------------------------
// SPECIAL CASE: Organization Write/Update can never be granted through a
// project-scoped role assignment. The backend's default project "User"
// role only ever grants Organization:Read. 
// Write/Update Org should not be given on the project level.
function assertRoleValidForProjectScope(role: RoleSpec, project?: TestProject): void {
  if (!project || role === Roles.user) return; // DEFAULT_ROLE_PERMISSIONS is already capped to Read
  const orgPerms = role.permissions[PermissionResource.Organization];
  const violation = orgPerms?.find((a) => a === PermissionAction.Write || a === PermissionAction.Update);
  if (violation) {
    throw new Error(
      `Role "${role.name}" grants "${violation}" on Organization but is being assigned within project ` +
      `scope ("${project.name}"). Organization Write/Update is only reachable via isOrgAdmin — never ` +
      `through a project-level role. Remove it from the role, or provision this account without a project.`,
    );
  }
}

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
  if (provision.role) assertRoleValidForProjectScope(provision.role, provision.project);
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
export const fullPermissionUserX: TestAccount = defineTestAccount({ role: ROLES.allPermissions, org: ORGS.orgA }, 'fullPermissionUserX');

export const authFile = (name: string) => `playwright/.auth/${name}.json`;
export interface ActionScope {
  org?: TestOrg;
  project?: TestProject;
}

function sameOrg(a?: TestOrg, b?: TestOrg): boolean {
  return !!a && !!b && a.name === b.name;
}
function sameProject(a?: TestProject, b?: TestProject): boolean {
  return !!a && !!b && a.name === b.name;
}

export function can(
  account: TestAccount,
  resource: PermissionResource,
  action: PermissionAction,
  scope: ActionScope = {},
): boolean {
  if (account.isSysAdmin) return true;

  const provision = account.provision;
  if (!provision) {
    throw new Error(`can(): account "${account.name}" is not sysAdmin and has no provision — nothing to evaluate.`);
  }

  const targetOrg = scope.org ?? provision.org;
  const targetProject = scope.project ?? provision.project;

  if (provision.isOrgAdmin && sameOrg(provision.org, targetOrg)) {
    return true;
  }

  if (provision.isProjectAdmin && sameProject(provision.project, targetProject)) {
    if (resource === PermissionResource.Organization) {
      return action === PermissionAction.Read; // same ceiling, enforced at runtime too
    }
    return true;
  }

  function hasPermission(perms: RolePermissions, resource: PermissionResource, action: PermissionAction): boolean {
    const grantedActions = perms[resource] as PermissionAction[] | undefined;
    return grantedActions?.includes(action) ?? false;
  }

  if (!provision.role) return false;
  if (provision.role === Roles.user) {
    return hasPermission(DEFAULT_ROLE_PERMISSIONS, resource, action);
  }
  return hasPermission(provision.role.permissions, resource, action);
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