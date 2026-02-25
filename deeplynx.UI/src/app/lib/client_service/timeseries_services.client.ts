// src/app/lib/client_service/timeseries_services.client.ts
"use client";

import { LatestRowResponse, TimeseriesPlotData, TimeseriesPlotResponse } from "@/app/(home)/types/timeseries_types";
import api from "./api";
import { RecordResponseDto } from "@/app/(home)/types/responseDTOs";



/**
 * Upload timeseries file
 * @param organizationId - ID of organization that timeseries data is associated with
 * @param projectId - ID of project that timeseries data is associated with
 * @param datasourceId - ID of data source that timeseries data is associated with
 * @param file - The timeseries file to upload
 * @returns Promise with record response containing upload information
 */
export async function uploadTimeseriesFile(
    organizationId: number,
    projectId: number,
    datasourceId: number,
    file: File
): Promise<RecordResponseDto> {
    try {
        const form = new FormData();
        form.append("file", file, file.name);

        const res = await api.post<RecordResponseDto>(
            `/organizations/${organizationId}/projects/${projectId}/datasources/${datasourceId}/timeseries/upload`,
            form
        );

        return res.data;
    } catch (error) {
        console.error("Error uploading timeseries file:", error);
        throw error;
    }
}

/**
 * Get timeseries plot data
 * @param organizationId - ID of organization that timeseries data is associated with
 * @param projectId - ID of project that timeseries data is associated with
 * @param datasourceId - ID of data source that timeseries data is associated with
 * @param recordId - Name of the duckDB table on which the timeseries data is encoded
 * @param limit - Maximum number of data points to include
 * @param rowStride - Every nth row to get (row number 4 = every 4th row)
 * @returns Promise with timeseries plot data
 */
export async function getTimeseriesPlotData(
    organizationId: number,
    projectId: number,
    datasourceId: number,
    recordId: number,
    limit: number,
    rowStride: number
): Promise<TimeseriesPlotData> {
    try {
        const searchParams = new URLSearchParams();
        searchParams.append("recordId", recordId.toString());
        searchParams.append("limit", limit.toString());
        searchParams.append("rowStride", rowStride.toString());

        const res = await api.get<TimeseriesPlotResponse>(
            `/organizations/${organizationId}/projects/${projectId}/datasources/${datasourceId}/timeseries/plot?${searchParams.toString()}`
        );

        return res.data.timeseriesPlotData;
    } catch (error) {
        console.error("Error fetching timeseries plot data:", error);
        throw error;
    }
}

/**
 * Get the most recent row of timeseries data
 * @param organizationId - ID of organization that timeseries data is associated with
 * @param projectId - ID of project that timeseries data is associated with
 * @param datasourceId - ID of data source that timeseries data is associated with
 * @param recordId - Name of the duckDB table on which the timeseries data is encoded
 * @returns Promise with latest row data as key-value pairs
 */
export async function getLatestRow(
    organizationId: number,
    projectId: number,
    datasourceId: number,
    recordId: number
): Promise<Record<string, string | number>> {
    try {
        const searchParams = new URLSearchParams();
        searchParams.append("recordId", recordId.toString());

        const res = await api.get<LatestRowResponse>(
            `/organizations/${organizationId}/projects/${projectId}/datasources/${datasourceId}/timeseries/latest?${searchParams.toString()}`
        );

        return res.data.latestRowData;
    } catch (error) {
        console.error("Error fetching latest row:", error);
        throw error;
    }
}