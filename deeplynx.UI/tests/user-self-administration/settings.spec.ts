import { test, expect } from "../fixtures";
import { sysAdmin, ORGS, PROJECTS } from "../deeplynx-config";

const BASE_URL = 'http://localhost:5095/api/v1/';

test.describe("Settings Page", () => {
  test.use({ actingUser: sysAdmin, actingOrg: ORGS.orgA, actingProject: PROJECTS.projectX });
  test.beforeEach(async ({ page }) => {
    // Navigate to Settings via sidebar
    await page.getByRole('list').filter({ hasText: 'Use these IDs when using the' }).getByRole('button').click();
    await page.getByRole('link', { name: 'Settings', exact: true }).click();
    // Wait for the User Settings heading to confirm client-side render is done
    try {
        await expect(page.getByRole('heading', { name: 'User Settings' })).toBeVisible({ timeout: 15000 });
    } catch {
        await page.goto('localhost:3000/settings', {
            waitUntil: 'domcontentloaded',
            timeout: 10_000
        });
    }
  });

  test("Settings page renders with user name heading", async ({ page }) => {
    await expect(page.locator("h1").first()).toBeVisible();
  });

  test("User Settings section is visible", async ({ page }) => {
    await expect(page.getByText("User Settings")).toBeVisible();
  });

  test("Name and Email labels are displayed", async ({ page }) => {
    await expect(page.getByText("Name", { exact: true })).toBeVisible();
    await expect(page.getByText("Email", { exact: true })).toBeVisible();
  });

  test("Preferences section is visible", async ({ page }) => {
    await expect(page.getByText("Preferences")).toBeVisible();
  });

  test("Dark Mode toggle is visible", async ({ page }) => {
    await expect(page.getByText("Dark Mode")).toBeVisible();
  });

  test("API Keypairs section is visible", async ({ page }) => {
    await expect(page.getByText("API Keys")).toBeVisible();
  });

  test("Switch to dark mode", async ({ page }) => {
    const darkModeSelector = page.locator('div').filter({ hasText: /^Dark ModeToggle between light and dark themes$/ }).first();
    const html = page.locator('html');

    await expect(html).toHaveAttribute('data-theme', 'default');
    await darkModeSelector.locator('label').click();
    await expect(html).toHaveAttribute('data-theme', 'default-dark');
  });

  test("Switch to light mode", async ({ page }) => {
    const darkModeSelector = page.locator('div').filter({ hasText: /^Dark ModeToggle between light and dark themes$/ }).first();
    const html = page.locator('html');

    // start in dark mode
    await page.evaluate(() => { window.localStorage.setItem('dlx-theme-mode', 'dark'); });
    await page.reload({ waitUntil: 'domcontentloaded' });
    // Webkit navigates to the home page on reload
    // This try/catch checks to see if it's in the right spot, and gets there if not
    try {
      await expect(darkModeSelector).toBeVisible();
    } catch {
      await page.getByRole('list').filter({ hasText: 'Use these IDs when using the' }).getByRole('button').click();
      await page.getByRole('link', { name: 'Settings', exact: true }).click();
      await expect(darkModeSelector).toBeVisible();
    }

    await expect(html).toHaveAttribute('data-theme', 'default-dark');
    await darkModeSelector.locator('label').click();
    await expect(html).toHaveAttribute('data-theme', 'default');
  });

  test("Change language to español", async ({ page }) => {
    const languageSelector = page.getByText('LanguageChoose your preferred languageEnglishEspañol');
    await languageSelector.getByRole('combobox').selectOption('es');

    const lang = await page.evaluate(() => localStorage.getItem('lang'));
    expect(lang).toBe('es');
    await expect(page.getByRole('heading', { name: 'Configuración de usuario' })).toBeVisible();
  });

  test("Change language to english", async ({ page }) => {
    const languageSelector = page.getByText('IdiomaElige tu idioma preferidoEnglishEspañol');
    
    // start in spanish
    await page.evaluate(() => { window.localStorage.setItem('lang', 'es'); });
    await page.reload({ waitUntil: 'domcontentloaded' });
    try {
      await expect(languageSelector).toBeVisible();
    } catch {
      await page.getByRole('list').filter({ hasText: 'Usa estos identificadores al' }).getByRole('button').click();
      await page.getByRole('link', { name: 'Configuración', exact: true }).click();
      await expect(languageSelector).toBeVisible();
    }

    await languageSelector.getByRole('combobox').selectOption('en');

    const lang = await page.evaluate(() => localStorage.getItem('lang'));
    expect(lang).toBe('en');
    await expect(page.getByRole('heading', { name: 'User Settings' })).toBeVisible();
  });

  test.describe("Generate API key", () => {
    let key: string | undefined;
    test.afterEach(async ({ page }) => {
      if (!key) return;
      const row = page.locator('.flex.items-center.gap-3').filter({ has: page.locator('code', { hasText: key }) });
      await row.getByRole('button', { name: 'Delete API key' }).click();
    });

    test("create an API key", async ({ page }) => {
      const keyElement = page.getByText('Key:');
      const rows = page.locator('.space-y-3 > div');
      await expect(rows.getByText('1', { exact: true })).toBeVisible();
      const previousCount = await rows.count();
      
      await page.getByRole('button', { name: 'Generate New' }).click();
      await expect(page.getByText('API Keypair created')).toBeVisible();
      await expect(keyElement).toBeVisible();
      await expect(page.getByText('Secret:')).toBeVisible();
      const fullText = await keyElement.textContent();
      key = fullText?.replace('Key: ', '')
      await page.getByRole('button', { name: 'Dismiss' }).click();

      await expect(rows).toHaveCount(previousCount + 1);
      await expect(page.getByRole('main').getByText(`1${key}`, { exact: true })).toBeVisible();
    });
  });

  test.describe("Delete API key", () => {
    let key: string;
    test.beforeAll(async ({ page, request }) => {
      // create an API key for deleting
      const newKeyUrl = `${BASE_URL}oauth/keys`;
      const res = await request.post(newKeyUrl);
      if (!res.ok() && res.status() !== 409) {
        throw new Error(`Failed to create new API Key: ${res.status()}`);
      };
      await page.reload({ waitUntil: 'domcontentloaded' });
      const resJson = await res.json();
      key = resJson.apiKey;
    });

    test("delete an API key", async ({ page }) => {
      const rows = page.locator('.space-y-3 > div');
      await expect(rows.getByText('1', { exact: true })).toBeVisible();
      const previousCount = await rows.count();

      const row = page.locator('.flex.items-center.gap-3').filter({ has: page.locator('code', { hasText: key }) });
      await row.getByRole('button', { name: 'Delete API key' }).click();
      await expect(page.getByText('API Keypair deleted')).toBeVisible();
      await expect(page.getByText(key)).not.toBeVisible();
      await expect(rows).toHaveCount(previousCount - 1);    
      await expect(rows.getByText(String(previousCount))).not.toBeVisible();
    });
  });

  test.describe("Verify functionality of personal API key", () => {
    let key: string | undefined;
    let secret: string | undefined;
    test.beforeEach(async ({ page, request }) => {
      // create an API key for testing
      const newKeyUrl = `${BASE_URL}oauth/keys`;
      const res = await request.post(newKeyUrl);
      if (!res.ok() && res.status() !== 409) {
        throw new Error(`Failed to create new API Key: ${res.status()}`);
      };
      const resJson = await res.json();
      page.reload({ waitUntil: 'domcontentloaded' });
      key = resJson.apiKey;
      secret = resJson.apiSecret;
    });
    
    test.afterEach(async ({ page }) => {
      if (!key) return;
      await expect(page.getByText(key).first()).toBeVisible();
      const row = page.locator('.flex.items-center.gap-3').filter({ has: page.locator('code', { hasText: key }) });
      await row.getByRole('button', { name: 'Delete API key' }).click();
    });

    test("verify API key works", async ({ request }) => {
      // Create a JWT with the API key
      const newTokenUrl = `${BASE_URL}oauth/tokens`;
      const tokenRes = await request.post(newTokenUrl, { data: { ApiKey: key, ApiSecret: secret } });
      expect(tokenRes.ok()).toBeTruthy();
      const token = (await tokenRes.text()).trim();
      expect(token).toBeTruthy();
      expect(token.split('.')).toHaveLength(3);

      // Use the JWT and verify it works
      const orgsRes = await request.fetch(`${BASE_URL}organizations?hideArchived=true`, {
        headers: { Authorization: `Bearer ${token}` }
      });
      expect(orgsRes.ok()).toBeTruthy();
      const orgs = await orgsRes.json();
      expect(Array.isArray(orgs)).toBeTruthy();
      expect(orgs.length).toBeGreaterThan(0);

      for (const org of orgs) {
        expect(org).toMatchObject({
          id: expect.any(Number),
          name: expect.any(String),
          isArchived: expect.any(Boolean),
          defaultOrg: expect.any(Boolean)
        });
      };
      expect(orgs.some((org: any) => org.defaultOrg === true)).toBeTruthy();
    });
    
    test("Invalid secret is rejected", async ({ request }) => {
      const res = await request.post(`${BASE_URL}oauth/tokens`, {
        data: { ApiKey: key, ApiSecret: "wrong secret..." },
      });
      expect(res.ok()).toBeFalsy();
      expect(res.status()).toBe(401);
    });

    test("verify deleted API key no longer works", async ({ page, request }) => {
      // Delete the new api key, keep the values stored for reference
      if (!key) return;
      await expect(page.getByText(key).first()).toBeVisible();
      const row = page.locator('.flex.items-center.gap-3').filter({ has: page.locator('code', { hasText: key }) });
      await row.getByRole('button', { name: 'Delete API key' }).click();
      await expect(page.getByText(key)).not.toBeVisible();
      
      const res = await request.post(`${BASE_URL}oauth/tokens`, {
        data: {ApiKey: key, ApiSecret: secret},
      });
      expect(res.ok()).toBeFalsy();
      expect(res.status()).toBe(404);

      key = undefined;
      secret = undefined;
    });
  });
});
