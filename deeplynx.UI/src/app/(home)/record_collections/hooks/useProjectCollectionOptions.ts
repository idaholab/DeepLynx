"use client";

import { useLanguage } from "@/app/contexts/Language";
import { getAllSensitivityLabelsProject } from "@/app/lib/client_service/sensitivity_labels_services.client";
import { getAllTags } from "@/app/lib/client_service/tag_services.client";
import { useEffect, useState } from "react";
import { SensitivityLabelsDto, TagResponseDto } from "../../types/responseDTOs";
import { useToast } from "@/app/contexts/ToastProvider";

export function useProjectCollectionOptions(projectId: number) {
  const { t } = useLanguage();
  const [labelsLoading, setLabelsLoading] = useState(false);
  const [availableLabels, setAvailableLabels] = useState<
    SensitivityLabelsDto[]
  >([]);
  const [tagsLoading, setTagsLoading] = useState(false);
  const [availableTags, setAvailableTags] = useState<TagResponseDto[]>([]);
  const { showToast } = useToast();

  useEffect(() => {
    let cancelled = false;

    const loadOptions = async () => {
      setLabelsLoading(true);
      setTagsLoading(true);
      const [labelsResult, tagsResult] = await Promise.allSettled([
        getAllSensitivityLabelsProject(projectId),
        getAllTags(projectId),
      ]);

      if (cancelled) return;

      if (labelsResult.status === "fulfilled") {
        setAvailableLabels(labelsResult.value);
      } else {
        console.error("Failed to load project labels:", labelsResult.reason);
        showToast(
          "error",
          t.translations.RECORD_COLLECTIONS_FAILED_LOAD_PROJECT_LABELS,
        );
      }

      if (tagsResult.status === "fulfilled") {
        setAvailableTags(tagsResult.value);
      } else {
        console.error("Failed to load project tags:", tagsResult.reason);
        showToast(
          "error",
          t.translations.RECORD_COLLECTIONS_FAILED_LOAD_PROJECT_TAGS,
        );
      }

      if (!cancelled) {
        setLabelsLoading(false);
        setTagsLoading(false);
      }
    };

    void loadOptions();

    return () => {
      cancelled = true;
    };
  }, [projectId, t]);

  return {
    availableLabels,
    setAvailableLabels,
    labelsLoading,
    availableTags,
    setAvailableTags,
    tagsLoading,
  };
}
