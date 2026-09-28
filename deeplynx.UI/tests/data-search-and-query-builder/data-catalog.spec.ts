import { sysAdmin, ORGS, PROJECTS } from "../deeplynx-config";
import { test, expect } from "../fixtures";

test.use({ actingUser: sysAdmin, actingOrg: ORGS.orgA});

test.describe("Data Catalog - All Records", () => {
  test.beforeEach(async ({ page }) => {
    await page.getByRole('link').nth(2).click();
    await expect(page.getByRole('heading', { name: 'All Records' })).toBeVisible();
  });

  test("Data Catalog page renders with heading", async ({ page }) => {
    await expect(
      page.getByText(/data catalog/i).first(),
    ).toBeVisible();
  });

  test("search bar renders with placeholder", async ({ page }) => {
    // exact: true — "Search" alone would also match "Search classes" and
    // "Search tags..." elsewhere on the page (strict mode violation).
    await expect(page.getByPlaceholder("Search", { exact: true })).toBeVisible();
  });

  test("All Records subheading is visible", async ({ page }) => {
    await expect(page.getByText("All Records")).toBeVisible();
  });

  test("project dropdown is visible", async ({ page }) => {
    // The project dropdown shows "All Your Projects" with a count
    await expect(
      page.getByRole('button', { name: /All your Projects/i }),
    ).toBeVisible();
  });
});