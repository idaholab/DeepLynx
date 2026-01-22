// src/app/lib/client_service/projects_services.client.ts
"use client";

import { 
  CreateProjectRequestDto, 
  UpdateProjectRequestDto,
  InviteUserToProjectRequestDto 
} from "@/app/(home)/types/requestDTOs";
import { 
  ProjectResponseDto, 
  ProjectStatResponseDto, 
  ProjectMemberResponseDto 
} from "@/app/(home)/types/responseDTOs";
import api from "./api";
import { UploadProjectLogoRequest, UploadProjectLogoResponse, RemoveProjectLogoRequest, RemoveProjectLogoResponse, ProjectBannerSettings, SaveProjectBannerRequest, ProjectStorageSettings, AddStorageLocationRequest, RemoveStorageLocationRequest } from "@/app/(home)/types/project_setting_types";

/* -------------------------------------------------------------------------- */
/*                         Project CRUD Operations                            */
/* -------------------------------------------------------------------------- */

/**
 * Get all projects for an organization
 * @param organizationId - The ID of the organization
 * @param hideArchived - Flag to hide archived projects (default: true)
 * @returns Promise with array of ProjectResponseDto
 */
export async function getAllProjects(
  organizationId: number,
  hideArchived: boolean = true
): Promise<ProjectResponseDto[]> {
  try {
    const res = await api.get(
      `/organizations/${organizationId}/projects`,
      { params: { hideArchived } }
    );
    return res.data;
  } catch (error) {
    console.error("Error getting all projects:", error);
    throw error;
  }
}

/**
 * Get a specific project by ID
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param hideArchived - Flag to hide archived projects (default: true)
 * @returns Promise with ProjectResponseDto
 */
export async function getProject(
  organizationId: number,
  projectId: number,
  hideArchived: boolean = true
): Promise<ProjectResponseDto> {
  try {
    const res = await api.get(
      `/organizations/${organizationId}/projects/${projectId}`,
      { params: { hideArchived } }
    );
    return res.data;
  } catch (error) {
    console.error(`Error getting project ${projectId}:`, error);
    throw error;
  }
}

/**
 * Create a new project
 * @param organizationId - The ID of the organization
 * @param dto - The project creation request DTO
 * @returns Promise with ProjectResponseDto
 */
export async function createProject(
  organizationId: number,
  dto: CreateProjectRequestDto
): Promise<ProjectResponseDto> {
  try {
    const res = await api.post(
      `/organizations/${organizationId}/projects`,
      dto,
      { headers: { "Content-Type": "application/json" } }
    );
    return res.data;
  } catch (error) {
    console.error("Error creating project:", error);
    throw error;
  }
}

/**
 * Update a project
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project to update
 * @param dto - The project update request DTO
 * @returns Promise with ProjectResponseDto
 */
export async function updateProject(
  organizationId: number,
  projectId: number,
  dto: UpdateProjectRequestDto
): Promise<ProjectResponseDto> {
  try {
    const res = await api.put(
      `/organizations/${organizationId}/projects/${projectId}`,
      dto,
      { headers: { "Content-Type": "application/json" } }
    );
    return res.data;
  } catch (error) {
    console.error(`Error updating project ${projectId}:`, error);
    throw error;
  }
}

/**
 * Delete a project
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project to delete
 * @returns Promise with success message
 */
export async function deleteProject(
  organizationId: number,
  projectId: number
): Promise<{ message: string }> {
  try {
    const res = await api.delete(
      `/organizations/${organizationId}/projects/${projectId}`
    );
    return res.data;
  } catch (error) {
    console.error(`Error deleting project ${projectId}:`, error);
    throw error;
  }
}

/**
 * Archive or unarchive a project
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project to archive/unarchive
 * @param archive - True to archive, false to unarchive
 * @returns Promise with success message
 */
export async function archiveProject(
  organizationId: number,
  projectId: number,
  archive: boolean
): Promise<{ message: string }> {
  try {
    const res = await api.patch(
      `/organizations/${organizationId}/projects/${projectId}`,
      null,
      { params: { archive } }
    );
    return res.data;
  } catch (error) {
    console.error(`Error ${archive ? 'archiving' : 'unarchiving'} project ${projectId}:`, error);
    throw error;
  }
}

/**
 * Get project statistics
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @returns Promise with ProjectStatResponseDto
 */
export async function getProjectStats(
  organizationId: number,
  projectId: number
): Promise<ProjectStatResponseDto> {
  try {
    const res = await api.get(
      `/organizations/${organizationId}/projects/${projectId}/stats`
    );
    return res.data;
  } catch (error) {
    console.error(`Error getting stats for project ${projectId}:`, error);
    throw error;
  }
}

/* -------------------------------------------------------------------------- */
/*                         Project Member Management                          */
/* -------------------------------------------------------------------------- */

/**
 * Get all members of a project
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @returns Promise with project members data
 */
export async function getProjectMembers(
  organizationId: number,
  projectId: number
): Promise<ProjectMemberResponseDto[]> {
  try {
    const res = await api.get(
      `/organizations/${organizationId}/projects/${projectId}/members`
    );
    return res.data;
  } catch (error) {
    console.error(`Error getting members for project ${projectId}:`, error);
    throw error;
  }
}

/**
 * Invite a user to a project
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param inviteData - The invite request data (userEmail, userName, roleId)
 * @returns Promise<void>
 */
export async function inviteUserToProject(
  organizationId: number,
  projectId: number,
  inviteData: InviteUserToProjectRequestDto
): Promise<void> {
  try {
    await api.post(
      `/organizations/${organizationId}/projects/${projectId}/invite`,
      null,
      {
        params: {
          userEmail: inviteData.userEmail,
          ...(inviteData.userName && { userName: inviteData.userName }),
          ...(inviteData.roleId && { roleId: inviteData.roleId })
        }
      }
    );
  } catch (error) {
    console.error(`Error inviting user to project ${projectId}:`, error);
    throw error;
  }
}

/**
 * Add a user or group to a project
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param data - Object containing roleId and either userId or groupId
 * @returns Promise with success message
 */
export async function addMemberToProject(
  organizationId: number,
  projectId: number,
  data: {
    roleId?: number;
    userId?: number;
    groupId?: number;
  }
): Promise<{ message: string }> {
  try {
    const res = await api.post(
      `/organizations/${organizationId}/projects/${projectId}/members`,
      null,
      { params: data }
    );
    return res.data;
  } catch (error) {
    console.error(`Error adding member to project ${projectId}:`, error);
    throw error;
  }
}

/**
 * Update a member's role in a project
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param roleId - The new role ID
 * @param userId - Optional user ID (required if not providing groupId)
 * @param groupId - Optional group ID (required if not providing userId)
 * @returns Promise with success message
 */
export async function updateProjectMemberRole(
  organizationId: number,
  projectId: number,
  roleId: number,
  userId?: number,
  groupId?: number
): Promise<{ message: string }> {
  try {
    const res = await api.put(
      `/organizations/${organizationId}/projects/${projectId}/members`,
      null,
      { params: { roleId, userId, groupId } }
    );
    return res.data;
  } catch (error) {
    console.error(`Error updating member role in project ${projectId}:`, error);
    throw error;
  }
}

/**
 * Remove a user or group from a project
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param userId - Optional user ID (required if not providing groupId)
 * @param groupId - Optional group ID (required if not providing userId)
 * @returns Promise with success message
 */
export async function removeMemberFromProject(
  organizationId: number,
  projectId: number,
  userId?: number,
  groupId?: number
): Promise<{ message: string }> {
  try {
    const res = await api.delete(
      `/organizations/${organizationId}/projects/${projectId}/members`,
      { params: { userId, groupId } }
    );
    return res.data;
  } catch (error) {
    console.error(`Error removing member from project ${projectId}:`, error);
    throw error;
  }
}

/* -------------------------------------------------------------------------- */
/*                            PROJECT LOGO SERVICES                           */
/* -------------------------------------------------------------------------- */

/**
 * Upload project logo
 * Saves the logo to /public/images/project-{projectId}-logo.{ext}
 */
export const uploadProjectLogo = async (
  request: UploadProjectLogoRequest
): Promise<UploadProjectLogoResponse> => {
  const formData = new FormData();
  formData.append("file", request.file);

  const response = await fetch(
    `/api/project/${request.projectId}/logo`,
    {
      method: "POST",
      body: formData,
    }
  );

  if (!response.ok) {
    const error = await response.json();
    throw new Error(error.message || "Failed to upload logo");
  }

  return response.json();
};

/**
 * Remove project logo
 * Deletes the logo file from /public/images
 */
export const removeProjectLogo = async (
  request: RemoveProjectLogoRequest
): Promise<RemoveProjectLogoResponse> => {
  const response = await fetch(
    `/api/project/${request.projectId}/logo`,
    {
      method: "DELETE",
    }
  );

  if (!response.ok) {
    const error = await response.json();
    throw new Error(error.message || "Failed to remove logo");
  }

  return response.json();
};

/**
 * Get project logo URL and check if it exists
 * Returns the logo URL if the file exists, null otherwise
 */
export const getProjectLogoUrl = async (
  projectId: number
): Promise<string | null> => {
  try {
    const response = await fetch(`/api/project/${projectId}/logo`, {
      method: "GET",
    });

    if (!response.ok) {
      return null;
    }

    const data = await response.json();
    return data.exists ? data.logoUrl : null;
  } catch (error) {
    console.error("Error getting project logo URL:", error);
    return null;
  }
};

/**
 * Check if project logo exists
 * Returns true if a logo file exists for the project
 */
export const checkProjectLogoExists = async (
  projectId: number
): Promise<boolean> => {
  try {
    const logoUrl = await getProjectLogoUrl(projectId);
    return logoUrl !== null;
  } catch (error) {
    console.error("Error checking project logo existence:", error);
    return false;
  }
};
