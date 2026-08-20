import { test, expect } from "../../fixtures";
import { sysAdmin, ORGS, PROJECTS } from "../../deeplynx-config";
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import {
    extractProjectIdFromURL, getOrgIdByName, navigateToUploadCenter,
    deleteRecordIfExists, checkDataSourcesAndStorageDestinations, clickToBrowse, FileTypeConfig,
    setUp, verifyInProject, parseRecordFromUrl
} from "../../helpers/upload-helpers";

let projectId: string;
let orgId: string;

const fileType: FileTypeConfig =
{
    label: 'JSON',
    fileName: 'test-file.json',
    mimeType: 'application/json',
    content: JSON.stringify({

        Name: "",
        Description: "Describe this file",
        OriginalId: "unique-original-id",
        ClassId: "akdlfj",
        Properties: {
            exampleKey: "exampleValue"
        }

    }, null, 2),
};

test.describe("File Upload -> Update Existing Record", () => {

    test.use({
        actingUser: sysAdmin,
        actingOrg: ORGS.orgA,
        actingProject: PROJECTS.projectX,
    });

    test.beforeEach(async ({ page, request }) => {
        orgId = await getOrgIdByName(request, ORGS.orgA.name);
        projectId = await extractProjectIdFromURL(page);
        await navigateToUploadCenter(page);
    });


    test.describe("Upload timeseries file then update said file's metadata", () => {
        const fileName: string = `${Date.now()}-${Math.random().toString(36).slice(2)}-timeseries-test-file.csv`;
        const fileNameMetadata: string = `${Date.now()}-${Math.random().toString(36).slice(2)}-timeseries-test-file-metadata.json`;
        let filePath: string;
        let filePathMetadata: string;
        let createdRecord: { recordId: string; projectId: string } | null = null;
        let createdRecordMetadata: { recordId: string; projectId: string } | null = null;

        test.beforeEach(async () => {
            // Create the file locally
            filePath = path.join(os.tmpdir(), fileName);
            const csvContent = 'Date, Candy Eaten\n20050101, 5\n20050102, 6\n20050103, 3\n20050104, 15\n20050105, 3\n20050106, 5\n20050107, 9\n20050108, 12\n20050109, 6\n20050110, 9\n20050111, 6\n20050112, 3\n20050113, 2';

            if (!fs.existsSync(filePath)) {
                await fs.promises.writeFile(filePath, csvContent, 'utf8');
            }
            createdRecord = null;
            createdRecordMetadata = null;

            filePathMetadata = await setUp(fileNameMetadata, fileType.content);
        });

        test.afterAll(async () => {
            if (fs.existsSync(filePath)) {
                fs.unlinkSync(filePath);
            }
            if (fs.existsSync(filePathMetadata)) {
                fs.unlinkSync(filePathMetadata);
            }
        });

        test.afterEach(async ({ request }) => {
            await deleteRecordIfExists({ request }, createdRecord, orgId);
            await deleteRecordIfExists({ request }, createdRecordMetadata, orgId);
            createdRecord = null;
            createdRecordMetadata = null;
        });

        test("update with invalid metadata", async ({ page }) => {
            test.setTimeout(120_000); // two minutes buffer time

            createdRecord = await clickToBrowse({ page }, fileName, filePath);
            await navigateToUploadCenter(page);

            await expect(page.getByRole('button', { name: 'File Upload Drag and Drop' })).toBeVisible();
            await checkDataSourcesAndStorageDestinations(page);

            await page.getByRole('button', { name: 'File Upload Drag and Drop Area and Button' }).click();

            const fileInput = page.locator('input[type="file"]');
            await fileInput.setInputFiles(filePath);

            await expect(page.getByRole('radio', { name: 'Update Existing Record' })).toBeVisible();
            await page.getByRole('radio', { name: 'Update Existing Record' }).click();

            await expect(page.getByRole('button', { name: fileName })).toBeVisible();
            await page.getByRole('button', { name: fileName }).click();

            await page.getByRole('button', { name: "Metadata File Optional" }).click();

            const fileInputMetadata = page.getByRole('button', { name: 'Metadata File Optional' });
            await fileInputMetadata.setInputFiles(filePathMetadata);

            await page.getByRole('button', { name: 'Upload', exact: true }).click();

            // Verify in Project Dashboard
            await page.getByRole("link", { name: "Project Dashboard" }).click();
            await page.waitForURL(/\/project/);
            await expect(page.getByRole('heading', { name: 'Project Overview' })).toBeVisible();
            await verifyInProject(page, fileName);

            const sideBar = page.getByRole('list').filter({ hasText: /^$/ });
            const dataCatalogButton = sideBar.getByRole('link').nth(1);
            await dataCatalogButton.click();

            const recordLink = page.getByRole('link', { name: fileName, exact: true }).first();
            for (let attempt = 1; attempt <= 2; attempt++) {
                await page.getByRole('textbox', { name: 'Search' }).click();
                await page.getByRole('textbox', { name: 'Search' }).fill(fileName);
                await page.getByRole('textbox', { name: 'Search' }).press('Enter');
                try {
                    await expect(page.locator('span').filter({ hasText: fileName })).toBeVisible(); // search term success
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

            createdRecordMetadata = parseRecordFromUrl(page.url());
        });
    });
    test.describe("Update existing record with valid metadata", () => {
        let originalFilePath: string;
        let replacementFilePath: string;
        let metadataFilePath: string;
        let originalFileName: string;
        let replacementFileName: string;
        let createdRecord: {
            recordId: string;
            projectId: string;
        } | null = null;

        test.beforeEach(async ({}, testInfo) => {
            originalFileName = `update-record-original-${testInfo.testId}.txt`;
            replacementFileName = `update-record-replacement-${testInfo.testId}.txt`;

            originalFilePath = path.join(os.tmpdir(), originalFileName);
            replacementFilePath = path.join(os.tmpdir(), replacementFileName);
            metadataFilePath = path.join(
                os.tmpdir(),
                `update-record-metadata-${testInfo.testId}.json`,
            );

            await fs.promises.writeFile(
                originalFilePath,
                "Original file contents",
                "utf8",
            );

            await fs.promises.writeFile(
                replacementFilePath,
                "Updated file contents",
                "utf8",
            );

            await fs.promises.writeFile(
                metadataFilePath,
                JSON.stringify(
                    {
                        Name: replacementFileName,
                        Description: "Updated record created by Playwright",
                        OriginalId: `playwright-update-${testInfo.testId}`,
                        ClassId: 1,
                    },
                    null,
                    2,
                ),
                "utf8",
            );
        });

        test.afterEach(async ({ request }) => {
            for (const filePath of [
                originalFilePath,
                replacementFilePath,
                metadataFilePath,
            ]) {
                if (filePath && fs.existsSync(filePath)) {
                    fs.unlinkSync(filePath);
                }
            }

            await deleteRecordIfExists({ request }, createdRecord, orgId);
            createdRecord = null;
        });

        test("updates an existing record using valid metadata", async ({
            page,
        }) => {
            // Create the record that will later be updated.
            createdRecord = await clickToBrowse(
                { page },
                originalFileName,
                originalFilePath,
            );

            expect(createdRecord).not.toBeNull();

            // Return to Upload Center.
            await page
                .getByRole("link", { name: "Upload Center", exact: true })
                .click();

            await page.waitForURL(/\/upload_center/);

            await expect(
                page.getByRole("heading", { name: "File Upload" }),
            ).toBeVisible();

            await checkDataSourcesAndStorageDestinations(page);

            const uploadInput = page.locator('input[type="file"][multiple]');

            await uploadInput.setInputFiles(replacementFilePath);

            await expect(
                page.getByText(`File 1: ${replacementFileName}`),
            ).toBeVisible({ timeout: 15_000 });

            await expect(
                page.getByRole("radio", {
                    name: "Update Existing Record",
                    exact: true,
                }),
            ).toBeVisible({ timeout: 15000 });

            await page
                .getByRole("radio", {
                    name: "Update Existing Record",
                    exact: true,
                })
                .click();

            const existingRecordButton = page.getByRole("button", {
                name: new RegExp(originalFileName, "i"),
            });

            await expect(existingRecordButton).toBeVisible({ timeout: 15_000 });
            await existingRecordButton.click();

            const metadataInput = page.locator(
                'input[type="file"]:not([multiple])',
            );

            await metadataInput.setInputFiles(metadataFilePath);

            await page.getByRole("button", { name: "Upload", exact: true }).click();
            await expect(
                page.getByText("Record file updated successfully."),
            ).toBeVisible({ timeout: 15_000 });

            // Verify in Project Dashboard
            await page.getByRole("link", { name: "Project Dashboard" }).click();
            await page.waitForURL(/\/project/);
            await expect(page.getByRole('heading', { name: 'Project Overview' })).toBeVisible();
            await verifyInProject(page, replacementFileName);

            const sideBar = page.getByRole('list').filter({ hasText: /^$/ });
            const dataCatalogButton = sideBar.getByRole('link').nth(1);
            await dataCatalogButton.click();

            const recordLink = page.getByRole('link', { name: replacementFileName, exact: true }).first();
            for (let attempt = 1; attempt <= 2; attempt++) {
                await page.getByRole('textbox', { name: 'Search' }).click();
                await page.getByRole('textbox', { name: 'Search' }).fill(replacementFileName);
                await page.getByRole('textbox', { name: 'Search' }).press('Enter');
                try {
                    await expect(page.locator('span').filter({ hasText: replacementFileName })).toBeVisible(); // search term success
                    await expect(recordLink).toBeVisible(); // file visible
                    break;
                } catch (error) {
                    if (attempt === 2) {
                        throw error;
                    }
                }
            }

            const updatedRecord = page
                .getByRole("link", {
                    name: replacementFileName,
                    exact: true,
                })
                .first();

            await expect(updatedRecord).toBeVisible({ timeout: 15_000 });
            await updatedRecord.click();

            console.log(createdRecord);
            await expect(
                page.getByText("Last Updated At"),
            ).toBeVisible({ timeout: 15_000 });
        });
    });
});