import { test, expect } from "../fixtures";
import { sysAdmin, ORGS, PROJECTS } from "../deeplynx-config";

test.describe("Create Project Modal", () => {
  test.use({ actingUser: sysAdmin, actingOrg: ORGS.orgA, actingProject: PROJECTS.projectX})
  test.beforeEach(async ({ page }) => {
    await page.locator('[data-tour="create-project"]').click();
    await page.locator("dialog.modal.modal-open").waitFor();
  });

  test("modal has Name and Description inputs", async ({ page }) => {
    const modal = page.locator("dialog.modal.modal-open");
    await expect(modal.locator('input[placeholder="Name"]')).toBeVisible();
    await expect(
      modal.locator('textarea[placeholder="Description"]'),
    ).toBeVisible();
  });

  test("modal has Cancel and Create buttons", async ({ page }) => {
    const modal = page.locator("dialog.modal.modal-open");
    await expect(
      modal.getByRole("button", { name: "Cancel" }),
    ).toBeVisible();
    await expect(
      modal.getByRole("button", { name: "Create" }),
    ).toBeVisible();
  });

  test("Cancel button closes the modal", async ({ page }) => {
    const modal = page.locator("dialog.modal.modal-open");
    await modal.getByRole("button", { name: "Cancel" }).click();
    await expect(modal).not.toBeVisible();
  });
});

test.describe("Create Project Flow", () => {
  test.use({ actingUser: sysAdmin, actingOrg: ORGS.orgA, actingProject: PROJECTS.projectX})

  test("creating a project navigates to the project dashboard", async ({
    page,
  }) => {
    await page.locator('[data-tour="create-project"]').click();

    const modal = page.locator("dialog.modal.modal-open");
    await modal.locator('input[placeholder="Name"]').fill("Test Project");
    await modal
      .locator('textarea[placeholder="Description"]')
      .fill("A test project");
    await modal.getByRole("button", { name: "Create" }).click();

    await page.waitForURL(/\/project\/\d+/, { timeout: 30000 });
    await expect(page).toHaveURL(/\/project\/\d+/);
  });
});
