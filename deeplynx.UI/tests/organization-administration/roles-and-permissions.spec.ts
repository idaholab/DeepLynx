import { test, expect, Page, APIRequestContext } from "../fixtures";
import { ORGS, PROJECTS, orgAdminA } from "../deeplynx-config";
import { getOrgIdByName } from "../helpers/api";
import { TEST_API_BASE_URL } from "../api-url";
// Adjust this base URL to match whichever environment the test config points at.

async function navigateToOrgLevelSensitivityLabelPermissions(page: Page) {
    await page.getByRole('link', { name: 'Organization Settings' }).click();
    await page.getByText('Roles & Permissions').click();
    await page.getByRole('button', { name: 'User User role with limited' }).click();
    await page.getByText('Sensitivity Labels', { exact: true }).click();
}

test.describe("Roles & Permissions", () => {
    test.use({
        actingUser: orgAdminA,
        actingOrg: ORGS.orgA,
        actingProject: PROJECTS.projectX,
    });

    let uniqueLabelName: string;
    let createdLabelId: number;
    let orgId: string;

    test.beforeAll(async ({ request }, testInfo) => {
        orgId = await getOrgIdByName(request, ORGS.orgA.name);
        uniqueLabelName = `Test SL-${testInfo.testId}`;

        const createResponse = await request.post(
        `${TEST_API_BASE_URL}/organizations/${orgId}/labels`,
        {
            data: {
                name: uniqueLabelName,
                description: "Created by Playwright test - safe to delete",
            },
        },
    );

        expect(createResponse.ok()).toBeTruthy();
        const created = await createResponse.json();
        createdLabelId = created.id;
    });

    test.afterAll(async ({ request }) => {
        if (!createdLabelId) return;

        const deleteResponse = await request.delete(
            `${TEST_API_BASE_URL}/organizations/${orgId}/labels/${createdLabelId}`,
        );

        expect(deleteResponse.ok()).toBeTruthy();
    });

    test.beforeEach(async ({ page }) => {
        await expect(page).toHaveURL(/\/project\/\d+/);
        await navigateToOrgLevelSensitivityLabelPermissions(page);
    });

    test("Org Admin can edit org level sensitivity label permissions", async ({ page }) => {
        // The label created in beforeAll is guaranteed to exist and be unique,
        // so there's no need to check-and-create at UI level anymore.
        await page.getByRole('button', { name: 'Edit Permissions' }).click();
        await page.getByTitle(`Permission to delete ${uniqueLabelName} labeled files`).getByLabel('delete file').check();
        await page.getByRole('button', { name: 'Save Changes' }).click();
        await expect(page.locator('div').filter({ hasText: 'Permissions updated' }).first()).toBeVisible();
    });
});