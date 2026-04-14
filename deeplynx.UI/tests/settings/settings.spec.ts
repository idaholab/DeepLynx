import { test, expect } from "@playwright/test";
import { seedSession } from "../helpers/seed";

test.describe("Settings Page", () => {
  test.beforeEach(async ({ page }) => {
    await seedSession(page);
    await page.goto("/settings", { waitUntil: "domcontentloaded" });
    // Wait for the User Settings heading to confirm the page has loaded
    // past the AuthGuard loading spinner. Needs a longer timeout because
    // the AuthGuard + session fetch chain can be slow under server load.
    await expect(page.getByText("User Settings")).toBeVisible({ timeout: 15000 });
  });

  test("Settings page renders with user name heading", async ({ page }) => {
    await expect(page.locator("h1").first()).toBeVisible();
  });

  test("User Settings section is visible", async ({ page }) => {
    await expect(page.getByText("User Settings")).toBeVisible();
  });

  test("Name and Email labels are displayed", async ({ page }) => {
    await expect(page.getByText("Name")).toBeVisible();
    await expect(page.getByText("Email")).toBeVisible();
  });

  test("Preferences section is visible", async ({ page }) => {
    await expect(page.getByText("Preferences")).toBeVisible();
  });

  test("Dark Mode toggle is visible", async ({ page }) => {
    await expect(page.getByText("Dark Mode")).toBeVisible();
  });

  test("API Keypairs section is visible", async ({ page }) => {
    await expect(page.getByText("API Keypairs")).toBeVisible();
  });
});
