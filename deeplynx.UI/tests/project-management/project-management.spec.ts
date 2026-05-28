import { test, expect } from "@playwright/test";
import { seedAndNavigateToProject } from "../helpers/seed";

test.describe("Project Management", () => {
  test.beforeEach(async ({ page }) => {
    await seedAndNavigateToProject(page);
    // Navigate to Project Management via sidebar
    await page.locator("aside a", { hasText: "Project Settings" }).click();
    await page.waitForURL(/\/project_management\/\d+/);
    // Wait for the heading to confirm client-side render is done
    await expect(
      page.getByRole("heading", { name: "Project Management" }),
    ).toBeVisible();
  });

  test("Project Management page renders with heading", async ({ page }) => {
    await expect(
      page.getByRole("heading", { name: "Project Management" }),
    ).toBeVisible();
  });

  test("Managing settings subtitle is visible", async ({ page }) => {
    await expect(
      page.getByText(/Managing settings for project/i),
    ).toBeVisible();
  });

  test("Users tab is visible", async ({ page }) => {
    // Tabs are rendered as <a class="tab"> elements
    await expect(
      page.locator("a.tab", { hasText: "Users" }).first(),
    ).toBeVisible();
  });

  test("Roles & Permissions tab is visible", async ({ page }) => {
    await expect(
      page.locator("a.tab", { hasText: "Roles & Permissions" }),
    ).toBeVisible();
  });

  test("Data Sources tab is visible", async ({ page }) => {
    await expect(
      page.locator("a.tab", { hasText: "Data Sources" }),
    ).toBeVisible();
  });

  test("Tags & Sensitivity Labels tab is visible", async ({ page }) => {
    await expect(
      page.locator("a.tab", { hasText: "Tags & Sensitivity Labels" }),
    ).toBeVisible();
  });

  test("Settings tab is visible", async ({ page }) => {
    await expect(
      page.locator("a.tab", { hasText: "Settings" }),
    ).toBeVisible();
  });

  test("clicking Data Sources tab shows data source content", async ({
    page,
  }) => {
    await page.locator("a.tab", { hasText: "Data Sources" }).click();
    // After clicking the tab, it should become active
    await expect(
      page.locator("a.tab.tab-active", { hasText: "Data Sources" }),
    ).toBeVisible();
  });
});
