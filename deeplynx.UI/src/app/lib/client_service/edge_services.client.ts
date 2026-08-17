'use client';

import { CreateEdgeRequestDto, UpdateEdgeRequestDto } from "@/app/(home)/types/requestDTOs";
import { EdgeResponseDto, PaginatedResponse } from "@/app/(home)/types/responseDTOs";
import api from "./api";


/**
 * Get all edges for a project
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param dataSourceId - Optional data source ID to filter edges
 * @param hideArchived - Flag to hide archived edges (default: true)
 * @param pageNumber - Page number to fetch (default: 1)
 * @param pageSize - Page size; -1 fetches all classes (default: -1)
 * @returns Promise with array of EdgeResponseDto
 */
export const getAllEdges = async (
    organizationId: number,
    projectId: number,
    dataSourceId?: number,
    hideArchived: boolean = true,
    pageNumber: number = 1,
    pageSize: number = -1
): Promise<PaginatedResponse<EdgeResponseDto[]>> => {
    try {
        const res = await api.get(
            `/organizations/${organizationId}/projects/${projectId}/edges`,
            { params: { dataSourceId, hideArchived, pageNumber, pageSize } }
        );
        return res.data;
    } catch (error) {
        console.error("Error getting all edges:", error);
        throw error;
    }
};

/**
 * Get an edge by ID
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param edgeId - The ID of the edge
 * @param hideArchived - Flag to hide archived edges (default: true)
 * @returns Promise with EdgeResponseDto
 */
export const getEdgeById = async (
    organizationId: number,
    projectId: number,
    edgeId: number,
    hideArchived: boolean = true
): Promise<EdgeResponseDto> => {
    try {
        const res = await api.get(
            `/organizations/${organizationId}/projects/${projectId}/edges/${edgeId}`,
            { params: { hideArchived } }
        );
        return res.data;
    } catch (error) {
        console.error(`Error getting edge ${edgeId}:`, error);
        throw error;
    }
};

/**
 * Get an edge by origin and destination relationship
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param originId - The origin node ID
 * @param destinationId - The destination node ID
 * @param hideArchived - Flag to hide archived edges (default: true)
 * @returns Promise with EdgeResponseDto
 */
export const getEdgeByRelationship = async (
    organizationId: number,
    projectId: number,
    originId: number,
    destinationId: number,
    hideArchived: boolean = true
): Promise<EdgeResponseDto> => {
    try {
        const res = await api.get(
            `/organizations/${organizationId}/projects/${projectId}/edges/by-relationship`,
            { params: { originId, destinationId, hideArchived } }
        );
        return res.data;
    } catch (error) {
        console.error(`Error getting edge (origin: ${originId}, destination: ${destinationId}):`, error);
        throw error;
    }
};

/**
 * Create a new edge
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param dataSourceId - The ID of the data source (required)
 * @param edge - The edge creation request DTO
 * @returns Promise with EdgeResponseDto
 */
export const createEdge = async (
    organizationId: number,
    projectId: number,
    dataSourceId: number,
    edge: CreateEdgeRequestDto
): Promise<EdgeResponseDto> => {
    try {
        const res = await api.post(
            `/organizations/${organizationId}/projects/${projectId}/edges`,
            edge,
            { params: { dataSourceId } }
        );
        return res.data;
    } catch (error) {
        console.error("Error creating edge:", error);
        throw error;
    }
};

/**
 * Bulk create edges
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param dataSourceId - The ID of the data source (required)
 * @param edges - Array of edge creation request DTOs
 * @returns Promise with array of EdgeResponseDto
 */
export const bulkCreateEdges = async (
    organizationId: number,
    projectId: number,
    dataSourceId: number,
    edges: CreateEdgeRequestDto[]
): Promise<EdgeResponseDto[]> => {
    try {
        const res = await api.post(
            `/organizations/${organizationId}/projects/${projectId}/edges/bulk`,
            edges,
            { params: { dataSourceId } }
        );
        return res.data;
    } catch (error) {
        console.error("Error bulk creating edges:", error);
        throw error;
    }
};

/**
 * Update an edge by ID
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param edgeId - The ID of the edge to update
 * @param dto - The edge update request DTO
 * @returns Promise with EdgeResponseDto
 */
export const updateEdgeById = async (
    organizationId: number,
    projectId: number,
    edgeId: number,
    dto: UpdateEdgeRequestDto
): Promise<EdgeResponseDto> => {
    try {
        const res = await api.put(
            `/organizations/${organizationId}/projects/${projectId}/edges/${edgeId}`,
            dto
        );
        return res.data;
    } catch (error) {
        console.error(`Error updating edge ${edgeId}:`, error);
        throw error;
    }
};

/**
 * Update an edge by origin and destination relationship
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param originId - The origin node ID
 * @param destinationId - The destination node ID
 * @param dto - The edge update request DTO
 * @returns Promise with EdgeResponseDto
 */
export const updateEdgeByRelationship = async (
    organizationId: number,
    projectId: number,
    originId: number,
    destinationId: number,
    dto: UpdateEdgeRequestDto
): Promise<EdgeResponseDto> => {
    try {
        const res = await api.put(
            `/organizations/${organizationId}/projects/${projectId}/edges/by-relationship`,
            dto,
            { params: { originId, destinationId } }
        );
        return res.data;
    } catch (error) {
        console.error(`Error updating edge (origin: ${originId}, destination: ${destinationId}):`, error);
        throw error;
    }
};

/**
 * Delete an edge by ID
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param edgeId - The ID of the edge to delete
 * @returns Promise with success message
 */
export const deleteEdgeById = async (
    organizationId: number,
    projectId: number,
    edgeId: number
): Promise<{ message: string }> => {
    try {
        const res = await api.delete(
            `/organizations/${organizationId}/projects/${projectId}/edges/${edgeId}`
        );
        return res.data;
    } catch (error) {
        console.error(`Error deleting edge ${edgeId}:`, error);
        throw error;
    }
};

/**
 * Delete an edge by origin and destination relationship
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param originId - The origin node ID
 * @param destinationId - The destination node ID
 * @returns Promise with success message
 */
export const deleteEdgeByRelationship = async (
    organizationId: number,
    projectId: number,
    originId: number,
    destinationId: number
): Promise<{ message: string }> => {
    try {
        const res = await api.delete(
            `/organizations/${organizationId}/projects/${projectId}/edges/by-relationship`,
            { params: { originId, destinationId } }
        );
        return res.data;
    } catch (error) {
        console.error(`Error deleting edge (origin: ${originId}, destination: ${destinationId}):`, error);
        throw error;
    }
};

/**
 * Archive or unarchive an edge by ID
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param edgeId - The ID of the edge to archive/unarchive
 * @param archive - True to archive, false to unarchive
 * @returns Promise with success message
 */
export const archiveEdgeById = async (
    organizationId: number,
    projectId: number,
    edgeId: number,
    archive: boolean
): Promise<{ message: string }> => {
    try {
        const res = await api.patch(
            `/organizations/${organizationId}/projects/${projectId}/edges/${edgeId}`,
            null,
            { params: { archive } }
        );
        return res.data;
    } catch (error) {
        console.error(`Error ${archive ? 'archiving' : 'unarchiving'} edge ${edgeId}:`, error);
        throw error;
    }
};

/**
 * Archive or unarchive an edge by origin and destination relationship
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param originId - The origin node ID
 * @param destinationId - The destination node ID
 * @param archive - True to archive, false to unarchive
 * @returns Promise with success message
 */
export const archiveEdgeByRelationship = async (
    organizationId: number,
    projectId: number,
    originId: number,
    destinationId: number,
    archive: boolean
): Promise<{ message: string }> => {
    try {
        const res = await api.patch(
            `/organizations/${organizationId}/projects/${projectId}/edges/by-relationship`,
            null,
            { params: { originId, destinationId, archive } }
        );
        return res.data;
    } catch (error) {
        console.error(`Error ${archive ? 'archiving' : 'unarchiving'} edge (origin: ${originId}, destination: ${destinationId}):`, error);
        throw error;
    }
};
