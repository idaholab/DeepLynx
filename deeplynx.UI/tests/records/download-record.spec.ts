import { test, expect, Page } from "../fixtures";
import { sysAdmin } from '../deeplynx-config';
import path from "path";
import * as os from 'os';
import * as fs from 'fs';

const ONE_GB = 1 * 1024 * 1024;
const TEN_GB = 10 * 1024 * 1024;
const FIVE_MIN_MS = 5 * 60 * 1000;
const TWENTY_MIN_MS = 20 * 60 * 1000;

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
    await page.locator("aside a", { hasText: "Upload Center" }).click();
    await page.waitForURL(/\/upload_center/);
    await expect(page.getByRole("heading", { name: "Upload Center" })).toBeVisible();

    await checkDataSourcesAndStorageDestinations(page);

    await page.getByText('click to browse').click();
    const fileInput = page.locator('input[type="file"]');
    await fileInput.setInputFiles(filePath);
    await page.getByRole('button', { name: 'Upload' }).click();
    await expect(page.getByText('File uploaded successfully!')).toBeVisible({
    timeout: FIVE_MIN_MS,
    });

    // Download the file
    await page.locator("aside a", { hasText: "Project Dashboard" }).click();
    await page.waitForURL(/\/project/);
    await expect(page.getByRole("heading", { name: "PROJECT Overview" })).toBeVisible();

    // go to record page
    await expect(page.getByText('uploaded-test-file').first()).toBeVisible();
    await page.getByText('uploaded-test-file').first().click();
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

test.describe("Download a 10 GB file", () => {
  test.use({ actingUser: sysAdmin, actingOrg: 'PW Org A', actingProject: 'PW Project X' });
  let filePath: string;

  test.beforeEach(async ({ page }) => {
    await page.getByRole('link', { name: 'PW Project X' }).click();

    // Create the 10GB file locally
    filePath = path.join(os.tmpdir(), 'big-test-file');

    if (fs.existsSync(filePath) && fs.statSync(filePath).size === TEN_GB)
      return;

    await new Promise<void>((resolve, reject) => {
      const stream = fs.createWriteStream(filePath);
      const chunkSize = 64 * 1024 * 1024;
      const chunk = Buffer.alloc(chunkSize, 'a');
      let written = 0;

      function writeNext() {
        if (written >= TEN_GB) {
          stream.end();
          return;
        }
        const remaining = TEN_GB - written;
        const toWrite = remaining < chunkSize ? chunk.subarray(0, remaining) : chunk;
        written += toWrite.length;

        if (!stream.write(toWrite)) {
          stream.once('drain', writeNext);
        } else {
          setImmediate(writeNext);
        }
      }
      stream.on('finish', resolve);
      stream.on('error', reject);
      writeNext();
    });
  });

  test.afterAll(async () => {
    if (fs.existsSync(filePath)) {
      fs.unlinkSync(filePath);
    }
  });

  test("downloads a 10GB+ file in under 5 minutes", async ({ page }) => {
    test.setTimeout(FIVE_MIN_MS + 60_000); // buffer time

    // Upload a 10GB file for download
    await page.locator("aside a", { hasText: "Upload Center" }).click();
    await page.waitForURL(/\/upload_center/);
    await expect(page.getByRole("heading", { name: "Upload Center" })).toBeVisible();

    await checkDataSourcesAndStorageDestinations(page);

    await page.getByText('click to browse').click();
    const fileInput = page.locator('input[type="file"]');
    await fileInput.setInputFiles(filePath);
    await page.getByRole('button', { name: 'Upload' }).click();
    await expect(page.getByText('File uploaded successfully!')).toBeVisible({
    timeout: TWENTY_MIN_MS,
    });

    // Download the file
    await page.locator("aside a", { hasText: "Project Dashboard" }).click();
    await page.waitForURL(/\/project/);
    await expect(page.getByRole("heading", { name: "PROJECT Overview" })).toBeVisible();

    // go to record page
    await expect(page.getByText('big-test-file').first()).toBeVisible();
    await page.getByText('big-test-file').first().click();
    const start = Date.now();

    const downloadPromise = page.waitForEvent('download');
    await page.getByRole('button', { name: /download/i }).click();

    const download = await downloadPromise;
    const downloadPath = await download.path();

    const elapsedMs = Date.now() - start;
    console.log(`Download completed in ${(elapsedMs / 1000 / 60).toFixed(2)} minutes.`);

    expect(elapsedMs).toBeLessThan(FIVE_MIN_MS);
    const stats = fs.statSync(downloadPath!);
    expect (stats.size).toBe(TEN_GB);

  });
});