import { expect, test } from "@playwright/test";
import { seedSession } from "../helpers/seed";

type CurrentUser = {
  isSysAdmin: boolean;
  isOrgAdmin: boolean | null;
  isProjectAdmin: boolean | null;
};

type Project = {
  id: number | string;
};

const API_BASE =
  process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5095/api/v1";
const ORGANIZATION_ID = 1;

async function fetchJson<T>(path: string): Promise<T | null> {
  try {
    const response = await fetch(`${API_BASE}${path}`);
    if (!response.ok) return null;
    return (await response.json()) as T;
  } catch {
    return null;
  }
}

async function getCurrentUser(projectId?: number): Promise<CurrentUser | null> {
  const params = new URLSearchParams({
    organizationId: String(ORGANIZATION_ID),
  });

  if (projectId !== undefined) {
    params.set("projectId", String(projectId));
  }

  return fetchJson<CurrentUser>(`/users/current?${params.toString()}`);
}

async function getFirstProjectId(): Promise<number | null> {
  const projects = await fetchJson<Project[]>(
    `/organizations/${ORGANIZATION_ID}/projects`,
  );
  const firstProject = projects?.[0];
  if (!firstProject) return null;

  const projectId = Number(firstProject.id);
  return Number.isFinite(projectId) ? projectId : null;
}

test.describe("management route RBAC", () => {
  test.beforeEach(async ({ page }) => {
    await seedSession(page);
  });

  test("non-sys-admin direct navigation to site management redirects to unauthorized", async ({
    page,
  }) => {
    const user = await getCurrentUser();
    test.skip(!user, "Backend current-user endpoint is unavailable.");
    test.skip(user?.isSysAdmin === true, "Current test user is a sys admin.");

    await page.goto("/site_management", { waitUntil: "domcontentloaded" });
    await expect(page).toHaveURL(/\/unauthorized$/);
  });

  test("non-org-admin direct navigation to organization management redirects to unauthorized", async ({
    page,
  }) => {
    const user = await getCurrentUser();
    test.skip(!user, "Backend current-user endpoint is unavailable.");
    test.skip(
      user?.isSysAdmin || user?.isOrgAdmin === true,
      "Current test user is an organization admin.",
    );

    await page.goto("/organization_management", {
      waitUntil: "domcontentloaded",
    });
    await expect(page).toHaveURL(/\/unauthorized$/);
  });

  test("non-project-admin direct navigation to project management redirects to unauthorized", async ({
    page,
  }) => {
    const projectId = await getFirstProjectId();
    test.skip(!projectId, "No project is available for route-access testing.");

    const user = await getCurrentUser(Number(projectId));
    test.skip(!user, "Backend current-user endpoint is unavailable.");
    test.skip(
      user?.isSysAdmin ||
        user?.isOrgAdmin === true ||
        user?.isProjectAdmin === true,
      "Current test user is a project admin.",
    );

    await page.goto(`/project_management/${projectId}`, {
      waitUntil: "domcontentloaded",
    });
    await expect(page).toHaveURL(/\/unauthorized$/);
  });

  test("unauthorized page gives users a clear way back home", async ({
    page,
  }) => {
    await page.goto("/unauthorized", { waitUntil: "domcontentloaded" });

    await expect(
      page.getByRole("heading", { name: "Unauthorized" }),
    ).toBeVisible();
    await expect(
      page.getByRole("link", { name: "Return home" }),
    ).toHaveAttribute("href", "/");
  });
});
