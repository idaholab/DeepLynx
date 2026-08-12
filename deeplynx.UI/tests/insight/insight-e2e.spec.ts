import { test, expect, Page, APIRequestContext } from "../fixtures";
import { sysAdmin } from "../deeplynx-config";
import * as fs from "fs";
import * as path from "path";
import * as os from "os";
import { testApiUrl } from "../api-url";

const SOURCE_PDF_PATH = path.resolve(__dirname, "genesis-mission.pdf");

type Organization = {
  id: number;
  name: string;
};

type RecordSummary = {
  id: string;
  name: string;
  projectId: string;
};

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

async function deleteRecordIfExists(
  { request }: { request: import('@playwright/test').APIRequestContext },
  record: { recordId: string; projectId: string } | null,
  orgId: string,
) {
  if (!record) return;
  const url = testApiUrl(
    `/organizations/${orgId}/projects/${record.projectId}/records/${record.recordId}`,
  );
  try {
    const response = await request.delete(url);
    if (!response.ok()) {
      console.warn(`Failed to delete record ${record.recordId}: ${response.status()} ${await response.text()}`);
    }
  } catch (err) {
    console.warn(`Error deleting record ${record.recordId}:`, err);
  }
}

async function getOrgIdByName(
  request: APIRequestContext, orgName: string
): Promise<string> {
  const res = await request.fetch(testApiUrl("/organizations"));
  if (!res.ok()) throw new Error(`Failed to fetch organizations: ${res.status()}`);
  const orgs: Organization[] = await res.json();
  const match = orgs.find((org) => org.name === orgName);
  if (!match) {
    throw new Error(`Could not find organization named "${orgName}" in ${JSON.stringify(orgs)}`);
  }
  return String(match.id);
}

// Fetches all records for the project and returns every record matching the
// given file name, so cleanup doesn't rely on clicking through the UI.
async function getRecordsByName(
  request: APIRequestContext,
  orgId: string,
  projectId: string,
  fileName: string,
): Promise<RecordSummary[]> {
  const url = testApiUrl(
    `/organizations/${orgId}/projects/${projectId}/records` +
      `?hideArchived=true&isInsightEligible=false`,
  );
  const res = await request.fetch(url);
  if (!res.ok()) throw new Error(`Failed to fetch records: ${res.status()}`);
  const records = await res.json();
  return records
    .filter((r: any) => r.name === fileName)
    .map((r: any) => ({ id: String(r.id), name: r.name, projectId: String(r.projectId) }));
}

test.describe("Insight E2E", () => {
  test.skip(process.env.RUN_INSIGHT_TESTS !== "true", "Insight tests disabled");
  let orgId: string;
  let projectId: string;
  let uniqueFileName: string;
  let uniqueFilePath: string;
  const ORG_NAME = "PW Org A";

  test.use({
    actingUser: sysAdmin,
    actingOrg: ORG_NAME,
    actingProject: "PW Project X",
  });

  test.beforeEach(async ({ page, request }, testInfo) => {
    // Give this run's uploaded file a unique name derived from testInfo.testId,
    // which is unique per test invocation -- including across parallel browser
    // projects (chromium/firefox/webkit) and retries. This means concurrent
    // runs never see or delete each other's records, without needing to force
    // serial execution.
    uniqueFileName = `genesis-mission-${testInfo.testId}.pdf`;
    uniqueFilePath = path.join(os.tmpdir(), uniqueFileName);
    if (!fs.existsSync(uniqueFilePath)) {
      fs.copyFileSync(SOURCE_PDF_PATH, uniqueFilePath);
    }

    orgId = await getOrgIdByName(request, ORG_NAME);
    await page.getByTestId("project-select").click();

    await page
      .getByRole("button", { name: "PW Project X", exact: true })
      .click();

    await expect(page).toHaveURL(/\/project\/\d+/);

    const match = page.url().match(/\/project\/(\d+)/);
    expect(match).not.toBeNull();
    projectId = match![1];
  });

  test.afterEach(async ({ request }) => {
    if (fs.existsSync(uniqueFilePath)) {
      fs.unlinkSync(uniqueFilePath);
    }

    // Query the API directly for every record matching this run's unique
    // file name, rather than clicking through the dashboard UI one at a time.
    const records = await getRecordsByName(request, orgId, projectId, uniqueFileName);
    for (const record of records) {
      await deleteRecordIfExists(
        { request },
        { recordId: record.id, projectId: record.projectId },
        orgId,
      );
    }

    // Confirm cleanup actually worked.
    const remaining = await getRecordsByName(request, orgId, projectId, uniqueFileName);
    expect(remaining).toHaveLength(0);
  });

  test("upload file, embed, and query chatbot", async ({ page }) => {
    // This test walks the full Insight pipeline: upload -> embed -> chat.
    // Embedding takes ~30s; give the entire test 3 minutes.
    test.setTimeout(360_000);

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
    await fileInput.setInputFiles(uniqueFilePath);

    // Wait for the file card to appear with the filename
    await expect(page.getByText(uniqueFileName)).toBeVisible({
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
      page.getByRole('article').filter({ hasText: uniqueFileName })
    ).toBeVisible({ timeout: 15000 });

    // Select the checkbox for this run's specific file, not just "first()",
    // since other parallel runs' files may also be pending in the same list.
    await page
      .getByRole('article')
      .filter({ hasText: uniqueFileName })
      .getByRole('checkbox')
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
        if (!response.url().includes("/insight/ingestion_status/")) return false;
        try {
          const body = await response.json();
          return body.indexed === true;
        } catch {
          return false;
        }
      },
      { timeout: 240_000 },
    );

    await page.getByRole('button', { name: 'Embedded' }).click();

    await expect(
      page.getByRole('heading', { name: uniqueFileName }).first()
    ).toBeVisible({ timeout: 10_000 });

    // The chat intro message should now reference 1 embedded file.
    // Type a question in the chat textarea.
    const chatInput = page.getByRole('textbox', { name: 'Ask Insight about the' })
    await expect(chatInput).toBeVisible({ timeout: 10000 });
    await chatInput.fill("What is the Genesis Mission?");

    // Submit the question
    await page
      .getByRole('button', { name: 'Send insight prompt' })
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
