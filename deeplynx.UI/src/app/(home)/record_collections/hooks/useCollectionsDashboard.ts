"use client";

import { useLocalPagination } from "@/app/hooks/useLocalPagination";
import {
  type ChangeEvent,
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from "react";
import { RecordCollectionResponseDto } from "../../types/responseDTOs";
import {
  buildAlphabeticalFacetOptions,
  getSensitivity,
} from "../components/recordCollections.utils";
import {
  COLLECTIONS_DASHBOARD_PER_PAGE,
  COLLECTION_SORT_OPTIONS,
} from "../components/recordCollections.constants";
import { CollectionSortOption } from "../components/recordCollections.types";

type Params = {
  collections: RecordCollectionResponseDto[];
};

export function useCollectionsDashboard({ collections }: Params) {
  const [searchTerm, setSearchTerm] = useState("");
  const [collectionSort, setCollectionSort] =
    useState<CollectionSortOption>("updatedDesc");
  const [collectionSortMenuOpen, setCollectionSortMenuOpen] = useState(false);
  const [expandedDashboardLabelIds, setExpandedDashboardLabelIds] = useState<
    number[]
  >([]);
  const [expandedDashboardTagIds, setExpandedDashboardTagIds] = useState<number[]>([]);
  const [selectedSensitivityFilters, setSelectedSensitivityFilters] = useState<string[]>(
    [],
  );
  const [selectedTagFilters, setSelectedTagFilters] = useState<string[]>([]);
  const [sensitivityFacetQuery, setSensitivityFacetQuery] = useState("");
  const [tagFacetQuery, setTagFacetQuery] = useState("");
  const collectionSortMenuRef = useRef<HTMLDivElement | null>(null);

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

  const toggleSensitivityFilter = useCallback((value: string) => {
    setSelectedSensitivityFilters((current) =>
      current.includes(value)
        ? current.filter((filter) => filter !== value)
        : [...current, value],
    );
  }, []);

  const toggleTagFilter = useCallback((value: string) => {
    setSelectedTagFilters((current) =>
      current.includes(value)
        ? current.filter((filter) => filter !== value)
        : [...current, value],
    );
  }, []);

  const clearFacetFilters = useCallback(() => {
    setSelectedSensitivityFilters([]);
    setSelectedTagFilters([]);
    setSensitivityFacetQuery("");
    setTagFacetQuery("");
  }, []);

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
  }, [collections, searchTerm, selectedSensitivityFilters, selectedTagFilters]);

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
    currentPage,
    pageSize,
    paginatedItems,
    resetPagination,
    setCurrentPage,
    setPageSize,
    startIndex,
    totalPages,
  } = useLocalPagination({
    items: sortedCollections,
    initialPageSize: COLLECTIONS_DASHBOARD_PER_PAGE,
  });

  useEffect(() => {
    resetPagination();
  }, [
    collectionSort,
    resetPagination,
    searchTerm,
    selectedSensitivityFilters,
    selectedTagFilters,
  ]);

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

  return {
    summary: {
      filteredCount: filteredCollections.length,
    },
    searchInput: {
      value: searchTerm,
      onChange: (event: ChangeEvent<HTMLInputElement>) =>
        setSearchTerm(event.target.value),
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
      items: paginatedItems,
      isLabelsExpanded: isDashboardLabelsExpanded,
      isTagsExpanded: isDashboardTagsExpanded,
      onToggleLabels: toggleDashboardLabelsExpanded,
      onToggleTags: toggleDashboardTagsExpanded,
    },
    pagination: {
      currentPage,
      pageSize,
      totalPages,
      totalItems: sortedCollections.length,
      startIndex,
      onPageChange: setCurrentPage,
      onPageSizeChange: setPageSize,
    },
  };
}
