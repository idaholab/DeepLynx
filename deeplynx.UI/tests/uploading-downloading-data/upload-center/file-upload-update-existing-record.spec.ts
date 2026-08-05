import { test } from "../../fixtures";
import { sysAdmin } from "../../deeplynx-config";
import { navigateToProjectDashboard, navigateToUploadCenter } from "../../helpers/upload-helpers";


let orgId: string;
const ORG_NAME = "PW Org A";

test.describe("File Upload -> Update Existing Record", () => {

    test.use({
        actingUser: sysAdmin,
        actingOrg: ORG_NAME,
        actingProject: "PW Project X",
    });

    test.beforeEach(async ({ page }) => {
        navigateToProjectDashboard(page);
        navigateToUploadCenter(page);
    });


    //Future Tests Dealing with Updating Existing Records
});