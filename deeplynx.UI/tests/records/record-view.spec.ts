import { test, expect } from "@playwright/test";
import { seedSession } from "../helpers/seed";

test.describe("Add Record Modal", () => {
  test.beforeEach(async ({ page }) => {
    await seedSession(page);
    await page.goto("/", { waitUntil: "domcontentloaded" });
    // Open the "Add a Record" modal from the landing page
    await page.locator('[data-tour="add-record"]').click();
    const modal = page.locator("dialog.modal-open");
    await expect(modal).toBeVisible();
  });

  test("modal displays 'Add a record' heading", async ({ page }) => {
    const modal = page.locator("dialog.modal-open");
    await expect(
      modal.getByRole("heading", { name: "Add a record" }),
    ).toBeVisible();
  });

  test("modal has a project selector dropdown", async ({ page }) => {
    const modal = page.locator("dialog.modal-open");
    // The project select is the first combobox in the modal
    const projectSelect = modal.getByRole("combobox").first();
    await expect(projectSelect).toBeVisible();
  });

  test("modal has a data source selector dropdown", async ({ page }) => {
    const modal = page.locator("dialog.modal-open");
    // The data source select is the second combobox in the modal
    const dsSelect = modal.getByRole("combobox").nth(1);
    await expect(dsSelect).toBeVisible();
  });

  test("modal has Name, Original ID, Description, and Properties fields", async ({
    page,
  }) => {
    const modal = page.locator("dialog.modal-open");
    await expect(modal.getByPlaceholder("Name")).toBeVisible();
    await expect(modal.getByPlaceholder("Original ID")).toBeVisible();
    await expect(modal.getByPlaceholder("Description")).toBeVisible();
    await expect(
      modal.locator('textarea[placeholder*="Properties"]'),
    ).toBeVisible();
  });

  test("modal has Cancel and Save buttons", async ({ page }) => {
    const modal = page.locator("dialog.modal-open");
    await expect(
      modal.getByRole("button", { name: "Cancel" }),
    ).toBeVisible();
    await expect(
      modal.getByRole("button", { name: "Save" }),
    ).toBeVisible();
  });
});
