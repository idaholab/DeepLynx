"use client";

import { useLanguage } from "@/app/contexts/Language";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import {
  uploadFile,
  uploadFilesBatch,
  cancelChunkedUpload,
  cancelCurrentUpload
} from "@/app/lib/client_service/file_upload_services.client";
import { uploadTimeseriesFile } from "@/app/lib/client_service/timeseries_services.client";
import { uploadBulkMetadata } from "@/app/lib/client_service/metadata_service.client";
import { useEffect, useMemo, useRef } from "react";
import toast from "react-hot-toast";
import { parseBackendErrors } from "@/app/lib/error_parser";

// Components
import FileDetailsCard from "../components/FileDetailCard";
import SelectedFilesCard from "../components/SelectedFilesCard";

// Hooks
import { useUploadState } from "./hooks/useUploadState";
import { useBulkUploadState } from "./hooks/useBulkUploadState";
import { useProjectResources } from "./hooks/useProjectResources";

// Types
import type { ExistingFile, RecentUpload } from "../types/types";
import ProjectResourceSelectors from "./components/ProjectResourceSelectors";
import BulkUploadSection from "./components/BulkUploadSection";
import FileUploadSection from "./components/FileUploadSection";
import {
  ArrowUpOnSquareStackIcon,
  DocumentIcon,
} from "@heroicons/react/24/outline";

// ============================================================================
// TYPES
// ============================================================================

type Props = {
  initialAvailableFiles: ExistingFile[];
};

// ============================================================================
// MAIN COMPONENT
// ============================================================================

export default function UploadCenterClient({ initialAvailableFiles }: Props) {
  const { t } = useLanguage();
  const { organization } = useOrganizationSession();

  // ============================================================================
  // STATE MANAGEMENT (via Custom Hooks)
  // ============================================================================

  const fileUploadState = useUploadState();
  const bulkUploadState = useBulkUploadState();
  const projectResources = useProjectResources(
    organization?.organizationId as number
  );

  const { setTargetFileId, setMulti } = fileUploadState;
  const { multi } = fileUploadState;
  const uploadToastIdRef = useRef<string | undefined>(undefined);

  const showOrUpdateUploadToast = (args: {
    title: string;
    message: string;
    percent?: number;
    isCancelling?: boolean;
  }) => {
    uploadToastIdRef.current = toast.custom(
      () => (
        <div className="w-[320px] rounded-lg border border-base-300 bg-base-100 p-4 shadow-lg">
          <p className="text-sm font-semibold text-base-content">{args.title}</p>
          <p className="mt-1 text-xs text-base-content/70">{args.message}</p>
          {typeof args.percent === "number" && (
            <div className="mt-3">
              <div className="mb-1 flex items-center justify-between text-xs">
                <span className="text-base-content/70">Progress</span>
                <span className="font-semibold text-base-content">
                  {Math.round(args.percent)}%
                </span>
              </div>
              <progress
                className="progress progress-primary w-full"
                value={args.percent}
                max="100"
              />
            </div>
          )}
          {args.isCancelling && (
            <p className="mt-2 text-xs font-medium text-warning">
              Cancelling upload...
            </p>
          )}
        </div>
      ),
      { id: uploadToastIdRef.current, duration: Infinity }
    );
  };

  // ============================================================================
  // COMPUTED VALUES
  // ============================================================================

  const needsTarget =
    fileUploadState.uploadType === "version" ||
    fileUploadState.uploadType === "properties";
  const isMultiAllowed = fileUploadState.uploadType === "new";
  const showRightPanel =
    fileUploadState.selectedFiles.length > 0 &&
    fileUploadState.uploadMode === "file";

  const availableFiles = useMemo(
    () => initialAvailableFiles,
    [initialAvailableFiles]
  );

  const selectedTarget = useMemo(
    () =>
      availableFiles.find((f) => f.id === fileUploadState.targetFileId) ?? null,
    [availableFiles, fileUploadState.targetFileId]
  );

  const canUpload =
    fileUploadState.selectedFiles.length > 0 &&
    !!projectResources.projectId &&
    !!projectResources.dataSourceId &&
    !!projectResources.objectStorageId &&
    (!needsTarget || !!fileUploadState.targetFileId);

  // ============================================================================
  // EFFECTS
  // ============================================================================

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

  // Keep a persistent upload toast updated during long-running uploads
  useEffect(() => {
    if (!fileUploadState.isUploading) return;

    if (!fileUploadState.uploadProgress) {
      showOrUpdateUploadToast({
        title: "Uploading file",
        message: "Preparing upload...",
      });
      return;
    }

    showOrUpdateUploadToast({
      title: "Uploading file",
      message: `${fileUploadState.uploadProgress.chunksCompleted} / ${fileUploadState.uploadProgress.totalChunks} chunks`,
      percent: fileUploadState.uploadProgress.percentComplete,
      isCancelling: fileUploadState.isCancelling,
    });
  }, [
    fileUploadState.isUploading,
    fileUploadState.isCancelling,
    fileUploadState.uploadProgress,
  ]);

  // Cleanup any persistent upload toast if component unmounts mid-upload
  useEffect(() => {
    return () => {
      if (uploadToastIdRef.current) {
        toast.dismiss(uploadToastIdRef.current);
      }
    };
  }, []);

  // ============================================================================
  // FILE UPLOAD HANDLERS
  // ============================================================================

  const handleFileUpload = async () => {
    if (
      !projectResources.projectId ||
      fileUploadState.selectedFiles.length === 0
    ) {
      toast.error("Select a project and at least one file.");
      return;
    }

    fileUploadState.setIsUploading(true);
    fileUploadState.setUploadProgress(null);

    try {
      if (fileUploadState.selectedFiles.length === 1) {
        const file = fileUploadState.selectedFiles[0];
        const metadata = fileUploadState.filesMetadata[0] ?? {};

        // Check if this is a timeseries file
        if (metadata.isTimeSeries) {
          // Use timeseries upload endpoint
          await uploadTimeseriesFile(
            organization?.organizationId as number,
            Number(projectResources.projectId),
            Number(projectResources.dataSourceId),
            file
          );
          toast.success("Timeseries file uploaded successfully!", {
            id: uploadToastIdRef.current,
          });
        } else {
          // Use regular file upload
          await uploadFile({
            organizationId: organization?.organizationId as number,
            projectId: projectResources.projectId,
            dataSourceId: projectResources.dataSourceId,
            objectStorageId: projectResources.objectStorageId,
            file,
            name: metadata.name || file.name,
            description: metadata.description || "",
            onProgress: (progress) => {
              fileUploadState.setUploadProgress(progress);
            },
          });
          toast.success("File uploaded successfully!", {
            id: uploadToastIdRef.current,
          });
        }
      } else {
        const results = await uploadFilesBatch({
          organizationId: organization?.organizationId as number,
          projectId: projectResources.projectId,
          dataSourceId: projectResources.dataSourceId,
          objectStorageId: projectResources.objectStorageId,
          files: fileUploadState.selectedFiles,
        });

        const ok = results.filter((r) => r.status === "fulfilled").length;
        const fail = results.length - ok;
        toast.success(`Uploaded ${ok} file(s)${fail ? ` • ${fail} failed` : ""}`, {
          id: uploadToastIdRef.current,
        });
        if (fail) console.warn("Batch upload failures:", results);
      }

      fileUploadState.resetFileUpload();
    } catch (err) {
      if (err instanceof DOMException && err.name === 'AbortError') {
        toast("Upload cancelled.", {
          id: uploadToastIdRef.current,
        });

      } else {
        console.error("Upload error:", err);
        toast.error("Upload failed. See console for details.", {
          id: uploadToastIdRef.current,
        });
      }
      fileUploadState.setUploadProgress(null);
    } finally {
      uploadToastIdRef.current = undefined;
      fileUploadState.setIsUploading(false);
      fileUploadState.setIsCancelling(false);
    }
  };

  const handleCancel = async () => {
    if (!fileUploadState.uploadProgress?.uploadId) return;

    fileUploadState.setIsCancelling(true);
    cancelCurrentUpload();

    try {
      await cancelChunkedUpload({
        organizationId: organization?.organizationId as number,
        projectId: projectResources.projectId,
        dataSourceId: projectResources.dataSourceId,
        objectStorageId: projectResources.objectStorageId,
        uploadId: fileUploadState.uploadProgress.uploadId,
      });
    } catch (err) {
      console.error("Failed to cleanup cancelled upload:", err);
    }
  };

  // ============================================================================
  // BULK UPLOAD HANDLERS
  // ============================================================================

  const handleBulkUpload = async () => {
    if (
      !bulkUploadState.validationResult ||
      !bulkUploadState.validationResult.isValid
    ) {
      toast.error("Please fix validation errors before uploading");
      return;
    }

    if (!projectResources.projectId || !projectResources.dataSourceId) {
      toast.error("Please select project and data source");
      return;
    }

    if (!organization?.organizationId) {
      toast.error("Organization not found");
      return;
    }

    bulkUploadState.setIsUploading(true);
    bulkUploadState.setBackendErrors([]);
    bulkUploadState.setUploadProgress(0);

    try {
      // Progress simulation
      const progressInterval = setInterval(() => {
        bulkUploadState.setUploadProgress((prev) => {
          if (prev >= 90) return prev;
          return prev + 10;
        });
      }, 200);

      // Upload
      await uploadBulkMetadata(
        organization.organizationId as number,
        Number(projectResources.projectId),
        Number(projectResources.dataSourceId),
        bulkUploadState.validationResult.validRecords
      );

      clearInterval(progressInterval);
      bulkUploadState.setUploadProgress(100);

      await new Promise((resolve) => setTimeout(resolve, 500));

      toast.success(
        `Successfully uploaded ${bulkUploadState.validationResult.validCount} records!`
      );

      bulkUploadState.resetBulkUpload();
    } catch (error: unknown) {
      console.error("Upload error:", error);

      bulkUploadState.setUploadProgress(0);

      // Extract error messages
      const errorMessages = extractErrorMessages(error);

      const parsedErrors = parseBackendErrors(errorMessages);
      bulkUploadState.setBackendErrors(parsedErrors);

      toast.error("Upload failed. Please check the error details below.");
    } finally {
      bulkUploadState.setIsUploading(false);
    }
  };

  // ============================================================================
  // RENDER
  // ============================================================================

  return (
    <div>
      {/* HEADER */}
      <div className="bg-base-200/40 px-3 sm:px-6 lg:px-12 p-6">
        <h1 className="text-xl sm:text-2xl font-bold text-base-content">
          {t.translations.UPLOAD_CENTER}
        </h1>
      </div>

      <div
        className={`flex flex-col xl:flex-row gap-6 xl:gap-8 p-3 sm:p-6 lg:p-10 ${showRightPanel ? "xl:justify-between" : "justify-center"
          }`}
      >
        {/* LEFT PANEL */}
        <div
          className={`w-full xl:w-3/5 ${showRightPanel ? "" : "max-w-5xl mx-auto"
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
                className={`btn btn-sm mr-5 ${fileUploadState.uploadMode === "file"
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
                className={`btn btn-sm ${fileUploadState.uploadMode === "bulk"
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
                availableFiles={availableFiles}
                needsTarget={needsTarget}
                isMultiAllowed={isMultiAllowed}
                isUploading={fileUploadState.isUploading}
              />
            ) : (
              <BulkUploadSection
                {...bulkUploadState}
                projectId={projectResources.projectId}
                dataSourceId={projectResources.dataSourceId}
                organizationId={organization?.organizationId as number}
                projects={projectResources.projects}
                dataSources={projectResources.dataSources}
              />
            )}
          </div>
        </div>

        {/* RIGHT PANEL */}
        {showRightPanel && (
          <div className="w-full xl:w-2/5">
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
            {/* Upload Status: Spinner + Progress */}
            {fileUploadState.isUploading && (
              <>
                {/* Show spinner while waiting for progress to start */}
                {!fileUploadState.uploadProgress && (
                  <div className="mt-4 p-4 bg-base-200 rounded-lg flex flex-col items-center justify-center space-y-3">
                    <span className="loading loading-spinner loading-lg text-primary"></span>
                    <p className="text-sm text-base-content/70 text-center">
                      Preparing upload...
                    </p>
                  </div>
                )}

                {/* Show progress bar once chunked upload starts */}
                {fileUploadState.uploadProgress && (
                  <div className="mt-4 p-4 bg-base-200 rounded-lg">
                    <div className="flex justify-between items-center mb-2">
                      <span className="text-sm font-medium">
                        {fileUploadState.uploadProgress.chunksCompleted} / {fileUploadState.uploadProgress.totalChunks} chunks
                      </span>
                      <span className="text-sm font-bold text-base-content">
                        {Math.round(fileUploadState.uploadProgress.percentComplete)}%
                      </span>
                    </div>
                    <progress
                      className="progress progress-success w-full"
                      value={fileUploadState.uploadProgress.percentComplete}
                      max="100"
                    ></progress>
                    <button
                      className="btn btn-sm btn-outline btn-error w-full mt-3"
                      onClick={handleCancel}
                      disabled={fileUploadState.isCancelling}
                    >
                      {fileUploadState.isCancelling ? (
                        <>
                          <span className="loading loading-spinner loading-xs"></span>
                          Cancelling and cleaning up...
                        </>
                      ) : (
                        'Cancel Upload'
                      )}
                    </button>
                  </div>
                )}
              </>
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
                    projectResources.projects.find(
                      (p) => p.id === Number(projectResources.projectId)
                    )?.name
                  }
                </p>
                <p>
                  <strong>{t.translations.DATA_SOURCE}:</strong>{" "}
                  {
                    projectResources.dataSources.find(
                      (d) => d.id === Number(projectResources.dataSourceId)
                    )?.name
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
