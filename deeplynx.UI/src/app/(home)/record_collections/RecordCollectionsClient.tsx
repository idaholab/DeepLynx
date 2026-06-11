"use client";

import PaginationControls, {
  DEFAULT_PAGE_SIZE_OPTIONS,
} from "@/app/(home)/components/PaginationControls";
import Tabs from "@/app/(home)/components/Tabs";
import {
  ArrowRightIcon,
  ArrowLeftIcon,
  MagnifyingGlassIcon,
} from "@heroicons/react/24/outline";
import { useLocalPagination } from "@/app/hooks/useLocalPagination";
import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import toast from "react-hot-toast";
import AdditionalPropertiesEditor from "../record/components/AdditionalPropertiesEditor";
import {
  HistoricalRecordResponseDto,
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
import { formatLocalDateTime } from "@/app/lib/date_time";
import { countFacet, parseRecordTags } from "./components/utils";

type Props = {
  recordCollections: RecordCollectionResponseDto[];
  organizationId: number;
  projectId: number;
};

type TopLevelTabId = "All Collections" | "New Collection";
type CollectionWorkspaceTabId = "Details" | "Records";
type NewCollectionStep = "Records" | "Metadata" | "Modify" | "Review";
type CollectionSortOption =
  | "updatedDesc"
  | "updatedAsc"
  | "alphabeticalAsc"
  | "alphabeticalDesc"
  | "recordCountDesc"
  | "recordCountAsc";

type MetadataRow = {
  label: string;
  value: string;
};

type FacetOption = {
  label: string;
  count: number;
};

type NewCollectionSelectedRecord = HistoricalRecordResponseDto & {
  fullRecord?: RecordResponseDto;
};

type PendingRecordChanges = {
  added: number[];
  removed: number[];
};

const COLLECTION_SORT_OPTIONS: CollectionSortOption[] = [
  "updatedDesc",
  "updatedAsc",
  "alphabeticalAsc",
  "alphabeticalDesc",
  "recordCountDesc",
  "recordCountAsc",
];

function getSelectedRecordLabelNames(record: NewCollectionSelectedRecord) {
  if (record.fullRecord?.labels?.length) {
    return record.fullRecord.labels.map((label) => label.name);
  }
  return parseRecordTags(record.labels);
}

function getSelectedRecordTagNames(record: NewCollectionSelectedRecord) {
  if (record.fullRecord?.tags?.length) {
    return record.fullRecord.tags.map((tag) => tag.name);
  }
  return parseRecordTags(record.tags);
}

const NEW_COLLECTION_RECORDS_PER_PAGE = 6;
const COLLECTIONS_DASHBOARD_PER_PAGE = 6;
const COLLECTION_BADGE_DISPLAY_LIMIT = 10;

function buildAlphabeticalFacetOptions(values: string[]): FacetOption[] {
  return countFacet(values).sort((a, b) =>
    a.label.localeCompare(b.label, undefined, { sensitivity: "base" }),
  );
}

function parseProperties(properties?: string | null): Record<string, unknown> {
  if (!properties) return {};

  try {
    const parsed = JSON.parse(properties);
    return typeof parsed === "object" && parsed !== null ? parsed : {};
  } catch {
    return {};
  }
}

function getMetadataRows(properties?: string | null): MetadataRow[] {
  return Object.entries(parseProperties(properties)).map(([label, value]) => ({
    label,
    value:
      typeof value === "string" || typeof value === "number" || typeof value === "boolean"
        ? String(value)
        : JSON.stringify(value),
  }));
}

function getSensitivity(collection: RecordCollectionResponseDto) {
  return collection.labels?.[0]?.name ?? "Unlabeled";
}

function getSensitivityClass(label: string) {
  const lower = label.toLowerCase();
  if (lower.includes("high")) return "badge-error";
  if (lower.includes("moderate") || lower.includes("medium")) return "badge-warning";
  if (lower.includes("low")) return "badge-success";
  return "badge-outline";
}

function renderCollectionSortLabel(option: CollectionSortOption) {
  switch (option) {
    case "updatedDesc":
      return "Last Updated (Newest)";
    case "updatedAsc":
      return "Last Updated (Oldest)";
    case "alphabeticalAsc":
      return (
        <>
          Alphabetical (A
          <ArrowRightIcon className="size-3" />
          Z)
        </>
      );
    case "alphabeticalDesc":
      return (
        <>
          Alphabetical (Z
          <ArrowRightIcon className="size-3" />
          A)
        </>
      );
    case "recordCountDesc":
      return "# of Records (Highest)";
    case "recordCountAsc":
      return "# of Records (Lowest)";
    default:
      return option;
  }
}

function mergeDraftEntities<T extends { id: number; name: string }>(
  baseline: T[] | undefined,
  draft: T[] | undefined,
  refreshed: T[] | undefined,
): T[] {
  const baselineItems = baseline ?? [];
  const draftItems = draft ?? [];
  const refreshedItems = refreshed ?? [];

  const itemMap = new Map<number, T>();
  [...baselineItems, ...draftItems, ...refreshedItems].forEach((item) => {
    itemMap.set(item.id, item);
  });

  const baselineIds = new Set(baselineItems.map((item) => item.id));
  const draftIds = new Set(draftItems.map((item) => item.id));
  const resultIds = new Set(refreshedItems.map((item) => item.id));

  draftIds.forEach((id) => {
    if (!baselineIds.has(id)) resultIds.add(id);
  });

  baselineIds.forEach((id) => {
    if (!draftIds.has(id)) resultIds.delete(id);
  });

  return Array.from(resultIds)
    .map((id) => itemMap.get(id))
    .filter((item): item is T => Boolean(item))
    .sort((a, b) => a.name.localeCompare(b.name, undefined, { sensitivity: "base" }));
}

/* ─── Component ──────────────────────────────────────────────────────────── */

export default function RecordCollectionsClient({
  recordCollections,
  organizationId,
  projectId,
}: Props) {
  const [collections, setCollections] =
    useState<RecordCollectionResponseDto[]>(recordCollections);
  const [activeTab, setActiveTab] = useState<TopLevelTabId>("All Collections");
  const [searchTerm, setSearchTerm] = useState("");
  const [collectionSort, setCollectionSort] =
    useState<CollectionSortOption>("updatedDesc");
  const [collectionSortMenuOpen, setCollectionSortMenuOpen] = useState(false);
  const [expandedDashboardLabelIds, setExpandedDashboardLabelIds] = useState<
    number[]
  >([]);
  const [expandedDashboardTagIds, setExpandedDashboardTagIds] = useState<
    number[]
  >([]);
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
  const [selectedDescriptionExpandable, setSelectedDescriptionExpandable] =
    useState(false);
  const [selectedDescriptionExpanded, setSelectedDescriptionExpanded] =
    useState(false);
  const [selectedLabelsExpanded, setSelectedLabelsExpanded] = useState(false);
  const [selectedTagsExpanded, setSelectedTagsExpanded] = useState(false);
  const [collectionWorkspaceTab, setCollectionWorkspaceTab] =
    useState<CollectionWorkspaceTabId>("Details");
  const [collectionRecords, setCollectionRecords] = useState<RecordResponseDto[]>([]);
  const [recordSearchTerm, setRecordSearchTerm] = useState("");
  const [recordSearchResults, setRecordSearchResults] = useState<
    HistoricalRecordResponseDto[]
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
  const [collectionDetailRecordSearchTerm, setCollectionDetailRecordSearchTerm] =
    useState("");
  const [collectionDetailRecordPage, setCollectionDetailRecordPage] = useState(1);
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
    useState<HistoricalRecordResponseDto[]>([]);
  const [newCollectionRecordPage, setNewCollectionRecordPage] = useState(1);
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
  const [selectedSensitivityFilters, setSelectedSensitivityFilters] = useState<string[]>([]);
  const [selectedTagFilters, setSelectedTagFilters] = useState<string[]>([]);
  const [sensitivityFacetQuery, setSensitivityFacetQuery] = useState("");
  const [tagFacetQuery, setTagFacetQuery] = useState("");
  const collectionSortMenuRef = useRef<HTMLDivElement | null>(null);
  const selectedDescriptionRef = useRef<HTMLParagraphElement | null>(null);

  useEffect(() => {
    const loadLabels = async () => {
      setLabelsLoading(true);
      try {
        const labels = await getAllSensitivityLabelsProject(projectId);
        setAvailableLabels(labels);
      } catch (error) {
        console.error("Failed to load project labels:", error);
        toast.error("Failed to load project labels");
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
        toast.error("Failed to load project tags");
      } finally {
        setTagsLoading(false);
      }
    };

    loadTags();
  }, [projectId]);

  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (
        collectionSortMenuRef.current &&
        !collectionSortMenuRef.current.contains(event.target as Node)
      ) {
        setCollectionSortMenuOpen(false);
      }
    };

    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

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
      toast.error("Failed to load collection records");
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
      toast.error("Failed to search records");
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
    async (records: HistoricalRecordResponseDto[]) => {
      const unselectedRecords = records.filter(
        (record) => !newCollectionSelectedRecordIds.includes(record.id),
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

  const toggleNewCollectionRecord = async (record: HistoricalRecordResponseDto) => {
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
    const visibleRecordIds = visibleRecords.map((record) => record.id);
    const allVisibleRecordsSelected =
      visibleRecordIds.length > 0 &&
      visibleRecordIds.every((id) => newCollectionSelectedRecordIds.includes(id));

    if (allVisibleRecordsSelected) {
      const visibleIdSet = new Set(visibleRecordIds);
      setNewCollectionSelectedRecordIds((prev) =>
        prev.filter((id) => !visibleIdSet.has(id)),
      );
      setNewCollectionSelectedRecords((prev) =>
        prev.filter((record) => !visibleIdSet.has(record.id)),
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
      remainingRecords.map((record) => record.id),
    );
  };

  const deselectNewCollectionRecordsByTag = (tagName: string) => {
    const remainingRecords = newCollectionSelectedRecords.filter(
      (record) => !getSelectedRecordTagNames(record).includes(tagName),
    );
    setNewCollectionSelectedRecords(remainingRecords);
    setNewCollectionSelectedRecordIds(
      remainingRecords.map((record) => record.id),
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
      toast.success("Label created");
    } catch (error) {
      console.error("Failed to create sensitivity label:", error);
      toast.error("Failed to create sensitivity label");
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
      toast.error("Failed to search records");
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
      toast.success("Records added to collection");
    } catch (error) {
      console.error("Failed to add records to collection:", error);
      toast.error("Failed to add records");
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
      toast.success("Record added to collection");
    } catch (error) {
      console.error("Failed to add record to collection:", error);
      toast.error("Failed to add record to collection");
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
      toast.success("Record removed from collection");
    } catch (error) {
      console.error("Failed to remove record from collection:", error);
      toast.error("Failed to remove record from collection");
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
    setSelectedDescriptionExpanded(false);
    setSelectedLabelsExpanded(false);
    setSelectedTagsExpanded(false);
    setCollectionWorkspaceTab("Details");
    setCollectionRecords([]);
    setCollectionDetailRecordSearchTerm("");
    setCollectionDetailRecordPage(1);
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
      setIsEditingSelectedCollection(false);
    } catch (error) {
      console.error("Failed to cancel record collection edit:", error);
      toast.error("Failed to cancel collection changes");
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
      toast.error("Name and description are required");
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
      setSelectedDescriptionExpanded(false);
      setSelectedLabelsExpanded(false);
      setSelectedTagsExpanded(false);
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
      toast.success("Record collection created");
    } catch (error) {
      console.error("Failed to create record collection:", error);
      toast.error("Failed to create record collection");
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
      setIsEditingSelectedCollection(false);
      toast.success("Record collection updated");
    } catch (error) {
      console.error("Failed to update record collection:", error);
      toast.error("Failed to update record collection");
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
      toast.success("Label created");
    } catch (error) {
      console.error("Failed to create sensitivity label:", error);
      toast.error("Failed to create sensitivity label");
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
      toast.success("Tag created");
    } catch (error) {
      console.error("Failed to create tag:", error);
      toast.error("Failed to create tag");
    } finally {
      setSelectedCollectionTagCreating(false);
    }
  };

  const toggleSensitivityFilter = (value: string) => {
    setSelectedSensitivityFilters((prev) =>
      prev.includes(value)
        ? prev.filter((filter) => filter !== value)
        : [...prev, value],
    );
  };

  const toggleTagFilter = (value: string) => {
    setSelectedTagFilters((prev) =>
      prev.includes(value)
        ? prev.filter((filter) => filter !== value)
        : [...prev, value],
    );
  };

  const clearFacetFilters = () => {
    setSelectedSensitivityFilters([]);
    setSelectedTagFilters([]);
    setSensitivityFacetQuery("");
    setTagFacetQuery("");
  };

  const activeFacetCount =
    selectedSensitivityFilters.length + selectedTagFilters.length;

  const newCollectionSelectedLabelTally = useMemo(
    () =>
      countFacet(
        newCollectionSelectedRecords.flatMap((record) =>
          getSelectedRecordLabelNames(record),
        ),
      ),
    [newCollectionSelectedRecords],
  );

  const newCollectionSelectedTagTally = useMemo(
    () =>
      countFacet(
        newCollectionSelectedRecords.flatMap((record) =>
          getSelectedRecordTagNames(record),
        ),
      ),
    [newCollectionSelectedRecords],
  );

  const selectedNewCollectionLabels = useMemo(
    () =>
      availableLabels.filter((label) =>
        newCollectionSelectedLabelIds.includes(label.id),
      ),
    [availableLabels, newCollectionSelectedLabelIds],
  );

  const filteredNewCollectionLabelOptions = useMemo(() => {
    const query = newCollectionLabelSearchTerm.trim().toLowerCase();
    return availableLabels
      .filter((label) => !newCollectionSelectedLabelIds.includes(label.id))
      .filter((label) => label.name.toLowerCase().includes(query));
  }, [
    availableLabels,
    newCollectionLabelSearchTerm,
    newCollectionSelectedLabelIds,
  ]);

  const filteredNewCollectionTagOptions = useMemo(() => {
    const query = newCollectionTagSearchTerm.trim().toLowerCase();
    return availableTags
      .filter(
        (tag) =>
          !newCollectionSelectedTagNames.some(
            (name) => name.toLowerCase() === tag.name.toLowerCase(),
          ),
      )
      .filter((tag) => tag.name.toLowerCase().includes(query));
  }, [availableTags, newCollectionSelectedTagNames, newCollectionTagSearchTerm]);

  const canAddTypedNewCollectionTag =
    newCollectionTagSearchTerm.trim().length > 0 &&
    !newCollectionSelectedTagNames.some(
      (name) =>
        name.toLowerCase() === newCollectionTagSearchTerm.trim().toLowerCase(),
    );

  const canAddTypedNewCollectionLabel =
    newCollectionLabelSearchTerm.trim().length > 0 &&
    !selectedNewCollectionLabels.some(
      (label) =>
        label.name.toLowerCase() ===
        newCollectionLabelSearchTerm.trim().toLowerCase(),
    );

  const goToNewCollectionModifyStep = () => {
    const selectedRecordLabelIds = new Set<number>();
    const selectedRecordLabelNames = new Set<string>();

    newCollectionSelectedRecords.forEach((record) => {
      record.fullRecord?.labels?.forEach((label) => {
        if (label.id !== null) selectedRecordLabelIds.add(label.id);
        selectedRecordLabelNames.add(label.name.toLowerCase());
      });
    });

    newCollectionSelectedLabelTally.forEach((label) =>
      selectedRecordLabelNames.add(label.label.toLowerCase()),
    );

    availableLabels
      .filter((label) => selectedRecordLabelNames.has(label.name.toLowerCase()))
      .forEach((label) => selectedRecordLabelIds.add(label.id));

    setNewCollectionSelectedLabelIds(Array.from(selectedRecordLabelIds));
    setNewCollectionSelectedTagNames(
      newCollectionSelectedTagTally.map((tag) => tag.label),
    );
    setNewCollectionStep("Modify");
  };

  const goToNewCollectionReviewStep = () => {
    setNewCollectionReviewSearchTerm("");
    setNewCollectionReviewPage(1);
    setNewCollectionStep("Review");
  };

  const filteredCollections = useMemo(() => {
    const query = searchTerm.trim().toLowerCase();

    return collections.filter((collection) => {
      const collectionLabelNames =
        collection.labels?.map((label) => label.name) ?? [];
      const collectionTagNames = collection.tags?.map((tag) => tag.name) ?? [];
      const matchesSensitivity =
        selectedSensitivityFilters.length === 0 ||
        selectedSensitivityFilters.every((filter) =>
          collectionLabelNames.includes(filter),
        );
      const matchesTags =
        selectedTagFilters.length === 0 ||
        selectedTagFilters.every((filter) => collectionTagNames.includes(filter));

      if (!matchesSensitivity || !matchesTags) return false;
      if (!query) return true;

      const haystack = [
        collection.name,
        collection.description,
        getSensitivity(collection),
        collectionTagNames.join(" "),
        collectionLabelNames.join(" "),
      ]
        .join(" ")
        .toLowerCase();

      return haystack.includes(query);
    });
  }, [
    collections,
    searchTerm,
    selectedSensitivityFilters,
    selectedTagFilters,
  ]);

  const sortedCollections = useMemo(() => {
    return [...filteredCollections].sort((a, b) => {
      const alphabeticalComparison = a.name.localeCompare(b.name, undefined, {
        sensitivity: "base",
      });
      const updatedComparison =
        new Date(a.lastUpdatedAt).getTime() -
        new Date(b.lastUpdatedAt).getTime();
      const recordCountComparison = a.recordCount - b.recordCount;

      switch (collectionSort) {
        case "alphabeticalAsc":
          return alphabeticalComparison;
        case "alphabeticalDesc":
          return alphabeticalComparison * -1;
        case "recordCountAsc":
          return recordCountComparison || alphabeticalComparison;
        case "recordCountDesc":
          return recordCountComparison * -1 || alphabeticalComparison;
        case "updatedAsc":
          return updatedComparison || alphabeticalComparison;
        case "updatedDesc":
        default:
          return updatedComparison * -1 || alphabeticalComparison;
      }
    });
  }, [collectionSort, filteredCollections]);

  const {
    currentPage: collectionDashboardPage,
    pageSize: collectionDashboardPageSize,
    paginatedItems: visibleSortedCollections,
    resetPagination: resetCollectionDashboardPagination,
    setCurrentPage: setCollectionDashboardPage,
    setPageSize: setCollectionDashboardPageSize,
    startIndex: collectionDashboardStartIndex,
    totalPages: collectionDashboardPageCount,
  } = useLocalPagination({
    items: sortedCollections,
    initialPageSize: COLLECTIONS_DASHBOARD_PER_PAGE,
  });

  useEffect(() => {
    resetCollectionDashboardPagination();
  }, [searchTerm, selectedSensitivityFilters, selectedTagFilters, collectionSort]);

  useEffect(() => {
    const measureDescriptionOverflow = () => {
      const selectedDescriptionElement = selectedDescriptionRef.current;
      const nextSelectedExpandable = Boolean(
        selectedDescriptionElement &&
        selectedDescriptionElement.scrollHeight - selectedDescriptionElement.clientHeight > 1,
      );
      setSelectedDescriptionExpandable((current) =>
        current === nextSelectedExpandable ? current : nextSelectedExpandable,
      );
    };

    const frameId = window.requestAnimationFrame(measureDescriptionOverflow);
    window.addEventListener("resize", measureDescriptionOverflow);

    return () => {
      window.cancelAnimationFrame(frameId);
      window.removeEventListener("resize", measureDescriptionOverflow);
    };
  }, [selectedCollection?.id, selectedCollection?.description]);

  const sensitivityFacetOptions = useMemo(
    () =>
      buildAlphabeticalFacetOptions(
        filteredCollections.flatMap((collection) =>
          collection.labels?.map((label) => label.name) ?? [],
        ),
      ),
    [filteredCollections],
  );

  const tagFacetOptions = useMemo(
    () =>
      buildAlphabeticalFacetOptions(
        filteredCollections.flatMap((collection) =>
          collection.tags?.map((tag) => tag.name) ?? [],
        ),
      ),
    [filteredCollections],
  );

  const filteredSensitivityFacetOptions = useMemo(() => {
    const query = sensitivityFacetQuery.trim().toLowerCase();
    return sensitivityFacetOptions.filter((option) =>
      option.label.toLowerCase().includes(query),
    );
  }, [sensitivityFacetOptions, sensitivityFacetQuery]);

  const filteredTagFacetOptions = useMemo(() => {
    const query = tagFacetQuery.trim().toLowerCase();
    return tagFacetOptions.filter((option) =>
      option.label.toLowerCase().includes(query),
    );
  }, [tagFacetOptions, tagFacetQuery]);

  const unattachedLabels = useMemo(() => {
    const attachedIds = new Set(
      selectedCollectionDraft?.labels?.map((label) => label.id) ?? [],
    );
    return availableLabels.filter((label) => !attachedIds.has(label.id));
  }, [availableLabels, selectedCollectionDraft?.labels]);

  const unattachedTags = useMemo(() => {
    const attachedIds = new Set(
      selectedCollectionDraft?.tags?.map((tag) => tag.id) ?? [],
    );
    return availableTags.filter((tag) => !attachedIds.has(tag.id));
  }, [availableTags, selectedCollectionDraft?.tags]);

  const filteredSelectedCollectionLabelOptions = useMemo(() => {
    const query = selectedCollectionLabelSearchTerm.trim().toLowerCase();
    return unattachedLabels.filter((label) =>
      label.name.toLowerCase().includes(query),
    );
  }, [selectedCollectionLabelSearchTerm, unattachedLabels]);

  const filteredSelectedCollectionTagOptions = useMemo(() => {
    const query = selectedCollectionTagSearchTerm.trim().toLowerCase();
    return unattachedTags.filter((tag) =>
      tag.name.toLowerCase().includes(query),
    );
  }, [selectedCollectionTagSearchTerm, unattachedTags]);

  const canAddTypedSelectedCollectionLabel =
    selectedCollectionLabelSearchTerm.trim().length > 0 &&
    !(selectedCollectionDraft?.labels ?? []).some(
      (label) =>
        label.name.toLowerCase() ===
        selectedCollectionLabelSearchTerm.trim().toLowerCase(),
    );

  const canAddTypedSelectedCollectionTag =
    selectedCollectionTagSearchTerm.trim().length > 0 &&
    !(selectedCollectionDraft?.tags ?? []).some(
      (tag) =>
        tag.name.toLowerCase() ===
        selectedCollectionTagSearchTerm.trim().toLowerCase(),
    );

  const addableRecordResults = useMemo(() => {
    const existingIds = new Set(
      collectionRecords
        .map((record) => record.id)
        .filter((id): id is number => typeof id === "number"),
    );
    return recordSearchResults.filter((record) => !existingIds.has(record.id));
  }, [collectionRecords, recordSearchResults]);

  const collectionRecordIds = useMemo(
    () =>
      new Set(
        collectionRecords
          .map((record) => record.id)
          .filter((id): id is number => typeof id === "number"),
      ),
    [collectionRecords],
  );

  const editRecordResults = useMemo(
    () =>
      recordSearchTerm.trim().length
        ? recordSearchResults
        : collectionRecords,
    [collectionRecords, recordSearchResults, recordSearchTerm],
  );

  const newCollectionRecordPageCount = Math.max(
    1,
    Math.ceil(
      newCollectionRecordSearchResults.length / NEW_COLLECTION_RECORDS_PER_PAGE,
    ),
  );

  const visibleNewCollectionRecords = useMemo(() => {
    const startIndex =
      (newCollectionRecordPage - 1) * NEW_COLLECTION_RECORDS_PER_PAGE;
    return newCollectionRecordSearchResults.slice(
      startIndex,
      startIndex + NEW_COLLECTION_RECORDS_PER_PAGE,
    );
  }, [newCollectionRecordPage, newCollectionRecordSearchResults]);

  const visibleNewCollectionRecordIds = useMemo(
    () => visibleNewCollectionRecords.map((record) => record.id),
    [visibleNewCollectionRecords],
  );

  const allVisibleNewCollectionRecordsSelected =
    visibleNewCollectionRecordIds.length > 0 &&
    visibleNewCollectionRecordIds.every((id) =>
      newCollectionSelectedRecordIds.includes(id),
    );

  const allRetrievedNewCollectionRecordsSelected =
    newCollectionRecordSearchResults.length > 0 &&
    newCollectionRecordSearchResults.every((record) =>
      newCollectionSelectedRecordIds.includes(record.id),
    );

  const someVisibleNewCollectionRecordsSelected =
    visibleNewCollectionRecordIds.some((id) =>
      newCollectionSelectedRecordIds.includes(id),
    );

  const filteredNewCollectionSelectedRecords = useMemo(() => {
    const query = newCollectionReviewSearchTerm.trim().toLowerCase();
    if (!query) return newCollectionSelectedRecords;

    return newCollectionSelectedRecords.filter((record) => {
      const haystack = [
        record.name,
        record.description,
        record.className,
        record.dataSourceName,
        record.projectName,
        getSelectedRecordLabelNames(record).join(" "),
        getSelectedRecordTagNames(record).join(" "),
      ]
        .join(" ")
        .toLowerCase();

      return haystack.includes(query);
    });
  }, [newCollectionReviewSearchTerm, newCollectionSelectedRecords]);

  const newCollectionReviewPageCount = Math.max(
    1,
    Math.ceil(
      filteredNewCollectionSelectedRecords.length /
      NEW_COLLECTION_RECORDS_PER_PAGE,
    ),
  );

  const visibleNewCollectionReviewRecords = useMemo(() => {
    const startIndex =
      (newCollectionReviewPage - 1) * NEW_COLLECTION_RECORDS_PER_PAGE;
    return filteredNewCollectionSelectedRecords.slice(
      startIndex,
      startIndex + NEW_COLLECTION_RECORDS_PER_PAGE,
    );
  }, [filteredNewCollectionSelectedRecords, newCollectionReviewPage]);

  useEffect(() => {
    setNewCollectionReviewPage(1);
  }, [newCollectionReviewSearchTerm, newCollectionSelectedRecords.length]);

  const filteredCollectionDetailRecords = useMemo(() => {
    const query = collectionDetailRecordSearchTerm.trim().toLowerCase();
    if (!query) return collectionRecords;

    return collectionRecords.filter((record) => {
      const haystack = [
        record.name,
        record.description,
        record.uri,
        record.classId,
        record.dataSourceId,
        record.projectId,
        record.lastUpdatedBy,
      ]
        .join(" ")
        .toLowerCase();

      return haystack.includes(query);
    });
  }, [collectionDetailRecordSearchTerm, collectionRecords]);

  const collectionDetailRecordPageCount = Math.max(
    1,
    Math.ceil(
      filteredCollectionDetailRecords.length / NEW_COLLECTION_RECORDS_PER_PAGE,
    ),
  );

  const visibleCollectionDetailRecords = useMemo(() => {
    const startIndex =
      (collectionDetailRecordPage - 1) * NEW_COLLECTION_RECORDS_PER_PAGE;
    return filteredCollectionDetailRecords.slice(
      startIndex,
      startIndex + NEW_COLLECTION_RECORDS_PER_PAGE,
    );
  }, [collectionDetailRecordPage, filteredCollectionDetailRecords]);

  useEffect(() => {
    setCollectionDetailRecordPage(1);
  }, [collectionDetailRecordSearchTerm, collectionRecords.length]);

  const editableSelectedCollection = selectedCollectionDraft ?? selectedCollection;
  const selectedCollectionLabels = selectedCollection?.labels ?? [];
  const selectedCollectionTags = selectedCollection?.tags ?? [];
  const visibleSelectedCollectionLabels = selectedLabelsExpanded
    ? selectedCollectionLabels
    : selectedCollectionLabels.slice(0, COLLECTION_BADGE_DISPLAY_LIMIT);
  const visibleSelectedCollectionTags = selectedTagsExpanded
    ? selectedCollectionTags
    : selectedCollectionTags.slice(0, COLLECTION_BADGE_DISPLAY_LIMIT);
  const collectionSummaryPanel = selectedCollection ? (
    <div className="grid gap-4 rounded-2xl border border-base-300 bg-base-200/30 p-4 text-sm sm:grid-cols-2 lg:grid-cols-4">
      <div>
        <p className="text-base-content/60">Collection ID</p>
        <p className="font-semibold text-base-content">{selectedCollection.id}</p>
      </div>
      <div>
        <p className="text-base-content/60">Total Records</p>
        <p className="font-semibold text-base-content">
          {selectedCollection.recordCount}
        </p>
      </div>
      <div>
        <p className="text-base-content/60">Updated</p>
        <p className="font-semibold text-base-content">
          {formatLocalDateTime(selectedCollection.lastUpdatedAt)}
        </p>
      </div>
      <div>
        <p className="text-base-content/60">Last Updated By</p>
        <p className="font-semibold text-base-content">
          {selectedCollection.lastUpdatedBy ?? "Unknown"}
        </p>
      </div>
    </div>
  ) : null;

  const newCollectionController = {
    workflow: {
      projectId,
      newCollectionStep,
      setNewCollectionStep,
      recordsPerPage: NEW_COLLECTION_RECORDS_PER_PAGE,
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
    selectedCollection && editableSelectedCollection
      ? {
          readonlyView: {
            selectedCollection,
            collectionSummaryPanel,
            selectedDescriptionRef,
            selectedDescriptionExpanded,
            selectedDescriptionExpandable,
            setSelectedDescriptionExpanded,
            selectedCollectionLabels,
            visibleSelectedCollectionLabels,
            selectedLabelsExpanded,
            setSelectedLabelsExpanded,
            selectedCollectionTags,
            visibleSelectedCollectionTags,
            selectedTagsExpanded,
            setSelectedTagsExpanded,
            badgeDisplayLimit: COLLECTION_BADGE_DISPLAY_LIMIT,
            getMetadataRows,
            getSensitivityClass,
            collectionRecords,
            filteredCollectionDetailRecords,
            collectionDetailRecordSearchTerm,
            setCollectionDetailRecordSearchTerm,
            visibleCollectionDetailRecords,
            recordsLoading,
            onViewAllCollectionRecords: viewAllCollectionRecords,
            recordsPerPage: NEW_COLLECTION_RECORDS_PER_PAGE,
            collectionDetailRecordPage,
            setCollectionDetailRecordPage,
            collectionDetailRecordPageCount,
            projectId,
            onOpenSelectedCollectionEdit: openSelectedCollectionEdit,
          },
          editView: {
            editableSelectedCollection,
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
            canAddTypedSelectedCollectionLabel,
            canAddTypedSelectedCollectionTag,
            filteredSelectedCollectionLabelOptions,
            filteredSelectedCollectionTagOptions,
            labelsLoading,
            tagsLoading,
            onAddSelectedCollectionLabelFromSearch:
              addSelectedCollectionLabelFromSearch,
            onAddSelectedCollectionTagFromSearch:
              addSelectedCollectionTagFromSearch,
            onAddSelectedCollectionLabel: addSelectedCollectionLabel,
            onAddSelectedCollectionTag: addSelectedCollectionTag,
            onRemoveLabel: handleRemoveLabel,
            onRemoveTag: handleRemoveTag,
            recordSearchTerm,
            setRecordSearchTerm,
            recordSearchLoading,
            onSearchRecords: handleSearchRecords,
            editRecordResults,
            collectionRecordIds,
            addingRecordIds,
            removingRecordIds,
            onRemoveCollectionRecord: handleRemoveCollectionRecord,
            onAddCollectionRecord: handleAddCollectionRecord,
            onCancelSelectedCollectionEdit: cancelSelectedCollectionEdit,
            onSaveSelectedDetails: handleSaveSelectedDetails,
          },
        }
      : null;

  const selectedCollectionDetailsTab = selectedCollectionDetailsController ? (
    <SelectedCollectionDetailsTab controller={selectedCollectionDetailsController} />
  ) : null;

  const selectedCollectionRecordsTab = selectedCollectionRecordsController ? (
    <SelectedCollectionRecordsTab controller={selectedCollectionRecordsController} />
  ) : null;

  const topLevelTabs = [
    {
      label: "All Collections",
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
              title="All Collections"
              subtitle="Browse, search, and open record collections for this project."
              action={
                <div className="rounded-lg border border-base-300 bg-base-200/50 px-3 py-2 text-sm">
                  <span className="text-base-content/70">Total Collections: </span>
                  <span className="font-semibold text-base-content">
                    {filteredCollections.length}
                  </span>{" "}
                </div>
              }
            >
              <div className="grid gap-3 lg:grid-cols-[minmax(0,1fr)_18rem]">
                <label className="input input-bordered flex w-full items-center gap-2 self-end">
                  <MagnifyingGlassIcon className="size-5 text-base-content/60" />
                  <input
                    type="text"
                    className="grow"
                    placeholder="Search by collection title or description..."
                    value={searchTerm}
                    onChange={(event) => setSearchTerm(event.target.value)}
                  />
                </label>
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
                  renderLabel={renderCollectionSortLabel}
                />
              </div>

              <div className="grid gap-4">
                {visibleSortedCollections.map((collection) => {
                  const labelsExpanded = expandedDashboardLabelIds.includes(
                    collection.id,
                  );
                  const tagsExpanded = expandedDashboardTagIds.includes(
                    collection.id,
                  );
                  return (
                    <CollectionDashboardCard
                      key={collection.id}
                      collection={collection}
                      labelsExpanded={labelsExpanded}
                      tagsExpanded={tagsExpanded}
                      badgeDisplayLimit={COLLECTION_BADGE_DISPLAY_LIMIT}
                      getSensitivityClass={getSensitivityClass}
                      onToggleLabels={(collectionId) =>
                        setExpandedDashboardLabelIds((current) =>
                          labelsExpanded
                            ? current.filter((id) => id !== collectionId)
                            : [...current, collectionId],
                        )
                      }
                      onToggleTags={(collectionId) =>
                        setExpandedDashboardTagIds((current) =>
                          tagsExpanded
                            ? current.filter((id) => id !== collectionId)
                            : [...current, collectionId],
                        )
                      }
                      onOpenCollection={openCollection}
                    />
                  );
                })}
              </div>
              {sortedCollections.length > COLLECTIONS_DASHBOARD_PER_PAGE ? (
                <div className="flex flex-col gap-3 border-t border-base-300 pt-4">
                  <span className="text-sm text-base-content/70">
                    Showing {collectionDashboardStartIndex + 1}-
                    {Math.min(
                      collectionDashboardStartIndex + collectionDashboardPageSize,
                      sortedCollections.length,
                    )}{" "}
                    of {sortedCollections.length}
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
      content: <NewCollectionTabContent controller={newCollectionController} />,
    },
  ];

  return (
    <div className="min-h-screen bg-base-200/30 px-4 py-6 lg:px-8">
      <div className="mx-auto max-w-7xl space-y-5">
        <div className="flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
          <div>
            <p className="text-xs font-semibold uppercase tracking-wide text-base-content/60">
              Record Collections
            </p>
            <h1 className="mt-2 text-3xl font-bold text-base-content">
              {selectedCollection ? "Collection Details" : "Collection Dashboard"}
            </h1>
            <p className="mt-2 max-w-3xl text-sm text-base-content/70">
              {selectedCollection ? "Review and modify collection details." : "Browse collections, create new collections, and modify existing collections."}
            </p>
          </div>
          {selectedCollection ? (
            <button
              type="button"
              className="btn btn-outline btn-sm"
              onClick={() => {
                setSelectedCollection(null);
                setIsEditingSelectedCollection(false);
                setSelectedCollectionPropertiesEditorOpen(false);
                refreshCollections(false).catch((error) => {
                  console.error("Failed to refresh record collections:", error);
                });
              }}
            >
              <ArrowLeftIcon className="size-4" />
              Back to collections
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
