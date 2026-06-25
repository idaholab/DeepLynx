import "server-only";
import { cookies } from "next/headers";
import { redirect } from "next/navigation";

export async function getRecordCollectionsRouteContext() {
  const cookieStore = await cookies();
  const orgSessionCookie = cookieStore.get("organizationSession");

  if (!orgSessionCookie) {
    redirect("/select-org");
  }

  let organizationId: number;
  try {
    organizationId = Number(JSON.parse(orgSessionCookie.value).organizationId);
  } catch (error) {
    console.error("Failed to parse organization session:", error);
    redirect("/select-org");
  }

  const projectSessionCookie = cookieStore.get("projectSession");
  let projectId: number | undefined;

  if (projectSessionCookie) {
    try {
      projectId = Number(JSON.parse(projectSessionCookie.value).projectId);
    } catch (error) {
      console.error("Failed to parse project session cookie:", error);
    }
  }

  if (!projectId) {
    redirect("/");
  }

  return { organizationId, projectId };
}
