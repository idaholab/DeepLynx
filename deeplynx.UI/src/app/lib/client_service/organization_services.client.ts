import {
    CreateOrganizationRequestDto,
    InviteUserToOrganizationRequestDto,
    UpdateOrganizationRequestDto
} from "@/app/(home)/types/requestDTOs";
import { OrganizationResponseDto } from "@/app/(home)/types/responseDTOs";
import api from "./api";
import { UploadLogoRequest, UploadLogoResponse, RemoveLogoRequest, RemoveLogoResponse, FetchOrganizationLogoResponse } from "@/app/(home)/types/org_setting_types";

/* -------------------------------------------------------------------------- */
/*                         Organization CRUD Operations                        */
/* -------------------------------------------------------------------------- */

/**
 * Get all organizations
 * @param hideArchived - Flag to hide archived organizations (default: true)
 * @returns Promise with array of OrganizationResponseDto
 */
export const getAllOrganizations = async (
    hideArchived: boolean = true
): Promise<OrganizationResponseDto[]> => {
    try {
        const res = await api.get<OrganizationResponseDto[]>(
            `/organizations`,
            { params: { hideArchived } }
        );
        return res.data;
    } catch (error) {
        console.error("Error fetching organizations:", error);
        throw error;
    }
};

/**
 * Get all organizations for the current user
 * @param hideArchived - Flag to hide archived organizations (default: true)
 * @returns Promise with array of OrganizationResponseDto
 */
export const getAllOrganizationsForUser = async (
    hideArchived: boolean = true
): Promise<OrganizationResponseDto[]> => {
    try {
        const res = await api.get<OrganizationResponseDto[]>(
            `/organizations/user`,
            { params: { hideArchived } }
        );
        return res.data;
    } catch (error) {
        console.error("Error fetching organizations for user:", error);
        throw error;
    }
};

/**
 * Get a specific organization by ID
 * @param organizationId - The ID of the organization
 * @param hideArchived - Flag to hide archived organizations (default: true)
 * @returns Promise with OrganizationResponseDto
 */
export const getOrganization = async (
    organizationId: number,
    hideArchived: boolean = true
): Promise<OrganizationResponseDto> => {
    try {
        const res = await api.get<OrganizationResponseDto>(
            `/organizations/${organizationId}`,
            { params: { hideArchived } }
        );
        return res.data;
    } catch (error) {
        console.error(`Error fetching organization ${organizationId}:`, error);
        throw error;
    }
};

/**
 * Create a new organization
 * @param dto - The organization creation request DTO
 * @returns Promise with OrganizationResponseDto
 */
export const createOrganization = async (
    dto: CreateOrganizationRequestDto
): Promise<OrganizationResponseDto> => {
    try {
        const res = await api.post<OrganizationResponseDto>(
            `/organizations`,
            dto
        );
        return res.data;
    } catch (error) {
        console.error("Error creating organization:", error);
        throw error;
    }
};

/**
 * Update an organization
 * @param organizationId - The ID of the organization to update
 * @param dto - The organization update request DTO
 * @returns Promise with OrganizationResponseDto
 */
export const updateOrganization = async (
    organizationId: number,
    dto: UpdateOrganizationRequestDto
): Promise<OrganizationResponseDto> => {
    try {
        const res = await api.put<OrganizationResponseDto>(
            `/organizations/${organizationId}`,
            dto
        );
        return res.data;
    } catch (error) {
        console.error(`Error updating organization ${organizationId}:`, error);
        throw error;
    }
};

/**
 * Delete an organization
 * @param organizationId - The ID of the organization to delete
 * @returns Promise with success message
 */
export const deleteOrganization = async (
    organizationId: number
): Promise<{ message: string }> => {
    try {
        const res = await api.delete(
            `/organizations/${organizationId}`
        );
        return res.data;
    } catch (error) {
        console.error(`Error deleting organization ${organizationId}:`, error);
        throw error;
    }
};

/**
 * Archive or unarchive an organization
 * @param organizationId - The ID of the organization to archive/unarchive
 * @param archive - True to archive, false to unarchive
 * @returns Promise with success message
 */
export const archiveOrganization = async (
    organizationId: number,
    archive: boolean = true
): Promise<{ message: string }> => {
    try {
        const res = await api.patch(
            `/organizations/${organizationId}`,
            null,
            { params: { archive } }
        );
        return res.data;
    } catch (error) {
        console.error(`Error ${archive ? 'archiving' : 'unarchiving'} organization ${organizationId}:`, error);
        throw error;
    }
};

/**
 * Get organization storage size in bytes
 * @param organizationId - The ID of the organization
 * @returns Promise with the organization storage size in bytes
 */
export const getOrganizationStorageSize = async (
    organizationId: number
): Promise<number> => {
    try {
        const res = await api.get<number>(
            `/organization/${organizationId}/metrics/storage/size`
        );
        return res.data;
    } catch (error) {
        console.error(
            `Error fetching storage size for organization ${organizationId}:`,
            error
        );
        throw error;
    }
};

/**
 * Get organization data source count
 * @param organizationId - The ID of the organization
 * @returns Promise with the organization data source count
 */
export const getOrganizationDataSourceCount = async (
    organizationId: number
): Promise<number> => {
    try {
        const res = await api.get<number>(
            `/organization/${organizationId}/metrics/count`,
            { params: { hideArchived: true } }
        );
        return res.data;
    } catch (error) {
        console.error(
            `Error fetching data source count for organization ${organizationId}:`,
            error
        );
        throw error;
    }
};

/**
 * Get organization data modality count
 * @param organizationId - The ID of the organization
 * @returns Promise with the organization data modality count
 */
export const getOrganizationDataModalityCount = async (
    organizationId: number
): Promise<number> => {
    try {
        const res = await api.get<number>(
            `/organization/${organizationId}/metrics/modalities/count`
        );
        return res.data;
    } catch (error) {
        console.error(
            `Error fetching data modality count for organization ${organizationId}:`,
            error
        );
        throw error;
    }
};

/**
 * Get organization record count
 * @param organizationId - The ID of the organization
 * @returns Promise with the organization record count
 */
export const getOrganizationRecordCount = async (
    organizationId: number
): Promise<number> => {
    try {
        const res = await api.get<number>(
            `/organization/${organizationId}/metrics/records/count`,
            { params: { hideArchived: true } }
        );
        return res.data;
    } catch (error) {
        console.error(
            `Error fetching record count for organization ${organizationId}:`,
            error
        );
        throw error;
    }
};

/**
 * Get organization file count
 * @param organizationId - The ID of the organization
 * @returns Promise with the organization file count
 */
export const getOrganizationFileCount = async (
    organizationId: number
): Promise<number> => {
    try {
        const res = await api.get<number>(
            `/organization/${organizationId}/metrics/files/count`,
            { params: { hideArchived: true } }
        );
        return res.data;
    } catch (error) {
        console.error(
            `Error fetching file count for organization ${organizationId}:`,
            error
        );
        throw error;
    }
};

/* -------------------------------------------------------------------------- */
/*                      Organization User Management                          */
/* -------------------------------------------------------------------------- */

/**
 * Add a user to an organization
 * @param organizationId - The ID of the organization
 * @param userId - The ID of the user to add
 * @param isAdmin - Whether to add the user as an admin (default: false)
 * @returns Promise with success message
 */
export const addUserToOrganization = async (
    organizationId: number,
    userId: number,
    isAdmin: boolean = false
): Promise<{ message: string }> => {
    try {
        const res = await api.post(
            `/organizations/${organizationId}/user`,
            null,
            { params: { userId, isAdmin } }
        );
        return res.data;
    } catch (error) {
        console.error(`Error adding user ${userId} to organization ${organizationId}:`, error);
        throw error;
    }
};

/**
 * Set admin status for an organization user
 * @param organizationId - The ID of the organization
 * @param userId - The ID of the user
 * @param isAdmin - The admin status to set
 * @returns Promise with success message
 */
export const setOrganizationAdminStatus = async (
    organizationId: number,
    userId: number,
    isAdmin: boolean
): Promise<{ message: string }> => {
    try {
        const res = await api.put(
            `/organizations/${organizationId}/admin`,
            null,
            { params: { userId, isAdmin } }
        );
        return res.data;
    } catch (error) {
        console.error(`Error setting admin status for user ${userId} in organization ${organizationId}:`, error);
        throw error;
    }
};

/**
 * Remove a user from an organization
 * @param organizationId - The ID of the organization
 * @param userId - The ID of the user to remove
 * @returns Promise with success message
 */
export const removeUserFromOrganization = async (
    organizationId: number,
    userId: number
): Promise<{ message: string }> => {
    try {
        const res = await api.delete(
            `/organizations/${organizationId}/user`,
            { params: { userId } }
        );
        return res.data;
    } catch (error) {
        console.error(`Error removing user ${userId} from organization ${organizationId}:`, error);
        throw error;
    }
};

/**
 * Upload organization logo
 */
export const uploadOrganizationLogo = async (
    request: UploadLogoRequest
): Promise<UploadLogoResponse> => {
    try {
        const formData = new FormData();
        formData.append("file", request.file);

        const res = await api.post(
            `/organizations/${request.organizationId}/logo`,
            formData
        );
        return res.data;
    } catch (error) {
        console.error(
            `Failed to upload organization logo for organization ID ${request.organizationId}: ${error}`
        );
        throw new Error(`Failed to upload organization logo: ${error}`);
    }
};

/**
 * Fetch organization logo image as a Blob URL
 * Returns an object containing the blob URL and filename (if known)
 */
export const fetchOrganizationLogo = async (
    organizationId: number,
): Promise<FetchOrganizationLogoResponse> => {
    try {
        const res = await api.get<Blob>(
            `/organizations/${organizationId}/logo/image`,
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
            `Failed to fetch organization logo for organization ID ${organizationId}:`,
            error
        );
        return { blobUrl: null };
    }
};

/**
 * Remove organization logo
 * Deletes the logo file from the logos folder and updates the active_logo.txt file.
 */
export const removeOrganizationLogo = async (
    request: RemoveLogoRequest
): Promise<RemoveLogoResponse> => {
    try {
        const response = await api.delete<RemoveLogoResponse>(`/organizations/${request.organizationId}/logo/delete`);
        return response.data;
    } catch (error: any) {
        throw new Error(error.response?.data?.message || "Failed to remove logo");
    }
};

/**
 * Get organization logo URL and check if it exists
 * Returns the logo URL if the file exists, null otherwise
 */
export const getOrganizationLogoUrl = async (
    organizationId: number
): Promise<string | null> => {
    try {
        const response = await fetch(`/api/organization/${organizationId}/logo`, {
            method: "GET",
        });

        if (!response.ok) {
            return null;
        }

        const data = await response.json();
        return data.exists ? data.logoUrl : null;
    } catch (error) {
        console.error("Error getting logo URL:", error);
        return null;
    }
};

/**
 * Check if organization logo exists
 * Returns true if a logo file exists for the organization
 */
export const checkLogoExists = async (
    organizationId: number
): Promise<boolean> => {
    try {
        const logoUrl = await getOrganizationLogoUrl(organizationId);
        return logoUrl !== null;
    } catch (error) {
        console.error("Error checking logo existence:", error);
        return false;
    }
}

/*
* Invite a user to an organization
* @param organizationId - The ID of the organization
* @param inviteData - The invite request data (userEmail, optional userName)
* @returns Promise<void>
*/
export const inviteUserToOrganization = async (
    organizationId: number,
    inviteData: InviteUserToOrganizationRequestDto
): Promise<void> => {
    try {
        await api.post(
            `/organizations/${organizationId}/invite`,
            null,
            {
                params: {
                    userEmail: inviteData.userEmail
                }
            }
        );
    } catch (error) {
        console.error(`Error inviting user to organization ${organizationId}:`, error);
        throw error;
    }
};