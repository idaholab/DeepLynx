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
import {
  createSensitivityLabelProject,
  getAllSensitivityLabelsProject,
} from "@/app/lib/client_service/sensitivity_labels_services.client";
import {
  fullTextSearch,
  getMultiProjectRecords,
} from "@/app/lib/client_service/query_services.client";
import { createTag, getAllTags } from "@/app/lib/client_service/tag_services.client";
import { useCallback, useEffect, useRef, useState } from "react";
import toast from "react-hot-toast";
import {
  QueryRecordViewResponseDto,
  RecordCollectionLabelDto,
  RecordCollectionResponseDto,
  RecordCollectionTagDto,
  RecordResponseDto,
  SensitivityLabelsDto,
  TagResponseDto,
} from "../../../types/responseDTOs";
import {
  COLLECTION_BADGE_DISPLAY_LIMIT,
  NEW_COLLECTION_RECORDS_PER_PAGE,
} from "../../components/recordCollections.constants";
import { PendingRecordChanges } from "../../components/recordCollections.types";
import {
  getMetadataRows,
  getSensitivityClass,
  parseProperties,
} from "../../components/recordCollections.utils";
import { useSelectedCollectionDetailsController } from "../../hooks/useSelectedCollectionDetailsController";
import { useSelectedCollectionDetailsView } from "../../hooks/useSelectedCollectionDetailsView";
import { useSelectedCollectionEditDerived } from "../../hooks/useSelectedCollectionEditDerived";

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
  const [collectionWorkspaceTab, setCollectionWorkspaceTab] =
    useState<CollectionWorkspaceTabId>("Details");
  const [collectionRecords, setCollectionRecords] = useState<RecordResponseDto[]>(
    initialCollectionRecords,
  );
  const [recordSearchTerm, setRecordSearchTerm] = useState("");
  const [recordSearchResults, setRecordSearchResults] = useState<
    QueryRecordViewResponseDto[]
  >([]);
  const [selectedRecordIds, setSelectedRecordIds] = useState<number[]>([]);
  const [addingRecordIds, setAddingRecordIds] = useState<number[]>([]);
  const [removingRecordIds, setRemovingRecordIds] = useState<number[]>([]);
  const [pendingRecordChanges, setPendingRecordChanges] = useState<PendingRecordChanges>({
    added: [],
    removed: [],
  });
  const [recordsLoading, setRecordsLoading] = useState(false);
  const [recordSearchLoading, setRecordSearchLoading] = useState(false);
  const [labelsLoading, setLabelsLoading] = useState(false);
  const [availableLabels, setAvailableLabels] = useState<SensitivityLabelsDto[]>([]);
  const [tagsLoading, setTagsLoading] = useState(false);
  const [availableTags, setAvailableTags] = useState<TagResponseDto[]>([]);
  const [saving, setSaving] = useState(false);
  const [selectedCollectionLabelSearchTerm, setSelectedCollectionLabelSearchTerm] =
    useState("");
  const [selectedCollectionTagSearchTerm, setSelectedCollectionTagSearchTerm] =
    useState("");
  const [selectedCollectionLabelCreating, setSelectedCollectionLabelCreating] =
    useState(false);
  const [selectedCollectionTagCreating, setSelectedCollectionTagCreating] =
    useState(false);

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

  const syncSelectedCollectionRecordCount = useCallback((count: number) => {
    setSelectedCollection((current) => ({ ...current, recordCount: count }));
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
      toast.error(t.translations.RECORD_COLLECTIONS_FAILED_LOAD_COLLECTION_RECORDS);
      return [];
    } finally {
      setRecordsLoading(false);
    }
  }, [organizationId, projectId, selectedCollection.id, t]);

  const refreshSelectedCollection = useCallback(async () => {
    let pageNumber = 1;

    while (true) {
      const page = await getAllRecordCollections(
        organizationId,
        projectId,
        { pageNumber, pageSize: 500 },
      );

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
      toast.error(t.translations.RECORD_COLLECTIONS_FAILED_SEARCH_RECORDS);
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
      toast.success(t.translations.RECORD_COLLECTIONS_RECORDS_ADDED);
    } catch (error) {
      console.error("Failed to add records to collection:", error);
      toast.error(t.translations.RECORD_COLLECTIONS_FAILED_UPDATE);
    } finally {
      setSaving(false);
    }
  };

  const handleAddCollectionRecord = async (recordId?: number | null) => {
    if (typeof recordId !== "number") return;

    setAddingRecordIds((prev) => [...prev, recordId]);
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
      toast.success(t.translations.RECORD_COLLECTIONS_RECORD_ADDED);
    } catch (error) {
      console.error("Failed to add record to collection:", error);
      toast.error(t.translations.RECORD_COLLECTIONS_FAILED_UPDATE);
    } finally {
      setAddingRecordIds((prev) => prev.filter((id) => id !== recordId));
    }
  };

  const handleRemoveCollectionRecord = async (recordId?: number | null) => {
    if (typeof recordId !== "number") return;

    setRemovingRecordIds((prev) => [...prev, recordId]);
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
      toast.success(t.translations.RECORD_COLLECTIONS_RECORD_REMOVED);
    } catch (error) {
      console.error("Failed to remove record from collection:", error);
      toast.error(t.translations.RECORD_COLLECTIONS_FAILED_UPDATE);
    } finally {
      setRemovingRecordIds((prev) => prev.filter((id) => id !== recordId));
    }
  };

  const openSelectedCollectionEdit = () => {
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
    setSaving(true);
    try {
      if (pendingRecordChanges.added.length > 0) {
        await removeRecordsFromRecordCollection(
          organizationId,
          projectId,
          selectedCollection.id,
          pendingRecordChanges.added,
        );
      }

      if (pendingRecordChanges.removed.length > 0) {
        await addRecordsToRecordCollection(
          organizationId,
          projectId,
          selectedCollection.id,
          pendingRecordChanges.removed,
        );
      }

      const records = await loadCollectionRecords();
      syncSelectedCollectionRecordCount(records.length);
      setPendingRecordChanges({ added: [], removed: [] });
      setSelectedCollectionDraft(null);
      setSelectedCollectionPropertiesEditorOpen(false);
      setSelectedCollectionLabelSearchTerm("");
      setSelectedCollectionTagSearchTerm("");
      resetCollectionDetailsView();
      setIsEditingSelectedCollection(false);
    } catch (error) {
      console.error("Failed to cancel record collection edit:", error);
      toast.error(t.translations.RECORD_COLLECTIONS_FAILED_CANCEL_CHANGES);
    } finally {
      setSaving(false);
    }
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

      const mutationOperations = [
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

      const mutationResults = await Promise.allSettled(
        mutationOperations.map((operation) => operation.execute()),
      );
      const failedOperations = mutationResults.flatMap((result, index) =>
        result.status === "rejected"
          ? [mutationOperations[index].description]
          : [],
      );

      await refreshSelectedCollection();
      setSelectedCollectionDraft(null);
      setSelectedCollectionPropertiesEditorOpen(false);
      setPendingRecordChanges({ added: [], removed: [] });
      resetCollectionDetailsView();
      setIsEditingSelectedCollection(false);

      if (failedOperations.length > 0) {
        toast.error(
          t.translations.RECORD_COLLECTIONS_PARTIAL_UPDATE.replace(
            "{operations}",
            failedOperations.join(", "),
          ),
        );
      } else {
        toast.success(t.translations.RECORD_COLLECTIONS_UPDATE_SUCCESS);
      }
    } catch (error) {
      console.error("Failed to update record collection:", error);
      toast.error(t.translations.RECORD_COLLECTIONS_FAILED_UPDATE);
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
      labels: selectedCollectionDraft.labels?.filter((label) => label.id !== labelId),
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
      tags: (selectedCollectionDraft.tags ?? []).some((item) => item.id === tag.id)
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
      toast.success(t.translations.RECORD_COLLECTIONS_LABEL_CREATED);
    } catch (error) {
      console.error("Failed to create sensitivity label:", error);
      toast.error(t.translations.RECORD_COLLECTIONS_FAILED_CREATE_LABEL);
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
      toast.success(t.translations.RECORD_COLLECTIONS_TAG_CREATED);
    } catch (error) {
      console.error("Failed to create tag:", error);
      toast.error(t.translations.RECORD_COLLECTIONS_FAILED_CREATE_TAG);
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

  const detailsController = useSelectedCollectionDetailsController({
    context: {
      projectId,
      selectedCollection,
      collectionRecords,
      recordsLoading,
      saving,
      isEditingSelectedCollection,
    },
    view: collectionDetailsView,
    editDerived: selectedCollectionEditDerived,
    editState: {
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
    },
    labelAndTagActions: {
      onAddSelectedCollectionLabelFromSearch: addSelectedCollectionLabelFromSearch,
      onAddSelectedCollectionTagFromSearch: addSelectedCollectionTagFromSearch,
      onAddSelectedCollectionLabel: addSelectedCollectionLabel,
      onAddSelectedCollectionTag: addSelectedCollectionTag,
      onRemoveLabel: handleRemoveLabel,
      onRemoveTag: handleRemoveTag,
    },
    recordSearch: {
      recordSearchTerm,
      setRecordSearchTerm,
      recordSearchLoading,
      onSearchRecords: handleSearchRecords,
    },
    recordMutations: {
      addingRecordIds,
      removingRecordIds,
      onRemoveCollectionRecord: handleRemoveCollectionRecord,
      onAddCollectionRecord: handleAddCollectionRecord,
    },
    navigation: {
      onCancelSelectedCollectionEdit: cancelSelectedCollectionEdit,
      onSaveSelectedDetails: handleSaveSelectedDetails,
      onViewAllCollectionRecords: () => setCollectionWorkspaceTab("Records"),
      onOpenSelectedCollectionEdit: openSelectedCollectionEdit,
    },
    formatting: {
      getMetadataRows,
      getSensitivityClass,
    },
  });

  return {
    workspace: {
      tab: collectionWorkspaceTab,
    },
    detailsController: detailsController!,
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
