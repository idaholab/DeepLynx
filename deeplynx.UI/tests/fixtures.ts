// tests/fixtures.ts
import { test as base, BrowserContext, Page } from '@playwright/test';
import {
  authFile,
  TestAccount,
  selectOrganization,
  selectProject,
} from './deeplynx-config';

type Fixtures = {
  actAs: (account: TestAccount) => Promise<Page>;

  // No default value. Every test file MUST declare
  // `test.use({ actingUser: <account> })` — there is no implicit fallback
  // account. This is intentional: silently defaulting to some account (even
  // sysAdmin) hides which identity a test is actually running as.
  actingUser: TestAccount;

  // Org to select in the UI. Required when `actingUser` has no
  // `provision.org` of its own (e.g. sysAdmin — not org-scoped by role, but
  // org-scoped pages still need *some* organizationSession set to render).
  // Declared per-file/describe: `test.use({ actingOrg: ORGS.orgA.name })`.
  // No default — accounts that ARE provisioned into an org use that org
  // automatically and don't need this set.
  actingOrg: string;

  page: Page;
};

export const test = base.extend<Fixtures>({
  actAs: async ({ browser }, use) => {
    const contexts: BrowserContext[] = [];
    await use(async (account) => {
      const context = await browser.newContext({ storageState: authFile(account.name) });
      contexts.push(context);
      return context.newPage();
    });
    await Promise.all(contexts.map((c) => c.close()));
  },

  // `undefined` default + explicit check in `page` below is what forces
  // every test file to declare this rather than silently inheriting one.
  actingUser: [undefined as unknown as TestAccount, { option: true }],
  actingOrg: [undefined as unknown as string, { option: true }],

  page: async ({ actAs, actingUser, actingOrg }, use) => {
    if (!actingUser) {
      throw new Error(
        'No actingUser declared. Add `test.use({ actingUser: <account> })` ' +
        'to this file/describe block before using `page`.',
      );
    }

    const page = await actAs(actingUser);

    if (actingUser.provision?.org) {
      // Provisioned accounts always act in their own configured org —
      // actingOrg is ignored here, since a test can't put a provisioned
      // account into an org it wasn't set up for.
      await selectOrganization(page, actingUser);
    } else if (actingOrg) {
      await selectOrganization(page, actingUser, actingOrg);
    } else {
      throw new Error(
        `"${actingUser.name}" has no provisioned org. Add ` +
        `\`test.use({ actingOrg: <org name> })\` alongside actingUser.`,
      );
    }

    if (actingUser.provision?.project) {
      await selectProject(page, actingUser);
    }

    await page.goto('/', { waitUntil: 'domcontentloaded' });
    await page.goto('/data_catalog/all_records', { waitUntil: 'domcontentloaded' });

    await use(page);
  },
});

export { expect } from '@playwright/test';