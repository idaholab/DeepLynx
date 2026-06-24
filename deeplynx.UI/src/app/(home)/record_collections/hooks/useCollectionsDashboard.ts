"use client";

import { useLanguage } from "@/app/contexts/Language";
import { getAllRecordCollections } from "@/app/lib/client_service/record_collection_services.client";
import { getAllSensitivityLabelsProject } from "@/app/lib/client_service/sensitivity_labels_services.client";
import { getAllTags } from "@/app/lib/client_service/tag_services.client";
import {
  SensitivityLabelsDto,
  TagResponseDto,
  PaginatedRecordCollectionsResponseDto,
} from "@/app/(home)/types/responseDTOs";
import {
  type ChangeEvent,
  useCallback,
  useDeferredValue,
  useEffect,
  useMemo,
  useRef,
  useState,
} from "react";
import toast from "react-hot-toast";
import {
  COLLECTIONS_DASHBOARD_PER_PAGE,
  COLLECTION_SORT_OPTIONS,
} from "../components/recordCollections.constants";
import { FacetOption, CollectionSortOption } from "../components/recordCollections.types";

type Params = {
  organizationId: number;
  projectId: number;
  initialPage: PaginatedRecordCollectionsResponseDto;
};

function buildFacetOptions<T extends { id: number; name: string }>(
  availableItems: T[],
  getCount: (itemId: number) => number,
) {
  return availableItems
    .map<FacetOption>((item) => ({
      id: item.id,
      label: item.name,
      count: getCount(item.id),
    }))
    .sort((a, b) => a.label.localeCompare(b.label, undefined, { sensitivity: "base" }));
}

export function useCollectionsDashboard({
  organizationId,
  projectId,
  initialPage,
}: Params) {
  const { t } = useLanguage();
  const [searchTerm, setSearchTerm] = useState("");
  const deferredSearchTerm = useDeferredValue(searchTerm);
  const [collectionSort, setCollectionSort] =
    useState<CollectionSortOption>("updatedDesc");
  const [collectionSortMenuOpen, setCollectionSortMenuOpen] = useState(false);
  const [expandedDashboardLabelIds, setExpandedDashboardLabelIds] = useState<number[]>(
    [],
  );
  const [expandedDashboardTagIds, setExpandedDashboardTagIds] = useState<number[]>([]);
  const [selectedSensitivityFilters, setSelectedSensitivityFilters] = useState<number[]>(
    [],
  );
  const [selectedTagFilters, setSelectedTagFilters] = useState<number[]>([]);
  const [sensitivityFacetQuery, setSensitivityFacetQuery] = useState("");
  const [tagFacetQuery, setTagFacetQuery] = useState("");
  const [pageNumber, setPageNumber] = useState(initialPage.pageNumber || 1);
  const [pageSize, setPageSize] = useState(
    initialPage.pageSize || COLLECTIONS_DASHBOARD_PER_PAGE,
  );
  const [pageData, setPageData] = useState<PaginatedRecordCollectionsResponseDto>(
    initialPage,
  );
  const [availableLabels, setAvailableLabels] = useState<SensitivityLabelsDto[]>([]);
  const [availableTags, setAvailableTags] = useState<TagResponseDto[]>([]);
  const [loading, setLoading] = useState(false);
  const collectionSortMenuRef = useRef<HTMLDivElement | null>(null);
  const skipInitialFetch = useRef(true);

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

  useEffect(() => {
    let cancelled = false;

    const loadFacetSources = async () => {
      try {
        const [labels, tags] = await Promise.all([
          getAllSensitivityLabelsProject(projectId),
          getAllTags(projectId),
        ]);

        if (cancelled) return;
        setAvailableLabels(labels);
        setAvailableTags(tags);
      } catch (error) {
        console.error("Failed to load record collection dashboard filters:", error);
        toast.error(t.translations.RECORD_COLLECTIONS_FAILED_LOAD_PROJECT_TAGS);
      }
    };

    void loadFacetSources();

    return () => {
      cancelled = true;
    };
  }, [projectId, t]);

  useEffect(() => {
    if (skipInitialFetch.current) {
      skipInitialFetch.current = false;
      return;
    }

    let cancelled = false;

    const loadCollections = async () => {
      setLoading(true);
      try {
        const nextPage = await getAllRecordCollections(
          organizationId,
          projectId,
          {
            search: deferredSearchTerm.trim() || undefined,
            sensitivityLabelIds:
              selectedSensitivityFilters.length > 0
                ? selectedSensitivityFilters
                : undefined,
            tagIds: selectedTagFilters.length > 0 ? selectedTagFilters : undefined,
            sort: collectionSort,
            pageNumber,
            pageSize,
          },
          true,
        );

        if (cancelled) return;
        setPageData(nextPage);
      } catch (error) {
        console.error("Failed to load record collections:", error);
        toast.error(t.translations.RECORD_COLLECTIONS_FAILED_LOAD_COLLECTIONS);
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    };

    void loadCollections();

    return () => {
      cancelled = true;
    };
  }, [
    collectionSort,
    deferredSearchTerm,
    organizationId,
    pageNumber,
    pageSize,
    projectId,
    selectedSensitivityFilters,
    selectedTagFilters,
    t,
  ]);

  const toggleSensitivityFilter = useCallback((value: number) => {
    setSelectedSensitivityFilters((current) =>
      current.includes(value)
        ? current.filter((filter) => filter !== value)
        : [...current, value],
    );
    setPageNumber(1);
  }, []);

  const toggleTagFilter = useCallback((value: number) => {
    setSelectedTagFilters((current) =>
      current.includes(value)
        ? current.filter((filter) => filter !== value)
        : [...current, value],
    );
    setPageNumber(1);
  }, []);

  const clearFacetFilters = useCallback(() => {
    setSelectedSensitivityFilters([]);
    setSelectedTagFilters([]);
    setSensitivityFacetQuery("");
    setTagFacetQuery("");
    setPageNumber(1);
  }, []);

  const labelCountMap = useMemo(() => {
    const counts = new Map<number, number>();
    const collections = pageData.items ?? [];

    collections.forEach((collection) => {
      collection.labels?.forEach((label) => {
        counts.set(label.id, (counts.get(label.id) ?? 0) + 1);
      });
    });

    return counts;
  }, [pageData.items]);

  const tagCountMap = useMemo(() => {
    const counts = new Map<number, number>();
    const collections = pageData.items ?? [];

    collections.forEach((collection) => {
      collection.tags?.forEach((tag) => {
        counts.set(tag.id, (counts.get(tag.id) ?? 0) + 1);
      });
    });

    return counts;
  }, [pageData.items]);

  const sensitivityFacetOptions = useMemo(
    () =>
      buildFacetOptions(
        availableLabels,
        (labelId) => labelCountMap.get(labelId) ?? 0,
      ),
    [availableLabels, labelCountMap],
  );

  const tagFacetOptions = useMemo(
    () =>
      buildFacetOptions(
        availableTags,
        (tagId) => tagCountMap.get(tagId) ?? 0,
      ),
    [availableTags, tagCountMap],
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

  const activeFacetCount =
    selectedSensitivityFilters.length + selectedTagFilters.length;

  const isDashboardLabelsExpanded = useCallback(
    (collectionId: number) => expandedDashboardLabelIds.includes(collectionId),
    [expandedDashboardLabelIds],
  );

  const isDashboardTagsExpanded = useCallback(
    (collectionId: number) => expandedDashboardTagIds.includes(collectionId),
    [expandedDashboardTagIds],
  );

  const toggleDashboardLabelsExpanded = useCallback((collectionId: number) => {
    setExpandedDashboardLabelIds((current) =>
      current.includes(collectionId)
        ? current.filter((id) => id !== collectionId)
        : [...current, collectionId],
    );
  }, []);

  const toggleDashboardTagsExpanded = useCallback((collectionId: number) => {
    setExpandedDashboardTagIds((current) =>
      current.includes(collectionId)
        ? current.filter((id) => id !== collectionId)
        : [...current, collectionId],
    );
  }, []);

  const totalPages = Math.max(1, Math.ceil(pageData.totalCount / pageSize));
  const startIndex = Math.max(0, (pageNumber - 1) * pageSize);

  return {
    summary: {
      filteredCount: pageData.totalCount,
      isLoading: loading,
    },
    searchInput: {
      value: searchTerm,
      onChange: (event: ChangeEvent<HTMLInputElement>) => {
        setSearchTerm(event.target.value);
        setPageNumber(1);
      },
    },
    sortControl: {
      collectionSort,
      collectionSortMenuOpen,
      collectionSortMenuRef,
      options: COLLECTION_SORT_OPTIONS,
      onToggleMenu: () => setCollectionSortMenuOpen((current) => !current),
      onSelectOption: (option: CollectionSortOption) => {
        setCollectionSort(option);
        setCollectionSortMenuOpen(false);
        setPageNumber(1);
      },
    },
    filterSidebar: {
      selectedSensitivityFilters,
      onToggleSensitivityFilter: toggleSensitivityFilter,
      filteredSensitivityFacetOptions,
      sensitivityFacetQuery,
      onSensitivityFacetQueryChange: setSensitivityFacetQuery,
      selectedTagFilters,
      onToggleTagFilter: toggleTagFilter,
      filteredTagFacetOptions,
      tagFacetQuery,
      onTagFacetQueryChange: setTagFacetQuery,
      activeFacetCount,
      onClearFacetFilters: clearFacetFilters,
    },
    collectionCards: {
      items: pageData.items,
      isLabelsExpanded: isDashboardLabelsExpanded,
      isTagsExpanded: isDashboardTagsExpanded,
      onToggleLabels: toggleDashboardLabelsExpanded,
      onToggleTags: toggleDashboardTagsExpanded,
    },
    pagination: {
      currentPage: pageNumber,
      pageSize,
      totalPages,
      totalItems: pageData.totalCount,
      startIndex,
      onPageChange: setPageNumber,
      onPageSizeChange: (nextPageSize: number) => {
        setPageSize(nextPageSize);
        setPageNumber(1);
      },
    },
  };
}
