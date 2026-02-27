"use client";

import { useLanguage } from "@/app/contexts/Language";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import {
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
  const showRightPanel =
    selectedFiles.length > 0 && fileUploadState.uploadMode === "file";
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
      toast.error("Select a project and at least one file.");
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
        title: "Uploading file",
        message: "Preparing upload...",
      });
    }

    const showProgressToast = (progress: UploadProgressEvent) => {
      uploadToastManager.show({
        title: "Uploading file",
        message: `${progress.chunksCompleted} / ${progress.totalChunks} chunks`,
        percent: progress.percentComplete,
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
        const results = await uploadFilesBatch({
          ...uploadContext,
          files: selectedFiles,
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
        uploadToastManager.success("Timeseries file uploaded successfully!");
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
        uploadToastManager.success("File uploaded successfully!");
      }

      fileUploadState.resetFileUpload();
    } catch (err) {
      if (err instanceof DOMException && err.name === "AbortError") {
        uploadToastManager.message("Upload cancelled.");
      } else {
        console.error("Upload error:", err);
        uploadToastManager.error("Upload failed. See console for details.");
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
      toast.error("Please fix validation errors before uploading");
      return;
    }

    if (!projectId || !dataSourceId) {
      toast.error("Please select project and data source");
      return;
    }

    if (!organizationId) {
      toast.error("Organization not found");
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
      const errorMessages = extractErrorMessages(error);
      const parsedErrors = parseBackendErrors(errorMessages);
      bulkUploadState.setBackendErrors(parsedErrors);

      toast.error("Upload failed. Please check the error details below.");
    } finally {
      if (progressInterval) {
        clearInterval(progressInterval);
      }
      bulkUploadState.setIsUploading(false);
    }
  };

  return (
    <div>
      {/* HEADER */}
      <div className="bg-base-200/40 pl-12 p-6">
        <h1 className="text-2xl font-bold text-base-content">
          {t.translations.UPLOAD_CENTER}
        </h1>
      </div>

      <div
        className={`flex gap-8 p-10 lg:p-20 ${
          showRightPanel ? "justify-between" : "justify-center"
        }`}
      >
        {/* LEFT PANEL */}
        <div
          className={`w-full lg:w-3/5 ${
            showRightPanel ? "" : "max-w-5xl mx-auto"
          }`}
        >
          {/* UPLOAD MODE TOGGLE */}
          <div className="mb-6">
            <label className="label">
              <span className="label-text font-bold text-base-content">
                {t.translations.UPLOAD_MODE || "Upload Mode"}
              </span>
            </label>
            <div className="btn-group">
              <button
                type="button"
                className={`btn btn-sm mr-5 ${
                  fileUploadState.uploadMode === "file"
                    ? "btn-primary"
                    : "btn-ghost"
                }`}
                onClick={() => {
                  fileUploadState.setUploadMode("file");
                  bulkUploadState.setCsvFile(null);
                }}
              >
                <DocumentIcon className="size-6" />
                {t.translations.FILE_UPLOAD || "File Upload"}
              </button>
              <button
                type="button"
                className={`btn btn-sm ${
                  fileUploadState.uploadMode === "bulk"
                    ? "btn-primary"
                    : "btn-ghost"
                }`}
                onClick={() => {
                  fileUploadState.setUploadMode("bulk");
                  fileUploadState.setSelectedFiles([]);
                  fileUploadState.resetFileUpload();
                }}
              >
                <ArrowUpOnSquareStackIcon className="size-6" />
                {t.translations.BULK_METADATA || "Bulk Metadata"}
              </button>
            </div>
          </div>

          {/* PROJECT RESOURCE SELECTORS */}
          <div className="p-4 space-y-4">
            <ProjectResourceSelectors
              {...projectResources}
              hasOrganization={!!organization}
              uploadMode={fileUploadState.uploadMode}
            />

            {/* MODE-SPECIFIC CONTENT */}
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

        {/* RIGHT PANEL */}
        {showRightPanel && (
          <div className="lg:w-2/5">
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
              <div className="mt-4 p-4 bg-base-200 rounded-lg flex flex-col items-center justify-center space-y-3">
                <span className="loading loading-spinner loading-lg text-primary"></span>
                <p className="text-sm text-base-content/70 text-center">
                  Preparing upload...
                </p>
              </div>
            )}
          </div>
        )}
      </div>

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
                  {
                    projects.find((p) => p.id === Number(projectId))?.name
                  }
                </p>
                <p>
                  <strong>{t.translations.DATA_SOURCE}:</strong>{" "}
                  {
                    dataSources.find((d) => d.id === Number(dataSourceId))?.name
                  }
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
                    "Confirm Upload"
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

function extractErrorMessages(error: unknown): string[] {
  const fallback = ["Unknown error occurred"];

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
      return d.errors.map((err) => toMessage(err));
    }

    if (d.error !== undefined) {
      return [toMessage(d.error)];
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

function toMessage(value: unknown): string {
  if (typeof value === "string") return value;
  if (typeof value === "object" && value !== null) {
    const maybe = value as { message?: unknown };
    if (typeof maybe.message === "string") return maybe.message;
  }
  try {
    return JSON.stringify(value);
  } catch {
    return "Unknown error";
  }
}
