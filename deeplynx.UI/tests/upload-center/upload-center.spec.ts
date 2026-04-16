import { test, expect } from "@playwright/test";
import { seedAndNavigateToProject } from "../helpers/seed";

test.describe("Upload Center", () => {
  test.beforeEach(async ({ page }) => {
    await seedAndNavigateToProject(page);
    // Navigate to Upload Center via sidebar
    await page.locator("aside a", { hasText: "Upload Center" }).click();
    await page.waitForURL(/\/upload_center/);
    // Wait for the Upload Center heading to confirm client-side render is done
    await expect(
      page.getByRole("heading", { name: "Upload Center" }),
    ).toBeVisible();
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

  test("File Upload is the default mode", async ({ page }) => {
    // In file upload mode, the drag & drop zone is visible
    await expect(
      page.getByText("Drag & drop files here"),
    ).toBeVisible();
  });

  test("Project / Data Source section is visible", async ({ page }) => {
    await expect(
      page.getByRole("heading", { name: "Project / Data Source" }),
    ).toBeVisible();
  });

  test("Select a project box is visible", async ({ page}) => {
    await expect(
      page.getByText("Select a projectProjectTest"),
    ).toBeVisible();
  })

  test("Select a project dropdown is visible", async ({ page }) =>{
    await expect(
      page.getByLabel("Select a project")
    ).toBeVisible();
  });
  
  test("Data source box is visible", async ({ page}) => {
    await expect(
      page.getByText("Data sourceData"),
    ).toBeVisible();
  })

  test("Data source dropdown is visible", async ({ page }) =>{
    await expect(
      page.getByLabel("Data source")
    ).toBeVisible();
  });

  test("Storage destination box is visible", async ({ page}) => {
    await expect(
      page.getByText("Storage DestinationObject"),
    ).toBeVisible();
  })

    test("Storage Destination dropdown is visible", async ({ page }) =>{
    await expect(
      page.getByLabel("Storage Destination")
    ).toBeVisible();
  });

  /* await page.getByText('Select a project').click();


  await page.getByText('Select a projectProjectTest').click();
  await page.locator('.size-6.text-success').first().click();
  await page.getByText('Data sourceData').click();
  await page.getByText('Data source', { exact: true }).click();
  await page.locator('span').filter({ hasText: 'Data source' }).first().click();
  await page.getByText('Storage DestinationObject').click();
  await page.locator('span').filter({ hasText: 'Storage Destination' }).first().click();
  await page.getByText('Storage Destination').click();
  await page.locator('label:nth-child(3) > .flex > .size-6').click();
  await page.locator('.space-y-3 > .p-4').first().click();
 */
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
      page.getByText("Bulk Metadata Upload"),
    ).toBeVisible();
  });
});
