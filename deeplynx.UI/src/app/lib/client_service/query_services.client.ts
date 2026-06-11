// src/app/lib/query_services.client.ts
"use client";

import { CustomQueryRequestDto } from "@/app/(home)/types/requestDTOs";
import { QueryRecordViewResponseDto } from "@/app/(home)/types/responseDTOs";
import api from "./api";


/**
 * Full text search for records
 * @param organizationId - The ID of the organization
 * @param userQuery - String phrase entered by user
 * @param projectIds - Array of project IDs to search across
 * @param hideArchived - Flag to hide archived records (default: true)
 * @returns Promise with array of QueryRecordViewResponseDto
 */
export async function fullTextSearch(
    organizationId: number,
    userQuery: string,
    projectIds: number[],
    hideArchived: boolean = true,
): Promise<QueryRecordViewResponseDto[]> {
    try {
        const projectIdsQuery = projectIds.map(id => `projectIds=${id}`).join('&');
        const res = await api.get(
            `/organizations/${organizationId}/query/records?userQuery=${encodeURIComponent(userQuery)}&${projectIdsQuery}&hideArchived=${hideArchived}`
        );
        return res.data;
    } catch (error) {
        console.error("Error performing full text search:", error);
        throw error;
    }
}

/**
 * Build a custom query for records
 * @param organizationId - The ID of the organization
 * @param queryObj - Array of custom query request DTOs
 * @param projectIds - Array of project IDs to search across
 * @param textSearch - Optional full text search phrase
 * @returns Promise with array of QueryRecordViewResponseDto
 */
export async function queryBuilder(
    organizationId: number,
    queryObj: CustomQueryRequestDto[],
    projectIds: number[],
    textSearch?: string | null
): Promise<QueryRecordViewResponseDto[]> {
    try {
        // Building json string format from key/value input
        for (const obj of queryObj) {
            if (obj.jsonKey && obj.jsonValue) {
                const json = `{"${obj.jsonKey}": "${obj.jsonValue}"}`;
                obj.json = json;
            }
        }

        const projectIdsQuery = projectIds.map(id => `projectIds=${id}`).join('&');
        const textSearchParam = textSearch ? `&textSearch=${encodeURIComponent(textSearch)}` : '';

        const res = await api.post(
            `/organizations/${organizationId}/query/records/advanced?${projectIdsQuery}${textSearchParam}`,
            queryObj,
            { headers: { "Content-Type": "application/json" } }
        );
        return res.data;
    } catch (error) {
        console.error("Error building query:", error);
        throw error;
    }
}

/**
 * Get recently added records
 * @param organizationId - The ID of the organization
 * @param projectIds - Array of project IDs
 * @returns Promise with array of QueryRecordViewResponseDto sorted by most recent
 */
export async function getRecentlyAddedRecords(
    organizationId: number,
    projectIds: number[]
): Promise<QueryRecordViewResponseDto[]> {
    try {
        const projectIdsQuery = projectIds.map(id => `projectIds=${id}`).join('&');
        const res = await api.get<QueryRecordViewResponseDto[]>(
            `/organizations/${organizationId}/query/recent?${projectIdsQuery}`
        );
        return res.data;
    } catch (error) {
        console.error("Error getting recently added records:", error);
        throw error;
    }
}

/**
 * Get records from multiple projects
 * @param organizationId - The ID of the organization
 * @param projectIds - Array of project IDs whose records are to be retrieved
 * @param hideArchived - Flag to hide archived records (default: true)
 * @returns Promise with array of QueryRecordViewResponseDto
 */
export async function getMultiProjectRecords(
    organizationId: number,
    projectIds: number[],
    hideArchived: boolean = true
): Promise<QueryRecordViewResponseDto[]> {
    try {
        const projectIdsQuery = projectIds.map(id => `projects=${id}`).join('&');
        const res = await api.get(
            `/organizations/${organizationId}/query/multiproject?${projectIdsQuery}&hideArchived=${hideArchived}`
        );
        return res.data;
    } catch (error) {
        console.error("Error getting multi-project records:", error);
        throw error;
    }
}
