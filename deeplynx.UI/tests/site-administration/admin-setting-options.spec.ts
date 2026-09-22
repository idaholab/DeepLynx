import { test, expect } from "../fixtures";
import { sysAdmin, orgAdminA, projectAdminX, standardUserX, ORGS, PROJECTS } from "../deeplynx-config";

// Each describe block below acts as a different account to verify that
// settings visibility scales down correctly with role: sysAdmin sees
// everything, down to standardUserX who sees no settings at all.
//
// The settings links are icon-only, so they're selected by role + accessible
// name (aria-label) rather than text — see SysAdminRoute/OrgAdminRoute links.
//
// sysAdmin and orgAdminA have no provisioned project of their own, so
// actingProject is used to put them on a project dashboard — otherwise
// "Project Settings" would never be reachable to check for either of them.

// SysAdmin Acting
test.describe("SysAdmin settings visibility", () => {
  test.use({ actingUser: sysAdmin, actingOrg: ORGS.orgA, actingProject: PROJECTS.projectX });

  test("sees the sysAdmin settings link", async ({ page }) => {
    await expect(page.getByRole("link", { name: "Admin Settings" })).toBeVisible();
  });

  test("sees the organization settings link", async ({ page }) => {
    await expect(page.getByRole("link", { name: "Organization Settings" })).toBeVisible();
  });

  test("sees the project settings link", async ({ page }) => {
    await expect(page.getByRole("link", { name: "Project Settings" })).toBeVisible();
  });
});

// OrgAdmin Acting
test.describe("Org admin settings visibility", () => {
  test.use({ actingUser: orgAdminA, actingProject: PROJECTS.projectX });

  test("does not see the sysAdmin settings link", async ({ page }) => {
    await expect(page.getByRole("link", { name: "Admin Settings" })).not.toBeVisible();
  });

  test("sees the organization settings link", async ({ page }) => {
    await expect(page.getByRole("link", { name: "Organization Settings" })).toBeVisible();
  });

  test("sees the project settings link", async ({ page }) => {
    await expect(page.getByRole("link", { name: "Project Settings" })).toBeVisible();
  });
});

// Project Admin Acting
test.describe("Project admin settings visibility", () => {
  test.use({ actingUser: projectAdminX });

  test("does not see the sysAdmin settings link", async ({ page }) => {
    await expect(page.getByRole("link", { name: "Admin Settings" })).not.toBeVisible();
  });

  test("does not see the organization settings link", async ({ page }) => {
    await expect(page.getByRole("link", { name: "Organization Settings" })).not.toBeVisible();
  });

  test("sees the project settings link", async ({ page }) => {
    await expect(page.getByRole("link", { name: "Project Settings" })).toBeVisible();
  });
});

// Standard User Acting
test.describe("Standard user settings visibility", () => {
  test.use({ actingUser: standardUserX });

  test("sees no settings links at all", async ({ page }) => {
    await expect(page.getByRole("link", { name: "Admin Settings" })).not.toBeVisible();
    await expect(page.getByRole("link", { name: "Organization Settings" })).not.toBeVisible();
    await expect(page.getByRole("link", { name: "Project Settings" })).not.toBeVisible();
  });
});