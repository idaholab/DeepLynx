import type {
  ClassResponseDto,
  DataSourceResponseDto,
  RecordResponseDto,
} from "@/app/(home)/types/responseDTOs";
import { useLanguage } from "@/app/contexts/Language";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import { useProjectSession } from "@/app/contexts/ProjectSessionProvider";
import { fetchInsightIngestionStatus } from "@/app/lib/client_service/insight_services.client";
import {
  searchRecords,
  searchRecordsPaginated,
} from "@/app/lib/client_service/record_services.client";
import { useCallback, useEffect, useRef, useState } from "react";
import toast from "react-hot-toast";
import {
  ProjectInsightRecord,
  ProjectInsightStatus,
} from "../components/projectInsight.types";
import {
  EMPTY_TAB_FILTER_STATE,
  getStatusFromError,
  TabFilterState,
} from "../components/projectInsight.view-utils";
import { mapProjectInsightRecords } from "../components/projectInsight.utils";

// ============================== NON-PAGINATED RECORD SEARCH ==============================

/**
 *
 * @param embedding The record Insight embedding status
 * @param classes Required record classes
 * @param sources Required
 * @returns
 */
export function useRecordSearch(
  embedding: "embedded" | "pending",
  classes: ClassResponseDto[] | null,
  sources: DataSourceResponseDto[] | null,
) {
  const { t } = useLanguage();

  const [total, setTotal] = useState(0);
  const [found, setFound] = useState(0);

  function reset() {
    setTotal(0);
  }

  const fetchRecords = useCallback(
    async (
      organizationId: number,
      projectId: number,
      filters: TabFilterState,
      embedding: "embedded" | "pending",
      cancel: () => boolean,
    ) => {
      const recordDtos = await searchRecords(organizationId, projectId, {
        userQuery: filters.searchQuery,
        tagIds: filters.tagIds,
        classIds: filters.classIds,
        embedding,
        isInsightEligible: true,
        hideArchived: true,
      });
      if (cancel()) return [];

      // for first time loading. This is a hacky solution to find the total number of records with the initial empty filter search
      setTotal((t) => Math.max(t, recordDtos.length));
      setFound(recordDtos.length);

      return recordDtos;
    },
    [t],
  );

  const search = useRecordSearchGeneric(
    embedding,
    classes,
    sources,
    fetchRecords,
    reset,
  );

  return {
    ...search,
    total,
    found,
  };
}

// ============================== PAGINATED RECORD SEARCH ==============================

export function useRecordSearchPaginated(
  initialPageSize: number,
  embedding: "embedded" | "pending",
  classes: ClassResponseDto[] | null,
  sources: DataSourceResponseDto[] | null,
) {
  const { t } = useLanguage();

  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [found, setFound] = useState(0);
  const [pageSize, setPageSize] = useState(initialPageSize);

  const prePageSize = useRef(initialPageSize);
  const preFilters = useRef(EMPTY_TAB_FILTER_STATE);

  function reset() {
    setFound(0);
    setTotal(0);
    setTotalPages(0);
    setPage(1);
  }

  const fetchRecords = useCallback(
    async (
      organizationId: number,
      projectId: number,
      filters: TabFilterState,
      embedding: "embedded" | "pending",
      cancel: () => boolean,
    ) => {
      const recordDtos = await searchRecordsPaginated(
        organizationId,
        projectId,
        {
          userQuery: filters.searchQuery,
          tagIds: filters.tagIds,
          classIds: filters.classIds,
          embedding,
          isInsightEligible: true,
          hideArchived: true,
        },
        pageSize,
        prePageSize.current !== pageSize ? 1 : page,
      );
      if (cancel()) return [];

      // avoids trying to load a page out of bounds
      if (prePageSize.current !== pageSize || preFilters.current !== filters)
        setPage(1);
      prePageSize.current = pageSize;
      preFilters.current = filters;

      setFound(recordDtos.totalCount);
      // for first time loading. This is a hacky solution to find the total number of records with the initial empty filter search
      setTotal((t) => Math.max(t, recordDtos.totalCount));
      setTotalPages(recordDtos.totalPages);

      return recordDtos.items;
    },
    [t, page, pageSize],
  );

  const search = useRecordSearchGeneric(
    embedding,
    classes,
    sources,
    fetchRecords,
    reset,
  );

  return {
    ...search,
    page,
    setPage,
    pageSize,
    setPageSize,
    totalPages,
    total,
    found,
  };
}

// ============================== GENERIC RECORD SEARCH ==============================

function useRecordSearchGeneric(
  embedding: "embedded" | "pending",
  classes: ClassResponseDto[] | null,
  sources: DataSourceResponseDto[] | null,
  fetchRecords: (
    organizationId: number,
    projectId: number,
    filters: TabFilterState,
    embedding: "embedded" | "pending",
    cancel: () => boolean,
  ) => Promise<RecordResponseDto[]>,
  resetState: () => void,
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

  const [filters, setFilters] = useState<TabFilterState>(
    EMPTY_TAB_FILTER_STATE,
  );
  const [records, setRecords] = useState<ProjectInsightRecord[]>([]);
  const [status, setStatus] = useState<Record<number, ProjectInsightStatus>>(
    {},
  );

  const [error, setError] = useState("");

  function reset() {
    setRecords([]);
    setStatus({});
    setError("");
    resetState();
  }

  useEffect(() => {
    let cancel = false;

    async function loadRecordsPage() {
      if (!hasProjectLoaded || !hasOrganizationLoaded) return;

      if (!projectId || !organizationId || !classes || !sources) {
        reset();
        return;
      }

      setError("");

      try {
        const recordDtos = await fetchRecords(
          organizationId,
          projectId,
          filters,
          embedding,
          () => cancel,
        );
        if (cancel) return;

        // append records
        const newRecords = mapProjectInsightRecords(
          recordDtos,
          classes,
          sources,
        );
        setRecords(newRecords);

        setStatus((previous) =>
          updateStatusKeepQueuedOrProcessing(
            previous,
            newRecords.map((record) => [
              record.id,
              { state: embedding == "embedded" ? "embedded" : "not_embedded" },
            ]),
          ),
        );
      } catch (error) {
        reset();
        console.error("Failed to load project Insight records:", error);
        toast.error(t.translations.PROJECT_INSIGHT_LOADING_RECORDS);
        setError(t.translations.FAILED_TO_SEARCH_RECORDS);
      }
    }

    loadRecordsPage();

    return () => {
      cancel = true;
    };
  }, [
    t,
    hasProjectLoaded,
    hasOrganizationLoaded,
    projectId,
    organizationId,
    classes,
    sources,
    filters,
    fetchRecords,
  ]);

  return {
    filters,
    setFilters,
    records,
    status,
    error,
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
