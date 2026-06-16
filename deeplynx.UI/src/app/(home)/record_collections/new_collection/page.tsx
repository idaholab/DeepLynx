import NewCollectionClient from "./NewCollectionClient";
import { cookies } from "next/headers";
import { redirect } from "next/navigation";

export const metadata = { title: "Record Collections" };

export default async function Page() {
  // Get organization from cookies
  const cookieStore = await cookies();
  const orgSessionCookie = cookieStore.get("organizationSession");

  if (!orgSessionCookie) {
    redirect("/select-org");
  }

  let organizationId: string | number | undefined;
  try {
    const orgSession = JSON.parse(orgSessionCookie.value);
    organizationId = orgSession.organizationId;
  } catch (e) {
    console.error("Failed to parse organization session:", e);
    redirect("/select-org");
  }

  // Get initial project from project session cookie
  const projectSessionCookie = cookieStore.get("projectSession");
  let projectId: number | undefined;

  if (projectSessionCookie) {
    try {
      const projectSession = JSON.parse(projectSessionCookie.value);
      projectId = projectSession.projectId;
    } catch (e) {
      console.error("Failed to parse project session cookie:", e);
    }
  }

  if (!projectId) {
    redirect("/");
  }

  return (
    <NewCollectionClient
      organizationId={Number(organizationId)}
      projectId={Number(projectId)}
    />
  );
}
