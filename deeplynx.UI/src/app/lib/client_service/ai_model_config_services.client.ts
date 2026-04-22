"use client";

import type {
  CreateAiModelConfigRequestDto,
  UpdateAiModelConfigRequestDto,
} from "@/app/(home)/types/requestDTOs";
import type { AiModelConfigResponseDto } from "@/app/(home)/types/responseDTOs";
import api from "./api";

function buildProjectAiModelConfigRoute(
  organizationId: number,
  projectId: number,
): string {
  return `/organizations/${organizationId}/projects/${projectId}/ai-model-configs`;
}

export async function getProjectAiModelConfigs(
  organizationId: number,
  projectId: number,
  hideArchived = true,
): Promise<AiModelConfigResponseDto[]> {
  const response = await api.get<AiModelConfigResponseDto[]>(
    buildProjectAiModelConfigRoute(organizationId, projectId),
    { params: { hideArchived } },
  );

  return response.data;
}

export async function createProjectAiModelConfig(
  organizationId: number,
  projectId: number,
  createModelConfigRequest: CreateAiModelConfigRequestDto,
): Promise<AiModelConfigResponseDto> {
  const response = await api.post<AiModelConfigResponseDto>(
    buildProjectAiModelConfigRoute(organizationId, projectId),
    createModelConfigRequest,
  );

  return response.data;
}

export async function updateProjectAiModelConfig(
  organizationId: number,
  projectId: number,
  aiModelConfigId: number,
  updateModelConfigRequest: UpdateAiModelConfigRequestDto,
): Promise<AiModelConfigResponseDto> {
  const response = await api.put<AiModelConfigResponseDto>(
    `${buildProjectAiModelConfigRoute(organizationId, projectId)}/${aiModelConfigId}`,
    updateModelConfigRequest,
  );

  return response.data;
}

export async function archiveProjectAiModelConfig(
  organizationId: number,
  projectId: number,
  aiModelConfigId: number,
): Promise<void> {
  await api.patch(
    `${buildProjectAiModelConfigRoute(organizationId, projectId)}/${aiModelConfigId}/archive`,
    undefined,
    { params: { archive: true } },
  );
}
