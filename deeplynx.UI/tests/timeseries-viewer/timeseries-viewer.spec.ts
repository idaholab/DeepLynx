import { test, expect } from "@playwright/test";
import { seedAndNavigateToProject } from "../helpers/seed";

test.describe("Timeseries Viewer", () => {
  test.beforeEach(async ({ page }) => {
    await seedAndNavigateToProject(page);
    // Navigate to Timeseries Viewer via sidebar
    await page.locator("aside a", { hasText: "Timeseries Viewer" }).click();
    await page.waitForURL(/\/timeseries_viewer/);
    // Wait for the heading to confirm client-side render is done.
    // Needs a longer timeout because the page has async loading gates.
    await expect(
      page.getByRole("heading", { name: "Timeseries Viewer" }),
    ).toBeVisible({ timeout: 15000 });
  });

  test("Timeseries Viewer page renders with heading", async ({ page }) => {
    await expect(
      page.getByRole("heading", { name: "Timeseries Viewer" }),
    ).toBeVisible();
  });

  test("No file selected placeholder is visible initially", async ({
    page,
  }) => {
    await expect(page.getByText("No file selected")).toBeVisible();
  });

  test("Set Up tab is visible", async ({ page }) => {
    await expect(
      page.getByText("Set Up", { exact: true }),
    ).toBeVisible();
  });

  test("Data Check tab is visible", async ({ page }) => {
    await expect(page.getByText("Data Check")).toBeVisible();
  });

  test("Data Schema tab is visible", async ({ page }) => {
    await expect(page.getByText("Data Schema")).toBeVisible();
  });

  test("Plot Options section is visible in sidebar", async ({ page }) => {
    await expect(page.getByText("Plot Options")).toBeVisible();
  });
});
