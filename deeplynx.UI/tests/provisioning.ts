
// tests/provisioning.ts
import { request, APIRequestContext } from '@playwright/test';
import jsonWebToken from 'jsonwebtoken';
import fs from 'fs';
import { loadEnvConfig } from '@next/env';
import {
  TestAccount, TestOrg, TestProject, CustomRole, RoleSpec, RolePermissions,
  DEFAULT_ROLE_NAME, authFile,
} from './deeplynx-config';

loadEnvConfig(process.cwd());

function requireEnv(name: string): string {
  const value = process.env[name];
  if (!value) throw new Error(`Missing required env var: ${name} — check your .env files`);
  return value;
}

const API_URL = requireEnv('BACKEND_BASE_URL');
const FRONTEND_URL = requireEnv('NEXTAUTH_URL');

// ---- Local JSON Cache files ------------------------------------------------------------
const scopeCacheFile = 'playwright/.auth/scopeCache.json'; // { [orgOrProjectName]: id }
const roleCacheFile = 'playwright/.auth/roleCache.json'; // { [`${orgId}:${roleName}`]: id }
const testUserCacheFile = 'playwright/.auth/testUserCache.json';

interface TestUserCacheEntry {
  email: string;
  organizationId?: string;
  projectId?: string;
  userId?: string;
  apiKey?: string;
  apiSecret?: string;
}

// Read and Write Json Cache Helpers
function readJsonCache<T>(file: string): Record<string, T> {
  try {
    return JSON.parse(fs.readFileSync(file, 'utf8'));
  } catch {
    return {};
  }
}

function writeJsonCache(file: string, data: unknown) {
  fs.mkdirSync('playwright/.auth', { recursive: true });
  fs.writeFileSync(file, JSON.stringify(data, null, 2));
}

// Re-use the sysAdmin JWT (Memoization Cache for sysAdmin)
// Promise is Memoized to avoid race conditions when multiple workers are running
let sysApiPromise: Promise<APIRequestContext> | undefined;

async function generateJwt(api: APIRequestContext, apiKey: string, apiSecret: string): Promise<string> {
  if (!apiKey || !apiSecret) {
    throw new Error(`generateJwt called with missing credentials (apiKey=${apiKey ? 'set' : 'MISSING'}, apiSecret=${apiSecret ? 'set' : 'MISSING'})`);
  }
  const res = await api.post(`${API_URL}/oauth/tokens`, {
    data: { apiKey, apiSecret, expirationMinutes: null },
  });
  if (!res.ok()) throw new Error(`Token generation failed (${res.status()}): ${await res.text()}`);
  const jwt = (await res.text()).trim();
  if (!jwt || jwt.split('.').length !== 3) {
    throw new Error(`Unexpected token response, doesn't look like a JWT: ${jwt}`);
  }
  return jwt;
}

async function fetchCurrentUser(api: APIRequestContext, jwt: string): Promise<{ email: string }> {
  const res = await api.get(`${API_URL}/users/current`, { headers: { Authorization: `Bearer ${jwt}` } });
  if (!res.ok()) throw new Error(`GET /users/current failed (${res.status()}): ${await res.text()}`);
  const user = await res.json();
  return { email: user.email };
}

// Local JSON Cache for Test Account cookies 
async function saveStorageState(accountName: string, jwt: string, email: string): Promise<void> {
  const url = new URL(FRONTEND_URL);
  const isSecure = url.protocol === 'https:';
  const cookieName = isSecure ? '__Secure-authjs.session-token' : 'authjs.session-token';
  const maxAgeSeconds = 60 * 60 * 2;

  const sessionToken = jsonWebToken.sign(
    {
      access_token: jwt,
      id_token: 'test-id-token',
      expires_at: Math.floor(Date.now() / 1000) + maxAgeSeconds,
      oktaId: email,
      username: email,
      groups: [],
      name: accountName,
      email,
      sub: email,
      organizationId: undefined,
    },
    requireEnv('NEXTAUTH_SECRET'),
    { expiresIn: maxAgeSeconds },
  );

  const state = {
    cookies: [{
      name: cookieName,
      value: sessionToken,
      domain: url.hostname,
      path: '/',
      expires: Math.floor(Date.now() / 1000) + maxAgeSeconds,
      httpOnly: true,
      secure: isSecure,
      sameSite: 'Lax' as const,
    }],
    origins: [],
  };
  fs.mkdirSync('playwright/.auth', { recursive: true });
  fs.writeFileSync(authFile(accountName), JSON.stringify(state, null, 2));
}

function getSysApi(): Promise<APIRequestContext> {
  if (!sysApiPromise) {
    sysApiPromise = (async () => {
      const sysKey = requireEnv('TEST_SYSADMIN_API_KEY');
      const sysSecret = requireEnv('TEST_SYSADMIN_SECRET');
      const anonApi = await request.newContext();
      const jwt = await generateJwt(anonApi, sysKey, sysSecret);
      const { email } = await fetchCurrentUser(anonApi, jwt);
      await saveStorageState('sysAdmin', jwt, email);
      return request.newContext({ extraHTTPHeaders: { Authorization: `Bearer ${jwt}` } });
    })();
  }
  return sysApiPromise;
}

// --------------------------------
// Org & project helpers
// --------------------------------
interface Scope { id: number; name: string; }

const orgIdMemo = new Map<TestOrg, Promise<string>>();
const projectIdMemo = new Map<TestProject, Promise<string>>();

async function findOrgId(sysApi: APIRequestContext, orgName: string): Promise<string | undefined> {
  const res = await sysApi.get(`${API_URL}/organizations?hideArchived=true`);
  if (!res.ok()) throw new Error(`Fetch orgs failed for "${orgName}" (${res.status()}): ${await res.text()}`);
  const orgs = (await res.json()) as Scope[];
  return orgs.find(o => o.name === orgName)?.id.toString();
}

async function createOrg(sysApi: APIRequestContext, orgName: string): Promise<string> {
  const res = await sysApi.post(`${API_URL}/organizations`, {
    data: { name: orgName, description: null, banner: null, requireSensitivityLabel: null },
  });
  if (!res.ok()) throw new Error(`Create org failed for "${orgName}" (${res.status()}): ${await res.text()}`);
  return String((await res.json() as Scope).id);
}

export function ensureOrg(org: TestOrg): Promise<string> {
  if (!orgIdMemo.has(org)) {
    orgIdMemo.set(org, (async () => {
      const scopeCache = readJsonCache<string>(scopeCacheFile);
      if (scopeCache[org.name]) return scopeCache[org.name];

      const sysApi = await getSysApi();
      const id = (await findOrgId(sysApi, org.name)) ?? (await createOrg(sysApi, org.name));

      scopeCache[org.name] = id;
      writeJsonCache(scopeCacheFile, scopeCache);
      return id;
    })());
  }
  return orgIdMemo.get(org)!;
}

async function findProjectId(sysApi: APIRequestContext, projectName: string, orgId: string): Promise<string | undefined> {
  const res = await sysApi.get(`${API_URL}/organizations/${orgId}/projects?hideArchived=true`);
  if (!res.ok()) throw new Error(`Fetch projects failed for "${projectName}" (${res.status()}): ${await res.text()}`);
  const projects = (await res.json()) as Scope[];
  return projects.find(p => p.name === projectName)?.id.toString();
}

async function createProject(sysApi: APIRequestContext, projectName: string, orgId: string): Promise<string> {
  const res = await sysApi.post(`${API_URL}/organizations/${orgId}/projects`, {
    data: { name: projectName, description: null, abbreviation: null, banner: null, requireSensitivityLabel: null },
  });
  if (!res.ok()) throw new Error(`Create project failed for "${projectName}" (${res.status()}): ${await res.text()}`);
  return String((await res.json() as Scope).id);
}

export function ensureProject(project: TestProject): Promise<string> {
  if (!projectIdMemo.has(project)) {
    projectIdMemo.set(project, (async () => {
      const scopeCache = readJsonCache<string>(scopeCacheFile);
      if (scopeCache[project.name]) return scopeCache[project.name];

      const orgId = await ensureOrg(project.org);
      const sysApi = await getSysApi();
      const id = (await findProjectId(sysApi, project.name, orgId)) ?? (await createProject(sysApi, project.name, orgId));

      scopeCache[project.name] = id;
      writeJsonCache(scopeCacheFile, scopeCache);
      return id;
    })());
  }
  return projectIdMemo.get(project)!;
}

// --------------------------------
// Roles & permissions helpers
// --------------------------------
// Roles and permissions can be defined at either the org or project level —
// this file only ever creates them at the ORG level for shareability
interface Role { id: number; name: string; }
interface Permission {
  id: number;
  name: string;
  description: string | null;
  lastUpdatedAt: string;
  lastUpdatedBy: string | null;
  isArchived: boolean;
  projectId: number | null;
  organizationId: number | null;
}

const roleIdCache = new Map<string, string>(); // `${orgId}:${roleName}`

async function findRoleIdByName(sysApi: APIRequestContext, orgId: string, roleName: string): Promise<string | undefined> {
  const cacheKey = `${orgId}:${roleName}`;
  if (roleIdCache.has(cacheKey)) return roleIdCache.get(cacheKey)!;

  const res = await sysApi.get(`${API_URL}/organizations/${orgId}/roles`);
  if (!res.ok()) throw new Error(`Fetch roles failed for org ${orgId} (${res.status()}): ${await res.text()}`);
  const roles = (await res.json()) as Role[];
  const role = roles.find(r => r.name === roleName);
  if (!role) return undefined;

  const id = String(role.id);
  roleIdCache.set(cacheKey, id);
  return id;
}

async function requireExistingRoleId(sysApi: APIRequestContext, orgId: string, roleName: string): Promise<string> {
  const id = await findRoleIdByName(sysApi, orgId, roleName);
  if (!id) {
    throw new Error(`Role "${roleName}" not found for org ${orgId} — expected a built-in org-level role with this name to already exist`);
  }
  return id;
}

async function getAllPermissions(sysApi: APIRequestContext, orgId: string): Promise<Permission[]> {
  const res = await sysApi.get(`${API_URL}/organizations/${orgId}/permissions?hideArchived=true`);
  if (!res.ok()) throw new Error(`Fetch permissions failed for org ${orgId} (${res.status()}): ${await res.text()}`);
  return (await res.json()) as Permission[];
}

function titleCaseWord(word: string): string {
  return word.charAt(0).toUpperCase() + word.slice(1);
}

function titleCaseResource(resource: string): string {
  return resource.split('_').map(titleCaseWord).join(' ');
}

function permissionLookupKey(resource: string, action: string): string {
  return `${titleCaseWord(action)} ${titleCaseResource(resource)}`;
}

function flattenRolePermissions(perms: RolePermissions): string[] {
  return Object.entries(perms).flatMap(([resource, actions]) =>
    ((actions ?? []) as string[]).map((action) => permissionLookupKey(resource, action)),
  );
}

async function resolvePermissionIds(sysApi: APIRequestContext, orgId: string, perms: RolePermissions): Promise<number[]> {
  const wantedKeys = flattenRolePermissions(perms);
  const catalog = await getAllPermissions(sysApi, orgId);
  const byKey = new Map(catalog.map(p => [p.name, p.id]));

  return wantedKeys.map((key) => {
    const id = byKey.get(key);
    if (id === undefined) throw new Error(`Permission "${key}" not found in org ${orgId} permission catalog`);
    return id;
  });
}

async function createCustomRole(sysApi: APIRequestContext, orgId: string, role: CustomRole): Promise<string> {
  const res = await sysApi.post(`${API_URL}/organizations/${orgId}/roles`, {
    data: { name: role.name, description: null },
  });
  if (!res.ok()) throw new Error(`Create custom role "${role.name}" failed (${res.status()}): ${await res.text()}`);
  const created = (await res.json()) as Role;
  const roleId = String(created.id);

  const permissionIds = await resolvePermissionIds(sysApi, orgId, role.permissions);
  const permsRes = await sysApi.put(`${API_URL}/organizations/${orgId}/roles/${roleId}/permissions`, {
    data: permissionIds,
  });
  if (!permsRes.ok()) {
    throw new Error(`Set permissions for role "${role.name}" (id ${roleId}) failed (${permsRes.status()}): ${await permsRes.text()}`);
  }

  return roleId;
}

function persistRoleId(cacheKey: string, id: string): void {
  roleIdCache.set(cacheKey, id);
  const diskCache = readJsonCache<string>(roleCacheFile);
  diskCache[cacheKey] = id;
  writeJsonCache(roleCacheFile, diskCache);
}

async function ensureCustomRoleId(sysApi: APIRequestContext, orgId: string, role: CustomRole): Promise<string> {
  const cacheKey = `${orgId}:${role.name}`;

  if (roleIdCache.has(cacheKey)) return roleIdCache.get(cacheKey)!;

  const diskCache = readJsonCache<string>(roleCacheFile);
  if (diskCache[cacheKey]) {
    roleIdCache.set(cacheKey, diskCache[cacheKey]);
    return diskCache[cacheKey];
  }

  const existing = await findRoleIdByName(sysApi, orgId, role.name);
  if (existing) {
    persistRoleId(cacheKey, existing);
    return existing;
  }

  try {
    const id = await createCustomRole(sysApi, orgId, role);
    persistRoleId(cacheKey, id);
    return id;
  } catch (err) {
    // If Err, Likely a concurrent worker created the exact same role (same
    // permissions -> same hash -> same name) between our check and our
    // create call. Re-check before giving up — if it exists now, use it
    // instead of treating this as a real failure.
    const raceWinner = await findRoleIdByName(sysApi, orgId, role.name);
    if (raceWinner) {
      persistRoleId(cacheKey, raceWinner);
      return raceWinner;
    }
    throw err;
  }
}

async function resolveProjectUserRoleId(sysApi: APIRequestContext, orgId: string, role: RoleSpec): Promise<string> {
  if (role === 'user') return requireExistingRoleId(sysApi, orgId, DEFAULT_ROLE_NAME);
  if (typeof role === 'string') throw new Error(`Unexpected role "${role}" reaching resolveNonAdminRoleId`);
  return ensureCustomRoleId(sysApi, orgId, role);
}

// --------------------------------
// Membership Helpers
// --------------------------------
interface UserSummary { id: number; }

async function isOrgMember(sysApi: APIRequestContext, orgId: string, userId: string): Promise<boolean> {
  const res = await sysApi.get(`${API_URL}/users`, {
    params: { organizationId: orgId, includeArchived: 'false', includeServiceAccounts: 'false', includeTestAccounts: 'true' },
  });
  if (!res.ok()) throw new Error(`Check org membership failed for user ${userId} org ${orgId} (${res.status()}): ${await res.text()}`);
  return ((await res.json()) as UserSummary[]).some(u => String(u.id) === userId);
}

async function isProjectMember(sysApi: APIRequestContext, projectId: string, userId: string): Promise<boolean> {
  const res = await sysApi.get(`${API_URL}/users`, {
    params: { projectId, includeArchived: 'false', includeServiceAccounts: 'false', includeTestAccounts: 'true' },
  });
  if (!res.ok()) throw new Error(`Check project membership failed for user ${userId} project ${projectId} (${res.status()}): ${await res.text()}`);
  return ((await res.json()) as UserSummary[]).some(u => String(u.id) === userId);
}

async function addUserToOrg(sysApi: APIRequestContext, orgId: string, userId: string, isAdmin: boolean): Promise<void> {
  if (await isOrgMember(sysApi, orgId, userId)) return;
  const res = await sysApi.post(`${API_URL}/organizations/${orgId}/user`, { params: { userId, isAdmin: String(isAdmin) } });
  if (!res.ok()) throw new Error(`Add user ${userId} to org ${orgId} failed (${res.status()}): ${await res.text()}`);
}

async function addUserToProject(
  sysApi: APIRequestContext, orgId: string, projectId: string, userId: string, isProjectAdmin: boolean, roleId?: string,
): Promise<void> {
  if (await isProjectMember(sysApi, projectId, userId)) return;
  const params: Record<string, string> = { userId, isProjectAdmin: String(isProjectAdmin) };
  if (roleId) params.roleId = roleId;
  const res = await sysApi.post(`${API_URL}/organizations/${orgId}/projects/${projectId}/members`, { params });
  if (!res.ok()) throw new Error(`Add user ${userId} to project ${projectId} failed (${res.status()}): ${await res.text()}`);
}

async function assignRole(
  sysApi: APIRequestContext, userId: string, provision: NonNullable<TestAccount['provision']>, orgId: string, projectId?: string,
): Promise<void> {
  await addUserToOrg(sysApi, orgId, userId, provision.role === 'org_admin');

  if (provision.project) {
    if (!projectId) throw new Error(`assignRole: provision.project "${provision.project.name}" set but no projectId resolved for user ${userId}`);

    if (provision.role === 'project_admin') {
      await addUserToProject(sysApi, orgId, projectId, userId, true);
    } else {
      // Built-in "user" or a custom role — both are org-level role
      // definitions, inherited by the project; only the resulting roleId
      // travels down to the project-membership call.
      const roleId = await resolveProjectUserRoleId(sysApi, orgId, provision.role);
      await addUserToProject(sysApi, orgId, projectId, userId, false, roleId);
    }
  }
}
// --------------------------------
// Account credentials
// --------------------------------
async function upsertTestAccountCreds(sysApi: APIRequestContext, accountName: string): Promise<{ apiKey: string; apiSecret: string; userId: string }> {
  const userRes = await sysApi.post(`${API_URL}/users/test?name=${accountName}`);
  if (!userRes.ok()) throw new Error(`Create test user failed for ${accountName} (${userRes.status()}): ${await userRes.text()}`);
  const userId = (await userRes.json()).id;

  const keyRes = await sysApi.post(`${API_URL}/oauth/keys/test/${userId}`);
  if (!keyRes.ok()) throw new Error(`Create key failed for ${accountName} (${keyRes.status()}): ${await keyRes.text()}`);
  const key = await keyRes.json();
  return { apiKey: key.apiKey, apiSecret: key.apiSecret, userId };
}

// Call before using any account. Memoized per-process (concurrent uses of
// the same account within a worker share one provisioning run) and cached
// locally as JSON under the playwright Directory (later runs / other workers skip straight to a validity check).
const accountMemo = new Map<TestAccount, Promise<TestUserCacheEntry>>();

export function ensureAccount(account: TestAccount): Promise<TestUserCacheEntry> {

  // Ensure auth is enabled so accounts can be provisioned
  if(process.env['NEXT_PUBLIC_DISABLE_FRONTEND_AUTHENTICATION'] == "true"){
      throw new Error(`Frontend & Backend Authentication must be enabled`);
  }

  if (!accountMemo.has(account)) {
    accountMemo.set(account, (async () => {
      // sysAdmin comes from env-var creds, not the test-user-creation API.
      if (account.name === 'sysAdmin') {
        await getSysApi();
        return {} as TestUserCacheEntry;
      }

      const cache = readJsonCache<TestUserCacheEntry>(testUserCacheFile);
      const cached = cache[account.name];
      const anonApi = await request.newContext();

      if (cached?.apiKey && cached?.apiSecret) {
        try {
          const jwt = await generateJwt(anonApi, cached.apiKey, cached.apiSecret);
          const { email } = await fetchCurrentUser(anonApi, jwt);
          await saveStorageState(account.name, jwt, email);
          return cached;
        } catch (err) {
          console.warn(`Cached credentials for "${account.name}" no longer valid, re-provisioning: ${err}`);
        }
      }

      const sysApi = await getSysApi();
      const { apiKey, apiSecret, userId } = await upsertTestAccountCreds(sysApi, account.name);
      const jwt = await generateJwt(anonApi, apiKey, apiSecret);
      const { email } = await fetchCurrentUser(anonApi, jwt);
      await saveStorageState(account.name, jwt, email);

      let organizationId: string | undefined;
      let projectId: string | undefined;

      if (account.provision) {
        organizationId = account.provision.org ? await ensureOrg(account.provision.org) : undefined;
        projectId = account.provision.project ? await ensureProject(account.provision.project) : undefined;
        if (organizationId) await assignRole(sysApi, userId, account.provision, organizationId, projectId);
      }

      const entry: TestUserCacheEntry = { email, organizationId, projectId, userId, apiKey, apiSecret };
      cache[account.name] = entry;
      writeJsonCache(testUserCacheFile, cache);
      return entry;
    })());
  }
  return accountMemo.get(account)!;
}