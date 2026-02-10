import { SensitivityLabelsDto } from "@/app/(home)/types/responseDTOs";
import api from "./api";
import { CreateSensitivityLabelDto } from "@/app/(home)/types/requestDTOs";


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
): Promise<SensitivityLabelsDto> => {
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

