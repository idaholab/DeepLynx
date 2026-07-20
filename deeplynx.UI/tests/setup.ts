// tests/setup.ts
import { test as setup, request, APIRequestContext } from '@playwright/test';
import jsonWebToken from 'jsonwebtoken';
import fs from 'fs';
import { loadEnvConfig } from "@next/env";
import {
  ACTINGUSERS, ORGS, PROJECTS, DEFAULT_ROLE_NAME, TestAccount,
  authFile, testUserCacheFile, TestUserCacheEntry,
} from './deeplynx-config';

loadEnvConfig(process.cwd());

function requireEnv(name: string): string {
  const value = process.env[name];
  if (!value) throw new Error(`Missing required env var: ${name} — check your .env files`);
  return value;
}

const API_URL = requireEnv('BACKEND_BASE_URL');
const FRONTEND_URL = requireEnv('NEXTAUTH_URL');

// ---- Token + session helpers ------------------------------------------------
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

// We are making our own valid cookie with the JWT for UI testing with a test account
async function saveStorageState(account: TestAccount, jwt: string, email: string) {
  const url = new URL(FRONTEND_URL);
  const isSecure = url.protocol === 'https:'; // NextJS Cookie name changes based on https
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
      name: account.name,
      email: email,
      sub: email,
      organizationId: undefined,
    },
    requireEnv('NEXTAUTH_SECRET'),
    { expiresIn: maxAgeSeconds }
  );

  // create the state object and save it in the JSON file to cache for repeated tests
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
  fs.writeFileSync(authFile(account.name), JSON.stringify(state, null, 2));
}

// used to fetch the user email for the JWT claim, and (see resolveTestAccount)
// to confirm a set of credentials still authenticates before reusing them.
async function fetchCurrentUser(api: APIRequestContext, jwt: string): Promise<{ email: string }> {
  const res = await api.get(`${API_URL}/users/current`, {
    headers: { Authorization: `Bearer ${jwt}` },
  });
  if (!res.ok()) throw new Error(`GET /users/current failed (${res.status()}): ${await res.text()}`);
  const user = await res.json();
  return { email: user.email };
}

// Additional test account creation and setup done by system admin test account defined in the .env
async function upsertTestAccount(sysApi: APIRequestContext, account: TestAccount): Promise<{ apiKey: string; apiSecret: string; userId: string }> {
  const userRes = await sysApi.post(`${API_URL}/users/test?name=${account.name}`);
  if (!userRes.ok()) {
    throw new Error(`Create test user failed for ${account.name} (${userRes.status()}): ${await userRes.text()}`);
  }
  const user = await userRes.json();
  const userId = user.id;

  const keyRes = await sysApi.post(`${API_URL}/oauth/keys/test/${userId}`);
  if (!keyRes.ok()) {
    throw new Error(`Create key failed for ${account.name} (${keyRes.status()}): ${await keyRes.text()}`);
  }
  const key = await keyRes.json();
  return { apiKey: key.apiKey, apiSecret: key.apiSecret, userId };
}

function loadTestUserCache(): Record<string, TestUserCacheEntry> {
  try {
    return JSON.parse(fs.readFileSync(testUserCacheFile, 'utf8'));
  } catch {
    return {};
  }
}

async function resolveTestAccount(
  sysApi: APIRequestContext,
  anonApi: APIRequestContext,
  account: TestAccount,
  cache: Record<string, TestUserCacheEntry>,
): Promise<{ apiKey: string; apiSecret: string; userId: string; jwt: string; email: string }> {
  const cached = cache[account.name];

  if (cached?.apiKey && cached?.apiSecret && cached?.userId) {
    try {
      const jwt = await generateJwt(anonApi, cached.apiKey, cached.apiSecret);
      const { email } = await fetchCurrentUser(anonApi, jwt);
      return { apiKey: cached.apiKey, apiSecret: cached.apiSecret, userId: cached.userId, jwt, email };
    } catch (err) {
      console.warn(`Cached credentials for "${account.name}" no longer valid, re-provisioning: ${err}`);
    }
  }

  const { apiKey, apiSecret, userId } = await upsertTestAccount(sysApi, account);
  const jwt = await generateJwt(anonApi, apiKey, apiSecret);
  const { email } = await fetchCurrentUser(anonApi, jwt);
  return { apiKey, apiSecret, userId, jwt, email };
}

// ---- Org / project resolution (find-or-create by name, run once per org/project) --------
interface Scope {
  id: number;
  name: string;
}

async function findOrgId(sysApi: APIRequestContext, orgName: string): Promise<string | undefined> {
  const res = await sysApi.get(`${API_URL}/organizations?hideArchived=true`);
  if (!res.ok()) {
    throw new Error(`Fetch orgs failed for "${orgName}" (${res.status()}): ${await res.text()}`);
  }
  const orgs = (await res.json()) as Scope[];
  const org = orgs.find(o => o.name === orgName);
  return org ? String(org.id) : undefined;
}

async function createOrg(sysApi: APIRequestContext, orgName: string): Promise<string> {
  const res = await sysApi.post(`${API_URL}/organizations`, {
    data: { name: orgName, description: null, banner: null, requireSensitivityLabel: null },
  });
  if (!res.ok()) {
    throw new Error(`Create org failed for "${orgName}" (${res.status()}): ${await res.text()}`);
  }
  const created = (await res.json()) as Scope;
  return String(created.id);
}

async function ensureOrgId(sysApi: APIRequestContext, orgName: string): Promise<string> {
  const existing = await findOrgId(sysApi, orgName);
  return existing ?? (await createOrg(sysApi, orgName));
}

async function findProjectId(sysApi: APIRequestContext, projectName: string, orgId: string): Promise<string | undefined> {
  const res = await sysApi.get(`${API_URL}/organizations/${orgId}/projects?hideArchived=true`);
  if (!res.ok()) {
    throw new Error(`Fetch projects failed for "${projectName}" (${res.status()}): ${await res.text()}`);
  }
  const projects = (await res.json()) as Scope[];
  const project = projects.find(p => p.name === projectName);
  return project ? String(project.id) : undefined;
}

async function createProject(sysApi: APIRequestContext, projectName: string, orgId: string): Promise<string> {
  const res = await sysApi.post(`${API_URL}/organizations/${orgId}/projects`, {
    data: { name: projectName, description: null, abbreviation: null, banner: null, requireSensitivityLabel: null },
  });
  if (!res.ok()) {
    throw new Error(`Create project failed for "${projectName}" (${res.status()}): ${await res.text()}`);
  }
  const created = (await res.json()) as Scope;
  return String(created.id);
}

async function ensureProjectId(sysApi: APIRequestContext, projectName: string, orgId: string): Promise<string> {
  const existing = await findProjectId(sysApi, projectName, orgId);
  return existing ?? (await createProject(sysApi, projectName, orgId));
}

// ---- Project roles only supporting the default roles currently
interface Role {
  id: number;
  name: string;
}


const roleIdCache = new Map<string, string>(); // key: `${orgId}:${roleName}`

// Default roles are org-scoped, not project-scoped
async function findDefaultRoleId(
  sysApi: APIRequestContext,
  orgId: string,
  roleName: string,
): Promise<string | undefined> {
  const cacheKey = `${orgId}:${roleName}`;
  if (roleIdCache.has(cacheKey)) return roleIdCache.get(cacheKey)!;

  const res = await sysApi.get(`${API_URL}/organizations/${orgId}/roles`);
  if (!res.ok()) {
    throw new Error(`Fetch roles failed for org ${orgId} (${res.status()}): ${await res.text()}`);
  }

  const roles = (await res.json()) as Role[];
  const role = roles.find(r => r.name === roleName);
  if (!role) return undefined;

  const id = String(role.id);
  roleIdCache.set(cacheKey, id);
  return id;
}

async function requireRoleId(
  sysApi: APIRequestContext,
  orgId: string,
  roleName: string,
): Promise<string> {
  const id = await findDefaultRoleId(sysApi, orgId, roleName);
  if (!id) {
    throw new Error(
      `Role "${roleName}" not found for org ${orgId} — ` +
      `expected a built-in org-level role with this name to already exist`
    );
  }
  return id;
}

// ---- Membership checks will skip adding if already present
interface UserSummary {
  id: number;
}

async function isOrgMember(sysApi: APIRequestContext, orgId: string, userId: string): Promise<boolean> {
  const res = await sysApi.get(`${API_URL}/users`, {
    params: {
      organizationId: orgId,
      includeArchived: 'false',
      includeServiceAccounts: 'false',
      includeTestAccounts: 'true',
    },
  });
  if (!res.ok()) {
    throw new Error(`Check org membership failed for user ${userId} org ${orgId} (${res.status()}): ${await res.text()}`);
  }
  const users = (await res.json()) as UserSummary[];
  return users.some(u => String(u.id) === userId);
}

async function isProjectMember(sysApi: APIRequestContext, projectId: string, userId: string): Promise<boolean> {
  const res = await sysApi.get(`${API_URL}/users`, {
    params: {
      projectId,
      includeArchived: 'false',
      includeServiceAccounts: 'false',
      includeTestAccounts: 'true',
    },
  });
  if (!res.ok()) {
    throw new Error(`Check project membership failed for user ${userId} project ${projectId} (${res.status()}): ${await res.text()}`);
  }
  const users = (await res.json()) as UserSummary[];
  return users.some(u => String(u.id) === userId);
}

async function addUserToOrg(
  sysApi: APIRequestContext,
  orgId: string,
  userId: string,
  isAdmin: boolean,
): Promise<void> {
  const alreadyMember = await isOrgMember(sysApi, orgId, userId);
  if (alreadyMember) return;

  const res = await sysApi.post(`${API_URL}/organizations/${orgId}/user`, {
    params: { userId, isAdmin: String(isAdmin) },
  });
  if (!res.ok()) {
    throw new Error(`Add user ${userId} to org ${orgId} failed (${res.status()}): ${await res.text()}`);
  }
}

async function addUserToProject(
  sysApi: APIRequestContext,
  orgId: string,
  projectId: string,
  userId: string,
  isProjectAdmin: boolean,
  roleId?: string,
): Promise<void> {
  const alreadyMember = await isProjectMember(sysApi, projectId, userId);
  if (alreadyMember) return;

  const params: Record<string, string> = { userId, isProjectAdmin: String(isProjectAdmin) };
  if (roleId) params.roleId = roleId;

  const res = await sysApi.post(`${API_URL}/organizations/${orgId}/projects/${projectId}/members`, {
    params,
  });
  if (!res.ok()) {
    throw new Error(`Add user ${userId} to project ${projectId} failed (${res.status()}): ${await res.text()}`);
  }
}

// Every test account (except for a sysAdmin) gets an org membership row.
// If the account also provisions a project, it additionally gets a project membership
// row. project_admin uses isProjectAdmin=true; the role "user" resolves to the default user role
// The logic to assign custom roles is not yet implemented
async function assignRole(
  sysApi: APIRequestContext,
  userId: string,
  provision: NonNullable<TestAccount['provision']>,
  orgId?: string,
  projectId?: string,
): Promise<void> {
  if (!orgId) {
    throw new Error(`assignRole called without orgId for user ${userId} (provision.org: "${provision.org}")`);
  }

  const isOrgAdmin = provision.role === 'org_admin';
  await addUserToOrg(sysApi, orgId, userId, isOrgAdmin);

  if (provision.project) {
    if (!projectId) {
      throw new Error(`assignRole: provision.project "${provision.project}" set but no projectId resolved for user ${userId}`);
    }

    if (provision.role === 'project_admin') {
      await addUserToProject(sysApi, orgId, projectId, userId, true);
    } else {
      // 'user' — assign the built-in non-admin project role by id.
      const roleId = await requireRoleId(sysApi, orgId, DEFAULT_ROLE_NAME);
      await addUserToProject(sysApi, orgId, projectId, userId, false, roleId);
    }
  }
}

// Main setup orchestration
setup('provision and authenticate all TestAccounts', async () => {
  const sysKey = process.env.TEST_SYSADMIN_API_KEY;
  const sysSecret = process.env.TEST_SYSADMIN_SECRET;
  if (!sysKey || !sysSecret) {
    throw new Error('Missing TEST_SYSADMIN_API_KEY / TEST_SYSADMIN_SECRET — see testing README for details steps');
  }

  const anonApi = await request.newContext();

  // 1. Authenticate the sysadmin itself
  const sysJwt = await generateJwt(anonApi, sysKey, sysSecret);
  const { email: sysEmail } = await fetchCurrentUser(anonApi, sysJwt);
  const sysAdminAccount = ACTINGUSERS.find(a => a.name === 'sysAdmin')!;
  await saveStorageState(sysAdminAccount, sysJwt, sysEmail);

  // API context so the JWT is on all api calls
  const sysApi = await request.newContext({
    extraHTTPHeaders: { Authorization: `Bearer ${sysJwt}` },
  });

  // 2. Ensure every org/project from the config exists
  const orgIdByName = new Map<string, string>();
  for (const org of Object.values(ORGS)) {
    orgIdByName.set(org.name, await ensureOrgId(sysApi, org.name));
  }

  const projectIdByName = new Map<string, string>();
  for (const project of Object.values(PROJECTS)) {
    const orgId = orgIdByName.get(project.org);
    if (!orgId) {
      throw new Error(`Project "${project.name}" references unknown org "${project.org}" — check PROJECTS/ORGS in config`);
    }
    projectIdByName.set(project.name, await ensureProjectId(sysApi, project.name, orgId));
  }

  // 3. Each remaining account: reuse cached creds or create if needed
  const testUserCache: Record<string, TestUserCacheEntry> = loadTestUserCache();

  for (const account of ACTINGUSERS.filter(a => a.provision)) {
    const { apiKey, apiSecret, userId, jwt, email } = await resolveTestAccount(sysApi, anonApi, account, testUserCache);
    await saveStorageState(account, jwt, email);

    const orgId = account.provision!.org ? orgIdByName.get(account.provision!.org) : undefined;
    if (account.provision!.org && !orgId) {
      throw new Error(`Account "${account.name}" references unknown org "${account.provision!.org}"`);
    }
    const projectId = account.provision!.project ? projectIdByName.get(account.provision!.project) : undefined;
    if (account.provision!.project && !projectId) {
      throw new Error(`Account "${account.name}" references unknown project "${account.provision!.project}"`);
    }

    await assignRole(sysApi, userId, account.provision!, orgId, projectId);

    testUserCache[account.name] = { email, organizationId: orgId, projectId, userId, apiKey, apiSecret };
  }

  fs.mkdirSync('playwright/.auth', { recursive: true });
  fs.writeFileSync(testUserCacheFile, JSON.stringify(testUserCache, null, 2));
});