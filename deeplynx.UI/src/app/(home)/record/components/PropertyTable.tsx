// src/app/(home)/components/PropertyTable.tsx
"use client";

import {
  ArrowDownTrayIcon,
  CheckCircleIcon,
  ChevronDownIcon,
  ChevronRightIcon,
  PencilIcon,
  XCircleIcon,
  XMarkIcon,
} from "@heroicons/react/24/outline";
import React, { useRef, useEffect, useState } from "react";
import { useSearchParams } from "next/navigation";
import {
  downloadFile,
  getStorageType,
  isPresignedUrlStorage,
} from "@/app/lib/client_service/file_services.client";
import { getRecord } from "@/app/lib/client_service/record_services.client";
import { useLanguage } from "@/app/contexts/Language";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import axios from "axios";
import toast from "react-hot-toast";
import CopyToClipboardButton from "../../components/CopyToClipboardButton";

interface PropertyRow {
  label: string;
  value: React.ReactNode;
  editable?: boolean;
  onEdit?: (newValue: string) => void;
  maxCharacterLimit?: number;
  isNested?: boolean;
  nestedRows?: PropertyRow[];
  copyValue?: string;
  copyTooltipLabel?: string;
  copyAriaLabel?: string;
  idleIconClassName?: string;
  copiedIconClassName?: string;
}

interface PropertyTableProps {
  title?: string;
  rows: PropertyRow[];
  className?: string;
  download?: boolean;
  recordName?: string | null;
  onEditProperties?: () => void;
}

const PropertyTable: React.FC<PropertyTableProps> = ({
  title,
  rows,
  className,
  download = false,
  recordName,
  onEditProperties,
}) => {
  const [editingIndex, setEditingIndex] = useState<number | null>(null);
  const [editValue, setEditValue] = useState<string>("");
  const [expandedRows, setExpandedRows] = useState<Set<number>>(new Set());
  const [abortController, setAbortController] =
    useState<AbortController | null>(null);
  const [downloadProgress, setDownloadProgress] = useState<number | null>(null);
  const [folderDownloadProgress, setFolderDownloadProgress] = useState<number | null>(null);
  const [downloading, setDownloading] = useState(false);
  const [timeRemaining, setTimeRemaining] = useState<number | null>(null);
  const [bytesDownloaded, setBytesDownloaded] = useState<{
    loaded: number;
    total: number;
  } | null>(null);
  const [isPresignedUrl, setIsPresignedUrl] = useState<boolean>(false);
  const [preparingDownload, setPreparingDownload] = useState<boolean>(false);

  const searchParams = useSearchParams();
  const [isFolder, setIsFolder] = useState(false);
  const isFolderRef = useRef(false);
  const projectIdParam = searchParams.get("projectId");
  const [folderDownloadSpeed, setFolderDownloadSpeed] = React.useState<number | null>(null);
  const recordIdParam = searchParams.get("recordId");
  const projectId = projectIdParam ? Number(projectIdParam) : NaN;
  const recordId = recordIdParam ? Number(recordIdParam) : NaN;
  const canDownload = Number.isFinite(projectId) && Number.isFinite(recordId);
  const { t } = useLanguage();
  const { organization, hasLoaded } = useOrganizationSession();

  useEffect(() => {
    isFolderRef.current = isFolder;
  }, [isFolder]);

  const handleDownload = async () => {
    if (!canDownload) return;

    const controller = new AbortController();
    setAbortController(controller);

    setDownloading(true);
    setDownloadProgress(null);
    setTimeRemaining(null);
    setBytesDownloaded(null);
    setFolderDownloadProgress(null);
    setPreparingDownload(true);

    try {
      // First, determine the storage type
      const storageType = await getStorageType(
        organization?.organizationId as number,
        projectId,
        recordId,
      );

      const usePresignedUrl = isPresignedUrlStorage(storageType);
      setIsPresignedUrl(usePresignedUrl);

      // For blob downloads, initialize progress bar at 0% immediately
      if (!usePresignedUrl) {
        setDownloadProgress(0);
        setBytesDownloaded({ loaded: 0, total: 0 });
      }

      setPreparingDownload(false);

      const startTime = Date.now();
      let lastDisplayUpdateTime = startTime;
      let lastDisplayLoaded = 0;

      const record = await getRecord(
        organization?.organizationId as number,
        projectId,
        recordId,
      )

      const recordUri = record?.uri || "";
      setIsFolder(recordUri?.endsWith("/"));

      await downloadFile(
        organization?.organizationId as number,
        projectId,
        recordId,
        recordName,
        (progressInfo) => {
          // Only process progress for blob downloads (non-presigned URL)
          if (isFolderRef.current) {
            const now = Date.now();
            const timeSinceLastDisplay = (now - lastDisplayUpdateTime) / 1000;

            setFolderDownloadProgress(progressInfo.loaded);

            if (timeSinceLastDisplay >= 2) {
              const bytesDownloadedSinceLastDisplay = progressInfo.loaded - lastDisplayLoaded;
              const instantSpeed = timeSinceLastDisplay > 0 ? bytesDownloadedSinceLastDisplay / timeSinceLastDisplay : 0;

              setFolderDownloadSpeed(instantSpeed);

              lastDisplayUpdateTime = now;
              lastDisplayLoaded = progressInfo.loaded;
            }

            setDownloadProgress(1);
            setBytesDownloaded({ loaded: 1, total: 1 });
          } else if (!usePresignedUrl) {
            const now = Date.now();
            const timeSinceLastDisplay = (now - lastDisplayUpdateTime) / 1000;

            setDownloadProgress(progressInfo.percentage);
            setBytesDownloaded({
              loaded: progressInfo.loaded,
              total: progressInfo.total,
            });

            if (timeSinceLastDisplay >= 2) {
              const elapsed = (now - startTime) / 1000;

              const bytesDownloadedSinceLastDisplay =
                progressInfo.loaded - lastDisplayLoaded;
              const instantSpeed =
                timeSinceLastDisplay > 0
                  ? bytesDownloadedSinceLastDisplay / timeSinceLastDisplay
                  : 0;

              const avgSpeed = elapsed > 0 ? progressInfo.loaded / elapsed : 0;
              const speed = instantSpeed * 0.7 + avgSpeed * 0.3;

              const remaining = progressInfo.total - progressInfo.loaded;
              const eta = speed > 0 ? remaining / speed : null;

              setTimeRemaining(eta);

              lastDisplayUpdateTime = now;
              lastDisplayLoaded = progressInfo.loaded;
            }
          }
        },
        controller,
      );

      // For presigned URL downloads, show toast message
      if (usePresignedUrl) {
        toast.success(t.translations.DOWNLOAD_STARTED_IN_BROWSER, {
          icon: "📥",
          duration: 3000,
        });
      } else {
        // For blob downloads, clear progress after 2 seconds
        setTimeout(() => {
          setDownloadProgress(null);
          setTimeRemaining(null);
          setBytesDownloaded(null);
        }, 2000);
      }
    } catch (error) {
      if (axios.isAxiosError(error) && error.code === "ERR_CANCELED") {
        return;
      }
      console.error("Download error:", error);
      setDownloadProgress(null);
      setTimeRemaining(null);
      setBytesDownloaded(null);
      setPreparingDownload(false);
    } finally {
      setDownloading(false);
      setAbortController(null);
      setIsPresignedUrl(false);
    }
  };

  const handleCancelDownload = () => {
    if (abortController) {
      abortController.abort();
      // Clear progress states first
      setDownloadProgress(null);
      setTimeRemaining(null);
      setBytesDownloaded(null);
      setFolderDownloadProgress(null);
      setPreparingDownload(false);
      // Then clear controller and downloading state
      setAbortController(null);
      setDownloading(false);
      setIsPresignedUrl(false);
    }
  };

  const formatBytes = (bytes: number, decimals = 2): string => {
    if (bytes === 0) return "0 Bytes";
    const k = 1000;
    const dm = decimals < 0 ? 0 : decimals;
    const sizes = ["Bytes", "KB", "MB", "GB", "TB"];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(dm)) + " " + sizes[i];
  };

  const formatTime = (seconds: number): string => {
    if (seconds < 60) return `${Math.round(seconds)}s`;
    if (seconds < 3600) {
      const mins = Math.floor(seconds / 60);
      const secs = Math.round(seconds % 60);
      return `${mins}m ${secs}s`;
    }
    const hours = Math.floor(seconds / 3600);
    const mins = Math.round((seconds % 3600) / 60);
    return `${hours}h ${mins}m`;
  };

  const handleEdit = (index: number, currentValue: string) => {
    setEditingIndex(index);
    setEditValue(currentValue);
  };

  const handleSave = (row: PropertyRow) => {
    row.onEdit?.(editValue);
    setEditingIndex(null);
  };

  const handleCancel = () => {
    setEditingIndex(null);
    setEditValue("");
  };

  const toggleExpand = (index: number) => {
    setExpandedRows((prev) => {
      const newSet = new Set(prev);
      if (newSet.has(index)) {
        newSet.delete(index);
      } else {
        newSet.add(index);
      }
      return newSet;
    });
  };

  const renderRow = (
    row: PropertyRow,
    index: number,
    depth: number = 0,
    isLast: boolean = false,
    parentIsLast: boolean[] = [],
  ) => {
    const isExpanded = expandedRows.has(index);
    const hasNested =
      row.isNested && row.nestedRows && row.nestedRows.length > 0;

    return (
      <React.Fragment key={index}>
        <div className={`grid grid-cols-12 border-b border-base-300/50`}>
          <div className="col-span-4 p-3 font-medium text-base-content text-sm bg-base-200 border-r border-base-300/50 flex items-center relative">
            {/* Tree branch visualization */}
            {depth > 0 && (
              <div className="absolute left-0 top-0 bottom-0 flex">
                {parentIsLast.map((parentLast, i) => (
                  <div key={i} className="relative" style={{ width: "1.5rem" }}>
                    {!parentLast && (
                      <div className="absolute left-1/2 top-0 bottom-0 w-px bg-base-300" />
                    )}
                  </div>
                ))}
                <div className="relative" style={{ width: "1.5rem" }}>
                  {/* Vertical line */}
                  {!isLast && (
                    <div className="absolute left-1/2 top-0 bottom-0 w-px bg-base-300" />
                  )}
                  {/* Horizontal line */}
                  <div
                    className="absolute top-1/2 left-1/2 w-2 h-px bg-base-300"
                    style={{ transform: "translateY(-50%)" }}
                  />
                  {/* Corner for last item */}
                  {isLast && (
                    <div
                      className="absolute left-1/2 top-0 w-px bg-base-300"
                      style={{ height: "50%" }}
                    />
                  )}
                </div>
              </div>
            )}

            <div
              className="flex items-center"
              style={{
                paddingLeft: depth > 0 ? `${depth * 1.5 + 0.5}rem` : "0",
              }}
            >
              {hasNested && (
                <button
                  onClick={() => toggleExpand(index)}
                  className="mr-2 hover:bg-base-300 rounded p-1 transition-colors flex-shrink-0"
                >
                  {isExpanded ? (
                    <ChevronDownIcon className="w-4 h-4" />
                  ) : (
                    <ChevronRightIcon className="w-4 h-4" />
                  )}
                </button>
              )}
              <span className="truncate ml-2">{row.label}</span>
            </div>
          </div>
          <div className="col-span-7 p-3 text-sm text-base-content break-words">
            {editingIndex === index ? (
              <div className="flex items-center gap-1">
                <input
                  type="text"
                  value={editValue}
                  maxLength={row.maxCharacterLimit}
                  onChange={(e) => setEditValue(e.target.value)}
                  className="input input-sm input-bordered w-full"
                />
                {row.maxCharacterLimit && (
                  <span className={`text-xs float-right mt-1 ${!row.maxCharacterLimit ? "text-base-content" :
                    editValue.length == row.maxCharacterLimit ? "text-error" :
                      editValue.length >= row.maxCharacterLimit - 10 ? "text-warning" :
                        "text-base-content"
                    }`}>
                    {editValue.length}/{row.maxCharacterLimit}
                  </span>
                )}
              </div>
            ) : (
              <div className="break-words">
                {hasNested ? (
                  <span className="text-base-content/60 italic">
                    {isExpanded
                      ? t.translations.RECORD_HISTORY_EXPANDED
                      : t.translations.PROPERTIES_COUNT.replace(
                        "{count}",
                        String(row.nestedRows?.length ?? 0),
                      )}
                  </span>
                ) : (
                  row.value
                )}
              </div>
            )}
          </div>
          <div className="col-span-1 p-3 flex justify-center items-center gap-1">
            {row.copyValue && editingIndex !== index && !hasNested && (
              <CopyToClipboardButton
                value={row.copyValue}
                tooltipLabel={row.copyTooltipLabel ?? t.translations.COPY}
                ariaLabel={row.copyAriaLabel ?? t.translations.COPY_VALUE}
                idleIconClassName={row.idleIconClassName}
                copiedIconClassName={row.copiedIconClassName}
              />
            )}
            {row.editable && editingIndex !== index && !hasNested && (
              <PencilIcon
                className="text-primary hover:text-primary-focus size-6 cursor-pointer transition-colors"
                onClick={() => handleEdit(index, String(row.value))}
              />
            )}
            {editingIndex === index && (
              <>
                <button>
                  <CheckCircleIcon
                    className="text-success hover:text-success-content size-6 cursor-pointer transition-colors"
                    onClick={() => handleSave(row)}
                  />
                </button>
                <button>
                  <XCircleIcon
                    className="text-error hover:text-error-content size-6 cursor-pointer transition-colors"
                    onClick={handleCancel}
                  />
                </button>
              </>
            )}
          </div>
        </div>

        {/* Render nested rows if expanded */}
        {isExpanded && hasNested && (
          <>
            {row.nestedRows!.map((nestedRow, nestedIndex) =>
              renderRow(
                nestedRow,
                index * 1000 + nestedIndex,
                depth + 1,
                nestedIndex === row.nestedRows!.length - 1,
                [...parentIsLast, isLast],
              ),
            )}
          </>
        )}
      </React.Fragment>
    );
  };

  // Show progress bar only for blob downloads (non-presigned URL)


  const showProgressBar =
    !isPresignedUrl && downloadProgress !== null && bytesDownloaded !== null;

  return (
    <div className={`${className}`}>
      <div className="card border border-base-300/50 bg-base-100 p-2 shadow-sm">
        {title && (
          <div className="flex justify-between items-center m-4">
            <h2 className="text-xl font-bold text-base-content">{title}</h2>
            <div className="flex gap-2">
              {/* Edit Properties Button */}
              {onEditProperties && (
                <button
                  onClick={onEditProperties}
                  title={t.translations.EDIT_PROPERTIES}
                  className="p-1 transition-colors cursor-pointer hover:text-primary"
                >
                  <PencilIcon className="size-6 text-primary" />
                </button>
              )}

              {download && (
                <div className="flex items-center gap-3">
                  {/* Status indicator - show during preparation or for presigned URL downloads */}
                  {downloading && (preparingDownload || isPresignedUrl) && !showProgressBar && !isFolder && (
                    <div className="flex items-center gap-2 min-w-[200px]">
                      <div className="loading loading-spinner loading-sm text-primary"></div>
                      <span className="text-sm text-base-content">
                        {t.translations.PREPARING_DOWNLOAD}
                      </span>
                    </div>
                  )}

                  {/* Folder download: show "Downloading..." with current size */}
                  {downloading && isFolder && (
                    <div className="flex items-center gap-2 min-w-[250px]">
                      <div className="loading loading-spinner loading-sm text-primary"></div>
                      <span className="text-sm text-base-content">
                        Downloading... Current Size: {formatBytes(folderDownloadProgress || 0)}
                        {folderDownloadSpeed !== null && (
                          <> ({formatBytes(folderDownloadSpeed)}/s)</>
                        )}
                      </span>
                    </div>
                  )}

                  {/* File download: show progress bar */}
                  {downloading && !isFolder && showProgressBar && (
                    <div className="flex flex-col gap-1 min-w-[200px]">
                      <div className="flex items-center gap-2">
                        <div className="flex-1 bg-base-300 rounded-full h-2">
                          <div
                            className="bg-primary h-2 rounded-full transition-all duration-300"
                            style={{ width: `${downloadProgress}%` }}
                          />
                        </div>
                        <span className="text-sm font-medium text-base-content whitespace-nowrap">
                          {downloadProgress}%
                        </span>
                      </div>
                      <div className="flex justify-between text-xs text-base-content/70">
                        <span>
                          {formatBytes(bytesDownloaded.loaded)} / {formatBytes(bytesDownloaded.total)}
                        </span>
                        {timeRemaining !== null && timeRemaining > 0 && (
                          <span>
                            {t.translations.ETA}: {formatTime(timeRemaining)}
                          </span>
                        )}
                      </div>
                    </div>
                  )}

                  {/* Download or Cancel button */}
                  {downloading ? (
                    <button
                      onClick={handleCancelDownload}
                      className="p-1 text-error hover:text-error-focus transition-colors cursor-pointer"
                      title={t.translations.CANCEL_DOWNLOAD}
                    >
                      <XMarkIcon className="w-8 h-8" />
                    </button>
                  ) : (
                    <button
                      onClick={handleDownload}
                      disabled={!canDownload}
                      title={
                        canDownload
                          ? t.translations.DOWNLOAD_FILE
                          : t.translations.MISSING_PROJECT_OR_RECORD_ID_IN_URL
                      }
                      className={`p-1 transition-colors ${canDownload ? "hover:text-primary cursor-pointer" : "opacity-50 cursor-not-allowed"
                        }`}
                    >
                      <ArrowDownTrayIcon className="w-8 h-8" />
                    </button>
                  )}
                </div>
              )}
            </div>
          </div>
        )}

        <div className="card-body p-4">
          <div className="border border-base-300/50 rounded-lg overflow-hidden bg-base-100">
            {rows.map((row, index) =>
              renderRow(row, index, 0, index === rows.length - 1, []),
            )}
          </div>
        </div>
      </div>
    </div>
  );
};

export default PropertyTable;