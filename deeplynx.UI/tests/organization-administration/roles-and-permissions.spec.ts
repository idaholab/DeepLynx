import { test, expect, Page, APIRequestContext } from "../fixtures";
import { ORGS, PROJECTS, orgAdminA } from "../deeplynx-config";
import { getOrgIdByName } from "../helpers/api";

// Adjust this base URL to match whichever environment the test config points at.
const API_BASE_URL = process.env.API_BASE_URL || "http://localhost:5095";

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

    // Unique per test run so parallel runs / reruns never collide on name.
    let uniqueLabelName: string;
    let createdLabelId: number;
    let apiContext: APIRequestContext;
    let orgId: string;

    test.beforeAll(async ({ request }, testInfo) => {
        // NOTE: adjust auth header/token retrieval to match how your test
        // fixtures normally authenticate API calls (e.g. a helper that logs
        // in orgAdminA and returns a bearer token). Swap ACCESS_TOKEN below.
        apiContext = request;
        orgId = await getOrgIdByName(request, ORGS.orgA.name);
        uniqueLabelName = `Test SL-${testInfo.testId}`;

        const createResponse = await apiContext.post(
            `${API_BASE_URL}/api/v1/organizations/${orgId}/labels`,
            {
                headers: {
                    Authorization: `Bearer ${process.env.TEST_ACCESS_TOKEN}`,
                    "Content-Type": "application/json",
                },
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
        apiContext = request;

        const deleteResponse = await apiContext.delete(
            `${API_BASE_URL}/api/v1/organizations/${orgId}/labels/${createdLabelId}`,
            {
                headers: {
                    Authorization: `Bearer ${process.env.TEST_ACCESS_TOKEN}`,
                },
            },
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