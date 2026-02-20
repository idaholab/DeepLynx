// src/app/lib/permission_services.server.ts
import "server-only";
import { apiFetch, asJson } from "./api.server";
import { PermissionResponseDto } from "../../(home)/types/responseDTOs";

/** ===== Server-safe calls ===== */

export async function getAllPermissionsServer(
  organizationId: number,
  projectId: number,
  labelId?: number,
  hideArchived: boolean = true
): Promise<PermissionResponseDto[]> {
  const searchParams = new URLSearchParams();

  if (labelId !== undefined) {
    searchParams.append("labelId", labelId.toString());
  }
  searchParams.append("hideArchived", hideArchived.toString());

  const queryString = searchParams.toString();
  const path = `/organizations/${organizationId}/projects/${projectId}/permissions${
    queryString ? `?${queryString}` : ""
  }`;

  const res = await apiFetch(path);
  return asJson<PermissionResponseDto[]>(res);
}

export async function getPermissionByIdServer(
  organizationId: number,
  projectId: number,
  permissionId: number,
  hideArchived: boolean = true
): Promise<PermissionResponseDto> {
  const searchParams = new URLSearchParams();
  searchParams.append("hideArchived", hideArchived.toString());

  const queryString = searchParams.toString();
  const path = `/organizations/${organizationId}/projects/${projectId}/permissions/${permissionId}${
    queryString ? `?${queryString}` : ""
  }`;

  const res = await apiFetch(path);
  return asJson<PermissionResponseDto>(res);
}

export async function createPermissionServer(
  organizationId: number,
  projectId: number,
  body: {
    name: string;
    description?: string;
    action: string;
    labelId?: number;
    projectId?: number;
    organizationId?: number;
  }
): Promise<PermissionResponseDto> {
  const path = `/organizations/${organizationId}/projects/${projectId}/permissions`;

  const res = await apiFetch(path, {
    method: "POST",
    body: JSON.stringify(body),
  });

  return asJson<PermissionResponseDto>(res);
}

export async function updatePermissionServer(
  organizationId: number,
  projectId: number,
  permissionId: number,
  body: {
    name?: string;
    description?: string;
    action?: string;
    labelId?: number;
  }
): Promise<PermissionResponseDto> {
  const path = `/organizations/${organizationId}/projects/${projectId}/permissions/${permissionId}`;

  const res = await apiFetch(path, {
    method: "PUT",
    body: JSON.stringify(body),
  });

  return asJson<PermissionResponseDto>(res);
}

export async function deletePermissionServer(
  organizationId: number,
  projectId: number,
  permissionId: number
): Promise<void> {
  const path = `/organizations/${organizationId}/projects/${projectId}/permissions/${permissionId}`;

  await apiFetch(path, {
    method: "DELETE",
  });
}

export async function archivePermissionServer(
  organizationId: number,
  projectId: number,
  permissionId: number,
  archive: boolean
): Promise<void> {
  const searchParams = new URLSearchParams();
  searchParams.append("archive", archive.toString());

  const path = `/organizations/${organizationId}/projects/${projectId}/permissions/${permissionId}?${searchParams.toString()}`;

  await apiFetch(path, {
    method: "PATCH",
  });
}

/**
 * Get permissions for a specific role (server-side)
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param roleId - The ID of the role whose permissions to retrieve
 * @returns Promise with array of PermissionResponseDto
 */
export async function getPermissionsForRoleServer(
  organizationId: number,
  projectId: number,
  roleId: number
): Promise<PermissionResponseDto[]> {
  const path = `/organizations/${organizationId}/projects/${projectId}/roles/${roleId}/permissions`;

  const res = await apiFetch(path);
  return asJson<PermissionResponseDto[]>(res);
}

/**
 * Get permissions for all including sensitivity labels (server-side)
 * @param organizationId - The ID of the organization
 * @returns Promise with array of PermissionResponseDto
 */
export async function getAllOrgPermissionsServer(
  organizationId: number,
  hideArchived: boolean = true,
): Promise<PermissionResponseDto[]> {
  const searchParams = new URLSearchParams();
  searchParams.append("hideArchived", hideArchived.toString());

  const queryString = searchParams.toString();
  const path = `/organizations/${organizationId}/permissions${
    queryString ? `?${queryString}` : ""
  }`;

  const res = await apiFetch(path);

  return asJson<PermissionResponseDto[]>(res);
}
