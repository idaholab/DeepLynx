"use client";

import { DEFAULT_PAGE_SIZE_OPTIONS } from "@/app/(home)/components/PaginationControls";
import { useLanguage } from "@/app/contexts/Language";
import {
  addRecordsToRecordCollection,
  createRecordCollection,
} from "@/app/lib/client_service/record_collection_services.client";
import {
  createSensitivityLabelProject,
  getAllSensitivityLabelsProject,
} from "@/app/lib/client_service/sensitivity_labels_services.client";
import {
  fullTextSearch,
  getMultiProjectRecords,
} from "@/app/lib/client_service/query_services.client";
import { getRecord } from "@/app/lib/client_service/record_services.client";
import { getAllTags } from "@/app/lib/client_service/tag_services.client";
import { useRouter } from "next/navigation";
import { useCallback, useEffect, useState } from "react";
import toast from "react-hot-toast";
import {
  QueryRecordViewResponseDto,
  SensitivityLabelsDto,
  TagResponseDto,
} from "../../../types/responseDTOs";
import { NewCollectionTabController, NewCollectionStep } from "../../components/componentTypes";
import { NewCollectionSelectedRecord } from "../../components/recordCollections.types";
import {
  getSelectedRecordLabelNames,
  getSelectedRecordTagNames,
  getSensitivityClass,
} from "../../components/recordCollections.utils";
import { useNewCollectionDerived } from "./useNewCollectionDerived";

type Params = {
  organizationId: number;
  projectId: number;
};

export function useNewCollectionWorkflow({
  organizationId,
  projectId,
}: Params): { controller: NewCollectionTabController } {
  const router = useRouter();
  const { t } = useLanguage();
  const [labelsLoading, setLabelsLoading] = useState(false);
  const [newCollectionLabelCreating, setNewCollectionLabelCreating] =
    useState(false);
  const [availableLabels, setAvailableLabels] = useState<SensitivityLabelsDto[]>([]);
  const [tagsLoading, setTagsLoading] = useState(false);
  const [availableTags, setAvailableTags] = useState<TagResponseDto[]>([]);
  const [saving, setSaving] = useState(false);
  const [newCollectionStep, setNewCollectionStep] =
    useState<NewCollectionStep>("Records");
  const [newCollectionName, setNewCollectionName] = useState("");
  const [newCollectionDescription, setNewCollectionDescription] = useState("");
  const [newCollectionSelectedTagNames, setNewCollectionSelectedTagNames] =
    useState<string[]>([]);
  const [newCollectionLabelSearchTerm, setNewCollectionLabelSearchTerm] =
    useState("");
  const [newCollectionTagSearchTerm, setNewCollectionTagSearchTerm] =
    useState("");
  const [newCollectionSelectedLabelIds, setNewCollectionSelectedLabelIds] =
    useState<number[]>([]);
  const [newCollectionRecordSearchTerm, setNewCollectionRecordSearchTerm] =
    useState("");
  const [newCollectionRecordSearchResults, setNewCollectionRecordSearchResults] =
    useState<QueryRecordViewResponseDto[]>([]);
  const [newCollectionRecordPage, setNewCollectionRecordPage] = useState(1);
  const [newCollectionRecordsPerPage, setNewCollectionRecordsPerPage] = useState(
    DEFAULT_PAGE_SIZE_OPTIONS[0],
  );
  const [newCollectionSelectedRecordIds, setNewCollectionSelectedRecordIds] =
    useState<number[]>([]);
  const [newCollectionSelectedRecords, setNewCollectionSelectedRecords] =
    useState<NewCollectionSelectedRecord[]>([]);
  const [confirmClearNewCollectionRecords, setConfirmClearNewCollectionRecords] =
    useState(false);
  const [newCollectionReviewSearchTerm, setNewCollectionReviewSearchTerm] =
    useState("");
  const [newCollectionReviewPage, setNewCollectionReviewPage] = useState(1);
  const [newCollectionRecordSearchLoading, setNewCollectionRecordSearchLoading] =
    useState(false);

  const {
    newCollectionSelectedLabelTally,
    newCollectionSelectedTagTally,
    selectedNewCollectionLabels,
    filteredNewCollectionLabelOptions,
    filteredNewCollectionTagOptions,
    canAddTypedNewCollectionTag,
    canAddTypedNewCollectionLabel,
    newCollectionRecordPageCount,
    visibleNewCollectionRecords,
    allVisibleNewCollectionRecordsSelected,
    allRetrievedNewCollectionRecordsSelected,
    someVisibleNewCollectionRecordsSelected,
    filteredNewCollectionSelectedRecords,
    newCollectionReviewPageCount,
    visibleNewCollectionReviewRecords,
    selectedRecordMetadata,
  } = useNewCollectionDerived({
    availableLabels,
    availableTags,
    newCollectionSelectedLabelIds,
    newCollectionSelectedTagNames,
    newCollectionLabelSearchTerm,
    newCollectionTagSearchTerm,
    newCollectionRecordSearchResults,
    newCollectionRecordPage,
    newCollectionSelectedRecordIds,
    newCollectionSelectedRecords,
    newCollectionReviewSearchTerm,
    newCollectionReviewPage,
    recordsPerPage: newCollectionRecordsPerPage,
    setNewCollectionReviewPage,
  });

  const handleSetNewCollectionRecordsPerPage = useCallback((pageSize: number) => {
    setNewCollectionRecordsPerPage(pageSize);
    setNewCollectionRecordPage(1);
    setNewCollectionReviewPage(1);
  }, []);

  useEffect(() => {
    const loadLabels = async () => {
      setLabelsLoading(true);
      try {
        const labels = await getAllSensitivityLabelsProject(projectId);
        setAvailableLabels(labels);
      } catch (error) {
        console.error("Failed to load project labels:", error);
        toast.error(t.translations.RECORD_COLLECTIONS_FAILED_LOAD_PROJECT_LABELS);
      } finally {
        setLabelsLoading(false);
      }
    };

    void loadLabels();
  }, [projectId, t]);

  useEffect(() => {
    const loadTags = async () => {
      setTagsLoading(true);
      try {
        const tags = await getAllTags(projectId);
        setAvailableTags(tags);
      } catch (error) {
        console.error("Failed to load project tags:", error);
        toast.error(t.translations.RECORD_COLLECTIONS_FAILED_LOAD_PROJECT_TAGS);
      } finally {
        setTagsLoading(false);
      }
    };

    void loadTags();
  }, [projectId, t]);

  const addNewCollectionRecords = useCallback(
    async (records: QueryRecordViewResponseDto[]) => {
      const unselectedRecords = records.filter(
        (
          record,
        ): record is QueryRecordViewResponseDto & { id: number; projectId?: number | null } =>
          typeof record.id === "number" &&
          !newCollectionSelectedRecordIds.includes(record.id),
      );
      if (unselectedRecords.length === 0) return;

      setNewCollectionSelectedRecordIds((prev) => [
        ...prev,
        ...unselectedRecords.map((record) => record.id),
      ]);
      setNewCollectionSelectedRecords((prev) => [...prev, ...unselectedRecords]);

      const enrichedRecords = await Promise.all(
        unselectedRecords.map(async (record) => {
          try {
            const fullRecord = await getRecord(
              organizationId,
              record.projectId ?? projectId,
              record.id,
            );
            return { ...record, fullRecord };
          } catch (error) {
            console.error("Failed to load selected record labels:", error);
            return record;
          }
        }),
      );

      setNewCollectionSelectedRecords((prev) =>
        prev.map((selectedRecord) => {
          const enrichedRecord = enrichedRecords.find(
            (record) => record.id === selectedRecord.id,
          );
          return enrichedRecord ?? selectedRecord;
        }),
      );
    },
    [newCollectionSelectedRecordIds, organizationId, projectId],
  );

  const toggleNewCollectionRecord = async (record: QueryRecordViewResponseDto) => {
    if (typeof record.id !== "number") return;

    if (newCollectionSelectedRecordIds.includes(record.id)) {
      setNewCollectionSelectedRecordIds((prev) =>
        prev.filter((id) => id !== record.id),
      );
      setNewCollectionSelectedRecords((prev) =>
        prev.filter((selectedRecord) => selectedRecord.id !== record.id),
      );
      return;
    }

    await addNewCollectionRecords([record]);
  };

  const toggleSelectAllVisibleRecords = async () => {
    const visibleRecordIds = visibleNewCollectionRecords
      .map((record) => record.id)
      .filter((id): id is number => typeof id === "number");

    if (allVisibleNewCollectionRecordsSelected) {
      const visibleIdSet = new Set(visibleRecordIds);
      setNewCollectionSelectedRecordIds((prev) =>
        prev.filter((id) => !visibleIdSet.has(id)),
      );
      setNewCollectionSelectedRecords((prev) =>
        prev.filter(
          (record) =>
            typeof record.id !== "number" || !visibleIdSet.has(record.id),
        ),
      );
      return;
    }

    await addNewCollectionRecords(visibleNewCollectionRecords);
  };

  const handleSelectAllSearchedRecords = async () => {
    await addNewCollectionRecords(newCollectionRecordSearchResults);
  };

  const deselectNewCollectionRecordsByLabel = (labelName: string) => {
    const remainingRecords = newCollectionSelectedRecords.filter(
      (record) => !getSelectedRecordLabelNames(record).includes(labelName),
    );
    setNewCollectionSelectedRecords(remainingRecords);
    setNewCollectionSelectedRecordIds(
      remainingRecords
        .map((record) => record.id)
        .filter((id): id is number => typeof id === "number"),
    );
  };

  const deselectNewCollectionRecordsByTag = (tagName: string) => {
    const remainingRecords = newCollectionSelectedRecords.filter(
      (record) => !getSelectedRecordTagNames(record).includes(tagName),
    );
    setNewCollectionSelectedRecords(remainingRecords);
    setNewCollectionSelectedRecordIds(
      remainingRecords
        .map((record) => record.id)
        .filter((id): id is number => typeof id === "number"),
    );
  };

  const clearNewCollectionSelectedRecords = () => {
    setNewCollectionSelectedRecordIds([]);
    setNewCollectionSelectedRecords([]);
    setConfirmClearNewCollectionRecords(false);
  };

  const addNewCollectionLabel = (labelId: number) => {
    setNewCollectionSelectedLabelIds((prev) =>
      prev.includes(labelId) ? prev : [...prev, labelId],
    );
  };

  const addNewCollectionLabelFromSearch = async () => {
    const trimmed = newCollectionLabelSearchTerm.trim();
    if (!trimmed) return;

    const existingLabel = availableLabels.find(
      (label) => label.name.toLowerCase() === trimmed.toLowerCase(),
    );
    if (existingLabel) {
      addNewCollectionLabel(existingLabel.id);
      setNewCollectionLabelSearchTerm("");
      return;
    }

    setNewCollectionLabelCreating(true);
    try {
      const createdLabel = await createSensitivityLabelProject(projectId, {
        name: trimmed,
        description: "",
      });
      setAvailableLabels((prev) => [...prev, createdLabel]);
      addNewCollectionLabel(createdLabel.id);
      setNewCollectionLabelSearchTerm("");
      toast.success(t.translations.RECORD_COLLECTIONS_LABEL_CREATED);
    } catch (error) {
      console.error("Failed to create sensitivity label:", error);
      toast.error(t.translations.RECORD_COLLECTIONS_FAILED_CREATE_LABEL);
    } finally {
      setNewCollectionLabelCreating(false);
    }
  };

  const removeNewCollectionLabel = (labelId: number) => {
    setNewCollectionSelectedLabelIds((prev) =>
      prev.filter((id) => id !== labelId),
    );
  };

  const addNewCollectionTag = (tagName: string) => {
    const trimmed = tagName.trim();
    if (!trimmed) return;

    setNewCollectionSelectedTagNames((prev) =>
      prev.some((name) => name.toLowerCase() === trimmed.toLowerCase())
        ? prev
        : [...prev, trimmed],
    );
    setNewCollectionTagSearchTerm("");
  };

  const removeNewCollectionTag = (tagName: string) => {
    setNewCollectionSelectedTagNames((prev) =>
      prev.filter((name) => name !== tagName),
    );
  };

  const handleSearchNewCollectionRecords = async (overrideTerm?: string) => {
    const query = (overrideTerm ?? newCollectionRecordSearchTerm).trim();

    setNewCollectionRecordSearchLoading(true);
    try {
      const results = query
        ? await fullTextSearch(organizationId, query, [projectId])
        : await getMultiProjectRecords(organizationId, [projectId]);
      setNewCollectionRecordSearchResults(results);
      setNewCollectionRecordPage(1);
    } catch (error) {
      console.error("Failed to search records:", error);
      toast.error(t.translations.RECORD_COLLECTIONS_FAILED_SEARCH_RECORDS);
    } finally {
      setNewCollectionRecordSearchLoading(false);
    }
  };

  const clearNewCollectionRecordSearch = () => {
    setNewCollectionRecordSearchTerm("");
    void handleSearchNewCollectionRecords("");
  };

  useEffect(() => {
    if (
      newCollectionStep !== "Records" ||
      newCollectionRecordSearchTerm.trim() ||
      newCollectionRecordSearchResults.length > 0 ||
      newCollectionRecordSearchLoading
    ) {
      return;
    }

    void handleSearchNewCollectionRecords();
  }, [
    newCollectionStep,
    newCollectionRecordSearchResults.length,
    newCollectionRecordSearchTerm,
    newCollectionRecordSearchLoading,
  ]);

  const handleCreateCollection = async () => {
    const name = newCollectionName.trim();
    const description = newCollectionDescription.trim();
    if (!name || !description) {
      toast.error(t.translations.RECORD_COLLECTIONS_NAME_AND_DESCRIPTION_REQUIRED);
      return;
    }

    setSaving(true);
    try {
      const created = await createRecordCollection(
        organizationId,
        projectId,
        {
          name,
          description,
          properties: {},
          tags: newCollectionSelectedTagNames,
        },
        newCollectionSelectedLabelIds,
      );
      if (newCollectionSelectedRecordIds.length > 0) {
        await addRecordsToRecordCollection(
          organizationId,
          projectId,
          created.id,
          newCollectionSelectedRecordIds,
        );
      }
      toast.success(t.translations.RECORD_COLLECTIONS_CREATED);
      router.push("/record_collections");
      router.refresh();
    } catch (error) {
      console.error("Failed to create record collection:", error);
      toast.error(t.translations.RECORD_COLLECTIONS_FAILED_CREATE);
    } finally {
      setSaving(false);
    }
  };

  const goToNewCollectionModifyStep = () => {
    setNewCollectionSelectedLabelIds(selectedRecordMetadata.labelIds);
    setNewCollectionSelectedTagNames(selectedRecordMetadata.tagNames);
    setNewCollectionStep("Modify");
  };

  const goToNewCollectionReviewStep = () => {
    setNewCollectionReviewSearchTerm("");
    setNewCollectionReviewPage(1);
    setNewCollectionStep("Review");
  };

  const controller: NewCollectionTabController = {
    workflow: {
      projectId,
      newCollectionStep,
      setNewCollectionStep,
      recordsPerPage: newCollectionRecordsPerPage,
      setRecordsPerPage: handleSetNewCollectionRecordsPerPage,
      recordPageSizeOptions: DEFAULT_PAGE_SIZE_OPTIONS,
      saving,
      getSensitivityClass,
    },
    metadata: {
      newCollectionName,
      setNewCollectionName,
      newCollectionDescription,
      setNewCollectionDescription,
    },
    recordSearch: {
      newCollectionRecordSearchTerm,
      setNewCollectionRecordSearchTerm,
      newCollectionRecordSearchResults,
      newCollectionRecordSearchLoading,
      visibleNewCollectionRecords,
      allVisibleNewCollectionRecordsSelected,
      allRetrievedNewCollectionRecordsSelected,
      someVisibleNewCollectionRecordsSelected,
      newCollectionRecordPage,
      setNewCollectionRecordPage,
      newCollectionRecordPageCount,
      onSearchRecords: handleSearchNewCollectionRecords,
      onClearRecordSearch: clearNewCollectionRecordSearch,
      onToggleSelectAllVisibleRecords: toggleSelectAllVisibleRecords,
      onToggleNewCollectionRecord: toggleNewCollectionRecord,
      onSelectAllSearchedRecords: handleSelectAllSearchedRecords,
    },
    selection: {
      newCollectionSelectedRecordIds,
      newCollectionSelectedRecords,
      confirmClearNewCollectionRecords,
      setConfirmClearNewCollectionRecords,
      newCollectionSelectedLabelTally,
      newCollectionSelectedTagTally,
      onDeselectRecordsByLabel: deselectNewCollectionRecordsByLabel,
      onDeselectRecordsByTag: deselectNewCollectionRecordsByTag,
      onClearSelectedRecords: clearNewCollectionSelectedRecords,
    },
    review: {
      newCollectionReviewSearchTerm,
      setNewCollectionReviewSearchTerm,
      filteredNewCollectionSelectedRecords,
      visibleNewCollectionReviewRecords,
      newCollectionReviewPage,
      setNewCollectionReviewPage,
      newCollectionReviewPageCount,
    },
    labelsAndTags: {
      selectedNewCollectionLabels,
      newCollectionSelectedTagNames,
      newCollectionLabelSearchTerm,
      setNewCollectionLabelSearchTerm,
      newCollectionTagSearchTerm,
      setNewCollectionTagSearchTerm,
      filteredNewCollectionLabelOptions,
      filteredNewCollectionTagOptions,
      labelsLoading,
      tagsLoading,
      newCollectionLabelCreating,
      canAddTypedNewCollectionLabel,
      canAddTypedNewCollectionTag,
      onAddNewCollectionLabelFromSearch: addNewCollectionLabelFromSearch,
      onAddNewCollectionLabel: addNewCollectionLabel,
      onRemoveNewCollectionLabel: removeNewCollectionLabel,
      onAddNewCollectionTag: addNewCollectionTag,
      onRemoveNewCollectionTag: removeNewCollectionTag,
    },
    actions: {
      onCancelToAllCollections: () => {
        setNewCollectionStep("Records");
        router.push("/record_collections");
      },
      onGoToModifyStep: goToNewCollectionModifyStep,
      onGoToReviewStep: goToNewCollectionReviewStep,
      onCreateCollection: handleCreateCollection,
    },
  };

  return { controller };
}
