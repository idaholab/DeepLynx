"use client";

import { HistoricalRecordResponseDto } from "@/app/(home)/types/responseDTOs";
import api from "./api";

/**
 * Get all historical records for a project
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param dataSourceId - Optional data source ID to filter records
 * @param pointInTime - Optional point in time to get most current records before
 * @param hideArchived - Flag to hide archived records (default: true)
 * @returns Promise with array of HistoricalRecordResponseDto
 */
export async function getAllHistoricalRecords(
  organizationId: number,
  projectId: number,
  dataSourceId?: number | string,
  pointInTime?: string,
  hideArchived: boolean = true
): Promise<HistoricalRecordResponseDto[]> {
  try {
    const res = await api.get(
      `/organizations/${organizationId}/projects/${projectId}/records/historical`,
      { params: { dataSourceId, pointInTime, hideArchived } }
    );
    return res.data;
  } catch (error) {
    console.error("Error getting all historical records:", error);
    throw error;
  }
}

/**
 * Get a specific historical record by ID
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param recordId - The ID of the record
 * @param pointInTime - Optional point in time to get the most current record before
 * @param hideArchived - Flag to hide archived records (default: true)
 * @returns Promise with HistoricalRecordResponseDto
 */
export async function getHistoricalRecord(
  organizationId: number,
  projectId: number,
  recordId: number,
  pointInTime?: string,
  hideArchived: boolean = true
): Promise<HistoricalRecordResponseDto> {
  try {
    const res = await api.get(
      `/organizations/${organizationId}/projects/${projectId}/records/historical/${recordId}`,
      { params: { pointInTime, hideArchived } }
    );
    return res.data;
  } catch (error) {
    console.error(`Error getting historical record ${recordId}:`, error);
    throw error;
  }
}

/**
 * Get complete history for a record
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param recordId - The ID of the record
 * @returns Promise with array of HistoricalRecordResponseDto
 */
export async function getRecordHistory(
  organizationId: number,
  projectId: number,
  recordId: number
): Promise<HistoricalRecordResponseDto[]> {
  try {
    const res = await api.get(
      `/organizations/${organizationId}/projects/${projectId}/records/historical/${recordId}/history`
    );
    return res.data;
  } catch (error) {
    console.error(`Error getting history for record ${recordId}:`, error);
    throw error;
  }
}
