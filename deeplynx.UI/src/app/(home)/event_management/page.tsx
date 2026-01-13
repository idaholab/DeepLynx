import EventHistoryClient from "./EventHistoryClient"
import { cookies } from "next/headers";
import { auth } from "../../../../auth";
import { getAllProjectsServer } from "@/app/lib/server_service/projects_services.server";
import { ProjectResponseDto } from "../types/responseDTOs";

const EventManagementPage = async () => {

  // Get organization ID - prioritize cookie over session for real-time updates
  const cookieStore = await cookies();
  const orgSessionCookie = cookieStore.get("organizationSession");

  let organizationId: number | undefined;

  if (orgSessionCookie) {
    try {
      const orgSession = JSON.parse(orgSessionCookie.value);
      organizationId = orgSession.organizationId;
    } catch (e) {
      console.error("Failed to parse organization cookie:", e);
      // Fallback to session if cookie parsing fails
      const session = await auth();
      organizationId = session?.user?.organizationId;
    }
  } else {
    // No cookie, fallback to session
    const session = await auth();
    organizationId = session?.user?.organizationId;
  }

  // Fetch projects filtered by organization
  const projects = (await getAllProjectsServer(
    organizationId as number
  )) as ProjectResponseDto[];
  const initialProjects = projects.map((p) => ({
    id: String(p.id),
    name: p.name,
  }));

  // Get initial project from project session cookie
  const projectSessionCookie = cookieStore.get("projectSession");
  let initialProjectId: string | null = null;

  if (projectSessionCookie) {
    try {
      const projectSession = JSON.parse(projectSessionCookie.value);
      initialProjectId = String(projectSession.projectId);
    } catch (e) {
      console.error("Failed to parse project session cookie:", e);
    }
  }

  const initialSelectedProjects = initialProjectId ? [initialProjectId] : [];

  return (
    <section>
      <EventHistoryClient 
        initialProjects={initialProjects}
        initialSelectedProjects={initialSelectedProjects}
      />
    </section>
  )
}

export default EventManagementPage