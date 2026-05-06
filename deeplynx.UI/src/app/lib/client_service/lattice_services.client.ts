"use client";

import { TriggerLatticeExtractionRequestDTO } from "@/app/(home)/types/requestDTOs";
import { TriggerLatticeExtractionResponseDTO } from "@/app/(home)/types/responseDTOs";
import { ExtractionListItemDTO, ExtractionStagingResponseDTO } from "@/app/(home)/types/latticeDTOs";
import api from "./api";

export async function triggerLatticeExtraction(
  organizationId: number,
  projectId: number,
  recordId: number,
  dto: TriggerLatticeExtractionRequestDTO,
): Promise<TriggerLatticeExtractionResponseDTO> {
  try {
    const res = await api.post(
      `/organizations/${organizationId}/projects/${projectId}/records/${recordId}/trigger`,
      null,
      { params: { mode: dto.mode } },
    );

    return res.data;
  } catch (error) {
    console.error("Error triggering Lattice extraction:", error);
    throw error;
  }
}

export async function listExtractions(
  organizationId: number,
  projectId: number,
): Promise<ExtractionListItemDTO[]> {
  const res = await api.get(
    `/organizations/${organizationId}/projects/${projectId}/extractions`,
  );
  return res.data;
}

export async function getExtractionStaging(
  organizationId: number,
  projectId: number,
  extractionId: number,
): Promise<ExtractionStagingResponseDTO> {
  const res = await api.get(
    `/organizations/${organizationId}/projects/${projectId}/extractions/${extractionId}/staging`,
  );
  return res.data;
}

export async function promoteExtraction(
  organizationId: number,
  projectId: number,
  extractionId: number,
  approve: boolean,
): Promise<ExtractionStagingResponseDTO> {
  const res = await api.post(
    `/organizations/${organizationId}/projects/${projectId}/extractions/${extractionId}/promote`,
    null,
    { params: { approve } },
  );
  return res.data;
}
