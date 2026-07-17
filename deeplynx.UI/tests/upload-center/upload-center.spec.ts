import { test, expect, Page } from "@playwright/test";
import { seedAndNavigateToProject } from "../helpers/seed";
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';

const TEN_GB = 10 * 1024 * 1024 * 1024;
const TWENTY_MIN_MS = 20 * 60 * 1000;


test.describe("Upload Center", () => {

  async function checkDataSourcesAndStorageDestinations({ page }: { page: Page }) {
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


  test.beforeEach(async ({ page }) => {
    await seedAndNavigateToProject(page);
    // Navigate to Upload Center via sidebar
    await page.locator("aside a", { hasText: "Upload Center" }).click();
    await page.waitForURL(/\/upload_center/);
    // Wait for the Upload Center heading to confirm client-side render is done
    await expect(
      page.getByRole("heading", { name: "Upload Center" }),
    ).toBeVisible();
  });

  test("Upload Center page renders with heading", async ({ page }) => {
    await expect(
      page.getByRole("heading", { name: "Upload Center" }),
    ).toBeVisible();
  });

  test("page shows Upload Mode heading", async ({ page }) => {
    await expect(
      page.getByRole("heading", { name: "Upload Mode" }),
    ).toBeVisible();
  });

  test("File Upload radio button is visible", async ({ page }) => {
    await expect(
      page.getByRole("radio", { name: "File Upload" }),
    ).toBeVisible();
  });

  test("Bulk Metadata radio button is visible", async ({ page }) => {
    await expect(
      page.getByRole("radio", { name: "Bulk Metadata" }),
    ).toBeVisible();
  });

  test("File Upload is the default mode", async ({ page }) => {
    // In file upload mode, the drag & drop zone is visible
    await expect(
      page.getByRole("heading", { name: "File Upload" }),
    ).toBeVisible();
    await expect(
      page.getByRole("button", { name: "Download Metadata Template" }),
    ).toBeVisible();
    await expect(
      page.getByText("Drag & drop files here"),
    ).toBeVisible();
  });

  test("Project / Data Source section is visible", async ({ page }) => {
    await expect(
      page.getByRole("heading", { name: "Project / Data Source" }),
    ).toBeVisible();
  });


  test("Select a project box is visible", async ({ page }) => {
    await expect(
      page.getByText("Select a projectProject"),
    ).toBeVisible();
  })

  test("Select a project dropdown is visible", async ({ page }) => {
    await expect(
      page.getByLabel("Select a")
    ).toBeVisible();
  });

  test("Data source box is visible", async ({ page }) => {
    await expect(
      page.getByText("Data sourceData"),
    ).toBeVisible();
  })

  test("Data source dropdown is visible", async ({ page }) => {
    await expect(
      page.getByLabel("Data source")
    ).toBeVisible();
  });

  test("Storage destination box is visible", async ({ page }) => {
    await expect(
      page.getByText("Storage DestinationObject"),
    ).toBeVisible();
  })

  test("Storage Destination dropdown is visible", async ({ page }) => {
    await expect(
      page.getByLabel("Storage Destination")
    ).toBeVisible();
  });

  test("drag and drop zone is visible in file mode", async ({ page }) => {
    await expect(
      page.getByText("Drag & drop files here"),
    ).toBeVisible();
  });

  test("switching to Bulk Metadata mode shows bulk upload section", async ({
    page,
  }) => {
    await page.getByRole("radio", { name: "Bulk Metadata" }).click();
    // The Bulk Metadata section renders with an info box containing
    // "Bulk Metadata Upload" heading and the CsvTemplateDownload button.
    await expect(
      page.getByRole("heading", { name: "Bulk Metadata", exact: true }),
    ).toBeVisible();
    await expect(
      page.getByText("Bulk Metadata Upload"),
    ).toBeVisible();
    await expect(
      page.getByText("Create multiple records at"),
    ).toBeVisible();
  });

  test("switching to Bulk Metadata mode renders bulk upload section components", async ({
    page,
  }) => {
    await page.getByRole("radio", { name: "Bulk Metadata" }).click();
    // The Bulk Metadata section renders with an info box containing
    // "Bulk Metadata Upload" heading and the CsvTemplateDownload button.
    await expect(
      page.getByRole("heading", { name: "Bulk Metadata", exact: true }),
    ).toBeVisible();
    await expect(
      page.getByText("Bulk Metadata Upload"),
    ).toBeVisible();
    await expect(
      page.getByText("Create multiple records at"),
    ).toBeVisible();
    await expect(
      page.getByText("Step 1: Download Template"),
    ).toBeVisible();
    await expect(
      page.getByRole("button", { name: "Step 1: Download Template" }),
    ).toBeVisible();
    await expect(
      page.getByText("Step 2: Upload Your CSV"),
    ).toBeVisible();
    await expect(
      page.getByRole("button", { name: "Step 2: Upload Your CSV" }),
    ).toBeVisible();
  });

  test.describe('Large file upload', () => {
    let filePath: string;

    test.beforeAll(async () => {
      filePath = path.join(os.tmpdir(), 'ten-gb-test-file.bin');

      // Reuse the file across runs if it already exists and is the right size
      if (fs.existsSync(filePath) && fs.statSync(filePath).size === TEN_GB) {
        return;
      }

      await new Promise<void>((resolve, reject) => {
        const stream = fs.createWriteStream(filePath);
        const chunkSize = 64 * 1024 * 1024; // 64 MB chunks
        const chunk = Buffer.alloc(chunkSize, 'a'); // fill, not sparse
        let written = 0;

        function writeNext() {
          if (written >= TEN_GB) {
            stream.end();
            return;
          }
          const remaining = TEN_GB - written;
          const toWrite = remaining < chunkSize ? chunk.subarray(0, remaining) : chunk;
          written += toWrite.length;

          // Handle backpressure correctly
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
      // Comment this out if you want to cache the file between test runs
      // to avoid regenerating 10 GB every time.
      if (fs.existsSync(filePath)) {
        fs.unlinkSync(filePath);
      }
    });

    test('Upload a 10 GB file and verify completion in < 20 minutes', async ({ page }) => {
      await checkDataSourcesAndStorageDestinations({ page });

      test.setTimeout(TWENTY_MIN_MS + 60_000); // budget + buffer for setup/assertions

      const start = Date.now();

      await page.getByText('click to browse').click();

      const fileInput = page.locator('input[type="file"]');
      await fileInput.setInputFiles(filePath);

      await page.getByRole('button', { name: 'Upload' }).click();

      await expect(page.getByText('File uploaded successfully!')).toBeVisible({
        timeout: TWENTY_MIN_MS,
      });

      const elapsedMs = Date.now() - start;
      console.log(`Upload completed in ${(elapsedMs / 1000 / 60).toFixed(2)} minutes`);
      expect(elapsedMs).toBeLessThan(TWENTY_MIN_MS);
    });
  });

  test.describe("Upload multiple files", () => {
    let filePaths: [string, string, string, string, string];

    test.beforeEach(async ({ page }) => {
      await seedAndNavigateToProject(page);

      // Create the files locally
      filePaths = [
          path.join(os.tmpdir(), 'upload-test-file-one'),
          path.join(os.tmpdir(), 'upload-test-file-two'),
          path.join(os.tmpdir(), 'upload-test-file-three'),
          path.join(os.tmpdir(), 'upload-test-file-four'),
          path.join(os.tmpdir(), 'upload-test-file-five')
      ]

      for (const filePath of filePaths) {
        if (!fs.existsSync(filePath) || fs.statSync(filePath).size !== 400 * 1024 * 1024) {
          await fs.promises.writeFile(filePath, Buffer.alloc(1));
          await fs.promises.truncate(filePath, 400 * 1024 * 1024);
        }
      }
    });

    test.afterAll(async () => {
      for (const filePath in filePaths) {
        if (fs.existsSync(filePath)) {
          fs.unlinkSync(filePath);
        }
      }
    });

    test("uploads multiple files", async ({ page }) => {
      test.setTimeout(120_000); // two minutes buffer time
      const start = Date.now();

      // Upload the files
      await page.locator("aside a", { hasText: "Upload Center" }).click();
      await page.waitForURL(/\/upload_center/);
      await expect(page.getByRole("heading", { name: "Upload Center" })).toBeVisible();

      await checkDataSourcesAndStorageDestinations({ page });

      await page.getByText('click to browse').click();
      const fileInput = page.locator('input[type="file"]');
      await fileInput.setInputFiles(filePaths);
      await page.getByRole('button', { name: 'Upload' }).click();
      await expect(page.getByText('Uploaded 5 file(s)')).toBeVisible({
      timeout: 120_000,
      });

      // Navigate to Project Page
      await page.locator("aside a", { hasText: "Project Dashboard" }).click();
      await page.waitForURL(/\/project/);
      await expect(page.getByRole("heading", { name: "PROJECT" })).toBeVisible();

      await expect(page.getByText('upload-test-file-one')).toBeVisible();
      await expect(page.getByText('upload-test-file-two')).toBeVisible();
      await expect(page.getByText('upload-test-file-three')).toBeVisible();
      await expect(page.getByText('upload-test-file-four')).toBeVisible();
      await expect(page.getByText('upload-test-file-five')).toBeVisible();

      const elapsedMs = Date.now() - start;

      expect(elapsedMs).toBeLessThan(60_000);
    });
  });

  test.describe("Upload bulk records", () => {
    let filePath: string;

    test.beforeEach(async ({}) => {
      // Create the file locally
      filePath = path.join(os.tmpdir(), 'bulk-upload.csv');

      const fileContent = [
        'name (required),description (required),original_id (required),properties (required - JSON format),uri (optional),object_storage_id (optional),class_id (optional),class_name (optional),file_type (optional),tags (optional - comma-separated),sensitivity_labels (optional - comma-separated)',
        'Bulk test 1,A test file for bulk upload testing,bt1,"{""created"":""June 2026"",""candy"":""smarties"",""color"":""red""}",,,,,txt,,',
        'Bulk test 2,A test file for bulk upload testing,bt2,"{""created"":""July 2026"",""candy"":""M&Ms"",""color"":""yellow""}",,,,,pdf,,',
        'Bulk test 3,A test file for bulk upload testing,bt3,"{""created"":""July 2026"",""candy"":""skittles"",""color"":""purple""}",,,,,docx,,',
        'Bulk test 4,A test file for bulk upload testing,bt4,"{""created"":""July 2026"",""chips"":""takis"",""spice"":""extreme""}",,,,,txt,,',
        'Bulk test 5,A test file for bulk upload testing,btS,"{""created"":""July 2026"",""cookies"":""oreos"",""type"":""birthday cake""}",,,,,json,,'
      ].join('\n');

      await fs.promises.writeFile(filePath, fileContent, 'utf8');
    });

    test.afterAll(async () => {
      if (fs.existsSync(filePath)) {
        fs.unlinkSync(filePath);
      }
    });

    test("uploads bulk records via a CSV", async ({ page }) => {
      test.setTimeout(120_000); // buffer time
      const start = Date.now();

      // Upload the csv file
      await page.getByText('Bulk Metadata').click();

      await checkDataSourcesAndStorageDestinations({ page });

      await page.getByRole('button', {name: 'Step 2: Upload Your CSV'}).click();
      const fileInput = page.locator('input[type="file"]');
      await fileInput.setInputFiles(filePath);
      await expect(page.getByText('Validation Successful!')).toBeVisible();
      await page.getByRole('button', { name: 'Upload 5 Records' }).click();
      await page.getByRole('button', { name: 'Confirm Upload' }).click();
      await expect(page.getByText('Successfully uploaded 5 Records!')).toBeVisible({
      timeout: 60_000,
      });

      // Verify the new files appear
      await page.locator("aside a", { hasText: "Project Dashboard" }).click();
      await page.waitForURL(/\/project/);
      await expect(page.getByRole("heading", { name: "PROJECT" })).toBeVisible();
      await expect(page.getByText('Bulk test 1')).toBeVisible();
      await expect(page.getByText('Bulk test 2')).toBeVisible();
      await expect(page.getByText('Bulk test 3')).toBeVisible();
      await expect(page.getByText('Bulk test 4')).toBeVisible();
      await expect(page.getByText('Bulk test 5')).toBeVisible();

      const elapsedMs = Date.now() - start;
      expect(elapsedMs).toBeLessThan(120_000);

    });
  });
});