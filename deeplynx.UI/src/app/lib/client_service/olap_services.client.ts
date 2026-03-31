// src/app/lib/client_service/olap_services.client.ts
"use client";

import { OlapPlotData, OlapPlotResponse } from "@/app/(home)/types/olap_types";
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

        const res = await api.get<OlapPlotResponse>(
            `/organizations/${organizationId}/projects/${projectId}/records/${recordId}/olap/plot?${searchParams.toString()}`
        );

        return res.data.plotData;
    } catch (error) {
        console.error("Error fetching plot data:", error);
        throw error;
    }
}