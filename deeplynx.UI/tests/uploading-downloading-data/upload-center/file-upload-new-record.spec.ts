import { test, expect } from "../../fixtures";
import { sysAdmin, ORGS, PROJECTS } from "../../deeplynx-config";
import { randomBytes } from "crypto";
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import * as zlib from 'zlib';
import {
    extractProjectIdFromURL, getOrgIdByName, navigateToUploadCenter,
    deleteRecordIfExists, checkDataSourcesAndStorageDestinations, clickToBrowse, FileTypeConfig, createFakeHdf5, createFakeTdms,
    createMinimalDocx, createMinimalXlsx, createZip, setUp, dragAndDrop, verifyInProject, parseRecordFromUrl, getNonDefaultProject,
    getNonDefault, checkDataSources, checkStorageDestinations, CreatedMetadata, toSafeFileName, getClass, buildMetadata
} from "../../helpers/upload-helpers";

const TEN_GB = 10 * 1024 * 1024 * 1024;
const TWENTY_MIN_MS = 20 * 60 * 1000;



let projectId: string;
let orgId: string;
const ORG_NAME = ORGS.orgA;

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

test.describe("File Upload -> New Record", () => {

    test.use({
        actingUser: sysAdmin,
        actingOrg: ORG_NAME,
        actingProject: PROJECTS.projectX,
    });

    test.beforeAll(async ({ page, request }) => {
        orgId = await getOrgIdByName(request, ORGS.orgA.name);
        projectId = await extractProjectIdFromURL(page);
    });

    test.beforeEach(async ({ page }) => {
        await navigateToUploadCenter(page);
    })

    test.describe('Upload a file', () => {
        for (const fileType of fileTypes) {
            test.describe(`${fileType.label} upload`, () => {
                let uniqueName: string;
                let filePath: string;
                let createdRecord: { recordId: string; projectId: string } | null = null;

                test.beforeEach(async ({}, testInfo) => {
                    uniqueName = `${testInfo.testId}-${fileType.fileName}`;
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
    test.describe('Metadata file uploads', () => {
        let usableClass: { id: number; name: string; };
        test.beforeAll(async ({ request }) => {
        usableClass = await getClass(request, projectId);
        })

        // Source of truth -- error type tells us if/where the error will show up, message is more information about the error or what should happen
        const scenarios: {
            name: string;
            error: { type: string, message: string };
            options: (cls: typeof usableClass) => CreatedMetadata;
        }[] = [
            { name: 'normal', error: {type: 'none', message: ''}, options: (cls: typeof usableClass) => ({Name: `${Math.random().toString(36).slice(2)}-normal-metadata-${Date.now()}.json`, Description: "What is metadata? And what does it truly mean?", OriginalId: `A random number ${Math.random().toString(36).slice(2)}`, ClassId: cls.id, ClassName: cls.name, Properties: {"background color": "white"}}) },
            { name: 'exclude-name', error: {type: 'preview', message: 'Name: Invalid input: expected string, received undefined'}, options: (cls: typeof usableClass) => ({Description: "What is metadata? And what does it truly mean?", OriginalId: `A random number ${Math.random().toString(36).slice(2)}`, ClassId: cls.id, ClassName: cls.name, Properties: {"background color": "white"}}) },
            { name: 'exclude-description', error: {type: 'preview', message: 'Description: Invalid input: expected string, received undefined'}, options: (cls: typeof usableClass) => ({Name: `${Math.random().toString(36).slice(2)}-excluding-description-${Date.now()}.json`, OriginalId: `A random number ${Math.random().toString(36).slice(2)}`, ClassId: cls.id, ClassName: cls.name, Properties: {"background color": "white"}}) },
            { name: 'exclude-original-id', error: {type: 'none', message: ''}, options: (cls: typeof usableClass) => ({Name: `${Math.random().toString(36).slice(2)}-excluding-original-id-${Date.now()}.json`, Description: "What is metadata? And what does it truly mean?", ClassId: cls.id, ClassName: cls.name, Properties: {"background color": "white"}}) },
            { name: 'exclude-class-name', error: {type: 'none', message: 'class info missing'}, options: (cls: typeof usableClass) => ({Name: `${Math.random().toString(36).slice(2)}-normal-metadata-${Date.now()}.json`, Description: "What is metadata? And what does it truly mean?", OriginalId: `A random number ${Math.random().toString(36).slice(2)}`, ClassId: cls.id, Properties: {"background color": "white"}}) },
            { name: 'exclude-class-id', error: {type: 'none', message: 'class info missing'}, options: (cls: typeof usableClass) => ({Name: `${Math.random().toString(36).slice(2)}-normal-metadata-${Date.now()}.json`, Description: "What is metadata? And what does it truly mean?", OriginalId: `A random number ${Math.random().toString(36).slice(2)}`, ClassName: cls.name, Properties: {"background color": "white"}}) },
            { name: 'exclude-properties', error: {type: 'preview', message: 'Properties: Invalid input'}, options: (cls: typeof usableClass) => ({Name: `${Math.random().toString(36).slice(2)}-normal-metadata-${Date.now()}.json`, Description: "What is metadata? And what does it truly mean?", OriginalId: `A random number ${Math.random().toString(36).slice(2)}`, ClassId: cls.id, ClassName: cls.name}) },
            { name: '1000-character-name', error: {type: 'exception', message: "The field Name must be a string or array type with a maximum length of '500'."}, options: (cls: typeof usableClass) => ({Name: `${randomBytes(500).toString('hex')}.json`, Description: "What is metadata? And what does it truly mean?", OriginalId: `A random number ${Math.random().toString(36).slice(2)}`, ClassId: cls.id, ClassName: cls.name, Properties: {"background color": "white"}}) },
            { name: '1000-character-description', error: {type: 'exception', message: "The field Description must be a string or array type with a maximum length of '250'."}, options: (cls: typeof usableClass) => ({Name: `${Math.random().toString(36).slice(2)}-normal-metadata-${Date.now()}.json`, Description: randomBytes(500).toString('hex'), OriginalId: `A random number ${Math.random().toString(36).slice(2)}`, ClassId: cls.id, ClassName: cls.name, Properties: {"background color": "white"}}) },
            { name: '1000-character-id', error: {type: 'none', message: 'long id'}, options: (cls: typeof usableClass) => ({Name: `${Math.random().toString(36).slice(2)}-long-id-${Date.now()}.json`, Description: "What is metadata? And what does it truly mean?", OriginalId: randomBytes(500).toString('hex'), ClassId: cls.id, ClassName: cls.name, Properties: {"background color": "white"}}) },
            { name: '1000-character-properties', error: {type: 'none', message: 'long properties'}, options: (cls: typeof usableClass) => ({Name: `${Math.random().toString(36).slice(2)}-normal-metadata-${Date.now()}.json`, Description: "What is metadata? And what does it truly mean?", OriginalId: `A random number ${Math.random().toString(36).slice(2)}`, ClassId: cls.id, ClassName: cls.name, Properties: {[randomBytes(500).toString('hex')] : randomBytes(500).toString('hex')}}) },
        ]
            for (const scenario of scenarios) {
                let filePath: string;
                let fileName: string;
                let metadata: CreatedMetadata;
                let metadataPath: string;
                let options: CreatedMetadata;
                let createdRecord: { recordId: string; projectId: string } | null = null;

                test.beforeEach(async ({}, testInfo) => {
                    const fileContent = "The cow jumped over the moon.";
                    fileName = `${testInfo.testId}-metadata-${scenario.name}`;
                    filePath = await setUp(fileName, fileContent);
                    createdRecord = null;

                    options = scenario.options(usableClass);
                    metadata = await buildMetadata(options);
                    const rawName = options.Name ?? `${testInfo.testId}-missing-name-${scenario.name}.json`;
                    const metadataFileName = toSafeFileName(rawName);
                    metadataPath = await setUp(metadataFileName, JSON.stringify(metadata, null, 2));
                });

                test.afterEach(async ({ request }) => {
                    if (fs.existsSync(filePath)) { 
                    fs.unlinkSync(filePath);
                    }
                    if (fs.existsSync(metadataPath)) {
                    fs.unlinkSync(metadataPath);
                    }
                    await deleteRecordIfExists({ request }, createdRecord, orgId);
                });

                test(`metadata upload: ${scenario.name}`, async ({ page }) => {
                    createdRecord = await clickToBrowse({ page }, fileName, filePath, undefined, undefined, metadataPath, scenario.error, options, usableClass);
                });
        }
    });
});