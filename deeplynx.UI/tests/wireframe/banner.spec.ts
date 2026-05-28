import { test, expect } from "@playwright/test";
import { seedSession } from "../helpers/seed";

test.describe("Wireframe - Banner", () => {
  test.beforeEach(async ({ page }) => {
    await seedSession(page);
  });

  test("banner displays the Nexus logo", async ({ page }) => {
    await page.goto("/", { waitUntil: "domcontentloaded" });
    const logo = page.locator('header img[alt="Logo"]');
    await expect(logo).toBeVisible();
  });
});
