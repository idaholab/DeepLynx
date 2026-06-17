// src/app/(home)/project_management/[id]/page.tsx

import { cookies } from "next/headers";
import { redirect, notFound } from "next/navigation";
import ProjectManagementClient from "./ProjectManagementClient";

import {
  GroupResponseDto,
  ProjectResponseDto,
  RoleResponseDto,
  PermissionResponseDto,
  ProjectMemberResponseDto,
} from "@/app/(home)/types/responseDTOs";

import {
  getProjectServer,
  getProjectMembersServer,
} from "@/app/lib/server_service/projects_services.server";
import { getAllRolesServer } from "@/app/lib/server_service/role_services.server";
import { getAllPermissionsServer } from "@/app/lib/server_service/permissions_services.server";

export const dynamic = "force-dynamic";

type Props = {
  params: Promise<{ id?: string }>;
};

export default async function ProjectManagementPage({ params }: Props) {
  const { id } = await params;
  if (!id) {
    return notFound();
  }

  const projectId = Number(id);
  if (isNaN(projectId)) {
    redirect("/");
  }

  // Get organization from cookies (same pattern as other page)
  const cookieStore = await cookies();
  const orgSessionCookie = cookieStore.get("organizationSession");

  if (!orgSessionCookie) {
    redirect("/select-org");
  }

  let organizationId: number;
  try {
    const orgSession = JSON.parse(orgSessionCookie.value);
    organizationId = Number(orgSession.organizationId);
  } catch (e) {
    console.error("Failed to parse organization session:", e);
    redirect("/select-org");
  }

  let project: ProjectResponseDto | null = null;
  let projectMembers: ProjectMemberResponseDto[] = [];
  let projectRoles: RoleResponseDto[] = [];
  let projectPermissions: PermissionResponseDto[] = [];

  if (!isNaN(organizationId) && !isNaN(projectId)) {
    console.log(`[ProjectManagementPage] Loading project ${projectId} for org ${organizationId}`);
    try {
      project = await getProjectServer(organizationId, projectId, true);
      console.log(`[ProjectManagementPage] Project loaded:`, project ? "SUCCESS" : "NULL");
    } catch (e) {
      console.error("[ProjectManagementPage] getProjectServer failed:", e);
    }

    try {
      projectMembers = await getProjectMembersServer(organizationId, projectId);
    } catch (e) {
      console.error("getProjectMembersServer failed:", e);
    }

    try {
      projectRoles = await getAllRolesServer(organizationId, projectId);
    } catch (e) {
      console.error("getAllRoles failed: ", e);
    }

    try {
      projectPermissions = await getAllPermissionsServer(
        organizationId,
        projectId,
        undefined,
        true,
      );
    } catch (e) {
      console.error("getAllPermissionsServer failed: ", e);
    }

    // Extract groups from projectMembers (groups have empty emails)
    const projectGroups: GroupResponseDto[] = projectMembers
      .filter(member => member.email === "" && member.memberId !== undefined)
      .map(member => ({
        id: member.memberId!,
        name: member.name,
        isArchived: false,
        organizationId: organizationId,
      }));

    // If project isn't found, mirror behavior of the other page
    if (!project) {
      return notFound();
    }

    return (
      <ProjectManagementClient
        project={project}
        projectMembers={projectMembers}
        projectGroups={projectGroups}
        projectRoles={projectRoles}
        projectPermissions={projectPermissions}
      />
    );
  }

  return notFound();
}