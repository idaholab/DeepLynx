"use client";

import { useEffect, useMemo, useState } from "react";
import { getProjectAiModelConfigs } from "@/app/lib/client_service/ai_model_config_services.client";
import type { AiModelConfigResponseDto } from "@/app/(home)/types/responseDTOs";

export interface InsightModelSelection {
  queryModelConfigId: number | null;
  queryModelName: string | null;
  uploadModelConfigId: number | null;
  uploadModelName: string | null;
  embeddingModelConfigId: number | null;
  embeddingModelName: string | null;
}

export const EMPTY_INSIGHT_MODEL_SELECTION: InsightModelSelection = {
  queryModelConfigId: null,
  queryModelName: null,
  uploadModelConfigId: null,
  uploadModelName: null,
  embeddingModelConfigId: null,
  embeddingModelName: null,
};

function buildInsightModelSelectionStorageKey(
  organizationId?: number | null,
  projectId?: number | null,
): string | null {
  if (!organizationId || !projectId) {
    return null;
  }

  return `insight-model-selection:${organizationId}:${projectId}`;
}

function parseStoredInsightModelSelection(
  rawStoredValue: string | null,
): InsightModelSelection {
  if (!rawStoredValue) {
    return EMPTY_INSIGHT_MODEL_SELECTION;
  }

  try {
    const parsedValue = JSON.parse(rawStoredValue) as Partial<InsightModelSelection>;

    return {
      queryModelConfigId:
        typeof parsedValue.queryModelConfigId === "number"
          ? parsedValue.queryModelConfigId
          : null,
      queryModelName:
        typeof parsedValue.queryModelName === "string"
          ? parsedValue.queryModelName
          : null,
      uploadModelConfigId:
        typeof parsedValue.uploadModelConfigId === "number"
          ? parsedValue.uploadModelConfigId
          : null,
      uploadModelName:
        typeof parsedValue.uploadModelName === "string"
          ? parsedValue.uploadModelName
          : null,
      embeddingModelConfigId:
        typeof parsedValue.embeddingModelConfigId === "number"
          ? parsedValue.embeddingModelConfigId
          : null,
      embeddingModelName:
        typeof parsedValue.embeddingModelName === "string"
          ? parsedValue.embeddingModelName
          : null,
    };
  } catch {
    return EMPTY_INSIGHT_MODEL_SELECTION;
  }
}

function syncSelectionAgainstAvailableModelConfigs(
  currentSelection: InsightModelSelection,
  availableModelConfigs: AiModelConfigResponseDto[],
): InsightModelSelection {
  const queryModelConfig = availableModelConfigs.find(
    (aiModelConfig) => aiModelConfig.id === currentSelection.queryModelConfigId,
  );
  const uploadModelConfig = availableModelConfigs.find(
    (aiModelConfig) => aiModelConfig.id === currentSelection.uploadModelConfigId,
  );
  const embeddingModelConfig = availableModelConfigs.find(
    (aiModelConfig) =>
      aiModelConfig.id === currentSelection.embeddingModelConfigId,
  );

  return {
    queryModelConfigId: queryModelConfig?.id ?? null,
    queryModelName: queryModelConfig?.modelName ?? null,
    uploadModelConfigId: uploadModelConfig?.id ?? null,
    uploadModelName: uploadModelConfig?.modelName ?? null,
    embeddingModelConfigId: embeddingModelConfig?.id ?? null,
    embeddingModelName: embeddingModelConfig?.modelName ?? null,
  };
}

export function useInsightModelSelection(
  organizationId?: number | null,
  projectId?: number | null,
) {
  const selectionStorageKey = useMemo(
    () => buildInsightModelSelectionStorageKey(organizationId, projectId),
    [organizationId, projectId],
  );
  const [selectedInsightModels, setSelectedInsightModels] =
    useState<InsightModelSelection>(EMPTY_INSIGHT_MODEL_SELECTION);
  const [loadedSelectionStorageKey, setLoadedSelectionStorageKey] = useState<
    string | null
  >(null);
  const [validatedSelectionStorageKey, setValidatedSelectionStorageKey] =
    useState<string | null>(null);

  useEffect(() => {
    if (!selectionStorageKey || typeof window === "undefined") {
      setSelectedInsightModels(EMPTY_INSIGHT_MODEL_SELECTION);
      setLoadedSelectionStorageKey(selectionStorageKey);
      setValidatedSelectionStorageKey(selectionStorageKey);
      return;
    }

    const storedSelection = parseStoredInsightModelSelection(
      window.localStorage.getItem(selectionStorageKey),
    );
    setSelectedInsightModels(storedSelection);
    setLoadedSelectionStorageKey(selectionStorageKey);
    setValidatedSelectionStorageKey(null);
  }, [selectionStorageKey]);

  useEffect(() => {
    if (
      !selectionStorageKey ||
      loadedSelectionStorageKey !== selectionStorageKey ||
      validatedSelectionStorageKey === selectionStorageKey ||
      !organizationId ||
      !projectId
    ) {
      return;
    }

    const resolvedOrganizationId = organizationId;
    const resolvedProjectId = projectId;
    let hasCancelled = false;

    async function validateStoredSelection() {
      try {
        // Local storage is only a preference cache. Validate the ids against the
        // current project configs so deleted or archived configs do not linger.
        const availableModelConfigs = await getProjectAiModelConfigs(
          resolvedOrganizationId,
          resolvedProjectId,
        );
        if (hasCancelled) {
          return;
        }

        setSelectedInsightModels((currentSelection) =>
          syncSelectionAgainstAvailableModelConfigs(
            currentSelection,
            availableModelConfigs,
          ),
        );
      } catch (error) {
        if (!hasCancelled) {
          console.error(
            "Failed to validate stored Insight model selection:",
            error,
          );
        }
      } finally {
        if (!hasCancelled) {
          setValidatedSelectionStorageKey(selectionStorageKey);
        }
      }
    }

    void validateStoredSelection();

    return () => {
      hasCancelled = true;
    };
  }, [
    loadedSelectionStorageKey,
    organizationId,
    projectId,
    selectionStorageKey,
    validatedSelectionStorageKey,
  ]);

  useEffect(() => {
    if (
      !selectionStorageKey ||
      loadedSelectionStorageKey !== selectionStorageKey ||
      typeof window === "undefined"
    ) {
      return;
    }

    // Persist only after the current project's stored selection has been loaded.
    window.localStorage.setItem(
      selectionStorageKey,
      JSON.stringify(selectedInsightModels),
    );
  }, [loadedSelectionStorageKey, selectedInsightModels, selectionStorageKey]);

  return {
    hasLoadedStoredSelection: loadedSelectionStorageKey === selectionStorageKey,
    selectedInsightModels,
    setSelectedInsightModels,
  };
}
