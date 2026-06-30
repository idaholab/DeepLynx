import type {
  ClassResponseDto,
  DataSourceResponseDto,
} from "@/app/(home)/types/responseDTOs";
import { useLanguage } from "@/app/contexts/Language";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import { useProjectSession } from "@/app/contexts/ProjectSessionProvider";
import { fetchInsightIngestionStatus } from "@/app/lib/client_service/insight_services.client";
import { searchRecordsPaginated } from "@/app/lib/client_service/record_services.client";
import { useCallback, useEffect, useState } from "react";
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
  pageSize: number,
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

  const [filters, setFilters] = useState<TabFilterState>({
    ...EMPTY_TAB_FILTER_STATE, // unpack here for differentiating from EMPTY_TAB_FILTER_STATE for initial loads
  });
  const [records, setRecords] = useState<ProjectInsightRecord[]>([]);
  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);
  const [found, setFound] = useState(0);

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
    const newObjects = Object.fromEntries(
      newRecords.map((record) => [
        record.id,
        { state: "checking" } satisfies ProjectInsightStatus,
      ]),
    );

    setStatus((previous) => ({
      ...previous,
      ...newObjects,
    }));

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
    setPage(1);
    setError("");
  }

  const loadRecordsPage = useCallback(
    async (page: number, cancel: () => boolean) => {
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
          page,
        );
        if (cancel()) return;

        // append records
        const newRecords = mapProjectInsightRecords(
          recordDtos.items,
          classes,
          sources,
        );

        setRecords((records) => [...records, ...newRecords]);
        setFound(recordDtos.totalCount);
        setTotal((t) => Math.max(t, recordDtos.totalCount)); // for first time loading

        // append status map
        var newStatus = await loadRecordStatus(
          newRecords,
          organizationId,
          projectId,
        );
        if (cancel()) return;

        setStatus((status) => ({ ...status, ...newStatus }));
      } catch (error) {
        reset();
        console.error("Failed to load project Insight records:", error);
        toast.error(t.translations.PROJECT_INSIGHT_LOADING_RECORDS);
        setError(t.translations.FAILED_TO_SEARCH_RECORDS);
      }
    },
    [
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
    ],
  );

  const loadNextPage = useCallback(async () => {
    if (records.length === total) return; // no more records to load
    setPage(page + 1); // this won't work if spammed, but it also shouldn't be spammed
    await loadRecordsPage(page + 1, () => false);
  }, [page, records, loadRecordsPage]);

  useEffect(() => {
    if (projectId && organizationId && sources && classes) {
      reset();
      setFilters(EMPTY_TAB_FILTER_STATE);
    }
  }, [projectId, organizationId, sources, classes]);

  useEffect(() => {
    let cancel = false;
    if (projectId && organizationId && sources && classes) {
      reset();
      loadRecordsPage(1, () => cancel);
    }
    return () => {
      cancel = true;
    };
  }, [filters]);

  return {
    filters,
    setFilters,
    records,
    status,
    total,
    found,
    error,
    loadNextPage,
  };
}
