import { test, expect } from "@playwright/test";
import { seedSession } from "../helpers/seed";

test.describe("Data Catalog - All Records", () => {
  test.beforeEach(async ({ page }) => {
    await seedSession(page);
    await page.goto("/data_catalog/all_records", {
      waitUntil: "domcontentloaded",
    });
    // Wait for the heading to confirm page has loaded
    await expect(
      page.getByRole("heading", { name: "Data Catalog" }),
    ).toBeVisible();
  });

  test("Data Catalog page renders with heading", async ({ page }) => {
    await expect(
      page.getByRole("heading", { name: "Data Catalog" }),
    ).toBeVisible();
  });

  test("search bar renders with placeholder", async ({ page }) => {
    await expect(page.getByPlaceholder("Search")).toBeVisible();
  });

  test("All Records subheading is visible", async ({ page }) => {
    await expect(page.getByText("All Records")).toBeVisible();
  });

  test("list view button is visible", async ({ page }) => {
    const listViewBtn = page.locator('button[title="List view"]');
    await expect(listViewBtn).toBeVisible();
  });

  test("table view button is visible", async ({ page }) => {
    const tableViewBtn = page.locator('button[title="Table view"]');
    await expect(tableViewBtn).toBeVisible();
  });

  test("clicking table view button switches the view", async ({ page }) => {
    const tableViewBtn = page.locator('button[title="Table view"]');
    await expect(tableViewBtn).toBeVisible();
    await tableViewBtn.click();
    // Table view renders a <table> element; list view renders a <ul>.
    await expect(page.locator("table")).toBeVisible({ timeout: 10000 });
  });

  test("project dropdown is visible", async ({ page }) => {
    // The project dropdown shows "All Your Projects" with a count
    await expect(
      page.getByText(/All Your Projects/),
    ).toBeVisible();
  });
});
