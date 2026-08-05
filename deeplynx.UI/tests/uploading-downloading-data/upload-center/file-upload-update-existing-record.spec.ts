import { test, expect } from "../../fixtures";
import { sysAdmin } from "../../deeplynx-config";
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import {
    extractProjectIdFromURL, getOrgIdByName, navigateToProjectDashboard, navigateToUploadCenter,
    deleteRecordIfExists, checkDataSourcesAndStorageDestinations, clickToBrowse, FileTypeConfig, createFakeHdf5, createFakeTdms,
    createMinimalDocx, createMinimalXlsx, createZip, setUp, dragAndDrop, verifyInProject, parseRecordFromUrl, getNonDefaultProject,
    getNonDefault, checkDataSources, checkStorageDestinations
} from "../../helpers/upload-helpers";


let projectId: string;
let orgId: string;
const ORG_NAME = "PW Org A";

const fileType: FileTypeConfig =
{
    label: 'JSON',
    fileName: 'test-file.json',
    mimeType: 'application/json',
    content: JSON.stringify({

        Name: "example_file_name",
        Description: "Describe this file",
        OriginalId: "unique-original-id",
        Properties: {
            exampleKey: "exampleValue"
        }

    }, null, 2),
};

test.describe("File Upload -> Update Existing Record", () => {

    test.use({
        actingUser: sysAdmin,
        actingOrg: ORG_NAME,
        actingProject: "PW Project X",
    });

    test.beforeEach(async ({ page, request }) => {
        orgId = await getOrgIdByName(request, ORG_NAME);
        await navigateToProjectDashboard(page);
        projectId = await extractProjectIdFromURL(page);
        await navigateToUploadCenter(page);
    });


    test.describe("Upload timeseries file then update said file's metadata", () => {
        const fileName: string = `${Date.now()}-${Math.random().toString(36).slice(2)}-timeseries-test-file.csv`;
        const fileNameMetadata: string = `${Date.now()}-${Math.random().toString(36).slice(2)}-timeseries-test-file-metadata.json`;
        let filePath: string;
        let filePathMetadata: string;
        let createdRecord: { recordId: string; projectId: string } | null = null;

        test.beforeEach(async () => {
            // Create the file locally
            filePath = path.join(os.tmpdir(), fileName);
            const csvContent = 'Date, Candy Eaten\n20050101, 5\n20050102, 6\n20050103, 3\n20050104, 15\n20050105, 3\n20050106, 5\n20050107, 9\n20050108, 12\n20050109, 6\n20050110, 9\n20050111, 6\n20050112, 3\n20050113, 2';

            if (!fs.existsSync(filePath)) {
                await fs.promises.writeFile(filePath, csvContent, 'utf8');
            }
            createdRecord = null;

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
            createdRecord = null;
        });

        test("uploads a timeseries file", async ({ page }) => {
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

            await expect(page.getByText('File uploaded successfully!')).toBeVisible();
        });
    });
});