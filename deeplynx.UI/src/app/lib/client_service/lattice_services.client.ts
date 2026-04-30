"use client";

import { TriggerLatticeExtractionRequestDTO } from "@/app/(home)/types/requestDTOs";
import { TriggerLatticeExtractionResponseDTO } from "@/app/(home)/types/responseDTOs";
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
      dto,
      { headers: { "Content-Type": "application/json" } },
    );

    return res.data;
  } catch (error) {
    console.error("Error triggering Lattice extraction:", error);
    throw error;
  }
}
