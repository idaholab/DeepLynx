'use client';

import { CreateOauthApplicationRequestDto, UpdateOauthApplicationRequestDto } from "@/app/(home)/types/requestDTOs";
import { OauthApplicationResponseDto, OauthApplicationSecureResponseDto, PaginatedResponse } from "@/app/(home)/types/responseDTOs";
import api from "./api";

/**
 * Get all OAuth applications
 * @param pageNumber - Page number to fetch (default: 1)
 * @param pageSize - Page size; -1 fetches all projects (default: -1)
 * @param hideArchived - Flag to hide archived applications (default: true)
 * @returns Promise with array of OauthApplicationResponseDto
 */
export const getAllOauthApplications = async (
    pageNumber: number = 1,
    pageSize: number = -1,
    hideArchived: boolean = true
): Promise<PaginatedResponse<OauthApplicationResponseDto>> => {
    try {
        const res = await api.get<PaginatedResponse<OauthApplicationResponseDto>>(
            `/oauth/applications`,
            { params: { pageNumber, pageSize, hideArchived } }
        );
        return res.data;
    } catch (error) {
        console.error("Error fetching OAuth applications:", error);
        throw error;
    }
};

/**
 * Get a specific OAuth application by ID
 * @param applicationId - The ID of the OAuth application
 * @param hideArchived - Flag to hide archived applications (default: true)
 * @returns Promise with OauthApplicationResponseDto
 */
export const getOauthApplication = async (
    applicationId: number,
    hideArchived: boolean = true
): Promise<OauthApplicationResponseDto> => {
    try {
        const res = await api.get<OauthApplicationResponseDto>(
            `/oauth/applications/${applicationId}`,
            { params: { hideArchived } }
        );
        return res.data;
    } catch (error) {
        console.error(`Error fetching OAuth application ${applicationId}:`, error);
        throw error;
    }
};

/**
 * Create a new OAuth application
 * @param dto - The OAuth application creation request DTO
 * @returns Promise with OauthApplicationSecureResponseDto (includes client secret)
 */
export const createOauthApplication = async (
    dto: CreateOauthApplicationRequestDto
): Promise<OauthApplicationSecureResponseDto> => {
    try {
        const res = await api.post<OauthApplicationSecureResponseDto>(
            `/oauth/applications`,
            dto,
            { headers: { "Content-Type": "application/json" } }
        );
        return res.data;
    } catch (error) {
        console.error("Error creating OAuth application:", error);
        throw error;
    }
};

/**
 * Update an OAuth application
 * @param applicationId - The ID of the OAuth application to update
 * @param dto - The OAuth application update request DTO
 * @returns Promise with OauthApplicationResponseDto
 */
export const updateOauthApplication = async (
    applicationId: number,
    dto: UpdateOauthApplicationRequestDto
): Promise<OauthApplicationResponseDto> => {
    try {
        const res = await api.put<OauthApplicationResponseDto>(
            `/oauth/applications/${applicationId}`,
            dto
        );
        return res.data;
    } catch (error) {
        console.error(`Error updating OAuth application ${applicationId}:`, error);
        throw error;
    }
};

/**
 * Delete an OAuth application
 * @param applicationId - The ID of the OAuth application to delete
 * @returns Promise with success message
 */
export const deleteOauthApplication = async (
    applicationId: number
): Promise<{ message: string }> => {
    try {
        const res = await api.delete(
            `/oauth/applications/${applicationId}`
        );
        return res.data;
    } catch (error) {
        console.error(`Error deleting OAuth application ${applicationId}:`, error);
        throw error;
    }
};

/**
 * Archive or unarchive an OAuth application
 * @param applicationId - The ID of the OAuth application to archive/unarchive
 * @param archive - True to archive, false to unarchive
 * @returns Promise with success message
 */
export const archiveOauthApplication = async (
    applicationId: number,
    archive: boolean = true
): Promise<{ message: string }> => {
    try {
        const res = await api.patch(
            `/oauth/applications/${applicationId}`,
            null,
            { params: { archive } }
        );
        return res.data;
    } catch (error) {
        console.error(`Error ${archive ? 'archiving' : 'unarchiving'} OAuth application ${applicationId}:`, error);
        throw error;
    }
};