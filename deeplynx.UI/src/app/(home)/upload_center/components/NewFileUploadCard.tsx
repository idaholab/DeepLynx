// src/app/(home)/components/NewFileUploadCard.tsx

"use client";
import { FileMetadata } from "../../types/types";
import { useLanguage } from "@/app/contexts/Language";
import { TrashIcon, XMarkIcon } from "@heroicons/react/24/outline";
import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ChangeEvent,
} from "react";
import type { ExistingFile } from "../../types/types";
import SearchBar from "../../components/SearchBar";
import { formatLocalDateTime } from "@/app/lib/date_time";
import { createMetadataUploadSchema } from "./metadataUploadSchema";
import { getClass } from "@/app/lib/client_service/class_services.client";

const MAX_VISIBLE_FILES = 100;

const initialVisibleFiles = (files: ExistingFile[]) =>
  files.slice(0, MAX_VISIBLE_FILES);
const interpolate = (
  template: string,
  values: Record<string, string | number>,
) =>
  Object.entries(values).reduce(
    (result, [key, value]) => result.replace(`{${key}}`, String(value)),
    template,
  );

interface NewFileUploadCardProps {
  defaultName?: string;
  fileIndex: number;
  disableMetadataFile?: boolean;
  onMetadataChange: (fileIndex: number, metadata: FileMetadata) => void;
  onRemove?: () => void;
  availableFiles: ExistingFile[];
  onSearchFiles: (query: string) => Promise<ExistingFile[]>;
  projectId: number;
  uploadError?: string;
}

export default function NewFileUploadCard({
  defaultName = "",
  fileIndex,
  disableMetadataFile = false,
  onMetadataChange,
  onRemove,
  availableFiles,
  onSearchFiles,
  projectId,
  uploadError,
}: NewFileUploadCardProps) {
  const { t } = useLanguage();
  const metadataUploadSchema = useMemo(
    () =>
      createMetadataUploadSchema({
        NAME_REQUIRED: t.translations.NAME_REQUIRED,
        DESCRIPTION_REQUIRED: t.translations.DESCRIPTION_REQUIRED,
        ORIGINAL_ID_REQUIRED: t.translations.ORIGINAL_ID_REQUIRED,
        CLASS_ID_MUST_BE_NUMBER_NOT_STRING:
          t.translations.CLASS_ID_MUST_BE_NUMBER_NOT_STRING,
        CLASS_ID_MUST_BE_INTEGER: t.translations.CLASS_ID_MUST_BE_INTEGER,
        CLASS_ID_MUST_BE_GREATER_THAN_ZERO:
          t.translations.CLASS_ID_MUST_BE_GREATER_THAN_ZERO,
      }),
    [t],
  );
  const [recordMode, setRecordMode] = useState<"new" | "update">("new");
  const [targetRecordId, setTargetRecordId] = useState("");
  const [recordSearchInput, setRecordSearchInput] = useState("");
  const [isSearching, setIsSearching] = useState(false);
  const [hasSearched, setHasSearched] = useState(false);
  const [isTimeSeries, setIsTimeSeries] = useState(false);
  const [metadataFile, setMetadataFile] = useState<File | undefined>(undefined);
  const [metadataPreview, setMetadataPreview] = useState<
    Record<string, unknown> | undefined
  >(undefined);
  const [metadataPreviewError, setMetadataPreviewError] = useState("");
  const metadataFileInputRef = useRef<HTMLInputElement | null>(null);
  const [displayedFiles, setDisplayedFiles] = useState<ExistingFile[]>(
    initialVisibleFiles(availableFiles),
  );
  const metadataInputId = `metadata-file-${fileIndex}`;
  const metadataHelpId = `metadata-file-help-${fileIndex}`;

  const clearMetadataFile = () => {
    setMetadataFile(undefined);
    setMetadataPreview(undefined);
    setMetadataPreviewError("");
    if (metadataFileInputRef.current) {
      metadataFileInputRef.current.value = "";
    }
  };

  const getPreviewString = (
    source: Record<string, unknown> | undefined,
    pascalKey: string,
    camelKey: string,
  ): string | undefined => {
    if (!source) return undefined;
    const value = source[pascalKey] ?? source[camelKey];
    if (value === undefined || value === null) return undefined;
    if (typeof value === "string") return value.trim() || undefined;
    if (typeof value === "number") return String(value);
    return undefined;
  };

  const handleMetadataFileChange = async (e: ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) {
      clearMetadataFile();
      return;
    }

    if (!file.name.toLowerCase().endsWith(".json")) {
      clearMetadataFile();
      setMetadataPreviewError(t.translations.METADATA_FILE_JSON_ONLY);
      return;
    }

    try {
      const content = await file.text();
      const parsedJson: unknown = JSON.parse(content);

      const result = metadataUploadSchema.safeParse(parsedJson);

      if (!result.success) {
        const message = result.error.issues
          .map((issue) => {
            const path = issue.path.join(".") || "metadata";
            return `${path}: ${issue.message}`;
          })
          .join("\n");

        clearMetadataFile();
        setMetadataPreviewError(message);

        return;
      }

      const metadata = result.data;

      if (metadata.ClassId != null) {
        try {
          await getClass(Number(projectId), metadata.ClassId, true);
        } catch (error) {
          clearMetadataFile();
          setMetadataPreviewError(
            interpolate(t.translations.CLASS_ID_DOES_NOT_EXIST_IN_PROJECT, {
              id: metadata.ClassId,
            }),
          );
          return;
        }
      }

      setMetadataFile(file);
      setMetadataPreview(metadata as Record<string, unknown>);
      setMetadataPreviewError("");
    } catch {
      clearMetadataFile();
      setMetadataPreviewError(t.translations.METADATA_FILE_INVALID_JSON_OBJECT);
    }
  };

  const selectedRecord =
    displayedFiles.find((f) => String(f.id) === String(targetRecordId)) ??
    availableFiles.find((f) => String(f.id) === String(targetRecordId));
  const selectedDataType =
    recordMode === "update"
      ? "standard"
      : isTimeSeries
        ? "timeseries"
        : "standard";

  const handleSearch = useCallback(
    async ({ query }: { query: string; option?: string }) => {
      const trimmedQuery = query.trim();

      if (!trimmedQuery) {
        setHasSearched(false);
        setDisplayedFiles(initialVisibleFiles(availableFiles));
        return;
      }

      setHasSearched(true);
      setIsSearching(true);
      try {
        const results = await onSearchFiles(trimmedQuery);
        setDisplayedFiles(results);
      } finally {
        setIsSearching(false);
      }
    },
    [availableFiles, onSearchFiles],
  );

  useEffect(() => {
    if (recordMode === "update" && isTimeSeries) {
      setIsTimeSeries(false);
    }
  }, [recordMode, isTimeSeries]);

  useEffect(() => {
    if (recordMode !== "update") {
      setRecordSearchInput("");
      setHasSearched(false);
      setDisplayedFiles(initialVisibleFiles(availableFiles));
    }
  }, [recordMode, availableFiles]);

  useEffect(() => {
    if (!hasSearched) {
      setDisplayedFiles(initialVisibleFiles(availableFiles));
    }
  }, [availableFiles, hasSearched]);

  useEffect(() => {
    if (
      recordMode === "update" &&
      targetRecordId &&
      !availableFiles.some((f) => String(f.id) === String(targetRecordId))
    ) {
      setTargetRecordId("");
    }
  }, [recordMode, targetRecordId, availableFiles]);

  useEffect(() => {
    if (disableMetadataFile && metadataFile) {
      clearMetadataFile();
    }
  }, [disableMetadataFile, metadataFile]);

  useEffect(() => {
    const metadata: FileMetadata = {
      name: defaultName,
      description: "",
      isTimeSeries: recordMode === "update" ? false : isTimeSeries,
      recordMode,
      ...(recordMode === "update" && targetRecordId ? { targetRecordId } : {}),
      ...(metadataFile && { metadataFile }),
    };
    onMetadataChange(fileIndex, metadata);
  }, [
    defaultName,
    recordMode,
    targetRecordId,
    isTimeSeries,
    metadataFile,
    fileIndex,
    onMetadataChange,
  ]);

  return (
    <div className="space-y-2">
      <div className="divider my-6 text-md  text-base-content/70">
        {interpolate(t.translations.FILE_CARD_TITLE, {
          index: fileIndex + 1,
          name: defaultName,
        })}
      </div>

      <div className="card card-border">
        <div className="card-body w-full space-y-4">
          <div className="flex justify-between">
            <div
              role="radiogroup"
              aria-label={t.translations.RECORD_MODE_ARIA}
              className="inline-flex rounded-full border border-base-300/70 bg-base-200/50 p-1"
            >
              <button
                type="button"
                role="radio"
                aria-checked={recordMode === "new"}
                className={`rounded-full px-3 py-1 text-xs font-medium transition ${
                  recordMode === "new"
                    ? "bg-base-100 text-base-content shadow-sm"
                    : "text-base-content/70"
                }`}
                onClick={() => setRecordMode("new")}
              >
                {t.translations.NEW_RECORD}
              </button>
              <button
                type="button"
                role="radio"
                aria-checked={recordMode === "update"}
                className={`rounded-full px-3 py-1 text-xs font-medium transition ${
                  recordMode === "update"
                    ? "bg-base-100 text-base-content shadow-sm"
                    : "text-base-content/70"
                }`}
                onClick={() => setRecordMode("update")}
              >
                {t.translations.UPDATE_EXISTING_RECORD}
              </button>
            </div>
            {onRemove && (
              <div className="flex justify-end">
                <button
                  type="button"
                  className="btn btn-ghost btn-xs"
                  onClick={onRemove}
                >
                  <TrashIcon className="size-6 text-error" />
                </button>
              </div>
            )}
          </div>

          {recordMode === "update" && (
            <div className="space-y-2">
              <label className="label-text font-semibold">
                {t.translations.SELECT_EXISTING_RECORD}
              </label>
              <SearchBar
                placeholder={t.translations.SEARCH_FILES_PLACEHOLDER}
                value={recordSearchInput}
                onChange={(e) => setRecordSearchInput(e.target.value)}
                onSubmit={handleSearch}
                onClearAll={() => {
                  setRecordSearchInput("");
                  setHasSearched(false);
                  setDisplayedFiles(initialVisibleFiles(availableFiles));
                }}
                aditionalFilters={false}
              />

              <div className="rounded-lg border border-base-300/70 max-h-40 overflow-y-auto">
                {isSearching ? (
                  <div className="p-3 text-sm text-base-content/70">
                    <span className="loading loading-spinner loading-xs mr-2"></span>
                    {t.translations.SEARCHING_FILES}
                  </div>
                ) : displayedFiles.length === 0 ? (
                  <div className="p-3 text-sm text-base-content/70">
                    {hasSearched
                      ? t.translations.NO_FILES_FOUND
                      : t.translations.NO_FILES_AVAILABLE}
                  </div>
                ) : (
                  displayedFiles.map((f) => {
                    const selected = String(targetRecordId) === String(f.id);
                    return (
                      <button
                        key={f.id}
                        type="button"
                        onClick={() => setTargetRecordId(String(f.id))}
                        className={`w-full border-b border-base-300/60 px-3 py-2 text-left last:border-b-0 transition ${
                          selected ? "bg-base-200/70" : "hover:bg-base-200/30"
                        }`}
                      >
                        <div className="flex items-center justify-between gap-2">
                          <div className="min-w-0">
                            <p className="truncate text-sm font-medium">
                              {f.name}
                            </p>
                            <p className="truncate text-xs text-base-content/60">
                              {interpolate(t.translations.ID_LAST_UPDATED, {
                                id: f.id,
                                updated: f.lastUpdate
                                  ? formatLocalDateTime(String(f.lastUpdate))
                                  : t.translations.RECORD_HISTORY_NOT_AVAILABLE,
                              })}
                            </p>
                          </div>
                          {selected && (
                            <span className="badge badge-sm badge-outline">
                              {t.translations.SELECTED}
                            </span>
                          )}
                        </div>
                      </button>
                    );
                  })
                )}
              </div>

              {!hasSearched && availableFiles.length > MAX_VISIBLE_FILES && (
                <p className="text-xs text-base-content/60">
                  {interpolate(t.translations.SHOWING_FIRST_FILES_USE_SEARCH, {
                    count: MAX_VISIBLE_FILES,
                  })}
                </p>
              )}

              {selectedRecord && (
                <p className="text-xs text-base-content/70">
                  {t.translations.SELECTED_RECORD}{" "}
                  <span className="font-semibold">{selectedRecord.name}</span>
                </p>
              )}
            </div>
          )}
          <div className="flex justify-between flex-col-2">
            <div>
              {/* Row 1: Data Type + Metadata Preview */}
              <div className="grid gap-4 md:grid-cols-2 md:items-start">
                <div className="min-w-0 space-y-2">
                  <span className="label-text block font-semibold">
                    {t.translations.DATA_TYPE}
                  </span>
                  <div
                    role="radiogroup"
                    aria-label={t.translations.DATA_TYPE}
                    className="inline-flex rounded-full border border-base-300/70 bg-base-200/50 p-1"
                  >
                    <button
                      type="button"
                      role="radio"
                      aria-checked={selectedDataType === "standard"}
                      className={`rounded-full px-3 py-1 text-xs font-medium transition ${
                        selectedDataType === "standard"
                          ? "bg-base-100 text-base-content shadow-sm"
                          : "text-base-content/70"
                      }`}
                      onClick={() => setIsTimeSeries(false)}
                    >
                      {t.translations.STANDARD_FILE}
                    </button>
                    <button
                      type="button"
                      role="radio"
                      aria-checked={selectedDataType === "timeseries"}
                      className={`rounded-full px-3 py-1 text-xs font-medium transition ${
                        selectedDataType === "timeseries"
                          ? "bg-base-100 text-base-content shadow-sm"
                          : "text-base-content/70"
                      } ${recordMode === "update" ? "cursor-not-allowed opacity-50" : ""}`}
                      onClick={() => {
                        if (recordMode === "update") return;
                        setIsTimeSeries(true);
                      }}
                      disabled={recordMode === "update"}
                    >
                      {t.translations.TIMESERIES}
                    </button>
                  </div>
                </div>
              </div>
              {/* Row 2: Metadata File */}
              <div className="flex flex-col gap-1 mt-4">
                <label
                  htmlFor={metadataInputId}
                  className="flex items-center gap-2"
                >
                  <span className="label-text block font-semibold">
                    {t.translations.METADATA_FILE}
                  </span>
                  <span className="badge badge-xs">
                    {t.translations.OPTIONAL}
                  </span>
                </label>

                <div className="flex items-center gap-3">
                  <input
                    id={metadataInputId}
                    ref={metadataFileInputRef}
                    type="file"
                    accept=".json,application/json"
                    className="file-input file-input-sm"
                    onChange={handleMetadataFileChange}
                    disabled={disableMetadataFile}
                    aria-describedby={metadataHelpId}
                  />
                  {metadataFile && !disableMetadataFile && (
                    <button
                      type="button"
                      className="btn btn-xs btn-ghost text-error"
                      onClick={clearMetadataFile}
                    >
                      <XMarkIcon className="size-6" />
                    </button>
                  )}
                </div>
                <p id={metadataHelpId} className="text-xs text-base-content/60">
                  {disableMetadataFile
                    ? t.translations.METADATA_FILE_UNAVAILABLE_FOR_LARGE_FILES
                    : t.translations.METADATA_FILE_HELP_OPTIONAL}
                </p>
              </div>
            </div>
            <div className="w-full max-w-md mx-auto">
              <div className="min-w-0 space-y-2">
                <span className="label-text block font-semibold">
                  {t.translations.METADATA_PREVIEW_TITLE}
                </span>
                <div className="h-31 max-h-36 overflow-x-hidden overflow-y-auto rounded-lg border border-base-300/60 bg-base-200/30 p-3">
                  {metadataPreviewError ? (
                    <p className="text-xs text-error">{metadataPreviewError}</p>
                  ) : metadataPreview ? (
                    <div className="grid gap-1 text-xs text-base-content/80">
                      <p className="break-words">
                        <span className="font-semibold">
                          {t.translations.METADATA_PREVIEW_NAME}:
                        </span>{" "}
                        <span className="break-all">
                          {getPreviewString(metadataPreview, "Name", "name") ??
                            t.translations.NOT_AVAILABLE}
                        </span>
                      </p>
                      <p className="break-words">
                        <span className="font-semibold">
                          {t.translations.METADATA_PREVIEW_DESCRIPTION}:
                        </span>{" "}
                        <span className="break-all">
                          {getPreviewString(
                            metadataPreview,
                            "Description",
                            "description",
                          ) ?? t.translations.NOT_AVAILABLE}
                        </span>
                      </p>
                      <p className="break-words">
                        <span className="font-semibold">
                          {t.translations.METADATA_PREVIEW_ORIGINAL_ID}:
                        </span>{" "}
                        <span className="break-all">
                          {getPreviewString(
                            metadataPreview,
                            "OriginalId",
                            "originalId",
                          ) ?? t.translations.NOT_AVAILABLE}
                        </span>
                      </p>
                      <p className="break-words">
                        <span className="font-semibold">
                          {t.translations.METADATA_PREVIEW_CLASS}:
                        </span>{" "}
                        <span className="break-all">
                          {getPreviewString(
                            metadataPreview,
                            "ClassName",
                            "className",
                          ) ?? t.translations.NOT_AVAILABLE}
                        </span>
                      </p>
                      <p className="break-words">
                        <span className="font-semibold">
                          {t.translations.CLASS_ID}:
                        </span>{" "}
                        <span className="break-all">
                          {getPreviewString(
                            metadataPreview,
                            "ClassId",
                            "classId",
                          ) ?? t.translations.NOT_AVAILABLE}
                        </span>
                      </p>
                    </div>
                  ) : (
                    <p className="text-xs text-base-content/70">
                      {t.translations.METADATA_PREVIEW_SELECT_FILE}
                    </p>
                  )}
                </div>
                {uploadError && (
                  <div className="rounded-md border border-error/30 bg-error/10 p-3 text-sm text-error">
                    {uploadError}
                  </div>
                )}
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
