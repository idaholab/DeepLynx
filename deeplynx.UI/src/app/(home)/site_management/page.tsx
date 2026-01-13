import React from "react";
import SysAdminClient from "./SysAdminClient";
import {
  OauthApplicationResponseDto,
  OrganizationResponseDto,
  UserResponseDto,
  ProjectResponseDto,
} from "../types/responseDTOs";
import { getAllOrganizationsServer } from "@/app/lib/server_service/organization_services.server";
import { getAllOauthApplicationsServer } from "@/app/lib/server_service/oauth_services.server";
import { getAllUsersServer } from "@/app/lib/server_service/user_services.server";
import { getAllProjectsServer } from "@/app/lib/server_service/projects_services.server";
import { cookies } from "next/headers";
import { auth } from "../../../../auth";

export const dynamic = "force-dynamic";

const SysAdminPage = async () => {
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

  // Fetch all data
  const OrganizationResponseDtos =
    (await getAllOrganizationsServer()) as OrganizationResponseDto[];
  const oAuthApplications =
    (await getAllOauthApplicationsServer()) as OauthApplicationResponseDto[];
  const members = (await getAllUsersServer()) as UserResponseDto[];
  
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
    <SysAdminClient
      organizations={OrganizationResponseDtos}
      applications={oAuthApplications}
      members={members}
      initialProjects={initialProjects}
      initialSelectedProjects={initialSelectedProjects}
    />
  );
};

export default SysAdminPage;