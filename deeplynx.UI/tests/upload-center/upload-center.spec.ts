import { test, expect, Page } from "@playwright/test";
import { seedAndNavigateToProject } from "../helpers/seed";
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import * as zlib from 'zlib';

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

  async function dragAndDrop({ page }: { page: Page }, baseFileName: string, filePath: string, type: string) {
    await checkDataSourcesAndStorageDestinations({ page });

    const buffer = fs.readFileSync(filePath);
    const fileName = path.basename(filePath);

    // Build a DataTransfer object in the browser context containing the file
    const dataTransfer = await page.evaluateHandle(
      ({ bufferData, fileName, type }) => {
        const dt = new DataTransfer();
        const file = new File([new Uint8Array(bufferData)], fileName, {
          type: type,
        });
        dt.items.add(file);
        return dt;
      },
      { bufferData: Array.from(buffer), fileName, type }
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
      page.getByText(baseFileName).first()
    ).toBeVisible();

    await page.getByRole('link', { name: 'Visit' }).first().click();

    await page.getByRole('textbox', { name: 'Search' }).click();

    await page.getByRole('textbox', { name: 'Search' }).fill(baseFileName);

    await page.getByRole('textbox', { name: 'Search' }).press('Enter');

    await expect(
      page.getByRole('link', { name: baseFileName, exact: true }).first()
    ).toBeVisible();
  }

  async function clickToBrowse({ page }: { page: Page }, baseFileName: string, filePath: string) {
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
      page.getByText(baseFileName).first()
    ).toBeVisible();

    await page.getByRole('link', { name: 'Visit' }).first().click();

    await page.getByRole('textbox', { name: 'Search' }).click();

    await page.getByRole('textbox', { name: 'Search' }).fill(baseFileName);

    await page.getByRole('textbox', { name: 'Search' }).press('Enter');

    await expect(
      page.getByRole('link', { name: baseFileName, exact: true }).first()
    ).toBeVisible();
  }

  function crc32(buf: Buffer): number {
    let crc = ~0;
    for (const byte of buf) {
      crc ^= byte;
      for (let i = 0; i < 8; i++) {
        crc = (crc >>> 1) ^ (0xedb88320 & -(crc & 1));
      }
    }
    return ~crc >>> 0;
  }

  function createZip(fileName: string, content: string): Buffer {
    const data = Buffer.from(content, 'utf8');
    const compressed = zlib.deflateRawSync(data);
    const crc = crc32(data);
    const nameBuf = Buffer.from(fileName, 'utf8');

    const localHeader = Buffer.alloc(30);
    localHeader.writeUInt32LE(0x04034b50, 0); // local file header signature
    localHeader.writeUInt16LE(20, 4); // version needed
    localHeader.writeUInt16LE(0, 6); // flags
    localHeader.writeUInt16LE(8, 8); // compression method (deflate)
    localHeader.writeUInt16LE(0, 10); // mod time
    localHeader.writeUInt16LE(0, 12); // mod date
    localHeader.writeUInt32LE(crc, 14);
    localHeader.writeUInt32LE(compressed.length, 18);
    localHeader.writeUInt32LE(data.length, 22);
    localHeader.writeUInt16LE(nameBuf.length, 26);
    localHeader.writeUInt16LE(0, 28);

    const centralHeader = Buffer.alloc(46);
    centralHeader.writeUInt32LE(0x02014b50, 0); // central directory signature
    centralHeader.writeUInt16LE(20, 4);
    centralHeader.writeUInt16LE(20, 6);
    centralHeader.writeUInt16LE(0, 8);
    centralHeader.writeUInt16LE(8, 10);
    centralHeader.writeUInt16LE(0, 12);
    centralHeader.writeUInt16LE(0, 14);
    centralHeader.writeUInt32LE(crc, 16);
    centralHeader.writeUInt32LE(compressed.length, 20);
    centralHeader.writeUInt32LE(data.length, 24);
    centralHeader.writeUInt16LE(nameBuf.length, 28);
    centralHeader.writeUInt16LE(0, 30);
    centralHeader.writeUInt16LE(0, 32);
    centralHeader.writeUInt16LE(0, 34);
    centralHeader.writeUInt16LE(0, 36);
    centralHeader.writeUInt32LE(0, 38);
    centralHeader.writeUInt32LE(0, 42); // offset of local header

    const endRecord = Buffer.alloc(22);
    endRecord.writeUInt32LE(0x06054b50, 0);
    endRecord.writeUInt16LE(0, 4);
    endRecord.writeUInt16LE(0, 6);
    endRecord.writeUInt16LE(1, 8);
    endRecord.writeUInt16LE(1, 10);
    const centralDirSize = centralHeader.length + nameBuf.length;
    endRecord.writeUInt32LE(centralDirSize, 12);
    const localSectionSize = localHeader.length + nameBuf.length + compressed.length;
    endRecord.writeUInt32LE(localSectionSize, 16);
    endRecord.writeUInt16LE(0, 20);

    return Buffer.concat([
      localHeader,
      nameBuf,
      compressed,
      centralHeader,
      nameBuf,
      endRecord,
    ]);
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

  let filePath: string;
  let baseFileName: string;

  async function setUp(fileName: string, fileContents: any) {
    filePath = path.join(os.tmpdir(), fileName);
    // Reuse the file across runs if it already exists
    if (fs.existsSync(filePath)) {
      return;
    }
    await fs.promises.writeFile(filePath, fileContents);
  }

  test.describe('Uploading Tests of Various File Types', () => {

    test.afterEach(async () => {
      // Comment this out if you want to cache the file between test runs
      if (fs.existsSync(filePath)) {
        fs.unlinkSync(filePath);
      }
    });

    test.describe('SQL upload', () => {
      test.beforeEach(async () => {
        baseFileName = 'test-file.sql';
        await setUp(baseFileName, `-- Minimal test SQL file
CREATE TABLE test_table (
  id INTEGER PRIMARY KEY,
  name TEXT NOT NULL,
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
INSERT INTO test_table (id, name) VALUES (1, 'Test Row');
`);
      });
      test('Upload a single SQL file using click to browse', async ({ page }) => {
        await clickToBrowse({ page }, baseFileName, filePath);
      });

      test('Upload a single SQL file using drag and drop', async ({ page }) => {
        await dragAndDrop({ page }, baseFileName, filePath, 'application/sql');
      });
    });

    test.describe('TXT upload', () => {
      test.beforeEach(async () => {
        baseFileName = 'test-file.txt';
        await setUp(baseFileName, 'TESTING');
      });
      test('Upload a single TXT file using click to browse', async ({ page }) => {
        await clickToBrowse({ page }, baseFileName, filePath);
      });
      test('Upload a single TXT file using drag and drop', async ({ page }) => {
        await dragAndDrop({ page }, baseFileName, filePath, 'text/plain');
      });
    });

    test.describe('PDF upload', () => {
      test.beforeEach(async () => {
        baseFileName = 'test-file.pdf';
        await setUp(baseFileName, `%PDF-1.4
1 0 obj
<< /Type /Catalog /Pages 2 0 R >>
endobj
2 0 obj
<< /Type /Pages /Kids [3 0 R] /Count 1 >>
endobj
3 0 obj
<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>
endobj
4 0 obj
<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>
endobj
5 0 obj
<< /Length 44 >>
stream
BT /F1 24 Tf 100 700 Td (Test PDF file) Tj ET
endstream
endobj
xref
0 6
0000000000 65535 f 
0000000009 00000 n 
0000000058 00000 n 
0000000115 00000 n
0000000241 00000 n
0000000312 00000 n
trailer
<< /Size 6 /Root 1 0 R >>
startxref
407
%%EOF`);
      });
      test('Upload a single PDF file using drag and drop', async ({ page }) => {
        await dragAndDrop({ page }, baseFileName, filePath, 'application/pdf');
      });

      test('Upload a single PDF file using click to browse', async ({ page }) => {
        await clickToBrowse({ page }, baseFileName, filePath);
      });
    });

    test.describe('CSV upload', () => {
      test.beforeEach(async () => {
        baseFileName = 'test-file.csv';
        await setUp(baseFileName, 'id,name,email,value,timestamp\n');
      });
      test('Upload a single CSV file using click to browse', async ({ page }) => {
        await clickToBrowse({ page }, baseFileName, filePath);
      });

      test('Upload a single CSV file using drag and drop', async ({ page }) => {
        await dragAndDrop({ page }, baseFileName, filePath, 'text/csv');
      });
    });

    test.describe('ZIP upload', () => {
      test.beforeEach(async () => {
        baseFileName = 'test-file.zip';
        await setUp(baseFileName, createZip('fileToZip.txt', 'Zipped'));
      });
      test('Upload a single ZIP file using drag and drop', async ({ page }) => {
        await dragAndDrop({ page }, baseFileName, filePath, 'application/zip');
      });

      test('Upload a single ZIP file using click to browse', async ({ page }) => {
        await clickToBrowse({ page }, baseFileName, filePath);
      });
    });
  });
});