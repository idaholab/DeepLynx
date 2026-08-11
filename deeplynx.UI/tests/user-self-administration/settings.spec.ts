import { test, expect } from "../fixtures";
import { sysAdmin, ORGS, PROJECTS } from "../deeplynx-config";

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
    await expect(page.getByText("Name")).toBeVisible();
    await expect(page.getByText("Email")).toBeVisible();
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
});
