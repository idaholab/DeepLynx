import { expect, Page, APIRequestContext } from "../fixtures";
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import * as zlib from 'zlib';
import { Project } from "@/app/(home)/types/types";

type DataSourceOrStorage = {
    id: string;
    name: string;
    default?: boolean;
};

type Organization = {
    id: number;
    name: string;
};

// ---------------------------------------------------------------------
// Single source of truth for every uploadable file type.
// Add a new format by adding one entry here — no new test blocks needed.
// ---------------------------------------------------------------------
export type FileTypeConfig = {
    label: string;           // used in describe/test names
    fileName: string;
    mimeType: string;
    content: any;            // string | Buffer, whatever setUp/writeFile accepts
    dragAndDropOnly?: boolean;   // e.g. PDF-only quirks can go here if ever needed
};

export async function checkDataSources(page: Page) {
    const dataSourceSelect = page.getByLabel('Data sourceData Sources');
    const selectedText = await dataSourceSelect.locator('option:checked').textContent();

    if (selectedText === 'Data Sources') {
        await dataSourceSelect.selectOption({ index: 1 }); // first real option, skipping the placeholder
    }
}

export async function checkStorageDestinations(page: Page) {
    const storageSelect = page.getByLabel('Storage DestinationObject');
    const selectedText = await storageSelect.locator('option:checked').textContent();

    if (selectedText === 'Object storages') {
        await storageSelect.selectOption({ index: 1 }); // first real option, skipping the placeholder
    }
}
export async function checkDataSourcesAndStorageDestinations(page: Page) {
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

export async function setUp(fileName: string, fileContents: any): Promise<string> {
    const filePath = path.join(os.tmpdir(), fileName);
    // Reuse the file across runs if it already exists
    if (fs.existsSync(filePath)) {
        return filePath;
    }
    await fs.promises.writeFile(filePath, fileContents);
    return filePath;
}

// Resolves an org name (e.g. "PW Org A") to its numeric ID (as a string,
// to match how projectId/URLs are handled throughout this file). We can
// no longer assume orgId === "1" now that fixtures let tests run as
// accounts scoped to arbitrary orgs.
export async function getOrgIdByName(
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

export async function getNonDefault(
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

export async function getNonDefaultProject(
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
export function parseRecordFromUrl(url: string): { recordId: string; projectId: string } | null {
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

export async function deleteRecordIfExists(
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

export async function selectProjectWithRetry(page: Page, projectName: string, maxAttempts = 3) {
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

export async function verifyInProject(page: Page, fileName: string) {
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

export async function dragAndDrop({ page }: { page: Page }, baseFileName: string, filePath: string, type: string, projectNav?: string): Promise<{ recordId: string; projectId: string } | null> {
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

export async function clickToBrowse(
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

export function crc32(buf: Buffer): number {
    let crc = ~0;
    for (const byte of buf) {
        crc ^= byte;
        for (let i = 0; i < 8; i++) {
            crc = (crc >>> 1) ^ (0xedb88320 & -(crc & 1));
        }
    }
    return ~crc >>> 0;
}

export function createZip(fileName: string, content: string): Buffer {
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

export function createMultiEntryZip(files: { name: string; content: string }[]): Buffer {
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

export function createMinimalXlsx(): Buffer {
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

export function createMinimalDocx(): Buffer {
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

export function createFakeHdf5(): Buffer {
    // Real HDF5 signature (8 bytes): \x89 H D F \r \n \x1a \n
    // This lets any magic-byte/content-sniffing on the backend correctly
    // identify the file as HDF5. Everything after the signature is
    // arbitrary filler, NOT a valid superblock — this file will not
    // open in h5py/HDFView. It only exercises the upload pipeline.
    const signature = Buffer.from([0x89, 0x48, 0x44, 0x46, 0x0d, 0x0a, 0x1a, 0x0a]);
    const filler = Buffer.from('Fake HDF5 content for upload test purposes only.', 'utf8');
    return Buffer.concat([signature, filler]);
}

export function createFakeTdms(): Buffer {
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

export async function navigateToProjectDashboard(page: Page) {
    await page.getByTestId("project-select").click();

    await selectProjectWithRetry(page, "PW Project X");

    await expect(page.getByRole('heading', { name: 'Project Overview' })).toBeVisible();
}

export async function extractProjectIdFromURL(page: Page) {
    // Extract project ID from the URL (e.g. /project/42)
    const url = page.url();
    const match = url.match(/\/project\/(\d+)/);
    expect(match).not.toBeNull();
    return match![1];
}

export async function navigateToUploadCenter(page: Page) {
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
}