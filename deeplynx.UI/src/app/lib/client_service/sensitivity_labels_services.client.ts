import {
    SensitivityLabelsDto,
    SensitivityLabelGrantResponseDto,
    SensitivityLabelPermissionResponseDto,
    GroupSensitivityLabelResponseDto,
} from "@/app/(home)/types/responseDTOs";
import api from "./api";
import { CreateSensitivityLabelDto, UpdateSensitivityLabelDto } from "@/app/(home)/types/requestDTOs";


// ============================================================================
// ORGANIZATION LEVEL API CALLS
// ============================================================================

/**
 * Get all Sensitivity Labels for an organization
 * @param organizationId - The ID of the organization
 * @param projectIds - Optional array of project IDs to filter by
 * @param hideArchived - Flag to hide archived tags (default: true)
 * @returns Promise with array of SensitivityLabelsDto
 */
export const getAllSensitivityLabelsOrg = async (
    organizationId: number,
    projectIds?: number[],
    hideArchived: boolean = true
): Promise<SensitivityLabelsDto[]> => {
    try {
        const res = await api.get(
            `/organizations/${organizationId}/labels`,
            { params: { projectIds, hideArchived }}
        );
        return res.data;
    } catch (error) {
        console.error("Error getting all Sensitivity Labels for Organization: ", error);
        throw error;
    }
}

/**
 * Create a new Sensitivity Label​ at organization level
 * @param organizationId - The ID of the organization
 * @param dto - The tag creation request DTO
 * @returns Promise with SensitivityLabelsDto
 */
export const createSensitivityLabelsOrg = async (
    organizationId: number,
    dto: CreateSensitivityLabelDto
): Promise<SensitivityLabelsDto> => {
    try {
        const res = await api.post(
            `/organizations/${organizationId}/labels`,
            dto
        );

        return res.data
    } catch (error) {
        console.error("Error creating Sensitivity Label", error);
        throw error;
    }
}

/**
 * Get Sensitivity Label by ID​ in Org
 * @param organizationId required - ID of the organization
 * @param labelId required - ID of the label
 * @param hideArchived optional - The default is true
 * @returns Promise with SensitivityLabelsDto
 */
export const getSensitivityLabelById = async (
    organizationId: number,
    labelId: number,
    hideArchived: boolean = true
): Promise<SensitivityLabelsDto> => {
    try {
        const res = await api.get(
            `/organizations/${organizationId}/labels/${labelId}`,
            { params: { hideArchived}}
        );

        return res.data;
    } catch (error) {
        console.error("Error getting the requested Sensitivity Label ", error);
        throw error;
    }
}

/**
 * Update a Sensitivity Label at organization level
 * @param organizationId - The ID of the organization
 * @param labelId - The ID of the sensitivity label to update
 * @param dto - The sensitivity label update request DTO
 * @returns Promise with SensitivityLabelsDto
 */
export const updateSensitivityLabelOrg = async (
    organizationId: number,
    labelId: number,
    dto: UpdateSensitivityLabelDto
): Promise<SensitivityLabelsDto> => {
    try {
        const res = await api.put(
            `/organizations/${organizationId}/labels/${labelId}`,
            dto
        );

        return res.data;
    } catch (error) {
        console.error(`Error updating Sensitivity Label ${labelId}:`, error);
        throw error;
    }
}

/**
 * Delete a Sensitivity Label at organization level
 * @param organizationId - The ID of the organization
 * @param labelId - The ID of the sensitivity label to delete
 * @returns Promise with success message
 */
export const deleteSensitivityLabelOrg = async (
    organizationId: number,
    labelId: number
): Promise<{ message: string }> => {
    try {
        const res = await api.delete(
            `/organizations/${organizationId}/labels/${labelId}`
        );

        return res.data;
    } catch (error) {
        console.error(`Error deleting Sensitivity Label ${labelId}:`, error);
        throw error;
    }
}

/**
 * Archive or unarchive a Sensitivity Label at organization level
 * @param organizationId - The ID of the organization
 * @param labelId - The ID of the sensitivity label to archive/unarchive
 * @param archive - True to archive, false to unarchive
 * @returns Promise with success message
 */
export const archiveSensitivityLabelOrg = async (
    organizationId: number,
    labelId: number,
    archive: boolean
): Promise<{ message: string }> => {
    try {
        const res = await api.patch(
            `/organizations/${organizationId}/labels/${labelId}`,
            null,
            { params: { archive } }
        );

        return res.data;
    } catch (error) {
        console.error(`Error ${archive ? 'archiving' : 'unarchiving'} Sensitivity Label ${labelId}:`, error);
        throw error;
    }
}

/**
 * Get all users with access to an organization-level Sensitivity Label
 * @param organizationId - The ID of the organization
 * @param labelId - The ID of the sensitivity label
 * @returns Promise with array of SensitivityLabelGrantResponseDto
 */
export const getUsersWithAccessToLabelOrg = async (
    organizationId: number,
    labelId: number
): Promise<SensitivityLabelGrantResponseDto[]> => {
    try {
        const res = await api.get(
            `/organizations/${organizationId}/labels/${labelId}/users`
        );
        return res.data;
    } catch (error) {
        console.error(`Error getting users with access to Sensitivity Label ${labelId}:`, error);
        throw error;
    }
}

/**
 * Grant a user access to an organization-level Sensitivity Label
 * @param organizationId - The ID of the organization
 * @param labelId - The ID of the sensitivity label
 * @param userId - The ID of the user to grant access to
 * @returns Promise with SensitivityLabelGrantResponseDto
 */
export const grantSensitivityLabelAccessOrg = async (
    organizationId: number,
    labelId: number,
    userId: number
): Promise<SensitivityLabelGrantResponseDto> => {
    try {
        const res = await api.post(
            `/organizations/${organizationId}/labels/${labelId}/users/${userId}`
        );
        return res.data;
    } catch (error) {
        console.error(`Error granting user ${userId} access to Sensitivity Label ${labelId}:`, error);
        throw error;
    }
}

/**
 * Revoke a user's access to an organization-level Sensitivity Label
 * @param organizationId - The ID of the organization
 * @param labelId - The ID of the sensitivity label
 * @param userId - The ID of the user to revoke access from
 * @returns Promise with a boolean success flag
 */
export const revokeSensitivityLabelAccessOrg = async (
    organizationId: number,
    labelId: number,
    userId: number
): Promise<boolean> => {
    try {
        const res = await api.delete(
            `/organizations/${organizationId}/labels/${labelId}/users/${userId}`
        );
        return res.data;
    } catch (error) {
        console.error(`Error revoking user ${userId} access to Sensitivity Label ${labelId}:`, error);
        throw error;
    }
}

/**
 * Get all groups granted access to an organization-level Sensitivity Label
 * @param organizationId - The ID of the organization
 * @param labelId - The ID of the sensitivity label
 * @returns Promise with array of GroupSensitivityLabelResponseDto
 */
export const getGroupsWithAccessToLabelOrg = async (
    organizationId: number,
    labelId: number
): Promise<GroupSensitivityLabelResponseDto[]> => {
    try {
        const res = await api.get(
            `/organizations/${organizationId}/labels/${labelId}/groups`
        );
        return res.data;
    } catch (error) {
        console.error(`Error getting groups with access to Sensitivity Label ${labelId}:`, error);
        throw error;
    }
}

/**
 * Grant a group access to an organization-level Sensitivity Label
 * @param organizationId - The ID of the organization
 * @param labelId - The ID of the sensitivity label
 * @param groupId - The ID of the group to grant access to
 * @returns Promise with GroupSensitivityLabelResponseDto
 */
export const grantSensitivityLabelAccessToGroupOrg = async (
    organizationId: number,
    labelId: number,
    groupId: number
): Promise<GroupSensitivityLabelResponseDto> => {
    try {
        const res = await api.post(
            `/organizations/${organizationId}/labels/${labelId}/groups/${groupId}`
        );
        return res.data;
    } catch (error) {
        console.error(`Error granting group ${groupId} access to Sensitivity Label ${labelId}:`, error);
        throw error;
    }
}

/**
 * Revoke a group's access to an organization-level Sensitivity Label
 * @param organizationId - The ID of the organization
 * @param labelId - The ID of the sensitivity label
 * @param groupId - The ID of the group to revoke access from
 * @returns Promise with a boolean success flag
 */
export const revokeSensitivityLabelAccessFromGroupOrg = async (
    organizationId: number,
    labelId: number,
    groupId: number
): Promise<boolean> => {
    try {
        const res = await api.delete(
            `/organizations/${organizationId}/labels/${labelId}/groups/${groupId}`
        );
        return res.data;
    } catch (error) {
        console.error(`Error revoking group ${groupId} access to Sensitivity Label ${labelId}:`, error);
        throw error;
    }
}

// ============================================================================
// PROJECT LEVEL API CALLS
// ============================================================================

/**
 * Get all Sensitivity Labels for a project
 * @param projectId - The ID of the project
 * @param hideArchived - Flag to hide archived labels (default: true)
 * @returns Promise with array of SensitivityLabelsDto
 */
export const getAllSensitivityLabelsProject = async (
    projectId: number,
    hideArchived: boolean = true
): Promise<SensitivityLabelsDto[]> => {
    try {
        const res = await api.get(
            `/projects/${projectId}/labels`,
            { params: { hideArchived } }
        );
        return res.data;
    } catch (error) {
        console.error("Error getting all Sensitivity Labels for Project: ", error);
        throw error;
    }
}

/**
 * Create a new Sensitivity Label at project level
 * @param projectId - The ID of the project
 * @param dto - The label creation request DTO
 * @returns Promise with SensitivityLabelsDto
 */
export const createSensitivityLabelProject = async (
    projectId: number,
    dto: CreateSensitivityLabelDto
): Promise<SensitivityLabelsDto> => {
    try {
        const res = await api.post(
            `/projects/${projectId}/labels`,
            dto
        );

        return res.data;
    } catch (error) {
        console.error("Error creating Sensitivity Label for Project", error);
        throw error;
    }
}

/**
 * Get Sensitivity Label by ID in project
 * @param projectId - ID of the project
 * @param labelId - ID of the label
 * @param hideArchived optional - The default is true
 * @returns Promise with SensitivityLabelsDto
 */
export const getSensitivityLabelByIdProject = async (
    projectId: number,
    labelId: number,
    hideArchived: boolean = true
): Promise<SensitivityLabelsDto> => {
    try {
        const res = await api.get(
            `/projects/${projectId}/labels/${labelId}`,
            { params: { hideArchived } }
        );

        return res.data;
    } catch (error) {
        console.error("Error getting the requested Sensitivity Label for Project", error);
        throw error;
    }
}

/**
 * Update a Sensitivity Label at project level
 * @param projectId - The ID of the project
 * @param labelId - The ID of the sensitivity label to update
 * @param dto - The sensitivity label update request DTO
 * @returns Promise with SensitivityLabelsDto
 */
export const updateSensitivityLabelProject = async (
    projectId: number,
    labelId: number,
    dto: UpdateSensitivityLabelDto
): Promise<SensitivityLabelsDto> => {
    try {
        const res = await api.put(
            `/projects/${projectId}/labels/${labelId}`,
            dto
        );

        return res.data;
    } catch (error) {
        console.error(`Error updating Sensitivity Label ${labelId} for Project:`, error);
        throw error;
    }
}

/**
 * Delete a Sensitivity Label at project level
 * @param projectId - The ID of the project
 * @param labelId - The ID of the sensitivity label to delete
 * @returns Promise with success message
 */
export const deleteSensitivityLabelProject = async (
    projectId: number,
    labelId: number
): Promise<{ message: string }> => {
    try {
        const res = await api.delete(
            `/projects/${projectId}/labels/${labelId}`
        );

        return res.data;
    } catch (error) {
        console.error(`Error deleting Sensitivity Label ${labelId} for Project:`, error);
        throw error;
    }
}

/**
 * Archive or unarchive a Sensitivity Label at project level
 * @param projectId - The ID of the project
 * @param labelId - The ID of the sensitivity label to archive/unarchive
 * @param archive - True to archive, false to unarchive
 * @returns Promise with success message
 */
export const archiveSensitivityLabelProject = async (
    projectId: number,
    labelId: number,
    archive: boolean
): Promise<{ message: string }> => {
    try {
        const res = await api.patch(
            `/projects/${projectId}/labels/${labelId}`,
            null,
            { params: { archive } }
        );

        return res.data;
    } catch (error) {
        console.error(`Error ${archive ? 'archiving' : 'unarchiving'} Sensitivity Label ${labelId} for Project:`, error);
        throw error;
    }
}

// ============================================================================
// PROJECT LEVEL — USER ACCESS API CALLS
// ============================================================================

/**
 * Get all users with access to a project-level Sensitivity Label
 * @param projectId - The ID of the project
 * @param labelId - The ID of the sensitivity label
 * @returns Promise with array of SensitivityLabelGrantResponseDto
 */
export const getUsersWithAccessToLabelProject = async (
    projectId: number,
    labelId: number
): Promise<SensitivityLabelGrantResponseDto[]> => {
    try {
        const res = await api.get(
            `/projects/${projectId}/labels/${labelId}/users`
        );
        return res.data;
    } catch (error) {
        console.error(`Error getting users with access to Sensitivity Label ${labelId}:`, error);
        throw error;
    }
}

/**
 * Grant a user access to a project-level Sensitivity Label
 * @param projectId - The ID of the project
 * @param labelId - The ID of the sensitivity label
 * @param userId - The ID of the user to grant access to
 * @returns Promise with SensitivityLabelGrantResponseDto
 */
export const grantSensitivityLabelAccessProject = async (
    projectId: number,
    labelId: number,
    userId: number
): Promise<SensitivityLabelGrantResponseDto> => {
    try {
        const res = await api.post(
            `/projects/${projectId}/labels/${labelId}/users/${userId}`
        );
        return res.data;
    } catch (error) {
        console.error(`Error granting user ${userId} access to Sensitivity Label ${labelId}:`, error);
        throw error;
    }
}

/**
 * Revoke a user's access to a project-level Sensitivity Label
 * @param projectId - The ID of the project
 * @param labelId - The ID of the sensitivity label
 * @param userId - The ID of the user to revoke access from
 * @returns Promise with a boolean success flag
 */
export const revokeSensitivityLabelAccessProject = async (
    projectId: number,
    labelId: number,
    userId: number
): Promise<boolean> => {
    try {
        const res = await api.delete(
            `/projects/${projectId}/labels/${labelId}/users/${userId}`
        );
        return res.data;
    } catch (error) {
        console.error(`Error revoking user ${userId} access to Sensitivity Label ${labelId}:`, error);
        throw error;
    }
}

// ============================================================================
// RECORD LEVEL API CALLS
// ============================================================================

/**
 * Attach a sensitivity label to a record
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param recordId - The ID of the record
 * @param sensitivityLabelId - The ID of the sensitivity label to attach
 * @returns Promise with success message
 */
export const attachSensitivityLabelToRecord = async (
    organizationId: number,
    projectId: number,
    recordId: number,
    sensitivityLabelId: number
): Promise<{ message: string }> => {
    try {
        const res = await api.post(
            `/organizations/${organizationId}/projects/${projectId}/records/${recordId}/sensitivity-labels`,
            null,
            { params: { sensitivityLabelId } }
        );

        return res.data;
    } catch (error) {
        console.error(`Error attaching sensitivity label ${sensitivityLabelId} to record ${recordId}:`, error);
        throw error;
    }
}

/**
 * Unattach a sensitivity label from a record
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param recordId - The ID of the record
 * @param sensitivityLabelId - The ID of the sensitivity label to unattach
 * @returns Promise with success message
 */
export const unattachSensitivityLabelFromRecord = async (
    organizationId: number,
    projectId: number,
    recordId: number,
    sensitivityLabelId: number
): Promise<{ message: string }> => {
    try {
        const res = await api.delete(
            `/organizations/${organizationId}/projects/${projectId}/records/${recordId}/sensitivity-labels`,
            { params: { sensitivityLabelId } }
        );

        return res.data;
    } catch (error) {
        console.error(`Error unattaching sensitivity label ${sensitivityLabelId} from record ${recordId}:`, error);
        throw error;
    }
}
