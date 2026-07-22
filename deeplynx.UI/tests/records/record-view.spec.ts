import { test, expect } from "../fixtures";
import { sysAdmin } from '../deeplynx-config';
import { seedSession } from "../helpers/seed";

test.use({ actingUser: sysAdmin, actingOrg: 'PW Org A', actingProject: 'PW Project X' });

test.describe("Add Record Modal", () => {
  test.beforeEach(async ({ page }) => {
    // Open the "Add a Record" modal from the landing page
    await page.getByRole('button', { name: 'Record' }).click();
    const modal = page.getByRole('dialog');
    await expect(modal).toBeVisible();
  });

  test("modal displays 'Add a record' heading", async ({ page }) => {
    const modal = page.getByRole('dialog');
    await expect(
      modal.getByRole("heading", { name: "Add a record" }),
    ).toBeVisible();
  });

  test("modal has a project selector dropdown", async ({ page }) => {
    const modal = page.getByRole('dialog');
    // The project select is the first combobox in the modal
    const projectSelect = modal.getByRole("combobox").first();
    await expect(projectSelect).toBeVisible();
  });

  test("modal has a data source selector dropdown", async ({ page }) => {
    const modal = page.getByRole('dialog');
    // The data source select is the second combobox in the modal
    const dsSelect = modal.getByRole("combobox").nth(1);
    await expect(dsSelect).toBeVisible();
  });

  test("modal has Name, Original ID, Description, and Properties fields", async ({
    page,
  }) => {
    const modal = page.getByRole('dialog');
    await expect(modal.getByRole('textbox', { name: 'Name' })).toBeVisible();
    await expect(modal.getByRole('textbox', { name: 'Original ID' })).toBeVisible();
    await expect(modal.getByRole('textbox', { name: 'Description' })).toBeVisible();
    await expect(
      modal.getByRole('textbox', { name: 'Properties. Example: { "key1' }),
    ).toBeVisible();
  });

  test("modal has Cancel and Save buttons", async ({ page }) => {
    const modal = page.getByRole('dialog');
    await expect(
      modal.getByRole("button", { name: "Cancel" }),
    ).toBeVisible();
    await expect(
      modal.getByRole("button", { name: "Save" }),
    ).toBeVisible();
  });
});
