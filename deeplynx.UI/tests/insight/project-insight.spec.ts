import { test, expect } from "../fixtures";
import { sysAdmin } from "../deeplynx-config";

test.describe("Project Insight", () => {
  test.use({
    actingUser: sysAdmin,
    actingOrg: "PW Org A",
    actingProject: "PW Project X",
  });
  test.beforeEach(async ({ page }) => {
    // Navigate to Project Insight via sidebar
    await page.getByTestId("project-select").click();
    await page
      .getByRole("button", { name: "PW Project X", exact: true })
      .click();
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
    await expect(page.getByRole('button', { name: 'Embedded' })).toBeVisible();
  });

  test("Need Embedding tab is visible", async ({ page }) => {
    await expect(page.getByText("Need Embedding")).toBeVisible();
  });

  test("Filters button is visible", async ({ page }) => {
    await expect(
      page.getByRole('button', { name: 'Filters' }),
    ).toBeVisible();
  });

  test("search input is visible", async ({ page }) => {
    await expect(
      page.getByPlaceholder(/Search embedded files/),
    ).toBeVisible();
  });
});
