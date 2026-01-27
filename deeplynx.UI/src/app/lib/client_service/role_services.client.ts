import api from './api';
import { PermissionResponseDto, RoleResponseDto } from '../../(home)/types/responseDTOs';
import { CreateRoleRequestDto, UpdateRoleRequestDto } from '../../(home)/types/requestDTOs';

// ============================================================================
// PROJECT LEVEL API CALLS
// ============================================================================

// ===== Role CRUD Operations =====

/**
 * Get all roles for a project
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param hideArchived - Flag to hide archived roles (default: true)
 * @returns Promise with array of RoleResponseDto
 */
export async function getAllRoles(
  organizationId: number,
  projectId?: number,
  hideArchived: boolean = true
): Promise<RoleResponseDto[]> {
  try {
    const res = await api.get(
      `/organizations/${organizationId}/projects/${projectId}/roles`,
      { params: { hideArchived } }
    );
    return res.data;
  } catch (error) {
    console.error("Error getting all roles:", error);
    throw error;
  }
}

/**
 * Get a specific role by ID
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param roleId - The ID of the role
 * @param hideArchived - Flag to hide archived roles (default: true)
 * @returns Promise with RoleResponseDto
 */
export async function getRoleById(
  organizationId: number,
  projectId: number,
  roleId: number,
  hideArchived: boolean = true
): Promise<RoleResponseDto> {
  try {
    const res = await api.get(
      `/organizations/${organizationId}/projects/${projectId}/roles/${roleId}`,
      { params: { hideArchived } }
    );
    return res.data;
  } catch (error) {
    console.error(`Error getting role ${roleId}:`, error);
    throw error;
  }
}

/**
 * Create a new role
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param dto - The role creation request DTO
 * @returns Promise with RoleResponseDto
 */
export async function createRole(
  organizationId: number,
  projectId: number,
  dto: CreateRoleRequestDto
): Promise<RoleResponseDto> {
  try {
    const res = await api.post(
      `/organizations/${organizationId}/projects/${projectId}/roles`,
      dto,
      { headers: { "Content-Type": "application/json" } }
    );
    return res.data;
  } catch (error) {
    console.error("Error creating role:", error);
    throw error;
  }
}

/**
 * Update a role
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param roleId - The ID of the role to update
 * @param dto - The role update request DTO
 * @returns Promise with RoleResponseDto
 */
export async function updateRole(
  organizationId: number,
  projectId: number,
  roleId: number,
  dto: UpdateRoleRequestDto
): Promise<RoleResponseDto> {
  try {
    const res = await api.put(
      `/organizations/${organizationId}/projects/${projectId}/roles/${roleId}`,
      dto,
      { headers: { "Content-Type": "application/json" } }
    );
    return res.data;
  } catch (error) {
    console.error(`Error updating role ${roleId}:`, error);
    throw error;
  }
}

/**
 * Delete a role
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param roleId - The ID of the role to delete
 * @returns Promise with success message
 */
export async function deleteRole(
  organizationId: number,
  projectId: number,
  roleId: number
): Promise<{ message: string }> {
  try {
    const res = await api.delete(
      `/organizations/${organizationId}/projects/${projectId}/roles/${roleId}`
    );
    return res.data;
  } catch (error) {
    console.error(`Error deleting role ${roleId}:`, error);
    throw error;
  }
}

/**
 * Archive or unarchive a role
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param roleId - The ID of the role to archive/unarchive
 * @param archive - True to archive, false to unarchive
 * @returns Promise with success message
 */
export async function archiveRole(
  organizationId: number,
  projectId: number,
  roleId: number,
  archive: boolean = true
): Promise<{ message: string }> {
  try {
    const res = await api.patch(
      `/organizations/${organizationId}/projects/${projectId}/roles/${roleId}`,
      null,
      { params: { archive } }
    );
    return res.data;
  } catch (error) {
    console.error(`Error ${archive ? 'archiving' : 'unarchiving'} role ${roleId}:`, error);
    throw error;
  }
}

// ===== Project Level Permission Operations =====

/**
 * Get all permissions for a role at the organization level
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param roleId - The ID of the role
 * @returns Promise with array of PermissionResponseDto
 */
export async function getRolePermissions(
  organizationId: number,
  projectId: number,
  roleId: number
): Promise<PermissionResponseDto[]> {
  try {
    const res = await api.get(
      `/organizations/${organizationId}/projects/${projectId}/roles/${roleId}/permissions`
    );
    return res.data;
  } catch (error) {
    console.error(`Error getting permissions for role ${roleId}:`, error);
    throw error;
  }
}

/**
 * Add a permission to a role
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param roleId - The ID of the role
 * @param permissionId - The ID of the permission to add
 * @returns Promise with success message
 */
export async function addPermissionToRole(
  organizationId: number,
  projectId: number,
  roleId: number,
  permissionId: number
): Promise<{ message: string }> {
  try {
    const res = await api.post(
      `/organizations/${organizationId}/projects/${projectId}/roles/${roleId}/permissions/${permissionId}`
    );
    return res.data;
  } catch (error) {
    console.error(`Error adding permission ${permissionId} to role ${roleId}:`, error);
    throw error;
  }
}

/**
 * Remove a permission from a role
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param roleId - The ID of the role
 * @param permissionId - The ID of the permission to remove
 * @returns Promise with success message
 */
export async function removePermissionFromRole(
  organizationId: number,
  projectId: number,
  roleId: number,
  permissionId: number
): Promise<{ message: string }> {
  try {
    const res = await api.delete(
      `/organizations/${organizationId}/projects/${projectId}/roles/${roleId}/permissions/${permissionId}`
    );
    return res.data;
  } catch (error) {
    console.error(`Error removing permission ${permissionId} from role ${roleId}:`, error);
    throw error;
  }
}

/**
 * Set all permissions for a role (replaces existing permissions)
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param roleId - The ID of the role
 * @param permissionIds - Array of permission IDs to assign to the role
 * @returns Promise with success message
 */
export async function setPermissionsForRole(
  organizationId: number,
  projectId: number,
  roleId: number,
  permissionIds: number[]
): Promise<{ message: string }> {
  try {
    const res = await api.put(
      `/organizations/${organizationId}/projects/${projectId}/roles/${roleId}/permissions`,
      permissionIds,
      { headers: { "Content-Type": "application/json" } }
    );
    return res.data;
  } catch (error) {
    console.error(`Error setting permissions for role ${roleId}:`, error);
    throw error;
  }
}

// ============================================================================
// ORGANIZATION LEVEL API CALLS
// ============================================================================

// ===== Org Role CRUD Operations =====

/**
 * Get all roles for an org
 * @param organizationId - The ID of the organization
 * @param hideArchived - Flag to hide archived roles (default: true)
 * @returns Promise with array of RoleResponseDto
 */
export async function getAllOrgRoles(
  organizationId: number,
  hideArchived: boolean = true,
): Promise<RoleResponseDto[]> {
  try {
    const res = await api.get(
      `/organizations/${organizationId}/roles`,
      { params: { hideArchived } }
    );
    return res.data;
  } catch (error) {
    console.error("Error getting all roles:", error);
    throw error;
  }
}

/**
 * Get a specific role by ID
 * @param organizationId - The ID of the organization
 * @param roleId - The ID of the role
 * @param hideArchived - Flag to hide archived roles (default: true)
 * @returns Promise with RoleResponseDto
 */
export async function getOrgRoleById(
  organizationId: number,
  roleId: number,
  hideArchived: boolean = true,
): Promise<RoleResponseDto> {
  try {
    const res = await api.get(
      `/organizations/${organizationId}/roles/${roleId}`,
      { params: { hideArchived } }
    );
    return res.data;
  } catch (error) {
    console.error(`Error getting role ${roleId}:`, error);
    throw error;
  }
}

/**
 * Create a new org role
 * @param organizationId - The ID of the organization
 * @param dto - The role creation request DTO
 * @returns Promise with RoleResponseDto
 */
export async function createOrgRole(
  organizationId: number,
  dto: CreateRoleRequestDto,
): Promise<RoleResponseDto> {
  try {
    const res = await api.post(
      `/organizations/${organizationId}/roles`,
      dto,
      { headers: { "Content-Type": "application/json" } }
    );
    return res.data;
  } catch (error) {
    console.error("Error creating role:", error);
    throw error;
  }
}

/**
 * Update a role
 * @param organizationId - The ID of the organization
 * @param roleId - The ID of the role to update
 * @param dto - The role update request DTO
 * @returns Promise with RoleResponseDto
 */
export async function updateOrgRole(
  organizationId: number,
  roleId: number,
  dto: UpdateRoleRequestDto,
): Promise<RoleResponseDto> {
  try {
    const res = await api.put(
      `/organizations/${organizationId}/roles/${roleId}`,
      dto,
      { headers: { "Content-Type": "application/json" } }
    );
    return res.data;
  } catch (error) {
    console.error(`Error updating role ${roleId}:`, error);
    throw error;
  }
}

/**
 * Delete a role
 * @param organizationId - The ID of the organization
 * @param roleId - The ID of the role to delete
 * @returns Promise with success message
 */
export async function deleteOrgRole(
  organizationId: number,
  roleId: number,
): Promise<{ message: string }> {
  try {
    const res = await api.delete(
      `/organizations/${organizationId}/roles/${roleId}`
    );
    return res.data;
  } catch (error) {
    console.error(`Error deleting role ${roleId}:`, error);
    throw error;
  }
}

/**
 * Archive or unarchive a role
 * @param organizationId - The ID of the organization
 * @param roleId - The ID of the role to archive/unarchive
 * @param archive - True to archive, false to unarchive
 * @returns Promise with success message
 */
export async function archiveOrgRole(
  organizationId: number,
  roleId: number,
  archive: boolean = true,
): Promise<{ message: string }> {
  try {
    const res = await api.patch(
      `/organizations/${organizationId}/roles/${roleId}`,
      null,
      { params: { archive } }
    );
    return res.data;
  } catch (error) {
    console.error(`Error ${archive ? 'archiving' : 'unarchiving'} role ${roleId}:`, error);
    throw error;
  }
}

// ===== Organization Level Permission Operations =====

/**
 * Get all permissions for a role at the organization level
 * @param organizationId - The ID of the organization
 * @param roleId - The ID of the role
 * @returns Promise with array of PermissionResponseDto
 */
export async function getOrgRolePermissions(
  organizationId: number,
  roleId: number
): Promise<PermissionResponseDto[]> {
  try {
    const res = await api.get(
      `/organizations/${organizationId}/roles/${roleId}/permissions`
    );
    return res.data;
  } catch (error) {
    console.error(`Error getting permissions for role ${roleId}:`, error);
    throw error;
  }
}

/**
 * Add a permission to a role
 * @param organizationId - The ID of the organization
 * @param roleId - The ID of the role
 * @param permissionId - The ID of the permission to add
 * @returns Promise with success message
 */
export async function addPermissionToOrgRole(
  organizationId: number,
  roleId: number,
  permissionId: number,
): Promise<{ message: string }> {
  try {
    const res = await api.post(
      `/organizations/${organizationId}/roles/${roleId}/permissions/${permissionId}`
    );
    return res.data;
  } catch (error) {
    console.error(`Error adding permission ${permissionId} to role ${roleId}:`, error);
    throw error;
  }
}

/**
 * Remove a permission from a role
 * @param organizationId - The ID of the organization
 * @param roleId - The ID of the role
 * @param permissionId - The ID of the permission to remove
 * @returns Promise with success message
 */
export async function removePermissionFromOrgRole(
  organizationId: number,
  roleId: number,
  permissionId: number,
): Promise<{ message: string }> {
  try {
    const res = await api.delete(
      `/organizations/${organizationId}/roles/${roleId}/permissions/${permissionId}`
    );
    return res.data;
  } catch (error) {
    console.error(`Error removing permission ${permissionId} from role ${roleId}:`, error);
    throw error;
  }
}

/**
 * Set all permissions for a role (replaces existing permissions)
 * @param organizationId - The ID of the organization
 * @param roleId - The ID of the role
 * @param permissionIds - Array of permission IDs to assign to the role
 * @returns Promise with success message
 */
export async function setPermissionsForOrgRole(
  organizationId: number,
  roleId: number,
  permissionIds: number[],
): Promise<{ message: string }> {
  try {
    const res = await api.put(
      `/organizations/${organizationId}/roles/${roleId}/permissions`,
      permissionIds,
      { headers: { "Content-Type": "application/json" } }
    );
    return res.data;
  } catch (error) {
    console.error(`Error setting permissions for role ${roleId}:`, error);
    throw error;
  }
}