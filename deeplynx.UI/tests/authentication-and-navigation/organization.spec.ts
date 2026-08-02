import { sysAdmin, ORGS, PROJECTS } from "../deeplynx-config";
import { test, expect } from "../fixtures";

test.describe("Organizations", () => {
  test.use({
    actingUser: sysAdmin,
    actingOrg: ORGS.orgA,
  });

  test("user is automatically assigned an organization on startup", async ({
    page,
  }) => {
    // The header has the org name in a heading inside the dropdown trigger
    const orgDropdown = page.getByRole('button', { name: 'Organization' });
    const orgName = orgDropdown.getByRole('heading');
    await expect(orgName).toBeVisible();
    await expect(orgName).not.toHaveText("No Organization");
  });

  test("banner has an Organization dropdown label", async ({ page }) => {
    const orgDropdown = page.getByRole('button', { name: 'Organization' });
    const orgLabel = orgDropdown.getByText('Organization', { exact: true });
    await expect(orgLabel).toBeVisible();
  });

  test("clicking the Organization dropdown shows the current organization", async ({
    page,
  }) => {
    const dropdownTrigger = page.getByRole('button', { name: 'Organization' });
    await dropdownTrigger.click();

    // Verifies the box is open
    await expect(page.getByRole('listitem').filter({ hasText: 'Switch Organization' })).toBeVisible();

    // There should be a "Current" badge next to the active organization
    await expect(page.getByText(/Current$/)).toBeVisible();
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

    const dropdownContent = page.getByRole('link', { name: 'View All Organizations' });
    await expect(dropdownContent).toBeVisible();
    await dropdownContent.click();

    await expect(page).toHaveURL('/select-org');
  });
});
