import { test, expect } from "@playwright/test";
import { seedSession } from "../helpers/seed";

test.describe("Select Organization", () => {
  test.beforeEach(async ({ page }) => {
    await seedSession(page);
    await page.goto("/select-org", { waitUntil: "domcontentloaded" });
  });

  

  test("banner has an Organization dropdown label", async ({ page }) => {
    const orgLabel = page
      .locator("header .dropdown")
      .getByText("Organization", { exact: true });
    await expect(orgLabel).toBeVisible();
  });

  test("clicking the Organization dropdown shows the current organization", async ({
    page,
  }) => {
    const dropdownTrigger = page.locator(
      'header .dropdown [role="button"]',
    );
    await dropdownTrigger.click();

    const dropdownContent = page.locator(
      "header .dropdown .dropdown-content",
    );
    await expect(dropdownContent).toBeVisible();

    // There should be a "Current" badge next to the active organization
    await expect(dropdownContent.getByText("Current")).toBeVisible();
  });

  test("Organization dropdown has a button to view all organizations", async ({
    page,
  }) => {
    const dropdownTrigger = page.locator(
      'header .dropdown [role="button"]',
    );
    await dropdownTrigger.click();

    const dropdownContent = page.locator(
      "header .dropdown .dropdown-content",
    );
    await expect(
      dropdownContent.getByText("View All Organizations"),
    ).toBeVisible();
  });

  test('Clicking the View All Organizations button opens Select Org', async ({ 
    page 
  }) => {
    await page.getByRole('link', { name: 'View All Organizations' }).click();
    await expect(page).toHaveURL('/select-org'); 
  });

  

});