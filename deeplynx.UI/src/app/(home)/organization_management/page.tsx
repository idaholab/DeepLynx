// src/app/(home)/organization_management/page.tsx

import { getAllGroupsServer } from "@/app/lib/server_service/group_services.server";
import { getAllProjectsServer } from "@/app/lib/server_service/projects_services.server";
import {
  getAllOrgRolesServer,
} from "@/app/lib/server_service/role_services.server";
import { getAllUsersServer } from "@/app/lib/server_service/user_services.server";
import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { mapToProjectResponseDtos } from "../page";
import {
  GroupResponseDto,
  PermissionResponseDto,
  ProjectResponseDto,
  RoleResponseDto,
  UserResponseDto,
} from "../types/responseDTOs";
import OrganizationManagmentClient from "./OrganizationManagementClient";
import { getAllOrgPermissionsServer } from "@/app/lib/server_service/permissions_services.server";

export const dynamic = "force-dynamic";

const mapToGroupResponseDtos = (group: GroupResponseDto): GroupResponseDto => {
  return {
    ...group,
  } as GroupResponseDto;
};

const OrganizationManagementPage = async ({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) => {
  const cookieStore = await cookies();
  const orgSessionCookie = cookieStore.get("organizationSession");

  if (!orgSessionCookie) {
    redirect("/select-org");
  }

  let organizationId: string | number;
  try {
    const orgSession = JSON.parse(orgSessionCookie.value);
    organizationId = orgSession.organizationId;
  } catch (e) {
    console.error("Failed to parse organization session:", e);
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

  // Fetch groups filtered by organization
  let groups: GroupResponseDto[] = [];
  try {
    const apiGroups = await getAllGroupsServer(organizationId as number, true);
    groups = apiGroups.map(mapToGroupResponseDtos);
  } catch (error) {
    console.error("getAllGroups failed:", error);
  }

  // Fetch roles and permissions in parallel
  let roles: RoleResponseDto[] = [];
  let permissions: PermissionResponseDto[] = [];

  try {
    // First, fetch roles for the organization
    roles = await getAllOrgRolesServer(Number(organizationId));
    permissions = await getAllOrgPermissionsServer(Number(organizationId), true);
  } catch (error) {
    console.error("Failed to fetch roles or permissions:", error);
  }

  // Fetch users filtered by organization
  const members = (await getAllUsersServer(
    undefined,
    Number(organizationId),
  )) as UserResponseDto[];

  const params = await searchParams;
  const fromProject =
    typeof params.fromProject === "string" ? params.fromProject : "";

  const initialSelectedProject = fromProject
    ? projects.find((p) => String(p.id) === fromProject) || projects[0]
    : projects[0];

  return (
    <OrganizationManagmentClient
      key={organizationId}
      members={members}
      initialProjects={projects}
      initialGroups={groups}
      initialRoles={roles}
      initialSelectedProject={initialSelectedProject}
      initialPermissions={permissions}
    />
  );
};

export default OrganizationManagementPage;
