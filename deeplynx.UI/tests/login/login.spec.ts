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

import * as fs from "fs";
import * as path from "path";

// Read .env files in Next.js precedence order to determine whether
// frontend authentication is enabled. Later files override earlier ones.
function isAuthEnabled(): boolean {
  const root = path.resolve(__dirname, "../..");
  const envFiles = [
    ".env",
    ".env.local",
    ".env.development",
    ".env.development.local",
  ];
  let disabled = false;

  for (const file of envFiles) {
    try {
      const content = fs.readFileSync(path.join(root, file), "utf-8");
      const match = content.match(
        /NEXT_PUBLIC_DISABLE_FRONTEND_AUTHENTICATION\s*=\s*(.+)/,
      );
      if (match) {
        disabled = match[1].trim().replace(/["']/g, "") === "true";
      }
    } catch {
      // File doesn't exist, skip
    }
  }

  return !disabled;
}

const AUTH_ENABLED = isAuthEnabled();

test.describe("Login Page", () => {
  test.skip(
    !AUTH_ENABLED,
    "Auth is disabled (NEXT_PUBLIC_DISABLE_FRONTEND_AUTHENTICATION=true)",
  );

  test.beforeEach(async ({ page }) => {
    await page.goto("/login/signin", { waitUntil: "domcontentloaded" });
  });

  test("displays the System Use Notification heading", async ({ page }) => {
    await expect(
      page.getByRole("heading", { name: "System Use Notification" }),
    ).toBeVisible({ timeout: 15000 });
  });

  test("displays DOE computer system notice", async ({ page }) => {
    await expect(
      page.getByText("This is a DOE computer system"),
    ).toBeVisible({ timeout: 15000 });
    await expect(
      page.getByText("THERE IS NO RIGHT OF PRIVACY IN THIS SYSTEM"),
    ).toBeVisible();
  });

  test("displays warning banner text", async ({ page }) => {
    await expect(
      page.getByText(/\*\*WARNING\*\*WARNING\*\*/),
    ).toBeVisible({ timeout: 15000 });
  });

  test("displays I Acknowledge button", async ({ page }) => {
    await expect(
      page.getByRole("button", { name: "I Acknowledge" }),
    ).toBeVisible({ timeout: 15000 });
  });

  test("Sign In does not trigger OAuth without acknowledgement", async ({
    page,
  }) => {
    await expect(
      page.getByRole("heading", { name: "System Use Notification" }),
    ).toBeVisible({ timeout: 15000 });

    // Track whether any auth request is made
    let authRequestMade = false;
    await page.route("**/api/auth/signin/**", async (route) => {
      authRequestMade = true;
      await route.fulfill({ status: 200, body: "" });
    });

    // The Sign In button exists in the DOM but handleOktaSignIn
    // returns early when hasAcknowledged is false
    await page.getByRole("button", { name: "Sign In" }).click({ force: true });

    // Give time for any async request to fire, then verify none did
    await page.waitForTimeout(2000);
    expect(authRequestMade).toBe(false);
  });

  test("clicking I Acknowledge reveals the Sign In button", async ({
    page,
  }) => {
    await expect(
      page.getByRole("button", { name: "I Acknowledge" }),
    ).toBeVisible({ timeout: 15000 });
    await page.getByRole("button", { name: "I Acknowledge" }).click();

    await expect(
      page.getByRole("button", { name: "Sign In" }),
    ).toBeVisible();
  });

  test("clicking Sign In initiates the OAuth flow", async ({ page }) => {
    // Intercept the CSRF endpoint so signIn() can get a token
    await page.route("**/api/auth/csrf", async (route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({ csrfToken: "test-token" }),
      });
    });

    // Intercept the signin endpoint to prevent external redirect
    await page.route("**/api/auth/signin/**", async (route) => {
      await route.fulfill({
        status: 200,
        contentType: "text/html",
        body: "<html></html>",
      });
    });

    await expect(
      page.getByRole("button", { name: "I Acknowledge" }),
    ).toBeVisible({ timeout: 15000 });
    await page.getByRole("button", { name: "I Acknowledge" }).click();

    const [request] = await Promise.all([
      page.waitForRequest(
        (req) => req.url().includes("/api/auth/signin"),
        { timeout: 10000 },
      ),
      page.getByRole("button", { name: "Sign In" }).click(),
    ]);

    expect(request.url()).toContain("/api/auth/signin");
  });

  test("displays Vulnerability Disclosure Program banner", async ({
    page,
  }) => {
    const link = page.getByRole("link", {
      name: "Vulnerability Disclosure Program",
    });
    await expect(link).toBeVisible({ timeout: 15000 });
    await expect(link).toHaveAttribute(
      "href",
      "https://www.synack.us/vdp/us-department-of-energy/",
    );
    await expect(link).toHaveAttribute("target", "_blank");
  });

  test("displays Privacy and Accessibility link", async ({ page }) => {
    const link = page.getByRole("link", {
      name: /Privacy and Accessibility/,
    });
    await expect(link).toBeVisible({ timeout: 15000 });
    await expect(link).toHaveAttribute(
      "href",
      "https://inl.gov/privacy-and-accessibility/",
    );
  });
});

test.describe("Auth Guard", () => {
  test.skip(
    !AUTH_ENABLED,
    "Auth is disabled (NEXT_PUBLIC_DISABLE_FRONTEND_AUTHENTICATION=true)",
  );

  test("unauthenticated visit to home page redirects to login", async ({
    page,
  }) => {
    // No session seeded — AuthGuard should redirect to /login/signin
    await page.goto("/", { waitUntil: "domcontentloaded" });
    await page.waitForURL(/\/login\/signin/, { timeout: 15000 });
    await expect(
      page.getByRole("heading", { name: "System Use Notification" }),
    ).toBeVisible({ timeout: 15000 });
  });

  test("unauthenticated visit to a protected route redirects to login", async ({
    page,
  }) => {
    // Try accessing a protected project route without any session
    await page.goto("/settings", { waitUntil: "domcontentloaded" });
    await page.waitForURL(/\/login\/signin/, { timeout: 15000 });
    await expect(
      page.getByRole("heading", { name: "System Use Notification" }),
    ).toBeVisible({ timeout: 15000 });
  });
});
