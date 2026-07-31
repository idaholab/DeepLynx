import { test, expect, Page } from "../fixtures";
import { sysAdmin } from "../deeplynx-config";
import path from "path";

const BACKEND_URL = "http://localhost:5000/api/v1";
const PDF_PATH = path.resolve(__dirname, "genesis-mission.pdf");

async function checkDataSources(page: Page) {
  const dataSourceSelect = page.getByLabel('Data sourceData Sources');
  const selectedText = await dataSourceSelect.locator('option:checked').textContent();

  if (selectedText === 'Data Sources') {
    await dataSourceSelect.selectOption({ index: 1 }); // first real option, skipping the placeholder
  }
}

async function checkStorageDestinations(page: Page) {
  const storageSelect = page.getByLabel('Storage DestinationObject');
  const selectedText = await storageSelect.locator('option:checked').textContent();

  if (selectedText === 'Object storages') {
    await storageSelect.selectOption({ index: 1 }); // first real option, skipping the placeholder
  }
}
async function checkDataSourcesAndStorageDestinations(page: Page) {
  const dataSourceBox = page.locator('span').filter({ hasText: 'Data source' }).first();
  const storageDestinationBox = page.locator('span').filter({ hasText: 'Storage Destination' }).first();
  try {
    await expect(dataSourceBox.locator('.size-6.text-success')).toBeVisible({ timeout: 3000 });
  } catch {
    await checkDataSources(page);
  }
  try {
    await expect(storageDestinationBox.locator('.size-6.text-success')).toBeVisible({ timeout: 3000 });
  } catch {
    await checkStorageDestinations(page);
  }
}

test.describe("Insight E2E", () => {
  test.skip(process.env.RUN_INSIGHT_TESTS !== "true", "Insight tests disabled");

  test.use({
    actingUser: sysAdmin,
    actingOrg: "PW Org A",
    actingProject: "PW Project X",
  });

  test.beforeEach(async ({ page }) => {
    await page.getByTestId("project-select").click();

    await page
      .getByRole("button", { name: "PW Project X", exact: true })
      .click();

    await expect(page).toHaveURL(/\/project\/\d+/);
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

    // Attach the PDF via the hidden file input inside DropUpload
    const fileInput = page.locator('input[type="file"]');
    await fileInput.setInputFiles(PDF_PATH);

    // Wait for the file card to appear with the filename
    await expect(page.getByText("genesis-mission.pdf")).toBeVisible({
      timeout: 10000,
    });
    await checkDataSourcesAndStorageDestinations(page);

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
      page.getByRole('article').filter({ hasText: 'Ready to embed' }).locator('h3').first()
    ).toBeVisible({ timeout: 15000 });

    // Select all visible pending records (our file)
    await page
      .getByRole("checkbox").first()
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
