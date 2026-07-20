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
import {
  UploadProjectLogoRequest,
  UploadProjectLogoResponse,
  RemoveProjectLogoRequest,
  RemoveProjectLogoResponse,
  ProjectBannerSettings,
  SaveProjectBannerRequest,
  ProjectStorageSettings,
  AddStorageLocationRequest,
  RemoveStorageLocationRequest,
  FetchProjectLogoResponse
} from "@/app/(home)/types/project_setting_types";

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

/**
 * Get project storage size in bytes
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @returns Promise with the project storage size in bytes
 */
export async function getProjectStorageSize(
  organizationId: number,
  projectId: number
): Promise<number> {
  try {
    const res = await api.get(
      `/organizations/${organizationId}/projects/${projectId}/metrics/storage/size`
    );
    return res.data;
  } catch (error) {
    console.error(
      `Error getting storage size for project ${projectId}:`,
      error
    );
    throw error;
  }
}

/**
 * Get project data source count
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @returns Promise with the project data source count
 */
export async function getProjectDataSourceCount(
  organizationId: number,
  projectId: number
): Promise<number> {
  try {
    const res = await api.get(
      `/organizations/${organizationId}/projects/${projectId}/metrics/count`,
      { params: { hideArchived: true } }
    );
    return res.data;
  } catch (error) {
    console.error(
      `Error getting data source count for project ${projectId}:`,
      error
    );
    throw error;
  }
}

/**
 * Get project data modality count
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @returns Promise with the project data modality count
 */
export async function getProjectDataModalityCount(
  organizationId: number,
  projectId: number
): Promise<number> {
  try {
    const res = await api.get(
      `/organizations/${organizationId}/projects/${projectId}/metrics/modalities/count`,
      { params: { projectId } }
    );
    return res.data;
  } catch (error) {
    console.error(
      `Error getting data modality count for project ${projectId}:`,
      error
    );
    throw error;
  }
}

/**
 * Get project record count
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @returns Promise with the project record count
 */
export async function getProjectRecordCount(
  organizationId: number,
  projectId: number
): Promise<number> {
  try {
    const res = await api.get(
      `/organizations/${organizationId}/projects/${projectId}/metrics/records/count`,
      { params: { hideArchived: true } }
    );
    return res.data;
  } catch (error) {
    console.error(
      `Error getting record count for project ${projectId}:`,
      error
    );
    throw error;
  }
}

/**
 * Get project file count
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @returns Promise with the project file count
 */
export async function getProjectFileCount(
  organizationId: number,
  projectId: number
): Promise<number> {
  try {
    const res = await api.get(
      `/organizations/${organizationId}/projects/${projectId}/metrics/files/count`,
      { params: { hideArchived: true } }
    );
    return res.data;
  } catch (error) {
    console.error(
      `Error getting file count for project ${projectId}:`,
      error
    );
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
    isProjectAdmin?: boolean;
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
 * @param isProjectAdmin - Optional flag to set project admin status
 * @returns Promise with success message
 */
export async function updateProjectMemberRole(
  organizationId: number,
  projectId: number,
  roleId: number,
  userId?: number,
  groupId?: number,
  isProjectAdmin?: boolean
): Promise<{ message: string }> {
  try {
    const res = await api.put(
      `/organizations/${organizationId}/projects/${projectId}/members`,
      null,
      { params: { roleId, userId, groupId, isProjectAdmin } }
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

/**
 * Set project admin status for a user
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param userId - The ID of the user
 * @param isAdmin - The admin status to set
 * @returns Promise with success message
 */
export async function setProjectAdminStatus(
  organizationId: number,
  projectId: number,
  userId: number,
  isAdmin: boolean
): Promise<{ message: string }> {
  try {
    const res = await api.put(
      `/organizations/${organizationId}/projects/${projectId}/admin`,
      null,
      { params: { userId, isAdmin } }
    );
    return res.data;
  } catch (error) {
    console.error(`Error setting project admin status for user ${userId} in project ${projectId}:`, error);
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
  try {
    const formData = new FormData();
    formData.append("file", request.file);

    const res = await api.post(
      `/organizations/${request.organizationId}/projects/${request.projectId}/logo`,
      formData
    );
    return res.data;
  } catch (error) {
    console.error(
      `Failed to upload project logo for project ID ${request.projectId}: ${error}`
    );
    throw new Error(`Failed to upload project logo: ${error}`);
  }
};


/**
 * Fetch project logo image as a Blob URL
 * Returns an object containing the blob URL and filename (if known)
 */
export const fetchProjectLogo = async (
  organizationId: number,
  projectId: number
): Promise<FetchProjectLogoResponse> => {
  try {
    const res = await api.get<Blob>(
      `/organizations/${organizationId}/projects/${projectId}/logo/image`,
      { responseType: "blob" }
    );

    const contentDisposition = res.headers["content-disposition"];
    let fileName: string | undefined = undefined;

    if (contentDisposition) {
      const fileNameMatch = contentDisposition.match(/filename="?(.+)"?/);
      if (fileNameMatch && fileNameMatch.length > 1) {
        fileName = fileNameMatch[1];
      }
    }

    const blobUrl = URL.createObjectURL(res.data);
    return { blobUrl, fileName };
  } catch (error) {
    console.error(
      `Failed to fetch project logo for project ID ${projectId}:`,
      error
    );
    return { blobUrl: null };
  }
};

/**
 * Remove project logo
 * Deletes the logo file from the logos folder and updates the active_logo.txt file.
 */
export const removeProjectLogo = async (
  request: RemoveProjectLogoRequest
): Promise<RemoveProjectLogoResponse> => {
  try {
    const response = await api.delete<RemoveProjectLogoResponse>(`/organizations/${request.organizationId}/projects/${request.projectId}/logo/delete`);
    return response.data;
  } catch (error: any) {
    throw new Error(error.response?.data?.message || "Failed to remove logo");
  }
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