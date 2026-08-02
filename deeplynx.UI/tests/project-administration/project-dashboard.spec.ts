import { test, expect } from "../fixtures";
import { sysAdmin, ORGS, PROJECTS } from "../deeplynx-config";

test.describe("Project Dashboard", () => {
  test.use({ actingUser: sysAdmin, actingOrg: ORGS.orgA, actingProject: PROJECTS.projectX})

  test.beforeEach(async ({ page }) => {
    await page.getByTestId("project-select").click();

    await page
      .getByRole("button", { name: "PW Project X", exact: true })
      .click();
  });

  test("dashboard URL matches /project/{id} pattern", async ({ page }) => {
    await expect(page).toHaveURL(/\/project\/\d+/);
  });

  test("dashboard displays the project header", async ({ page }) => {
    const header = page.locator('[data-tour="project-header"]');
    await expect(header).toBeVisible();
  });

  test("dashboard has a Project Overview widget", async ({ page }) => {
    await expect(page.getByText("Project Overview")).toBeVisible();
  });

  test("dashboard has a Team Members widget", async ({ page }) => {
    await expect(page.getByText("Team Members")).toBeVisible();
  });

  test("dashboard has a Data Catalog Overview widget", async ({ page }) => {
    const dataCatalogCard = page.locator('[data-tour="data-catalog-card"]');
    await expect(dataCatalogCard).toBeVisible();
    await expect(page.getByText("Data Catalog Overview")).toBeVisible();
  });
});
