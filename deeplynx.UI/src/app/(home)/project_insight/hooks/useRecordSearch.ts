import type {
  ClassResponseDto,
  DataSourceResponseDto,
} from "@/app/(home)/types/responseDTOs";
import { useLanguage } from "@/app/contexts/Language";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import { useProjectSession } from "@/app/contexts/ProjectSessionProvider";
import { fetchInsightIngestionStatus } from "@/app/lib/client_service/insight_services.client";
import { searchRecordsPaginated } from "@/app/lib/client_service/record_services.client";
import { useEffect, useRef, useState } from "react";
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

export default function useRecordSearch(
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

  const [filters, setFilters] = useState<TabFilterState>(
    EMPTY_TAB_FILTER_STATE,
  );
  const [records, setRecords] = useState<ProjectInsightRecord[]>([]);
  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [found, setFound] = useState(0);
  const [pageSize, setPageSize] = useState(initialPageSize);
  const prePageSize = useRef(initialPageSize);

  const [status, setStatus] = useState<Record<number, ProjectInsightStatus>>(
    {},
  );

  const [error, setError] = useState("");

  async function fetchInsightStatus(
    record: ProjectInsightRecord,
    organizationId: number,
    projectId: number,
  ) {
    try {
      const ingestionStatus = await fetchInsightIngestionStatus({
        organizationId,
        projectId,
        fileId: record.id,
      });
      return [
        record.id,
        ingestionStatus.indexed
          ? {
              state: "embedded",
              chunkCount: ingestionStatus.chunk_count,
              pageCount: ingestionStatus.page_count,
            }
          : { state: "not_embedded" },
      ] as const;
    } catch (error) {
      return [record.id, getStatusFromError(error)] as const;
    }
  }

  async function loadRecordStatus(
    newRecords: ProjectInsightRecord[],
    organizationId: number,
    projectId: number,
  ) {
    // Sets default values while they load
    setStatus(
      Object.fromEntries(
        newRecords.map((record) => [
          record.id,
          { state: "checking" } satisfies ProjectInsightStatus,
        ]),
      ),
    );

    // Load the actual values
    return Object.fromEntries(
      await Promise.all(
        newRecords.map(async (r) =>
          fetchInsightStatus(r, organizationId, projectId),
        ),
      ),
    );
  }

  function reset() {
    setRecords([]);
    setFound(0);
    setStatus({});
    setTotal(0);
    setTotalPages(0);
    setPage(1);
    setError("");
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
        if (cancel) return;

        // avoids trying to load a page out of bounds
        if (prePageSize.current !== pageSize) setPage(1);
        prePageSize.current = pageSize;

        // append records
        const newRecords = mapProjectInsightRecords(
          recordDtos.items,
          classes,
          sources,
        );

        setRecords(newRecords);
        setFound(recordDtos.totalCount);
        // for first time loading. This is a hacky solution to find the total number of records with the initial empty filter search
        setTotal((t) => Math.max(t, recordDtos.totalCount));
        setTotalPages(recordDtos.totalPages);

        // append status map
        var newStatus = await loadRecordStatus(
          newRecords,
          organizationId,
          projectId,
        );
        if (cancel) return;

        setStatus(newStatus);
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
    page,
    pageSize,
    hasProjectLoaded,
    hasOrganizationLoaded,
    projectId,
    organizationId,
    classes,
    sources,
    filters,
  ]);

  // const loadNextPage = useCallback(async () => {
  //   if (records.length === total) return; // no more records to load
  //   setPage(page + 1); // this won't work if spammed, but it also shouldn't be spammed
  //   await loadRecordsPage(page + 1, () => false);
  // }, [page, records, loadRecordsPage]);

  // useEffect(() => {
  //   if (projectId && organizationId && sources && classes) {
  //     reset();
  //     setFilters(EMPTY_TAB_FILTER_STATE);
  //   }
  // }, [projectId, organizationId, sources, classes]);

  // useEffect(() => {
  //   let cancel = false;
  //   if (projectId && organizationId && sources && classes) {
  //     reset();
  //     loadRecordsPage(1, () => cancel);
  //   }
  //   return () => {
  //     cancel = true;
  //   };
  // }, [filters]);

  return {
    page,
    setPage,
    pageSize,
    setPageSize,
    totalPages,
    filters,
    setFilters,
    records,
    status,
    total,
    found,
    error,
  };
}
