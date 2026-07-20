"use client";

import SearchBar from "@/app/(home)/components/SearchBar";
import { useInsightModelSelection } from "@/app/(home)/components/insight/useInsightModelSelection";
import type {
  ClassResponseDto,
  DataSourceResponseDto,
  TagResponseDto,
} from "@/app/(home)/types/responseDTOs";
import { useLanguage } from "@/app/contexts/Language";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import { useProjectSession } from "@/app/contexts/ProjectSessionProvider";
import { getAllClasses } from "@/app/lib/client_service/class_services.client";
import { getAllDataSources } from "@/app/lib/client_service/data_source_services.client";
import {
  fetchInsightIngestionStatus,
  queueInsightUpload,
} from "@/app/lib/client_service/insight_services.client";
import { getAllTags } from "@/app/lib/client_service/tag_services.client";
import {
  AdjustmentsHorizontalIcon,
  XMarkIcon,
} from "@heroicons/react/24/outline";
import { useEffect, useState, useMemo, useCallback } from "react";
import toast from "react-hot-toast";
import ProjectInsightChat from "./components/ProjectInsightChat";
import ProjectInsightFilters from "./components/ProjectInsightFilters";
import ProjectInsightLoadingSkeleton from "./components/ProjectInsightLoadingSkeleton";
import ProjectInsightRecordCard from "./components/ProjectInsightRecordCard";
import ProjectInsightRecordSection from "./components/ProjectInsightRecordSection";
import type {
  NamedInsightOption,
  ProjectInsightFiltersState,
  ProjectInsightStatus,
} from "./components/projectInsight.types";
import {
  getProjectInsightStatus,
} from "./components/projectInsight.utils";
import {
  buildActiveFilterPills,
  getStatusFromError,
  sortNamedOptions,
  withTokens,
} from "./components/projectInsight.view-utils";
import { useProjectInsightTabState } from "./hooks/useProjectInsightTabState";
import { BetaBadge } from "@/app/(home)/components/BetaBadge";
import { useRecordSearchPaginated, useRecordSearch } from "./hooks/useRecordSearch";
import PaginationControls from "../components/PaginationControls";

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
  const [classOptions, setClassOptions] = useState<NamedInsightOption[]>([]);
  const [tagOptions, setTagOptions] = useState<NamedInsightOption[]>([]);
  const [statusMap, setStatusMap] = useState<
    Record<number, ProjectInsightStatus>
  >({});
  const [isQueryModelUnavailable, setIsQueryModelUnavailable] = useState(false);
  const [isUploadModelUnavailable, setIsUploadModelUnavailable] = useState(false);
  const [isEmbeddingModelUnavailable, setIsEmbeddingModelUnavailable] = useState(false);
  const isChatUnavailable = isQueryModelUnavailable || isEmbeddingModelUnavailable;
  const isIngestionUnavailable = isUploadModelUnavailable || isEmbeddingModelUnavailable;  
  const pollingKey = useMemo(
      () =>
          Object.entries(statusMap)
              .filter(
                  ([, status]) =>
                      status.state === "queued" || status.state === "processing",
              )
              .map(([recordId]) => Number(recordId))
              .sort((a, b) => a - b)
              .join(","),
      [statusMap],
  );
  const [isLoadingRecords, setIsLoadingRecords] = useState(false);
  const [isFiltersOpen, setIsFiltersOpen] = useState(false);
  const [activeTabKey, setActiveTabKey] = useState<"library" | "pending">(
    "library",
  );
  const [selectedPendingIds, setSelectedPendingIds] = useState<Map<number, string>>(new Map());
  const [isQueueing, setIsQueueing] = useState(false);
  const [isLibrarySearchLoading, setIsLibrarySearchLoading] = useState(false);
  const { selectedInsightModels, setSelectedInsightModels } =
    useInsightModelSelection(organizationId, projectId);

  const selectedPendingIdsLength = selectedPendingIds.size;

  // Effects
  useEffect(() => {
    setSelectedPendingIds(new Map());
    setActiveTabKey("library");
  }, [projectId]);

  const [classes, setClasses] = useState<ClassResponseDto[] | null>(null);
  const [sources, setSources] = useState<DataSourceResponseDto[] | null>(null);

  const loadRecordMeta = useCallback(async () => {
    if (!projectId) return;

    const [classDtos, dataSourceDtos, tagDtos] =
      await Promise.all([
        getAllClasses(projectId, true),
        getAllDataSources(projectId, true),
        getAllTags(projectId, true),
      ]);

    setClasses(classDtos);
    setSources(dataSourceDtos);

    setClassOptions(sortNamedOptions(classDtos as ClassResponseDto[]));
    setTagOptions(sortNamedOptions(tagDtos as TagResponseDto[]));
  }, [projectId]);

  useEffect(() => {
    loadRecordMeta();
  }, [loadRecordMeta]);

  const pageSize = 10;

  const {
    filters: libraryState,
    setFilters: setLibraryState,
    records: embedded,
    status: embeddedStatus,
    total: embeddedTotal,
    found: embeddedFound,
    error: embeddedError,
  } = useRecordSearch("embedded", classes, sources);

  const {
    page: pendingPage,
    setPage: setPendingPage,
    pageSize: pendingPageSize,
    setPageSize: setPendingPageSize,
    totalPages: pendingTotalPages,
    filters: pendingState,
    setFilters: setPendingState,
    records: pending,
    status: pendingStatus,
    total: pendingTotal,
    found: pendingFound,
    error: pendingError,
  } = useRecordSearchPaginated(pageSize, "pending", classes, sources);

  useEffect(() => {
    setStatusMap({...embeddedStatus, ...pendingStatus});
  }, [embeddedStatus, pendingStatus]);

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

  function retryRecordSearch() {
    // Hacky solution that refreshes the search because the filters have "changed"
    setLibraryState((state) => ({...state}));
    setPendingState((state) => ({...state}));
  }

  useEffect(() => {
    if (!organizationId || !projectId || !pollingKey || isEmbeddingModelUnavailable) return;

    const pollingIds = pollingKey.split(",").map(Number);

    let cancelled = false;

    const pollStatuses = async () => {
      const updatedStatuses: Array<[number, ProjectInsightStatus]> = await Promise.all(
        pollingIds.map(async (recordId) => {
          try {
            const ingestionStatus = await fetchInsightIngestionStatus({
              organizationId,
              projectId,
              fileId: recordId,
            });
            return [
              recordId,
              ingestionStatus.indexed
                ? {
                    state: "embedded",
                    chunkCount: ingestionStatus.chunk_count,
                    pageCount: ingestionStatus.page_count,
                  }
                : { state: "processing" },
            ] as const;
          } catch (error) {
            return [recordId, getStatusFromError(error)] as const;
          }
        }),
      );

      if (cancelled) return;

      setStatusMap((current) => {
        let next: typeof current | undefined;

        for (const [recordId, newStatus] of updatedStatuses) {
          const oldStatus = current[recordId];

          const changed =
              oldStatus?.state !== newStatus.state ||
              oldStatus?.chunkCount !== newStatus.chunkCount ||
              oldStatus?.pageCount !== newStatus.pageCount;

          if (!changed) continue;

          next ??= { ...current };
          next[recordId] = newStatus;

          if (newStatus.state === "embedded")
            retryRecordSearch(); // Embedded records need to be moved to the embedded section
        }

        return next ?? current;
      });
        
    };

    void pollStatuses();
    const interval = window.setInterval(() => {
      void pollStatuses();
    }, STATUS_POLL_INTERVAL_MS);

    return () => {
      cancelled = true;
      window.clearInterval(interval);
    };
  }, [organizationId, projectId, pollingKey, isEmbeddingModelUnavailable]);

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

  useEffect(() => {
    setSelectedPendingIds((current) =>
      new Map([...current].filter(([recordId, _]) => {
        const status = statusMap[recordId];
        return status?.state !== "embedded";
      })),
    );
  }, [statusMap]);

  useEffect(() => {
    setSelectedPendingIds(new Map());
  }, [pendingState]);

  const normalizedLibrarySearchQuery = libraryState.searchQuery
    .trim()
    .toLowerCase();
  const normalizedPendingSearchQuery = pendingState.searchQuery
    .trim()
    .toLowerCase();

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
    activeTabKey === "library" ? embeddedError : pendingError;
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

  const activeFilterStats = {
    totalRecords: embeddedFound + pendingFound,
    embeddedRecords: embeddedFound,
    pendingRecords: pendingFound,
  };

  // UI event handlers
  async function handleQueueSelected() {
    if (isIngestionUnavailable) return;
    if (!organizationId || !projectId) return;

    const uploadFileInfo = [...selectedPendingIds]
      .map(([id, uri]) => ({ fileId: id, fileUri: uri }));

    if (uploadFileInfo.length === 0) return;

    setIsQueueing(true);
    setStatusMap((current) => ({
      ...current,
      ...Object.fromEntries(
        uploadFileInfo.map((file) => [file.fileId, { state: "queued" }]),
      ),
    }));

    try {
      await queueInsightUpload({
        organizationId,
        projectId,
        fileInfo: uploadFileInfo,
        vlmModelConfigId:
          selectedInsightModels.uploadModelConfigId ?? undefined,
        embeddingModelConfigId:
          selectedInsightModels.embeddingModelConfigId ?? undefined,
      });
      toast.success(
        withTokens(t.translations.PROJECT_INSIGHT_QUEUED_SUMMARY, {
          count: uploadFileInfo.length,
        }),
      );
    } catch (error) {
      console.error("Failed to queue project Insight uploads:", error);
      setStatusMap((current) => ({
        ...current,
        ...Object.fromEntries(
          uploadFileInfo.map((file) => [
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
          count: uploadFileInfo.length,
        }),
      );
    } finally {
      setSelectedPendingIds(new Map());
      setIsQueueing(false);
    }
  }

  // Render summaries
  const embeddedSearchSummary = normalizedLibrarySearchQuery
    ? isLibrarySearchLoading
      ? t.translations.PROJECT_INSIGHT_SEARCHING
      : embeddedTotal
        ? withTokens(t.translations.PROJECT_INSIGHT_SEARCH_RESULTS, {
            count: embeddedTotal,
            query: libraryState.searchQuery.trim(),
          })
        : withTokens(t.translations.PROJECT_INSIGHT_SEARCH_RESULTS_EMPTY, {
            query: libraryState.searchQuery.trim(),
          })
    : t.translations.PROJECT_INSIGHT_LIBRARY_DESCRIPTION;
  const pendingSearchSummary = normalizedPendingSearchQuery
    ? withTokens(t.translations.PROJECT_INSIGHT_PENDING_SEARCH_RESULTS, {
        count: pendingTotal,
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
      ? embeddedTotal
      : pendingTotal;

  // Render content
  const libraryContent = (
    <ProjectInsightRecordSection
      title={t.translations.PROJECT_INSIGHT_EMBEDDED_TITLE}
      description={embeddedSearchSummary}
      count={embeddedTotal}
      emptyMessage={t.translations.PROJECT_INSIGHT_EMBEDDED_EMPTY}
    >
      <div className="space-y-1">
        {embedded.map((record) => (
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
      count={pendingTotal}
      emptyMessage={t.translations.PROJECT_INSIGHT_PENDING_EMPTY}
      actions={
        pending.length > 0 ? (
          <div className="flex flex-wrap items-center gap-2">
            <button
              type="button"
              className="btn btn-sm btn-ghost"
              onClick={() => setSelectedPendingIds((current) =>
                (new Map([...current, ...pending.map(r => [r.id, r.uri ?? ""] as const)])))}
            >
              {t.translations.PROJECT_INSIGHT_SELECT_ALL_VISIBLE}
            </button>
            <button
              type="button"
              className="btn btn-sm btn-ghost"
              onClick={() => setSelectedPendingIds(new Map())}
              disabled={selectedPendingIdsLength === 0}
            >
              {t.translations.PROJECT_INSIGHT_CLEAR_SELECTION}
            </button>
            <button
              type="button"
              className="btn btn-sm btn-primary"
              onClick={() => void handleQueueSelected()}
              disabled={isIngestionUnavailable || selectedPendingIdsLength === 0 || isQueueing}
            >
              {isQueueing
                ? t.translations.UPLOADING
                : t.translations.PROJECT_INSIGHT_EMBED_SELECTED}
            </button>
            {selectedPendingIdsLength > 0 && (
              <span className="badge badge-outline badge-secondary">
                {withTokens(t.translations.PROJECT_INSIGHT_SELECTED_COUNT, {
                  count: selectedPendingIdsLength,
                  total: pendingFound,
                })}
              </span>
            )}
          </div>
        ) : undefined
      }
    >
      <div className="space-y-1">
        {pending.map((record) => {
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
              checked={selectedPendingIds.has(record.id)}
              onToggle={(recordId, recordUri) =>
                setSelectedPendingIds((current) =>
                  current.has(recordId)
                    ? new Map([...current].filter(([id, _]) => id !== recordId))
                    : new Map([...current, [recordId, recordUri]]),
                )
              }
            />
          );
        })}
      </div>
    </ProjectInsightRecordSection>
  );

  if (!hasProjectLoaded || !hasOrganizationLoaded || isLoadingRecords) {
    return <ProjectInsightLoadingSkeleton />;
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
    <main className="flex h-[calc(100dvh-7rem)] min-h-0 flex-col overflow-hidden bg-base-200/30">
      <section className="border-b border-base-300 bg-base-100">
        <div className="mx-auto flex w-full max-w-7xl flex-col gap-5 px-3 py-5 sm:px-6 lg:px-8">
          <div className="flex flex-wrap items-center justify-between gap-4">
            <div className="max-w-4xl">
              <p className="text-xs font-semibold uppercase tracking-wide text-base-content/60">
                {t.translations.PROJECT}
              </p>
              <div className="flex flex-wrap items-center gap-3">
                <h1 className="text-2xl font-bold text-base-content sm:text-3xl">
                  {t.translations.PROJECT_INSIGHT_SCOPE}
                </h1>
                <BetaBadge size="sm" />
                {isQueryModelUnavailable && (
                    <span className="badge badge-warning badge-sm">
                      Query model unavailable
                    </span>
                )}
                {isUploadModelUnavailable && (
                    <span className="badge badge-warning badge-sm">
                      Upload/OCR model unavailable
                    </span>
                )}
                {isEmbeddingModelUnavailable && (
                    <span className="badge badge-warning badge-sm">
                      Embedding model unavailable
                    </span>
                )}
              </div>
              <p className="mt-3 text-base-content/70">
                {withTokens(t.translations.PROJECT_INSIGHT_DESCRIPTION, {
                  projectName,
                })}
              </p>
            </div>
          </div>
        </div>
      </section>

      <section className="mx-auto flex min-h-0 w-full max-w-7xl flex-1 overflow-hidden px-3 py-5 sm:px-6 lg:px-8">
        <div className="grid h-full min-h-0 grid-cols-1 gap-6 overflow-y-auto pr-1 xl:grid-cols-[minmax(0,1.7fr)_minmax(340px,1fr)]">
          <section className="flex min-h-0 flex-col">
            <ProjectInsightChat
              organizationId={organizationId}
              projectId={projectId}
              projectName={projectName}
              selectedInsightModels={selectedInsightModels}
              onSelectedInsightModelsChange={setSelectedInsightModels}
              scopedRecordIds={embedded.map(
                (record) => record.id,
              )}
              isChatUnavailable={isChatUnavailable}
            />
          </section>

          <aside className="card card-border bg-base-100 shadow-md shadow-base-content/10 xl:h-full xl:min-h-0">
            <div className="card-body h-full min-h-0 gap-1 p-4 sm:p-5">
              {/* <div className="flex flex-wrap items-start justify-between gap-3">
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
              </div> */}

              <div className="inline-flex w-fit rounded-full border border-base-300/60 bg-base-200/60 p-1">
                <button
                  type="button"
                  className={`flex gap-3 items-center rounded-full px-4 py-1.5 text-sm font-medium transition ${
                    activeTabKey === "library"
                      ? "bg-base-100 text-base-content shadow-sm"
                      : "text-base-content/70 hover:text-base-content"
                  }`}
                  onClick={() => setActiveTabKey("library")}
                >
                  {tabLabels.library}
                  <span className="badge badge-secondary badge-sm shrink-0">
                    {embeddedTotal}
                  </span>
                </button>
                <button
                  type="button"
                  className={`flex gap-3 items-center rounded-full px-4 py-1.5 text-sm font-medium transition ${
                    activeTabKey === "pending"
                      ? "bg-base-100 text-base-content shadow-sm"
                      : "text-base-content/70 hover:text-base-content"
                  }`}
                  onClick={() => setActiveTabKey("pending")}
                >
                  {tabLabels.pending}
                  <span className="badge badge-secondary badge-sm shrink-0">
                    {pendingTotal}
                  </span>
                </button>
              </div>

              <div className="card bg-base-100">
                <div className="card-body gap-2 p-4">
                  <div className="flex flex-wrap items-center gap-3">
                    <SearchBar
                      className="flex-1 min-w-[160px]"
                      value={activeSearchQuery}
                      onChange={(event) => updateActiveSearchQuery(event.target.value)}
                      onEnter={updateActiveSearchQuery}
                      onClearAll={clearActiveSearchQuery}
                      placeholder={activeSearchPlaceholder}
                      aditionalFilters={false}
                    />

                    <button
                      type="button"
                      className="btn btn-outline btn-sm gap-2 shrink-0"
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

                  {(activeFilterPills.length !== 0 || activeSearchError) &&
                  <div className="flex flex-col gap-3">
                      {activeFilterPills.length === 0 ?
                      null : (
                        <div className="flex flex-wrap items-center gap-2">
                          {activeFilterPills.map((pill) => (
                            <button
                              key={pill.id}
                              type="button"
                              className="btn btn-xs btn-outline gap-1"
                              onClick={() => removeActiveFilterPill(pill)}
                            >
                              {pill.label}
                              <XMarkIcon className="size-3.5" />
                            </button>
                          ))}
                        </div>
                      )}

                    {activeSearchError && (
                      <span className="text-sm text-warning">
                        {activeSearchError}
                      </span>
                    )}
                  </div>}
                </div>
              </div>

              {activeTabKey === "library" ? libraryContent : pendingContent}

              {(activeTabKey !== "library") &&
                <PaginationControls
                  currentPage={pendingPage}
                  pageSize={pendingPageSize}
                  totalPages={pendingTotalPages}
                  onPageChange={setPendingPage}
                  onPageSizeChange={setPendingPageSize}
                />}
            </div>
          </aside>
        </div>
      </section>

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
    </main>
  );
}
