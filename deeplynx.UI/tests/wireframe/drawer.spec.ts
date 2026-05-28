import { test, expect } from "@playwright/test";
import { seedSession } from "../helpers/seed";

test.describe("Wireframe - Drawer", () => {
  test.beforeEach(async ({ page }) => {
    await seedSession(page);
    await page.goto("/", { waitUntil: "domcontentloaded" });
  });

  test("drawer has a Project Dashboard button", async ({ page }) => {
    await expect(
      page.locator("aside a", { hasText: "Project Dashboard" }),
    ).toBeVisible();
  });

  test("drawer has an Upload Center button", async ({ page }) => {
    await expect(
      page.locator("aside a", { hasText: "Upload Center" }),
    ).toBeVisible();
  });

  test("drawer has a Timeseries Viewer button", async ({ page }) => {
    await expect(
      page.locator("aside a", { hasText: "Timeseries Viewer" }),
    ).toBeVisible();
  });

  test("drawer has a Project Settings button", async ({ page }) => {
    await expect(
      page.locator("aside").getByText("Project Settings"),
    ).toBeVisible();
  });

  test("drawer has an Insight button", async ({ page }) => {
    await expect(
      page.locator("aside a", { hasText: "Insight" }),
    ).toBeVisible();
  });

  test.describe("navigation", () => {
    // Navigation tests need more time: load home page, click a project,
    // wait for project page, then test the sidebar link.
    test.setTimeout(60_000);

    test.beforeEach(async ({ page }) => {
      // Sidebar links have pointer-events-none on the home page ("/").
      // Click a project from the main content table to navigate to a
      // project page where sidebar links become clickable.
      await page.locator('main a[href^="/project/"]').first().click();
      await page.waitForURL(/\/project\/\d+/);
    });

    test("Project Dashboard navigates to /project/{id}", async ({ page }) => {
      await page.locator("aside a", { hasText: "Project Dashboard" }).click();
      await expect(page).toHaveURL(/\/project\/\d+/);
    });

    test("Upload Center navigates to /upload_center", async ({ page }) => {
      await page.locator("aside a", { hasText: "Upload Center" }).click();
      await expect(page).toHaveURL(/\/upload_center/);
    });

    test("Timeseries Viewer navigates to /timeseries_viewer", async ({
      page,
    }) => {
      await page
        .locator("aside a", { hasText: "Timeseries Viewer" })
        .click();
      await expect(page).toHaveURL(/\/timeseries_viewer/);
    });

    test("Project Settings navigates to /project_management/{id}", async ({
      page,
    }) => {
      await page
        .locator("aside a", { hasText: "Project Settings" })
        .click();
      await expect(page).toHaveURL(/\/project_management\/\d+/);
    });

    test("Insight navigates to /project_insight", async ({ page }) => {
      await page.locator("aside a", { hasText: "Insight" }).click();
      await expect(page).toHaveURL(/\/project_insight/);
    });
  });
});
