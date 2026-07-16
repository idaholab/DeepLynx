// src/app/lib/client_service/airflow_services.client.ts
"use client";

import { TriggerDagRunRequestDto } from "@/app/(home)/types/requestDTOs";
import {
  AirflowDagResponseDto,
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
 * Get details for a DAG.
 * @param dagId - The DAG to inspect
 * @returns Promise with DAG details
 */
export async function getDagDetails(
  dagId: string,
): Promise<AirflowDagResponseDto> {
  try {
    const res = await api.get(
      `/airflow/dags/${encodeURIComponent(dagId)}/details`,
    );
    return res.data;
  } catch (error) {
    console.error(`Error getting DAG details for '${dagId}':`, error);
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

/**
 * Get the latest state for a DAG run.
 * @param dagId - The DAG that owns the run
 * @param dagRunId - The DAG run id
 * @returns Promise with the DAG run details
 */
export async function getDagRun(
  dagId: string,
  dagRunId: string,
): Promise<AirflowDagRunResponseDto> {
  try {
    const res = await api.get(
      `/airflow/dags/${encodeURIComponent(dagId)}/runs/${encodeURIComponent(
        dagRunId,
      )}`,
    );
    return res.data;
  } catch (error) {
    console.error(
      `Error getting DAG run '${dagRunId}' for '${dagId}':`,
      error,
    );
    throw error;
  }
}
