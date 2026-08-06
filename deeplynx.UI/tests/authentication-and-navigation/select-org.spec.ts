import { test, expect } from "../fixtures";
import { sysAdmin, ORGS, PROJECTS } from "../deeplynx-config";

test.describe("Select Organization", () => {
  test.use({
    actingUser: sysAdmin,
    actingOrg: ORGS.orgA,
  });

  test("banner has an Organization dropdown label", async ({ page }) => {
    const orgDropdown = page.getByRole('button', { name: 'Organization' });
    const orgLabel = orgDropdown.getByText('Organization', { exact: true });
    await expect(orgLabel).toBeVisible();
  });

  test("clicking the Organization dropdown shows the current organization", async ({
    page,
  }) => {
    const dropdownTrigger = page.getByRole('button', { name: 'Organization' })
    await dropdownTrigger.click();

    await expect(page.getByRole('listitem').filter({ hasText: 'Switch Organization' })).toBeVisible();
  });

  test("Organization dropdown has a button to view all organizations", async ({
    page,
  }) => {
    const dropdownTrigger = page.getByRole('button', { name: 'Organization' });
    await dropdownTrigger.click();

    const dropdownContent = page.getByRole('link', { name: 'View All Organizations' });
    await expect(dropdownContent).toBeVisible();
  });

  test('Clicking the View All Organizations button opens Select Org', async ({
    page
  }) => {
    const dropdownTrigger = page.getByRole('button', { name: 'Organization' });
    await dropdownTrigger.click();
    await page.getByRole('link', { name: 'View All Organizations' }).click();
    await page.waitForLoadState("networkidle");
    await expect(page).toHaveURL('/select-org');
  });
});