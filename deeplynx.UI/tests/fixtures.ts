import { test as base, BrowserContext, Page, APIRequestContext } from '@playwright/test';
import {
  authFile, TestAccount, TestOrg, TestProject, selectOrganization, selectProject,
} from './deeplynx-config';
import { ensureAccount, ensureOrg, ensureProject, getApiContext } from './provisioning';

type Fixtures = {
  actAs: (account: TestAccount) => Promise<Page>;
  actingUser: TestAccount;
  actingOrg: TestOrg;
  actingProject: TestProject;
  page: Page;
  request: APIRequestContext;
};

export async function gotoScope(page: Page, org: TestOrg, project?: TestProject): Promise<void> {
  const orgId = await ensureOrg(org);
  await selectOrganization(page, orgId, org.name);

  if (project) {
    const projectId = await ensureProject(project);
    await selectProject(page, projectId, project.name);
    await page.goto(`/project/${projectId}`, { waitUntil: 'domcontentloaded' });
  } else {
    await page.goto('/', { waitUntil: 'domcontentloaded' });
  }

  await page.locator('header .dropdown').waitFor();
}

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

  page: async ({ actAs, actingUser, actingOrg, actingProject }, use) => {
    if (!actingUser) {
      throw new Error(
        'No actingUser declared. Add `test.use({ actingUser: <account> })` ' +
        'to this file/describe block before using `page`.',
      );
    }

    const org = actingOrg ?? actingUser.provision?.org;
    if (!org) {
      throw new Error(
        `"${actingUser.name}" has no provisioned org. Add ` +
        `\`test.use({ actingOrg: ORGS.someOrg })\` alongside actingUser.`,
      );
    }

    const page = await actAs(actingUser);
    await gotoScope(page, org, actingProject ?? actingUser.provision?.project);
    await use(page);
  },
});

export { expect, type Page, type APIRequestContext } from '@playwright/test';