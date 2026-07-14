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

  test.describe('CSV upload', () => {
    let filePath: string;

    test.beforeAll(async () => {
      filePath = path.join(os.tmpdir(), 'test-file.csv');
      const header = 'id,name,email,value,timestamp\n';

      // Reuse the file across runs if it already exists
      if (fs.existsSync(filePath)) {
        return;
      }

      await fs.promises.writeFile(filePath, header);
    });

    test.afterAll(async () => {
      // Comment this out if you want to cache the file between test runs
      if (fs.existsSync(filePath)) {
        fs.unlinkSync(filePath);
      }
    });

    test('Upload a single CSV file using click to browse', async ({ page }) => {
      await checkDataSourcesAndStorageDestinations({ page });

      await page.getByText('click to browse').click();

      const fileInput = page.locator('input[type="file"]');
      await fileInput.setInputFiles(filePath);

      await page.getByRole('button', { name: 'Upload' }).click();

      await expect(
        page.getByText('File uploaded successfully!')
      ).toBeVisible();

      await page.getByRole('link', { name: 'Project Dashboard' }).click();

      await expect(
        page.getByText('test-file.csv').first()
      ).toBeVisible();

      await page.getByRole('link', { name: 'Visit' }).first().click();

      await page.getByRole('textbox', { name: 'Search' }).click();

      await page.getByRole('textbox', { name: 'Search' }).fill('test-file.csv');

      await page.getByRole('textbox', { name: 'Search' }).press('Enter');

      await expect(
        page.getByRole('link', { name: 'test-file.csv', exact: true }).first()
      ).toBeVisible();
    });

    test('Upload a single CSV file using drag and drop', async ({ page }) => {
      const buffer = fs.readFileSync(filePath);
      const fileName = path.basename(filePath);

      // Build a DataTransfer object in the browser context containing the file
      const dataTransfer = await page.evaluateHandle(
        ({ bufferData, fileName }) => {
          const dt = new DataTransfer();
          const file = new File([new Uint8Array(bufferData)], fileName, {
            type: 'text/csv',
          });
          dt.items.add(file);
          return dt;
        },
        { bufferData: Array.from(buffer), fileName }
      );

      const dropZone = page.getByText('click to browse');

      // Dispatch the sequence of events a real drag-and-drop would fire
      await dropZone.dispatchEvent('dragenter', { dataTransfer });
      await dropZone.dispatchEvent('dragover', { dataTransfer });
      await dropZone.dispatchEvent('drop', { dataTransfer });

      await page.getByRole('button', { name: 'Upload' }).click();

      await expect(
        page.getByText('File uploaded successfully!')
      ).toBeVisible();

      await page.getByRole('link', { name: 'Project Dashboard' }).click();

      await expect(
        page.getByText('test-file.csv').first()
      ).toBeVisible();

      await page.getByRole('link', { name: 'Visit' }).first().click();

      await page.getByRole('textbox', { name: 'Search' }).click();

      await page.getByRole('textbox', { name: 'Search' }).fill('test-file.csv');

      await page.getByRole('textbox', { name: 'Search' }).press('Enter');

      await expect(
        page.getByRole('link', { name: 'test-file.csv', exact: true }).first()
      ).toBeVisible();
    });
  });
});