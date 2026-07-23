import { test, expect, Page } from "../fixtures";
import { sysAdmin } from '../deeplynx-config';
import path from "path";
import * as os from 'os';
import * as fs from 'fs';

const ONE_GB = 1 * 1024 * 1024 * 1024;

async function checkDataSourcesAndStorageDestinations(page: Page) {
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

  await checkDataSources(page);
  await checkStorageDestinations(page);
}

test.describe("Download an uploaded file", () => {
  test.use({ actingUser: sysAdmin, actingOrg: 'PW Org A', actingProject: 'PW Project X' });
  let filePath: string;

  test.beforeEach(async ({ page }) => {
    await page.getByRole('link', { name: 'PW Project X' }).click();

    // Create the file locally
    filePath = path.join(os.tmpdir(), 'uploaded-test-file');

    if (!fs.existsSync(filePath) || fs.statSync(filePath).size !== ONE_GB) {
      await fs.promises.writeFile(filePath, Buffer.alloc(1));
      await fs.promises.truncate(filePath, ONE_GB);
    }
  });

  test.afterAll(async () => {
    if (fs.existsSync(filePath)) {
      fs.unlinkSync(filePath);
    }
  });

  test("downloads an uploaded file", async ({ page }) => {
    test.setTimeout(60_000); // buffer time

    // Upload a file for download
    await page.getByRole('link', { name: 'Upload Center' }).click();
    await page.waitForURL(/\/upload_center/);
    await expect(page.getByRole("heading", { name: "Upload Center" })).toBeVisible();

    await checkDataSourcesAndStorageDestinations(page);

    await page.getByRole('button', { name: 'File Upload Drag and Drop Area and Button' }).click();
    const fileInput = page.locator('input[type="file"]');
    await fileInput.setInputFiles(filePath);
    await page.getByRole('button', { name: 'Upload' }).last().click();
    await expect(page.getByText('File uploaded successfully!', { exact: true })).toBeVisible({
    timeout: 30_000,
    });

    // Download the file
    await page.getByRole('link', { name: 'Project Dashboard' }).click();
    await page.waitForURL(/\/project/);
    await expect(page.getByRole("heading", { name: "PROJECT Overview" })).toBeVisible();

    // go to record page
    const fileList = page.getByRole('list').filter({ hasText: 'uploaded-test-fileClass' });
    const firstFile = fileList.getByRole('listitem').first();
    await expect(fileList).toBeVisible();
    await expect(firstFile).toContainText('uploaded-test-file');
    await firstFile.click();
    const start = Date.now();

    const downloadPromise = page.waitForEvent('download');
    await page.getByRole('button', { name: /download/i }).click();

    const download = await downloadPromise;
    const downloadPath = await download.path();

    const elapsedMs = Date.now() - start;
    console.log(`Download completed in ${(elapsedMs / 1000 / 60).toFixed(2)} minutes.`);

    expect(elapsedMs).toBeLessThan(60_000);
    const stats = fs.statSync(downloadPath!);
    expect (stats.size).toBe(ONE_GB);

  });
});