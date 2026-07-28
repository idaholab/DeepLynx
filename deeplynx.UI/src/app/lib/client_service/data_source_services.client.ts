"use client";

import api from "./api";
import type { DataSourceResponseDto } from "../../(home)/types/responseDTOs";
import type {
  CreateDataSourceRequestDto,
  UpdateDataSourceRequestDto,
} from "../../(home)/types/requestDTOs";

// ============================================================================
// PROJECT LEVEL API CALLS
// ============================================================================

/**
 * Get all data sources for a project
 * @param projectId - The ID of the project
 * @param hideArchived - Flag to hide archived data sources (default: true)
 * @returns Promise with array of DataSourceResponseDto
 */
export const getAllDataSources = async (
  projectId: number,
  hideArchived: boolean = true
): Promise<DataSourceResponseDto[]> => {
  try {
    const res = await api.get(`/projects/${projectId}/datasources`, {
      params: { hideArchived },
    });
    return res.data;
  } catch (error) {
    console.error("Error getting all data sources:", error);
    throw error;
  }
};

/**
 * Get a specific data source
 * @param projectId - The ID of the project
 * @param dataSourceId - The ID of the data source
 * @param hideArchived - Flag to hide archived data sources (default: true)
 * @returns Promise with DataSourceResponseDto
 */
export const getDataSource = async (
  projectId: number,
  dataSourceId: number,
  hideArchived: boolean = true
): Promise<DataSourceResponseDto> => {
  try {
    const res = await api.get(
      `/projects/${projectId}/datasources/${dataSourceId}`,
      { params: { hideArchived } }
    );
    return res.data;
  } catch (error) {
    console.error(`Error getting data source ${dataSourceId}:`, error);
    throw error;
  }
};

/**
 * Get default data source for a project
 * @param projectId - The ID of the project
 * @returns Promise with DataSourceResponseDto
 *
 * GET /projects/{projectId}/datasources/default
 */
export const getDefaultDataSource = async (
  projectId: number
): Promise<DataSourceResponseDto> => {
  try {
    const res = await api.get(`/projects/${projectId}/datasources/default`);
    return res.data;
  } catch (error) {
    console.error("Error getting default data source:", error);
    throw error;
  }
};

/**
 * Create a new data source
 * @param projectId - The ID of the project
 * @param dto - The data source creation request DTO
 * @returns Promise with DataSourceResponseDto
 */
export const createDataSource = async (
  projectId: number,
  dto: CreateDataSourceRequestDto
): Promise<DataSourceResponseDto> => {
  try {
    const res = await api.post(`/projects/${projectId}/datasources`, dto);
    return res.data;
  } catch (error) {
    console.error("Error creating data source:", error);
    throw error;
  }
};

/**
 * Update a data source
 * @param projectId - The ID of the project
 * @param dataSourceId - The ID of the data source to update
 * @param dto - The data source update request DTO
 * @returns Promise with DataSourceResponseDto
 *
 * PUT /projects/{projectId}/datasources/{dataSourceId}
 */
export const updateDataSource = async (
  projectId: number,
  dataSourceId: number,
  dto: UpdateDataSourceRequestDto
): Promise<DataSourceResponseDto> => {
  try {
    const res = await api.put(
      `/projects/${projectId}/datasources/${dataSourceId}`,
      dto
    );
    return res.data;
  } catch (error) {
    console.error(`Error updating data source ${dataSourceId}:`, error);
    throw error;
  }
};

/**
 * Delete a data source
 * @param projectId - The ID of the project
 * @param dataSourceId - The ID of the data source to delete
 * @returns Promise with success message
 */
export const deleteDataSource = async (
  projectId: number,
  dataSourceId: number
): Promise<{ message: string }> => {
  try {
    const res = await api.delete(
      `/projects/${projectId}/datasources/${dataSourceId}`
    );
    return res.data;
  } catch (error) {
    console.error(`Error deleting data source ${dataSourceId}:`, error);
    throw error;
  }
};

/**
 * Archive or unarchive a data source
 * @param projectId - The ID of the project
 * @param dataSourceId - The ID of the data source to archive/unarchive
 * @param archive - True to archive, false to unarchive
 * @returns Promise with success message
 */
export const archiveDataSource = async (
  projectId: number,
  dataSourceId: number,
  archive: boolean
): Promise<{ message: string }> => {
  try {
    const res = await api.patch(
      `/projects/${projectId}/datasources/${dataSourceId}`,
      null,
      { params: { archive } }
    );
    return res.data;
  } catch (error) {
    console.error(
      `Error ${
        archive ? "archiving" : "unarchiving"
      } data source ${dataSourceId}:`,
      error
    );
    throw error;
  }
};

/**
 * Set default data source for a project
 * @param projectId - The ID of the project
 * @param dataSourceId - The ID of the data source to set as default
 * @param isDefault - True to set as default, false to unset (default: true)
 * @returns Promise with DataSourceResponseDto
 *
 * PATCH /projects/{projectId}/datasources/{dataSourceId}/default?isDefault=true|false
 */
export const setDefaultDataSource = async (
  projectId: number,
  dataSourceId: number,
  isDefault: boolean = true
): Promise<DataSourceResponseDto> => {
  try {
    const res = await api.patch(
      `/projects/${projectId}/datasources/${dataSourceId}/default`,
      null,
      { params: { isDefault } }
    );
    return res.data;
  } catch (error) {
    console.error(`Error setting default data source ${dataSourceId}:`, error);
    throw error;
  }
};

/**
 * Get record count for a project, optionally filtered by data source.
 *
 * GET /organizations/{organizationId}/projects/{projectId}/records/count
 */
export const getRecordCountForDataSource = async (
  organizationId: number,
  projectId: number,
  dataSourceId?: number,
  hideArchived: boolean = true
): Promise<number | null> => {
  try {
    const res = await api.get(
      `/organizations/${organizationId}/projects/${projectId}/records/count`,
      {
        params: {
          dataSourceId,
          hideArchived,
        },
      }
    );

    // Backend may return `null` when there are no records
    return res.data as number | null;
  } catch (error) {
    console.error(
      `Error getting record count for project ${projectId}${
        dataSourceId ? ` and data source ${dataSourceId}` : ""
      }:`,
      error
    );
    throw error;
  }
};

// ============================================================================
// ORGANIZATION LEVEL API CALLS
// ============================================================================

/**
 * Get all data sources for an organization
 * @param organizationId - The ID of the organization
 * @param projectIds - Optional array of project IDs to filter by
 * @param hideArchived - Flag to hide archived data sources (default: true)
 * @returns Promise with array of DataSourceResponseDto
 */
export const getAllDataSourcesOrg = async (
  organizationId: number,
  projectIds?: number[],
  hideArchived: boolean = true
): Promise<DataSourceResponseDto[]> => {
  try {
    const projectIdsQuery = projectIds?.map(id => `projectIds=${id}`).join('&') ?? '';
    const separator = projectIdsQuery ? '&' : '';
    const res = await api.get(
      `/organizations/${organizationId}/datasources?${projectIdsQuery}${separator}hideArchived=${hideArchived}`
    );
    return res.data;
  } catch (error) {
    console.error("Error getting all data sources for organization:", error);
    throw error;
  }
};

/**
 * Get a specific data source at organization level
 * @param organizationId - The ID of the organization
 * @param dataSourceId - The ID of the data source
 * @param hideArchived - Flag to hide archived data sources (default: true)
 * @returns Promise with DataSourceResponseDto
 */
export const getDataSourceOrg = async (
  organizationId: number,
  dataSourceId: number,
  hideArchived: boolean = true
): Promise<DataSourceResponseDto> => {
  try {
    const res = await api.get(
      `/organizations/${organizationId}/datasources/${dataSourceId}`,
      { params: { hideArchived } }
    );
    return res.data;
  } catch (error) {
    console.error(
      `Error getting data source ${dataSourceId} for organization:`,
      error
    );
    throw error;
  }
};

/**
 * Get default data source at organization level
 * @param organizationId - The ID of the organization
 * @returns Promise with DataSourceResponseDto
 */
export const getDefaultDataSourceOrg = async (
  organizationId: number
): Promise<DataSourceResponseDto> => {
  try {
    const res = await api.get(
      `/organizations/${organizationId}/datasources/default`
    );
    return res.data;
  } catch (error) {
    console.error("Error getting default data source for organization:", error);
    throw error;
  }
};

/**
 * Update a data source at organization level
 * @param organizationId - The ID of the organization
 * @param dataSourceId - The ID of the data source to update
 * @param dto - The data source update request DTO
 * @returns Promise with DataSourceResponseDto
 */
export const updateDataSourceOrg = async (
  organizationId: number,
  dataSourceId: number,
  dto: UpdateDataSourceRequestDto
): Promise<DataSourceResponseDto> => {
  try {
    const res = await api.put(
      `/organizations/${organizationId}/datasources/${dataSourceId}`,
      dto
    );
    return res.data;
  } catch (error) {
    console.error(
      `Error updating data source ${dataSourceId} for organization:`,
      error
    );
    throw error;
  }
};

/**
 * Delete a data source at organization level
 * @param organizationId - The ID of the organization
 * @param dataSourceId - The ID of the data source to delete
 * @returns Promise with success message
 */
export const deleteDataSourceOrg = async (
  organizationId: number,
  dataSourceId: number
): Promise<{ message: string }> => {
  try {
    const res = await api.delete(
      `/organizations/${organizationId}/datasources/${dataSourceId}`
    );
    return res.data;
  } catch (error) {
    console.error(
      `Error deleting data source ${dataSourceId} for organization:`,
      error
    );
    throw error;
  }
};

/**
 * Archive or unarchive a data source at organization level
 * @param organizationId - The ID of the organization
 * @param dataSourceId - The ID of the data source to archive/unarchive
 * @param archive - True to archive, false to unarchive
 * @returns Promise with success message
 */
export const archiveDataSourceOrg = async (
  organizationId: number,
  dataSourceId: number,
  archive: boolean
): Promise<{ message: string }> => {
  try {
    const res = await api.patch(
      `/organizations/${organizationId}/datasources/${dataSourceId}`,
      null,
      { params: { archive } }
    );
    return res.data;
  } catch (error) {
    console.error(
      `Error ${
        archive ? "archiving" : "unarchiving"
      } data source ${dataSourceId} for organization:`,
      error
    );
    throw error;
  }
};

/**
 * Set default data source at organization level
 * @param organizationId - The ID of the organization
 * @param dataSourceId - The ID of the data source to set as default
 * @returns Promise with DataSourceResponseDto
 */
export const setDefaultDataSourceOrg = async (
  organizationId: number,
  dataSourceId: number
): Promise<DataSourceResponseDto> => {
  try {
    const res = await api.patch(
      `/organizations/${organizationId}/datasources/${dataSourceId}/default`
    );
    return res.data;
  } catch (error) {
    console.error(
      `Error setting default data source ${dataSourceId} for organization:`,
      error
    );
    throw error;
  }
};
