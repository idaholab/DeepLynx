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

  function createMultiEntryZip(files: { name: string; content: string }[]): Buffer {
    const localParts: Buffer[] = [];
    const centralParts: Buffer[] = [];
    let offset = 0;

    for (const { name, content } of files) {
      const data = Buffer.from(content, 'utf8');
      const compressed = zlib.deflateRawSync(data);
      const crc = crc32(data);
      const nameBuf = Buffer.from(name, 'utf8');

      const localHeader = Buffer.alloc(30);
      localHeader.writeUInt32LE(0x04034b50, 0);
      localHeader.writeUInt16LE(20, 4);
      localHeader.writeUInt16LE(0, 6);
      localHeader.writeUInt16LE(8, 8);
      localHeader.writeUInt16LE(0, 10);
      localHeader.writeUInt16LE(0, 12);
      localHeader.writeUInt32LE(crc, 14);
      localHeader.writeUInt32LE(compressed.length, 18);
      localHeader.writeUInt32LE(data.length, 22);
      localHeader.writeUInt16LE(nameBuf.length, 26);
      localHeader.writeUInt16LE(0, 28);

      const localEntry = Buffer.concat([localHeader, nameBuf, compressed]);
      localParts.push(localEntry);

      const centralHeader = Buffer.alloc(46);
      centralHeader.writeUInt32LE(0x02014b50, 0);
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
      centralHeader.writeUInt32LE(offset, 42);

      centralParts.push(Buffer.concat([centralHeader, nameBuf]));
      offset += localEntry.length;
    }

    const centralDir = Buffer.concat(centralParts);
    const localSection = Buffer.concat(localParts);

    const endRecord = Buffer.alloc(22);
    endRecord.writeUInt32LE(0x06054b50, 0);
    endRecord.writeUInt16LE(0, 4);
    endRecord.writeUInt16LE(0, 6);
    endRecord.writeUInt16LE(files.length, 8);
    endRecord.writeUInt16LE(files.length, 10);
    endRecord.writeUInt32LE(centralDir.length, 12);
    endRecord.writeUInt32LE(localSection.length, 16);
    endRecord.writeUInt16LE(0, 20);

    return Buffer.concat([localSection, centralDir, endRecord]);
  }

  function createMinimalXlsx(): Buffer {
    const contentTypes = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>`;

    const rootRels = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>`;

    const workbook = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Sheet1" sheetId="1" r:id="rId1"/></sheets></workbook>`;

    const workbookRels = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>`;

    const sheet1 = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData><row r="1"><c r="A1" t="str"><v>id</v></c><c r="B1" t="str"><v>name</v></c></row><row r="2"><c r="A2"><v>1</v></c><c r="B2" t="str"><v>Test Row</v></c></row></sheetData></worksheet>`;

    return createMultiEntryZip([
      { name: '[Content_Types].xml', content: contentTypes },
      { name: '_rels/.rels', content: rootRels },
      { name: 'xl/workbook.xml', content: workbook },
      { name: 'xl/_rels/workbook.xml.rels', content: workbookRels },
      { name: 'xl/worksheets/sheet1.xml', content: sheet1 },
    ]);
  }

  function createMinimalDocx(): Buffer {
    const contentTypes = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/></Types>`;

    const rootRels = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>`;

    const document = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body><w:p><w:r><w:t>Test Row content for upload test</w:t></w:r></w:p></w:body></w:document>`;

    return createMultiEntryZip([
      { name: '[Content_Types].xml', content: contentTypes },
      { name: '_rels/.rels', content: rootRels },
      { name: 'word/document.xml', content: document },
    ]);
  }

  function createFakeHdf5(): Buffer {
    // Real HDF5 signature (8 bytes): \x89 H D F \r \n \x1a \n
    // This lets any magic-byte/content-sniffing on the backend correctly
    // identify the file as HDF5. Everything after the signature is
    // arbitrary filler, NOT a valid superblock — this file will not
    // open in h5py/HDFView. It only exercises the upload pipeline.
    const signature = Buffer.from([0x89, 0x48, 0x44, 0x46, 0x0d, 0x0a, 0x1a, 0x0a]);
    const filler = Buffer.from('Fake HDF5 content for upload test purposes only.', 'utf8');
    return Buffer.concat([signature, filler]);
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

  async function setUp(fileName: string, fileContents: any): Promise<string> {
    const filePath = path.join(os.tmpdir(), fileName);
    // Reuse the file across runs if it already exists
    if (fs.existsSync(filePath)) {
      return filePath;
    }
    await fs.promises.writeFile(filePath, fileContents);
    return filePath;
  }

  type FileTypeConfig = {
    label: string;           // used in describe/test names
    fileName: string;
    mimeType: string;
    content: any;            // string | Buffer, whatever setUp/writeFile accepts
    dragAndDropOnly?: boolean;   // e.g. PDF-only quirks can go here if ever needed
  };

  const fileTypes: FileTypeConfig[] = [
    {
      label: 'TXT',
      fileName: 'test-file.txt',
      mimeType: 'text/plain',
      content: 'TESTING',
    },
    {
      label: 'SQL',
      fileName: 'test-file.sql',
      mimeType: 'application/sql',
      content: `-- Minimal test SQL file
CREATE TABLE test_table (
  id INTEGER PRIMARY KEY,
  name TEXT NOT NULL,
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
INSERT INTO test_table (id, name) VALUES (1, 'Test Row');
`,
    },
    {
      label: 'DOCX',
      fileName: 'test-file.docx',
      mimeType: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
      content: createMinimalDocx(),
    },
    {
      label: 'HDF5',
      fileName: 'test-file.hdf5',
      mimeType: 'application/x-hdf5',
      content: createFakeHdf5(),
    },
    {
      label: 'JSON',
      fileName: 'test-file.json',
      mimeType: 'application/json',
      content: JSON.stringify({
        id: 1,
        name: 'Test Row',
        createdAt: '2024-01-01T00:00:00Z',
      }, null, 2),
    },
    {
      label: 'XML',
      fileName: 'test-file.xml',
      mimeType: 'text/xml',
      content: `<?xml version="1.0" encoding="UTF-8"?>
<testData>
  <record id="1">
    <name>Test Row</name>
    <createdAt>2024-01-01T00:00:00Z</createdAt>
  </record>
</testData>
`,
    },
    {
      label: 'PDF',
      fileName: 'test-file.pdf',
      mimeType: 'application/pdf',
      content: `%PDF-1.4
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
%%EOF`,
    },
    {
      label: 'CSV',
      fileName: 'test-file.csv',
      mimeType: 'text/csv',
      content: 'id,name,email,value,timestamp\n',
    },
    {
      label: 'ZIP',
      fileName: 'test-file.zip',
      mimeType: 'application/zip',
      content: createZip('fileToZip.txt', 'Zipped'),
    },
    {
      label: 'XLSX',
      fileName: 'test-file.xlsx',
      mimeType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      content: createMinimalXlsx(),
    },
  ];

  test.describe('Upload a file', () => {
    for (const fileType of fileTypes) {
      test.describe(`${fileType.label} upload`, () => {
        let filePath: string;

        test.beforeEach(async () => {
          filePath = await setUp(fileType.fileName, fileType.content);
        });

        test.afterEach(async () => {
          if (fs.existsSync(filePath)) {
            fs.unlinkSync(filePath);
          }
        });

        test(`Upload a single ${fileType.label} file using click to browse`, async ({ page }) => {
          await clickToBrowse({ page }, fileType.fileName, filePath);
        });

        test(`Upload a single ${fileType.label} file using drag and drop`, async ({ page }) => {
          await dragAndDrop({ page }, fileType.fileName, filePath, fileType.mimeType);
        });
      });
    }
  });
});