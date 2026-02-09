// src/app/lib/record_services.client.ts
"use client";

import { CreateRecordRequestDto, UpdateRecordRequestDto } from "@/app/(home)/types/requestDTOs";
import { HistoricalRecordResponseDto, RecordResponseDto, RelatedRecordsResponseDto } from "@/app/(home)/types/responseDTOs";
import { GraphResponse } from "@/app/(home)/types/types";
import api from "./api";


/**
 * Get all records for a project
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param dataSourceId - Optional data source ID to filter records
 * @param fileType - Optional file extension to filter by (e.g., pdf, png, jpg)
 * @param hideArchived - Flag to hide archived records (default: true)
 * @returns Promise with array of RecordResponseDto
 */
export async function getAllRecords(
  organizationId: number,
  projectId: number,
  dataSourceId?: number,
  fileType?: string,
  hideArchived: boolean = true
): Promise<RecordResponseDto[]> {
  try {
    const res = await api.get(
      `/organizations/${organizationId}/projects/${projectId}/records`,
      { params: { dataSourceId, fileType, hideArchived } }
    );
    return res.data;
  } catch (error) {
    console.error("Error getting all records:", error);
    throw error;
  }
}

/**
 * Get records by tags
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param tagIds - Array of tag IDs to filter by (records must contain all tags)
 * @param hideArchived - Flag to hide archived records (default: true)
 * @returns Promise with array of RecordResponseDto
 */
export async function getRecordsByTags(
  organizationId: number,
  projectId: number,
  tagIds: number[],
  hideArchived: boolean = true
): Promise<RecordResponseDto[]> {
  try {
    const params = new URLSearchParams();
    tagIds.forEach((tagId) => params.append("tagIds", tagId.toString()));
    params.append("hideArchived", hideArchived.toString());

    const res = await api.get(
      `/organizations/${organizationId}/projects/${projectId}/records/by-tags?${params.toString()}`
    );
    return res.data;
  } catch (error) {
    console.error("Error fetching records by tags:", error);
    throw error;
  }
}

/**
 * Get a specific record by ID
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param recordId - The ID of the record
 * @param hideArchived - Flag to hide archived records (default: true)
 * @returns Promise with RecordResponseDto
 */
export async function getRecord(
  organizationId: number,
  projectId: number,
  recordId: number,
  hideArchived: boolean = true
): Promise<RecordResponseDto> {
  try {
    const res = await api.get(
      `/organizations/${organizationId}/projects/${projectId}/records/${recordId}`,
      { params: { hideArchived } }
    );
    return res.data;
  } catch (error) {
    console.error(`Error fetching record ${recordId}:`, error);
    throw error;
  }
}

/**
 * Create a new record
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param dataSourceId - The ID of the data source (required)
 * @param dto - The record creation request DTO
 * @returns Promise with RecordResponseDto
 */
export async function createRecord(
  organizationId: number,
  projectId: number,
  dataSourceId: number,
  dto: CreateRecordRequestDto
): Promise<RecordResponseDto> {
  try {
    const res = await api.post(
      `/organizations/${organizationId}/projects/${projectId}/records`,
      dto,
      {
        headers: { "Content-Type": "application/json" },
        params: { dataSourceId }
      }
    );
    return res.data;
  } catch (error) {
    console.error("Error creating record:", error);
    throw error;
  }
}

/**
 * Bulk create records
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param dataSourceId - The ID of the data source (required)
 * @param records - Array of record creation request DTOs
 * @returns Promise with array of RecordResponseDto
 */
export async function bulkCreateRecords(
  organizationId: number,
  projectId: number,
  dataSourceId: number,
  records: CreateRecordRequestDto[]
): Promise<RecordResponseDto[]> {
  try {
    const res = await api.post(
      `/organizations/${organizationId}/projects/${projectId}/records/bulk`,
      records,
      {
        headers: { "Content-Type": "application/json" },
        params: { dataSourceId }
      }
    );
    return res.data;
  } catch (error) {
    console.error("Error bulk creating records:", error);
    throw error;
  }
}

/**
 * Update a record
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param recordId - The ID of the record to update
 * @param dto - The record update request DTO
 * @returns Promise with RecordResponseDto
 */
export async function updateRecord(
  organizationId: number,
  projectId: number,
  recordId: number,
  dto: UpdateRecordRequestDto
): Promise<RecordResponseDto> {
  try {
    const res = await api.put(
      `/organizations/${organizationId}/projects/${projectId}/records/${recordId}`,
      dto,
      { headers: { "Content-Type": "application/json" } }
    );
    return res.data;
  } catch (error) {
    console.error(`Error updating record ${recordId}:`, error);
    throw error;
  }
}

/**
 * Delete a record
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param recordId - The ID of the record to delete
 * @returns Promise with success message
 */
export async function deleteRecord(
  organizationId: number,
  projectId: number,
  recordId: number
): Promise<{ message: string }> {
  try {
    const res = await api.delete(
      `/organizations/${organizationId}/projects/${projectId}/records/${recordId}`
    );
    return res.data;
  } catch (error) {
    console.error(`Error deleting record ${recordId}:`, error);
    throw error;
  }
}

/**
 * Archive or unarchive a record
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param recordId - The ID of the record to archive/unarchive
 * @param archive - True to archive, false to unarchive
 * @returns Promise with success message
 */
export async function archiveRecord(
  organizationId: number,
  projectId: number,
  recordId: number,
  archive: boolean
): Promise<{ message: string }> {
  try {
    const res = await api.patch(
      `/organizations/${organizationId}/projects/${projectId}/records/${recordId}`,
      null,
      { params: { archive } }
    );
    return res.data;
  } catch (error) {
    console.error(`Error ${archive ? 'archiving' : 'unarchiving'} record ${recordId}:`, error);
    throw error;
  }
}

/**
 * Attach a tag to a record
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param recordId - The ID of the record
 * @param tagId - The ID of the tag to attach
 * @returns Promise with success message
 */
export async function attachTagToRecord(
  organizationId: number,
  projectId: number,
  recordId: number,
  tagId: number
): Promise<{ message: string }> {
  try {
    const res = await api.post(
      `/organizations/${organizationId}/projects/${projectId}/records/${recordId}/tags`,
      null,
      { params: { tagId } }
    );
    return res.data;
  } catch (error) {
    console.error(`Error attaching tag ${tagId} to record ${recordId}:`, error);
    throw error;
  }
}

/**
 * Unattach a tag from a record
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param recordId - The ID of the record
 * @param tagId - The ID of the tag to unattach
 * @returns Promise with success message
 */
export async function unattachTagFromRecord(
  organizationId: number,
  projectId: number,
  recordId: number,
  tagId: number
): Promise<{ message: string }> {
  try {
    const res = await api.delete(
      `/organizations/${organizationId}/projects/${projectId}/records/${recordId}/tags`,
      { params: { tagId } }
    );
    return res.data;
  } catch (error) {
    console.error(`Error unattaching tag ${tagId} from record ${recordId}:`, error);
    throw error;
  }
}

/**
 * Get edges by record
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param recordId - The ID of the record
 * @param isOrigin - Whether to find where recordId is origin
 * @param page - Page number for pagination
 * @param hideArchived - Flag to hide archived edges (default: true)
 * @param pageSize - Page size for pagination (default: 20)
 * @returns Promise with array of RelatedRecordsResponseDto
 */
export async function getEdgesByRecord(
  organizationId: number,
  projectId: number,
  recordId: number,
  isOrigin: boolean,
  page: number,
  hideArchived: boolean = true,
  pageSize: number = 20
): Promise<RelatedRecordsResponseDto[]> {
  try {
    const res = await api.get(
      `/organizations/${organizationId}/projects/${projectId}/records/${recordId}/edges`,
      { params: { isOrigin, page, hideArchived, pageSize } }
    );
    return res.data;
  } catch (error) {
    console.error(`Error getting edges for record ${recordId}:`, error);
    throw error;
  }
}

/**
 * Get graph data for a record
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param recordId - The ID of the record
 * @param depth - The number of levels to search through
 * @returns Promise with GraphResponse
 */
export async function getGraphDataForRecord(
  organizationId: number,
  projectId: number,
  recordId: number,
  depth: number
): Promise<GraphResponse> {
  try {
    const res = await api.get(
      `/organizations/${organizationId}/projects/${projectId}/records/${recordId}/graph`,
      { params: { depth } }
    );
    return res.data;
  } catch (error) {
    console.error(`Error getting graph data for record ${recordId}:`, error);
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