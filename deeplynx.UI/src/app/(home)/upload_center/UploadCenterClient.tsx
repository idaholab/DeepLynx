"use client";

import { useLanguage } from "@/app/contexts/Language";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import {
  BatchUploadProgressEvent,
  cancelChunkedUpload,
  cancelCurrentUpload,
  CHUNK_THRESHOLD,
  uploadFile,
  uploadFilesBatch,
} from "@/app/lib/client_service/file_upload_services.client";
import { uploadBulkMetadata } from "@/app/lib/client_service/metadata_service.client";
import { uploadTimeseriesFile } from "@/app/lib/client_service/timeseries_services.client";
import { parseBackendErrors } from "@/app/lib/error_parser";
import { createUploadToastManager } from "@/app/lib/uploadToastManager";
import { useEffect, useMemo } from "react";
import toast from "react-hot-toast";
import FileDetailsCard from "../components/FileDetailCard";
import SelectedFilesCard from "../components/SelectedFilesCard";
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

type Props = {
  initialAvailableFiles: ExistingFile[];
};

const MAX_CONCURRENT_FILE_UPLOADS = 5;
const MULTI_FILE_PROGRESS_TOAST_THRESHOLD = 30;

export default function UploadCenterClient({ initialAvailableFiles }: Props) {
  const { t } = useLanguage();
  const { organization } = useOrganizationSession();
  const organizationId = organization?.organizationId;

  const fileUploadState = useUploadState();
  const bulkUploadState = useBulkUploadState();
  const projectResources = useProjectResources(organizationId as number);
  const uploadToastManager = useMemo(() => createUploadToastManager(), []);
  const { setTargetFileId, setMulti, multi } = fileUploadState;
  const { projectId, dataSourceId, objectStorageId, projects, dataSources } =
    projectResources;
  const selectedFiles = fileUploadState.selectedFiles;

  const needsTarget =
    fileUploadState.uploadType === "version" ||
    fileUploadState.uploadType === "properties";
  const isMultiAllowed = fileUploadState.uploadType === "new";
  const showRightPanel = fileUploadState.uploadMode === "file";
  const selectedTarget =
    initialAvailableFiles.find((f) => f.id === fileUploadState.targetFileId) ??
    null;

  const canUpload =
    selectedFiles.length > 0 &&
    !!projectId &&
    !!dataSourceId &&
    !!objectStorageId &&
    (!needsTarget || !!fileUploadState.targetFileId);

  // Clear target file when not needed
  useEffect(() => {
    if (!needsTarget) setTargetFileId("");
  }, [needsTarget, setTargetFileId]);

  // Manage multi toggle
  useEffect(() => {
    if (!isMultiAllowed && multi) {
      setMulti(false);
    }
  }, [isMultiAllowed, multi, setMulti]);

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

        const showMultiFileProgressToast = (
          progress: BatchUploadProgressEvent,
        ) => {
          uploadToastManager.show({
            title: t.translations.UPLOADING_FILES,
            message: `${progress.completed} / ${progress.total} ${t.translations.FILES_LABEL}`,
            percent: (progress.completed / progress.total) * 100,
          });
        };

        if (shouldShowMultiFileProgressToast) {
          uploadToastManager.show({
            title: t.translations.UPLOADING_FILES,
            message: `0 / ${selectedFiles.length} ${t.translations.FILES_LABEL}`,
            percent: 0,
          });
        }

        const results = await uploadFilesBatch({
          ...uploadContext,
          files: selectedFiles,
          maxConcurrentFiles: MAX_CONCURRENT_FILE_UPLOADS,
          onProgress: shouldShowMultiFileProgressToast
            ? showMultiFileProgressToast
            : undefined,
        });

        const ok = results.filter((r) => r.status === "fulfilled").length;
        const fail = results.length - ok;
        uploadToastManager.success(
          `Uploaded ${ok} file(s)${fail ? ` • ${fail} failed` : ""}`,
        );
        if (fail) console.warn("Batch upload failures:", results);
        fileUploadState.resetFileUpload();
        return;
      }

      const file = selectedFiles[0];
      const metadata = fileUploadState.filesMetadata[0] ?? {};

      if (metadata.isTimeSeries) {
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
        `Successfully uploaded ${bulkUploadState.validationResult.validCount} records!`,
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
                {t.translations.START_UPLOAD_BY_CHOOSING_TYPE ||
                  "Choose an upload mode, configure destination resources, then upload."}
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
        <div
          className={`mx-auto grid w-full max-w-7xl gap-6 ${
            showRightPanel ? "lg:grid-cols-12" : ""
          }`}
        >
          <section
            className={`w-full ${
              showRightPanel ? "lg:col-span-8" : "max-w-5xl mx-auto"
            }`}
          >
            <div className="card bg-base-100 border border-base-300/60 shadow-xl">
              <div className="card-body space-y-6">
                <div className="space-y-3">
                  <div className="flex items-center gap-2">
                    <span className="badge badge-primary badge-sm">1</span>
                    <h2 className="text-lg font-semibold text-base-content">
                      {t.translations.UPLOAD_MODE}
                    </h2>
                  </div>

                  <div className="grid gap-3 md:grid-cols-2">
                    <button
                      type="button"
                      className={`rounded-xl border p-4 text-left transition ${
                        fileUploadState.uploadMode === "file"
                          ? "border-primary bg-primary/10 shadow-sm"
                          : "border-base-300/70 hover:bg-base-200/40"
                      }`}
                      onClick={() => {
                        fileUploadState.setUploadMode("file");
                        bulkUploadState.setCsvFile(null);
                      }}
                    >
                      <div className="flex items-center gap-3">
                        <DocumentIcon className="size-6" />
                        <div>
                          <p className="font-semibold text-base-content">
                            {t.translations.FILE_UPLOAD}
                          </p>
                          <p className="text-xs text-base-content/70">
                            Upload one or more files with metadata.
                          </p>
                        </div>
                      </div>
                    </button>

                    <button
                      type="button"
                      className={`rounded-xl border p-4 text-left transition ${
                        fileUploadState.uploadMode === "bulk"
                          ? "border-primary bg-primary/10 shadow-sm"
                          : "border-base-300/70 hover:bg-base-200/40"
                      }`}
                      onClick={() => {
                        fileUploadState.setUploadMode("bulk");
                        fileUploadState.setSelectedFiles([]);
                        fileUploadState.resetFileUpload();
                      }}
                    >
                      <div className="flex items-center gap-3">
                        <ArrowUpOnSquareStackIcon className="size-6" />
                        <div>
                          <p className="font-semibold text-base-content">
                            {t.translations.BULK_METADATA}
                          </p>
                          <p className="text-xs text-base-content/70">
                            Create records from a CSV template.
                          </p>
                        </div>
                      </div>
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

                  <div className="rounded-xl border border-base-300/60 bg-base-200/30 p-4">
                    <ProjectResourceSelectors
                      {...projectResources}
                      hasOrganization={!!organization}
                      uploadMode={fileUploadState.uploadMode}
                    />
                  </div>
                </div>

                <div className="divider my-0" />

                <div className="space-y-3">
                  <div className="flex items-center gap-2">
                    <span className="badge badge-primary badge-sm">3</span>
                    <h2 className="text-lg font-semibold text-base-content">
                      {fileUploadState.uploadMode === "file"
                        ? t.translations.FILE_UPLOAD
                        : t.translations.BULK_METADATA}
                    </h2>
                  </div>

                  <div className="rounded-xl border border-base-300/60 bg-base-100 p-4">
                    {fileUploadState.uploadMode === "file" ? (
                      <FileUploadSection
                        uploadType={fileUploadState.uploadType}
                        setUploadType={fileUploadState.setUploadType}
                        multi={fileUploadState.multi}
                        setMulti={fileUploadState.setMulti}
                        selectedFiles={fileUploadState.selectedFiles}
                        setSelectedFiles={fileUploadState.setSelectedFiles}
                        setShowMultiFileWarning={
                          fileUploadState.setShowMultiFileWarning
                        }
                        dropKey={fileUploadState.dropKey}
                        filesMetadata={fileUploadState.filesMetadata}
                        handleMetadataChange={fileUploadState.handleMetadataChange}
                        targetFileId={fileUploadState.targetFileId}
                        setTargetFileId={fileUploadState.setTargetFileId}
                        availableFiles={initialAvailableFiles}
                        needsTarget={needsTarget}
                        isMultiAllowed={isMultiAllowed}
                        isUploading={fileUploadState.isUploading}
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

          {showRightPanel && (
            <aside className="w-full lg:col-span-4">
              <div className="space-y-4 lg:sticky lg:top-28">
                <div className="card bg-base-100 border border-base-300/60 shadow-xl">
                  <div className="card-body">
                    <h3 className="card-title text-base-content">
                      Upload Summary
                    </h3>
                    <div className="mt-1 space-y-2 text-sm">
                      <div className="flex items-center justify-between">
                        <span className="text-base-content/70">
                          {t.translations.PROJECT}
                        </span>
                        <span className={projectId ? "" : "text-warning"}>
                          {projectId ? "Ready" : "Required"}
                        </span>
                      </div>
                      <div className="flex items-center justify-between">
                        <span className="text-base-content/70">
                          {t.translations.DATA_SOURCE}
                        </span>
                        <span className={dataSourceId ? "" : "text-warning"}>
                          {dataSourceId ? "Ready" : "Required"}
                        </span>
                      </div>
                      <div className="flex items-center justify-between">
                        <span className="text-base-content/70">
                          {t.translations.STORAGE_DESTINATION}
                        </span>
                        <span className={objectStorageId ? "" : "text-warning"}>
                          {objectStorageId ? "Ready" : "Required"}
                        </span>
                      </div>
                      <div className="flex items-center justify-between">
                        <span className="text-base-content/70">
                          {t.translations.SELECTED_FILES}
                        </span>
                        <span>{selectedFiles.length}</span>
                      </div>
                    </div>
                  </div>
                </div>

                {selectedFiles.length === 0 && (
                  <div className="rounded-xl border border-dashed border-base-300 p-6 text-center bg-base-100">
                    <p className="text-sm text-base-content/70">
                      {t.translations.NO_FILES_SELECTED_YET}
                    </p>
                  </div>
                )}

                <FileDetailsCard
                  needsTarget={needsTarget}
                  selectedTarget={selectedTarget}
                />
                <SelectedFilesCard
                  files={fileUploadState.selectedFiles}
                  onRemoveAt={fileUploadState.removeAt}
                  onClear={fileUploadState.clearAll}
                  onUpload={handleFileUpload}
                  canUpload={canUpload}
                  isUploading={fileUploadState.isUploading}
                />
                {fileUploadState.isUploading && !fileUploadState.uploadProgress && (
                  <div className="p-4 bg-base-200 rounded-lg flex flex-col items-center justify-center space-y-3">
                    <span className="loading loading-spinner loading-lg text-primary"></span>
                    <p className="text-sm text-base-content/70 text-center">
                      {t.translations.PREPARING_UPLOAD}
                    </p>
                  </div>
                )}
              </div>
            </aside>
          )}
        </div>
      </main>

      {/* MODALS */}

      {/* Multi File Warning Modal */}
      {fileUploadState.showMultiFileWarning && (
        <div className="modal modal-open">
          <div className="modal-box">
            <h3 className="font-bold text-lg">
              {t.translations.CANT_SWITCH_TO_SINGLE_FILE}
            </h3>
            <p className="py-2">{t.translations.MULTI_FILE_WARNING}</p>
            <div className="modal-action">
              <button
                className="btn btn-secondary"
                onClick={() => fileUploadState.setShowMultiFileWarning(false)}
              >
                {t.translations.OKAY}
              </button>
            </div>
          </div>
          <div
            className="modal-backdrop"
            onClick={() => fileUploadState.setShowMultiFileWarning(false)}
          />
        </div>
      )}

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
