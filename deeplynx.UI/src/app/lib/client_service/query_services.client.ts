// src/app/lib/query_services.client.ts
"use client";

import { CustomQueryRequestDto } from "@/app/(home)/types/requestDTOs";
import { HistoricalRecordResponseDto, RecordResponseDto } from "@/app/(home)/types/responseDTOs";
import api from "./api";


/**
 * Full text search for records
 * @param organizationId - The ID of the organization
 * @param userQuery - String phrase entered by user
 * @param projectIds - Array of project IDs to search across
 * @returns Promise with array of HistoricalRecordResponseDto
 */
export async function fullTextSearch(
    organizationId: number,
    userQuery: string,
    projectIds: number[]
): Promise<HistoricalRecordResponseDto[]> {
    try {
        const projectIdsQuery = projectIds.map(id => `projectIds=${id}`).join('&');
        const res = await api.get(
            `/organizations/${organizationId}/query/records?userQuery=${encodeURIComponent(userQuery)}&${projectIdsQuery}`
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
 * @returns Promise with array of HistoricalRecordResponseDto
 */
export async function queryBuilder(
    organizationId: number,
    queryObj: CustomQueryRequestDto[],
    projectIds: number[],
    textSearch?: string | null
): Promise<HistoricalRecordResponseDto[]> {
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
 * @returns Promise with array of HistoricalRecordResponseDto sorted by most recent
 */
export async function getRecentlyAddedRecords(
    organizationId: number,
    projectIds: number[]
): Promise<HistoricalRecordResponseDto[]> {
    try {
        const projectIdsQuery = projectIds.map(id => `projectIds=${id}`).join('&');
        const res = await api.get<HistoricalRecordResponseDto[]>(
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
 * @returns Promise with array of RecordResponseDto
 */
export async function getMultiProjectRecords(
    organizationId: number,
    projectIds: number[],
    hideArchived: boolean = true
): Promise<HistoricalRecordResponseDto[]> {
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

/**
 * Get a historical record at a specific point in time
 * This allows you to see what a record looked like in the past
 * 
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project containing the record
 * @param recordId - The ID of the specific record to retrieve
 * @param pointInTime - Optional ISO 8601 timestamp to get record state at that moment
 * @param hideArchived - Whether to exclude archived records (default: true)
 * @returns Promise with a single HistoricalRecordResponseDto
 */
export async function getHistoricalRecord(
    organizationId: number,
    projectId: number,
    recordId: number,
    pointInTime?: string | null,
    hideArchived: boolean = true
): Promise<HistoricalRecordResponseDto> {
    try {
        const baseUrl = `/organizations/${organizationId}/projects/${projectId}/records/historical/${recordId}`;
        
        const queryParams: string[] = [];
        
        queryParams.push(`hideArchived=${hideArchived}`);
        
        if (pointInTime) {
            queryParams.push(`pointInTime=${encodeURIComponent(pointInTime)}`);
        }
        
        const queryString = queryParams.length > 0 ? `?${queryParams.join('&')}` : '';
        const fullUrl = `${baseUrl}${queryString}`;
        
        const res = await api.get<HistoricalRecordResponseDto>(fullUrl);
        
        return res.data;
    } catch (error) {
        console.error("Error getting historical record:", error);
        throw error;
    }
}