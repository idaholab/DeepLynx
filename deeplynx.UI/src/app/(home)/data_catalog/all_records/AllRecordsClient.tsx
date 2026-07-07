"use client";

/**
 * AllRecordsClient — Data Catalog "All Records" page
 *
 * This is the primary client component for the cross-project record browser.
 * It is responsible for:
 *  1. Keeping project/search/filter/page state in sync with the UI.
 *  2. Sending that state to the advanced paginated records endpoint.
 *  3. Rendering the single page of records returned by the API.
 *
 * Record filtering and pagination are intentionally server-side. tableData is
 * only the current API page, not a full in-memory catalog result set.
 */

import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";

import SearchBar from "@/app/(home)/components/SearchBar";
import { RecordTableRow } from "@/app/(home)/types/types";
import { useProjectSession } from "@/app/contexts/ProjectSessionProvider";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import { queryBuilderPaginated } from "@/app/lib/client_service/query_services.client";
import { getAllTagsOrg } from "@/app/lib/client_service/tag_services.client";
import { QueryRecordViewResponseDto } from "@/app/(home)/types/responseDTOs";
import ProjectDropdown from "@/app/(home)/components/ProjectDropdown";
import { useLanguage } from "@/app/contexts/Language";
import {
  ArchiveBoxIcon,
  ChevronLeftIcon,
  ChevronRightIcon,
  DocumentTextIcon,
} from "@heroicons/react/24/outline";

import ActiveFiltersBar from "./components/ActiveFiltersBar";
import FilterSidebar, { RecordStatusFilter } from "./components/FilterSidebar";
import RecordCard from "./components/RecordCard";
import ManageTagsCard from "./components/ManageTagsCard";
import { parseRecordTags } from "./components/utils";

import {
  bulkAttachTagsToRecords,
  bulkUnattachTagsFromRecords,
} from "@/app/lib/client_service/record_services.client";
import Skeleton from "react-loading-skeleton";
import { CustomQueryRequestDto } from "../../types/requestDTOs";
import { getAllClassesOrg } from "@/app/lib/client_service/class_services.client";

type BuildRecordQueryFiltersParams = {
  statusFilter: RecordStatusFilter;
  selectedClassFilters: string[];
  selectedTagFilters: string[];
  selectedUpdatedByFilters: string[];
};

function appendQueryGroup(
  filters: CustomQueryRequestDto[],
  filterName: string,
  values: string[],
  operator: string = "=",
) {
  values
    .map((value) => value.trim())
    .filter(Boolean)
    .forEach((value, index) => {
      filters.push({
        connector: filters.length === 0 ? "" : index === 0 ? "AND" : "OR",
        filter: filterName,
        operator,
        value,
      });
    });
}

function buildRecordQueryFilters({
  statusFilter,
  selectedClassFilters,
  selectedTagFilters,
  selectedUpdatedByFilters,
}: BuildRecordQueryFiltersParams): CustomQueryRequestDto[] {
  const filters: CustomQueryRequestDto[] = [];

  if (statusFilter !== "all") {
    filters.push({
      connector: "",
      filter: "is_archived",
      operator: "=",
      value: statusFilter === "archived" ? "true" : "false",
    });
  }

  appendQueryGroup(filters, "class_name", selectedClassFilters);
  appendQueryGroup(filters, "tags", selectedTagFilters);
  appendQueryGroup(filters, "last_updated_by", selectedUpdatedByFilters);

  return filters;
}
/* ─── Types ──────────────────────────────────────────────────────────────── */

type Props = {
  /** All projects the user has access to in this organisation. */
  initialProjects: { id: string; name: string }[];
  /** Project IDs pre-selected from the URL query string on the server. */
  initialSelectedProjects: string[];
  /** Search term pre-populated from the URL query string on the server. */
  initialSearchTerm: string;
};

/**
 * Represents how a tag applies across the currently selected records.
 *
 * checked - Every selected record already has the tag, or the tag is pending attach
 * unchecked - No selected records have the tag, or the tag is pending unattach
 * indeterminate - Only some selected records currently have the tag
 */
type BulkTagState = "checked" | "unchecked" | "indeterminate";

/**
 * Tag returned for the current project scope.
 *
 * projectId is null for organization-level tags.
 * projectId is set when the tag belongs to a specific project.
 */
type AvailableTag = {
  id: number;
  name: string;
  projectId: number | null;
};

/** Number of records shown per page in the paginated list. */
const RECORDS_PER_PAGE = 12;

/** Maximum number of class or tag facet options shown in the sidebar before truncation. */
const FACET_LIMIT = 8;

/* ─── Component ──────────────────────────────────────────────────────────── */

export default function DataCatalogClient({
  initialProjects,
  initialSelectedProjects,
  initialSearchTerm,
}: Props) {
  const { t } = useLanguage();
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();

  const { hasLoaded } = useProjectSession();
  const { organization } = useOrganizationSession();

  // Projects list is fixed for the lifetime of this page; no setter needed.
  const [projects] = useState(initialProjects);
  const [selectedProjects, setSelectedProjects] = useState<string[]>(
    initialSelectedProjects,
  );

  // tableData is the current page returned by the server-side paginated query.
  const [tableData, setTableData] = useState<RecordTableRow[]>([]);

  // searchTerm is the live value of the search input field.
  const [searchTerm, setSearchTerm] = useState(initialSearchTerm ?? "");

  // loading determines if the records are loading
  const [loading, setLoading] = useState(false);

  // render helper for loading skeleton
  const times = (n: number) => Array.from({ length: n }, (_, i) => i);

  /**
   * activeFilters is the list of submitted search terms that are currently
   * active. Each filter has a stable numeric id so it can be removed by id
   * without relying on array index or string equality.
   */
  const [activeFilters, setActiveFilters] = useState<
    Array<{ id: number; term: string }>
  >([]);
  const [nextFilterId, setNextFilterId] = useState(1);

  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize] = useState(RECORDS_PER_PAGE);
  const [totalCount, setTotalCount] = useState(0);
  const [serverTotalPages, setServerTotalPages] = useState(1);
  const [hasPreviousPage, setHasPreviousPage] = useState(false);
  const [hasNextPage, setHasNextPage] = useState(false);

  // Facet filter state — all local, no API calls.
  const [selectedClassFilters, setSelectedClassFilters] = useState<string[]>(
    [],
  );
  const [selectedTagFilters, setSelectedTagFilters] = useState<string[]>([]);
  const [selectedUpdatedByFilters, setSelectedUpdatedByFilters] = useState<
    string[]
  >([]);
  const [statusFilter, setStatusFilter] = useState<RecordStatusFilter>("all");

  // Local search queries inside the facet sidebar (filter the list of options,
  // not the records themselves).
  const [classFacetQuery, setClassFacetQuery] = useState("");
  const [tagFacetQuery, setTagFacetQuery] = useState("");

  /**
   * Bulk tag management state.
   *
   * selectedRecordKeys stores selected records as `${projectId}-${recordId}`, so selections remain unique.
   *
   * tagsToAttach and tagsToUnattach store pending tag IDs only. Actual API call happens when the user clicks apply.
   */
  const [selectedRecordKeys, setSelectedRecordKeys] = useState<string[]>([]);
  const [tagsToAttach, setTagsToAttach] = useState<number[]>([]);
  const [tagsToUnattach, setTagsToUnattach] = useState<number[]>([]);
  const [bulkTagQuery, setBulkTagQuery] = useState("");
  const [isApplyingBulkTags, setIsApplyingBulkTags] = useState(false);
  const [isBulkMode, setIsBulkMode] = useState(false);
  const [availableTags, setAvailableTags] = useState<AvailableTag[]>([]);
  const [availableClassNames, setAvailableClassNames] = useState<string[]>([]);

  /**
   * Guard ref that prevents the initial URL search term from being re-submitted
   * every time the component re-renders. We use a ref (not state) so that
   * storing the applied term does not itself trigger a re-render.
   */
  const initialSearchAppliedRef = useRef<string | null>(null);
  const [isInitialSearchPending, setIsInitialSearchPending] =
    useState(!!initialSearchTerm);
  // Pre-compute lowercase search terms once for use in getHighlightedContent
  // across all record cards, avoiding repeated toLowerCase calls per field.
  const activeSearchTerms = useMemo(
    () => activeFilters.map((f) => f.term.toLowerCase()),
    [activeFilters],
  );

  /**
   * Join selected project IDs into a single string token for use as a
   * useEffect dependency. Arrays fail referential equality even when their
   * contents haven't changed, so using the raw array would trigger the effect
   * on every render. The joined string is stable across renders when the
   * selection hasn't actually changed.
   */
  const selectedProjectsToken = useMemo(
    () => selectedProjects.join("|"),
    [selectedProjects],
  );

  const submittedSearchText = useMemo(
    () =>
      activeFilters
        .map((filter) => filter.term)
        .join(" ")
        .trim(),
    [activeFilters],
  );

  const recordsQueryToken = useMemo(
    () =>
      JSON.stringify({
        selectedProjectsToken,
        submittedSearchText,
        selectedClassFilters,
        selectedTagFilters,
        selectedUpdatedByFilters,
        statusFilter,
      }),
    [
      selectedProjectsToken,
      submittedSearchText,
      selectedClassFilters,
      selectedTagFilters,
      selectedUpdatedByFilters,
      statusFilter,
    ],
  );

  /**
   * Builds a unique selection key for records across multiple projects.
   */
  const getRecordKey = useCallback((record: RecordTableRow) => {
    return `${record.projectId}-${record.id}`;
  }, []);

  /**
   * Resolve the effective project IDs to fetch records for.
   * ProjectDropdown emits "ALL" as a sentinel when the user selects all
   * projects, and an empty array when nothing is explicitly chosen — both
   * cases should be treated as "fetch from all projects".
   */
  const effectiveProjectIds = useMemo(() => {
    const allIds = projects.map((p) => String(p.id));
    if (
      selectedProjects.length === 0 ||
      selectedProjects.includes("ALL") ||
      selectedProjects.length === projects.length
    ) {
      return allIds;
    }
    return selectedProjects.map(String);
  }, [projects, selectedProjects]);

  const transformRecord = useCallback(
    (record: QueryRecordViewResponseDto): RecordTableRow => ({
      ...record,
      id: record.id || 0,
      name: record.name,
      description: record.description ?? undefined,
      uri: record.uri ?? undefined,
      properties:
        typeof record.properties === "string"
          ? record.properties
          : JSON.stringify(record.properties),
      originalId: record.originalId ?? undefined,
      classId: record.classId ?? undefined,
      className: record.className ?? undefined,
      dataSourceId: record.dataSourceId ?? undefined,
      dataSourceName: record.dataSourceName ?? "",
      projectId: record.projectId ?? undefined,
      projectName:
        record.projectName ??
        projects.find((p) => Number(p.id) === record.projectId)?.name ??
        "",
      tags:
        typeof record.tags === "string"
          ? record.tags
          : JSON.stringify(record.tags),
      labels:
        typeof record.labels === "string"
          ? record.labels
          : JSON.stringify(record.labels),
      lastUpdatedAt: record.lastUpdatedAt || "",
      lastUpdatedBy: record.lastUpdatedBy ?? undefined,
      isArchived: record.isArchived || false,
      fileType: record.fileType ?? "",
      fileSize: record.fileSize ?? undefined,
      archivedAt: record.isArchived ? (record.lastUpdatedAt ?? null) : null,
      timeseries: undefined,
      select: false,
      associatedRecords: undefined,
    }),
    [projects],
  );

  const requestIdRef = useRef(0);

  /**
   * Fetches a single page using server-side project, text search, facet filter,
   * and pagination state.
   */
  const fetchRecordsPage = useCallback(
    async (pageNumber = currentPage) => {
      if (!organization?.organizationId) return;

      const requestId = requestIdRef.current + 1;
      requestIdRef.current = requestId;
      setLoading(true);

      const idsNum = effectiveProjectIds.map(Number).filter(Number.isFinite);
      if (idsNum.length === 0) {
        setTableData([]);
        setTotalCount(0);
        setServerTotalPages(1);
        setHasPreviousPage(false);
        setHasNextPage(false);
        setLoading(false);
        return;
      }

      const queryFilters = buildRecordQueryFilters({
        statusFilter,
        selectedClassFilters,
        selectedTagFilters,
        selectedUpdatedByFilters,
      });

      try {
        const result = await queryBuilderPaginated(
          Number(organization.organizationId),
          queryFilters,
          idsNum,
          pageNumber,
          pageSize,
          submittedSearchText || null,
        );

        if (requestId !== requestIdRef.current) return;

        setTableData(result.items.map(transformRecord));
        setCurrentPage(result.pageNumber);
        setTotalCount(result.totalCount);
        setServerTotalPages(Math.max(1, result.totalPages));
        setHasPreviousPage(result.hasPrevious);
        setHasNextPage(result.hasNext);
      } catch (error) {
        if (requestId !== requestIdRef.current) return;

        console.error("Failed to fetch records page:", error);
        setTableData([]);
        setTotalCount(0);
        setServerTotalPages(1);
        setHasPreviousPage(false);
        setHasNextPage(false);
      } finally {
        if (requestId === requestIdRef.current) {
          setLoading(false);
        }
      }
    },
    [
      currentPage,
      effectiveProjectIds,
      organization?.organizationId,
      pageSize,
      selectedClassFilters,
      selectedTagFilters,
      selectedUpdatedByFilters,
      statusFilter,
      submittedSearchText,
      transformRecord,
    ],
  );

  /**
   * Removes the `search` query parameter from the URL so that a page refresh
   * or back-navigation does not re-apply a search term the user has cleared.
   */
  const clearSearchQueryFromUrl = useCallback(() => {
    const params = new URLSearchParams(searchParams?.toString());
    params.delete("search");
    const nextUrl = params.toString() ? `${pathname}?${params}` : pathname;
    router.replace(nextUrl);
  }, [pathname, router, searchParams]);

  /** Clears all search filters and re-fetches the full browse result set. */
  const clearAllFilters = useCallback(() => {
    setActiveFilters([]);
    setSearchTerm("");
    setCurrentPage(1);
    clearSearchQueryFromUrl();
  }, [clearSearchQueryFromUrl]);

  /**
   * Removes a single search filter by id, re-runs the search with the
   * remaining terms, and clears the URL search param if no terms are left.
   */
  const handleRemoveFilter = useCallback(
    (id: number) => {
      const nextFilters = activeFilters.filter((f) => f.id !== id);

      setActiveFilters(nextFilters);

      if (nextFilters.length === 0) {
        setSearchTerm("");
        clearSearchQueryFromUrl();
      }
      setCurrentPage(1);
    },
    [activeFilters, clearSearchQueryFromUrl],
  );

  /**
   * Adds a new search term to activeFilters and fires the combined search.
   * Duplicate terms (case-insensitive) are silently ignored to avoid
   * redundant API calls and confusing duplicate badges in the UI.
   */
  const handleSearch = useCallback(
    async (value: string) => {
      const trimmed = value.trim();
      if (
        !trimmed ||
        activeFilters.some(
          (filter) => filter.term.toLowerCase() === trimmed.toLowerCase(),
        )
      ) {
        return;
      }

      const newFilter = { id: nextFilterId, term: trimmed };
      const nextFilters = [...activeFilters, newFilter];

      setActiveFilters(nextFilters);
      setNextFilterId((n) => n + 1);
      setSearchTerm("");
      setCurrentPage(1);
    },
    [activeFilters, nextFilterId],
  );

  /**
   * If the page was loaded with a `?search=` URL parameter (e.g. via a link
   * from another page), run that search once after the session is ready.
   *
   * The ref guard prevents this from re-firing if the component re-renders
   * while hasLoaded or initialSearchTerm are stable.
   */
  useEffect(() => {
    if (!hasLoaded) return;
    if (!initialSearchTerm) {
      setIsInitialSearchPending(false);
      return;
    }
    if (initialSearchAppliedRef.current === initialSearchTerm) return;

    initialSearchAppliedRef.current = initialSearchTerm;
    handleSearch(initialSearchTerm).finally(() => {
      setIsInitialSearchPending(false);
    });
  }, [hasLoaded, initialSearchTerm, handleSearch]);

  const lastRecordsQueryTokenRef = useRef(recordsQueryToken);

  /** Fetch records whenever server-side query state changes. */
  useEffect(() => {
    if (!hasLoaded) return;
    if (isInitialSearchPending) return;

    if (lastRecordsQueryTokenRef.current !== recordsQueryToken) {
      lastRecordsQueryTokenRef.current = recordsQueryToken;
      if (currentPage !== 1) {
        setCurrentPage(1);
        return;
      }
    }

    fetchRecordsPage().catch((e: unknown) => {
      console.error("Fetch records page failed:", e);
    });
  }, [
    hasLoaded,
    currentPage,
    recordsQueryToken,
    fetchRecordsPage,
    isInitialSearchPending,
  ]);

  /** Fetches class/tag metadata available to the current project scope. */
  useEffect(() => {
    if (!hasLoaded) return;
    if (!organization?.organizationId) return;

    const fetchMetadataForProjectScope = async () => {
      const organizationId = Number(organization.organizationId);
      const projectIds = effectiveProjectIds
        .map(Number)
        .filter(Number.isFinite);

      const [classes, tags] = await Promise.all([
        getAllClassesOrg(organizationId, projectIds, true),
        getAllTagsOrg(organizationId, projectIds, true),
      ]);

      setAvailableClassNames(
        Array.from(new Set(classes.map((item) => item.name).filter(Boolean))),
      );

      setAvailableTags(
        tags.map((tag) => ({
          id: tag.id,
          name: tag.name,
          projectId: tag.projectId ?? null,
        })),
      );
    };

    fetchMetadataForProjectScope().catch((error) => {
      console.error("Failed to fetch available record metadata:", error);
    });
  }, [hasLoaded, organization?.organizationId, selectedProjectsToken]);

  /** Bridge between the SearchBar's onSubmit callback shape and handleSearch. */
  const handleSubmit = useCallback(
    async ({ query }: { query: string }) => {
      try {
        await handleSearch(query);
      } catch (error) {
        console.error("Failed to send query", error);
      }
    },
    [handleSearch],
  );

  /* ─── Derived data ──────────────────────────────────────────────────────── */

  const classFacetOptions = useMemo(
    () => availableClassNames.map((label) => ({ label })),
    [availableClassNames],
  );

  const tagFacetOptions = useMemo(
    () => availableTags.map((tag) => ({ label: tag.name })),
    [availableTags],
  );

  // Filter and cap the facet option lists based on the sidebar search queries.
  const filteredClassFacetOptions = useMemo(
    () =>
      classFacetOptions
        .filter((option) =>
          option.label.toLowerCase().includes(classFacetQuery.toLowerCase()),
        )
        .slice(0, FACET_LIMIT),
    [classFacetOptions, classFacetQuery],
  );

  const filteredTagFacetOptions = useMemo(
    () =>
      tagFacetOptions
        .filter((option) =>
          option.label.toLowerCase().includes(tagFacetQuery.toLowerCase()),
        )
        .slice(0, FACET_LIMIT),
    [tagFacetOptions, tagFacetQuery],
  );

  /* ─── Pagination ────────────────────────────────────────────────────────── */

  const totalPages = serverTotalPages;
  const currentRecords = tableData;
  const selectedRecords = useMemo(() => {
    const selected = new Set(selectedRecordKeys);

    return tableData.filter((record) => selected.has(getRecordKey(record)));
  }, [getRecordKey, tableData, selectedRecordKeys]);
  const visibleAvailableTags = useMemo(() => {
    if (effectiveProjectIds.length === 1) {
      const projectId = Number(effectiveProjectIds[0]);

      return availableTags.filter(
        (tag) => tag.projectId === null || tag.projectId === projectId,
      );
    }
    return availableTags.filter((tag) => tag.projectId === null);
  }, [availableTags, effectiveProjectIds]);
  const selectedRecordCount = selectedRecords.length;
  const hasPendingBulkTagChanges =
    tagsToAttach.length > 0 || tagsToUnattach.length > 0;
  const pageStart = totalCount === 0 ? 0 : (currentPage - 1) * pageSize + 1;
  const pageEnd = Math.min(currentPage * pageSize, totalCount);

  // Total number of active facet selections, used to show/hide the "Clear" button.
  const activeFacetCount =
    selectedClassFilters.length +
    selectedTagFilters.length +
    selectedUpdatedByFilters.length +
    (statusFilter === "all" ? 0 : 1);

  /* ─── Filter toggle handlers ────────────────────────────────────────────── */

  const toggleClassFilter = useCallback((value: string) => {
    setSelectedClassFilters((prev) =>
      prev.includes(value)
        ? prev.filter((item) => item !== value)
        : [...prev, value],
    );
  }, []);

  const toggleTagFilter = useCallback((value: string) => {
    setSelectedTagFilters((prev) =>
      prev.includes(value)
        ? prev.filter((item) => item !== value)
        : [...prev, value],
    );
  }, []);

  const toggleRecordSelection = useCallback(
    (record: RecordTableRow) => {
      const recordKey = getRecordKey(record);

      setSelectedRecordKeys((prev) =>
        prev.includes(recordKey)
          ? prev.filter((item) => item !== recordKey)
          : [...prev, recordKey],
      );
    },
    [getRecordKey],
  );

  /**
   * Calculates the visual checkbox state for a tag across selected records.
   *
   * Checked tags are marked for unattach. Unchecked or indeterminate tags are
   * marked for attach so all selected records will receive the tag on Apply.
   */
  const getBulkTagState = useCallback(
    (tag: AvailableTag): BulkTagState => {
      if (!tag || selectedRecords.length === 0) {
        return "unchecked";
      }

      if (tagsToAttach.includes(tag.id)) {
        return "checked";
      }

      if (tagsToUnattach.includes(tag.id)) {
        return "unchecked";
      }

      const matchingCount = selectedRecords.filter((record) => {
        const tags = parseRecordTags(record.tags);

        return tags.includes(tag.name);
      }).length;

      if (matchingCount === 0) {
        return "unchecked";
      }

      if (matchingCount === selectedRecords.length) {
        return "checked";
      }

      return "indeterminate";
    },
    [selectedRecords, tagsToAttach, tagsToUnattach],
  );

  /**
   * Tracks pending bulk tag changes.
   *
   * Pending attach/unattach changes are checked first so the UI immediately
   * reflects what will happen on apply, even before the records are refreshed.
   */
  const toggleBulkTag = useCallback(
    (tag: AvailableTag) => {
      const state = getBulkTagState(tag);

      if (state === "checked") {
        setTagsToAttach((prev) => prev.filter((id) => id !== tag.id));

        setTagsToUnattach((prev) =>
          prev.includes(tag.id) ? prev : [...prev, tag.id],
        );
      } else {
        setTagsToUnattach((prev) => prev.filter((id) => id !== tag.id));

        setTagsToAttach((prev) =>
          prev.includes(tag.id) ? prev : [...prev, tag.id],
        );
      }
    },
    [getBulkTagState],
  );

  const handleCancelBulkTags = useCallback(() => {
    setIsBulkMode(false);
    setSelectedRecordKeys([]);
    setTagsToAttach([]);
    setTagsToUnattach([]);
    setBulkTagQuery("");
  }, []);

  const handleApplyBulkTags = useCallback(async () => {
    if (selectedRecords.length === 0) return;
    if (!hasPendingBulkTagChanges) return;
    if (!organization?.organizationId) return;

    setIsApplyingBulkTags(true);

    const organizationId = Number(organization.organizationId);

    try {
      const recordsByProject = new Map<number, RecordTableRow[]>();

      // Bulk record-tag APIs are project-scoped, so selected records must be grouped by projectId.
      selectedRecords.forEach((record) => {
        if (record.projectId === undefined) return;

        const projectId = Number(record.projectId);
        const records = recordsByProject.get(projectId) ?? [];

        records.push(record);
        recordsByProject.set(projectId, records);
      });

      await Promise.all(
        Array.from(recordsByProject.entries()).flatMap(
          ([projectId, records]) => {
            const requests: Promise<unknown>[] = [];

            // API expects one record/tag pair per operation, using keys.
            const attachDtos = records.flatMap((record) =>
              tagsToAttach.map((tagId) => ({
                record_id: Number(record.id),
                tag_id: tagId,
              })),
            );

            const unattachDtos = records.flatMap((record) =>
              tagsToUnattach.map((tagId) => ({
                record_id: Number(record.id),
                tag_id: tagId,
              })),
            );

            if (attachDtos.length > 0) {
              requests.push(
                bulkAttachTagsToRecords(organizationId, projectId, attachDtos),
              );
            }

            if (unattachDtos.length > 0) {
              requests.push(
                bulkUnattachTagsFromRecords(
                  organizationId,
                  projectId,
                  unattachDtos,
                ),
              );
            }

            return requests;
          },
        ),
      );

      await fetchRecordsPage();

      // Only clear selection and staged changes after every API request succeeds.
      handleCancelBulkTags();
    } catch (error) {
      console.error("Failed to apply bulk tag changes:", error);
    } finally {
      setIsApplyingBulkTags(false);
    }
  }, [
    fetchRecordsPage,
    handleCancelBulkTags,
    hasPendingBulkTagChanges,
    organization?.organizationId,
    selectedRecords,
    tagsToAttach,
    tagsToUnattach,
  ]);

  /** Resets all facet filters and clears both sidebar search inputs. */
  const clearFacetFilters = useCallback(() => {
    setSelectedClassFilters([]);
    setSelectedTagFilters([]);
    setSelectedUpdatedByFilters([]);
    setStatusFilter("all");
    setClassFacetQuery("");
    setTagFacetQuery("");
  }, []);

  // Clamp currentPage if totalPages shrinks below it (edge case when filters
  // reduce the result set to fewer pages than the user was on).
  useEffect(() => {
    if (currentPage > totalPages) setCurrentPage(totalPages);
  }, [currentPage, totalPages]);

  // Do not render until the project session is ready — avoids a flash of
  // incomplete data or a double-fetch on mount.
  if (!hasLoaded) return null;

  /* ─── Render ────────────────────────────────────────────────────────────── */

  return (
    <main className="min-h-screen bg-base-200/30">
      {/* ── Page header: title, project dropdown, search bar ──────────────── */}
      <section className="border-b border-base-300/50 bg-base-100">
        <div className="mx-auto flex w-full max-w-7xl flex-col gap-5 px-3 py-5 sm:px-6 lg:px-8">
          <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
            <div className="space-y-3">
              <div>
                <p className="text-xs font-semibold uppercase tracking-wide text-base-content/60">
                  {t.translations.DATA_CATALOG}
                </p>
                <h1 className="text-2xl font-bold text-base-content sm:text-3xl">
                  {t.translations.ALL_RECORDS}
                </h1>
              </div>
              <ProjectDropdown
                projects={projects}
                onSelectionChange={setSelectedProjects}
                defaultSelected={
                  initialSelectedProjects.length
                    ? initialSelectedProjects
                    : undefined
                }
                disabled={isBulkMode}
              />
            </div>

            <SearchBar
              placeholder={t.translations.SEARCH}
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              onSubmit={handleSubmit}
              activeFilters={activeFilters}
              onRemoveFilter={handleRemoveFilter}
              onClearAll={clearAllFilters}
              resultCount={totalCount}
              showResultsMessage={activeFilters.length > 0}
              className="w-full lg:max-w-xl"
            />
          </div>
        </div>
      </section>

      {/* ── Main content ──────────────────────────────────────────────────── */}
      <section className="mx-auto flex w-full max-w-7xl flex-col gap-4 px-3 py-5 sm:px-6 lg:px-8">
        {/* Result count and action badges */}
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div className="text-sm text-base-content/70">
            {pageStart}-{pageEnd} {t.translations.OF} {totalCount}
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <button
              type="button"
              disabled={isApplyingBulkTags}
              className="cursor-pointer text-sm font-semibold underline underline-offset-2 hover:text-primary disabled:cursor-not-allowed disabled:opacity-50"
              onClick={() => {
                if (isBulkMode) {
                  handleCancelBulkTags();
                } else {
                  setIsBulkMode(true);
                }
              }}
            >
              {isBulkMode ? "Cancel Selection" : "Select Records"}
            </button>
            {isBulkMode && (
              <button
                type="button"
                className="btn btn-sm btn-primary"
                onClick={handleApplyBulkTags}
                disabled={
                  selectedRecordCount === 0 ||
                  !hasPendingBulkTagChanges ||
                  isApplyingBulkTags
                }
              >
                {isApplyingBulkTags ? "Applying..." : "Apply Changes"}
              </button>
            )}
            {activeFilters.length > 0 && (
              <button
                type="button"
                onClick={clearAllFilters}
                className="btn btn-sm btn-ghost"
              >
                {t.translations.CLEAR_SEARCH}
              </button>
            )}
            {statusFilter === "archived" && totalCount > 0 && (
              <span className="badge badge-warning badge-outline">
                <ArchiveBoxIcon className="size-3" />
                {totalCount}
              </span>
            )}
          </div>
        </div>

        {/* Active search term pills — hidden when no terms are active */}
        <ActiveFiltersBar
          activeFilters={activeFilters}
          onRemoveFilter={handleRemoveFilter}
        />

        {/* Two-column layout: sidebar on left, record list on right */}
        <div className="grid grid-cols-1 gap-5 lg:grid-cols-[18rem_minmax(0,1fr)]">
          <div className="space-y-4 lg:sticky lg:top-4 lg:self-start">
            <FilterSidebar
              statusFilter={statusFilter}
              onStatusFilterChange={setStatusFilter}
              selectedClassFilters={selectedClassFilters}
              onToggleClassFilter={toggleClassFilter}
              filteredClassFacetOptions={filteredClassFacetOptions}
              classFacetQuery={classFacetQuery}
              onClassFacetQueryChange={setClassFacetQuery}
              selectedTagFilters={selectedTagFilters}
              onToggleTagFilter={toggleTagFilter}
              filteredTagFacetOptions={filteredTagFacetOptions}
              tagFacetQuery={tagFacetQuery}
              onTagFacetQueryChange={setTagFacetQuery}
              activeFacetCount={activeFacetCount}
              onClearFacetFilters={clearFacetFilters}
            />

            {isBulkMode && (
              <ManageTagsCard
                selectedRecordCount={selectedRecordCount}
                bulkTagQuery={bulkTagQuery}
                onBulkTagQueryChange={setBulkTagQuery}
                availableTags={visibleAvailableTags}
                getBulkTagState={getBulkTagState}
                onToggleBulkTag={toggleBulkTag}
                showProjectScopeNotice={effectiveProjectIds.length > 1}
              />
            )}
          </div>

          {/* Record list */}
          <div className="min-w-0">
            {loading === true ? (
              <div className="card border border-base-300/50 bg-base-100 shadow-sm p-1">
                <ul className="list mt-0">
                  {times(5).map((i) => (
                    <li
                      key={i}
                      className="border-b border-base-200 hover:bg-base-200/30 p-2 pl-0 rounded-sm"
                    >
                      <div className="text-accent-content mb-1">
                        <Skeleton width="55%" />
                      </div>
                      <div className="text-sm text-base-300 space-x-2 flex flex-wrap items-center">
                        <span>
                          {t.translations.CLASS}{" "}
                          <span className="badge badge-info badge-sm text-xs">
                            <Skeleton width={60} />
                          </span>
                        </span>
                        <span className="ml-4">
                          {t.translations.LAST_EDIT} <Skeleton width={80} />
                        </span>
                        <span className="ml-4">
                          {t.translations.PROJECT} <Skeleton width={120} />
                        </span>
                        <span className="ml-4">
                          {t.translations.DATA_SOURCE} <Skeleton width={100} />
                        </span>
                      </div>
                    </li>
                  ))}
                </ul>
              </div>
            ) : currentRecords.length === 0 ? (
              <div className="card border border-base-300/50 bg-base-100 shadow-sm">
                <div className="card-body items-center py-16 text-center">
                  <DocumentTextIcon className="size-12 text-base-content/30" />
                  <h2 className="card-title">
                    {t.translations.NO_RECORDS_FOUND}
                  </h2>
                  <p className="max-w-md text-sm text-base-content/60">
                    {t.translations.NO_RECORDS}
                  </p>
                </div>
              </div>
            ) : (
              <div className="divide-y divide-base-200 overflow-hidden rounded-box border border-base-300/50 bg-base-100 shadow-sm">
                {currentRecords.map((record) => (
                  <RecordCard
                    key={`${record.projectId}-${record.id}`}
                    record={record}
                    activeSearchTerms={activeSearchTerms}
                    isBulkMode={isBulkMode}
                    isSelected={selectedRecordKeys.includes(
                      getRecordKey(record),
                    )}
                    onToggleSelected={toggleRecordSelection}
                  />
                ))}
              </div>
            )}
          </div>
        </div>

        {/* Pagination — only rendered when the result set spans more than one page */}
        {totalPages > 1 && (
          <div className="join justify-end">
            <button
              className="btn join-item btn-sm"
              disabled={!hasPreviousPage}
              onClick={() => setCurrentPage((prev) => Math.max(1, prev - 1))}
            >
              <ChevronLeftIcon className="size-4" />
            </button>
            <button className="btn join-item btn-sm pointer-events-none">
              {t.translations.PAGE} {currentPage} {t.translations.OF}{" "}
              {totalPages}
            </button>
            <button
              className="btn join-item btn-sm"
              disabled={!hasNextPage}
              onClick={() =>
                setCurrentPage((prev) => Math.min(totalPages, prev + 1))
              }
            >
              <ChevronRightIcon className="size-4" />
            </button>
          </div>
        )}
      </section>
    </main>
  );
}
