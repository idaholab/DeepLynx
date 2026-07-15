// tests/fixtures.ts
import { test as base, BrowserContext, Page } from '@playwright/test';
import {
  authFile,
  TestAccount,
  selectOrganization,
  selectProject,
} from './deeplynx-config';

type Fixtures = {
  actAs: (account: TestAccount) => Promise<Page>; // returns a logged in page for the provided test account.
  actingUser: TestAccount; // the account a given test should run as.
  actingOrg: string; // an optional override to force a specific organization context (needed for accounts like SysAdmin that aren't tied to one org).
  page: Page; // this overrides Playwright's built-in page fixture.
};

export const test = base.extend<Fixtures>({
  // actAs launches a new isolated browser context with the specified account's saved login session
  actAs: async ({ browser }, use) => {
    // context is tracked
    const contexts: BrowserContext[] = [];
    await use(async (account) => {
      const context = await browser.newContext({ storageState: authFile(account.name) });
      contexts.push(context);
      // returns a new page in that context
      return context.newPage();
    });
    await Promise.all(contexts.map((c) => c.close()));
  },

  // forces every test file to explicitly declare actingUser
  actingUser: [undefined as unknown as TestAccount, { option: true }],
  actingOrg: [undefined as unknown as string, { option: true }],

  // override logic of Playwright's Page fixture
  page: async ({ actAs, actingUser, actingOrg }, use) => {
    if (!actingUser) {
      throw new Error(
        'No actingUser declared. Add `test.use({ actingUser: <account> })` ' +
        'to this file/describe block before using `page`.',
      );
    }

    const page = await actAs(actingUser);

    if (actingUser.provision?.org) {
      // accounts always act in their own configured org
      await selectOrganization(page, actingUser);
      // if no org is configured acting org must be defined
    } else if (actingOrg) {
      await selectOrganization(page, actingUser, actingOrg);
    } else {
      throw new Error(
        `"${actingUser.name}" has no provisioned org. Add ` +
        `\`test.use({ actingOrg: <org name> })\` alongside actingUser.`,
      );
    }

    // for a specific project ensure the the project session is set
    if (actingUser.provision?.project) {
      await selectProject(page, actingUser);
    }

    // start at the root (will be redirected to the org select if no org session is set)
    await page.goto('/', { waitUntil: 'domcontentloaded' });
    
    // hands the setup page to the test
    await use(page);
  },
});

export { expect } from '@playwright/test';