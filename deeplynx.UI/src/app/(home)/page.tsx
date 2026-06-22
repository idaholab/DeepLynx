// app/(home)/page.tsx
import HomeDashboardClient from "./HomeDashboardClient";
import { getAllProjectsServer } from "../lib/server_service/projects_services.server";
import { getLocalDevUserServer } from "../lib/server_service/user_services.server";
import { ProjectResponseDto, UserResponseDto } from "./types/responseDTOs";
import { cookies } from "next/headers";
import { redirect } from "next/navigation";

export const dynamic = "force-dynamic";

// Extend UserResponseDto to include organizationId for local dev user
interface LocalDevUser extends UserResponseDto {
  organizationId?: number;
}

export function mapToProjectResponseDtos(
  p: ProjectResponseDto,
): ProjectResponseDto {
  return {
    id: String(p.id),
    name: p.name ?? "",
    description: p.description ?? "",
    abbreviation: p.abbreviation ?? "",
    lastUpdatedAt: p.lastUpdatedAt,
    lastUpdatedBy: p.lastUpdatedBy ?? "",
    isArchived: p.isArchived,
    organizationId: p.organizationId,
  };
}

export default async function Page() {
  const isAuthDisabled =
    process.env.NEXT_PUBLIC_DISABLE_FRONTEND_AUTHENTICATION === "true";

  // Get organization from cookies
  const cookieStore = await cookies();
  const orgSessionCookie = cookieStore.get("organizationSession");

  let organizationId: string | number | undefined;

  if (orgSessionCookie) {
    try {
      const orgSession = JSON.parse(orgSessionCookie.value);
      organizationId = orgSession.organizationId;
    } catch (e) {
      console.error("Failed to parse organization session:", e);
      if (!isAuthDisabled) {
        redirect("/select-org");
      }
    }
  }

  // If no org, redirect
  if (organizationId == null) {
    redirect("/select-org");
  }

  // Fetch projects filtered by organization
  let projects: ProjectResponseDto[] = [];
  try {
    const apiProjects = await getAllProjectsServer(
      organizationId as number,
      true,
    );
    projects = apiProjects.map(mapToProjectResponseDtos);
  } catch (e) {
    console.error("getAllProjectsServer failed:", e);
  }

  return <HomeDashboardClient initialProjects={projects} />;
}
