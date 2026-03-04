"use client";

import { useLanguage } from "@/app/contexts/Language";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import { useProjectSession } from "@/app/contexts/ProjectSessionProvider";
import {
  cancelChunkedUpload,
  cancelCurrentUpload,
  CHUNK_THRESHOLD,
  uploadFile,
} from "@/app/lib/client_service/file_upload_services.client";
import { updateFile } from "@/app/lib/client_service/file_services.client";
import { uploadBulkMetadata } from "@/app/lib/client_service/metadata_service.client";
import { fullTextSearch } from "@/app/lib/client_service/query_services.client";
import { getAllRecords } from "@/app/lib/client_service/record_services.client";
import { uploadTimeseriesFile } from "@/app/lib/client_service/timeseries_services.client";
import { parseBackendErrors } from "@/app/lib/error_parser";
import { createUploadToastManager } from "@/app/lib/uploadToastManager";
import { useCallback, useEffect, useMemo, useState } from "react";
import toast from "react-hot-toast";
import { useBulkUploadState } from "./hooks/useBulkUploadState";
import { useProjectResources } from "./hooks/useProjectResources";
import { useUploadState } from "./hooks/useUploadState";
import {
  ArrowUpOnSquareStackIcon,
  DocumentIcon,
} from "@heroicons/react/24/outline";
import type { ExistingFile, UploadProgressEvent } from "../types/types";
import BulkUploadSection from "./components/BulkUploadSection";
import FileUploadSection from "./components/FileUploadSection";
import ProjectResourceSelectors from "./components/ProjectResourceSelectors";
import MetadataTemplateDownload from "./components/MetadataTemplateDownload";

const MAX_CONCURRENT_FILE_UPLOADS = 5;
const MULTI_FILE_PROGRESS_TOAST_THRESHOLD = 30;

function mapRecordToExistingFile(record: {
  id?: number | string | null;
  name?: string | null;
  description?: string | null;
  lastUpdatedAt?: string | null;
  lastUpdatedBy?: string | null;
  dataSourceName?: string | null;
}): ExistingFile | null {
  if (record.id == null) return null;
  return {
    id: String(record.id),
    name: record.name?.trim() || String(record.id),
    description: record.description ?? undefined,
    lastUpdate: record.lastUpdatedAt ?? undefined,
    updatedBy: record.lastUpdatedBy ?? undefined,
    dataSource: record.dataSourceName ?? undefined,
  };
}

function dedupeExistingFiles(files: ExistingFile[]): ExistingFile[] {
  const map = new Map<string, ExistingFile>();
  for (const file of files) {
    map.set(String(file.id), file);
  }
  return Array.from(map.values());
}

function interpolate(
  template: string,
  values: Record<string, string | number>,
): string {
  return Object.entries(values).reduce(
    (result, [key, value]) => result.replace(`{${key}}`, String(value)),
    template,
  );
}

export default function UploadCenterClient() {
  const { t } = useLanguage();
  const { organization } = useOrganizationSession();
  const { project: sessionProject, hasLoaded: hasLoadedProjectSession } =
    useProjectSession();
  const organizationId = organization?.organizationId;
  const numericOrganizationId =
    organizationId !== undefined ? Number(organizationId) : undefined;

  const fileUploadState = useUploadState();
  const bulkUploadState = useBulkUploadState();
  const projectResources = useProjectResources(numericOrganizationId);
  const uploadToastManager = useMemo(() => createUploadToastManager(), []);
  const { setTargetFileId } = fileUploadState;
  const {
    projectId,
    dataSourceId,
    objectStorageId,
    projects,
    dataSources,
    setProjectId,
  } = projectResources;
  const selectedFiles = fileUploadState.selectedFiles;
  const [availableFiles, setAvailableFiles] = useState<ExistingFile[]>([]);

  useEffect(() => {
    if (!organizationId || !projectId) {
      setAvailableFiles([]);
      return;
    }

    let cancelled = false;
    (async () => {
      try {
        const records = await getAllRecords(
          Number(organizationId),
          Number(projectId),
          undefined,
          undefined,
          true,
        );
        if (cancelled) return;

        const mapped = dedupeExistingFiles(
          records
            .map((record) => mapRecordToExistingFile(record))
            .filter((record): record is ExistingFile => record !== null),
        );
        setAvailableFiles(mapped);
      } catch (error) {
        console.error("Error loading records for update picker:", error);
        if (!cancelled) setAvailableFiles([]);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [organizationId, projectId]);

  useEffect(() => {
    if (projectId) return;
    if (!hasLoadedProjectSession) return;

    const sessionProjectId = sessionProject?.projectId;
    if (!sessionProjectId) return;

    const sessionProjectIdString = String(sessionProjectId);
    const existsInProjects = projects.some(
      (project) => String(project.id) === sessionProjectIdString,
    );
    if (existsInProjects) {
      setProjectId(sessionProjectIdString);
    }
  }, [
    projectId,
    hasLoadedProjectSession,
    sessionProject,
    projects,
    setProjectId,
  ]);

  const handleSearchAvailableFiles = useCallback(
    async (query: string): Promise<ExistingFile[]> => {
      const trimmedQuery = query.trim();
      if (!trimmedQuery) return availableFiles;
      if (!organizationId || !projectId) return [];

      try {
        const results = await fullTextSearch(
          Number(organizationId),
          trimmedQuery,
          [Number(projectId)],
        );

        return dedupeExistingFiles(
          results
            .map((record) => mapRecordToExistingFile(record))
            .filter((record): record is ExistingFile => record !== null),
        );
      } catch (error) {
        console.error("Error searching records for update picker:", error);
        return [];
      }
    },
    [availableFiles, organizationId, projectId],
  );

  const needsTarget =
    fileUploadState.uploadType === "version" ||
    fileUploadState.uploadType === "properties";
  const selectedMetadata = selectedFiles.map(
    (_, idx) => fileUploadState.filesMetadata[idx] ?? {},
  );
  const hasAnyNonTimeseriesNewFiles = selectedMetadata.some(
    (metadata) =>
      (metadata.recordMode ?? "new") === "new" && !metadata.isTimeSeries,
  );
  const hasUpdateRecordsMissingTarget = selectedMetadata.some(
    (metadata) =>
      (metadata.recordMode ?? "new") === "update" && !metadata.targetRecordId,
  );
  const requiresObjectStorage = hasAnyNonTimeseriesNewFiles;

  const canUpload =
    selectedFiles.length > 0 &&
    !!projectId &&
    !!dataSourceId &&
    (!requiresObjectStorage || !!objectStorageId) &&
    !hasUpdateRecordsMissingTarget &&
    (!needsTarget || !!fileUploadState.targetFileId);

  // Clear target file when not needed
  useEffect(() => {
    if (!needsTarget) setTargetFileId("");
  }, [needsTarget, setTargetFileId]);

  const handleFileUpload = async () => {
    if (!organizationId || !projectId || selectedFiles.length === 0) {
      toast.error(t.translations.SELECT_A_PROJECT_AND_AT_LEAST_ONE_FILE);
      return;
    }

    fileUploadState.setIsUploading(true);
    fileUploadState.setUploadProgress(null);

    let latestProgress: UploadProgressEvent | null = null;
    let cancelling = false;
    const selectedSingleFile = selectedFiles[0];
    const selectedMetadata = fileUploadState.filesMetadata[0];
    const showChunkedProgressToast =
      selectedFiles.length === 1 &&
      !!selectedSingleFile &&
      selectedMetadata?.recordMode !== "update" &&
      !selectedMetadata?.isTimeSeries &&
      selectedSingleFile.size > CHUNK_THRESHOLD;
    const uploadContext = {
      organizationId,
      projectId,
      dataSourceId,
      objectStorageId,
    };

    if (showChunkedProgressToast) {
      uploadToastManager.show({
        title: t.translations.UPLOADING_FILE,
        message: t.translations.PREPARING_UPLOAD,
      });
    }

    const showProgressToast = (progress: UploadProgressEvent) => {
      uploadToastManager.show({
        title: t.translations.UPLOADING_FILE,
        message: `${progress.chunksCompleted} / ${progress.totalChunks} ${
          t.translations.CHUNKS
        }`,
        percent: progress.percentComplete,
        chunksCompleted: progress.chunksCompleted,
        totalChunks: progress.totalChunks,
        isCancelling: cancelling,
        onCancel: progress.uploadId ? cancelFromToast : undefined,
        cancelDisabled: cancelling,
      });
    };

    const cancelFromToast = async () => {
      if (cancelling) return;
      if (!latestProgress?.uploadId) return;

      cancelling = true;
      showProgressToast(latestProgress);
      cancelCurrentUpload();

      try {
        await cancelChunkedUpload({
          ...uploadContext,
          uploadId: latestProgress.uploadId,
        });
      } catch (err) {
        console.error("Failed to cleanup cancelled upload:", err);
      }
    };

    try {
      if (selectedFiles.length > 1) {
        const shouldShowMultiFileProgressToast =
          selectedFiles.length >= MULTI_FILE_PROGRESS_TOAST_THRESHOLD;
        const results: PromiseSettledResult<unknown>[] = Array(
          selectedFiles.length,
        );
        let completed = 0;
        let succeeded = 0;
        let failed = 0;
        let nextFileIndex = 0;

        if (shouldShowMultiFileProgressToast) {
          uploadToastManager.show({
            title: t.translations.UPLOADING_FILES,
            message: `0 / ${selectedFiles.length} ${t.translations.FILES_LABEL}`,
            percent: 0,
          });
        }

        const uploadWorker = async (): Promise<void> => {
          while (true) {
            const currentIndex = nextFileIndex++;
            if (currentIndex >= selectedFiles.length) return;

            const file = selectedFiles[currentIndex];
            const metadata = fileUploadState.filesMetadata[currentIndex] ?? {};

            try {
              if ((metadata.recordMode ?? "new") === "update") {
                if (!metadata.targetRecordId) {
                  throw new Error("Missing target record id for file update");
                }
                const value = await updateFile(
                  Number(organizationId),
                  Number(projectId),
                  Number(metadata.targetRecordId),
                  file,
                );
                results[currentIndex] = { status: "fulfilled", value };
              } else if (metadata.isTimeSeries) {
                const value = await uploadTimeseriesFile(
                  Number(organizationId),
                  Number(projectId),
                  Number(dataSourceId),
                  file,
                );
                results[currentIndex] = { status: "fulfilled", value };
              } else {
                const value = await uploadFile({
                  ...uploadContext,
                  file,
                  name: metadata.name || file.name,
                  description: metadata.description || "",
                  metadataFile: metadata.metadataFile,
                });
                results[currentIndex] = { status: "fulfilled", value };
              }
              succeeded += 1;
            } catch (reason) {
              results[currentIndex] = { status: "rejected", reason };
              failed += 1;
            } finally {
              completed += 1;
              if (shouldShowMultiFileProgressToast) {
                uploadToastManager.show({
                  title: t.translations.UPLOADING_FILES,
                  message: `${completed} / ${selectedFiles.length} ${t.translations.FILES_LABEL}`,
                  percent: (completed / selectedFiles.length) * 100,
                });
              }
            }
          }
        };

        const workers = Array.from(
          {
            length: Math.min(MAX_CONCURRENT_FILE_UPLOADS, selectedFiles.length),
          },
          () => uploadWorker(),
        );
        await Promise.all(workers);

        uploadToastManager.success(
          failed
            ? interpolate(t.translations.UPLOAD_BATCH_SUCCESS_WITH_FAILURES, {
                success: succeeded,
                failed,
              })
            : interpolate(t.translations.UPLOAD_BATCH_SUCCESS, {
                success: succeeded,
              }),
        );
        if (failed) console.warn("Batch upload failures:", results);

        fileUploadState.resetFileUpload();

        return;
      }

      const file = selectedFiles[0];
      const metadata = fileUploadState.filesMetadata[0] ?? {};

      if ((metadata.recordMode ?? "new") === "update") {
        if (!metadata.targetRecordId) {
          toast.error(t.translations.PLEASE_SELECT_RECORD_TO_UPDATE);
          return;
        }
        await updateFile(
          Number(organizationId),
          Number(projectId),
          Number(metadata.targetRecordId),
          file,
        );
        uploadToastManager.success(
          t.translations.RECORD_FILE_UPDATED_SUCCESSFULLY,
        );
      } else if (metadata.isTimeSeries) {
        await uploadTimeseriesFile(
          Number(organizationId),
          Number(projectId),
          Number(dataSourceId),
          file,
        );
        uploadToastManager.success(
          t.translations.TIMESERIES_FILE_UPLOADED_SUCCESSFULLY,
        );
      } else {
        await uploadFile({
          ...uploadContext,
          file,
          name: metadata.name || file.name,
          description: metadata.description || "",
          metadataFile: metadata.metadataFile,
          onProgress: (progress) => {
            latestProgress = progress;
            fileUploadState.setUploadProgress(progress);
            if (showChunkedProgressToast) showProgressToast(progress);
          },
        });
        uploadToastManager.success(t.translations.FILE_UPLOADED_SUCCESSFULLY);
      }

      fileUploadState.resetFileUpload();
    } catch (err) {
      if (err instanceof DOMException && err.name === "AbortError") {
        uploadToastManager.message(t.translations.UPLOAD_CANCELLED);
      } else {
        console.error("Upload error:", err);
        uploadToastManager.error(
          t.translations.UPLOAD_FAILED_SEE_CONSOLE_FOR_DETAILS,
        );
      }
      fileUploadState.setUploadProgress(null);
    } finally {
      fileUploadState.setIsUploading(false);
    }
  };

  const handleBulkUpload = async () => {
    if (
      !bulkUploadState.validationResult ||
      !bulkUploadState.validationResult.isValid
    ) {
      toast.error(t.translations.PLEASE_FIX_VALIDATION_ERRORS_BEFORE_UPLOADING);
      return;
    }

    if (!projectId || !dataSourceId) {
      toast.error(t.translations.PLEASE_SELECT_PROJECT_AND_DATASOURCE);
      return;
    }

    if (!organizationId) {
      toast.error(t.translations.ORGANIZATION_NOT_FOUND);
      return;
    }

    bulkUploadState.setIsUploading(true);
    bulkUploadState.setBackendErrors([]);
    bulkUploadState.setUploadProgress(0);
    let progressInterval: ReturnType<typeof setInterval> | null = null;

    try {
      progressInterval = setInterval(() => {
        bulkUploadState.setUploadProgress((prev) => {
          if (prev >= 90) return prev;
          return prev + 10;
        });
      }, 200);

      await uploadBulkMetadata(
        Number(organizationId),
        Number(projectId),
        Number(dataSourceId),
        bulkUploadState.validationResult.validRecords,
      );

      bulkUploadState.setUploadProgress(100);
      await new Promise((resolve) => setTimeout(resolve, 500));

      toast.success(
        interpolate(t.translations.BULK_UPLOAD_SUCCESS_WITH_COUNT, {
          count: bulkUploadState.validationResult.validCount,
        }),
      );

      bulkUploadState.resetBulkUpload();
    } catch (error: unknown) {
      console.error("Upload error:", error);

      bulkUploadState.setUploadProgress(0);
      const errorMessages = extractErrorMessages(
        error,
        t.translations.UNKNOWN_ERROR_OCCURRED,
        t.translations.UNKNOWN_ERROR,
      );
      const parsedErrors = parseBackendErrors(errorMessages);
      bulkUploadState.setBackendErrors(parsedErrors);

      toast.error(
        t.translations.UPLOAD_FAILED_PLEASE_CHECK_ERROR_DETAILS_BELOW,
      );
    } finally {
      if (progressInterval) {
        clearInterval(progressInterval);
      }
      bulkUploadState.setIsUploading(false);
    }
  };

  return (
    <div className="min-h-screen bg-base-100">
      <header className="bg-base-200/50 border-b border-base-300/30">
        <div className="px-4 sm:px-6 lg:px-12 py-6">
          <div className="flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
            <div>
              <h1 className="text-2xl font-bold text-base-content">
                {t.translations.UPLOAD_CENTER}
              </h1>
              <p className="text-sm text-base-content/70 mt-1">
                {t.translations.UPLOAD_CENTER_DESCRIPTION}
              </p>
            </div>

            <div className="flex flex-wrap gap-2">
              <span className="badge badge-outline">
                {fileUploadState.uploadMode === "file"
                  ? t.translations.FILE_UPLOAD
                  : t.translations.BULK_METADATA}
              </span>
              <span className="badge badge-outline">
                {selectedFiles.length} {t.translations.FILES_LABEL}
              </span>
            </div>
          </div>
        </div>
      </header>

      <main className="px-4 sm:px-6 lg:px-12 py-6">
        <div className="mx-auto w-full max-w-5xl">
          <section className="w-full">
            <div className="card bg-base-100 shadow-xl">
              <div className="card-body space-y-6">
                <div className="space-y-3">
                  <div className="flex items-center gap-2">
                    <span className="badge badge-primary badge-sm">1</span>
                    <h2 className="text-lg font-semibold text-base-content">
                      {t.translations.UPLOAD_MODE}
                    </h2>
                  </div>

                  <div
                    role="radiogroup"
                    aria-label={t.translations.UPLOAD_WORKFLOW_VIEW_ARIA}
                    className="inline-flex rounded-full border border-base-300/70 bg-base-200/50 p-1"
                  >
                    <button
                      type="button"
                      role="radio"
                      aria-checked={fileUploadState.uploadMode === "file"}
                      className={`flex items-center gap-2 rounded-full px-4 py-2 text-sm font-medium transition ${
                        fileUploadState.uploadMode === "file"
                          ? "bg-base-100 text-base-content shadow-sm"
                          : "text-base-content/70 hover:text-base-content"
                      }`}
                      onClick={() => {
                        fileUploadState.setUploadMode("file");
                        bulkUploadState.setCsvFile(null);
                      }}
                    >
                      <DocumentIcon className="size-4 opacity-80" />
                      {t.translations.FILE_UPLOAD}
                    </button>

                    <button
                      type="button"
                      role="radio"
                      aria-checked={fileUploadState.uploadMode === "bulk"}
                      className={`flex items-center gap-2 rounded-full px-4 py-2 text-sm font-medium transition ${
                        fileUploadState.uploadMode === "bulk"
                          ? "bg-base-100 text-base-content shadow-sm"
                          : "text-base-content/70 hover:text-base-content"
                      }`}
                      onClick={() => {
                        fileUploadState.setUploadMode("bulk");
                        fileUploadState.resetFileUpload();
                      }}
                    >
                      <ArrowUpOnSquareStackIcon className="size-4 opacity-80" />
                      {t.translations.BULK_METADATA}
                    </button>
                  </div>
                </div>

                <div className="divider my-0" />

                <div className="space-y-3">
                  <div className="flex items-center gap-2">
                    <span className="badge badge-primary badge-sm">2</span>
                    <h2 className="text-lg font-semibold text-base-content">
                      {t.translations.PROJECT} / {t.translations.DATA_SOURCE}
                    </h2>
                  </div>

                  <div className="p-4">
                    <ProjectResourceSelectors
                      {...projectResources}
                      hasOrganization={!!organization}
                      uploadMode={fileUploadState.uploadMode}
                    />
                  </div>
                </div>

                <div className="divider my-0" />

                <div className="space-y-3">
                  <div className="flex justify-between">
                    <div className="flex gap-2 items-center">
                      <span className="badge badge-primary badge-sm">3</span>
                      <h2 className="text-lg font-semibold text-base-content">
                        {fileUploadState.uploadMode === "file"
                          ? t.translations.FILE_UPLOAD
                          : t.translations.BULK_METADATA}
                      </h2>
                    </div>
                    <div className="mb-2 flex justify-end">
                      <MetadataTemplateDownload />
                    </div>
                  </div>

                  <div className="bg-base-100 p-4">
                    {fileUploadState.uploadMode === "file" ? (
                      <FileUploadSection
                        selectedFiles={selectedFiles}
                        setSelectedFiles={fileUploadState.setSelectedFiles}
                        dropKey={fileUploadState.dropKey}
                        handleMetadataChange={
                          fileUploadState.handleMetadataChange
                        }
                        targetFileId={fileUploadState.targetFileId}
                        setTargetFileId={fileUploadState.setTargetFileId}
                        availableFiles={availableFiles}
                        onSearchFiles={handleSearchAvailableFiles}
                        needsTarget={needsTarget}
                        isUploading={fileUploadState.isUploading}
                        canUpload={canUpload}
                        onUpload={handleFileUpload}
                        onClear={fileUploadState.clearAll}
                        onRemoveAt={fileUploadState.removeAt}
                      />
                    ) : (
                      <BulkUploadSection
                        {...bulkUploadState}
                        projectId={projectId}
                        dataSourceId={dataSourceId}
                        organizationId={organizationId as number}
                        projects={projects}
                        dataSources={dataSources}
                      />
                    )}
                  </div>
                </div>
              </div>
            </div>
          </section>
        </div>
      </main>

      {/* MODALS */}

      {/* Upload Confirmation Modal */}
      {bulkUploadState.showUploadConfirm &&
        bulkUploadState.validationResult && (
          <div className="modal modal-open">
            <div className="modal-box">
              <h3 className="font-bold text-lg">
                {t.translations.CONFIRM_BULK_UPLOAD}
              </h3>
              <p className="py-4">
                {t.translations.YOUR_ABOUT_TO_UPLOAD}{" "}
                <span className="font-bold">
                  {bulkUploadState.validationResult.validCount}{" "}
                  {t.translations.L_RECORDS}
                </span>{" "}
                {t.translations.TO_THE_SYSTEM}
              </p>
              <div className="bg-base-200 p-3 rounded text-sm space-y-1">
                <p>
                  <strong>{t.translations.PROJECT}:</strong>{" "}
                  {projects.find((p) => p.id === Number(projectId))?.name}
                </p>
                <p>
                  <strong>{t.translations.DATA_SOURCE}:</strong>{" "}
                  {dataSources.find((d) => d.id === Number(dataSourceId))?.name}
                </p>
              </div>
              <div className="modal-action">
                <button
                  className="btn btn-ghost"
                  onClick={() => bulkUploadState.setShowUploadConfirm(false)}
                  disabled={bulkUploadState.isUploading}
                >
                  {t.translations.CANCEL}
                </button>
                <button
                  className="btn btn-primary"
                  onClick={() => {
                    bulkUploadState.setShowUploadConfirm(false);
                    handleBulkUpload();
                  }}
                  disabled={bulkUploadState.isUploading}
                >
                  {bulkUploadState.isUploading ? (
                    <>
                      <span className="loading loading-spinner loading-sm"></span>
                      {t.translations.UPLOADING}
                    </>
                  ) : (
                    t.translations.CONFIRM_UPLOAD
                  )}
                </button>
              </div>
            </div>
            <div
              className="modal-backdrop"
              onClick={() =>
                !bulkUploadState.isUploading &&
                bulkUploadState.setShowUploadConfirm(false)
              }
            />
          </div>
        )}
    </div>
  );
}

type ErrorResponseData = {
  errors?: unknown[];
  error?: unknown;
  message?: unknown;
};

function extractErrorMessages(
  error: unknown,
  unknownErrorOccurred: string,
  unknownError: string,
): string[] {
  const fallback = [unknownErrorOccurred];

  if (typeof error !== "object" || error === null) {
    return fallback;
  }

  const maybeError = error as {
    response?: { data?: unknown };
    message?: unknown;
  };

  const data = maybeError.response?.data;
  if (typeof data === "string") {
    return [data];
  }

  if (typeof data === "object" && data !== null) {
    const d = data as ErrorResponseData;

    if (Array.isArray(d.errors)) {
      return d.errors.map((err) => toMessage(err, unknownError));
    }

    if (d.error !== undefined) {
      return [toMessage(d.error, unknownError)];
    }

    if (typeof d.message === "string") {
      return [d.message];
    }

    return [JSON.stringify(d)];
  }

  if (typeof maybeError.message === "string") {
    return [maybeError.message];
  }

  return fallback;
}

function toMessage(value: unknown, unknownError: string): string {
  if (typeof value === "string") return value;
  if (typeof value === "object" && value !== null) {
    const maybe = value as { message?: unknown };
    if (typeof maybe.message === "string") return maybe.message;
  }
  try {
    return JSON.stringify(value);
  } catch {
    return unknownError;
  }
}
