import { CreateObjectStorageRequestDto, UpdateObjectStorageRequestDto } from "@/app/(home)/types/requestDTOs";
import { ObjectStorageResponseDto } from "@/app/(home)/types/responseDTOs";
import api from "./api";

// ============================================================================
// ORGANIZATION-SCOPED OBJECT STORAGE ENDPOINTS
// ============================================================================

/**
 * Get all object storages for an organization
 * @param organizationId - The ID of the organization
 * @param hideArchived - Flag to hide archived object storages (default: true)
 * @returns Promise with array of ObjectStorageResponseDto
 */
export async function getAllOrganizationObjectStorages(
    organizationId: number,
    hideArchived: boolean = true
): Promise<ObjectStorageResponseDto[]> {
    try {
        const res = await api.get<ObjectStorageResponseDto[]>(
            `/organizations/${organizationId}/storages`,
            { params: { hideArchived } }
        );
        return res.data;
    } catch (error) {
        console.error("Error fetching organization object storages:", error);
        throw error;
    }
}

/**
 * Get a specific object storage by ID for an organization
 * @param organizationId - The ID of the organization
 * @param objectStorageId - The ID of the object storage
 * @param hideArchived - Flag to hide archived object storages (default: true)
 * @returns Promise with ObjectStorageResponseDto
 */
export async function getOrganizationObjectStorage(
    organizationId: number,
    objectStorageId: number,
    hideArchived: boolean = true
): Promise<ObjectStorageResponseDto> {
    try {
        const res = await api.get<ObjectStorageResponseDto>(
            `/organizations/${organizationId}/storages/${objectStorageId}`,
            { params: { hideArchived } }
        );
        return res.data;
    } catch (error) {
        console.error(`Error fetching organization object storage ${objectStorageId}:`, error);
        throw error;
    }
}

/**
 * Get the default object storage for an organization
 * @param organizationId - The ID of the organization
 * @returns Promise with ObjectStorageResponseDto
 */
export async function getDefaultOrganizationObjectStorage(
    organizationId: number
): Promise<ObjectStorageResponseDto> {
    try {
        const res = await api.get<ObjectStorageResponseDto>(
            `/organizations/${organizationId}/storages/default`
        );
        return res.data;
    } catch (error) {
        console.error("Error fetching default organization object storage:", error);
        throw error;
    }
}

/**
 * Create a new object storage for an organization
 * @param organizationId - The ID of the organization
 * @param dto - The object storage creation request DTO
 * @returns Promise with ObjectStorageResponseDto
 */
export async function createOrganizationObjectStorage(
    organizationId: number,
    dto: CreateObjectStorageRequestDto,
    makeDefault: boolean = false
): Promise<ObjectStorageResponseDto> {
    try {
        const res = await api.post<ObjectStorageResponseDto>(
            `/organizations/${organizationId}/storages`,
            dto,
            { params: { makeDefault } }
        );
        return res.data;
    } catch (error) {
        console.error("Error creating organization object storage:", error);
        throw error;
    }
}

/**
 * Update an organization object storage
 * @param organizationId - The ID of the organization
 * @param objectStorageId - The ID of the object storage to update
 * @param dto - The object storage update request DTO
 * @returns Promise with ObjectStorageResponseDto
 */
export async function updateOrganizationObjectStorage(
    organizationId: number,
    objectStorageId: number,
    dto: UpdateObjectStorageRequestDto
): Promise<ObjectStorageResponseDto> {
    try {
        const res = await api.put<ObjectStorageResponseDto>(
            `/organizations/${organizationId}/storages/${objectStorageId}`,
            dto
        );
        return res.data;
    } catch (error) {
        console.error(`Error updating organization object storage ${objectStorageId}:`, error);
        throw error;
    }
}

/**
 * Delete an organization object storage
 * @param organizationId - The ID of the organization
 * @param objectStorageId - The ID of the object storage to delete
 * @returns Promise with success message
 */
export async function deleteOrganizationObjectStorage(
    organizationId: number,
    objectStorageId: number
): Promise<{ message: string }> {
    try {
        const res = await api.delete<{ message: string }>(
            `/organizations/${organizationId}/storages/${objectStorageId}`
        );
        return res.data;
    } catch (error) {
        console.error(`Error deleting organization object storage ${objectStorageId}:`, error);
        throw error;
    }
}

/**
 * Archive or unarchive an organization object storage
 * @param organizationId - The ID of the organization
 * @param objectStorageId - The ID of the object storage to archive/unarchive
 * @param archive - True to archive, false to unarchive
 * @returns Promise with success message
 */
export async function archiveOrganizationObjectStorage(
    organizationId: number,
    objectStorageId: number,
    archive: boolean
): Promise<{ message: string }> {
    try {
        const res = await api.patch<{ message: string }>(
            `/organizations/${organizationId}/storages/${objectStorageId}`,
            null,
            { params: { archive } }
        );
        return res.data;
    } catch (error) {
        console.error(`Error ${archive ? 'archiving' : 'unarchiving'} organization object storage ${objectStorageId}:`, error);
        throw error;
    }
}

/**
 * Set an object storage as the default for the organization
 * @param organizationId - The ID of the organization
 * @param objectStorageId - The ID of the object storage to set as default
 * @returns Promise with ObjectStorageResponseDto
 */
export async function setDefaultOrganizationObjectStorage(
    organizationId: number,
    objectStorageId: number
): Promise<ObjectStorageResponseDto> {
    try {
        const res = await api.patch<ObjectStorageResponseDto>(
            `/organizations/${organizationId}/storages/${objectStorageId}/default`
        );
        return res.data;
    } catch (error) {
        console.error(`Error setting default organization object storage ${objectStorageId}:`, error);
        throw error;
    }
}

// ============================================================================
// PROJECT-SCOPED OBJECT STORAGE ENDPOINTS
// ============================================================================

/**
 * Get all object storages for a project
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param hideArchived - Flag to hide archived object storages (default: true)
 * @returns Promise with array of ObjectStorageResponseDto
 */
export async function getAllProjectObjectStorages(
    organizationId: number,
    projectId: number,
    hideArchived: boolean = true
): Promise<ObjectStorageResponseDto[]> {
    try {
        const res = await api.get<ObjectStorageResponseDto[]>(
            `/organizations/${organizationId}/projects/${projectId}/storages`,
            { params: { hideArchived } }
        );
        return res.data;
    } catch (error) {
        console.error("Error fetching project object storages:", error);
        throw error;
    }
}

/**
 * Get a specific object storage by ID for a project
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param objectStorageId - The ID of the object storage
 * @param hideArchived - Flag to hide archived object storages (default: true)
 * @returns Promise with ObjectStorageResponseDto
 */
export async function getProjectObjectStorage(
    organizationId: number,
    projectId: number,
    objectStorageId: number,
    hideArchived: boolean = true
): Promise<ObjectStorageResponseDto> {
    try {
        const res = await api.get<ObjectStorageResponseDto>(
            `/organizations/${organizationId}/projects/${projectId}/storages/${objectStorageId}`,
            { params: { hideArchived } }
        );
        return res.data;
    } catch (error) {
        console.error(`Error fetching project object storage ${objectStorageId}:`, error);
        throw error;
    }
}

/**
 * Get the default object storage for a project
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @returns Promise with ObjectStorageResponseDto
 */
export async function getDefaultProjectObjectStorage(
    organizationId: number,
    projectId: number
): Promise<ObjectStorageResponseDto> {
    try {
        const res = await api.get<ObjectStorageResponseDto>(
            `/organizations/${organizationId}/projects/${projectId}/storages/default`
        );
        return res.data;
    } catch (error) {
        console.error("Error fetching default project object storage:", error);
        throw error;
    }
}

/**
 * Create a new object storage for a project
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param dto - The object storage creation request DTO
 * @param makeDefault - Flag to make the created storage default (default: false)
 * @returns Promise with ObjectStorageResponseDto
 */
export async function createProjectObjectStorage(
    organizationId: number,
    projectId: number,
    dto: CreateObjectStorageRequestDto,
    makeDefault: boolean = false
): Promise<ObjectStorageResponseDto> {
    try {
        const res = await api.post<ObjectStorageResponseDto>(
            `/organizations/${organizationId}/projects/${projectId}/storages`,
            dto,
            { params: { makeDefault } }
        );
        return res.data;
    } catch (error) {
        console.error("Error creating project object storage:", error);
        throw error;
    }
}

/**
 * Update a project object storage
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param objectStorageId - The ID of the object storage to update
 * @param dto - The object storage update request DTO
 * @returns Promise with ObjectStorageResponseDto
 */
export async function updateProjectObjectStorage(
    organizationId: number,
    projectId: number,
    objectStorageId: number,
    dto: UpdateObjectStorageRequestDto
): Promise<ObjectStorageResponseDto> {
    try {
        const res = await api.put<ObjectStorageResponseDto>(
            `/organizations/${organizationId}/projects/${projectId}/storages/${objectStorageId}`,
            dto
        );
        return res.data;
    } catch (error) {
        console.error(`Error updating project object storage ${objectStorageId}:`, error);
        throw error;
    }
}

/**
 * Delete a project object storage
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param objectStorageId - The ID of the object storage to delete
 * @returns Promise with success message
 */
export async function deleteProjectObjectStorage(
    organizationId: number,
    projectId: number,
    objectStorageId: number
): Promise<{ message: string }> {
    try {
        const res = await api.delete<{ message: string }>(
            `/organizations/${organizationId}/projects/${projectId}/storages/${objectStorageId}`
        );
        return res.data;
    } catch (error) {
        console.error(`Error deleting project object storage ${objectStorageId}:`, error);
        throw error;
    }
}

/**
 * Archive or unarchive a project object storage
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param objectStorageId - The ID of the object storage to archive/unarchive
 * @param archive - True to archive, false to unarchive
 * @returns Promise with success message
 */
export async function archiveProjectObjectStorage(
    organizationId: number,
    projectId: number,
    objectStorageId: number,
    archive: boolean
): Promise<{ message: string }> {
    try {
        const res = await api.patch<{ message: string }>(
            `/organizations/${organizationId}/projects/${projectId}/storages/${objectStorageId}`,
            null,
            { params: { archive } }
        );
        return res.data;
    } catch (error) {
        console.error(`Error ${archive ? 'archiving' : 'unarchiving'} project object storage ${objectStorageId}:`, error);
        throw error;
    }
}

/**
 * Set an object storage as the default for the project
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param objectStorageId - The ID of the object storage to set as default
 * @returns Promise with ObjectStorageResponseDto
 */
export async function setDefaultProjectObjectStorage(
    organizationId: number,
    projectId: number,
    objectStorageId: number
): Promise<ObjectStorageResponseDto> {
    try {
        const res = await api.patch<ObjectStorageResponseDto>(
            `/organizations/${organizationId}/projects/${projectId}/storages/${objectStorageId}/default`
        );
        return res.data;
    } catch (error) {
        console.error(`Error setting default project object storage ${objectStorageId}:`, error);
        throw error;
    }
}

export async function createProjectAzureContainer(
    organizationId: number,
    projectId: number,
    existingContainer: boolean,
    storageType: string = "azure_object",
    containerName?: string | null,
): Promise<ObjectStorageResponseDto> {
    try {
        const res = await api.post<ObjectStorageResponseDto>(
            `/organizations/${organizationId}/projects/${projectId}/storages/container`,
            null,
            {
                params: {
                    storageType,
                    existingContainer,
                    containerName,
                },
            },
        );
        return res.data;
    } catch (error) {
        console.error(`Error creating ${storageType} container for project ${projectId}:`, error);
        throw error;
    }
}