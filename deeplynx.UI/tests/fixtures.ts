// tests/fixtures.ts
import { test as base, BrowserContext, Page, APIRequestContext } from '@playwright/test';
import { authFile, TestAccount, TestOrg, TestProject, selectOrganization, selectProject } from './deeplynx-config';
import { ensureAccount, ensureOrg, ensureProject, getApiContext } from './provisioning';

type Fixtures = {
  actAs: (account: TestAccount) => Promise<Page>;
  actingUser: TestAccount;
  actingOrg: TestOrg;
  actingProject: TestProject;
  page: Page;
  request: APIRequestContext;
};

export const test = base.extend<Fixtures>({
  actAs: async ({ browser }, use) => {
    const contexts: BrowserContext[] = [];
    await use(async (account) => {
      await ensureAccount(account); // provisions + writes authFile if not already done
      const context = await browser.newContext({ storageState: authFile(account.name) });
      contexts.push(context);
      return context.newPage();
    });
    await Promise.all(contexts.map((c) => c.close()));
  },

  actingUser: [undefined as unknown as TestAccount, { option: true }],
  actingOrg: [undefined as unknown as TestOrg, { option: true }],
  actingProject: [undefined as unknown as TestProject, { option: true }],

  // Overrides Playwright's built-in `request` fixture. The stock fixture is an
  // anonymous, unauthenticated APIRequestContext — any direct backend calls
  // made with it (as several helpers in upload_center.spec.ts do) go out
  // without an Authorization header. This override makes `request` resolve
  // to an APIRequestContext authenticated as whichever `actingUser` the test
  // declared, so existing call sites don't need to change.
  //
  // sysAdmin's context is a shared, module-level singleton (see getSysApi()
  // in provisioning.ts) reused across every test in the worker — it must
  // NOT be disposed here, or the next test to run gets a dead context.
  // Only dispose contexts this fixture created itself.
  request: async ({ actingUser }, use) => {
    if (!actingUser) {
      throw new Error(
        'No actingUser declared. Add `test.use({ actingUser: <account> })` ' +
        'to this file/describe block before using `request`.',
      );
    }

    const { context, shouldDispose } = await getApiContext(actingUser);
    await use(context);
    if (shouldDispose) {
      await context.dispose();
    }
  },

  page: async ({ actAs, actingUser, actingOrg, actingProject }, use) => {
    if (!actingUser) {
      throw new Error(
        'No actingUser declared. Add `test.use({ actingUser: <account> })` ' +
        'to this file/describe block before using `page`.',
      );
    }

    const page = await actAs(actingUser);

    // An explicit override always wins; otherwise fall back to whatever the
    // account itself is provisioned into.
    const org = actingOrg ?? actingUser.provision?.org;
    if (!org) {
      throw new Error(
        `"${actingUser.name}" has no provisioned org. Add ` +
        `\`test.use({ actingOrg: ORGS.someOrg })\` alongside actingUser.`,
      );
    }
    const orgId = await ensureOrg(org);
    await selectOrganization(page, orgId, org.name);

    const project = actingProject ?? actingUser.provision?.project;
    if (project) {
      const projectId = await ensureProject(project);
      await selectProject(page, projectId, project.name);

      // Org/project are already selected via cookie above, so land directly
      // on the project page rather than '/'. Tests used to have to click
      // through the project-select dropdown themselves to get here; now
      // that this fixture pre-selects the project, that manual UI step is
      // redundant and can hang (the already-active project may not appear
      // as a clickable option in the dropdown).
      await page.goto(`/project/${projectId}`, { waitUntil: 'domcontentloaded' });
    } else {
      await page.goto('/', { waitUntil: 'domcontentloaded' });
    }

    await page.locator('header .dropdown').waitFor();

    await use(page);
  },
});

export { expect, type Page, type APIRequestContext } from '@playwright/test';