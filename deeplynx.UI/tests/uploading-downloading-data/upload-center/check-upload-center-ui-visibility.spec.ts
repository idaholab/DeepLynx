import { test, expect } from "../../fixtures";
import { sysAdmin, ORGS, PROJECTS } from "../../deeplynx-config";
import { navigateToUploadCenter } from "../../helpers/upload-helpers";

test.describe("Check Upload Center UI Visibility", () => {

    test.use({
        actingUser: sysAdmin,
        actingOrg: ORGS.orgA,
        actingProject: PROJECTS.projectX,
    });

    test.beforeEach(async ({ page }) => {
        await navigateToUploadCenter(page);
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

    test("File Upload radio is selected by default", async ({ page }) => {
        await expect(
            page.getByRole("radio", { name: "File Upload" }),
        ).toBeChecked();

        await expect(
            page.getByRole("radio", { name: "Bulk Metadata" }),
        ).not.toBeChecked();
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
});