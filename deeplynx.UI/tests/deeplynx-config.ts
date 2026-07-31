// tests/deeplynx-config.ts
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

// `All` is an authoring-time shorthand, not a real permission. Keeps provisioning more succinct
export const PermissionAction = {
  Read: 'Read',
  Write: 'Write',
  Update: 'Update',
  All: 'All',
} as const;

export type PermissionAction =
  Exclude<typeof PermissionAction[keyof typeof PermissionAction], typeof PermissionAction['All']>;

// 'Insight' only has Read/Write on the actual permission matrix so it's typed separately
type NonUpdateAction = Exclude<PermissionAction, typeof PermissionAction['Update']>;

// Hash the full shape. If 'all' is provided for the permission action, 
// the hash the full list of permission actions instead of 'all'
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

// Hash the full list of resource permission actions. 
// If all is provided Hash the full action list for that resource
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

// List specific resource permission actions or 'all' for faster provisioning.
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
  // Add more shared custom roles here, e.g.:
  //   dataEditor: defineRole({
  //     [PermissionResource.Record]: [PermissionAction.All],
  //     [PermissionResource.Edge]: [PermissionAction.All],
  //   }),
} as const satisfies Record<string, CustomRole>;

export type RoleSpec = 'org_admin' | 'project_admin' | 'user' | CustomRole;

// ---------------------------------------------------------------------------
// Configure Global Accounts
// Named accounts (sysAdmin, orgAdminA, ...) are available in any test to import and use. 
// One-off accounts can be created and provisioned per test with "defineTestAccount"
// ---------------------------------------------------------------------------
export interface Provision {
  role: RoleSpec;
  org?: TestOrg;
  project?: TestProject;
}

export interface TestAccount {
  readonly name: string; // stable across runs — the cache key & authFile name
  readonly provision?: Provision;
}

function roleKey(role: RoleSpec): string {
  return typeof role === 'string' ? role : role.name;
}

function provisionFingerprint(p: Provision): string {
  return [roleKey(p.role), p.org?.name ?? 'no-org', p.project?.name ?? 'no-project']
    .join('__')
    .replace(/\s+/g, '-');
}

export function defineTestAccount(provision: Provision, name?: string): TestAccount {
  return { name: name ?? `auto-${provisionFingerprint(provision)}`, provision };
}

export const sysAdmin: TestAccount = { name: 'sysAdmin' };
export const orgAdminA: TestAccount = { name: 'orgAdminA', provision: { role: 'org_admin', org: ORGS.orgA } };
export const orgAdminB: TestAccount = { name: 'orgAdminB', provision: { role: 'org_admin', org: ORGS.orgB } };
export const projectAdminX: TestAccount = {
  name: 'projectAdminX',
  provision: { role: 'project_admin', org: ORGS.orgA, project: PROJECTS.projectX },
};
export const standardUserX: TestAccount = {
  name: 'standardUserX',
  provision: { role: 'user', org: ORGS.orgA, project: PROJECTS.projectX },
};

export const authFile = (name: string) => `playwright/.auth/${name}.json`;


// ---------------------------------------------------------------------------
// UI selection helpers. To write the localStorage/cookie state the frontend reads.
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
    {
      name: 'projectSession',
      value: encodeURIComponent(serialized),
      url: FRONTEND_URL,
    },
  ]);
}