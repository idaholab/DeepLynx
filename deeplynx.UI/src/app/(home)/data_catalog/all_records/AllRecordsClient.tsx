"use client";

/**
 * AllRecordsClient — Data Catalog "All Records" page
 *
 * This is the primary client component for the cross-project record browser.
 * It is responsible for:
 *  1. Fetching records from the API when the user changes the project selection.
 *  2. Running full-text search when the user enters search terms.
 *  3. Applying local facet filters (status, class, tags) on top of the fetched
 *     data without triggering additional API calls.
 *  4. Paginating the filtered result set client-side.
 *
 * Data flow overview:
 *   API fetch (project change) → tableData
 *   Full-text search           → tableData  (replaces browse results)
 *   Project scope filter       → projectScopedRecords  (subset of tableData)
 *   Facet filters              → scopedRecords          (subset of projectScopedRecords)
 *   Pagination                 → currentRecords         (one page of scopedRecords)
 *
 * The two-stage scoping (projectScopedRecords then scopedRecords) exists
 * because search results arrive pre-scoped from the API while browse results
 * may contain records for all projects and need to be re-filtered when the
 * dropdown selection changes client-side.
 */

import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";

import SearchBar from "@/app/(home)/components/SearchBar";
import { RecordTableRow } from "@/app/(home)/types/types";
import { useProjectSession } from "@/app/contexts/ProjectSessionProvider";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import {
  fullTextSearch,
  getMultiProjectRecords,
} from "@/app/lib/client_service/query_services.client";
import { getAllTagsOrg } from "@/app/lib/client_service/tag_services.client";
import { HistoricalRecordResponseDto } from "@/app/(home)/types/responseDTOs";
import ProjectDropdown from "@/app/(home)/components/ProjectDropdown";
import { useLanguage } from "@/app/contexts/Language";
import {
  ArchiveBoxIcon,
  ChevronLeftIcon,
  ChevronRightIcon,
  DocumentTextIcon,
} from "@heroicons/react/24/outline";

import ActiveFiltersBar from "./components/ActiveFiltersBar";
import FilterSidebar, {
  RecordStatusFilter,
} from "./components/FilterSidebar";
import RecordCard from "./components/RecordCard";
import ManageTagsCard from "./components/ManageTagsCard";
import { countFacet, parseRecordTags } from "./components/utils";

/* ─── Types ──────────────────────────────────────────────────────────────── */

type Props = {
  /** All projects the user has access to in this organisation. */
  initialProjects: { id: string; name: string }[];
  /** Project IDs pre-selected from the URL query string on the server. */
  initialSelectedProjects: string[];
  /** Search term pre-populated from the URL query string on the server. */
  initialSearchTerm: string;
  /** Records fetched server-side for the initial render to avoid a loading flash. */
  initialRecords: RecordTableRow[];
};

type BulkTagState = "checked" | "unchecked" | "indeterminate";

/** Number of records shown per page in the paginated list. */
const RECORDS_PER_PAGE = 12;

/** Maximum number of class or tag facet options shown in the sidebar before truncation. */
const FACET_LIMIT = 8;

/* ─── Component ──────────────────────────────────────────────────────────── */

export default function DataCatalogClient({
  initialProjects,
  initialSelectedProjects,
  initialSearchTerm,
  initialRecords,
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

  // tableData is the raw result from the most recent API call (browse or search).
  const [tableData, setTableData] = useState<RecordTableRow[]>(
    initialRecords ?? [],
  );

  // searchTerm is the live value of the search input field.
  const [searchTerm, setSearchTerm] = useState(initialSearchTerm ?? "");

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

  // Facet filter state — all local, no API calls.
  const [selectedClassFilters, setSelectedClassFilters] = useState<string[]>([]);
  const [selectedTagFilters, setSelectedTagFilters] = useState<string[]>([]);
  const [selectedUpdatedByFilters, setSelectedUpdatedByFilters] = useState<string[]>([]);
  const [statusFilter, setStatusFilter] = useState<RecordStatusFilter>("all");

  // Local search queries inside the facet sidebar (filter the list of options,
  // not the records themselves).
  const [classFacetQuery, setClassFacetQuery] = useState("");
  const [tagFacetQuery, setTagFacetQuery] = useState("");
  
  // Bulk tag management state
  const [selectedRecordKeys, setSelectedRecordKeys] = useState<string[]>([]);
  const [tagsToAttach, setTagsToAttach] = useState<string[]>([]);
  const [tagsToUnattach, setTagsToUnattach] = useState<string[]>([]);
  const [bulkTagQuery, setBulkTagQuery] = useState("");
  const [isApplyingBulkTags, setIsApplyingBulkTags] = useState(false);
  const [isBulkMode, setIsBulkMode] = useState(false);
  const [availableTags, setAvailableTags] = useState<{ id: number; name: string }[]>([]);
  
  /**
   * Guard ref that prevents the initial URL search term from being re-submitted
   * every time the component re-renders. We use a ref (not state) so that
   * storing the applied term does not itself trigger a re-render.
   */
  const initialSearchAppliedRef = useRef<string | null>(null);

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

  /**
   * Fetches all records for the currently selected projects and stores them
   * in tableData. Used for browse mode (no active search terms). Does not
   * accept a search term — search results come from runSearchTerms instead.
   */
  const fetchRecordsForSelection = useCallback(async () => {
    const idsNum = effectiveProjectIds.map(Number).filter(Number.isFinite);
    if (idsNum.length === 0) {
      setTableData([]);
      return;
    }
    const data = await getMultiProjectRecords(
      organization?.organizationId as number,
      idsNum,
      true,
    );
    const transformedData: RecordTableRow[] = data.map((record) => ({
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
      dataSourceName: "",
      projectId: record.projectId ?? undefined,
      // Resolve project name locally from the projects list since the API
      // does not include it in the list endpoint response.
      projectName:
        projects.find((p) => Number(p.id) === record.projectId)?.name || "",
      tags:
        typeof record.tags === "string"
          ? record.tags
          : JSON.stringify(record.tags),
      lastUpdatedAt: record.lastUpdatedAt || "",
      lastUpdatedBy: record.lastUpdatedBy ?? undefined,
      isArchived: record.isArchived || false,
      fileType: "",
    }));
    setTableData(transformedData);
  }, [effectiveProjectIds, organization?.organizationId, projects]);

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
    clearSearchQueryFromUrl();

    fetchRecordsForSelection().catch((e: unknown) => {
      console.error("Clear all fetch failed:", e);
    });
  }, [clearSearchQueryFromUrl, fetchRecordsForSelection]);

  /**
   * Executes a full-text search for each unique term and merges the results
   * into a single deduplicated list keyed by `projectId-id`.
   *
   * Deduplication is needed because multiple search terms may return the same
   * record. We want to show it once, not once per matching term.
   *
   * If terms is empty, falls back to fetchRecordsForSelection so that removing
   * the last active filter restores the full browse view automatically.
   *
   * Results are scoped to the currently selected projects when search returns
   * records from all projects in the organisation.
   */
  const runSearchTerms = useCallback(
    async (terms: string[]) => {
      const uniqueTerms = Array.from(
        new Set(terms.map((term) => term.trim()).filter(Boolean)),
      );

      if (uniqueTerms.length === 0) {
        await fetchRecordsForSelection();
        return;
      }

      const searchResults = await Promise.all(
        uniqueTerms.map((term) =>
          fullTextSearch(
            organization?.organizationId as number,
            term,
            projects.map((p) => Number(p.id)),
          ),
        ),
      );

      const recordsByKey = new Map<string, RecordTableRow>();

      searchResults.flat().forEach((dto: HistoricalRecordResponseDto) => {
        const record: RecordTableRow = {
          ...dto,
          fileType: "",
          timeseries: undefined,
          fileSize: undefined,
          select: false,
          associatedRecords: undefined,
          // archivedAt is not a dedicated API field; we derive it from
          // lastUpdatedAt when the record is marked archived.
          archivedAt: dto.isArchived ? dto.lastUpdatedAt : null,
        };

        recordsByKey.set(`${record.projectId ?? "none"}-${record.id}`, record);
      });

      const selectedNums = effectiveProjectIds.map(Number);
      const convertedResults = Array.from(recordsByKey.values());

      // When all projects are selected we skip the filter — no point iterating
      // a potentially large list just to confirm everything passes.
      const scoped =
        selectedNums.length === projects.length
          ? convertedResults
          : convertedResults.filter((record) =>
              selectedNums.includes(Number(record.projectId)),
            );

      setTableData(scoped);
    },
    [
      effectiveProjectIds,
      fetchRecordsForSelection,
      organization?.organizationId,
      projects,
    ],
  );

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

      runSearchTerms(nextFilters.map((filter) => filter.term)).catch(
        (e: unknown) => {
          console.error("Fetch after filter removal failed:", e);
        },
      );
    },
    [activeFilters, clearSearchQueryFromUrl, runSearchTerms],
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

      await runSearchTerms(nextFilters.map((filter) => filter.term));
      setActiveFilters(nextFilters);
      setNextFilterId((n) => n + 1);
      setSearchTerm("");
    },
    [activeFilters, nextFilterId, runSearchTerms],
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
    if (!initialSearchTerm) return;
    if (initialSearchAppliedRef.current === initialSearchTerm) return;

    initialSearchAppliedRef.current = initialSearchTerm;
    handleSearch(initialSearchTerm);
  }, [hasLoaded, initialSearchTerm, handleSearch]);

  /**
   * Re-fetch records whenever the project dropdown selection changes.
   * Search takes precedence: if there are active search filters we do NOT
   * re-fetch here because runSearchTerms already scopes its results to the
   * currently selected projects via effectiveProjectIds.
   */
  useEffect(() => {
    if (!hasLoaded) return;
    if (activeFilters.length > 0) return;

    fetchRecordsForSelection().catch((e: unknown) => {
      console.error("Fetch on selection change failed:", e);
    });
  }, [
    hasLoaded,
    activeFilters.length,
    selectedProjectsToken,
    fetchRecordsForSelection,
  ]);

  /**
   * 
   */
  useEffect(() => {
    if (!hasLoaded) return;
    if (!organization?.organizationId) return;
    
    const projectIds = effectiveProjectIds.map(Number).filter(Number.isFinite);
    
    getAllTagsOrg(organization.organizationId, projectIds, true)
        .then((tags) => {
          setAvailableTags(
              tags.map((tag) => ({
                id: tag.id,
                name: tag.name,
              })),
          );
        })
        .catch((error) => {
          console.error("Failed to fetch available tags:", error);
        });
  }, [hasLoaded, organization?.organizationId, effectiveProjectIds]);
  
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

  /**
   * First stage of filtering: constrain tableData to the selected projects.
   * This is a no-op when all projects are selected (returns tableData as-is)
   * to avoid an unnecessary array allocation on every render.
   */
  const projectScopedRecords = useMemo(() => {
    if (effectiveProjectIds.length === projects.length) return tableData;

    const selected = new Set(effectiveProjectIds.map(Number));
    return tableData.filter(
      (record) =>
        record.projectId !== undefined &&
        selected.has(Number(record.projectId)),
    );
  }, [effectiveProjectIds, projects.length, tableData]);

  // Build facet option lists from the project-scoped records before applying
  // any facet filters. This keeps sidebar counts accurate regardless of which
  // filters are active.
  const classFacetOptions = useMemo(
    () =>
      countFacet(
        projectScopedRecords.map(
          (record) => record.className || t.translations.NO_CLASS,
        ),
      ),
    [projectScopedRecords, t.translations.NO_CLASS],
  );

  const tagFacetOptions = useMemo(
    () =>
      countFacet(
        projectScopedRecords.flatMap((record) => parseRecordTags(record.tags)),
      ),
    [projectScopedRecords],
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

  /**
   * Second stage of filtering: apply all active sidebar facet filters to the
   * project-scoped records. All filters within a group are AND-combined across
   * groups (a record must pass every group's condition), but within the tag
   * group OR semantics apply (a record only needs one matching tag).
   */
  const scopedRecords = useMemo(() => {
    return projectScopedRecords.filter((record) => {
      const className = record.className || t.translations.NO_CLASS;
      const tags = parseRecordTags(record.tags);
      const updatedBy = record.lastUpdatedBy || "Unknown";

      if (
        selectedClassFilters.length > 0 &&
        !selectedClassFilters.includes(className)
      ) {
        return false;
      }

      // Tag filter uses OR: record is included if it has ANY of the selected tags.
      if (
        selectedTagFilters.length > 0 &&
        !selectedTagFilters.some((tag) => tags.includes(tag))
      ) {
        return false;
      }

      if (
        selectedUpdatedByFilters.length > 0 &&
        !selectedUpdatedByFilters.includes(updatedBy)
      ) {
        return false;
      }

      if (statusFilter === "active" && record.isArchived) return false;
      if (statusFilter === "archived" && !record.isArchived) return false;

      return true;
    });
  }, [
    projectScopedRecords,
    selectedClassFilters,
    selectedTagFilters,
    selectedUpdatedByFilters,
    statusFilter,
    t.translations.NO_CLASS,
  ]);

  /* ─── Pagination ────────────────────────────────────────────────────────── */

  const totalPages = Math.max(
    1,
    Math.ceil(scopedRecords.length / RECORDS_PER_PAGE),
  );
  const firstRecordIndex = (currentPage - 1) * RECORDS_PER_PAGE;
  const currentRecords = scopedRecords.slice(
    firstRecordIndex,
    firstRecordIndex + RECORDS_PER_PAGE,
  );
  const selectedRecords = useMemo(() => {
    const selected = new Set(selectedRecordKeys);
    
    return tableData.filter((record) =>
      selected.has(getRecordKey(record)),
    );
  }, [getRecordKey, tableData, selectedRecordKeys]);
  const selectedRecordCount = selectedRecords.length;
  const pageStart = scopedRecords.length === 0 ? 0 : firstRecordIndex + 1;
  const pageEnd = Math.min(
    firstRecordIndex + RECORDS_PER_PAGE,
    scopedRecords.length,
  );

  // Derive stats from the final filtered set for the summary badges.
  const catalogStats = useMemo(() => {
    const archivedCount = scopedRecords.filter(
      (record) => record.isArchived,
    ).length;
    return { archivedCount };
  }, [scopedRecords]);

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
  
  const getBulkTagState = useCallback(
      (tagName: string): BulkTagState => {
        if (selectedRecords.length === 0) {
          return "unchecked";
        }
        
        const matchingCount = selectedRecords.filter((record) => {
          const tags = parseRecordTags(record.tags);
          
          return tags.includes(tagName);
        }).length;
        
        if (matchingCount === 0) {
          return "unchecked";
        }
        
        if (matchingCount === selectedRecords.length) {
          return "checked";
        }
        
        return "indeterminate";
      },
      [selectedRecords],
  );
  
  const handleCancelBulkTags = useCallback(() => {
    setIsBulkMode(false);
    setSelectedRecordKeys([]);
    setTagsToAttach([]);
    setTagsToUnattach([]);
    setBulkTagQuery("");
  }, []);

  /** Resets all facet filters and clears both sidebar search inputs. */
  const clearFacetFilters = useCallback(() => {
    setSelectedClassFilters([]);
    setSelectedTagFilters([]);
    setSelectedUpdatedByFilters([]);
    setStatusFilter("all");
    setClassFacetQuery("");
    setTagFacetQuery("");
  }, []);

  // Reset to page 1 whenever anything changes that could shrink the result set
  // so the user is never left on a page that no longer exists.
  useEffect(() => {
    setCurrentPage(1);
  }, [
    activeFilters.length,
    selectedProjectsToken,
    tableData.length,
    selectedClassFilters,
    selectedTagFilters,
    selectedUpdatedByFilters,
    statusFilter,
  ]);

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
      <section className="border-b border-base-300 bg-base-100">
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
              resultCount={scopedRecords.length}
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
            {pageStart}-{pageEnd} {t.translations.OF} {scopedRecords.length}
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <button
              type="button"
              className="cursor-pointer text-sm font-semibold underline underline-offset-2 hover:text-primary"
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
            {activeFilters.length > 0 && (
              <button
                type="button"
                onClick={clearAllFilters}
                className="btn btn-sm btn-ghost"
              >
                {t.translations.CLEAR_SEARCH}
              </button>
            )}
            {catalogStats.archivedCount > 0 && (
              <span className="badge badge-warning badge-outline">
                <ArchiveBoxIcon className="size-3" />
                {catalogStats.archivedCount}
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
                projectScopedRecords={projectScopedRecords}
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
                  availableTags={availableTags}
                  onCancelBulkTags={handleCancelBulkTags}
                  getBulkTagState={getBulkTagState}
                />
            )}
          </div>

          {/* Record list */}
          <div className="min-w-0">
            {scopedRecords.length === 0 ? (
              <div className="card border border-base-300 bg-base-100 shadow-sm">
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
              <div className="divide-y divide-base-300 overflow-hidden rounded-box border border-base-300 bg-base-100 shadow-sm">
                {currentRecords.map((record) => (
                  <RecordCard
                    key={`${record.projectId}-${record.id}`}
                    record={record}
                    activeSearchTerms={activeSearchTerms}
                    isBulkMode={isBulkMode}
                    isSelected={selectedRecordKeys.includes(getRecordKey(record))}
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
              disabled={currentPage === 1}
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
              disabled={currentPage === totalPages}
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
