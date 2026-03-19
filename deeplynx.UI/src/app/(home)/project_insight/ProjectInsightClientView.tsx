"use client";

import React, { useDeferredValue, useEffect, useState } from "react";
import toast from "react-hot-toast";
import {
  AdjustmentsHorizontalIcon,
  ChatBubbleLeftRightIcon,
  XMarkIcon,
} from "@heroicons/react/24/outline";
import SearchBar from "@/app/(home)/components/SearchBar";
import { useLanguage } from "@/app/contexts/Language";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import { useProjectSession } from "@/app/contexts/ProjectSessionProvider";
import { getAllClasses } from "@/app/lib/client_service/class_services.client";
import { getAllDataSources } from "@/app/lib/client_service/data_source_services.client";
import {
  fetchInsightIngestionStatus,
  queueInsightUpload,
} from "@/app/lib/client_service/insight_services.client";
import { fullTextSearch } from "@/app/lib/client_service/query_services.client";
import { getAllRecords } from "@/app/lib/client_service/record_services.client";
import { getAllTags } from "@/app/lib/client_service/tag_services.client";
import type {
  ClassResponseDto,
  DataSourceResponseDto,
  TagResponseDto,
} from "@/app/(home)/types/responseDTOs";
import ProjectInsightChat from "./components/ProjectInsightChat";
import ProjectInsightFilters from "./components/ProjectInsightFilters";
import ProjectInsightRecordCard from "./components/ProjectInsightRecordCard";
import ProjectInsightRecordSection from "./components/ProjectInsightRecordSection";
import type {
  NamedInsightOption,
  ProjectInsightFiltersState,
  ProjectInsightRecord,
  ProjectInsightStatus,
} from "./components/projectInsight.types";
import {
  getProjectInsightStatus,
  mapProjectInsightRecords,
  matchesInsightFilters,
} from "./components/projectInsight.utils";
import {
  buildActiveFilterPills,
  EMPTY_TAB_FILTER_STATE,
  getStatusFromError,
  matchesMetadataSearch,
  sortNamedOptions,
  type TabFilterState,
  withTokens,
} from "./components/projectInsight.view-utils";
import { useProjectInsightTabState } from "./hooks/useProjectInsightTabState";

const STATUS_POLL_INTERVAL_MS = 5000;

export default function ProjectInsightClientView() {
  // Session context
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

  // View state
  const [records, setRecords] = useState<ProjectInsightRecord[]>([]);
  const [classOptions, setClassOptions] = useState<NamedInsightOption[]>([]);
  const [tagOptions, setTagOptions] = useState<NamedInsightOption[]>([]);
  const [statusMap, setStatusMap] = useState<
    Record<number, ProjectInsightStatus>
  >({});
  const [isLoadingRecords, setIsLoadingRecords] = useState(true);
  const [isFiltersOpen, setIsFiltersOpen] = useState(false);
  const [activeTabKey, setActiveTabKey] = useState<"library" | "pending">(
    "library",
  );
  const [selectedPendingIds, setSelectedPendingIds] = useState<number[]>([]);
  const [isQueueing, setIsQueueing] = useState(false);
  const [libraryMatchedSearchIds, setLibraryMatchedSearchIds] = useState<
    number[] | null
  >(null);
  const [isLibrarySearchLoading, setIsLibrarySearchLoading] = useState(false);
  const [librarySearchError, setLibrarySearchError] = useState("");
  const [libraryState, setLibraryState] = useState<TabFilterState>(
    EMPTY_TAB_FILTER_STATE,
  );
  const [pendingState, setPendingState] = useState<TabFilterState>(
    EMPTY_TAB_FILTER_STATE,
  );
  const deferredLibrarySearchQuery = useDeferredValue(libraryState.searchQuery);

  // Tab state helpers
  const {
    clearActiveSearchQuery,
    removeActiveFilterPill,
    setActiveTabState,
    updateActiveSearchQuery,
  } = useProjectInsightTabState({
    activeTabKey,
    setLibraryState,
    setPendingState,
  });

  // Effects
  useEffect(() => {
    setLibraryState(EMPTY_TAB_FILTER_STATE);
    setPendingState(EMPTY_TAB_FILTER_STATE);
    setLibraryMatchedSearchIds(null);
    setLibrarySearchError("");
    setSelectedPendingIds([]);
    setActiveTabKey("library");
  }, [projectId]);

  useEffect(() => {
    if (!hasProjectLoaded || !hasOrganizationLoaded) return;

    if (!projectId || !organizationId) {
      setIsLoadingRecords(false);
      setRecords([]);
      setStatusMap({});
      setClassOptions([]);
      setTagOptions([]);
      return;
    }

    let cancelled = false;

    const loadProjectInsight = async () => {
      setIsLoadingRecords(true);
      setLibrarySearchError("");

      try {
        const [recordDtos, classDtos, dataSourceDtos, tagDtos] =
          await Promise.all([
            getAllRecords(organizationId, projectId),
            getAllClasses(projectId, true),
            getAllDataSources(projectId, true),
            getAllTags(projectId, true),
          ]);

        if (cancelled) return;

        const mappedRecords = mapProjectInsightRecords(
          recordDtos,
          classDtos as ClassResponseDto[],
          dataSourceDtos as DataSourceResponseDto[],
        );

        setRecords(mappedRecords);
        setClassOptions(sortNamedOptions(classDtos as ClassResponseDto[]));
        setTagOptions(sortNamedOptions(tagDtos as TagResponseDto[]));

        const supportedRecords = mappedRecords.filter(
          (record) => record.isInsightSupported,
        );

        setStatusMap(
          Object.fromEntries(
            supportedRecords.map((record) => [
              record.id,
              { state: "checking" } satisfies ProjectInsightStatus,
            ]),
          ),
        );

        const resolvedStatuses = await Promise.all(
          supportedRecords.map(async (record) => {
            try {
              const status = await fetchInsightIngestionStatus(record.id);
              return [
                record.id,
                status.indexed
                  ? {
                      state: "embedded",
                      chunkCount: status.chunk_count,
                      pageCount: status.page_count,
                    }
                  : { state: "not_embedded" },
              ] as const;
            } catch (error) {
              return [record.id, getStatusFromError(error)] as const;
            }
          }),
        );

        if (cancelled) return;

        setStatusMap(Object.fromEntries(resolvedStatuses));
      } catch (error) {
        console.error("Failed to load project Insight records:", error);
        if (!cancelled) {
          setRecords([]);
          setStatusMap({});
          toast.error(t.translations.PROJECT_INSIGHT_LOADING_RECORDS);
        }
      } finally {
        if (!cancelled) {
          setIsLoadingRecords(false);
        }
      }
    };

    void loadProjectInsight();

    return () => {
      cancelled = true;
    };
  }, [
    hasOrganizationLoaded,
    hasProjectLoaded,
    organizationId,
    projectId,
    t.translations.PROJECT_INSIGHT_LOADING_RECORDS,
  ]);

  useEffect(() => {
    const searchableQuery = deferredLibrarySearchQuery.trim();

    if (!organizationId || !projectId || searchableQuery.length < 2) {
      setLibraryMatchedSearchIds(null);
      setIsLibrarySearchLoading(false);
      setLibrarySearchError("");
      return;
    }

    let cancelled = false;

    const runSearch = async () => {
      setIsLibrarySearchLoading(true);
      setLibraryMatchedSearchIds(null);
      setLibrarySearchError("");

      try {
        const results = await fullTextSearch(organizationId, searchableQuery, [
          projectId,
        ]);

        if (cancelled) return;

        const recordIds = [
          ...new Set(results.map((result) => Number(result.id))),
        ].filter((id) => Number.isFinite(id));
        setLibraryMatchedSearchIds(recordIds);
      } catch (error) {
        console.error("Project Insight full-text search failed:", error);
        if (!cancelled) {
          setLibraryMatchedSearchIds([]);
          setLibrarySearchError(t.translations.FAILED_TO_SEARCH_RECORDS);
        }
      } finally {
        if (!cancelled) {
          setIsLibrarySearchLoading(false);
        }
      }
    };

    void runSearch();

    return () => {
      cancelled = true;
    };
  }, [
    deferredLibrarySearchQuery,
    organizationId,
    projectId,
    t.translations.FAILED_TO_SEARCH_RECORDS,
  ]);

  useEffect(() => {
    const pollingIds = Object.entries(statusMap)
      .filter(
        ([, status]) =>
          status.state === "queued" || status.state === "processing",
      )
      .map(([recordId]) => Number(recordId));

    if (pollingIds.length === 0) return;

    let cancelled = false;

    const pollStatuses = async () => {
      const updates = await Promise.all(
        pollingIds.map(async (recordId) => {
          try {
            const status = await fetchInsightIngestionStatus(recordId);
            return [
              recordId,
              status.indexed
                ? {
                    state: "embedded",
                    chunkCount: status.chunk_count,
                    pageCount: status.page_count,
                  }
                : { state: "processing" },
            ] as const;
          } catch (error) {
            return [recordId, getStatusFromError(error)] as const;
          }
        }),
      );

      if (cancelled) return;

      setStatusMap((current) => ({
        ...current,
        ...Object.fromEntries(updates),
      }));
    };

    void pollStatuses();
    const interval = window.setInterval(() => {
      void pollStatuses();
    }, STATUS_POLL_INTERVAL_MS);

    return () => {
      cancelled = true;
      window.clearInterval(interval);
    };
  }, [statusMap]);

  useEffect(() => {
    setSelectedPendingIds((current) =>
      current.filter((recordId) => {
        const status = statusMap[recordId];
        return status?.state === "not_embedded" || status?.state === "error";
      }),
    );
  }, [statusMap]);

  // Derived tab datasets
  const projectName = project?.projectName ?? "";
  const libraryFilters: ProjectInsightFiltersState = {
    classIds: libraryState.classIds,
    tagIds: libraryState.tagIds,
  };
  const pendingFilters: ProjectInsightFiltersState = {
    classIds: pendingState.classIds,
    tagIds: pendingState.tagIds,
  };

  const libraryFilteredRecords = records.filter((record) =>
    matchesInsightFilters(record, libraryFilters),
  );
  const libraryFilteredSupportedRecords = libraryFilteredRecords.filter(
    (record) => record.isInsightSupported,
  );
  const libraryEmbeddedRecords = libraryFilteredRecords.filter(
    (record) => getProjectInsightStatus(record, statusMap).state === "embedded",
  );
  const libraryPendingRecords = libraryFilteredSupportedRecords.filter(
    (record) => getProjectInsightStatus(record, statusMap).state !== "embedded",
  );

  const pendingFilteredRecords = records.filter((record) =>
    matchesInsightFilters(record, pendingFilters),
  );
  const pendingFilteredSupportedRecords = pendingFilteredRecords.filter(
    (record) => record.isInsightSupported,
  );
  const pendingEmbeddedRecords = pendingFilteredRecords.filter(
    (record) => getProjectInsightStatus(record, statusMap).state === "embedded",
  );
  const pendingRecords = pendingFilteredSupportedRecords.filter(
    (record) => getProjectInsightStatus(record, statusMap).state !== "embedded",
  );

  const normalizedLibrarySearchQuery = libraryState.searchQuery
    .trim()
    .toLowerCase();
  const normalizedPendingSearchQuery = pendingState.searchQuery
    .trim()
    .toLowerCase();
  const remoteMatchIds =
    normalizedLibrarySearchQuery.length >= 2 && libraryMatchedSearchIds
      ? new Set(libraryMatchedSearchIds)
      : null;

  const visibleEmbeddedRecords = normalizedLibrarySearchQuery
    ? libraryEmbeddedRecords.filter((record) => {
        const metadataMatch = matchesMetadataSearch(
          record,
          normalizedLibrarySearchQuery,
        );
        const remoteMatch = remoteMatchIds?.has(record.id) ?? false;
        return metadataMatch || remoteMatch;
      })
    : libraryEmbeddedRecords;

  const visiblePendingRecords = normalizedPendingSearchQuery
    ? pendingRecords.filter((record) =>
        matchesMetadataSearch(record, normalizedPendingSearchQuery),
      )
    : pendingRecords;

  const queueablePendingRecords = visiblePendingRecords.filter((record) => {
    const status = getProjectInsightStatus(record, statusMap).state;
    return (
      Boolean(record.uri) && (status === "not_embedded" || status === "error")
    );
  });
  const queueablePendingIds = queueablePendingRecords.map(
    (record) => record.id,
  );
  const selectedVisiblePendingIds = selectedPendingIds.filter((recordId) =>
    queueablePendingIds.includes(recordId),
  );

  const activeFilters =
    activeTabKey === "library" ? libraryFilters : pendingFilters;
  const activeSearchQuery =
    activeTabKey === "library"
      ? libraryState.searchQuery
      : pendingState.searchQuery;
  const activeSearchPlaceholder =
    activeTabKey === "library"
      ? t.translations.PROJECT_INSIGHT_SEARCH_PLACEHOLDER
      : t.translations.PROJECT_INSIGHT_PENDING_SEARCH_PLACEHOLDER;
  const activeSearchError =
    activeTabKey === "library" ? librarySearchError : "";
  const activeFilterCount =
    activeFilters.classIds.length + activeFilters.tagIds.length;
  const activeFilterPills = buildActiveFilterPills(
    activeFilters,
    classOptions,
    tagOptions,
  );

  const tabLabels = {
    library: t.translations.PROJECT_INSIGHT_LIBRARY_TAB,
    pending: t.translations.PROJECT_INSIGHT_PENDING_TAB,
  };

  const activeFilterStats =
    activeTabKey === "library"
      ? {
          totalRecords: libraryFilteredRecords.length,
          embeddedRecords: libraryEmbeddedRecords.length,
          pendingRecords: libraryPendingRecords.length,
        }
      : {
          totalRecords: pendingFilteredRecords.length,
          embeddedRecords: pendingEmbeddedRecords.length,
          pendingRecords: pendingRecords.length,
        };

  // UI event handlers
  async function handleQueueSelected() {
    if (selectedVisiblePendingIds.length === 0) return;

    const selectedRecords = visiblePendingRecords.filter((record) =>
      selectedVisiblePendingIds.includes(record.id),
    );
    const fileInfo = selectedRecords
      .filter((record) => record.uri)
      .map((record) => ({
        fileId: record.id,
        fileURI: record.uri as string,
      }));

    if (fileInfo.length === 0) return;

    setIsQueueing(true);
    setStatusMap((current) => ({
      ...current,
      ...Object.fromEntries(
        fileInfo.map((file) => [file.fileId, { state: "queued" }]),
      ),
    }));

    try {
      const result = await queueInsightUpload({ fileInfo });
      const queuedCount = result.results.filter(
        (item) => item.status === "queued",
      ).length;
      const failedCount = result.results.length - queuedCount;

      setStatusMap((current) => ({
        ...current,
        ...Object.fromEntries(
          result.results.map((item) => [
            item.file_id,
            item.status === "queued"
              ? { state: "queued" }
              : { state: "error", error: item.error },
          ]),
        ),
      }));

      if (queuedCount > 0) {
        toast.success(
          withTokens(t.translations.PROJECT_INSIGHT_QUEUED_SUMMARY, {
            count: queuedCount,
          }),
        );
      }

      if (failedCount > 0) {
        toast.error(
          withTokens(t.translations.PROJECT_INSIGHT_FAILED_SUMMARY, {
            count: failedCount,
          }),
        );
      }
    } catch (error) {
      console.error("Failed to queue project Insight uploads:", error);
      setStatusMap((current) => ({
        ...current,
        ...Object.fromEntries(
          fileInfo.map((file) => [
            file.fileId,
            {
              state: "error",
              error:
                error instanceof Error
                  ? error.message
                  : t.translations.INSIGHT_ERROR_PREFIX,
            },
          ]),
        ),
      }));
      toast.error(
        withTokens(t.translations.PROJECT_INSIGHT_FAILED_SUMMARY, {
          count: fileInfo.length,
        }),
      );
    } finally {
      setSelectedPendingIds([]);
      setIsQueueing(false);
    }
  }

  // Render summaries
  const embeddedSearchSummary = normalizedLibrarySearchQuery
    ? isLibrarySearchLoading
      ? t.translations.PROJECT_INSIGHT_SEARCHING
      : visibleEmbeddedRecords.length > 0
        ? withTokens(t.translations.PROJECT_INSIGHT_SEARCH_RESULTS, {
            count: visibleEmbeddedRecords.length,
            query: libraryState.searchQuery.trim(),
          })
        : withTokens(t.translations.PROJECT_INSIGHT_SEARCH_RESULTS_EMPTY, {
            query: libraryState.searchQuery.trim(),
          })
    : t.translations.PROJECT_INSIGHT_LIBRARY_DESCRIPTION;
  const pendingSearchSummary = normalizedPendingSearchQuery
    ? withTokens(t.translations.PROJECT_INSIGHT_PENDING_SEARCH_RESULTS, {
        count: visiblePendingRecords.length,
      })
    : t.translations.PROJECT_INSIGHT_PENDING_DESCRIPTION;
  const activeContextTitle =
    activeTabKey === "library"
      ? t.translations.PROJECT_INSIGHT_EMBEDDED_TITLE
      : t.translations.PROJECT_INSIGHT_PENDING_TITLE;
  const activeContextDescription =
    activeTabKey === "library" ? embeddedSearchSummary : pendingSearchSummary;
  const activeContextCount =
    activeTabKey === "library"
      ? visibleEmbeddedRecords.length
      : visiblePendingRecords.length;

  // Render content
  const libraryContent = (
    <ProjectInsightRecordSection
      title={t.translations.PROJECT_INSIGHT_EMBEDDED_TITLE}
      description={embeddedSearchSummary}
      count={visibleEmbeddedRecords.length}
      emptyMessage={t.translations.PROJECT_INSIGHT_EMBEDDED_EMPTY}
    >
      <div className="space-y-3">
        {visibleEmbeddedRecords.map((record) => (
          <ProjectInsightRecordCard
            key={record.id}
            projectId={projectId ?? 0}
            record={record}
            status={getProjectInsightStatus(record, statusMap)}
          />
        ))}
      </div>
    </ProjectInsightRecordSection>
  );

  const pendingContent = (
    <ProjectInsightRecordSection
      title={t.translations.PROJECT_INSIGHT_PENDING_TITLE}
      description={pendingSearchSummary}
      count={visiblePendingRecords.length}
      emptyMessage={t.translations.PROJECT_INSIGHT_PENDING_EMPTY}
      actions={
        queueablePendingIds.length > 0 ? (
          <div className="flex flex-wrap items-center gap-2">
            <button
              type="button"
              className="btn btn-sm btn-ghost"
              onClick={() => setSelectedPendingIds(queueablePendingIds)}
            >
              {t.translations.PROJECT_INSIGHT_SELECT_ALL_VISIBLE}
            </button>
            <button
              type="button"
              className="btn btn-sm btn-ghost"
              onClick={() => setSelectedPendingIds([])}
              disabled={selectedPendingIds.length === 0}
            >
              {t.translations.PROJECT_INSIGHT_CLEAR_SELECTION}
            </button>
            <button
              type="button"
              className="btn btn-sm btn-primary"
              onClick={() => void handleQueueSelected()}
              disabled={selectedVisiblePendingIds.length === 0 || isQueueing}
            >
              {isQueueing
                ? t.translations.UPLOADING
                : t.translations.PROJECT_INSIGHT_EMBED_SELECTED}
            </button>
            {selectedVisiblePendingIds.length > 0 && (
              <span className="badge badge-outline badge-secondary">
                {withTokens(t.translations.PROJECT_INSIGHT_SELECTED_COUNT, {
                  count: selectedVisiblePendingIds.length,
                })}
              </span>
            )}
          </div>
        ) : undefined
      }
    >
      <div className="space-y-3">
        {visiblePendingRecords.map((record) => {
          const status = getProjectInsightStatus(record, statusMap);
          const isSelectable =
            Boolean(record.uri) &&
            (status.state === "not_embedded" || status.state === "error");

          return (
            <ProjectInsightRecordCard
              key={record.id}
              projectId={projectId ?? 0}
              record={record}
              status={status}
              selectable={isSelectable}
              checked={selectedPendingIds.includes(record.id)}
              onToggle={(recordId) =>
                setSelectedPendingIds((current) =>
                  current.includes(recordId)
                    ? current.filter((id) => id !== recordId)
                    : [...current, recordId],
                )
              }
            />
          );
        })}
      </div>
    </ProjectInsightRecordSection>
  );

  if (!hasProjectLoaded || !hasOrganizationLoaded || isLoadingRecords) {
    return (
      <div className="px-4 py-10 lg:px-6">
        <div className="card border border-base-300/60 bg-base-100 shadow-lg">
          <div className="card-body">
            <p className="text-sm text-base-content/70">
              {t.translations.PROJECT_INSIGHT_LOADING_RECORDS}
            </p>
          </div>
        </div>
      </div>
    );
  }

  if (!projectId || !organizationId) {
    return (
      <div className="px-4 py-10 lg:px-6">
        <div className="card border border-base-300/60 bg-base-100 shadow-lg">
          <div className="card-body">
            <p className="text-sm text-base-content/70">
              {t.translations.PROJECT_INSIGHT_PROJECT_REQUIRED}
            </p>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="flex h-[calc(100dvh-7rem)] min-h-0 flex-col overflow-hidden bg-base-100">
      <div className="border-b border-base-300/40 bg-base-200/40 px-6 py-6 lg:px-10">
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div className="max-w-4xl">
            <h1 className="text-2xl font-bold text-base-content">
              {t.translations.PROJECT_INSIGHT_SCOPE}
            </h1>
            <p className="mt-1 text-sm text-base-content/70">
              {withTokens(t.translations.PROJECT_INSIGHT_DESCRIPTION, {
                projectName,
              })}
            </p>
          </div>
        </div>
      </div>

      <div className="flex-1 min-h-0 overflow-hidden p-6 lg:p-8">
        <div className="grid h-full min-h-0 grid-cols-1 gap-6 overflow-y-auto pr-1 xl:grid-cols-[minmax(0,1.7fr)_minmax(340px,1fr)]">
          <section className="flex min-h-0 flex-col">
            <ProjectInsightChat
              projectName={projectName}
              scopedRecordIds={visibleEmbeddedRecords.map(
                (record) => record.id,
              )}
            />
          </section>

          <aside className="card card-border bg-base-100 shadow-md shadow-dynamic-shadow xl:h-full xl:min-h-0">
            <div className="card-body h-full min-h-0 gap-4 p-4 sm:p-5">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div className="min-w-0">
                  <div className="flex gap-3 items-center">
                    <h2 className="card-title text-base-content">
                      {activeContextTitle}
                    </h2>
                    <span className="badge badge-secondary badge-sm shrink-0">
                      {activeContextCount}
                    </span>
                  </div>
                  <div className="mt-1 min-h-[2.5rem]">
                    <p className="line-clamp-2 text-sm text-base-content/70">
                      {activeContextDescription}
                    </p>
                  </div>
                </div>
              </div>

              <div className="inline-flex w-fit rounded-full border border-base-300/60 bg-base-200/60 p-1">
                <button
                  type="button"
                  className={`rounded-full px-4 py-1.5 text-sm font-medium transition ${
                    activeTabKey === "library"
                      ? "bg-base-100 text-base-content shadow-sm"
                      : "text-base-content/70 hover:text-base-content"
                  }`}
                  onClick={() => setActiveTabKey("library")}
                >
                  {tabLabels.library}
                </button>
                <button
                  type="button"
                  className={`rounded-full px-4 py-1.5 text-sm font-medium transition ${
                    activeTabKey === "pending"
                      ? "bg-base-100 text-base-content shadow-sm"
                      : "text-base-content/70 hover:text-base-content"
                  }`}
                  onClick={() => setActiveTabKey("pending")}
                >
                  {tabLabels.pending}
                </button>
              </div>

              <div className="card bg-base-100">
                <div className="card-body gap-4 p-4">
                  <div className="flex flex-wrap items-start justify-between gap-3">
                    <button
                      type="button"
                      className="btn btn-outline btn-sm gap-2"
                      onClick={() => setIsFiltersOpen(true)}
                    >
                      <AdjustmentsHorizontalIcon className="size-4" />
                      {t.translations.SELECT_FILTERS}
                      {activeFilterCount > 0 && (
                        <span className="badge badge-secondary">
                          {activeFilterCount}
                        </span>
                      )}
                    </button>
                  </div>

                  <SearchBar
                    className="w-full"
                    value={activeSearchQuery}
                    onChange={(event) =>
                      updateActiveSearchQuery(event.target.value)
                    }
                    onEnter={updateActiveSearchQuery}
                    onClearAll={clearActiveSearchQuery}
                    placeholder={activeSearchPlaceholder}
                    aditionalFilters={false}
                  />

                  <div className="flex flex-col gap-3">
                    <div className="flex flex-wrap items-center gap-2">
                      {activeFilterPills.length === 0 ? (
                        <span className="text-sm text-base-content/60">
                          {activeTabKey === "library"
                            ? t.translations.PROJECT_INSIGHT_SCOPE_HINT
                            : t.translations
                                .PROJECT_INSIGHT_PENDING_SEARCH_HINT}
                        </span>
                      ) : (
                        activeFilterPills.map((pill) => (
                          <button
                            key={pill.id}
                            type="button"
                            className="btn btn-xs btn-outline gap-1"
                            onClick={() => removeActiveFilterPill(pill)}
                          >
                            {pill.label}
                            <XMarkIcon className="size-3.5" />
                          </button>
                        ))
                      )}
                    </div>

                    {activeSearchError && (
                      <span className="text-sm text-warning">
                        {activeSearchError}
                      </span>
                    )}
                  </div>
                </div>
              </div>

              <div className="min-h-0 flex-1 overflow-y-auto pr-1">
                {activeTabKey === "library" ? libraryContent : pendingContent}
              </div>
            </div>
          </aside>
        </div>
      </div>

      {isFiltersOpen && (
        <dialog className="modal modal-open">
          <div className="modal-box max-w-5xl p-0">
            <div className="flex items-center justify-between border-b border-base-300 px-5 py-4">
              <div>
                <h2 className="text-lg font-semibold text-base-content">
                  {t.translations.PROJECT_INSIGHT_FILTERS}
                </h2>
                <p className="mt-1 text-sm text-base-content/70">
                  {t.translations.PROJECT_INSIGHT_FILTERS_DESCRIPTION}
                </p>
              </div>

              <button
                type="button"
                className="btn btn-sm btn-ghost"
                onClick={() => setIsFiltersOpen(false)}
              >
                {t.translations.CLOSE}
              </button>
            </div>

            <div className="p-5">
              <ProjectInsightFilters
                filters={activeFilters}
                onChange={(patch) =>
                  setActiveTabState((current) => ({
                    ...current,
                    ...patch,
                  }))
                }
                onClear={() =>
                  setActiveTabState((current) => ({
                    ...current,
                    classIds: [],
                    tagIds: [],
                  }))
                }
                classes={classOptions}
                tags={tagOptions}
                totalRecords={activeFilterStats.totalRecords}
                embeddedRecords={activeFilterStats.embeddedRecords}
                pendingRecords={activeFilterStats.pendingRecords}
              />
            </div>
          </div>

          <form method="dialog" className="modal-backdrop">
            <button type="button" onClick={() => setIsFiltersOpen(false)}>
              close
            </button>
          </form>
        </dialog>
      )}
    </div>
  );
}
