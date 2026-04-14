import { test, expect } from "@playwright/test";
import { seedAndNavigateToProject } from "../helpers/seed";

test.describe("Project Insight", () => {
  test.beforeEach(async ({ page }) => {
    await seedAndNavigateToProject(page);
    // Navigate to Project Insight via sidebar
    await page.locator("aside a", { hasText: "Insight" }).click();
    await page.waitForURL(/\/project_insight/);
    // Wait for the heading to confirm client-side render is done.
    // The Project Insight page has multiple async gates (org + project context
    // + record loading) so it needs a longer timeout under server load.
    await expect(
      page.getByRole("heading", { name: /Project Insight/ }),
    ).toBeVisible({ timeout: 15000 });
  });

  test("Project Insight page renders with heading", async ({ page }) => {
    await expect(
      page.getByRole("heading", { name: /Project Insight/ }),
    ).toBeVisible();
  });

  test("Embedded Library tab is visible", async ({ page }) => {
    await expect(page.getByText("Embedded Library")).toBeVisible();
  });

  test("Need Embedding tab is visible", async ({ page }) => {
    await expect(page.getByText("Need Embedding")).toBeVisible();
  });

  test("Select Filters button is visible", async ({ page }) => {
    await expect(
      page.getByRole("button", { name: /Select Filters/ }),
    ).toBeVisible();
  });

  test("search input is visible", async ({ page }) => {
    await expect(
      page.getByPlaceholder(/Search embedded files/),
    ).toBeVisible();
  });
});
