// src/app/lib/client_service/airflow_services.client.ts
"use client";

import { TriggerDagRunRequestDto } from "@/app/(home)/types/requestDTOs";
import {
  AirflowDagListResponseDto,
  AirflowDagRunResponseDto,
} from "@/app/(home)/types/responseDTOs";
import api from "./api";

/* -------------------------------------------------------------------------- */
/*                                  Airflow                                   */
/* -------------------------------------------------------------------------- */

/**
 * Get all DAGs from Airflow.
 * @returns Promise with the DAG list response (dags + total_entries)
 */
export async function getAllDags(): Promise<AirflowDagListResponseDto> {
  try {
    const res = await api.get("/airflow/dags");
    return res.data;
  } catch (error) {
    console.error("Error getting all DAGs:", error);
    throw error;
  }
}

/**
 * Trigger a run of the given DAG.
 * @param dagId - The DAG to trigger
 * @param dto - Optional run configuration (run id, dates, conf, note)
 * @returns Promise with the triggered DAG run
 */
export async function triggerDagRun(
  dagId: string,
  dto: TriggerDagRunRequestDto = {},
): Promise<AirflowDagRunResponseDto> {
  try {
    const res = await api.post(
      `/airflow/dags/${encodeURIComponent(dagId)}/trigger`,
      dto,
    );
    return res.data;
  } catch (error) {
    console.error(`Error triggering DAG run for '${dagId}':`, error);
    throw error;
  }
}
