// tests/fixtures.ts
import { test as base, BrowserContext, Page } from '@playwright/test';
import { authFile, TestAccount, TestOrg, TestProject, selectOrganization, selectProject } from './deeplynx-config';
import { ensureAccount, ensureOrg, ensureProject } from './provisioning';

type Fixtures = {
  actAs: (account: TestAccount) => Promise<Page>;
  actingUser: TestAccount;
  actingOrg: TestOrg;
  actingProject: TestProject;
  page: Page;
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
    }

    await page.goto('/', { waitUntil: 'domcontentloaded' });
    await page.locator('header .dropdown').waitFor();

    await use(page);
  },
});

export { expect, type Page, type APIRequestContext } from '@playwright/test';