"use client";

import React, { useDeferredValue, useEffect, useState } from "react";
import toast from "react-hot-toast";
import {
  AdjustmentsHorizontalIcon,
  XMarkIcon,
} from "@heroicons/react/24/outline";
import SearchBar from "@/app/(home)/components/SearchBar";
import Tabs from "@/app/(home)/components/Tabs";
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

const STATUS_POLL_INTERVAL_MS = 5000;

function withTokens(
  template: string,
  values: Record<string, string | number>,
): string {
  return Object.entries(values).reduce(
    (result, [key, value]) => result.replaceAll(`{${key}}`, String(value)),
    template,
  );
}

function sortNamedOptions<T extends { id: number; name: string }>(
  items: T[],
): NamedInsightOption[] {
  return [...items]
    .filter((item) => item.name.trim().length > 0)
    .map((item) => ({ id: item.id, name: item.name }))
    .sort((left, right) => left.name.localeCompare(right.name));
}

function matchesMetadataSearch(
  record: ProjectInsightRecord,
  normalizedQuery: string,
): boolean {
  if (!normalizedQuery) return true;

  const haystack = [
    record.id,
    record.name,
    record.description,
    record.className,
    record.dataSourceName,
    record.fileType,
    ...record.tags.map((tag) => tag.name),
  ]
    .filter(Boolean)
    .join(" ")
    .toLowerCase();

  return haystack.includes(normalizedQuery);
}

function getStatusFromError(error: unknown): ProjectInsightStatus {
  const message = error instanceof Error ? error.message : "";

  if (/404|not found|not indexed|missing/i.test(message)) {
    return { state: "not_embedded" };
  }

  return {
    state: "error",
    error: message,
  };
}

export default function ProjectInsightClientView() {
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

  const [records, setRecords] = useState<ProjectInsightRecord[]>([]);
  const [classOptions, setClassOptions] = useState<NamedInsightOption[]>([]);
  const [tagOptions, setTagOptions] = useState<NamedInsightOption[]>([]);
  const [statusMap, setStatusMap] = useState<Record<number, ProjectInsightStatus>>(
    {},
  );
  const [filters, setFilters] = useState<ProjectInsightFiltersState>({
    classIds: [],
    tagIds: [],
  });
  const [searchQuery, setSearchQuery] = useState("");
  const [matchedSearchIds, setMatchedSearchIds] = useState<number[] | null>(null);
  const [isSearchLoading, setIsSearchLoading] = useState(false);
  const [searchError, setSearchError] = useState("");
  const [isLoadingRecords, setIsLoadingRecords] = useState(true);
  const [isFiltersOpen, setIsFiltersOpen] = useState(false);
  const [activeTabKey, setActiveTabKey] = useState<"library" | "pending">(
    "library",
  );
  const [selectedPendingIds, setSelectedPendingIds] = useState<number[]>([]);
  const [isQueueing, setIsQueueing] = useState(false);
  const deferredSearchQuery = useDeferredValue(searchQuery);

  useEffect(() => {
    setFilters({ classIds: [], tagIds: [] });
    setSearchQuery("");
    setMatchedSearchIds(null);
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
      setSearchError("");

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
    const searchableQuery = deferredSearchQuery.trim();

    if (!organizationId || !projectId || searchableQuery.length < 2) {
      setMatchedSearchIds(null);
      setIsSearchLoading(false);
      setSearchError("");
      return;
    }

    let cancelled = false;

    const runSearch = async () => {
      setIsSearchLoading(true);
      setMatchedSearchIds(null);
      setSearchError("");

      try {
        const results = await fullTextSearch(
          organizationId,
          searchableQuery,
          [projectId],
        );

        if (cancelled) return;

        const recordIds = [...new Set(results.map((result) => Number(result.id)))].filter(
          (id) => Number.isFinite(id),
        );
        setMatchedSearchIds(recordIds);
      } catch (error) {
        console.error("Project Insight full-text search failed:", error);
        if (!cancelled) {
          setMatchedSearchIds([]);
          setSearchError(t.translations.FAILED_TO_SEARCH_RECORDS);
        }
      } finally {
        if (!cancelled) {
          setIsSearchLoading(false);
        }
      }
    };

    void runSearch();

    return () => {
      cancelled = true;
    };
  }, [
    deferredSearchQuery,
    organizationId,
    projectId,
    t.translations.FAILED_TO_SEARCH_RECORDS,
  ]);

  useEffect(() => {
    const pollingIds = Object.entries(statusMap)
      .filter(([, status]) =>
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

  const projectName = project?.projectName ?? "";
  const filteredRecords = records.filter((record) =>
    matchesInsightFilters(record, filters),
  );
  const filteredSupportedRecords = filteredRecords.filter(
    (record) => record.isInsightSupported,
  );
  const embeddedRecords = filteredRecords.filter(
    (record) => getProjectInsightStatus(record, statusMap).state === "embedded",
  );
  const pendingRecords = filteredSupportedRecords.filter(
    (record) => getProjectInsightStatus(record, statusMap).state !== "embedded",
  );

  const normalizedSearchQuery = searchQuery.trim().toLowerCase();
  const remoteMatchIds =
    normalizedSearchQuery.length >= 2 && matchedSearchIds
      ? new Set(matchedSearchIds)
      : null;

  const visibleEmbeddedRecords = normalizedSearchQuery
    ? embeddedRecords.filter((record) => {
        const metadataMatch = matchesMetadataSearch(record, normalizedSearchQuery);
        const remoteMatch = remoteMatchIds?.has(record.id) ?? false;
        return metadataMatch || remoteMatch;
      })
    : embeddedRecords;

  const queueablePendingRecords = pendingRecords.filter((record) => {
    const status = getProjectInsightStatus(record, statusMap).state;
    return Boolean(record.uri) && (status === "not_embedded" || status === "error");
  });
  const queueablePendingIds = queueablePendingRecords.map((record) => record.id);
  const selectedVisiblePendingIds = selectedPendingIds.filter((recordId) =>
    queueablePendingIds.includes(recordId),
  );

  const activeFilterCount = filters.classIds.length + filters.tagIds.length;
  const activeFilterPills = [
    ...classOptions
      .filter((option) => filters.classIds.includes(option.id))
      .map((option) => ({
        id: `class-${option.id}`,
        label: option.name,
        type: "class" as const,
        optionId: option.id,
      })),
    ...tagOptions
      .filter((option) => filters.tagIds.includes(option.id))
      .map((option) => ({
        id: `tag-${option.id}`,
        label: option.name,
        type: "tag" as const,
        optionId: option.id,
      })),
  ];

  const tabLabels = {
    library: t.translations.PROJECT_INSIGHT_LIBRARY_TAB,
    pending: t.translations.PROJECT_INSIGHT_PENDING_TAB,
  };

  async function handleQueueSelected() {
    if (selectedVisiblePendingIds.length === 0) return;

    const selectedRecords = pendingRecords.filter((record) =>
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
                error instanceof Error ? error.message : t.translations.INSIGHT_ERROR_PREFIX,
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

  const embeddedSearchSummary = normalizedSearchQuery
    ? isSearchLoading
      ? t.translations.PROJECT_INSIGHT_SEARCHING
      : visibleEmbeddedRecords.length > 0
        ? withTokens(t.translations.PROJECT_INSIGHT_SEARCH_RESULTS, {
            count: visibleEmbeddedRecords.length,
            query: searchQuery.trim(),
          })
        : withTokens(t.translations.PROJECT_INSIGHT_SEARCH_RESULTS_EMPTY, {
            query: searchQuery.trim(),
          })
    : t.translations.PROJECT_INSIGHT_LIBRARY_DESCRIPTION;

  const libraryContent = (
    <ProjectInsightRecordSection
      title={t.translations.PROJECT_INSIGHT_EMBEDDED_TITLE}
      description={embeddedSearchSummary}
      count={visibleEmbeddedRecords.length}
      emptyMessage={t.translations.PROJECT_INSIGHT_EMBEDDED_EMPTY}
    >
      <div className="grid gap-4 xl:grid-cols-2">
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
      description={t.translations.PROJECT_INSIGHT_PENDING_DESCRIPTION}
      count={pendingRecords.length}
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
      <div className="grid gap-4 xl:grid-cols-2">
        {pendingRecords.map((record) => {
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
    <div className="min-h-screen bg-base-100 px-4 py-6 lg:px-6">
      <div className="mx-auto flex w-full max-w-[1400px] flex-col gap-6">
        <section className="card border border-base-300/60 bg-gradient-to-r from-base-100 via-base-100 to-base-200/60 shadow-lg">
          <div className="card-body gap-5 p-5 lg:p-6">
            <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
              <div className="max-w-3xl">
                <h1 className="text-2xl font-semibold text-base-content lg:text-3xl">
                  {t.translations.PROJECT_INSIGHT_SCOPE}
                </h1>
                <p className="mt-2 text-sm text-base-content/70 lg:text-base">
                  {withTokens(t.translations.PROJECT_INSIGHT_DESCRIPTION, {
                    projectName,
                  })}
                </p>
              </div>

              <div className="flex flex-wrap items-center gap-2">
                <span className="badge badge-secondary badge-outline">
                  {withTokens(t.translations.PROJECT_INSIGHT_SCOPE_COUNT, {
                    count: visibleEmbeddedRecords.length,
                  })}
                </span>
                <span className="badge badge-ghost">
                  {filteredRecords.length} {t.translations.PROJECT_INSIGHT_FILTERS_RECORDS}
                </span>
              </div>
            </div>

            <div className="grid gap-3 lg:grid-cols-[minmax(0,1fr)_auto]">
              <SearchBar
                className="w-full"
                value={searchQuery}
                onChange={(event) => setSearchQuery(event.target.value)}
                onEnter={(value) => setSearchQuery(value)}
                onClearAll={() => setSearchQuery("")}
                placeholder={t.translations.PROJECT_INSIGHT_SEARCH_PLACEHOLDER}
                aditionalFilters={false}
              />

              <button
                type="button"
                className="btn btn-outline gap-2"
                onClick={() => setIsFiltersOpen(true)}
              >
                <AdjustmentsHorizontalIcon className="size-5" />
                {t.translations.SELECT_FILTERS}
                {activeFilterCount > 0 && (
                  <span className="badge badge-secondary">{activeFilterCount}</span>
                )}
              </button>
            </div>

            <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
              <div className="flex flex-wrap items-center gap-2">
                {activeFilterPills.length === 0 ? (
                  <span className="text-sm text-base-content/60">
                    {t.translations.PROJECT_INSIGHT_SCOPE_HINT}
                  </span>
                ) : (
                  activeFilterPills.map((pill) => (
                    <button
                      key={pill.id}
                      type="button"
                      className="btn btn-xs btn-outline gap-1"
                      onClick={() =>
                        setFilters((current) => ({
                          ...current,
                          classIds:
                            pill.type === "class"
                              ? current.classIds.filter((id) => id !== pill.optionId)
                              : current.classIds,
                          tagIds:
                            pill.type === "tag"
                              ? current.tagIds.filter((id) => id !== pill.optionId)
                              : current.tagIds,
                        }))
                      }
                    >
                      {pill.label}
                      <XMarkIcon className="size-3.5" />
                    </button>
                  ))
                )}
              </div>

              {searchError && (
                <span className="text-sm text-warning">{searchError}</span>
              )}
            </div>
          </div>
        </section>

        <ProjectInsightChat
          projectName={projectName}
          scopedRecordIds={visibleEmbeddedRecords.map((record) => record.id)}
        />

        <Tabs
          activeTab={tabLabels[activeTabKey]}
          onTabChange={(label) =>
            setActiveTabKey(
              label === tabLabels.pending ? "pending" : "library",
            )
          }
          tabs={[
            {
              label: tabLabels.library,
              content: libraryContent,
            },
            {
              label: tabLabels.pending,
              content: pendingContent,
            },
          ]}
        />
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
                filters={filters}
                onChange={(patch) =>
                  setFilters((current) => ({
                    ...current,
                    ...patch,
                  }))
                }
                onClear={() =>
                  setFilters({
                    classIds: [],
                    tagIds: [],
                  })
                }
                classes={classOptions}
                tags={tagOptions}
                totalRecords={filteredRecords.length}
                embeddedRecords={embeddedRecords.length}
                pendingRecords={pendingRecords.length}
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
