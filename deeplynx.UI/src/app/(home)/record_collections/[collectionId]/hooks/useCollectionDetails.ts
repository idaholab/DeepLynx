"use client";

import { useLanguage } from "@/app/contexts/Language";
import {
  addRecordsToRecordCollection,
  attachSensitivityLabelToRecordCollection,
  attachTagToRecordCollection,
  getAllRecordCollections,
  getRecordsInRecordCollection,
  removeRecordsFromRecordCollection,
  unattachSensitivityLabelFromRecordCollection,
  unattachTagFromRecordCollection,
  updateRecordCollection,
} from "@/app/lib/client_service/record_collection_services.client";
import { createSensitivityLabelProject } from "@/app/lib/client_service/sensitivity_labels_services.client";
import {
  fullTextSearch,
  getMultiProjectRecords,
} from "@/app/lib/client_service/query_services.client";
import { createTag } from "@/app/lib/client_service/tag_services.client";
import { useCallback, useEffect, useRef, useState } from "react";
import {
  QueryRecordViewResponseDto,
  RecordCollectionLabelDto,
  RecordCollectionResponseDto,
  RecordCollectionTagDto,
  RecordResponseDto,
} from "../../../types/responseDTOs";
import {
  COLLECTION_BADGE_DISPLAY_LIMIT,
  NEW_COLLECTION_RECORDS_PER_PAGE,
} from "../../components/recordCollections.constants";
import {
  PendingRecordChanges,
  RecordMutationStatusById,
} from "../../components/recordCollections.types";
import { useSelectedCollectionDetailsView } from "../../hooks/useSelectedCollectionDetailsView";
import { useSelectedCollectionEditDerived } from "../../hooks/useSelectedCollectionEditDerived";
import { useProjectCollectionOptions } from "../../hooks/useProjectCollectionOptions";
import { useToast } from "@/app/contexts/ToastProvider";
import {
  mapSearchResultToCollectionRecord,
  parseProperties,
  getMetadataRows,
  getSensitivityClass,
} from "@/app/lib/record_helpers";

type Params = {
  organizationId: number;
  projectId: number;
  initialCollection: RecordCollectionResponseDto;
  initialCollectionRecords: RecordResponseDto[];
};

type CollectionWorkspaceTabId = "Details" | "Records";

export function useCollectionDetails({
  organizationId,
  projectId,
  initialCollection,
  initialCollectionRecords,
}: Params) {
  const { t } = useLanguage();
  const [selectedCollection, setSelectedCollection] =
    useState<RecordCollectionResponseDto>(initialCollection);
  const [isEditingSelectedCollection, setIsEditingSelectedCollection] =
    useState(false);
  const [selectedCollectionDraft, setSelectedCollectionDraft] =
    useState<RecordCollectionResponseDto | null>(null);
  const [
    selectedCollectionPropertiesEditorOpen,
    setSelectedCollectionPropertiesEditorOpen,
  ] = useState(false);
  const skipInitialCollectionRecordsLoad = useRef(true);
  const collectionRecordsRef = useRef<RecordResponseDto[]>(
    initialCollectionRecords,
  );
  const editStartCollectionRecordsRef = useRef<RecordResponseDto[] | null>(
    null,
  );
  const [collectionWorkspaceTab, setCollectionWorkspaceTab] =
    useState<CollectionWorkspaceTabId>("Details");
  const [collectionRecords, setCollectionRecords] = useState<
    RecordResponseDto[]
  >(initialCollectionRecords);
  const [recordSearchTerm, setRecordSearchTerm] = useState("");
  const [recordSearchResults, setRecordSearchResults] = useState<
    QueryRecordViewResponseDto[]
  >([]);
  const [selectedRecordIds, setSelectedRecordIds] = useState<number[]>([]);
  const [recordMutationStatusById, setRecordMutationStatusById] =
    useState<RecordMutationStatusById>({});
  const [pendingRecordChanges, setPendingRecordChanges] =
    useState<PendingRecordChanges>({
      added: [],
      removed: [],
    });
  const [recordsLoading, setRecordsLoading] = useState(false);
  const [recordSearchLoading, setRecordSearchLoading] = useState(false);
  const {
    availableLabels,
    setAvailableLabels,
    labelsLoading,
    availableTags,
    setAvailableTags,
    tagsLoading,
  } = useProjectCollectionOptions(projectId);
  const [saving, setSaving] = useState(false);
  const [
    selectedCollectionLabelSearchTerm,
    setSelectedCollectionLabelSearchTerm,
  ] = useState("");
  const [selectedCollectionTagSearchTerm, setSelectedCollectionTagSearchTerm] =
    useState("");
  const [selectedCollectionLabelCreating, setSelectedCollectionLabelCreating] =
    useState(false);
  const [selectedCollectionTagCreating, setSelectedCollectionTagCreating] =
    useState(false);
  const { showToast } = useToast();

  const collectionDetailsView = useSelectedCollectionDetailsView({
    selectedCollection,
    selectedCollectionDraft,
    collectionRecords,
    badgeDisplayLimit: COLLECTION_BADGE_DISPLAY_LIMIT,
    recordsPerPage: NEW_COLLECTION_RECORDS_PER_PAGE,
  });
  const selectedCollectionEditDerived = useSelectedCollectionEditDerived({
    selectedCollectionDraft,
    availableLabels,
    availableTags,
    selectedCollectionLabelSearchTerm,
    selectedCollectionTagSearchTerm,
    recordSearchTerm,
    recordSearchResults,
    collectionRecords,
  });
  const { editableSelectedCollection, resetCollectionDetailsView } =
    collectionDetailsView;
  const { addableRecordResults } = selectedCollectionEditDerived;

  useEffect(() => {
    collectionRecordsRef.current = collectionRecords;
  }, [collectionRecords]);

  const syncSelectedCollectionRecordCount = useCallback((count: number) => {
    setSelectedCollection((current) => ({ ...current, recordCount: count }));
    setSelectedCollectionDraft((current) =>
      current ? { ...current, recordCount: count } : current,
    );
  }, []);

  const syncDraftCollectionRecordCount = useCallback((count: number) => {
    setSelectedCollectionDraft((current) =>
      current ? { ...current, recordCount: count } : current,
    );
  }, []);

  const trackAddedRecords = useCallback(
    (recordIds: number[]) => {
      if (!isEditingSelectedCollection) return;

      setPendingRecordChanges((current) => {
        const nextAdded = new Set(current.added);
        const nextRemoved = new Set(current.removed);

        recordIds.forEach((recordId) => {
          if (nextRemoved.has(recordId)) {
            nextRemoved.delete(recordId);
          } else {
            nextAdded.add(recordId);
          }
        });

        return {
          added: Array.from(nextAdded),
          removed: Array.from(nextRemoved),
        };
      });
    },
    [isEditingSelectedCollection],
  );

  const trackRemovedRecords = useCallback(
    (recordIds: number[]) => {
      if (!isEditingSelectedCollection) return;

      setPendingRecordChanges((current) => {
        const nextAdded = new Set(current.added);
        const nextRemoved = new Set(current.removed);

        recordIds.forEach((recordId) => {
          if (nextAdded.has(recordId)) {
            nextAdded.delete(recordId);
          } else {
            nextRemoved.add(recordId);
          }
        });

        return {
          added: Array.from(nextAdded),
          removed: Array.from(nextRemoved),
        };
      });
    },
    [isEditingSelectedCollection],
  );

  const setRecordMutationStatus = useCallback(
    (recordId: number, status: "adding" | "removing") => {
      setRecordMutationStatusById((current) => ({
        ...current,
        [recordId]: status,
      }));
    },
    [],
  );

  const clearRecordMutationStatus = useCallback((recordId: number) => {
    setRecordMutationStatusById((current) => {
      const next = { ...current };
      delete next[recordId];
      return next;
    });
  }, []);

  const loadCollectionRecords = useCallback(async () => {
    setRecordsLoading(true);
    try {
      const records = await getRecordsInRecordCollection(
        organizationId,
        projectId,
        selectedCollection.id,
      );
      setCollectionRecords(records);
      return records;
    } catch (error) {
      console.error("Failed to load records in record collection:", error);
      t.translations.RECORD_COLLECTIONS_FAILED_LOAD_COLLECTION_RECORDS;
      return [];
    } finally {
      setRecordsLoading(false);
    }
  }, [organizationId, projectId, selectedCollection.id, t]);

  const stageCollectionRecords = useCallback(
    (nextRecords: RecordResponseDto[]) => {
      collectionRecordsRef.current = nextRecords;
      setCollectionRecords(nextRecords);
      syncDraftCollectionRecordCount(nextRecords.length);
    },
    [syncDraftCollectionRecordCount],
  );

  const stageAddedRecords = useCallback(
    (recordIds: number[]) => {
      const existingIds = new Set(
        collectionRecordsRef.current
          .map((record) => record.id)
          .filter((id): id is number => typeof id === "number"),
      );
      const recordsToAdd = recordIds
        .filter((recordId) => !existingIds.has(recordId))
        .map((recordId) =>
          recordSearchResults.find((record) => record.id === recordId),
        )
        .filter((record): record is QueryRecordViewResponseDto =>
          Boolean(record && typeof record.id === "number"),
        )
        .map(mapSearchResultToCollectionRecord);

      if (recordsToAdd.length === 0) {
        return false;
      }

      stageCollectionRecords([
        ...collectionRecordsRef.current,
        ...recordsToAdd,
      ]);
      trackAddedRecords(
        recordsToAdd
          .map((record) => record.id)
          .filter((id): id is number => typeof id === "number"),
      );
      return true;
    },
    [recordSearchResults, stageCollectionRecords, trackAddedRecords],
  );

  const stageRemovedRecords = useCallback(
    (recordIds: number[]) => {
      const nextRecords = collectionRecordsRef.current.filter(
        (record) =>
          !(typeof record.id === "number" && recordIds.includes(record.id)),
      );

      if (nextRecords.length === collectionRecordsRef.current.length) {
        return false;
      }

      stageCollectionRecords(nextRecords);
      trackRemovedRecords(recordIds);
      return true;
    },
    [stageCollectionRecords, trackRemovedRecords],
  );

  const refreshSelectedCollection = useCallback(async () => {
    let pageNumber = 1;

    while (true) {
      const page = await getAllRecordCollections(organizationId, projectId, {
        pageNumber,
        pageSize: 500,
      });

      const refreshedCollection = page.items.find(
        (collection) => collection.id === selectedCollection.id,
      );

      if (refreshedCollection) {
        setSelectedCollection(refreshedCollection);
        return refreshedCollection;
      }

      if (pageNumber * page.pageSize >= page.totalCount) {
        throw new Error(
          `Unable to refresh record collection ${selectedCollection.id}`,
        );
      }

      pageNumber += 1;
    }
  }, [organizationId, projectId, selectedCollection.id]);

  useEffect(() => {
    if (skipInitialCollectionRecordsLoad.current) {
      skipInitialCollectionRecordsLoad.current = false;
      return;
    }

    void loadCollectionRecords();
  }, [loadCollectionRecords]);

  const handleSearchRecords = async () => {
    const query = recordSearchTerm.trim();

    setRecordSearchLoading(true);
    try {
      const results = query
        ? await fullTextSearch(organizationId, query, [projectId])
        : await getMultiProjectRecords(organizationId, [projectId]);
      setRecordSearchResults(results);
      setSelectedRecordIds([]);
    } catch (error) {
      console.error("Failed to search records:", error);
      showToast(
        "error",
        t.translations.RECORD_COLLECTIONS_FAILED_SEARCH_RECORDS,
      );
    } finally {
      setRecordSearchLoading(false);
    }
  };

  const toggleSelectedRecord = (recordId: number) => {
    setSelectedRecordIds((prev) =>
      prev.includes(recordId)
        ? prev.filter((id) => id !== recordId)
        : [...prev, recordId],
    );
  };

  const handleAddSelectedRecords = async () => {
    if (selectedRecordIds.length === 0) return;

    if (isEditingSelectedCollection) {
      const didStageRecords = stageAddedRecords(selectedRecordIds);
      if (!didStageRecords) return;

      setSelectedRecordIds([]);
      setRecordSearchResults([]);
      setRecordSearchTerm("");
      showToast("success", t.translations.RECORD_COLLECTIONS_RECORDS_ADDED);
      return;
    }

    setSaving(true);
    try {
      await addRecordsToRecordCollection(
        organizationId,
        projectId,
        selectedCollection.id,
        selectedRecordIds,
      );
      const records = await loadCollectionRecords();
      syncSelectedCollectionRecordCount(records.length);
      trackAddedRecords(selectedRecordIds);
      setSelectedRecordIds([]);
      setRecordSearchResults([]);
      setRecordSearchTerm("");
      showToast("success", t.translations.RECORD_COLLECTIONS_RECORDS_ADDED);
    } catch (error) {
      console.error("Failed to add records to collection:", error);
      showToast("error", t.translations.RECORD_COLLECTIONS_FAILED_UPDATE);
    } finally {
      setSaving(false);
    }
  };

  const handleAddCollectionRecord = async (recordId?: number | null) => {
    if (typeof recordId !== "number") return;

    if (isEditingSelectedCollection) {
      const didStageRecord = stageAddedRecords([recordId]);
      if (!didStageRecord) return;

      showToast("success", t.translations.RECORD_COLLECTIONS_RECORD_ADDED);
      return;
    }

    setRecordMutationStatus(recordId, "adding");
    try {
      await addRecordsToRecordCollection(
        organizationId,
        projectId,
        selectedCollection.id,
        [recordId],
      );
      const records = await loadCollectionRecords();
      syncSelectedCollectionRecordCount(records.length);
      trackAddedRecords([recordId]);
      showToast("success", t.translations.RECORD_COLLECTIONS_RECORD_ADDED);
    } catch (error) {
      console.error("Failed to add record to collection:", error);
      showToast("error", t.translations.RECORD_COLLECTIONS_FAILED_UPDATE);
    } finally {
      clearRecordMutationStatus(recordId);
    }
  };

  const handleRemoveCollectionRecord = async (recordId?: number | null) => {
    if (typeof recordId !== "number") return;

    if (isEditingSelectedCollection) {
      const didStageRecord = stageRemovedRecords([recordId]);
      if (!didStageRecord) return;

      setSelectedRecordIds((prev) => prev.filter((id) => id !== recordId));
      showToast("success", t.translations.RECORD_COLLECTIONS_RECORD_REMOVED);
      return;
    }

    setRecordMutationStatus(recordId, "removing");
    try {
      await removeRecordsFromRecordCollection(
        organizationId,
        projectId,
        selectedCollection.id,
        [recordId],
      );
      const records = await loadCollectionRecords();
      syncSelectedCollectionRecordCount(records.length);
      trackRemovedRecords([recordId]);
      setSelectedRecordIds((prev) => prev.filter((id) => id !== recordId));
      showToast("success", t.translations.RECORD_COLLECTIONS_RECORD_REMOVED);
    } catch (error) {
      console.error("Failed to remove record from collection:", error);
      showToast("error", t.translations.RECORD_COLLECTIONS_FAILED_UPDATE);
    } finally {
      clearRecordMutationStatus(recordId);
    }
  };

  const openSelectedCollectionEdit = () => {
    editStartCollectionRecordsRef.current = [...collectionRecordsRef.current];
    setPendingRecordChanges({ added: [], removed: [] });
    setSelectedCollectionDraft({
      ...selectedCollection,
      labels: [...(selectedCollection.labels ?? [])],
      tags: [...(selectedCollection.tags ?? [])],
    });
    setSelectedCollectionLabelSearchTerm("");
    setSelectedCollectionTagSearchTerm("");
    setRecordSearchTerm("");
    setRecordSearchResults([]);
    setSelectedRecordIds([]);
    setIsEditingSelectedCollection(true);
  };

  const cancelSelectedCollectionEdit = async () => {
    const originalRecords =
      editStartCollectionRecordsRef.current ?? collectionRecordsRef.current;

    collectionRecordsRef.current = originalRecords;
    setCollectionRecords(originalRecords);
    editStartCollectionRecordsRef.current = null;
    setPendingRecordChanges({ added: [], removed: [] });
    setSelectedCollectionDraft(null);
    setSelectedCollectionPropertiesEditorOpen(false);
    setSelectedCollectionLabelSearchTerm("");
    setSelectedCollectionTagSearchTerm("");
    setRecordSearchTerm("");
    setRecordSearchResults([]);
    setSelectedRecordIds([]);
    setRecordMutationStatusById({});
    resetCollectionDetailsView();
    setIsEditingSelectedCollection(false);
  };

  const handleSaveSelectedDetails = async () => {
    if (!selectedCollectionDraft) return;

    setSaving(true);
    try {
      const originalLabelIds = new Set(
        selectedCollection.labels?.map((label) => label.id) ?? [],
      );
      const draftLabelIds = new Set(
        selectedCollectionDraft.labels?.map((label) => label.id) ?? [],
      );
      const originalTagIds = new Set(
        selectedCollection.tags?.map((tag) => tag.id) ?? [],
      );
      const draftTagIds = new Set(
        selectedCollectionDraft.tags?.map((tag) => tag.id) ?? [],
      );

      await updateRecordCollection(
        organizationId,
        projectId,
        selectedCollection.id,
        {
          name: selectedCollectionDraft.name,
          description: selectedCollectionDraft.description,
          properties: parseProperties(selectedCollectionDraft.properties),
        },
      );

      const recordMutationOperations = [
        ...pendingRecordChanges.added.map((recordId) => {
          const addedRecord =
            collectionRecordsRef.current.find(
              (record) => record.id === recordId,
            ) ?? recordSearchResults.find((record) => record.id === recordId);

          return {
            description:
              t.translations.RECORD_COLLECTIONS_PARTIAL_UPDATE_ADD_RECORD.replace(
                "{name}",
                addedRecord?.name ?? String(recordId),
              ),
            execute: () =>
              addRecordsToRecordCollection(
                organizationId,
                projectId,
                selectedCollection.id,
                [recordId],
              ),
          };
        }),
        ...pendingRecordChanges.removed.map((recordId) => {
          const removedRecord = editStartCollectionRecordsRef.current?.find(
            (record) => record.id === recordId,
          );

          return {
            description:
              t.translations.RECORD_COLLECTIONS_PARTIAL_UPDATE_REMOVE_RECORD.replace(
                "{name}",
                removedRecord?.name ?? String(recordId),
              ),
            execute: () =>
              removeRecordsFromRecordCollection(
                organizationId,
                projectId,
                selectedCollection.id,
                [recordId],
              ),
          };
        }),
      ];

      const labelAndTagMutationOperations = [
        ...(selectedCollectionDraft.labels ?? [])
          .filter((label) => !originalLabelIds.has(label.id))
          .map((label) => ({
            description:
              t.translations.RECORD_COLLECTIONS_PARTIAL_UPDATE_ADD_LABEL.replace(
                "{name}",
                label.name,
              ),
            execute: () =>
              attachSensitivityLabelToRecordCollection(
                organizationId,
                projectId,
                selectedCollection.id,
                label.id,
              ),
          })),
        ...(selectedCollection.labels ?? [])
          .filter((label) => !draftLabelIds.has(label.id))
          .map((label) => ({
            description:
              t.translations.RECORD_COLLECTIONS_PARTIAL_UPDATE_REMOVE_LABEL.replace(
                "{name}",
                label.name,
              ),
            execute: () =>
              unattachSensitivityLabelFromRecordCollection(
                organizationId,
                projectId,
                selectedCollection.id,
                label.id,
              ),
          })),
        ...(selectedCollectionDraft.tags ?? [])
          .filter((tag) => !originalTagIds.has(tag.id))
          .map((tag) => ({
            description:
              t.translations.RECORD_COLLECTIONS_PARTIAL_UPDATE_ADD_TAG.replace(
                "{name}",
                tag.name,
              ),
            execute: () =>
              attachTagToRecordCollection(
                organizationId,
                projectId,
                selectedCollection.id,
                tag.id,
              ),
          })),
        ...(selectedCollection.tags ?? [])
          .filter((tag) => !draftTagIds.has(tag.id))
          .map((tag) => ({
            description:
              t.translations.RECORD_COLLECTIONS_PARTIAL_UPDATE_REMOVE_TAG.replace(
                "{name}",
                tag.name,
              ),
            execute: () =>
              unattachTagFromRecordCollection(
                organizationId,
                projectId,
                selectedCollection.id,
                tag.id,
              ),
          })),
      ];

      const recordMutationResults = await Promise.allSettled(
        recordMutationOperations.map((operation) => operation.execute()),
      );
      const labelAndTagMutationResults = await Promise.allSettled(
        labelAndTagMutationOperations.map((operation) => operation.execute()),
      );
      const failedOperations = [
        ...recordMutationResults.flatMap((result, index) =>
          result.status === "rejected"
            ? [recordMutationOperations[index].description]
            : [],
        ),
        ...labelAndTagMutationResults.flatMap((result, index) =>
          result.status === "rejected"
            ? [labelAndTagMutationOperations[index].description]
            : [],
        ),
      ];

      let didRefreshFail = false;

      try {
        await refreshSelectedCollection();
        await loadCollectionRecords();
      } catch (refreshError) {
        didRefreshFail = true;
        console.error(
          "Failed to refresh record collection after save:",
          refreshError,
        );
        showToast(
          "error",
          t.translations.RECORD_COLLECTIONS_SAVED_REFRESH_FAILED,
        );
      }

      editStartCollectionRecordsRef.current = null;
      setSelectedCollectionDraft(null);
      setSelectedCollectionPropertiesEditorOpen(false);
      setPendingRecordChanges({ added: [], removed: [] });
      setRecordSearchTerm("");
      setRecordSearchResults([]);
      setSelectedRecordIds([]);
      setRecordMutationStatusById({});
      resetCollectionDetailsView();
      setIsEditingSelectedCollection(false);

      if (failedOperations.length > 0) {
        showToast(
          "error",
          t.translations.RECORD_COLLECTIONS_PARTIAL_UPDATE.replace(
            "{operations}",
            failedOperations.join(", "),
          ),
        );
      } else if (!didRefreshFail) {
        showToast("success", t.translations.RECORD_COLLECTIONS_UPDATE_SUCCESS);
      }
    } catch (error) {
      console.error("Failed to update record collection:", error);
      showToast("error", t.translations.RECORD_COLLECTIONS_FAILED_UPDATE);
    } finally {
      setSaving(false);
    }
  };

  const handleSaveSelectedCollectionProperties = async (
    properties: Record<string, unknown>,
  ) => {
    setSelectedCollectionDraft((current) => {
      const draft = current ?? selectedCollection;
      return {
        ...draft,
        properties: JSON.stringify(properties ?? {}),
      };
    });
    setSelectedCollectionPropertiesEditorOpen(false);
  };

  const handleRemoveLabel = (labelId: number) => {
    if (!selectedCollectionDraft) return;

    setSelectedCollectionDraft({
      ...selectedCollectionDraft,
      labels: selectedCollectionDraft.labels?.filter(
        (label) => label.id !== labelId,
      ),
    });
  };

  const handleRemoveTag = (tagId: number) => {
    if (!selectedCollectionDraft) return;

    setSelectedCollectionDraft({
      ...selectedCollectionDraft,
      tags: selectedCollectionDraft.tags?.filter((tag) => tag.id !== tagId),
    });
  };

  const addSelectedCollectionLabel = (label: RecordCollectionLabelDto) => {
    if (!selectedCollectionDraft) return;

    setSelectedCollectionDraft({
      ...selectedCollectionDraft,
      labels: (selectedCollectionDraft.labels ?? []).some(
        (item) => item.id === label.id,
      )
        ? selectedCollectionDraft.labels
        : [...(selectedCollectionDraft.labels ?? []), label],
    });
    setSelectedCollectionLabelSearchTerm("");
  };

  const addSelectedCollectionTag = (tag: RecordCollectionTagDto) => {
    if (!selectedCollectionDraft) return;

    setSelectedCollectionDraft({
      ...selectedCollectionDraft,
      tags: (selectedCollectionDraft.tags ?? []).some(
        (item) => item.id === tag.id,
      )
        ? selectedCollectionDraft.tags
        : [...(selectedCollectionDraft.tags ?? []), tag],
    });
    setSelectedCollectionTagSearchTerm("");
  };

  const addSelectedCollectionLabelFromSearch = async () => {
    const trimmed = selectedCollectionLabelSearchTerm.trim();
    if (!trimmed || !selectedCollectionDraft) return;

    const existingLabel = availableLabels.find(
      (label) => label.name.toLowerCase() === trimmed.toLowerCase(),
    );
    if (existingLabel) {
      addSelectedCollectionLabel({
        id: existingLabel.id,
        name: existingLabel.name,
      });
      return;
    }

    setSelectedCollectionLabelCreating(true);
    try {
      const createdLabel = await createSensitivityLabelProject(projectId, {
        name: trimmed,
        description: "",
      });
      setAvailableLabels((prev) => [...prev, createdLabel]);
      addSelectedCollectionLabel({
        id: createdLabel.id,
        name: createdLabel.name,
      });
      showToast("success", t.translations.RECORD_COLLECTIONS_LABEL_CREATED);
    } catch (error) {
      console.error("Failed to create sensitivity label:", error);
      showToast("error", t.translations.RECORD_COLLECTIONS_FAILED_CREATE_LABEL);
    } finally {
      setSelectedCollectionLabelCreating(false);
    }
  };

  const addSelectedCollectionTagFromSearch = async () => {
    const trimmed = selectedCollectionTagSearchTerm.trim();
    if (!trimmed || !selectedCollectionDraft) return;

    const existingTag = availableTags.find(
      (tag) => tag.name.toLowerCase() === trimmed.toLowerCase(),
    );
    if (existingTag) {
      addSelectedCollectionTag({
        id: existingTag.id,
        name: existingTag.name,
      });
      return;
    }

    setSelectedCollectionTagCreating(true);
    try {
      const createdTag = await createTag(projectId, { name: trimmed });
      setAvailableTags((prev) => [...prev, createdTag]);
      addSelectedCollectionTag({
        id: createdTag.id,
        name: createdTag.name,
      });
      showToast("success", t.translations.RECORD_COLLECTIONS_TAG_CREATED);
    } catch (error) {
      console.error("Failed to create tag:", error);
      showToast("error", t.translations.RECORD_COLLECTIONS_FAILED_CREATE_TAG);
    } finally {
      setSelectedCollectionTagCreating(false);
    }
  };

  const recordsController = {
    overview: {
      selectedCollection,
      projectId,
      collectionRecords,
      recordsLoading,
    },
    search: {
      recordSearchTerm,
      setRecordSearchTerm,
      recordSearchLoading,
      recordSearchResults,
      addableRecordResults,
      onSearchRecords: handleSearchRecords,
    },
    selection: {
      saving,
      selectedRecordIds,
      onToggleSelectedRecord: toggleSelectedRecord,
      onAddSelectedRecords: handleAddSelectedRecords,
    },
    actions: {
      onBackToDetails: () => setCollectionWorkspaceTab("Details"),
    },
  };

  const activeSelectedCollection =
    editableSelectedCollection ?? selectedCollection;
  const detailsController = {
    readonlyView: {
      selectedCollection,
      ...collectionDetailsView,
      badgeDisplayLimit: COLLECTION_BADGE_DISPLAY_LIMIT,
      getMetadataRows,
      getSensitivityClass,
      collectionRecords,
      recordsLoading,
      onViewAllCollectionRecords: () => setCollectionWorkspaceTab("Records"),
      recordsPerPage: NEW_COLLECTION_RECORDS_PER_PAGE,
      projectId,
      onOpenSelectedCollectionEdit: openSelectedCollectionEdit,
    },
    editView: {
      editableSelectedCollection: activeSelectedCollection,
      isEditingSelectedCollection,
      saving,
      setSelectedCollectionDraft,
      setSelectedCollectionPropertiesEditorOpen,
      selectedCollectionLabelSearchTerm,
      setSelectedCollectionLabelSearchTerm,
      selectedCollectionTagSearchTerm,
      setSelectedCollectionTagSearchTerm,
      selectedCollectionLabelCreating,
      selectedCollectionTagCreating,
      labelsLoading,
      tagsLoading,
      ...selectedCollectionEditDerived,
      onAddSelectedCollectionLabelFromSearch:
        addSelectedCollectionLabelFromSearch,
      onAddSelectedCollectionTagFromSearch: addSelectedCollectionTagFromSearch,
      onAddSelectedCollectionLabel: addSelectedCollectionLabel,
      onAddSelectedCollectionTag: addSelectedCollectionTag,
      onRemoveLabel: handleRemoveLabel,
      onRemoveTag: handleRemoveTag,
      recordSearchTerm,
      setRecordSearchTerm,
      recordSearchLoading,
      onSearchRecords: handleSearchRecords,
      recordMutationStatusById,
      onRemoveCollectionRecord: handleRemoveCollectionRecord,
      onAddCollectionRecord: handleAddCollectionRecord,
      onCancelSelectedCollectionEdit: cancelSelectedCollectionEdit,
      onSaveSelectedDetails: handleSaveSelectedDetails,
    },
  };

  return {
    workspace: {
      tab: collectionWorkspaceTab,
    },
    detailsController,
    recordsController,
    propertiesEditor: {
      isOpen: selectedCollectionPropertiesEditorOpen,
      onClose: () => setSelectedCollectionPropertiesEditorOpen(false),
      properties: parseProperties(editableSelectedCollection?.properties),
      onSave: handleSaveSelectedCollectionProperties,
      isSaving: saving,
    },
  };
}

export type CollectionDetailsController = ReturnType<
  typeof useCollectionDetails
>;
