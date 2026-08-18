import "server-only";
import { apiFetch, asJson } from "./api.server";
import { PaginatedResponse, PermissionResponseDto, RoleResponseDto } from "../../(home)/types/responseDTOs";
import { CreateRoleRequestDto, UpdateRoleRequestDto } from "../../(home)/types/requestDTOs";

/** ===== Server-safe calls ===== */

export async function getAllRolesServer(
  organizationId: number,
  projectId: number,
  hideArchived: boolean = true,
  pageNumber: number = 1,
  pageSize: number = -1
): Promise<PaginatedResponse<RoleResponseDto>> {
  const searchParams = new URLSearchParams();
  searchParams.append("hideArchived", hideArchived.toString());
  searchParams.append("pageNumber", String(pageNumber));
  searchParams.append("pageSize", String(pageSize));

  const path = `/organizations/${organizationId}/projects/${projectId}/roles?${searchParams.toString()}`;

  const res = await apiFetch(path);
  return asJson<PaginatedResponse<RoleResponseDto>>(res);
}

export async function getRoleByIdServer(
  organizationId: number,
  projectId: number,
  roleId: number,
  hideArchived: boolean = true
): Promise<RoleResponseDto> {
  const searchParams = new URLSearchParams();
  searchParams.append("hideArchived", hideArchived.toString());

  const path = `/organizations/${organizationId}/projects/${projectId}/roles/${roleId}?${searchParams.toString()}`;

  const res = await apiFetch(path);
  return asJson<RoleResponseDto>(res);
}

export async function createRoleServer(
  organizationId: number,
  projectId: number,
  body: CreateRoleRequestDto
): Promise<RoleResponseDto> {
  const path = `/organizations/${organizationId}/projects/${projectId}/roles`;

  const res = await apiFetch(path, {
    method: "POST",
    body: JSON.stringify(body),
  });

  return asJson<RoleResponseDto>(res);
}

export async function updateRoleServer(
  organizationId: number,
  projectId: number,
  roleId: number,
  body: UpdateRoleRequestDto
): Promise<RoleResponseDto> {
  const path = `/organizations/${organizationId}/projects/${projectId}/roles/${roleId}`;

  const res = await apiFetch(path, {
    method: "PUT",
    body: JSON.stringify(body),
  });

  return asJson<RoleResponseDto>(res);
}

export async function deleteRoleServer(
  organizationId: number,
  projectId: number,
  roleId: number
): Promise<{ message: string }> {
  const path = `/organizations/${organizationId}/projects/${projectId}/roles/${roleId}`;

  const res = await apiFetch(path, {
    method: "DELETE",
  });

  return asJson<{ message: string }>(res);
}

export async function archiveRoleServer(
  organizationId: number,
  projectId: number,
  roleId: number,
  archive: boolean
): Promise<{ message: string }> {
  const searchParams = new URLSearchParams();
  searchParams.append("archive", archive.toString());

  const path = `/organizations/${organizationId}/projects/${projectId}/roles/${roleId}?${searchParams.toString()}`;

  const res = await apiFetch(path, {
    method: "PATCH",
  });

  return asJson<{ message: string }>(res);
}

export async function getPermissionsByRoleServer(
  organizationId: number,
  projectId: number,
  roleId: number
): Promise<PermissionResponseDto[]> {
  const path = `/organizations/${organizationId}/projects/${projectId}/roles/${roleId}/permissions`;

  const res = await apiFetch(path);
  return asJson<PermissionResponseDto[]>(res);
}

export async function addPermissionToRoleServer(
  organizationId: number,
  projectId: number,
  roleId: number,
  permissionId: number
): Promise<{ message: string }> {
  const path = `/organizations/${organizationId}/projects/${projectId}/roles/${roleId}/permissions/${permissionId}`;

  const res = await apiFetch(path, {
    method: "POST",
  });

  return asJson<{ message: string }>(res);
}

export async function removePermissionFromRoleServer(
  organizationId: number,
  projectId: number,
  roleId: number,
  permissionId: number
): Promise<{ message: string }> {
  const path = `/organizations/${organizationId}/projects/${projectId}/roles/${roleId}/permissions/${permissionId}`;

  const res = await apiFetch(path, {
    method: "DELETE",
  });

  return asJson<{ message: string }>(res);
}

export async function setPermissionsForRoleServer(
  organizationId: number,
  projectId: number,
  roleId: number,
  permissionIds: number[]
): Promise<{ message: string }> {
  const path = `/organizations/${organizationId}/projects/${projectId}/roles/${roleId}/permissions`;

  const res = await apiFetch(path, {
    method: "PUT",
    body: JSON.stringify(permissionIds),
  });

  return asJson<{ message: string }>(res);
}


/** ===== Org server side calls ===== */

export async function getAllOrgRolesServer(
  organizationId: number,
  hideArchived: boolean = true
): Promise<RoleResponseDto[]> {
  const searchParams = new URLSearchParams();
  searchParams.append("hideArchived", hideArchived.toString());

  const path = `/organizations/${organizationId}/roles?${searchParams.toString()}`;

  const res = await apiFetch(path);
  return asJson<RoleResponseDto[]>(res);
}

/**
 * Get all permissions for a role at the organization level
 * @param organizationId - The ID of the organization
 * @param roleId - The ID of the role
 * @returns Promise with array of PermissionResponseDto
 */
export async function getOrgRolePermissionsServer(
  organizationId: number,
  roleId: number
): Promise<PermissionResponseDto[]> {
  const path = `/organizations/${organizationId}/roles/${roleId}/permissions`;

  const res = await apiFetch(path);
  return asJson<PermissionResponseDto[]>(res);
}