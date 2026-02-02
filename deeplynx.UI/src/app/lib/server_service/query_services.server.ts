// src/app/lib/server_service/query_services.server.ts
import "server-only";
import { apiFetch, asJson } from "./api.server";
import { CustomQueryRequestDto } from "@/app/(home)/types/requestDTOs";
import { HistoricalRecordResponseDto } from "@/app/(home)/types/responseDTOs";

/**
 * Full text search for records (server-side)
 * @param organizationId - The ID of the organization
 * @param userQuery - String phrase entered by user
 * @param projectIds - Array of project IDs to search across
 * @returns Promise with array of HistoricalRecordResponseDto
 */
export async function fullTextSearchServer(
    organizationId: number,
    userQuery: string,
    projectIds: number[]
): Promise<HistoricalRecordResponseDto[]> {
    const searchParams = new URLSearchParams();
    searchParams.append("userQuery", userQuery);
    projectIds.forEach(id => searchParams.append("projectIds", id.toString()));

    const path = `/organizations/${organizationId}/query/records?${searchParams.toString()}`;

    const res = await apiFetch(path);
    return asJson<HistoricalRecordResponseDto[]>(res);
}

/**
 * Build a custom query for records (server-side)
 * @param organizationId - The ID of the organization
 * @param queryObj - Array of custom query request DTOs
 * @param projectIds - Array of project IDs to search across
 * @param textSearch - Optional full text search phrase
 * @returns Promise with array of HistoricalRecordResponseDto
 */
export async function queryBuilderServer(
    organizationId: number,
    queryObj: CustomQueryRequestDto[],
    projectIds: number[],
    textSearch?: string | null
): Promise<HistoricalRecordResponseDto[]> {
    // Building json string format from key/value input
    for (const obj of queryObj) {
        if (obj.jsonKey && obj.jsonValue) {
            const json = `{"${obj.jsonKey}": "${obj.jsonValue}"}`;
            obj.json = json;
        }
    }

    const searchParams = new URLSearchParams();
    projectIds.forEach(id => searchParams.append("projectIds", id.toString()));
    if (textSearch) {
        searchParams.append("textSearch", textSearch);
    }

    const path = `/organizations/${organizationId}/query/records/advanced?${searchParams.toString()}`;

    const res = await apiFetch(path, {
        method: "POST",
        body: JSON.stringify(queryObj),
    });

    return asJson<HistoricalRecordResponseDto[]>(res);
}

/**
 * Get recently added records (server-side)
 * @param organizationId - The ID of the organization
 * @param projectIds - Array of project IDs
 * @returns Promise with array of HistoricalRecordResponseDto sorted by most recent
 */
export async function getRecentlyAddedRecordsServer(
    organizationId: number,
    projectIds: number[]
): Promise<HistoricalRecordResponseDto[]> {
    const searchParams = new URLSearchParams();
    projectIds.forEach(id => searchParams.append("projectIds", id.toString()));

    const path = `/organizations/${organizationId}/query/recent?${searchParams.toString()}`;

    const res = await apiFetch(path);
    return asJson<HistoricalRecordResponseDto[]>(res);
}

/**
 * Get records from multiple projects (server-side)
 * @param organizationId - The ID of the organization
 * @param projectIds - Array of project IDs whose records are to be retrieved
 * @param hideArchived - Flag to hide archived records (default: true)
 * @returns Promise with array of HistoricalRecordResponseDto
 */
export async function getMultiProjectRecordsServer(
    organizationId: number,
    projectIds: number[],
    hideArchived: boolean = true
): Promise<HistoricalRecordResponseDto[]> {
    const searchParams = new URLSearchParams();
    projectIds.forEach(id => searchParams.append("projects", id.toString()));
    searchParams.append("hideArchived", hideArchived.toString());

    const path = `/organizations/${organizationId}/query/multiproject?${searchParams.toString()}`;

    const res = await apiFetch(path);
    return asJson<HistoricalRecordResponseDto[]>(res);
}