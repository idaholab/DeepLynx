'use client';

import api from './api';
import { ClassResponseDto } from '../../(home)/types/responseDTOs';
import { CreateClassRequestDto, UpdateClassRequestDto } from '../../(home)/types/requestDTOs';

// ============================================================================
// PROJECT LEVEL API CALLS
// ============================================================================

/**
 * Get all classes for a project
 * @param projectId - The ID of the project
 * @param hideArchived - Flag to hide archived classes (default: true)
 * @returns Promise with array of ClassResponseDto
 */
export const getAllClasses = async (
    projectId: number,
    hideArchived: boolean = true
): Promise<ClassResponseDto[]> => {
    try {
        const res = await api.get(
            `/projects/${projectId}/classes`,
            { params: { hideArchived } }
        );
        return res.data;
    } catch (error) {
        console.error("Error getting all classes:", error);
        throw error;
    }
};

/**
 * Get a specific class
 * @param projectId - The ID of the project
 * @param classId - The ID of the class
 * @param hideArchived - Flag to hide archived classes (default: true)
 * @returns Promise with ClassResponseDto
 */
export const getClass = async (
    projectId: number,
    classId: number,
    hideArchived: boolean = true
): Promise<ClassResponseDto> => {
    try {
        const res = await api.get(
            `/projects/${projectId}/classes/${classId}`,
            { params: { hideArchived } }
        );
        return res.data;
    } catch (error) {
        console.error(`Error getting class ${classId}:`, error);
        throw error;
    }
};

/**
 * Create a new class
 * @param projectId - The ID of the project
 * @param dto - The class creation request DTO
 * @returns Promise with ClassResponseDto
 */
export const createClass = async (
    projectId: number,
    dto: CreateClassRequestDto
): Promise<ClassResponseDto> => {
    try {
        const res = await api.post(
            `/projects/${projectId}/classes`,
            dto
        );
        return res.data;
    } catch (error) {
        console.error("Error creating class:", error);
        throw error;
    }
};

/**
 * Bulk create classes
 * @param projectId - The ID of the project
 * @param classes - Array of class creation request DTOs
 * @returns Promise with array of ClassResponseDto
 */
export const bulkCreateClasses = async (
    projectId: number,
    classes: CreateClassRequestDto[]
): Promise<ClassResponseDto[]> => {
    try {
        const res = await api.post(
            `/projects/${projectId}/classes/bulk`,
            classes
        );
        return res.data;
    } catch (error) {
        console.error("Error bulk creating classes:", error);
        throw error;
    }
};

/**
 * Update a class
 * @param projectId - The ID of the project
 * @param classId - The ID of the class to update
 * @param dto - The class update request DTO
 * @returns Promise with ClassResponseDto
 */
export const updateClass = async (
    projectId: number,
    classId: number,
    dto: UpdateClassRequestDto
): Promise<ClassResponseDto> => {
    try {
        const res = await api.put(
            `/projects/${projectId}/classes/${classId}`,
            dto
        );
        return res.data;
    } catch (error) {
        console.error(`Error updating class ${classId}:`, error);
        throw error;
    }
};

/**
 * Delete a class
 * @param projectId - The ID of the project
 * @param classId - The ID of the class to delete
 * @returns Promise with success message
 */
export const deleteClass = async (
    projectId: number,
    classId: number
): Promise<{ message: string }> => {
    try {
        const res = await api.delete(
            `/projects/${projectId}/classes/${classId}`
        );
        return res.data;
    } catch (error) {
        console.error(`Error deleting class ${classId}:`, error);
        throw error;
    }
};

/**
 * Archive or unarchive a class
 * @param projectId - The ID of the project
 * @param classId - The ID of the class to archive/unarchive
 * @param archive - True to archive, false to unarchive
 * @returns Promise with success message
 */
export const archiveClass = async (
    projectId: number,
    classId: number,
    archive: boolean
): Promise<{ message: string }> => {
    try {
        const res = await api.patch(
            `/projects/${projectId}/classes/${classId}`,
            null,
            { params: { archive } }
        );
        return res.data;
    } catch (error) {
        console.error(`Error ${archive ? 'archiving' : 'unarchiving'} class ${classId}:`, error);
        throw error;
    }
};

// ============================================================================
// ORGANIZATION LEVEL API CALLS
// ============================================================================

/**
 * Get all classes for an organization
 * @param organizationId - The ID of the organization
 * @param projectIds - Optional array of project IDs to filter by
 * @param hideArchived - Flag to hide archived classes (default: true)
 * @returns Promise with array of ClassResponseDto
 */
export const getAllClassesOrg = async (
    organizationId: number,
    projectIds?: number[],
    hideArchived: boolean = true
): Promise<ClassResponseDto[]> => {
  try {
    const projectIdsQuery = projectIds?.map(id => `projectIds=${id}`).join('&') ?? '';
    const separator = projectIdsQuery ? '&' : '';
    const res = await api.get(
        `/organizations/${organizationId}/classes?${projectIdsQuery}${separator}hideArchived=${hideArchived}`
    );
    return res.data;
  } catch (error) {
    console.error("Error getting all classes for organization:", error);
    throw error;
  }
};

/**
 * Get a specific class at organization level
 * @param organizationId - The ID of the organization
 * @param classId - The ID of the class
 * @param hideArchived - Flag to hide archived classes (default: true)
 * @returns Promise with ClassResponseDto
 */
export const getClassOrg = async (
    organizationId: number,
    classId: number,
    hideArchived: boolean = true
): Promise<ClassResponseDto> => {
    try {
        const res = await api.get(
            `/organizations/${organizationId}/classes/${classId}`,
            { params: { hideArchived } }
        );
        return res.data;
    } catch (error) {
        console.error(`Error getting class ${classId} for organization:`, error);
        throw error;
    }
};

/**
 * Create a new class at organization level
 * @param organizationId - The ID of the organization
 * @param dto - The class creation request DTO
 * @returns Promise with ClassResponseDto
 */
export const createClassOrg = async (
    organizationId: number,
    dto: CreateClassRequestDto
): Promise<ClassResponseDto> => {
    try {
        const res = await api.post(
            `/organizations/${organizationId}/classes`,
            dto
        );
        return res.data;
    } catch (error) {
        console.error("Error creating class for organization:", error);
        throw error;
    }
};

/**
 * Bulk create classes at organization level
 * @param organizationId - The ID of the organization
 * @param classes - Array of class creation request DTOs
 * @returns Promise with array of ClassResponseDto
 */
export const bulkCreateClassesOrg = async (
    organizationId: number,
    classes: CreateClassRequestDto[]
): Promise<ClassResponseDto[]> => {
    try {
        const res = await api.post(
            `/organizations/${organizationId}/classes/bulk`,
            classes
        );
        return res.data;
    } catch (error) {
        console.error("Error bulk creating classes for organization:", error);
        throw error;
    }
};

/**
 * Update a class at organization level
 * @param organizationId - The ID of the organization
 * @param classId - The ID of the class to update
 * @param dto - The class update request DTO
 * @returns Promise with ClassResponseDto
 */
export const updateClassOrg = async (
    organizationId: number,
    classId: number,
    dto: UpdateClassRequestDto
): Promise<ClassResponseDto> => {
    try {
        const res = await api.put(
            `/organizations/${organizationId}/classes/${classId}`,
            dto
        );
        return res.data;
    } catch (error) {
        console.error(`Error updating class ${classId} for organization:`, error);
        throw error;
    }
};

/**
 * Delete a class at organization level
 * @param organizationId - The ID of the organization
 * @param classId - The ID of the class to delete
 * @returns Promise with success message
 */
export const deleteClassOrg = async (
    organizationId: number,
    classId: number
): Promise<{ message: string }> => {
    try {
        const res = await api.delete(
            `/organizations/${organizationId}/classes/${classId}`
        );
        return res.data;
    } catch (error) {
        console.error(`Error deleting class ${classId} for organization:`, error);
        throw error;
    }
};

/**
 * Archive or unarchive a class at organization level
 * @param organizationId - The ID of the organization
 * @param classId - The ID of the class to archive/unarchive
 * @param archive - True to archive, false to unarchive
 * @returns Promise with success message
 */
export const archiveClassOrg = async (
    organizationId: number,
    classId: number,
    archive: boolean
): Promise<{ message: string }> => {
    try {
        const res = await api.patch(
            `/organizations/${organizationId}/classes/${classId}`,
            null,
            { params: { archive } }
        );
        return res.data;
    } catch (error) {
        console.error(`Error ${archive ? 'archiving' : 'unarchiving'} class ${classId} for organization:`, error);
        throw error;
    }
};