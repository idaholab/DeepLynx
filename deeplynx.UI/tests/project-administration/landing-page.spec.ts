import { test, expect } from "@playwright/test";
import { seedSession } from "../helpers/seed";

test.describe("Landing Page", () => {
  test.beforeEach(async ({ page }) => {
    await seedSession(page);
    await page.goto("/", { waitUntil: "domcontentloaded" });
  });

  test("displays welcome message for local developer", async ({ page }) => {
    await expect(
      page.getByRole("heading", { name: /Welcome Back/ }),
    ).toBeVisible();
  });

  test("renders Your Projects card", async ({ page }) => {
    await expect(page.getByText("Your Projects")).toBeVisible();
  });

  test("Your Projects card has a button to create a project", async ({
    page,
  }) => {
    const createProjectButton = page.locator('[data-tour="create-project"]');
    await expect(createProjectButton).toBeVisible();
    await expect(createProjectButton).toContainText("Project");
  });

  test("Your Projects card has a button to add a record", async ({ page }) => {
    const addRecordButton = page.locator('[data-tour="add-record"]');
    await expect(addRecordButton).toBeVisible();
    await expect(addRecordButton).toContainText("Record");
  });

  test("clicking Create Project button opens the Create New Project modal", async ({
    page,
  }) => {
    await page.locator('[data-tour="create-project"]').click();

    const modal = page.locator("dialog.modal.modal-open");
    await expect(modal).toBeVisible();
    await expect(modal.getByText("Create New Project")).toBeVisible();
  });

  test("clicking Add Record button opens the Add a Record modal", async ({
    page,
  }) => {
    await page.locator('[data-tour="add-record"]').click();

    const modal = page.locator("dialog.modal-open");
    await expect(modal).toBeVisible();
    await expect(modal.getByText("Add a record")).toBeVisible();
  });
});
