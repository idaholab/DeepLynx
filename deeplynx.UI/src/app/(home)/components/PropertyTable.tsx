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
import React, { useState } from "react";
import { useSearchParams } from "next/navigation";
import { downloadFile } from "@/app/lib/client_service/file_services.client";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import axios from 'axios';

interface PropertyRow {
  label: string;
  value: React.ReactNode;
  editable?: boolean;
  onEdit?: (newValue: string) => void;
  isNested?: boolean;
  nestedRows?: PropertyRow[];
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
  const [abortController, setAbortController] = useState<AbortController | null>(null);
  const [downloadProgress, setDownloadProgress] = useState<number | null>(null);
  const [downloading, setDownloading] = useState(false);
  const [timeRemaining, setTimeRemaining] = useState<number | null>(null);
  const [bytesDownloaded, setBytesDownloaded] = useState<{ loaded: number; total: number } | null>(null);
  const searchParams = useSearchParams();
  const projectIdParam = searchParams.get("projectId");
  const recordIdParam = searchParams.get("recordId");
  const projectId = projectIdParam ? Number(projectIdParam) : NaN;
  const recordId = recordIdParam ? Number(recordIdParam) : NaN;
  const canDownload = Number.isFinite(projectId) && Number.isFinite(recordId);
  const { organization, hasLoaded } = useOrganizationSession();

  const handleDownload = async () => {
    if (!canDownload) return;

    const controller = new AbortController();
    setAbortController(controller);


    setDownloading(true);
    setDownloadProgress(0);
    setTimeRemaining(null);
    setBytesDownloaded(null);

    const startTime = Date.now();
    let lastDisplayUpdateTime = startTime;
    let lastDisplayLoaded = 0; // Track loaded bytes at last display update

    try {
      await downloadFile(
        organization?.organizationId as number,
        projectId,
        recordId,
        recordName,
        (progressInfo) => {
          const now = Date.now();
          const timeSinceLastDisplay = (now - lastDisplayUpdateTime) / 1000;

          // Always update progress percentage and bytes (for accurate display)
          setDownloadProgress(progressInfo.percentage);
          setBytesDownloaded({ loaded: progressInfo.loaded, total: progressInfo.total });

          // Only update speed and ETA every 2 seconds
          if (timeSinceLastDisplay >= 2) {
            const elapsed = (now - startTime) / 1000;

            // Calculate instantaneous speed based on bytes since last display update
            const bytesDownloadedSinceLastDisplay = progressInfo.loaded - lastDisplayLoaded;
            const instantSpeed = timeSinceLastDisplay > 0
              ? bytesDownloadedSinceLastDisplay / timeSinceLastDisplay
              : 0;

            // Calculate average speed
            const avgSpeed = elapsed > 0 ? progressInfo.loaded / elapsed : 0;

            // Weighted average for smoother display
            const speed = instantSpeed * 0.7 + avgSpeed * 0.3;

            // Calculate time remaining
            const remaining = progressInfo.total - progressInfo.loaded;
            const eta = speed > 0 ? remaining / speed : null;

            setTimeRemaining(eta);

            // Update tracking variables for next display update
            lastDisplayUpdateTime = now;
            lastDisplayLoaded = progressInfo.loaded;
          }
        },
        controller
      );

      // Clear progress after 2 seconds
      setTimeout(() => {
        setDownloadProgress(null);
        setTimeRemaining(null);
        setBytesDownloaded(null);
      }, 2000);
    } catch (error) {
      // Check if it's an abort error (user cancelled)
      if (axios.isAxiosError(error) && error.code === 'ERR_CANCELED') {
        console.log('Download cancelled by user');
        // Don't treat this as an error - it's intentional
      } else {
        console.error('Download error:', error);
      }
      // Clear progress states for any error (including cancellation)
      setDownloadProgress(null);
      setTimeRemaining(null);
      setBytesDownloaded(null);
    } finally {
      setDownloading(false);
      setAbortController(null);
    }
  };

  const handleCancelDownload = () => {
    if (abortController) {
      abortController.abort();
      // CLEAR THESE FIRST
      setDownloadProgress(null);
      setTimeRemaining(null);
      setBytesDownloaded(null);
      // THEN clear controller and downloading state
      setAbortController(null);
      setDownloading(false);
    }
  };

  const formatBytes = (bytes: number, decimals = 2): string => {
    if (bytes === 0) return '0 Bytes';
    const k = 1000;
    const dm = decimals < 0 ? 0 : decimals;
    const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(dm)) + ' ' + sizes[i];
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
        <div className={`grid grid-cols-12 border-b border-base-300`}>
          <div className="col-span-4 p-3 font-medium text-base-content text-sm bg-base-200 border-r border-base-300 flex items-center relative">
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
              <input
                type="text"
                value={editValue}
                onChange={(e) => setEditValue(e.target.value)}
                className="input input-sm input-bordered w-full"
              />
            ) : (
              <div className="break-words">
                {hasNested ? (
                  <span className="text-base-content/60 italic">
                    {isExpanded
                      ? "Expanded"
                      : `${row.nestedRows?.length} properties`}
                  </span>
                ) : (
                  row.value
                )}
              </div>
            )}
          </div>
          <div className="col-span-1 p-3 flex justify-center items-center gap-1">
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

  return (
    <div className={`${className}`}>
      <div className="card bg-base-100 shadow-md p-2">
        {title && (
          <div className="flex justify-between items-center m-4">
            <h2 className="text-xl font-bold text-base-content">{title}</h2>

            <div className="flex gap-2">
              {/* Edit Properties Button */}
              {onEditProperties && (
                <button
                  onClick={onEditProperties}
                  title="Edit properties"
                  className="p-1 transition-colors cursor-pointer hover:text-primary"
                >
                  <PencilIcon className="size-6 text-primary" />
                </button>
              )}

              <div className="flex gap-2">
                {/* Edit Properties Button */}
                {onEditProperties && (
                  <button
                    onClick={onEditProperties}
                    title="Edit properties"
                    className="p-1 transition-colors cursor-pointer hover:text-primary"
                  >
                    <PencilIcon className="size-6 text-primary" />
                  </button>
                )}

                {download && (
                  <div className="flex items-center gap-3">
                    {/* Progress bar - only show when downloading */}
                    {downloadProgress !== null && downloading && (
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
                            {bytesDownloaded && `${formatBytes(bytesDownloaded.loaded)} / ${formatBytes(bytesDownloaded.total)}`}
                          </span>
                          {timeRemaining !== null && timeRemaining > 0 && (
                            <span>ETA: {formatTime(timeRemaining)}</span>
                          )}
                        </div>
                      </div>
                    )}

                    {downloading ? (
                      <button
                        onClick={handleCancelDownload}
                        className="p-1 text-error hover:text-error-focus transition-colors cursor-pointer"
                        title="Cancel download"
                      >
                        <XMarkIcon className="w-8 h-8" />
                      </button>
                    ) : (
                      <button
                        onClick={handleDownload}
                        disabled={!canDownload}
                        title={
                          canDownload
                            ? "Download file"
                            : "Missing projectId or recordId in URL"
                        }
                        className={`p-1 transition-colors ${canDownload
                          ? "hover:text-primary cursor-pointer"
                          : "opacity-50 cursor-not-allowed"
                          }`}
                      >
                        <ArrowDownTrayIcon className="w-8 h-8" />
                      </button>
                    )}
                  </div>
                )}

                <div className="card-body p-4">
                  <div className="border border-base-300 rounded-lg overflow-hidden bg-base-100">
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
