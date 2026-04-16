'use client';

import api from './api';
import { TagResponseDto } from '../../(home)/types/responseDTOs';
import { CreateTagRequestDto, UpdateTagRequestDto } from '../../(home)/types/requestDTOs';

// ============================================================================
// PROJECT LEVEL API CALLS
// ============================================================================

/**
 * Get all tags for a project
 * @param projectId - The ID of the project
 * @param hideArchived - Flag to hide archived tags (default: true)
 * @returns Promise with array of TagResponseDto
 */
export const getAllTags = async (
  projectId: number,
  hideArchived: boolean = true
): Promise<TagResponseDto[]> => {
  try {
    const res = await api.get(
      `/projects/${projectId}/tags`,
      { params: { hideArchived } }
    );
    return res.data;
  } catch (error) {
    console.error("Error getting all tags:", error);
    throw error;
  }
};

/**
 * Get a specific tag
 * @param projectId - The ID of the project
 * @param tagId - The ID of the tag
 * @param hideArchived - Flag to hide archived tags (default: true)
 * @returns Promise with TagResponseDto
 */
export const getTag = async (
  projectId: number,
  tagId: number,
  hideArchived: boolean = true
): Promise<TagResponseDto> => {
  try {
    const res = await api.get(
      `/projects/${projectId}/tags/${tagId}`,
      { params: { hideArchived } }
    );
    return res.data;
  } catch (error) {
    console.error(`Error getting tag ${tagId}:`, error);
    throw error;
  }
};

/**
 * Create a new tag
 * @param projectId - The ID of the project
 * @param dto - The tag creation request DTO
 * @returns Promise with TagResponseDto
 */
export const createTag = async (
  projectId: number,
  dto: CreateTagRequestDto
): Promise<TagResponseDto> => {
  try {
    const res = await api.post(
      `/projects/${projectId}/tags`,
      dto
    );
    return res.data;
  } catch (error) {
    console.error("Error creating tag:", error);
    throw error;
  }
};

/**
 * Bulk create tags
 * @param projectId - The ID of the project
 * @param tags - Array of tag creation request DTOs
 * @returns Promise with array of TagResponseDto
 */
export const bulkCreateTags = async (
  projectId: number,
  tags: CreateTagRequestDto[]
): Promise<TagResponseDto[]> => {
  try {
    const res = await api.post(
      `/projects/${projectId}/tags/bulk`,
      tags
    );
    return res.data;
  } catch (error) {
    console.error("Error bulk creating tags:", error);
    throw error;
  }
};

/**
 * Update a tag
 * @param projectId - The ID of the project
 * @param tagId - The ID of the tag to update
 * @param dto - The tag update request DTO
 * @returns Promise with TagResponseDto
 */
export const updateTag = async (
  projectId: number,
  tagId: number,
  dto: UpdateTagRequestDto
): Promise<TagResponseDto> => {
  try {
    const res = await api.put(
      `/projects/${projectId}/tags/${tagId}`,
      dto
    );
    return res.data;
  } catch (error) {
    console.error(`Error updating tag ${tagId}:`, error);
    throw error;
  }
};

/**
 * Delete a tag
 * @param projectId - The ID of the project
 * @param tagId - The ID of the tag to delete
 * @returns Promise with success message
 */
export const deleteTag = async (
  projectId: number,
  tagId: number
): Promise<{ message: string }> => {
  try {
    const res = await api.delete(
      `/projects/${projectId}/tags/${tagId}`
    );
    return res.data;
  } catch (error) {
    console.error(`Error deleting tag ${tagId}:`, error);
    throw error;
  }
};

/**
 * Archive or unarchive a tag
 * @param projectId - The ID of the project
 * @param tagId - The ID of the tag to archive/unarchive
 * @param archive - True to archive, false to unarchive
 * @returns Promise with success message
 */
export const archiveTag = async (
  projectId: number,
  tagId: number,
  archive: boolean
): Promise<{ message: string }> => {
  try {
    const res = await api.patch(
      `/projects/${projectId}/tags/${tagId}`,
      null,
      { params: { archive } }
    );
    return res.data;
  } catch (error) {
    console.error(`Error ${archive ? 'archiving' : 'unarchiving'} tag ${tagId}:`, error);
    throw error;
  }
};

// ============================================================================
// ORGANIZATION LEVEL API CALLS
// ============================================================================

/**
 * Get all tags for an organization
 * @param organizationId - The ID of the organization
 * @param projectIds - Optional array of project IDs to filter by
 * @param hideArchived - Flag to hide archived tags (default: true)
 * @returns Promise with array of TagResponseDto
 */
export const getAllTagsOrg = async (
  organizationId: number,
  projectIds?: number[],
  hideArchived: boolean = true
): Promise<TagResponseDto[]> => {
  try {
    const projectIdsQuery = projectIds?.map(id => `projectIds=${id}`).join('&') ?? '';
    const separator = projectIdsQuery ? '&' : '';
    const res = await api.get(
        `/organizations/${organizationId}/tags?${projectIdsQuery}${separator}hideArchived=${hideArchived}`
    );
    return res.data;
  } catch (error) {
    console.error("Error getting all tags for organization:", error);
    throw error;
  }
};

/**
 * Get a specific tag at organization level
 * @param organizationId - The ID of the organization
 * @param tagId - The ID of the tag
 * @param hideArchived - Flag to hide archived tags (default: true)
 * @returns Promise with TagResponseDto
 */
export const getTagOrg = async (
  organizationId: number,
  tagId: number,
  hideArchived: boolean = true
): Promise<TagResponseDto> => {
  try {
    const res = await api.get(
      `/organizations/${organizationId}/tags/${tagId}`,
      { params: { hideArchived } }
    );
    return res.data;
  } catch (error) {
    console.error(`Error getting tag ${tagId} for organization:`, error);
    throw error;
  }
};

/**
 * Create a new tag at organization level
 * @param organizationId - The ID of the organization
 * @param dto - The tag creation request DTO
 * @returns Promise with TagResponseDto
 */
export const createTagOrg = async (
  organizationId: number,
  dto: CreateTagRequestDto
): Promise<TagResponseDto> => {
  try {
    const res = await api.post(
      `/organizations/${organizationId}/tags`,
      dto
    );
    return res.data;
  } catch (error) {
    console.error("Error creating tag for organization:", error);
    throw error;
  }
};

/**
 * Bulk create tags at organization level
 * @param organizationId - The ID of the organization
 * @param tags - Array of tag creation request DTOs
 * @returns Promise with array of TagResponseDto
 */
export const bulkCreateTagsOrg = async (
  organizationId: number,
  tags: CreateTagRequestDto[]
): Promise<TagResponseDto[]> => {
  try {
    const res = await api.post(
      `/organizations/${organizationId}/tags/bulk`,
      tags
    );
    return res.data;
  } catch (error) {
    console.error("Error bulk creating tags for organization:", error);
    throw error;
  }
};

/**
 * Update a tag at organization level
 * @param organizationId - The ID of the organization
 * @param tagId - The ID of the tag to update
 * @param dto - The tag update request DTO
 * @returns Promise with TagResponseDto
 */
export const updateTagOrg = async (
  organizationId: number,
  tagId: number,
  dto: UpdateTagRequestDto
): Promise<TagResponseDto> => {
  try {
    const res = await api.put(
      `/organizations/${organizationId}/tags/${tagId}`,
      dto
    );
    return res.data;
  } catch (error) {
    console.error(`Error updating tag ${tagId} for organization:`, error);
    throw error;
  }
};

/**
 * Delete a tag at organization level
 * @param organizationId - The ID of the organization
 * @param tagId - The ID of the tag to delete
 * @returns Promise with success message
 */
export const deleteTagOrg = async (
  organizationId: number,
  tagId: number
): Promise<{ message: string }> => {
  try {
    const res = await api.delete(
      `/organizations/${organizationId}/tags/${tagId}`
    );
    return res.data;
  } catch (error) {
    console.error(`Error deleting tag ${tagId} for organization:`, error);
    throw error;
  }
};

/**
 * Archive or unarchive a tag at organization level
 * @param organizationId - The ID of the organization
 * @param tagId - The ID of the tag to archive/unarchive
 * @param archive - True to archive, false to unarchive
 * @returns Promise with success message
 */
export const archiveTagOrg = async (
  organizationId: number,
  tagId: number,
  archive: boolean
): Promise<{ message: string }> => {
  try {
    const res = await api.patch(
      `/organizations/${organizationId}/tags/${tagId}`,
      null,
      { params: { archive } }
    );
    return res.data;
  } catch (error) {
    console.error(`Error ${archive ? 'archiving' : 'unarchiving'} tag ${tagId} for organization:`, error);
    throw error;
  }
};