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
import FilterSidebar, {
  RecordStatusFilter,
} from "./components/FilterSidebar";
import RecordCard from "./components/RecordCard";
import ManageTagsCard from "./components/ManageTagsCard";
import { countFacet, parseRecordTags } from "./components/utils";

import {
  bulkAttachTagsToRecords,
  bulkUnattachTagsFromRecords,
} from "@/app/lib/client_service/record_services.client";
import Skeleton from "react-loading-skeleton";
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
}

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

  // Facet filter state — all local, no API calls.
  const [selectedClassFilters, setSelectedClassFilters] = useState<string[]>([]);
  const [selectedTagFilters, setSelectedTagFilters] = useState<string[]>([]);
  const [selectedUpdatedByFilters, setSelectedUpdatedByFilters] = useState<string[]>([]);
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
    setLoading(true);
    const idsNum = effectiveProjectIds.map(Number).filter(Number.isFinite);
    if (idsNum.length === 0) {
      setTableData([]);
      setLoading(false);
      return;
    }
    const data = await getMultiProjectRecords(
      organization?.organizationId as number,
      idsNum,
      false, // archived records will be marked as archived
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
    setLoading(false);
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
            false // keep archived ones showing, but will be flagged as archived
          ),
        ),
      );

      const recordsByKey = new Map<string, RecordTableRow>();

      searchResults.flat().forEach((dto: QueryRecordViewResponseDto) => {
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
   * Fetches tags available to the current project scope.
   * 
   * The organization tag endpoint returns organization-level tags by default.
   * When projectIds are provided, it also includes tags scoped to those projects.
   */
  useEffect(() => {
    if (!hasLoaded) return;
    if (!organization?.organizationId) return;
    
    const fetchTagsForProjectScope = async () => {
      const organizationId = Number(organization.organizationId);
      const projectIds = effectiveProjectIds
          .map(Number)
          .filter(Number.isFinite);
      
      const tags = await getAllTagsOrg(
          organizationId,
          projectIds,
          true,
      );
      
      setAvailableTags(
          tags.map((tag) => ({
            id: tag.id,
            name: tag.name,
            projectId: tag.projectId ?? null,
          })),
      );
    };
    
    fetchTagsForProjectScope().catch((error) => {
      console.error("Failed to fetch available tags:", error);
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
        !selectedUpdatedByFilters.includes(String(updatedBy))
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
  const hasPendingBulkTagChanges = tagsToAttach.length > 0 || tagsToUnattach.length > 0;
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
          Array.from(recordsByProject.entries()).flatMap(([projectId, records]) => {
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
                  bulkAttachTagsToRecords(
                      organizationId,
                      projectId,
                      attachDtos,
                  ),
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
          }),
      );

      // Refresh whichever data mode the page is currently in.
      if (activeFilters.length > 0) {
        await runSearchTerms(activeFilters.map((filter) => filter.term));
      } else {
        await fetchRecordsForSelection();
      }

      // Only clear selection and staged changes after every API request succeeds.
      handleCancelBulkTags();
    } catch (error) {
      console.error("Failed to apply bulk tag changes:", error);
    } finally {
      setIsApplyingBulkTags(false);
    }
  }, [
    activeFilters,
    fetchRecordsForSelection,
    handleCancelBulkTags,
    hasPendingBulkTagChanges,
    organization?.organizationId,
    runSearchTerms,
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
                  availableTags={visibleAvailableTags}
                  getBulkTagState={getBulkTagState}
                  onToggleBulkTag={toggleBulkTag}
                  showProjectScopeNotice={effectiveProjectIds.length > 1}
                />
            )}
          </div>

          {/* Record list */}
          <div className="min-w-0">
            { loading === true ? (
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
            ) : scopedRecords.length === 0 ? (
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
