import { test, expect, Page, APIRequestContext } from "../fixtures";
import { sysAdmin } from "../deeplynx-config";
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import * as zlib from 'zlib';
import { Project } from "@/app/(home)/types/types";

const TEN_GB = 10 * 1024 * 1024 * 1024;
const TWENTY_MIN_MS = 20 * 60 * 1000;

type DataSourceOrStorage = {
  id: string;
  name: string;
  default?: boolean;
};

type Organization = {
  id: number;
  name: string;
};

let projectId: string;
let orgId: string;
const ORG_NAME = "PW Org A";

test.describe("Upload Center", () => {
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

  // Resolves an org name (e.g. "PW Org A") to its numeric ID (as a string,
  // to match how projectId/URLs are handled throughout this file). We can
  // no longer assume orgId === "1" now that fixtures let tests run as
  // accounts scoped to arbitrary orgs.
  async function getOrgIdByName(
    request: APIRequestContext, orgName: string
  ): Promise<string> {
    const BASE_URL = 'http://localhost:5095/api/v1';
    const res = await request.fetch(`${BASE_URL}/organizations`);
    if (!res.ok()) throw new Error(`Failed to fetch organizations: ${res.status()}`);
    const orgs: Organization[] = await res.json();
    const match = orgs.find((org) => org.name === orgName);
    if (!match) {
      throw new Error(`Could not find organization named "${orgName}" in ${JSON.stringify(orgs)}`);
    }
    return String(match.id);
  }

  async function getNonDefault(
    request: APIRequestContext, orgId: string, projectId: string, type: string
  ) {
    if (!projectId) return;
    const BASE_URL = 'http://localhost:5095/api/v1';
    const isDataSource = type === 'data source';
    const getAllUrl = isDataSource
      ? `${BASE_URL}/projects/${projectId}/datasources?hideArchived=true`
      : `${BASE_URL}/organizations/${orgId}/projects/${projectId}/storages?hideArchived=true`;
    const createUrl = isDataSource
      ? `${BASE_URL}/projects/${projectId}/datasources`
      : `${BASE_URL}/organizations/${orgId}/projects/${projectId}/storages?makeDefault=false`
    const FIXED_NAME = isDataSource
      ? "Second data source for playwright tests"
      : "Second storage for playwright tests";
    try {
      for (let attempt = 0; attempt < 5; attempt++) {
        let res = await request.fetch(getAllUrl);
        if (!res.ok()) throw new Error(`Failed to fetch ${type}s: ${res.status()}`);
        const allOfType = await res.json();
        const nonDefault = allOfType.find((singleType: DataSourceOrStorage) => singleType.default !== true);
        if (nonDefault) return nonDefault.name;

        const postRes = isDataSource
          ? await request.post(createUrl, { data: { name: FIXED_NAME } })
          : await request.post(createUrl, { data: { name: FIXED_NAME, config: { mountPath: `../data/duckdb/org_${orgId}/project_${projectId}` } } });

        if (!postRes.ok() && postRes.status() !== 409) {
          throw new Error(`Failed to create new ${type}: ${postRes.status()}`);
        }
      }
      throw new Error(`Could not establish a non-default ${type} after retries`);
    } catch (err) {
      console.warn(`Error getting different ${type}.`, err);
      return undefined;
    }
  }

  async function getNonDefaultProject(
    request: APIRequestContext, orgId: string, projectId: string
  ) {
    if (!projectId) return;
    const BASE_URL = 'http://localhost:5095/api/v1';
    const getAllUrl = `${BASE_URL}/organizations/${orgId}/projects`;
    const createNewUrl = `${BASE_URL}/organizations/${orgId}/projects`;

    try {
      let res = await request.fetch(getAllUrl);
      if (!res.ok()) throw new Error(`Failed to fetch projects: ${res.status()}`);
      let projects = await res.json();
      if (projects.length === 1) {
        // create new of type
        const postRes = await request.post(createNewUrl, { data: { name: "New Project for playwright testing" } });
        if (!postRes.ok()) throw new Error(`Failed to create new project: ${postRes.status()}`);
        res = await request.get(getAllUrl);
        if (!res.ok()) throw new Error(`Failed to refecth project: ${res.status()}`);
        projects = await res.json();
      }
      // return non default
      return (projects.find((project: Project) => project.id != projectId)).name;
    } catch (err) {
      console.warn(`Error getting different project.`, err);
      return undefined;
    }
  }

  // Record page URLs look like: http://localhost:3000/record?recordId=955&projectId=213
  function parseRecordFromUrl(url: string): { recordId: string; projectId: string } | null {
    try {
      const parsed = new URL(url);
      const recordId = parsed.searchParams.get('recordId');
      const projectId = parsed.searchParams.get('projectId');
      if (!recordId || !projectId) return null;
      return { recordId, projectId };
    } catch {
      return null;
    }
  }

  async function deleteRecordIfExists(
    { request }: { request: import('@playwright/test').APIRequestContext },
    record: { recordId: string; projectId: string } | null,
    orgId: string,
  ) {
    if (!record) return;
    const url = `http://localhost:5095/api/v1/organizations/${orgId}/projects/${record.projectId}/records/${record.recordId}`;
    try {
      const response = await request.delete(url);
      if (!response.ok()) {
        console.warn(`Failed to delete record ${record.recordId}: ${response.status()} ${await response.text()}`);
      }
    } catch (err) {
      console.warn(`Error deleting record ${record.recordId}:`, err);
    }
  }

  async function selectProjectWithRetry(page: Page, projectName: string, maxAttempts = 3) {
    for (let attempt = 1; attempt <= maxAttempts; attempt++) {
      const projectButton = page.getByRole("button", { name: projectName, exact: true });
      await expect(projectButton).toBeVisible();
      await expect(projectButton).toBeEnabled();

      try {
        await Promise.all([
          projectButton.click(),
          page.waitForURL(/\/project\/\d+/, { timeout: 15_000 }),
        ]);
        return;
      } catch (e) {
        console.log(`[selectProjectWithRetry] attempt ${attempt} failed, url=${page.url()}`);

        if (attempt === maxAttempts) throw e;

        await page.reload({ waitUntil: 'domcontentloaded' });
        await page.getByTestId("project-select").click();
      }
    }
  }

  async function verifyInProject(page: Page, fileName: string) {
    const nextPage = page.getByRole('button', { name: 'Next page' }).first();
    const pageNumber = page.getByRole('spinbutton', { name: 'Go to page' }).first();
    const file = page.getByText(fileName).first();

    if (await pageNumber.isVisible()) {
      await pageNumber.fill('1');
      await pageNumber.press('Enter');
      await expect(pageNumber).toHaveValue('1');
    }

    while (true) {
      try {
        await expect(file).toBeVisible({ timeout: 1000 });
        return;
      } catch {
        if (page.isClosed() || !(await nextPage.isVisible()) || !(await nextPage.isEnabled())) {
          break;
        }
        const currentPage = Number(await pageNumber.inputValue());
        await nextPage.click();
        await expect(pageNumber).toHaveValue(String(currentPage + 1));
      };
    };
    console.warn(`Could not find "${fileName}" in the project overview.`);
  };

  async function dragAndDrop({ page }: { page: Page }, baseFileName: string, filePath: string, type: string, projectNav?: string): Promise<{ recordId: string; projectId: string } | null> {
    await checkDataSourcesAndStorageDestinations(page);

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

    await page.getByRole('button', { name: 'Upload', exact: true }).click();

    await expect(
      page.getByText('File uploaded successfully!')
    ).toBeVisible();

    if (projectNav) {
      try {
        await page.getByRole('button', { name: projectNav }).first().click();
      } catch {
        await page.getByTestId("project-select").click();
        await page.getByRole('button', { name: projectNav }).first().click();
      }
    }

    // Verify in Project Dashboard
    await page.getByRole("link", { name: "Project Dashboard" }).click();
    await page.waitForURL(/\/project/);
    await expect(page.getByRole('heading', { name: 'Project Overview' })).toBeVisible();
    await verifyInProject(page, fileName);

    const sideBar = page.getByRole('list').filter({ hasText: /^$/ });
    const dataCatalogButton = sideBar.getByRole('link').nth(1);
    await dataCatalogButton.click();

    const recordLink = page.getByRole('link', { name: baseFileName, exact: true }).first();
    for (let attempt = 1; attempt <= 2; attempt++) {
      await page.getByRole('textbox', { name: 'Search' }).click();
      await page.getByRole('textbox', { name: 'Search' }).fill(baseFileName);
      await page.getByRole('textbox', { name: 'Search' }).press('Enter');
      try {
        await expect(page.locator('span').filter({ hasText: baseFileName })).toBeVisible(); // search term success
        await expect(recordLink).toBeVisible(); // file visible
        break;
      } catch (error) {
        if (attempt === 2) {
          throw error;
        }
      }
    }

    // Navigate into the record (data-catalog -> record page) so we can
    // read the recordId/projectId out of the URL for cleanup.
    await recordLink.click();
    await page.waitForURL(/\/record\?/);

    return parseRecordFromUrl(page.url());
  }

  async function clickToBrowse(
    { page }: { page: Page },
    baseFileName: string,
    filePath: string,
    uploadTimeoutMs?: number,
    projectNav?: string,
  ): Promise<{ recordId: string; projectId: string } | null> {
    await checkDataSourcesAndStorageDestinations(page);

    await page.getByRole('button', { name: 'File Upload Drag and Drop Area and Button' }).click();

    const fileInput = page.locator('input[type="file"]');
    await fileInput.setInputFiles(filePath);

    await page.getByRole('button', { name: 'Upload', exact: true }).click();

    await expect(
      page.getByText('File uploaded successfully!')
    ).toBeVisible(uploadTimeoutMs ? { timeout: uploadTimeoutMs } : undefined);

    if (projectNav) {
      try {
        await page.getByRole('button', { name: projectNav }).first().click();
      } catch {
        await page.getByTestId("project-select").click();
        await page.getByRole('button', { name: projectNav }).first().click();
      }
    }

    // Verify in Project Dashboard
    await page.getByRole("link", { name: "Project Dashboard" }).click();
    await page.waitForURL(/\/project/);
    await expect(page.getByRole('heading', { name: 'Project Overview' })).toBeVisible();
    await verifyInProject(page, baseFileName);

    const sideBar = page.getByRole('list').filter({ hasText: /^$/ });
    const dataCatalogButton = sideBar.getByRole('link').nth(1);
    await dataCatalogButton.click();

    const recordLink = page.getByRole('link', { name: baseFileName, exact: true }).first();
    for (let attempt = 1; attempt <= 2; attempt++) {
      await page.getByRole('textbox', { name: 'Search' }).click();
      await page.getByRole('textbox', { name: 'Search' }).fill(baseFileName);
      await page.getByRole('textbox', { name: 'Search' }).press('Enter');
      try {
        await expect(page.locator('span').filter({ hasText: baseFileName })).toBeVisible(); // search term success
        await expect(recordLink).toBeVisible(); // file visible
        break;
      } catch (error) {
        if (attempt === 2) {
          throw error;
        }
      }
    }

    // Navigate into the record (data-catalog -> record page) so we can
    // read the recordId/projectId out of the URL for cleanup.
    await recordLink.click();
    await page.waitForURL(/\/record\?/);

    return parseRecordFromUrl(page.url());
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

  function createFakeTdms(): Buffer {
    // TDMS (National Instruments) lead-in structure — real tag + a
    // structurally-shaped (but not spec-complete) segment header, so
    // magic-byte/content-sniffing on the backend correctly identifies
    // the file as TDMS. This is NOT a valid TDMS file: there is no real
    // metadata section, no channel groups, no raw data index, and no
    // real lead-in values. It will NOT open in NI DIAdem or nptdms.
    // It only exercises the upload pipeline.
    //
    // Real TDMS lead-in is 28 bytes:
    //   [0:4]   tag              "TDSm" (0x54 0x44 0x53 0x6D)
    //   [4:8]   ToC mask         uint32 LE (table-of-contents flags)
    //   [8:12]  version number   uint32 LE (e.g. 4713 for TDMS 2.0)
    //   [12:20] next segment offset   uint64 LE
    //   [20:28] raw data offset       uint64 LE
    const lead = Buffer.alloc(28);
    lead.write('TDSm', 0, 'ascii');       // tag
    lead.writeUInt32LE(0x0e, 4);          // ToC mask (arbitrary nonzero flags)
    lead.writeUInt32LE(4713, 8);          // version number
    lead.writeUInt32LE(0, 12);            // next segment offset (low 32 bits)
    lead.writeUInt32LE(0, 16);            // next segment offset (high 32 bits)
    lead.writeUInt32LE(0, 20);            // raw data offset (low 32 bits)
    lead.writeUInt32LE(0, 24);            // raw data offset (high 32 bits)

    const filler = Buffer.from('Fake TDMS content for upload test purposes only.', 'utf8');
    return Buffer.concat([lead, filler]);
  }

  test.use({
    actingUser: sysAdmin,
    actingOrg: ORG_NAME,
    actingProject: "PW Project X",
  });

  test.beforeEach(async ({ page, request }) => {
    orgId = await getOrgIdByName(request, ORG_NAME);

    await page.getByTestId("project-select").click();

    await selectProjectWithRetry(page, "PW Project X");

    await expect(page.getByRole('heading', { name: 'Project Overview' })).toBeVisible();

    // Extract project ID from the URL (e.g. /project/42)
    const url = page.url();
    const match = url.match(/\/project\/(\d+)/);
    expect(match).not.toBeNull();
    projectId = match![1];
    // Navigate to Upload Center via sidebar
    await page.getByRole('link', { name: "Upload Center", exact: true }).click();
    // Wait for the Upload Center heading to confirm client-side render is done
    try {
      await expect(
        page.getByRole("heading", { name: "Upload Center" }),
      ).toBeVisible({ timeout: 10_000 });
    } catch {
      await page.goto('localhost:3000/upload_center', {
        waitUntil: 'domcontentloaded',
        timeout: 10_000
      });
    }
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
      page.getByLabel("Select a").first()
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
      page.getByRole("button", { name: 'Choose File Button' }),
    ).toBeVisible();
  });

  test.describe('Large file upload', () => {
    let filePath: string;
    const fileName = `${Date.now()}-${Math.random().toString(36).slice(2)}-ten-gb-test-file.bin`;
    let createdRecord: { recordId: string; projectId: string } | null = null;

    test.beforeAll(async () => {
      filePath = path.join(os.tmpdir(), fileName);

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

    test.afterEach(async ({ request }) => {
      await deleteRecordIfExists({ request }, createdRecord, orgId);
      createdRecord = null;
    });

    test('Upload a 10 GB file and verify completion in < 20 minutes', async ({ page }) => {
      await checkDataSourcesAndStorageDestinations(page);

      test.setTimeout(TWENTY_MIN_MS + 60_000); // budget + buffer for setup/assertions

      const start = Date.now();

      createdRecord = await clickToBrowse({ page }, fileName, filePath, TWENTY_MIN_MS);

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

  // ---------------------------------------------------------------------
  // Single source of truth for every uploadable file type.
  // Add a new format by adding one entry here — no new test blocks needed.
  // ---------------------------------------------------------------------
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
      label: 'TDMS',
      fileName: 'test-file.tdms',
      mimeType: 'application/octet-stream',
      content: createFakeTdms(),
    },
    {
      label: 'GZ',
      fileName: 'test-file.gz',
      mimeType: 'application/gzip',
      content: zlib.gzipSync(Buffer.from('id,name,email,value,timestamp\n1,Test Row,test@example.com,42,2024-01-01T00:00:00Z\n', 'utf8')),
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
        let uniqueName: string;
        let filePath: string;
        let createdRecord: { recordId: string; projectId: string } | null = null;

        test.beforeEach(async () => {
          uniqueName = `${Date.now()}-${Math.random().toString(36).slice(2)}-${fileType.fileName}`;
          filePath = await setUp(uniqueName, fileType.content);
          createdRecord = null;
        });

        test.afterEach(async ({ request }) => {
          if (fs.existsSync(filePath)) {
            fs.unlinkSync(filePath);
          }
          await deleteRecordIfExists({ request }, createdRecord, orgId);
        });

        test(`Upload a single ${fileType.label} file using click to browse`, async ({ page }) => {
          createdRecord = await clickToBrowse({ page }, uniqueName, filePath);
        });

        test(`Upload a single ${fileType.label} file using drag and drop`, async ({ page }) => {
          createdRecord = await dragAndDrop({ page }, uniqueName, filePath, fileType.mimeType);
        });
      });
    }
  });

  test.describe("Upload multiple files", () => {
    let filePaths: [string, string, string, string, string];
    const fileBaseNames = [
      `${Date.now()}-${Math.random().toString(36).slice(2)}-upload-test-file-one.bin`,
      `${Date.now()}-${Math.random().toString(36).slice(2)}-upload-test-file-two.bin`,
      `${Date.now()}-${Math.random().toString(36).slice(2)}-upload-test-file-three.bin`,
      `${Date.now()}-${Math.random().toString(36).slice(2)}-upload-test-file-four.bin`,
      `${Date.now()}-${Math.random().toString(36).slice(2)}-upload-test-file-five.bin`,
    ] as const;
    let createdRecords: ({ recordId: string; projectId: string } | null)[] = [];

    test.beforeEach(async ({ page }) => {

      // Create the files locally
      filePaths = fileBaseNames.map((name) => path.join(os.tmpdir(), name)) as [string, string, string, string, string];

      for (const filePath of filePaths) {
        if (!fs.existsSync(filePath) || fs.statSync(filePath).size !== 400 * 1024 * 1024) {
          await fs.promises.writeFile(filePath, Buffer.alloc(1));
          await fs.promises.truncate(filePath, 400 * 1024 * 1024);
        }
      }

      createdRecords = [];
    });

    test.afterAll(async () => {
      for (const filePath in filePaths) {
        if (fs.existsSync(filePath)) {
          fs.unlinkSync(filePath);
        }
      }
    });

    test.afterEach(async ({ request }) => {
      for (const record of createdRecords) {
        await deleteRecordIfExists({ request }, record, orgId);
      }
    });

    test("uploads multiple files", async ({ page }) => {
      test.setTimeout(180_000); // three minutes buffer time

      // Upload the files
      await page.getByRole('link', { name: "Upload Center", exact: true }).click();
      await page.waitForURL(/\/upload_center/);
      await expect(page.getByRole("heading", { name: "Upload Center" })).toBeVisible();

      await checkDataSourcesAndStorageDestinations(page);

      await page.getByRole('button', { name: 'File Upload Drag and Drop Area and Button' }).click();
      const fileInput = page.locator('input[type="file"]');
      await fileInput.setInputFiles(filePaths);
      await page.getByRole('button', { name: 'Upload', exact: true }).click();
      await expect(page.getByText('Uploaded 5 file(s)')).toBeVisible({
        timeout: 120_000,
      });

      // Verify in Project Dashboard
      await page.getByRole("link", { name: "Project Dashboard" }).click();
      await page.waitForURL(/\/project/);
      await expect(page.getByRole('heading', { name: 'Project Overview' })).toBeVisible();
      for (const fileName of fileBaseNames) {
        await verifyInProject(page, fileName);
      }

      // Visit the data catalog and resolve each uploaded file to its
      // recordId/projectId so we can clean them up afterward.
      const sideBar = page.getByRole('list').filter({ hasText: /^$/ });
      const dataCatalogButton = sideBar.getByRole('link').nth(1);
      await dataCatalogButton.click();

      for (const baseName of fileBaseNames) {
        const clearTermsButton = page.getByRole('button', { name: 'Clear search' });

        const recordLink = page.getByRole('link', { name: baseName, exact: true }).first();
        await expect(async () => {
          if (await clearTermsButton.isVisible()) {
            await clearTermsButton.click();
          }
          await page.getByRole('textbox', { name: 'Search' }).click();
          await page.getByRole('textbox', { name: 'Search' }).fill(baseName);
          await page.getByRole('textbox', { name: 'Search' }).press('Enter');
          await expect(recordLink).toBeVisible({ timeout: 3_000 });
        }).toPass({ timeout: 30_000 });

        await recordLink.click();
        await page.waitForURL(/\/record\?/);
        createdRecords.push(parseRecordFromUrl(page.url()));

        // Go back to the catalog to search for the next file
        await page.goBack();
      }
    });
  });

  test.describe('Empty file upload', () => {
    const emptyFileName = `${Date.now()}-${Math.random().toString(36).slice(2)}-empty-test-file.txt`;
    let filePath: string;

    test.beforeEach(async () => {
      filePath = path.join(os.tmpdir(), emptyFileName);
      await fs.promises.writeFile(filePath, Buffer.alloc(0)); // zero-byte file
    });

    test.afterEach(async () => {
      if (fs.existsSync(filePath)) {
        fs.unlinkSync(filePath);
      }
    });

    test('Uploading an empty file fails with a notification and no record is created', async ({ page }) => {
      // Keep default Project/Data Source settings — do not call
      await checkDataSourcesAndStorageDestinations(page);

      await page.getByRole('button', { name: 'File Upload Drag and Drop Area and Button' }).click();

      const fileInput = page.locator('input[type="file"]');
      await fileInput.setInputFiles(filePath);

      await page.getByRole('button', { name: 'Upload', exact: true }).click();

      // TODO: replace with the actual failure notification text/selector
      await expect(
        page.getByText('Upload failed')
      ).toBeVisible();

      // No success notification should appear
      await expect(
        page.getByText('File uploaded successfully!')
      ).not.toBeVisible();

      // No folder/record should show on the Project Dashboard
      await page.getByRole('link', { name: 'Project Dashboard' }).click();
      await expect(
        page.getByText(emptyFileName)
      ).not.toBeVisible();

      await page.getByRole('link', { name: 'Visit' }).first().click();
      await page.getByRole('textbox', { name: 'Search' }).click();
      await page.getByRole('textbox', { name: 'Search' }).fill(emptyFileName);
      await page.getByRole('textbox', { name: 'Search' }).press('Enter');

      await expect(
        page.getByRole('link', { name: emptyFileName, exact: true }).first()
      ).not.toBeVisible();
    });
  });

  test.describe("Upload bulk records", () => {
    let filePath: string;
    let createdRecords: ({ recordId: string; projectId: string } | null)[] = [];
    const file1 = `${Date.now()}-${Math.random().toString(36).slice(2)}-bulk-test-1`;
    const id1 = `${Date.now()}-${Math.random().toString(36).slice(2)}-bulk-test-1-id`;
    const file2 = `${Date.now()}-${Math.random().toString(36).slice(2)}-bulk-test-2`;
    const id2 = `${Date.now()}-${Math.random().toString(36).slice(2)}-bulk-test-2-id`;
    const file3 = `${Date.now()}-${Math.random().toString(36).slice(2)}-bulk-test-3`;
    const id3 = `${Date.now()}-${Math.random().toString(36).slice(2)}-bulk-test-3-id`;
    const file4 = `${Date.now()}-${Math.random().toString(36).slice(2)}-bulk-test-4`;
    const id4 = `${Date.now()}-${Math.random().toString(36).slice(2)}-bulk-test-4-id`;
    const file5 = `${Date.now()}-${Math.random().toString(36).slice(2)}-bulk-test-5`;
    const id5 = `${Date.now()}-${Math.random().toString(36).slice(2)}-bulk-test-5-id`;
    const bulkFileName = `${Date.now()}-${Math.random().toString(36).slice(2)}-bulk-upload.csv`;

    test.beforeEach(async ({ }) => {
      // Create the file locally
      filePath = path.join(os.tmpdir(), bulkFileName);

      const fileContent = [
        'name (required),description (required),original_id (required),properties (required - JSON format),uri (optional),object_storage_id (optional),class_id (optional),class_name (optional),file_type (optional),tags (optional - comma-separated),sensitivity_labels (optional - comma-separated)',
        `${file1},A test file for bulk upload testing,${id1},"{""created"":""June 2026"",""candy"":""smarties"",""color"":""red""}",,,,,txt,,`,
        `${file2},A test file for bulk upload testing,${id2},"{""created"":""July 2026"",""candy"":""M&Ms"",""color"":""yellow""}",,,,,pdf,,`,
        `${file3},A test file for bulk upload testing,${id3},"{""created"":""July 2026"",""candy"":""skittles"",""color"":""purple""}",,,,,docx,,`,
        `${file4},A test file for bulk upload testing,${id4},"{""created"":""July 2026"",""chips"":""takis"",""spice"":""extreme""}",,,,,txt,,`,
        `${file5},A test file for bulk upload testing,${id5},"{""created"":""July 2026"",""cookies"":""oreos"",""type"":""birthday cake""}",,,,,json,,`
      ].join('\n');

      await fs.promises.writeFile(filePath, fileContent, 'utf8');
      createdRecords = [];
    });

    test.afterAll(async () => {
      if (fs.existsSync(filePath)) {
        fs.unlinkSync(filePath);
      }
    });

    test.afterEach(async ({ request }) => {
      for (const record of createdRecords) {
        await deleteRecordIfExists({ request }, record, orgId);
      }
    });

    test("uploads bulk records via a CSV", async ({ page }) => {
      test.setTimeout(180_000); // buffer time
      const start = Date.now();

      const bulkFileNames = [
        file1,
        file2,
        file3,
        file4,
        file5,
      ];

      // Upload the csv file
      await page.getByRole('radio', { name: 'Bulk Metadata' }).click();

      await checkDataSourcesAndStorageDestinations(page);

      await page.getByRole('button', { name: 'Choose File Button' }).click();
      const fileInput = page.locator('input[type="file"]');
      await fileInput.setInputFiles(filePath);
      await expect(page.getByText('Validation Successful!')).toBeVisible();
      await page.getByRole('button', { name: 'Upload 5 Records' }).click();
      await page.getByRole('button', { name: 'Confirm Upload' }).click();
      await expect(page.getByText('Successfully uploaded 5 Records!')).toBeVisible({
        timeout: 60_000,
      });

      // Verify in Project Dashboard
      await page.getByRole("link", { name: "Project Dashboard" }).click();
      await page.waitForURL(/\/project/);
      await expect(page.getByRole('heading', { name: 'Project Overview' })).toBeVisible();
      for (const fileName of bulkFileNames) {
        await verifyInProject(page, fileName);
      }

      const elapsedMs = Date.now() - start;
      expect(elapsedMs).toBeLessThan(120_000);

      // Visit the data catalog and resolve each created record's
      // recordId/projectId so we can clean them up afterward.
      const sideBar = page.getByRole('list').filter({ hasText: /^$/ });
      const dataCatalogButton = sideBar.getByRole('link').nth(1);
      await dataCatalogButton.click();

      for (const name of bulkFileNames) {
        const clearTermsButton = page.getByRole('button', { name: 'Clear search' });

        const recordLink = page.getByRole('link', { name, exact: true }).first();
        await expect(async () => {
          if (await clearTermsButton.isVisible()) {
            await clearTermsButton.click();
          }
          await page.getByRole('textbox', { name: 'Search' }).click();
          await page.getByRole('textbox', { name: 'Search' }).fill(name);
          await page.getByRole('textbox', { name: 'Search' }).press('Enter');
          await expect(recordLink).toBeVisible({ timeout: 3_000 });
        }).toPass({ timeout: 30_000 });

        await recordLink.click();
        await page.waitForURL(/\/record\?/);
        createdRecords.push(parseRecordFromUrl(page.url()));

        // Go back to the catalog to search for the next record
        await page.goBack();
      }
    });
  });

  test.describe("Upload timeseries file", () => {
    const fileName: string = `${Date.now()}-${Math.random().toString(36).slice(2)}-timeseries-test-file.csv`;
    let filePath: string;
    let createdRecord: { recordId: string; projectId: string } | null = null;

    test.beforeEach(async () => {
      // Create the file locally
      filePath = path.join(os.tmpdir(), fileName);
      const csvContent = 'Date, Candy Eaten\n20050101, 5\n20050102, 6\n20050103, 3\n20050104, 15\n20050105, 3\n20050106, 5\n20050107, 9\n20050108, 12\n20050109, 6\n20050110, 9\n20050111, 6\n20050112, 3\n20050113, 2';

      if (!fs.existsSync(filePath)) {
        await fs.promises.writeFile(filePath, csvContent, 'utf8');
      }
      createdRecord = null;
    });

    test.afterAll(async () => {
      if (fs.existsSync(filePath)) {
        fs.unlinkSync(filePath);
      }
    });

    test.afterEach(async ({ request }) => {
      await deleteRecordIfExists({ request }, createdRecord, orgId);
      createdRecord = null;
    });

    test("uploads a timeseries file", async ({ page }) => {
      test.setTimeout(120_000); // two minutes buffer time

      createdRecord = await clickToBrowse({ page }, fileName, filePath);

      // Check that it shows up on the timeseries page
      await page.getByRole("link", { name: "Timeseries Viewer" }).click();
      await page.waitForURL(/\/timeseries_viewer/);
      await expect(page.getByRole("heading", { name: "Timeseries Viewer" })).toBeVisible();
      await expect(page.getByText(fileName).first()).toBeVisible();
      await page.getByRole('link', { name: fileName }).first().click();

      await expect(page.locator('canvas')).toBeVisible();
      await expect(page.locator('span').filter({ hasText: fileName })).toBeVisible();
    });
  });

  test.describe("Data Source and Storage uploads", () => {
    let filePaths: [string, string, string, string, string, string];
    let tmpDir: string;
    const differentDatasourceClickName = `${Date.now()}-${Math.random().toString(36).slice(2)}-upload-different-datasource-click`;
    const differentDatasourceDragName = `${Date.now()}-${Math.random().toString(36).slice(2)}-upload-different-datasource-drag`;
    const differentStorageClickName = `${Date.now()}-${Math.random().toString(36).slice(2)}-upload-different-storage-click`;
    const differentStorageDragName = `${Date.now()}-${Math.random().toString(36).slice(2)}-upload-different-storage-drag`;
    const differentProjectClickName = `${Date.now()}-${Math.random().toString(36).slice(2)}-upload-different-project-click`;
    const differentProjectDragName = `${Date.now()}-${Math.random().toString(36).slice(2)}-upload-different-project-drag`;

    test.beforeAll(async ({ }, workerInfo) => {
      tmpDir = await fs.promises.mkdtemp(
        path.join(os.tmpdir(), `upload-tests-${workerInfo.workerIndex}-`)
      );

      // Create the files to use locally
      filePaths = [
        path.join(tmpDir, differentDatasourceClickName),
        path.join(tmpDir, differentDatasourceDragName),
        path.join(tmpDir, differentStorageClickName),
        path.join(tmpDir, differentStorageDragName),
        path.join(tmpDir, differentProjectClickName),
        path.join(tmpDir, differentProjectDragName)
      ];

      await Promise.all(
        filePaths.map(filePath =>
          fs.promises.writeFile(filePath, Buffer.alloc(1))
        )
      );
    });

    test.afterAll(async () => {
      await fs.promises.rm(tmpDir, {
        recursive: true,
        force: true,
      });
    });

    test("default project and storage, nondefault data source, click to browse, successfully uploads file", async ({ page, request }) => {
      // set datasource and storage destination
      await checkStorageDestinations(page);
      const nondefaultDs = await getNonDefault(request, orgId, projectId, 'data source');
      const dataSourceSelect = page.getByLabel('Data sourceData');
      await expect(dataSourceSelect).toBeEnabled();
      const option = dataSourceSelect.locator('option', { hasText: nondefaultDs });
      await expect(option).toBeAttached({ timeout: 15_000 });
      await dataSourceSelect.selectOption(nondefaultDs);

      // click to browse
      await clickToBrowse({ page }, differentDatasourceClickName, filePaths[0]);
    });

    test("default project and storage, nondefault data source, drag and drop, successfully uploads file", async ({ page, request }) => {
      // set datasource and storage destination
      await checkStorageDestinations(page);
      const nondefaultDs = await getNonDefault(request, orgId, projectId, 'data source');
      const dataSourceSelect = page.getByLabel('Data sourceData');
      await expect(dataSourceSelect).toBeEnabled();
      const option = dataSourceSelect.locator('option', { hasText: nondefaultDs });
      await expect(option).toBeAttached({ timeout: 15_000 });
      await dataSourceSelect.selectOption(nondefaultDs);

      // click to browse
      await dragAndDrop({ page }, differentDatasourceDragName, filePaths[1], 'txt');
    });

    test("default project and data source, nondefault storage, click to browse, successfully uploads file", async ({ page, request }) => {
      // set datasource and storage destination
      await checkDataSources(page);
      const nondefaultOs = await getNonDefault(request, orgId, projectId, 'storage');
      const objectStorageSelect = page.getByLabel('Storage DestinationObject');
      await expect(objectStorageSelect).toBeEnabled();
      const option = objectStorageSelect.locator('option', { hasText: nondefaultOs });
      await expect(option).toBeAttached({ timeout: 15_000 });
      await objectStorageSelect.selectOption(nondefaultOs);

      // click to browse
      await clickToBrowse({ page }, differentStorageClickName, filePaths[2]);
    });

    test("default project and data source, nondefault storage, drag and drop, successfully uploads file", async ({ page, request }) => {
      // set datasource and storage destination
      await checkDataSources(page);
      const nondefaultOs = await getNonDefault(request, orgId, projectId, 'storage');
      const objectStorageSelect = page.getByLabel('Storage DestinationObject');
      await expect(objectStorageSelect).toBeEnabled();
      const option = objectStorageSelect.locator('option', { hasText: nondefaultOs });
      await expect(option).toBeAttached({ timeout: 15_000 });
      await objectStorageSelect.selectOption(nondefaultOs);

      // click to browse
      await dragAndDrop({ page }, differentStorageDragName, filePaths[3], 'txt');
    });

    test("default data source and storage, nondefault project, click to browse, successfully uploads file", async ({ page, request }) => {
      // project setup
      const nondefaultProj = await getNonDefaultProject(request, orgId, projectId);
      const projectSelect = page.getByRole('combobox', { name: /project/i }).first();
      await expect(projectSelect).toBeEnabled();
      await projectSelect.selectOption(nondefaultProj);

      // set datasource and storage destination
      await checkDataSourcesAndStorageDestinations(page);

      // click to browse
      await clickToBrowse({ page }, differentProjectClickName, filePaths[4], undefined, nondefaultProj);
    });

    test("default data source and storage, nondefault project, drag and drop, successfully uploads file", async ({ page, request }) => {
      // project setup
      const nondefaultProj = await getNonDefaultProject(request, orgId, projectId);
      const projectSelect = page.getByRole('combobox', { name: /project/i }).first();
      await expect(projectSelect).toBeEnabled();
      await projectSelect.selectOption(nondefaultProj);

      // set datasource and storage destination
      await checkDataSourcesAndStorageDestinations(page);

      // drag and drop
      await dragAndDrop({ page }, differentProjectDragName, filePaths[5], 'txt', nondefaultProj);
    });
  });
  test.describe("Bulk Metadata -> Default Settings -> Upload csv file with unexpected info", () => {
    let filePath: string;
    let row: string;
    let name: string;
    let description: string;
    let original_id: string;
    let properties: string;
    let uri: string;
    let object_storage_id: string;
    let class_id: string;
    let class_name: string;
    let file_type: string;
    let tags: string;
    let sensitivity_labels: string;

    const header = [
      'name (required)',
      'description (required)',
      'original_id (required)',
      'properties (required - JSON format)',
      'uri (optional)',
      'object_storage_id (optional)',
      'class_id (optional)',
      'class_name (optional)',
      'file_type (optional)',
      'tags (optional - comma-separated)',
      'sensitivity_labels (optional - comma-separated)',
    ].join('\t');

    test.beforeEach(async ({ }, testInfo) => {
      name = `Assembly 33-${testInfo.testId}`;
      description = `Assembly 33 Description`;
      original_id = `assy-033-${testInfo.testId}`;
      properties = `{"description":"Assembly 33 Description","diversion flag":false,"height":160,"number of fuel pins":72,"number of heat pipes":19,"temperature":1000}`;
      uri = `https://example.com/assembly/33`;
      object_storage_id = `1`;
      class_id = `5`;
      class_name = `FuelAssembly`;
      file_type = `csv`;
      tags = `monitoring,critical,assembly`;
      sensitivity_labels = `public,internal`;
    });
    test.describe("Upload csv file with missing name", () => {

      test.beforeEach(async ({ }, testInfo) => {
        // Tab-separated, matching the real template. Row 2 has an empty
        // name field (required) to trigger a validation failure.
        const bulkFileName = `bulk-upload-missing-name${testInfo.testId}.csv`;
        filePath = path.join(os.tmpdir(), bulkFileName);

        name = '';

        const row1 = [
          name,
          description,
          original_id,
          properties,
          uri,
          object_storage_id,
          class_id,
          class_name,
          file_type,
          tags,
          sensitivity_labels
        ].join('\t');

        const fileContent = [header, row1].join('\n');

        await fs.promises.writeFile(filePath, fileContent, 'utf8');
      });

      test.afterAll(async () => {
        if (fs.existsSync(filePath)) {
          fs.unlinkSync(filePath);
        }
      });

      test("[AUTO] [Bulk Metadata>Default Settings] Upload csv file with missing name", async ({ page }) => {
        // Switch to Bulk Metadata mode, keep default project/data source/storage settings
        await page.getByRole('radio', { name: 'Bulk Metadata' }).click();

        await checkDataSourcesAndStorageDestinations(page);

        await page.getByRole('button', { name: 'Choose File Button' }).click();
        const fileInput = page.locator('input[type="file"]');
        await fileInput.setInputFiles(filePath);

        await expect(page.getByRole('heading', { name: 'CSV Parsing Errors' })).toBeVisible();

        await expect(
          page.getByRole('button', { name: /Upload \d+ Records/ })
        ).not.toBeVisible();
      });
    });
  });
});