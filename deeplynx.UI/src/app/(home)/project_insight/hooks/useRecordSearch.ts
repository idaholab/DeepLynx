import type {
  ClassResponseDto,
  DataSourceResponseDto,
} from "@/app/(home)/types/responseDTOs";
import { useLanguage } from "@/app/contexts/Language";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import { useProjectSession } from "@/app/contexts/ProjectSessionProvider";
import { searchRecords } from "@/app/lib/client_service/record_services.client";
import { useEffect, useMemo, useRef, useState } from "react";
import toast from "react-hot-toast";
import {
  ProjectInsightRecord,
  ProjectInsightStatus,
} from "../components/projectInsight.types";
import {
  EMPTY_TAB_FILTER_STATE,
  TabFilterState,
} from "../components/projectInsight.view-utils";
import { mapProjectInsightRecords } from "../components/projectInsight.utils";

// ============================== HYBRID RECORD SEARCH ==============================
//
// Browse mode (no search query): records are paginated server-side, one page per
// request — mirrors a normal paginated table.
//
// Search mode (a submitted search query): the full matching set is fetched exactly
// once per committed query/filter combo, then paginated on the frontend. Turning
// pages while a search is active never re-hits the search records endpoint.

/**
 * @param initialPageSize Records per page in browse mode
 * @param embedding The record Insight embedding status this tab shows
 * @param classes Required record classes (used to resolve display names)
 * @param sources Required record data sources (used to resolve display names)
 */
export function useRecordSearchHybrid(
  initialPageSize: number,
  embedding: "embedded" | "pending",
  classes: ClassResponseDto[] | null,
  sources: DataSourceResponseDto[] | null,
) {
  const { t } = useLanguage();
  const { project, hasLoaded: hasProjectLoaded } = useProjectSession();
  const { organization, hasLoaded: hasOrganizationLoaded } =
    useOrganizationSession();

  const projectId =
    project?.projectId !== undefined ? Number(project.projectId) : null;
  const organizationId =
    organization?.organizationId !== undefined
      ? Number(organization.organizationId)
      : null;

  const [filters, setFilters] = useState<TabFilterState>(EMPTY_TAB_FILTER_STATE);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(initialPageSize);

  const isSearchMode = filters.searchQuery.trim().length > 0;

  // Browse mode: exactly the current server page.
  const [browseRecords, setBrowseRecords] = useState<ProjectInsightRecord[]>([]);
  const [browseTotalPages, setBrowseTotalPages] = useState(0);
  const [browseTotalCount, setBrowseTotalCount] = useState(0);

  // Search mode: the full matching set. Paginated on the frontend (see the
  // derivation below).
  const [searchMatches, setSearchMatches] = useState<ProjectInsightRecord[]>([]);

  const [status, setStatus] = useState<Record<number, ProjectInsightStatus>>({});
  const [error, setError] = useState("");
  const [isLoading, setIsLoading] = useState(false);
  const [total, setTotal] = useState(0);

  const prePageSize = useRef(initialPageSize);
  const preFilters = useRef(EMPTY_TAB_FILTER_STATE);

  const canLoad =
    hasProjectLoaded &&
    hasOrganizationLoaded &&
    !!projectId &&
    !!organizationId &&
    !!classes &&
    !!sources;

  function resetAll() {
    setBrowseRecords([]);
    setBrowseTotalPages(0);
    setBrowseTotalCount(0);
    setSearchMatches([]);
    setStatus({});
    setError("");
    setTotal(0);
    setPage(1);
  }

  function applyStatusDefaults(records: ProjectInsightRecord[]) {
    setStatus((previous) =>
      updateStatusKeepQueuedOrProcessing(
        previous,
        records.map((record) => [
          record.id,
          { state: embedding === "embedded" ? "embedded" : "not_embedded" },
        ]),
      ),
    );
  }

  useEffect(() => {
    if (isSearchMode) return;

    if (!canLoad) {
      resetAll();
      return;
    }

    // A new filter/class/tag combo (as opposed to just turning pages) lands
    // back on page 1 — and must not double-fetch once setPage(1) re-triggers
    // this same effect.
    const filtersChanged = preFilters.current !== filters;
    const pageSizeChanged = prePageSize.current !== pageSize;
    const targetPage = filtersChanged || pageSizeChanged ? 1 : page;

    preFilters.current = filters;
    prePageSize.current = pageSize;

    if (targetPage !== page) {
      setPage(targetPage);
      return;
    }

    let cancelled = false;
    setIsLoading(true);
    setError("");

    (async () => {
      try {
        const response = await searchRecords(
          organizationId!,
          projectId!,
          {
            userQuery: filters.searchQuery,
            tagIds: filters.tagIds,
            classIds: filters.classIds,
            embedding,
            isInsightEligible: true,
            hideArchived: true,
          },
          { pageSize, pageNumber: page },
        );
        if (cancelled) return;

        const mapped = mapProjectInsightRecords(response.items, classes!, sources!);
        setBrowseRecords(mapped);
        setBrowseTotalPages(response.totalPages);
        setBrowseTotalCount(response.totalCount);
        setTotal((current) => Math.max(current, response.totalCount));
        applyStatusDefaults(mapped);
      } catch (fetchError) {
        if (cancelled) return;
        console.error("Failed to load project Insight records:", fetchError);
        toast.error(t.translations.PROJECT_INSIGHT_LOADING_RECORDS);
        setError(t.translations.FAILED_TO_SEARCH_RECORDS);
        setBrowseRecords([]);
        setBrowseTotalPages(0);
      } finally {
        if (!cancelled) setIsLoading(false);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [
    isSearchMode,
    canLoad,
    filters,
    page,
    pageSize,
    embedding,
    classes,
    sources,
    organizationId,
    projectId,
    t,
  ]);

  // search mode: fetch the full matching set once per submitted
  // query/filters combo. Deliberately excludes page/pageSize from its deps —
  // turning pages must not trigger another fetch.
  useEffect(() => {
    if (!isSearchMode) return;

    if (!canLoad) {
      resetAll();
      return;
    }

    setPage(1);

    let cancelled = false;
    setIsLoading(true);
    setError("");

    (async () => {
      try {
        const response = await searchRecords(organizationId!, projectId!, {
          userQuery: filters.searchQuery,
          tagIds: filters.tagIds,
          classIds: filters.classIds,
          embedding,
          isInsightEligible: true,
          hideArchived: true,
        });
        if (cancelled) return;

        const mapped = mapProjectInsightRecords(response.items, classes!, sources!);
        setSearchMatches(mapped);
        setTotal((current) => Math.max(current, response.totalCount));
        applyStatusDefaults(mapped);
      } catch (fetchError) {
        if (cancelled) return;
        console.error("Failed to search project Insight records:", fetchError);
        toast.error(t.translations.PROJECT_INSIGHT_LOADING_RECORDS);
        setError(t.translations.FAILED_TO_SEARCH_RECORDS);
        setSearchMatches([]);
      } finally {
        if (!cancelled) setIsLoading(false);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [isSearchMode, canLoad, filters, embedding, classes, sources, organizationId, projectId, t]);

  // Browse mode: `browseRecords` is already just the current page (server
  // paginated), with `browseTotalPages` / `browseTotalCount` from the server.
  //
  // Search mode: `searchMatches` is the FULL matching set — nothing has been
  // paginated yet, so `page`/`pageSize` need to be applied locally here.
  const { records, totalPages, found } = useMemo(() => {
    if (isSearchMode) {
      const found = searchMatches.length;
      const totalPages = Math.ceil(found / pageSize);

      // Accounts for page changing based on page size, mirroring how PaginationControls itself
      // clamps `currentPage` for display via Math.max(1, totalPages).
      const safePage = Math.min(Math.max(page, 1), Math.max(totalPages, 1));
      const start = (safePage - 1) * pageSize;
      const records = searchMatches.slice(start, start + pageSize);

      return { records, totalPages, found };
    }

    return {
      records: browseRecords,
      totalPages: browseTotalPages,
      found: browseTotalCount,
    };
  }, [isSearchMode, page, pageSize, searchMatches, browseRecords, browseTotalPages, browseTotalCount]);

  return {
    filters,
    setFilters,
    page,
    setPage,
    pageSize,
    setPageSize,
    totalPages,
    records,
    status,
    error,
    isLoading,
    total,
    found,
  };
}

// ============================== INSIGHT STATUS FUNCTIONS ==============================

function updateStatusKeepQueuedOrProcessing(
  previous: Record<number, ProjectInsightStatus>,
  current: [number, ProjectInsightStatus][],
): Record<number, ProjectInsightStatus> {
  return {
    ...Object.fromEntries(current),
    ...Object.fromEntries(
      Object.entries(previous).filter(
        ([_, status]) =>
          status.state === "queued" || status.state === "processing",
      ),
    ),
  };
}
