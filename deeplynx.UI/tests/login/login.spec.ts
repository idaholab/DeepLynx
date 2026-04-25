import { test, expect } from "@playwright/test";
import { seedSession } from "../helpers/seed";

test.describe("Login", () => {
  /**
   * In the test environment NEXT_PUBLIC_DISABLE_FRONTEND_AUTHENTICATION=true,
   * so the login page immediately redirects to "/". These tests verify that
   * the auth-disabled flow works correctly and the local dev user is loaded.
   */

  test.describe("Login page redirect (auth disabled)", () => {
    test("visiting /login/signin redirects to the home page", async ({
      page,
    }) => {
      await seedSession(page);
      await page.goto("/login/signin", { waitUntil: "domcontentloaded" });
      // The login page detects auth is disabled and redirects to "/"
      await page.waitForURL("/", { timeout: 15000 });
    });

    test("login page shows Nexus logo while redirecting", async ({ page }) => {
      await seedSession(page);
      await page.goto("/login/signin", { waitUntil: "domcontentloaded" });
      // The logo should be visible during the redirect
      await expect(page.getByAltText("DeepLynx logo")).toBeVisible();
    });

    test("login page does not show the sign-in form when auth is disabled", async ({
      page,
    }) => {
      await seedSession(page);
      await page.goto("/login/signin", { waitUntil: "domcontentloaded" });
      // The System Use Notification and Sign In button should not render
      await expect(
        page.getByText("System Use Notification"),
      ).not.toBeVisible();
    });
  });

  test.describe("Authenticated session (local dev user)", () => {
    test.beforeEach(async ({ page }) => {
      await seedSession(page);
      await page.goto("/", { waitUntil: "domcontentloaded" });
    });

    test("home page loads with welcome greeting", async ({ page }) => {
      await expect(
        page.getByRole("heading", { name: /Welcome Back/i }),
      ).toBeVisible({ timeout: 15000 });
    });

    test("welcome greeting includes the dev user name", async ({ page }) => {
      await expect(
        page.getByText(/Welcome Back,\s+Local/i),
      ).toBeVisible({ timeout: 15000 });
    });

    test("Your Projects section is visible", async ({ page }) => {
      await expect(
        page.locator('[data-tour="projects-section"]'),
      ).toBeVisible({ timeout: 15000 });
    });

    test("sidebar renders with navigation links", async ({ page }) => {
      await expect(
        page.locator("aside", { hasText: "Project Dashboard" }),
      ).toBeVisible({ timeout: 15000 });
    });
  });
});
