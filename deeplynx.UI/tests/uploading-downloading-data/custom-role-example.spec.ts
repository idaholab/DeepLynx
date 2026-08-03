import { test, expect, Page, gotoScope } from "../fixtures";
import {
  sysAdmin, fullPermissionUserX, ORGS, PROJECTS,
  defineRole, defineTestAccount, PermissionResource, PermissionAction,
} from "../deeplynx-config";
import { getOrgIdByName, parseRecordFromUrl, deleteRecordIfExists } from "../helpers/api";
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';

// Define custom role permissions
const noFileWriteRole = defineRole({
  [PermissionResource.Project]: [PermissionAction.Read],
  [PermissionResource.Organization]: [PermissionAction.Read],
  [PermissionResource.Record]: [PermissionAction.Read, PermissionAction.Write],
  [PermissionResource.File]: [PermissionAction.Read],
  [PermissionResource.Edge]: [PermissionAction.Read, PermissionAction.Write],
  [PermissionResource.Tag]: [PermissionAction.Read],
});
// provision a new test account with the custom role
const noFileWriteUserX = defineTestAccount(
  { role: noFileWriteRole, org: ORGS.orgA, project: PROJECTS.projectX },
  'noFileWriteUserX',
);

// define the actors that this workflow will be tested against
const actors = [
  { account: sysAdmin, label: 'sysAdmin', expectSuccess: true },
  { account: fullPermissionUserX, label: 'fullPermissionUserX', expectSuccess: true },
  { account: noFileWriteUserX, label: 'noFileWriteUserX', expectSuccess: false },
] as const;

async function goToUploadCenter(page: Page): Promise<void> {
  await page.getByRole('link', { name: 'Upload Center', exact: true }).click();
  await page.waitForURL(/\/upload_center/);
  await expect(page.getByRole('heading', { name: 'Upload Center' })).toBeVisible();
}

async function attemptUpload(page: Page, filePath: string): Promise<void> {
  await page.getByRole('button', { name: 'File Upload Drag and Drop Area and Button' }).click();
  await page.locator('input[type="file"]').setInputFiles(filePath);
  await page.getByRole('button', { name: 'Upload', exact: true }).click();
}

test.describe('Upload Center: File:Write permission', () => {
  // sysAdmin only needed here to resolve orgId once for cleanup — the
  // actual multi-user upload attempts happen via actAs inside the test.
  test.use({ actingUser: sysAdmin, actingOrg: ORGS.orgA, actingProject: PROJECTS.projectX });

  let filePath: string;
  let orgId: string;
  const fileName = 'permission-test-file.txt';
  const createdRecords: ({ recordId: string; projectId: string } | null)[] = [];

  test.beforeAll(async () => {
    filePath = path.join(os.tmpdir(), fileName);
    await fs.promises.writeFile(filePath, 'small file for permission testing');
  });

  test.afterAll(async () => {
    if (fs.existsSync(filePath)) fs.unlinkSync(filePath);
  });

  test.beforeEach(async ({ request }) => {
    orgId = await getOrgIdByName(request, ORGS.orgA.name);
  });

  test.afterEach(async ({ request }) => {
    for (const record of createdRecords.splice(0)) {
      await deleteRecordIfExists(request, record, orgId);
    }
  });

  // run the test on a loop for every actor. 
  test('File:Write is the sole gate on uploading, across sysAdmin/fullPermission/noFileWrite', async ({ actAs }) => {
    for (const { account, label, expectSuccess } of actors) {
      await test.step(label, async () => {
        const page = await actAs(account);
        await gotoScope(page, ORGS.orgA, PROJECTS.projectX);
        await goToUploadCenter(page);
        await attemptUpload(page, filePath);

        if (expectSuccess) {
          await expect(page.getByText('File uploaded successfully!')).toBeVisible();
          createdRecords.push(parseRecordFromUrl(page.url())); // no-op today — see note below
        } else {
          // TODO: replace with the actual permission-denied notification
          // text/selector once confirmed against the app (see the
          // analogous TODO in upload_center.spec.ts's "Empty file upload"
          // test).
          await expect(page.getByText('File uploaded successfully!')).not.toBeVisible();
        }
      });
    }
  });
});