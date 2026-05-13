import { test, expect } from "@playwright/test";
import { seedAndNavigateToProject } from "../helpers/seed";

test.describe("Roles & Permissions", () => {
  test.beforeEach(async ({ page }) => {
    await seedAndNavigateToProject(page);
    // Navigate to Project Settings via sidebar
    await page.locator("aside a", { hasText: "Project Settings" }).click();
    await page.waitForURL(/\/project_management\/\d+/);
    await expect(
      page.getByRole("heading", { name: "Project Management" }),
    ).toBeVisible({ timeout: 15000 });
    // Click the Roles & Permissions tab
    await page.locator("a.tab", { hasText: "Roles & Permissions" }).click();
    await expect(
      page.locator("a.tab.tab-active", { hasText: "Roles & Permissions" }),
    ).toBeVisible();
  });

  /* ------------------------------------------------------------------------ */
  /*                         Page Rendering                                   */
  /* ------------------------------------------------------------------------ */

  test.describe("Page rendering", () => {
    test("displays the page heading", async ({ page }) => {
      await expect(
        page.getByRole("heading", { name: "Project Roles & Permissions" }),
      ).toBeVisible();
    });

    test("displays the page description", async ({ page }) => {
      await expect(
        page.getByText(
          /View project-level roles and their permissions/i,
        ),
      ).toBeVisible();
    });

    test("displays the View Layout label", async ({ page }) => {
      await expect(page.getByText("View Layout:")).toBeVisible();
    });

    test("displays Split View and Matrix View toggle buttons", async ({
      page,
    }) => {
      await expect(
        page.getByRole("button", { name: "Split View" }),
      ).toBeVisible();
      await expect(
        page.getByRole("button", { name: "Matrix View" }),
      ).toBeVisible();
    });

    test("Split View is active by default", async ({ page }) => {
      const splitBtn = page.getByRole("button", { name: "Split View" });
      await expect(splitBtn).toHaveClass(/btn-primary/);
    });
  });

  /* ------------------------------------------------------------------------ */
  /*                         Split View Layout                                */
  /* ------------------------------------------------------------------------ */

  test.describe("Split View layout", () => {
    test("displays the Roles sidebar with role count", async ({ page }) => {
      await expect(
        page.locator(".card-title", { hasText: "Roles" }).first(),
      ).toBeVisible();
      // The role count label (e.g. "3 Total")
      await expect(page.getByText(/\d+ Total/)).toBeVisible();
    });

    test("displays the Create Role button", async ({ page }) => {
      await expect(
        page.getByRole("button", { name: "Create Role" }),
      ).toBeVisible();
    });

    test("displays standard roles (Admin, User)", async ({ page }) => {
      // Standard roles should be listed in the sidebar
      await expect(
        page.locator("button", { hasText: "Admin" }).first(),
      ).toBeVisible();
      await expect(
        page.locator("button", { hasText: "User" }).first(),
      ).toBeVisible();
    });

    test("first standard role is selected by default", async ({ page }) => {
      // The selected role has a highlighted left border
      const selectedRole = page.locator("button.border-l-primary").first();
      await expect(selectedRole).toBeVisible();
    });

    test("displays role details panel for selected role", async ({ page }) => {
      // The right panel should show the selected role name as a heading
      const roleHeading = page.locator(".card-title").filter({
        hasText: /^(Admin|User)$/,
      });
      await expect(roleHeading.first()).toBeVisible();
    });

    test("standard roles show STD badge in sidebar", async ({ page }) => {
      // Standard roles (Admin, User, Viewer) get a "STD" badge
      const stdBadges = page.locator(".badge-info", { hasText: "STD" });
      await expect(stdBadges.first()).toBeVisible();
    });

    test("standard roles display Standard Role badge in detail panel", async ({
      page,
    }) => {
      // When a standard role is selected, the detail panel shows "Standard Role"
      await expect(
        page.locator(".badge", { hasText: "Standard Role" }).first(),
      ).toBeVisible();
    });

    test("displays Source label for roles", async ({ page }) => {
      // Each role in the sidebar shows its source (translation: "Source: ")
      await expect(
        page.getByText(/Source:/).first(),
      ).toBeVisible();
    });

    test("displays the Permissions section header", async ({ page }) => {
      // Wait for permissions to load
      await expect(
        page.locator("h3", { hasText: "Permissions" }).first(),
      ).toBeVisible({ timeout: 15000 });
    });

    test("displays Edit Permissions button", async ({ page }) => {
      await expect(
        page.getByRole("button", { name: "Edit Permissions" }),
      ).toBeVisible({ timeout: 15000 });
    });

    test("displays Resource Permissions and Sensitivity Labels tabs", async ({
      page,
    }) => {
      // The inner permission tabs (use exact text to avoid matching
      // the outer "Tags & Sensitivity Labels" project management tab)
      await expect(
        page.locator("a.tab", { hasText: "Resource Permissions" }),
      ).toBeVisible({ timeout: 15000 });
      await expect(
        page.getByText("Sensitivity Labels", { exact: true }),
      ).toBeVisible();
    });

    test("displays permission categories with checkboxes", async ({
      page,
    }) => {
      // Wait for permissions to load, then check that at least one
      // permission checkbox is rendered
      await expect(
        page.locator("input.checkbox").first(),
      ).toBeVisible({ timeout: 15000 });
    });

    test("permission checkboxes are disabled in read-only mode", async ({
      page,
    }) => {
      // In read-only mode (default), checkboxes should be disabled
      await expect(
        page.locator("input.checkbox").first(),
      ).toBeDisabled({ timeout: 15000 });
    });

    test("standard role shows info alert about read-only permissions", async ({
      page,
    }) => {
      // Standard roles display an info alert
      await expect(
        page.locator(".alert-info").first(),
      ).toBeVisible({ timeout: 15000 });
    });

    test("clicking a different role selects it", async ({ page }) => {
      // Click the "User" role in the sidebar (role buttons contain Source: text)
      await page
        .locator("button", { hasText: /Source:/ })
        .filter({ hasText: "User" })
        .first()
        .click();
      // The right panel heading (h2) should now show "User"
      const detailPanel = page.locator(".flex-1.card");
      await expect(
        detailPanel.locator("h2.card-title", { hasText: "User" }),
      ).toBeVisible();
    });

    test("switching between Sensitivity Labels and Resource Permissions tabs works", async ({
      page,
    }) => {
      // Wait for permission tabs to appear
      await expect(
        page.locator("a.tab", { hasText: "Resource Permissions" }),
      ).toBeVisible({ timeout: 15000 });

      // Click Sensitivity Labels tab (use exact text to avoid the outer tab)
      await page
        .getByText("Sensitivity Labels", { exact: true })
        .click();
      await expect(
        page.locator("a.tab.tab-active").filter({
          hasText: /^Sensitivity Labels$/,
        }),
      ).toBeVisible();

      // Click back to Resource Permissions
      await page
        .locator("a.tab", { hasText: "Resource Permissions" })
        .click();
      await expect(
        page.locator("a.tab.tab-active", { hasText: "Resource Permissions" }),
      ).toBeVisible();
    });
  });

  /* ------------------------------------------------------------------------ */
  /*                         Matrix View Layout                               */
  /* ------------------------------------------------------------------------ */

  test.describe("Matrix View layout", () => {
    test.beforeEach(async ({ page }) => {
      // Switch to Matrix View
      await page.getByRole("button", { name: "Matrix View" }).click();
      await expect(
        page.getByRole("button", { name: "Matrix View" }),
      ).toHaveClass(/btn-primary/);
    });

    test("displays the Permission Matrix heading", async ({ page }) => {
      await expect(
        page.getByText("Permission Matrix", { exact: true }),
      ).toBeVisible();
    });

    test("displays the Edit Matrix button", async ({ page }) => {
      await expect(
        page.getByRole("button", { name: "Edit Matrix" }),
      ).toBeVisible({ timeout: 15000 });
    });

    test("displays a table with Permission column header", async ({
      page,
    }) => {
      await expect(page.locator("table")).toBeVisible({ timeout: 15000 });
      await expect(
        page.locator("table thead th").first(),
      ).toHaveText("Permission");
    });

    test("displays role names as column headers", async ({ page }) => {
      await expect(page.locator("table")).toBeVisible({ timeout: 15000 });
      // Standard roles should appear in the table header
      await expect(
        page.locator("thead span", { hasText: "Admin" }).first(),
      ).toBeVisible();
      await expect(
        page.locator("thead span", { hasText: "User" }).first(),
      ).toBeVisible();
    });

    test("displays permission category rows", async ({ page }) => {
      await expect(page.locator("table")).toBeVisible({ timeout: 15000 });
      // Category rows have a distinct bg and span across all columns
      const categoryRows = page.locator("tr.bg-base-200 td[colspan]");
      await expect(categoryRows.first()).toBeVisible();
    });

    test("displays permission rows with names", async ({ page }) => {
      await expect(page.locator("table")).toBeVisible({ timeout: 15000 });
      // Permission rows show the permission name in the first column
      const permissionCells = page.locator(
        "tbody tr:not(.bg-base-200) td:first-child span.font-medium",
      );
      await expect(permissionCells.first()).toBeVisible();
    });

    test("displays check/x icons for permission state", async ({ page }) => {
      await expect(page.locator("table")).toBeVisible({ timeout: 15000 });
      // The matrix uses CheckIcon (svg) or XMarkIcon (svg) for each cell
      const icons = page.locator("tbody tr:not(.bg-base-200) td svg");
      await expect(icons.first()).toBeVisible();
    });

    test("switching back to Split View works", async ({ page }) => {
      await page.getByRole("button", { name: "Split View" }).click();
      await expect(
        page.getByRole("button", { name: "Split View" }),
      ).toHaveClass(/btn-primary/);
      // Split view content should be visible again
      await expect(
        page.getByRole("button", { name: "Create Role" }),
      ).toBeVisible();
    });
  });

  /* ------------------------------------------------------------------------ */
  /*                         Create Role Modal                                */
  /* ------------------------------------------------------------------------ */

  test.describe("Create Role modal", () => {
    test("opens when Create Role button is clicked", async ({ page }) => {
      await page.getByRole("button", { name: "Create Role" }).click();
      const modal = page.locator("dialog.modal.modal-open");
      await expect(modal).toBeVisible();
      await expect(
        modal.getByText("Create New Role", { exact: true }),
      ).toBeVisible();
    });

    test("displays name and description fields", async ({ page }) => {
      await page.getByRole("button", { name: "Create Role" }).click();
      const modal = page.locator("dialog.modal.modal-open");
      await expect(
        modal.locator('input[placeholder="Enter role name"]'),
      ).toBeVisible();
      await expect(
        modal.locator(
          'textarea[placeholder="Enter role description (optional)"]',
        ),
      ).toBeVisible();
    });

    test("shows Role Name label with required asterisk", async ({ page }) => {
      await page.getByRole("button", { name: "Create Role" }).click();
      const modal = page.locator("dialog.modal.modal-open");
      await expect(modal.getByText("Role Name")).toBeVisible();
      await expect(modal.locator(".text-error", { hasText: "*" })).toBeVisible();
    });

    test("Create Role submit button is disabled when name is empty", async ({
      page,
    }) => {
      await page.getByRole("button", { name: "Create Role" }).click();
      const modal = page.locator("dialog.modal.modal-open");
      await expect(
        modal.getByRole("button", { name: "Create Role" }),
      ).toBeDisabled();
    });

    test("Create Role submit button is enabled when name is provided", async ({
      page,
    }) => {
      await page.getByRole("button", { name: "Create Role" }).click();
      const modal = page.locator("dialog.modal.modal-open");
      await modal
        .locator('input[placeholder="Enter role name"]')
        .fill("Test Role");
      await expect(
        modal.getByRole("button", { name: "Create Role" }),
      ).toBeEnabled();
    });

    test("Cancel button closes the modal", async ({ page }) => {
      await page.getByRole("button", { name: "Create Role" }).click();
      const modal = page.locator("dialog.modal.modal-open");
      await expect(modal).toBeVisible();
      await modal.getByRole("button", { name: "Cancel" }).click();
      await expect(modal).not.toBeVisible();
    });

    test("displays Cancel and Create Role buttons in modal footer", async ({
      page,
    }) => {
      await page.getByRole("button", { name: "Create Role" }).click();
      const modal = page.locator("dialog.modal.modal-open");
      await expect(modal).toBeVisible();
      await expect(
        modal.locator(".modal-action").getByRole("button", { name: "Cancel" }),
      ).toBeVisible();
      await expect(
        modal
          .locator(".modal-action")
          .getByRole("button", { name: "Create Role" }),
      ).toBeVisible();
    });
  });

  /* ------------------------------------------------------------------------ */
  /*                         Create, Edit, Delete Role (E2E)                  */
  /* ------------------------------------------------------------------------ */

  test.describe("Role CRUD operations", () => {
    const testRoleName = `E2E Role ${Date.now()}`;
    const updatedRoleName = `${testRoleName} Updated`;

    test("create a new custom role", async ({ page }) => {
      await page.getByRole("button", { name: "Create Role" }).click();
      const modal = page.locator("dialog.modal.modal-open");
      await modal
        .locator('input[placeholder="Enter role name"]')
        .fill(testRoleName);
      await modal
        .locator('textarea[placeholder="Enter role description (optional)"]')
        .fill("Role created by E2E test");
      await modal.getByRole("button", { name: "Create Role" }).click();

      // Modal should close after creation
      await expect(modal).not.toBeVisible({ timeout: 15000 });

      // New role should appear in the sidebar
      await expect(
        page.locator("button", { hasText: testRoleName }),
      ).toBeVisible({ timeout: 15000 });

      // New role should be selected and show PRJ badge
      await expect(
        page.locator(".badge", { hasText: "PRJ" }).first(),
      ).toBeVisible();
    });

    test("edit a custom role via the edit modal", async ({ page }) => {
      // First create a role to edit
      const roleName = `Edit Test ${Date.now()}`;
      await page.getByRole("button", { name: "Create Role" }).click();
      const createModal = page.locator("dialog.modal.modal-open");
      await createModal
        .locator('input[placeholder="Enter role name"]')
        .fill(roleName);
      await createModal.getByRole("button", { name: "Create Role" }).click();
      await expect(createModal).not.toBeVisible({ timeout: 15000 });

      // Wait for the role to appear and be selected
      await expect(
        page.locator("button", { hasText: roleName }),
      ).toBeVisible({ timeout: 15000 });

      // Click the edit (pencil) button in the detail panel header
      // The pencil icon button is inside the right-side card's header
      const detailCard = page.locator(".flex-1.card");
      await detailCard.locator(".btn-ghost.btn-circle").first().click();

      // The edit modal should open
      const editModal = page.locator("dialog.modal.modal-open");
      await expect(editModal).toBeVisible();
      await expect(editModal.getByText("Edit Role")).toBeVisible();

      // Update the name
      const nameInput = editModal.locator(
        'input[placeholder="Enter role name"]',
      );
      await nameInput.clear();
      await nameInput.fill(`${roleName} Edited`);

      await editModal.getByRole("button", { name: "Update Role" }).click();
      await expect(editModal).not.toBeVisible({ timeout: 15000 });

      // The updated name should appear in the sidebar
      await expect(
        page.locator("button", { hasText: `${roleName} Edited` }),
      ).toBeVisible({ timeout: 15000 });
    });

    test("delete a custom role via the delete modal", async ({ page }) => {
      // First create a role to delete
      const roleName = `Delete Test ${Date.now()}`;
      await page.getByRole("button", { name: "Create Role" }).click();
      const createModal = page.locator("dialog.modal.modal-open");
      await createModal
        .locator('input[placeholder="Enter role name"]')
        .fill(roleName);
      await createModal.getByRole("button", { name: "Create Role" }).click();
      await expect(createModal).not.toBeVisible({ timeout: 15000 });

      // Wait for the role to appear and be selected
      await expect(
        page.locator("button", { hasText: roleName }),
      ).toBeVisible({ timeout: 15000 });

      // Click the delete (trash) button in the detail panel
      await page.locator(".btn-circle.text-error").click();

      // The delete modal should open
      const deleteModal = page.locator("dialog.modal.modal-open");
      await expect(deleteModal).toBeVisible();
      await expect(deleteModal.getByText("Delete Role")).toBeVisible();

      // Confirm the role name is shown in the modal
      await expect(deleteModal.getByText(roleName)).toBeVisible();

      // Click Delete to confirm
      await deleteModal.getByRole("button", { name: "Delete" }).click();
      await expect(deleteModal).not.toBeVisible({ timeout: 15000 });

      // The role should no longer appear in the sidebar
      await expect(
        page.locator("button", { hasText: roleName }),
      ).not.toBeVisible({ timeout: 10000 });
    });
  });

  /* ------------------------------------------------------------------------ */
  /*                         Edit Permissions (Standard Role)                 */
  /* ------------------------------------------------------------------------ */

  test.describe("Edit Permissions button state", () => {
    test("Edit Permissions button is disabled for standard roles", async ({
      page,
    }) => {
      // Admin is a standard role - Edit Permissions should be disabled
      await expect(
        page.locator("button", { hasText: "Admin" }).first(),
      ).toBeVisible();

      await expect(
        page.getByRole("button", { name: "Edit Permissions" }),
      ).toBeDisabled({ timeout: 15000 });
    });
  });
});
