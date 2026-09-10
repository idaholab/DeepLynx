// src/app/lib/client_service/olap_services.client.ts
"use client";

import { OlapPlotData } from "@/app/(home)/types/olap_types";
import { HistoricalRecordResponseDto, PaginatedResponse } from "@/app/(home)/types/responseDTOs";
import api from "./api";


/**
 * Get olap plot data
 * @param organizationId - ID of organization that tabular data is associated with
 * @param projectId - ID of project that tabular data is associated with
 * @param recordId - ID of the record pointing to the file or folder to plot
 * @param limit - Maximum number of data points to include
 * @param rowStride - Every nth row to get (row number 4 = every 4th row)
 * @returns Promise with plot data
 */
export async function getPlotData(
    organizationId: number,
    projectId: number,
    recordId: number,
    limit: number,
    rowStride: number
): Promise<OlapPlotData> {
    try {
        const searchParams = new URLSearchParams();
        searchParams.append("limit", limit.toString());
        searchParams.append("rowStride", rowStride.toString());

        const res = await api.get(
            `/organizations/${organizationId}/projects/${projectId}/records/${recordId}/olap/plot?${searchParams.toString()}`
        );

        return res.data;
    } catch (error) {
        console.error("Error fetching plot data:", error);
        throw error;
    }
}

/**
 * Get all timeseries files for a project
 * @param organizationId - ID of the organization
 * @param projectId - ID of the project
 * @param pageNumber - Page number to fetch; omit to use the API default
 * @param pageSize - Page size; omit to use the API default, or pass -1 for all matching records
 * @returns Promise with paginated HistoricalRecordResponseDto
 */
export async function getTimeseriesFiles(
    organizationId: number,
    projectId: number,
    pageNumber?: number,
    pageSize?: number
): Promise<PaginatedResponse<HistoricalRecordResponseDto>> {
    try {
        const params = new URLSearchParams();
        params.append("projectIds", String(projectId));
        if (pageNumber !== undefined) params.append("pageNumber", String(pageNumber));
        if (pageSize !== undefined) params.append("pageSize", String(pageSize));

        const res = await api.post<PaginatedResponse<HistoricalRecordResponseDto>>(
            `/organizations/${organizationId}/query/records/advanced?${params.toString()}`,
            [{ filter: "class_name", operator: "=", value: "Timeseries" }]
        );

        return res.data;
    } catch (error) {
        console.error("Error fetching timeseries files:", error);
        throw error;
    }
}