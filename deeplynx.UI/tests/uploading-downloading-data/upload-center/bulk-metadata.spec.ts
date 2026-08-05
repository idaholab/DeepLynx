import { test, expect } from "../../fixtures";
import { sysAdmin } from "../../deeplynx-config";
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import {
    parseRecordFromUrl, navigateToProjectDashboard, navigateToUploadCenter,
    deleteRecordIfExists, checkDataSourcesAndStorageDestinations, verifyInProject,
    getOrgIdByName
} from "../../helpers/upload-helpers";


const ORG_NAME = "PW Org A";
let orgId: string;

test.describe("Bulk Metadata", () => {

    test.use({
        actingUser: sysAdmin,
        actingOrg: ORG_NAME,
        actingProject: "PW Project X",
    });

    test.beforeEach(async ({ page, request }) => {
        orgId = await getOrgIdByName(request, ORG_NAME);
        await navigateToProjectDashboard(page);
        await navigateToUploadCenter(page);
        await page.getByRole('radio', { name: 'Bulk Metadata' }).click();
        await checkDataSourcesAndStorageDestinations(page);
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
    test.describe("Bulk Metadata -> Default Settings -> Upload csv file with unexpected info", () => {
        let filePath: string;
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
            description = 'Assembly 33 Description';
            original_id = `assy-033-${testInfo.testId}`;
            properties = '{"description":"Assembly 33 Description","diversion flag":false,"height":160,"number of fuel pins":72,"number of heat pipes":19,"temperature":1000}';
            uri = '';
            object_storage_id = '';
            class_id = '';
            class_name = '';
            file_type = '';
            tags = '';
            sensitivity_labels = '';
        });
        test.describe("Upload csv file with missing name", () => {

            test.beforeEach(async ({ }, testInfo) => {
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
                await page.getByRole('button', { name: 'Choose File Button' }).click();
                const fileInput = page.locator('input[type="file"]');
                await fileInput.setInputFiles(filePath);

                await expect(page.getByRole('heading', { name: 'CSV Parsing Errors' })).toBeVisible();

                await expect(
                    page.getByRole('button', { name: /Upload \d+ Records/ })
                ).not.toBeVisible();
            });
        });

        test.describe("Upload csv file with missing description", () => {

            test.beforeEach(async ({ }, testInfo) => {
                const bulkFileName = `bulk-upload-missing-description${testInfo.testId}.csv`;
                filePath = path.join(os.tmpdir(), bulkFileName);

                description = '';

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

            test("[AUTO] [Bulk Metadata>Default Settings] Upload csv file with missing description", async ({ page }) => {

                await page.getByRole('button', { name: 'Choose File Button' }).click();
                const fileInput = page.locator('input[type="file"]');
                await fileInput.setInputFiles(filePath);

                await expect(page.getByRole('heading', { name: 'Validation Errors Found' })).toBeVisible();

                await expect(page.getByRole('heading', { name: 'Error Details:' })).toBeVisible();

                await expect(
                    page.getByRole('button', { name: /Upload \d+ Records/ })
                ).not.toBeVisible();
            });
        });

        test.describe("Upload csv file with missing original_id", () => {

            test.beforeEach(async ({ }, testInfo) => {
                const bulkFileName = `bulk-upload-missing-orig-id${testInfo.testId}.csv`;
                filePath = path.join(os.tmpdir(), bulkFileName);

                original_id = '';

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

            test("[AUTO] [Bulk Metadata>Default Settings] Upload csv file with missing original_id", async ({ page }) => {

                await page.getByRole('button', { name: 'Choose File Button' }).click();
                const fileInput = page.locator('input[type="file"]');
                await fileInput.setInputFiles(filePath);

                await expect(page.getByRole('heading', { name: 'Validation Errors Found' })).toBeVisible();

                await expect(page.getByRole('heading', { name: 'Error Details:' })).toBeVisible();

                await expect(
                    page.getByRole('button', { name: /Upload \d+ Records/ })
                ).not.toBeVisible();
            });
        });

        test.describe("Upload csv file with missing properties", () => {

            test.beforeEach(async ({ }, testInfo) => {
                const bulkFileName = `bulk-upload-missing-props${testInfo.testId}.csv`;
                filePath = path.join(os.tmpdir(), bulkFileName);

                properties = '';

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

            test("[AUTO] [Bulk Metadata>Default Settings] Upload csv file with missing properties", async ({ page }) => {

                await page.getByRole('button', { name: 'Choose File Button' }).click();
                const fileInput = page.locator('input[type="file"]');
                await fileInput.setInputFiles(filePath);

                await expect(page.getByRole('heading', { name: 'Validation Errors Found' })).toBeVisible();

                await expect(page.getByRole('heading', { name: 'Error Details:' })).toBeVisible();

                await expect(
                    page.getByRole('button', { name: /Upload \d+ Records/ })
                ).not.toBeVisible();
            });
        });

        test.describe("Upload csv file with missing optional fields", () => {

            let createdRecord: { recordId: string; projectId: string } | null;

            test.beforeEach(async ({ }, testInfo) => {
                const bulkFileName = `bulk-upload-missing-optional-fields${testInfo.testId}.csv`;
                filePath = path.join(os.tmpdir(), bulkFileName);

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

            test.afterEach(async ({ request }) => {
                await deleteRecordIfExists({ request }, createdRecord, orgId);
            });

            test("[AUTO] [Bulk Metadata>Default Settings] Upload csv file with missing optional fields", async ({ page }) => {

                test.setTimeout(180_000); // buffer time
                const start = Date.now();

                await page.getByRole('button', { name: 'Choose File Button' }).click();
                const fileInput = page.locator('input[type="file"]');
                await fileInput.setInputFiles(filePath);

                await expect(page.getByRole('heading', { name: 'Validation Successful!' })).toBeVisible();

                await expect(
                    page.getByRole('button', { name: /Upload \d+ Records/ })
                ).toBeVisible();

                await page.getByRole('button', { name: /Upload \d+ Records/ }).click();
                await page.getByRole('button', { name: 'Confirm Upload' }).click();

                // Verify in Project Dashboard
                await page.getByRole("link", { name: "Project Dashboard" }).click();
                await page.waitForURL(/\/project/);
                await expect(page.getByRole('heading', { name: 'Project Overview' })).toBeVisible();

                await verifyInProject(page, name);


                const elapsedMs = Date.now() - start;
                expect(elapsedMs).toBeLessThan(120_000);

                // Visit the data catalog and resolve each created record's
                // recordId/projectId so we can clean them up afterward.
                const sideBar = page.getByRole('list').filter({ hasText: /^$/ });
                const dataCatalogButton = sideBar.getByRole('link').nth(1);
                await dataCatalogButton.click();

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
                createdRecord = parseRecordFromUrl(page.url());
            });
        });

        test.describe("Upload csv file with wrong properties format on one record", () => {

            test.beforeEach(async ({ }, testInfo) => {
                const bulkFileName = `bulk-upload-wrong-props${testInfo.testId}.csv`;
                filePath = path.join(os.tmpdir(), bulkFileName);


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
                properties = 'this is: not a json';
                const row2 = [
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

                const fileContent = [header, row1, row2].join('\n');

                await fs.promises.writeFile(filePath, fileContent, 'utf8');
            });

            test.afterAll(async () => {
                if (fs.existsSync(filePath)) {
                    fs.unlinkSync(filePath);
                }
            });

            test("[AUTO] [Bulk Metadata>Default Settings] Upload csv file with wrong format for properties on one record", async ({ page }) => {

                await page.getByRole('button', { name: 'Choose File Button' }).click();
                const fileInput = page.locator('input[type="file"]');
                await fileInput.setInputFiles(filePath);

                await expect(page.getByRole('heading', { name: 'Validation Errors Found' })).toBeVisible();

                await expect(page.getByRole('heading', { name: 'Error Details:' })).toBeVisible();

                await expect(
                    page.getByRole('button', { name: /Upload \d+ Records/ })
                ).not.toBeVisible();
            });
        });

        test.describe("Upload csv file with wrong properties format on multiple record", () => {

            test.beforeEach(async ({ }, testInfo) => {
                const bulkFileName = `bulk-upload-wrong-props${testInfo.testId}.csv`;
                filePath = path.join(os.tmpdir(), bulkFileName);

                properties = 'this is not a json';
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
                properties = 'this is: not a json';
                const row2 = [
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

                properties = '{still: not a json';
                const row3 = [
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

                const fileContent = [header, row1, row2, row3].join('\n');

                await fs.promises.writeFile(filePath, fileContent, 'utf8');
            });

            test.afterAll(async () => {
                if (fs.existsSync(filePath)) {
                    fs.unlinkSync(filePath);
                }
            });

            test("[AUTO] [Bulk Metadata>Default Settings] Upload csv file with wrong format for properties on multiple record", async ({ page }) => {

                await page.getByRole('button', { name: 'Choose File Button' }).click();
                const fileInput = page.locator('input[type="file"]');
                await fileInput.setInputFiles(filePath);

                await expect(page.getByRole('heading', { name: 'Validation Errors Found' })).toBeVisible();

                await expect(page.getByRole('heading', { name: 'Error Details:' })).toBeVisible();

                await expect(
                    page.getByRole('button', { name: /Upload \d+ Records/ })
                ).not.toBeVisible();
            });
        });

        test.describe("Upload csv file with 1000 character x and fails", () => {

            const attributeNames = ["name", "description"] as const;

            attributeNames.forEach(attrName => {

                test.describe(`Upload csv file with 1000 character ${attrName}`, () => {

                    let paddedValue: string;

                    test.beforeEach(async ({ }, testInfo) => {
                        const bulkFileName = `bulk-upload-1000-char-${attrName}${testInfo.testId}.csv`;
                        filePath = path.join(os.tmpdir(), bulkFileName);

                        paddedValue = 'a'.repeat(1000);

                        const row1 = [
                            attrName === 'name' ? paddedValue : name,
                            attrName === 'description' ? paddedValue : description,
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

                    test(`[AUTO] [Bulk Metadata>Default Settings] Upload csv file with 1000 character ${attrName}`, async ({ page }) => {

                        await page.getByRole('button', { name: 'Choose File Button' }).click();
                        const fileInput = page.locator('input[type="file"]');
                        await fileInput.setInputFiles(filePath);

                        await expect(page.getByRole('heading', { name: 'Validation Successful!' })).toBeVisible();

                        await expect(
                            page.getByRole('button', { name: /Upload \d+ Records/ })
                        ).toBeVisible();

                        await page.getByRole('button', { name: /Upload \d+ Records/ }).click();
                        await page.getByRole('button', { name: 'Confirm Upload' }).click();

                        await expect(page.getByRole('heading', { name: 'Upload Failed' })).toBeVisible();
                    });
                });
            });
        });

        test.describe("Upload csv file with 1000 character x and succeeds", () => {

            const attributeNames = ["class_id", "class_name", "original_id", "properties"] as const;

            attributeNames.forEach(attrName => {

                test.describe(`Upload csv file with 1000 character ${attrName}`, () => {

                    let createdRecord: { recordId: string; projectId: string } | null;
                    let paddedValue: string;

                    test.beforeEach(async ({ }, testInfo) => {
                        const bulkFileName = `bulk-upload-1000-char-${attrName}${testInfo.testId}.csv`;
                        filePath = path.join(os.tmpdir(), bulkFileName);

                        attrName !== "properties" ? paddedValue = 'a'.repeat(1000) : paddedValue = '{"' + 'a'.repeat(1000) + '":"' + 'b'.repeat(1000) + '"}';

                        const row1 = [
                            name,
                            description,
                            attrName === "original_id" ? paddedValue : original_id,
                            attrName === "properties" ? paddedValue : properties,
                            uri,
                            object_storage_id,
                            attrName === 'class_id' ? paddedValue : class_id,
                            attrName === 'class_name' ? paddedValue : class_name,
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

                    test.afterEach(async ({ request }) => {
                        await deleteRecordIfExists({ request }, createdRecord, orgId);
                    });

                    test(`[AUTO] [Bulk Metadata>Default Settings] Upload csv file with 1000 character ${attrName}`, async ({ page }) => {

                        test.setTimeout(180_000); // buffer time
                        const start = Date.now();

                        await page.getByRole('button', { name: 'Choose File Button' }).click();
                        const fileInput = page.locator('input[type="file"]');
                        await fileInput.setInputFiles(filePath);

                        await expect(page.getByRole('heading', { name: 'Validation Successful!' })).toBeVisible();

                        await expect(
                            page.getByRole('button', { name: /Upload \d+ Records/ })
                        ).toBeVisible();

                        await page.getByRole('button', { name: /Upload \d+ Records/ }).click();
                        await page.getByRole('button', { name: 'Confirm Upload' }).click();

                        // Verify in Project Dashboard
                        await page.getByRole("link", { name: "Project Dashboard" }).click();
                        await page.waitForURL(/\/project/);
                        await expect(page.getByRole('heading', { name: 'Project Overview' })).toBeVisible();

                        await verifyInProject(page, name);

                        const elapsedMs = Date.now() - start;
                        expect(elapsedMs).toBeLessThan(120_000);

                        const sideBar = page.getByRole('list').filter({ hasText: /^$/ });
                        const dataCatalogButton = sideBar.getByRole('link').nth(1);
                        await dataCatalogButton.click();

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
                        createdRecord = parseRecordFromUrl(page.url());
                    });
                });
            });
        });
    });
});