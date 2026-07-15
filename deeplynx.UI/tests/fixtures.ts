// tests/fixtures.ts
import { test as base, BrowserContext, Page } from '@playwright/test';
import { authFile, TestAccount } from './deeplynx-config';

type Fixtures = {
  actAs: (account: TestAccount) => Promise<Page>;
};

export const test = base.extend<Fixtures>({
  actAs: async ({ browser }, use) => {
    const contexts: BrowserContext[] = [];
    await use(async (account) => {
      const context = await browser.newContext({ storageState: authFile(account.name) });
      contexts.push(context);
      return context.newPage();
    });
    await Promise.all(contexts.map(c => c.close()));
  },
});

export { expect } from '@playwright/test';