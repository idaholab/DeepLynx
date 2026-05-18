import type { Page } from "@playwright/test";

/**
 * Seeds localStorage and cookies with an organization session so that
 * OrganizationSessionProvider finds it on mount.  Also suppresses the
 * Shepherd guided-tour overlays.
 *
 * Call this in every test's `beforeEach` **before** any `page.goto()`.
 */
export async function seedSession(page: Page) {
  await page.addInitScript(() => {
    localStorage.setItem(
      "organizationSession",
      JSON.stringify({ organizationId: 1, organizationName: "INL" }),
    );
    localStorage.setItem("dashboard-tour-completed", "true");
    localStorage.setItem("project-tour-completed", "true");
  });
  await page.context().addCookies([
    {
      name: "organizationSession",
      value: encodeURIComponent(
        JSON.stringify({ organizationId: 1, organizationName: "INL" }),
      ),
      url: "http://localhost:3000",
    },
  ]);
}

/**
 * Seeds the session, then creates a project via the UI and waits for
 * the project dashboard to load.  Returns after the project header is
 * visible so the caller can immediately interact with project-scoped
 * pages (sidebar links are enabled once off "/").
 */
export async function seedAndCreateProject(
  page: Page,
  name = "Test Project",
) {
  await seedSession(page);
  await page.goto("/", { waitUntil: "domcontentloaded" });
  await page.locator('[data-tour="create-project"]').click();

  const modal = page.locator("dialog.modal.modal-open");
  await modal.locator('input[placeholder="Name"]').fill(name);
  await modal
    .locator('textarea[placeholder="Description"]')
    .fill("Auto-created for testing");
  await modal.getByRole("button", { name: "Create" }).click();

  await page.waitForURL(/\/project\/\d+/, { timeout: 30000 });
  await page.locator('[data-tour="project-header"]').waitFor();
}

/**
 * Seeds the session, navigates to "/", then clicks the first project
 * link in the table to enter a project context.  Faster than
 * seedAndCreateProject because it reuses an existing project.
 */
export async function seedAndNavigateToProject(page: Page) {
  await seedSession(page);
  await page.goto("/", { waitUntil: "domcontentloaded" });
  await page.locator('main a[href^="/project/"]').first().click();
  await page.waitForURL(/\/project\/\d+/);
}
