"use client";

import PaginationControls, {
  DEFAULT_PAGE_SIZE_OPTIONS,
} from "@/app/(home)/components/PaginationControls";
import SearchInput from "@/app/(home)/components/SearchInput";
import Tabs from "@/app/(home)/components/Tabs";
import { useLanguage } from "@/app/contexts/Language";
import { ArrowLeftIcon } from "@heroicons/react/24/outline";
import React, { useCallback, useEffect, useState } from "react";
import toast from "react-hot-toast";
import AdditionalPropertiesEditor from "../record/components/AdditionalPropertiesEditor";
import {
  QueryRecordViewResponseDto,
  RecordCollectionLabelDto,
  RecordCollectionResponseDto,
  RecordCollectionTagDto,
  RecordResponseDto,
  SensitivityLabelsDto,
  TagResponseDto,
} from "../types/responseDTOs";
import {
  addRecordsToRecordCollection,
  attachSensitivityLabelToRecordCollection,
  attachTagToRecordCollection,
  createRecordCollection,
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
import { getRecord } from "@/app/lib/client_service/record_services.client";
import { createTag, getAllTags } from "@/app/lib/client_service/tag_services.client";
import CollectionDashboardCard from "./components/CollectionDashboardCard";
import CollectionSortControl from "./components/CollectionSortControl";
import FilterSidebar from "./components/FilterSidebar";
import NewCollectionTabContent from "./components/NewCollectionTabContent";
import SectionCard from "./components/SectionCard";
import SelectedCollectionDetailsTab from "./components/SelectedCollectionDetailsTab";
import SelectedCollectionRecordsTab from "./components/SelectedCollectionRecordsTab";
import {
  COLLECTION_BADGE_DISPLAY_LIMIT,
  COLLECTION_SORT_OPTIONS,
  NEW_COLLECTION_RECORDS_PER_PAGE,
} from "./components/recordCollections.constants";
import {
  getSelectedRecordLabelNames,
  getSelectedRecordTagNames,
  mergeDraftEntities,
  getMetadataRows,
  getSensitivityClass,
  parseProperties,
} from "./components/recordCollections.utils";
import { renderCollectionSortLabel } from "./components/recordCollections.view-utils";
import {
  NewCollectionSelectedRecord,
  PendingRecordChanges,
} from "./components/recordCollections.types";
import { useCollectionsDashboard } from "./hooks/useCollectionsDashboard";
import { useSelectedCollectionDetailsController } from "./hooks/useSelectedCollectionDetailsController";
import { useNewCollectionDerived } from "./hooks/useNewCollectionDerived";
import { useSelectedCollectionDetailsView } from "./hooks/useSelectedCollectionDetailsView";
import { useSelectedCollectionEditDerived } from "./hooks/useSelectedCollectionEditDerived";
import { interpolateTemplate } from "./components/utils";

type Props = {
  recordCollections: RecordCollectionResponseDto[];
  organizationId: number;
  projectId: number;
};

type TopLevelTabId = "All Collections" | "New Collection";
type CollectionWorkspaceTabId = "Details" | "Records";
type NewCollectionStep = "Records" | "Metadata" | "Modify" | "Review";
/* ─── Component ──────────────────────────────────────────────────────────── */

export default function RecordCollectionsClient({
  recordCollections,
  organizationId,
  projectId,
}: Props) {
  const { t } = useLanguage();
  const [collections, setCollections] =
    useState<RecordCollectionResponseDto[]>(recordCollections);
  const [activeTab, setActiveTab] = useState<TopLevelTabId>("All Collections");
  const [selectedCollection, setSelectedCollection] =
    useState<RecordCollectionResponseDto | null>(null);
  const [isEditingSelectedCollection, setIsEditingSelectedCollection] =
    useState(false);
  const [selectedCollectionDraft, setSelectedCollectionDraft] =
    useState<RecordCollectionResponseDto | null>(null);
  const [
    selectedCollectionPropertiesEditorOpen,
    setSelectedCollectionPropertiesEditorOpen,
  ] = useState(false);
  const [collectionWorkspaceTab, setCollectionWorkspaceTab] =
    useState<CollectionWorkspaceTabId>("Details");
  const [collectionRecords, setCollectionRecords] = useState<RecordResponseDto[]>([]);
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
  const [saving, setSaving] = useState(false);
  const [labelsLoading, setLabelsLoading] = useState(false);
  const [availableLabels, setAvailableLabels] = useState<SensitivityLabelsDto[]>([]);
  const [newCollectionLabelCreating, setNewCollectionLabelCreating] =
    useState(false);
  const [tagsLoading, setTagsLoading] = useState(false);
  const [availableTags, setAvailableTags] = useState<TagResponseDto[]>([]);
  const [selectedCollectionLabelSearchTerm, setSelectedCollectionLabelSearchTerm] =
    useState("");
  const [selectedCollectionTagSearchTerm, setSelectedCollectionTagSearchTerm] =
    useState("");
  const [selectedCollectionLabelCreating, setSelectedCollectionLabelCreating] =
    useState(false);
  const [selectedCollectionTagCreating, setSelectedCollectionTagCreating] =
    useState(false);
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

  const dashboard = useCollectionsDashboard({ collections });
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
  const newCollectionDerived = useNewCollectionDerived({
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

  const {
    searchTerm,
    setSearchTerm,
    collectionSort,
    setCollectionSort,
    collectionSortMenuOpen,
    setCollectionSortMenuOpen,
    collectionSortMenuRef,
    filteredCollections,
    sortedCollections,
    activeFacetCount,
    selectedSensitivityFilters,
    toggleSensitivityFilter,
    filteredSensitivityFacetOptions,
    sensitivityFacetQuery,
    setSensitivityFacetQuery,
    selectedTagFilters,
    toggleTagFilter,
    filteredTagFacetOptions,
    tagFacetQuery,
    setTagFacetQuery,
    clearFacetFilters,
    isDashboardLabelsExpanded,
    isDashboardTagsExpanded,
    toggleDashboardLabelsExpanded,
    toggleDashboardTagsExpanded,
    collectionDashboardPage,
    collectionDashboardPageSize,
    visibleSortedCollections,
    setCollectionDashboardPage,
    setCollectionDashboardPageSize,
    collectionDashboardStartIndex,
    collectionDashboardPageCount,
  } = dashboard;

  const { editableSelectedCollection, resetCollectionDetailsView } =
    collectionDetailsView;

  const { addableRecordResults } = selectedCollectionEditDerived;

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
  } = newCollectionDerived;

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

    loadLabels();
  }, [projectId]);

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

    loadTags();
  }, [projectId]);

  const refreshCollections = useCallback(async (preserveSelection = true) => {
    const refreshed = await getAllRecordCollections(organizationId, projectId);
    setCollections(refreshed);
    let refreshedSelection: RecordCollectionResponseDto | null = null;
    if (preserveSelection && selectedCollection) {
      refreshedSelection = refreshed.find(
        (collection) => collection.id === selectedCollection.id,
      ) ?? null;
      setSelectedCollection(refreshedSelection);
      if (isEditingSelectedCollection && refreshedSelection) {
        const baselineSelection = selectedCollection;
        const nextSelection = refreshedSelection;
        setSelectedCollectionDraft((currentDraft) => {
          if (!currentDraft || currentDraft.id !== nextSelection.id) {
            return currentDraft;
          }

          return {
            ...currentDraft,
            lastUpdatedAt: nextSelection.lastUpdatedAt,
            lastUpdatedBy: nextSelection.lastUpdatedBy,
            isArchived: nextSelection.isArchived,
            recordCount: nextSelection.recordCount,
            labels: mergeDraftEntities(
              baselineSelection.labels,
              currentDraft.labels,
              nextSelection.labels,
            ),
            tags: mergeDraftEntities(
              baselineSelection.tags,
              currentDraft.tags,
              nextSelection.tags,
            ),
          };
        });
      }
    }
    return refreshedSelection;
  }, [organizationId, projectId, selectedCollection, isEditingSelectedCollection]);

  useEffect(() => {
    const refreshOnFocus = () => {
      if (document.visibilityState === "visible") {
        refreshCollections().catch((error) => {
          console.error("Failed to refresh record collections:", error);
        });
      }
    };

    window.addEventListener("focus", refreshOnFocus);
    document.addEventListener("visibilitychange", refreshOnFocus);

    return () => {
      window.removeEventListener("focus", refreshOnFocus);
      document.removeEventListener("visibilitychange", refreshOnFocus);
    };
  }, [refreshCollections]);

  const syncSelectedCollectionRecordCount = useCallback(
    (recordCount: number, collectionId = selectedCollection?.id) => {
      setSelectedCollection((current) =>
        current && current.id === collectionId
          ? { ...current, recordCount }
          : current,
      );
      setSelectedCollectionDraft((current) =>
        current && current.id === collectionId
          ? { ...current, recordCount }
          : current,
      );
      setCollections((prev) =>
        prev.map((collection) =>
          collection.id === collectionId
            ? { ...collection, recordCount }
            : collection,
        ),
      );
    },
    [selectedCollection?.id],
  );

  const trackAddedRecords = useCallback(
    (recordIds: number[]) => {
      if (!isEditingSelectedCollection || recordIds.length === 0) return;

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
      if (!isEditingSelectedCollection || recordIds.length === 0) return;

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

  const loadCollectionRecords = async (collection: RecordCollectionResponseDto) => {
    setRecordsLoading(true);
    try {
      const records = await getRecordsInRecordCollection(
        organizationId,
        projectId,
        collection.id,
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
  };

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
    const visibleRecords = visibleNewCollectionRecords;
    const visibleRecordIds = visibleRecords
      .map((record) => record.id)
      .filter((id): id is number => typeof id === "number");
    const allVisibleRecordsSelected =
      visibleRecordIds.length > 0 &&
      visibleRecordIds.every((id) => newCollectionSelectedRecordIds.includes(id));

      if (allVisibleRecordsSelected) {
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

    await addNewCollectionRecords(visibleRecords);
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
      activeTab !== "New Collection" ||
      newCollectionStep !== "Records" ||
      newCollectionRecordSearchTerm.trim() ||
      newCollectionRecordSearchResults.length > 0 ||
      newCollectionRecordSearchLoading
    ) {
      return;
    }

    handleSearchNewCollectionRecords();
  }, [
    activeTab,
    newCollectionStep,
    newCollectionRecordSearchResults.length,
    newCollectionRecordSearchTerm,
    newCollectionRecordSearchLoading,
  ]);

  const handleAddSelectedRecords = async () => {
    if (!selectedCollection || selectedRecordIds.length === 0) return;

    setSaving(true);
    try {
      await addRecordsToRecordCollection(
        organizationId,
        projectId,
        selectedCollection.id,
        selectedRecordIds,
      );
      const records = await loadCollectionRecords(selectedCollection);
      syncSelectedCollectionRecordCount(records.length);
      trackAddedRecords(selectedRecordIds);
      await refreshCollections();
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
    if (!selectedCollection || typeof recordId !== "number") return;

    setAddingRecordIds((prev) => [...prev, recordId]);
    try {
      await addRecordsToRecordCollection(
        organizationId,
        projectId,
        selectedCollection.id,
        [recordId],
      );
      const records = await loadCollectionRecords(selectedCollection);
      syncSelectedCollectionRecordCount(records.length);
      trackAddedRecords([recordId]);
      await refreshCollections();
      toast.success(t.translations.RECORD_COLLECTIONS_RECORD_ADDED);
    } catch (error) {
      console.error("Failed to add record to collection:", error);
      toast.error(t.translations.RECORD_COLLECTIONS_FAILED_UPDATE);
    } finally {
      setAddingRecordIds((prev) => prev.filter((id) => id !== recordId));
    }
  };

  const handleRemoveCollectionRecord = async (recordId?: number | null) => {
    if (!selectedCollection || typeof recordId !== "number") return;

    setRemovingRecordIds((prev) => [...prev, recordId]);
    try {
      await removeRecordsFromRecordCollection(
        organizationId,
        projectId,
        selectedCollection.id,
        [recordId],
      );
      const records = await loadCollectionRecords(selectedCollection);
      syncSelectedCollectionRecordCount(records.length);
      trackRemovedRecords([recordId]);
      await refreshCollections();
      setSelectedRecordIds((prev) => prev.filter((id) => id !== recordId));
      toast.success(t.translations.RECORD_COLLECTIONS_RECORD_REMOVED);
    } catch (error) {
      console.error("Failed to remove record from collection:", error);
      toast.error(t.translations.RECORD_COLLECTIONS_FAILED_UPDATE);
    } finally {
      setRemovingRecordIds((prev) => prev.filter((id) => id !== recordId));
    }
  };

  const openCollection = async (collection: RecordCollectionResponseDto) => {
    setSelectedCollection(collection);
    setIsEditingSelectedCollection(false);
    setSelectedCollectionDraft(null);
    setSelectedCollectionPropertiesEditorOpen(false);
    setSelectedCollectionLabelSearchTerm("");
    setSelectedCollectionTagSearchTerm("");
    resetCollectionDetailsView();
    setCollectionWorkspaceTab("Details");
    setCollectionRecords([]);
    const records = await loadCollectionRecords(collection);
    syncSelectedCollectionRecordCount(records.length, collection.id);
  };

  const openSelectedCollectionEdit = () => {
    if (!selectedCollection) return;
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
    if (!selectedCollection) return;

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

      const refreshedSelection = await refreshCollections();
      if (refreshedSelection) {
        const records = await loadCollectionRecords(refreshedSelection);
        syncSelectedCollectionRecordCount(records.length, refreshedSelection.id);
      }

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

  const viewAllCollectionRecords = () => {
    setCollectionWorkspaceTab("Records");
    requestAnimationFrame(() => {
      window.scrollTo({ top: 0, behavior: "smooth" });
    });
  };

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
      const createdWithRecordCount = {
        ...created,
        recordCount: newCollectionSelectedRecordIds.length,
      };
      setCollections((prev) => [createdWithRecordCount, ...prev]);
      setSelectedCollection(createdWithRecordCount);
      setIsEditingSelectedCollection(false);
      resetCollectionDetailsView();
      setCollectionWorkspaceTab("Details");
      setActiveTab("All Collections");
      setNewCollectionStep("Records");
      setNewCollectionName("");
      setNewCollectionDescription("");
      setNewCollectionSelectedTagNames([]);
      setNewCollectionSelectedLabelIds([]);
      setNewCollectionLabelSearchTerm("");
      setNewCollectionTagSearchTerm("");
      setNewCollectionRecordSearchTerm("");
      setNewCollectionRecordSearchResults([]);
      setNewCollectionRecordPage(1);
      clearNewCollectionSelectedRecords();
      setCollectionRecords([]);
      if (newCollectionSelectedRecordIds.length > 0) {
        await loadCollectionRecords(createdWithRecordCount);
      }
      toast.success(t.translations.RECORD_COLLECTIONS_CREATED);
    } catch (error) {
      console.error("Failed to create record collection:", error);
      toast.error(t.translations.RECORD_COLLECTIONS_FAILED_CREATE);
    } finally {
      setSaving(false);
    }
  };

  const handleSaveSelectedDetails = async () => {
    if (!selectedCollection || !selectedCollectionDraft) return;

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

      const updated = await updateRecordCollection(
        organizationId,
        projectId,
        selectedCollection.id,
        {
          name: selectedCollectionDraft.name,
          description: selectedCollectionDraft.description,
          properties: parseProperties(selectedCollectionDraft.properties),
        },
      );

      await Promise.all([
        ...(selectedCollectionDraft.labels ?? [])
          .filter((label) => !originalLabelIds.has(label.id))
          .map((label) =>
            attachSensitivityLabelToRecordCollection(
              organizationId,
              projectId,
              selectedCollection.id,
              label.id,
            ),
          ),
        ...(selectedCollection.labels ?? [])
          .filter((label) => !draftLabelIds.has(label.id))
          .map((label) =>
            unattachSensitivityLabelFromRecordCollection(
              organizationId,
              projectId,
              selectedCollection.id,
              label.id,
            ),
          ),
        ...(selectedCollectionDraft.tags ?? [])
          .filter((tag) => !originalTagIds.has(tag.id))
          .map((tag) =>
            attachTagToRecordCollection(
              organizationId,
              projectId,
              selectedCollection.id,
              tag.id,
            ),
          ),
        ...(selectedCollection.tags ?? [])
          .filter((tag) => !draftTagIds.has(tag.id))
          .map((tag) =>
            unattachTagFromRecordCollection(
              organizationId,
              projectId,
              selectedCollection.id,
              tag.id,
            ),
          ),
      ]);

      const updatedCollection = {
        ...updated,
        labels: selectedCollectionDraft.labels,
        tags: selectedCollectionDraft.tags,
        recordCount: selectedCollectionDraft.recordCount,
      };
      setSelectedCollection(updatedCollection);
      setSelectedCollectionDraft(null);
      setSelectedCollectionPropertiesEditorOpen(false);
      setCollections((prev) =>
        prev.map((collection) =>
          collection.id === updatedCollection.id ? updatedCollection : collection,
        ),
      );
      setPendingRecordChanges({ added: [], removed: [] });
      resetCollectionDetailsView();
      setIsEditingSelectedCollection(false);
      toast.success(t.translations.RECORD_COLLECTIONS_UPDATE_SUCCESS);
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
      if (!draft) return current;

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
  const newCollectionController = {
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
        setActiveTab("All Collections");
      },
      onGoToModifyStep: goToNewCollectionModifyStep,
      onGoToReviewStep: goToNewCollectionReviewStep,
      onCreateCollection: handleCreateCollection,
    },
  };

  const selectedCollectionRecordsController = selectedCollection
    ? {
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
      }
    : null;

  const selectedCollectionDetailsController =
    useSelectedCollectionDetailsController({
      projectId,
      selectedCollection,
      collectionRecords,
      collectionDetailsView,
      selectedCollectionEditDerived,
      recordsLoading,
      saving,
      isEditingSelectedCollection,
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
      onAddSelectedCollectionLabelFromSearch: addSelectedCollectionLabelFromSearch,
      onAddSelectedCollectionTagFromSearch: addSelectedCollectionTagFromSearch,
      onAddSelectedCollectionLabel: addSelectedCollectionLabel,
      onAddSelectedCollectionTag: addSelectedCollectionTag,
      onRemoveLabel: handleRemoveLabel,
      onRemoveTag: handleRemoveTag,
      recordSearchTerm,
      setRecordSearchTerm,
      recordSearchLoading,
      onSearchRecords: handleSearchRecords,
      addingRecordIds,
      removingRecordIds,
      onRemoveCollectionRecord: handleRemoveCollectionRecord,
      onAddCollectionRecord: handleAddCollectionRecord,
      onCancelSelectedCollectionEdit: cancelSelectedCollectionEdit,
      onSaveSelectedDetails: handleSaveSelectedDetails,
      onViewAllCollectionRecords: viewAllCollectionRecords,
      onOpenSelectedCollectionEdit: openSelectedCollectionEdit,
      getMetadataRows,
      getSensitivityClass,
    });

  const selectedCollectionDetailsTab = selectedCollectionDetailsController ? (
    <SelectedCollectionDetailsTab controller={selectedCollectionDetailsController} />
  ) : null;

  const selectedCollectionRecordsTab = selectedCollectionRecordsController ? (
    <SelectedCollectionRecordsTab controller={selectedCollectionRecordsController} />
  ) : null;

  const topLevelTabs = [
    {
      label: "All Collections",
      displayLabel: t.translations.RECORD_COLLECTIONS_ALL,
      content: (
        <div className="mt-4 space-y-6">
          <div className="grid gap-4 lg:grid-cols-[280px_minmax(0,1fr)] lg:items-start">
            <div className="lg:sticky lg:top-4">
              <FilterSidebar
                selectedSensitivityFilters={selectedSensitivityFilters}
                onToggleSensitivityFilter={toggleSensitivityFilter}
                filteredSensitivityFacetOptions={filteredSensitivityFacetOptions}
                sensitivityFacetQuery={sensitivityFacetQuery}
                onSensitivityFacetQueryChange={setSensitivityFacetQuery}
                selectedTagFilters={selectedTagFilters}
                onToggleTagFilter={toggleTagFilter}
                filteredTagFacetOptions={filteredTagFacetOptions}
                tagFacetQuery={tagFacetQuery}
                onTagFacetQueryChange={setTagFacetQuery}
                activeFacetCount={activeFacetCount}
                onClearFacetFilters={clearFacetFilters}
              />
            </div>

            <SectionCard
              title={t.translations.RECORD_COLLECTIONS_ALL}
              subtitle={t.translations.RECORD_COLLECTIONS_BROWSE_SEARCH_OPEN_PROJECT}
              action={
                <div className="rounded-lg border border-base-300 bg-base-200/50 px-3 py-2 text-sm">
                  <span className="text-base-content/70">
                    {t.translations.RECORD_COLLECTIONS_TOTAL_COLLECTIONS}{" "}
                  </span>
                  <span className="font-semibold text-base-content">
                    {filteredCollections.length}
                  </span>{" "}
                </div>
              }
            >
              <div className="grid gap-3 lg:grid-cols-[minmax(0,1fr)_18rem]">
                <SearchInput
                  className="self-end"
                  placeholder={t.translations.RECORD_COLLECTIONS_FILTER_BY_TITLE_OR_DESCRIPTION}
                  value={searchTerm}
                  onChange={(event) => setSearchTerm(event.target.value)}
                />
                <CollectionSortControl
                  collectionSort={collectionSort}
                  collectionSortMenuOpen={collectionSortMenuOpen}
                  collectionSortMenuRef={collectionSortMenuRef}
                  options={COLLECTION_SORT_OPTIONS}
                  onToggleMenu={() =>
                    setCollectionSortMenuOpen((current) => !current)
                  }
                  onSelectOption={(option) => {
                    setCollectionSort(option);
                    setCollectionSortMenuOpen(false);
                  }}
                  renderLabel={(option) => renderCollectionSortLabel(option, t)}
                />
              </div>

              <div className="grid gap-4">
                {visibleSortedCollections.map((collection) => {
                  const labelsExpanded = isDashboardLabelsExpanded(collection.id);
                  const tagsExpanded = isDashboardTagsExpanded(collection.id);
                  return (
                    <CollectionDashboardCard
                      key={collection.id}
                      collection={collection}
                      labelsExpanded={labelsExpanded}
                      tagsExpanded={tagsExpanded}
                      badgeDisplayLimit={COLLECTION_BADGE_DISPLAY_LIMIT}
                      getSensitivityClass={getSensitivityClass}
                      onToggleLabels={toggleDashboardLabelsExpanded}
                      onToggleTags={toggleDashboardTagsExpanded}
                      onOpenCollection={openCollection}
                    />
                  );
                })}
              </div>
              {sortedCollections.length > collectionDashboardPageSize ? (
                <div className="flex flex-col gap-3 border-t border-base-300 pt-4">
                  <span className="text-sm text-base-content/70">
                    {`${t.translations.SHOWING} ${collectionDashboardStartIndex + 1}-${Math.min(
                      collectionDashboardStartIndex + collectionDashboardPageSize,
                      sortedCollections.length,
                    )} ${t.translations.OF} ${sortedCollections.length}`}
                  </span>
                  <PaginationControls
                    currentPage={collectionDashboardPage}
                    pageSize={collectionDashboardPageSize}
                    totalPages={collectionDashboardPageCount}
                    pageSizeOptions={DEFAULT_PAGE_SIZE_OPTIONS}
                    onPageChange={setCollectionDashboardPage}
                    onPageSizeChange={setCollectionDashboardPageSize}
                  />
                </div>
              ) : null}
            </SectionCard>
          </div>
        </div>
      ),
    },
    {
      label: "New Collection",
      displayLabel: t.translations.RECORD_COLLECTIONS_NEW,
      content: <NewCollectionTabContent controller={newCollectionController} />,
    },
  ];

  return (
    <div className="min-h-screen bg-base-200/30 px-4 py-6 lg:px-8">
      <div className="mx-auto max-w-7xl space-y-5">
        <div className="flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
          <div>
            <p className="text-xs font-semibold uppercase tracking-wide text-base-content/60">
              {t.translations.RECORD_COLLECTIONS}
            </p>
            <h1 className="mt-2 text-3xl font-bold text-base-content">
              {selectedCollection
                ? t.translations.RECORD_COLLECTIONS_COLLECTION_DETAILS
                : t.translations.RECORD_COLLECTIONS_COLLECTION_DASHBOARD}
            </h1>
            <p className="mt-2 max-w-3xl text-sm text-base-content/70">
              {selectedCollection
                ? t.translations.RECORD_COLLECTIONS_REVIEW_AND_MODIFY_DETAILS
                : t.translations.RECORD_COLLECTIONS_BROWSE_CREATE_MODIFY_EXISTING}
            </p>
          </div>
          {selectedCollection ? (
            <button
              type="button"
              className="btn btn-outline btn-sm"
              onClick={() => {
                setSelectedCollection(null);
                setSelectedCollectionDraft(null);
                setIsEditingSelectedCollection(false);
                setSelectedCollectionPropertiesEditorOpen(false);
                resetCollectionDetailsView();
                refreshCollections(false).catch((error) => {
                  console.error("Failed to refresh record collections:", error);
                });
              }}
            >
              <ArrowLeftIcon className="size-4" />
              {t.translations.RECORD_COLLECTIONS_BACK_TO_COLLECTIONS}
            </button>
          ) : null}
        </div>

        {selectedCollection ? (
          <div className="space-y-4">
            {collectionWorkspaceTab === "Details"
              ? selectedCollectionDetailsTab
              : selectedCollectionRecordsTab}
          </div>
        ) : (
          <Tabs
            tabs={topLevelTabs}
            className="w-full"
            activeTab={activeTab}
            onTabChange={(label) => setActiveTab(label as TopLevelTabId)}
          />
        )}
      </div>
      {selectedCollection ? (
        <AdditionalPropertiesEditor
          isOpen={selectedCollectionPropertiesEditorOpen}
          onClose={() => setSelectedCollectionPropertiesEditorOpen(false)}
          properties={parseProperties(editableSelectedCollection?.properties)}
          onSave={handleSaveSelectedCollectionProperties}
          isSaving={saving}
        />
      ) : null}
    </div>
  );
}
