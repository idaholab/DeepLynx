import { test as setup, request, APIRequestContext } from "@playwright/test";
import jsonWebToken from "jsonwebtoken";
import fs from "fs";
import { loadEnvConfig } from "@next/env";
import {
  TestAccount,
  TestOrg,
  TestProject,
  CustomRole,
  RoleSpec,
  RolePermissions,
  Roles,
  DEFAULT_ROLE_NAME,
  TEST_ACCOUNTS,
  authFile,
  scopeCacheFile,
  roleCacheFile,
  testUserCacheFile,
  TestUserCacheEntry,
  readJsonCache,
} from "./deeplynx-config";
import { TEST_API_BASE_URL } from "./api-url";

loadEnvConfig(process.cwd());

function requireEnv(name: string): string {
  const value = process.env[name];
  if (!value)
    throw new Error(
      `Missing required env var: ${name} — check your .env files`,
    );
  return value;
}

const API_URL = TEST_API_BASE_URL;
const FRONTEND_URL = requireEnv("NEXTAUTH_URL");

function writeJsonCache(file: string, data: unknown) {
  fs.mkdirSync("playwright/.auth", { recursive: true });
  fs.writeFileSync(file, JSON.stringify(data, null, 2));
}

function updateJsonCache<T>(
  file: string,
  updater: (current: Record<string, T>) => Record<string, T>,
): Record<string, T> {
  const current = readJsonCache<T>(file);
  const next = updater(current);
  writeJsonCache(file, next);
  return next;
}

let sysApiPromise: Promise<APIRequestContext> | undefined;

async function generateJwt(
  api: APIRequestContext,
  apiKey: string,
  apiSecret: string,
): Promise<string> {
  if (!apiKey || !apiSecret) {
    throw new Error(
      `generateJwt called with missing credentials (apiKey=${apiKey ? "set" : "MISSING"}, apiSecret=${apiSecret ? "set" : "MISSING"})`,
    );
  }
  const res = await api.post(`${API_URL}/oauth/tokens`, {
    data: { apiKey, apiSecret, expirationMinutes: null },
  });
  if (!res.ok())
    throw new Error(
      `Token generation failed (${res.status()}): ${await res.text()}`,
    );
  const jwt = (await res.text()).trim();
  if (!jwt || jwt.split(".").length !== 3)
    throw new Error(
      `Unexpected token response, doesn't look like a JWT: ${jwt}`,
    );
  return jwt;
}

async function fetchCurrentUser(
  api: APIRequestContext,
  jwt: string,
): Promise<{ email: string }> {
  const res = await api.get(`${API_URL}/users/current`, {
    headers: { Authorization: `Bearer ${jwt}` },
  });
  if (!res.ok())
    throw new Error(
      `GET /users/current failed (${res.status()}): ${await res.text()}`,
    );
  return { email: (await res.json()).email };
}

async function saveStorageState(
  accountName: string,
  jwt: string,
  email: string,
): Promise<void> {
  const url = new URL(FRONTEND_URL);
  const isSecure = url.protocol === "https:";
  const cookieName = isSecure
    ? "__Secure-authjs.session-token"
    : "authjs.session-token";
  const maxAgeSeconds = 60 * 60 * 2;

  const sessionToken = jsonWebToken.sign(
    {
      access_token: jwt,
      id_token: "test-id-token",
      expires_at: Math.floor(Date.now() / 1000) + maxAgeSeconds,
      oktaId: email,
      username: email,
      groups: [],
      name: accountName,
      email,
      sub: email,
      organizationId: undefined,
    },
    requireEnv("NEXTAUTH_SECRET"),
    { expiresIn: maxAgeSeconds },
  );

  const state = {
    cookies: [
      {
        name: cookieName,
        value: sessionToken,
        domain: url.hostname,
        path: "/",
        expires: Math.floor(Date.now() / 1000) + maxAgeSeconds,
        httpOnly: true,
        secure: isSecure,
        sameSite: "Lax" as const,
      },
    ],
    origins: [],
  };
  fs.mkdirSync("playwright/.auth", { recursive: true });
  fs.writeFileSync(authFile(accountName), JSON.stringify(state, null, 2));
}

function getSysApi(): Promise<APIRequestContext> {
  if (!sysApiPromise) {
    sysApiPromise = (async () => {
      const sysKey = requireEnv("TEST_SYSADMIN_API_KEY");
      const sysSecret = requireEnv("TEST_SYSADMIN_SECRET");
      const anonApi = await request.newContext();
      try {
        const jwt = await generateJwt(anonApi, sysKey, sysSecret);
        const { email } = await fetchCurrentUser(anonApi, jwt);
        await saveStorageState("sysAdmin", jwt, email);
        return request.newContext({
          extraHTTPHeaders: { Authorization: `Bearer ${jwt}` },
        });
      } finally {
        await anonApi.dispose();
      }
    })();
  }
  return sysApiPromise;
}

// --------------------------------
// Org & project helpers
// --------------------------------
interface Scope {
  id: number;
  name: string;
}

async function findOrgId(
  sysApi: APIRequestContext,
  orgName: string,
): Promise<string | undefined> {
  const res = await sysApi.get(`${API_URL}/organizations?hideArchived=true`);
  if (!res.ok())
    throw new Error(
      `Fetch orgs failed for "${orgName}" (${res.status()}): ${await res.text()}`,
    );
  const orgs = (await res.json()) as Scope[];
  return orgs.find((o) => o.name === orgName)?.id.toString();
}

async function createOrg(
  sysApi: APIRequestContext,
  orgName: string,
): Promise<string> {
  const res = await sysApi.post(`${API_URL}/organizations`, {
    data: {
      name: orgName,
      description: null,
      banner: null,
      requireSensitivityLabel: null,
    },
  });
  if (!res.ok())
    throw new Error(
      `Create org failed for "${orgName}" (${res.status()}): ${await res.text()}`,
    );
  return String(((await res.json()) as Scope).id);
}

async function ensureOrg(org: TestOrg): Promise<string> {
  const scopeCache = readJsonCache<string>(scopeCacheFile);
  if (scopeCache[org.name]) return scopeCache[org.name];

  const sysApi = await getSysApi();
  const existing = await findOrgId(sysApi, org.name);
  const id = existing ?? (await createOrg(sysApi, org.name));

  updateJsonCache<string>(scopeCacheFile, (cache) => ({
    ...cache,
    [org.name]: id,
  }));
  return id;
}

async function findProjectId(
  sysApi: APIRequestContext,
  projectName: string,
  orgId: string,
): Promise<string | undefined> {
  const res = await sysApi.get(
    `${API_URL}/organizations/${orgId}/projects?hideArchived=true`,
  );
  if (!res.ok())
    throw new Error(
      `Fetch projects failed for "${projectName}" (${res.status()}): ${await res.text()}`,
    );
  const projects = (await res.json()) as Scope[];
  return projects.find((p) => p.name === projectName)?.id.toString();
}

async function createProject(
  sysApi: APIRequestContext,
  projectName: string,
  orgId: string,
): Promise<string> {
  const res = await sysApi.post(`${API_URL}/organizations/${orgId}/projects`, {
    data: {
      name: projectName,
      description: null,
      abbreviation: null,
      banner: null,
      requireSensitivityLabel: null,
    },
  });
  if (!res.ok())
    throw new Error(
      `Create project failed for "${projectName}" (${res.status()}): ${await res.text()}`,
    );
  return String(((await res.json()) as Scope).id);
}

async function ensureProject(project: TestProject): Promise<string> {
  const scopeCache = readJsonCache<string>(scopeCacheFile);
  if (scopeCache[project.name]) return scopeCache[project.name];

  const orgId = await ensureOrg(project.org);
  const sysApi = await getSysApi();
  const existing = await findProjectId(sysApi, project.name, orgId);
  const id = existing ?? (await createProject(sysApi, project.name, orgId));

  updateJsonCache<string>(scopeCacheFile, (cache) => ({
    ...cache,
    [project.name]: id,
  }));
  return id;
}

// --------------------------------
// Roles & permissions helpers
// --------------------------------
interface Role {
  id: number;
  name: string;
}
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

async function findRoleIdByName(
  sysApi: APIRequestContext,
  orgId: string,
  roleName: string,
): Promise<string | undefined> {
  const res = await sysApi.get(`${API_URL}/organizations/${orgId}/roles`);
  if (!res.ok())
    throw new Error(
      `Fetch roles failed for org ${orgId} (${res.status()}): ${await res.text()}`,
    );
  const roles = (await res.json()) as Role[];
  return roles.find((r) => r.name === roleName)?.id.toString();
}

async function getAllPermissions(
  sysApi: APIRequestContext,
  orgId: string,
): Promise<Permission[]> {
  const res = await sysApi.get(
    `${API_URL}/organizations/${orgId}/permissions?hideArchived=true`,
  );
  if (!res.ok())
    throw new Error(
      `Fetch permissions failed for org ${orgId} (${res.status()}): ${await res.text()}`,
    );
  return (await res.json()) as Permission[];
}

function flattenRolePermissions(perms: RolePermissions): string[] {
  return Object.entries(perms).flatMap(([resource, actions]) =>
    ((actions ?? []) as string[]).map((action) => `${action} ${resource}`),
  );
}

async function resolvePermissionIds(
  sysApi: APIRequestContext,
  orgId: string,
  perms: RolePermissions,
): Promise<number[]> {
  const wantedKeys = flattenRolePermissions(perms);
  const catalog = await getAllPermissions(sysApi, orgId);
  const byKey = new Map(catalog.map((p) => [p.name, p.id]));
  return wantedKeys.map((key) => {
    const id = byKey.get(key);
    if (id === undefined)
      throw new Error(
        `Permission "${key}" not found in org ${orgId} permission catalog`,
      );
    return id;
  });
}

async function createCustomRole(
  sysApi: APIRequestContext,
  orgId: string,
  role: CustomRole,
): Promise<string> {
  const res = await sysApi.post(`${API_URL}/organizations/${orgId}/roles`, {
    data: { name: role.name, description: null },
  });
  if (!res.ok())
    throw new Error(
      `Create custom role "${role.name}" failed (${res.status()}): ${await res.text()}`,
    );
  const created = (await res.json()) as Role;
  const roleId = String(created.id);
  const permissionIds = await resolvePermissionIds(
    sysApi,
    orgId,
    role.permissions,
  );
  const permsRes = await sysApi.put(
    `${API_URL}/organizations/${orgId}/roles/${roleId}/permissions`,
    { data: permissionIds },
  );
  if (!permsRes.ok())
    throw new Error(
      `Set permissions for role "${role.name}" (id ${roleId}) failed (${permsRes.status()}): ${await permsRes.text()}`,
    );
  return roleId;
}

async function resolveProjectUserRoleId(
  sysApi: APIRequestContext,
  orgId: string,
  role: RoleSpec,
): Promise<string> {
  const isBuiltIn = role === Roles.user;
  const roleName = isBuiltIn ? DEFAULT_ROLE_NAME : role.name;
  const cacheKey = `${orgId}:${roleName}`;

  if (!isBuiltIn) {
    const diskCache = readJsonCache<string>(roleCacheFile);
    if (diskCache[cacheKey]) return diskCache[cacheKey];
  }

  const existing = await findRoleIdByName(sysApi, orgId, roleName);

  if (isBuiltIn) {
    if (!existing)
      throw new Error(
        `Role "${roleName}" not found for org ${orgId} — expected a built-in org-level role with this name to already exist`,
      );
    return existing;
  }

  const id =
    existing ?? (await createCustomRole(sysApi, orgId, role as CustomRole));
  updateJsonCache<string>(roleCacheFile, (cache) => ({
    ...cache,
    [cacheKey]: id,
  }));
  return id;
}

// --------------------------------
// Membership helpers
// --------------------------------
interface UserSummary {
  id: number;
}

async function isOrgMember(
  sysApi: APIRequestContext,
  orgId: string,
  userId: string,
): Promise<boolean> {
  const res = await sysApi.get(`${API_URL}/users`, {
    params: {
      organizationId: orgId,
      includeArchived: "false",
      includeServiceAccounts: "false",
      includeTestAccounts: "true",
    },
  });
  if (!res.ok())
    throw new Error(
      `Check org membership failed for user ${userId} org ${orgId} (${res.status()}): ${await res.text()}`,
    );
  return ((await res.json()) as UserSummary[]).some(
    (u) => String(u.id) === userId,
  );
}

async function isProjectMember(
  sysApi: APIRequestContext,
  projectId: string,
  userId: string,
): Promise<boolean> {
  const res = await sysApi.get(`${API_URL}/users`, {
    params: {
      projectId,
      includeArchived: "false",
      includeServiceAccounts: "false",
      includeTestAccounts: "true",
    },
  });
  if (!res.ok())
    throw new Error(
      `Check project membership failed for user ${userId} project ${projectId} (${res.status()}): ${await res.text()}`,
    );
  return ((await res.json()) as UserSummary[]).some(
    (u) => String(u.id) === userId,
  );
}

async function addUserToOrg(
  sysApi: APIRequestContext,
  orgId: string,
  userId: string,
): Promise<void> {
  if (await isOrgMember(sysApi, orgId, userId)) return;
  const res = await sysApi.post(`${API_URL}/organizations/${orgId}/user`, {
    params: { userId, isAdmin: "false" },
  });
  if (!res.ok())
    throw new Error(
      `Add user ${userId} to org ${orgId} failed (${res.status()}): ${await res.text()}`,
    );
}

async function addUserToProject(
  sysApi: APIRequestContext,
  orgId: string,
  projectId: string,
  userId: string,
  isProjectAdmin: boolean,
  roleId?: string,
): Promise<void> {
  if (await isProjectMember(sysApi, projectId, userId)) return;
  const params: Record<string, string> = {
    userId,
    isProjectAdmin: String(isProjectAdmin),
  };
  if (roleId) params.roleId = roleId;
  const res = await sysApi.post(
    `${API_URL}/organizations/${orgId}/projects/${projectId}/members`,
    { params },
  );
  if (!res.ok())
    throw new Error(
      `Add user ${userId} to project ${projectId} failed (${res.status()}): ${await res.text()}`,
    );
}

// --------------------------------
// Admin grant endpoints
// --------------------------------
async function grantSysAdmin(
  sysApi: APIRequestContext,
  userId: string,
): Promise<void> {
  const res = await sysApi.patch(`${API_URL}/users/${userId}/admin`, {
    params: { isAdmin: "true" },
  });
  if (!res.ok())
    throw new Error(
      `Grant sysAdmin failed for user ${userId} (${res.status()}): ${await res.text()}`,
    );
}

async function setOrgAdminStatus(
  sysApi: APIRequestContext,
  orgId: string,
  userId: string,
  isAdmin: boolean,
): Promise<void> {
  const res = await sysApi.put(`${API_URL}/organizations/${orgId}/admin`, {
    params: { userId, isAdmin: String(isAdmin) },
  });
  if (!res.ok())
    throw new Error(
      `Set org admin status failed for user ${userId} org ${orgId} (${res.status()}): ${await res.text()}`,
    );
}

async function setProjectAdminStatus(
  sysApi: APIRequestContext,
  orgId: string,
  projectId: string,
  userId: string,
  isAdmin: boolean,
): Promise<void> {
  const res = await sysApi.put(
    `${API_URL}/organizations/${orgId}/projects/${projectId}/members`,
    { params: { userId, isProjectAdmin: String(isAdmin) } },
  );
  if (!res.ok())
    throw new Error(
      `Set project admin status failed for user ${userId} project ${projectId} (${res.status()}): ${await res.text()}`,
    );
}

// --------------------------------
// assignRole
// --------------------------------
async function assignRole(
  sysApi: APIRequestContext,
  userId: string,
  provision: NonNullable<TestAccount["provision"]>,
  orgId: string,
  projectId?: string,
): Promise<void> {
  if (provision.role && !provision.project) {
    throw new Error(
      `assignRole: provision has a "role" set but no "project" — roles are only assignable ` +
        `via project membership. Add a "project" to this account's provision, or remove "role" ` +
        `if this account only needs org-level access.`,
    );
  }

  if (provision.role && provision.isProjectAdmin) {
    throw new Error(
      `assignRole: provision has both "role" and "isProjectAdmin" set for user ${userId} — ` +
        `project admins are granted access via admin status, not a permission-bearing role. ` +
        `Remove "role" from this account's provision.`,
    );
  }

  await addUserToOrg(sysApi, orgId, userId);
  if (provision.isOrgAdmin) {
    await setOrgAdminStatus(sysApi, orgId, userId, true);
  }

  if (provision.project) {
    if (!projectId)
      throw new Error(
        `assignRole: provision.project "${provision.project.name}" set but no projectId resolved for user ${userId}`,
      );

    if (!provision.isProjectAdmin && !provision.role) {
      throw new Error(
        `assignRole: project membership requested for "${provision.project.name}" but neither ` +
          `isProjectAdmin nor role was set — nothing to assign for user ${userId}.`,
      );
    }

    if (provision.isProjectAdmin) {
      await addUserToProject(sysApi, orgId, projectId, userId, true);
      await setProjectAdminStatus(sysApi, orgId, projectId, userId, true);
    } else {
      const roleId = await resolveProjectUserRoleId(
        sysApi,
        orgId,
        provision.role!,
      );
      await addUserToProject(sysApi, orgId, projectId, userId, false, roleId);
    }
  }
}

// --------------------------------
// Account credentials
// --------------------------------
async function upsertTestAccountCreds(
  sysApi: APIRequestContext,
  accountName: string,
): Promise<{ apiKey: string; apiSecret: string; userId: string }> {
  const userRes = await sysApi.post(
    `${API_URL}/users/test?name=${accountName}`,
  );
  if (!userRes.ok())
    throw new Error(
      `Create test user failed for ${accountName} (${userRes.status()}): ${await userRes.text()}`,
    );
  const userId = (await userRes.json()).id;
  const keyRes = await sysApi.post(`${API_URL}/oauth/keys/test/${userId}`);
  if (!keyRes.ok())
    throw new Error(
      `Create key failed for ${accountName} (${keyRes.status()}): ${await keyRes.text()}`,
    );
  const key = await keyRes.json();
  return { apiKey: key.apiKey, apiSecret: key.apiSecret, userId };
}

async function ensureAccount(account: TestAccount): Promise<void> {
  if (process.env["NEXT_PUBLIC_DISABLE_FRONTEND_AUTHENTICATION"] == "true") {
    throw new Error(`Frontend & Backend Authentication must be enabled`);
  }

  // The literal env-backed sysAdmin uses env-var creds, never the test-user-creation API
  if (account.isSysAdmin && account.name === "sysAdmin") {
    await getSysApi();
    return;
  }

  const cache = readJsonCache<TestUserCacheEntry>(testUserCacheFile);
  const cached = cache[account.name];

  if (cached?.apiKey && cached?.apiSecret) {
    const verifyApi = await request.newContext();
    try {
      const jwt = await generateJwt(verifyApi, cached.apiKey, cached.apiSecret);
      const { email } = await fetchCurrentUser(verifyApi, jwt);
      await saveStorageState(account.name, jwt, email);
      return;
    } catch (err) {
      console.warn(
        `Cached credentials for "${account.name}" no longer valid, re-provisioning: ${err}`,
      );
    } finally {
      await verifyApi.dispose();
    }
  }

  const sysApi = await getSysApi();
  const { apiKey, apiSecret, userId } = await upsertTestAccountCreds(
    sysApi,
    account.name,
  );

  const provisionApi = await request.newContext();
  let email: string;
  try {
    const jwt = await generateJwt(provisionApi, apiKey, apiSecret);
    ({ email } = await fetchCurrentUser(provisionApi, jwt));
    await saveStorageState(account.name, jwt, email);
  } finally {
    await provisionApi.dispose();
  }

  if (account.isSysAdmin) {
    await grantSysAdmin(sysApi, userId);
  }

  let organizationId: string | undefined;
  let projectId: string | undefined;

  if (account.provision) {
    organizationId = account.provision.org
      ? await ensureOrg(account.provision.org)
      : undefined;
    projectId = account.provision.project
      ? await ensureProject(account.provision.project)
      : undefined;
    if (organizationId)
      await assignRole(
        sysApi,
        userId,
        account.provision,
        organizationId,
        projectId,
      );
  }

  const entry: TestUserCacheEntry = {
    email,
    organizationId,
    projectId,
    userId,
    apiKey,
    apiSecret,
  };
  updateJsonCache<TestUserCacheEntry>(testUserCacheFile, (cache) => ({
    ...cache,
    [account.name]: entry,
  }));
}

// --------------------------------
// Entry point — runs once before any test worker starts.
// --------------------------------
setup("provision all test accounts", async () => {
  for (const account of TEST_ACCOUNTS) {
    await ensureAccount(account);
  }
});
