import { test, expect } from "@playwright/test";
import { seedAndCreateProject } from "../helpers/seed";
import path from "path";

const BACKEND_URL = "http://localhost:5000/api/v1";
const PDF_PATH = path.resolve(__dirname, "genesis-mission.pdf");

test.describe("Insight E2E", () => {
  let projectId: string;

  test.beforeEach(async ({ page }) => {
    await seedAndCreateProject(page, "Insight E2E Test");

    // Extract project ID from the URL (e.g. /project/42)
    const url = page.url();
    const match = url.match(/\/project\/(\d+)/);
    expect(match).not.toBeNull();
    projectId = match![1];

    // New projects have a default data source but NO object storage.
    // The Upload Center requires all three selectors to be filled,
    // so create one via the backend API.
    const storageRes = await fetch(
      `${BACKEND_URL}/organizations/1/projects/${projectId}/storages?makeDefault=true`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          name: "Test Storage",
          config: { MountPath: "/data" },
        }),
      },
    );
    expect(storageRes.ok).toBe(true);
  });

  test("upload file, embed, and query chatbot", async ({ page }) => {
    // This test walks the full Insight pipeline: upload -> embed -> chat.
    // Embedding takes ~30s; give the entire test 3 minutes.
    test.setTimeout(180_000);

    // ----------------------------------------------------------------
    // Step 1: Navigate to Upload Center and upload the PDF
    // ----------------------------------------------------------------
    await page.locator("aside a", { hasText: "Upload Center" }).click();
    await page.waitForURL(/\/upload_center/);
    await expect(
      page.getByRole("heading", { name: "Upload Center" }),
    ).toBeVisible();

    // Wait for project and data source to auto-select (they each have 1 option).
    // Storage Destination may have multiple options from the org, so it won't
    // auto-select — we need to pick it explicitly.
    const storageSelect = page.getByLabel("Storage Destination");
    await expect(storageSelect).toBeEnabled({ timeout: 15000 });
    await storageSelect.selectOption({ label: "Test Storage" });

    // Attach the PDF via the hidden file input inside DropUpload
    const fileInput = page.locator('input[type="file"]');
    await fileInput.setInputFiles(PDF_PATH);

    // Wait for the file card to appear with the filename
    await expect(page.getByText("genesis-mission.pdf")).toBeVisible({
      timeout: 10000,
    });

    // Click the Upload button
    await page.locator("button.btn-secondary", { hasText: "Upload" }).click();

    // Wait for the success toast
    await expect(page.getByText("File uploaded successfully")).toBeVisible({
      timeout: 30000,
    });

    // ----------------------------------------------------------------
    // Step 2: Navigate to Project Insight
    // ----------------------------------------------------------------
    await page.locator("aside a", { hasText: "Insight" }).click();
    await page.waitForURL(/\/project_insight/);
    await expect(
      page.getByRole("heading", { name: "Project Insight Chat" }),
    ).toBeVisible({ timeout: 15000 });

    // ----------------------------------------------------------------
    // Step 3: Queue the file for embedding
    // ----------------------------------------------------------------

    // Switch to the "Need Embedding" tab
    await page
      .locator("button", { hasText: "Need Embedding" })
      .click();

    // Wait for the uploaded record to appear in the pending list
    await expect(
      page.getByText("genesis-mission.pdf"),
    ).toBeVisible({ timeout: 15000 });

    // Select all visible pending records (our file)
    await page
      .locator("button", { hasText: "Select Visible" })
      .click();

    // Click "Embed Selected" to queue for embedding
    await page
      .locator("button", { hasText: "Embed Selected" })
      .click();

    // ----------------------------------------------------------------
    // Step 4: Wait for embedding to complete
    // ----------------------------------------------------------------

    // Rather than waiting on the UI's 5-second polling to eventually render
    // the file, watch the network directly: wait for a status response that
    // reports indexed:true, then verify the UI caught up.
    await page.waitForResponse(
      async (response) => {
        if (!response.url().includes("/api/insight/status/")) return false;
        try {
          const body = await response.json();
          return body.indexed === true;
        } catch {
          return false;
        }
      },
      { timeout: 120_000 },
    );

    await page
      .locator("button", { hasText: "Embedded Library" })
      .click();

    await expect(
      page.locator("article").filter({ hasText: "genesis-mission.pdf" }),
    ).toBeVisible({ timeout: 10_000 });

    // The chat intro message should now reference 1 embedded file.
    // Type a question in the chat textarea.
    const chatInput = page.locator(
      'textarea[placeholder*="Ask Insight"]',
    );
    await expect(chatInput).toBeVisible({ timeout: 10000 });
    await chatInput.fill("What is the Genesis Mission?");

    // Submit the question
    await page
      .locator('button[aria-label="Send insight prompt"]')
      .click();

    // Wait for an assistant response that mentions "Genesis".
    // The chat uses .chat-start for assistant messages. The first one is the
    // intro message; we need a second assistant bubble with the actual answer.
    // Wait for any chat bubble containing "Genesis" (case-insensitive) that
    // is NOT the intro message.
    await expect(
      page
        .locator(".chat-start .chat-bubble")
        .filter({ hasText: /genesis/i })
        .last(),
    ).toBeVisible({ timeout: 60000 });

    // Final assertion: the response text contains meaningful content about Genesis
    const responseBubbles = page.locator(".chat-start .chat-bubble");
    const lastBubble = responseBubbles.last();
    const responseText = await lastBubble.textContent();
    expect(responseText?.toLowerCase()).toContain("genesis");
  });
});
