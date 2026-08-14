import { test as base, BrowserContext, Page, APIRequestContext, request } from '@playwright/test';
import fs from 'fs';
import { loadEnvConfig } from '@next/env';
import {
  authFile, TestAccount, TestOrg, TestProject, selectOrganization, selectProject,
  scopeCacheFile, testUserCacheFile, readJsonCache,
} from './deeplynx-config';
import { TEST_API_BASE_URL } from './api-url';
const API_URL = TEST_API_BASE_URL;

loadEnvConfig(process.cwd());

type Fixtures = {
  actAs: (account: TestAccount) => Promise<Page>; // returns a logged in page for the provided test account.
  actingUser: TestAccount; // the account a given test should run as.
  actingOrg: TestOrg; // an optional override to force a specific organization context (needed for accounts like SysAdmin that aren't tied to one org).
  actingProject: TestProject; // an optional override to force a specific project context (needed for accounts like SysAdmin/orgAdmin that aren't tied to one project).
  page: Page; // this overrides Playwright's built-in page fixture.
  request: APIRequestContext; // this overrides Playwright's built-in request fixture — always authenticated as actingUser.
};

function getCachedOrgId(orgName: string): string {
  const cache = readJsonCache<string>(scopeCacheFile);
  const id = cache[orgName];
  if (!id) {
    throw new Error(
      `Org "${orgName}" not found in scope cache. Make sure it's referenced by an entry in ` +
      `TEST_ACCOUNTS (deeplynx-config.ts), then run the setup project ` +
      `(\`npx playwright test --project=setup\`).`,
    );
  }
  return id;
}

function getCachedProjectId(projectName: string): string {
  const cache = readJsonCache<string>(scopeCacheFile);
  const id = cache[projectName];
  if (!id) {
    throw new Error(
      `Project "${projectName}" not found in scope cache. Make sure it's referenced by an entry in ` +
      `TEST_ACCOUNTS (deeplynx-config.ts), then run the setup project ` +
      `(\`npx playwright test --project=setup\`).`,
    );
  }
  return id;
}

function getCachedAccountEntry(accountName: string) {
  const cache = readJsonCache<{ apiKey?: string; apiSecret?: string }>(testUserCacheFile);
  const entry = cache[accountName];
  if (!entry) {
    throw new Error(
      `No cached credentials for "${accountName}". Add it to TEST_ACCOUNTS in deeplynx-config.ts ` +
      `and run the setup project (\`npx playwright test --project=setup\`).`,
    );
  }
  return entry;
}

let sysApiPromise: Promise<APIRequestContext> | undefined;

// Mirrors setup.ts's own sysAdmin JWT — kept intentionally tiny and
// separate rather than shared, since this is the one bit of "provisioning-ish"
// logic fixtures need (an authenticated request context), not account/org/
// role creation.
function getSysApi(): Promise<APIRequestContext> {
  if (!sysApiPromise) {
    sysApiPromise = (async () => {
      const apiKey = process.env.TEST_SYSADMIN_API_KEY!;
      const apiSecret = process.env.TEST_SYSADMIN_SECRET!;
      const anonApi = await request.newContext();
      try {
        const res = await anonApi.post(`${API_URL}/oauth/tokens`, { data: { apiKey, apiSecret, expirationMinutes: null } });
        if (!res.ok()) throw new Error(`Token generation failed (${res.status()}): ${await res.text()}`);
        const jwt = (await res.text()).trim();
        return request.newContext({ extraHTTPHeaders: { Authorization: `Bearer ${jwt}` } });
      } finally {
        await anonApi.dispose();
      }
    })();
  }
  return sysApiPromise;
}

async function getApiContext(
  account: TestAccount,
): Promise<{ context: APIRequestContext; shouldDispose: boolean }> {
  if (account.isSysAdmin) {
    return { context: await getSysApi(), shouldDispose: false };
  }

  const entry = getCachedAccountEntry(account.name);
  if (!entry.apiKey || !entry.apiSecret) {
    throw new Error(`getApiContext: no API credentials cached for account "${account.name}"`);
  }

  const anonApi = await request.newContext();
  const res = await anonApi.post(`${API_URL}/oauth/tokens`, {
    data: { apiKey: entry.apiKey, apiSecret: entry.apiSecret, expirationMinutes: null },
  });
  if (!res.ok()) throw new Error(`Token generation failed for "${account.name}" (${res.status()}): ${await res.text()}`);
  const jwt = (await res.text()).trim();
  await anonApi.dispose();

  const context = await request.newContext({ extraHTTPHeaders: { Authorization: `Bearer ${jwt}` } });
  return { context, shouldDispose: true };
}

// Points the page at a given org/project's UI session and navigates there.
export async function gotoScope(page: Page, org: TestOrg, project?: TestProject): Promise<void> {
  const orgId = getCachedOrgId(org.name);
  await selectOrganization(page, orgId, org.name);

  if (project) {
    const projectId = getCachedProjectId(project.name);
    await selectProject(page, projectId, project.name);
    await page.goto(`/project/${projectId}`, { waitUntil: 'domcontentloaded' });
  } else {
    await page.goto('/', { waitUntil: 'domcontentloaded' });
  }

  await page.locator('header .dropdown').waitFor();
}

export const test = base.extend<Fixtures>({
  // actAs launches a new isolated browser context with the specified account's saved login session
  actAs: async ({ browser }, use) => {
    const contexts: BrowserContext[] = [];
    await use(async (account) => {
      if (!fs.existsSync(authFile(account.name))) {
        throw new Error(
          `No auth file for "${account.name}". Add it to TEST_ACCOUNTS in deeplynx-config.ts ` +
          `and run the setup project (\`npx playwright test --project=setup\`).`,
        );
      }
      const context = await browser.newContext({ storageState: authFile(account.name) });
      contexts.push(context);
      return context.newPage();
    });
    await Promise.all(contexts.map((c) => c.close()));
  },

  // forces every test file to explicitly declare actingUser
  actingUser: [undefined as unknown as TestAccount, { option: true }],
  actingOrg: [undefined as unknown as TestOrg, { option: true }],
  actingProject: [undefined as unknown as TestProject, { option: true }],

  request: async ({ actingUser }, use) => {
    if (!actingUser) {
      throw new Error(
        'No actingUser declared. Add `test.use({ actingUser: <account> })` ' +
        'to this file/describe block before using `request`.',
      );
    }
    const { context, shouldDispose } = await getApiContext(actingUser);
    await use(context);
    if (shouldDispose) await context.dispose();
  },

  // override logic of Playwright's Page fixture
  page: async ({ actAs, actingUser, actingOrg, actingProject }, use) => {
    if (!actingUser) {
      throw new Error(
        'No actingUser declared. Add `test.use({ actingUser: <account> })` ' +
        'to this file/describe block before using `page`.',
      );
    }

    // explicit override always wins — assumes the account already has (or
    // doesn't need) backend membership in the overridden scope; see gotoScope.
    const org = actingOrg ?? actingUser.provision?.org;
    if (!org) {
      throw new Error(
        `"${actingUser.name}" has no provisioned org. Add ` +
        `\`test.use({ actingOrg: ORGS.someOrg })\` alongside actingUser.`,
      );
    }

    const project = actingProject ?? actingUser.provision?.project;
    if (project && project.org !== org) {
      throw new Error(
        `actingProject "${project.name}" belongs to org "${project.org.name}", not "${org.name}". ` +
        `Pass a matching actingOrg, or an actingProject that belongs to it.`,
      );
    }

    const page = await actAs(actingUser);
    await gotoScope(page, org, project);

    // hands the setup page to the test
    await use(page);
  },
});

export { expect, type Page, type APIRequestContext } from '@playwright/test';