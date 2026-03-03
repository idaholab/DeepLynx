// src/app/(home)/components/NewFileUploadCard.tsx

"use client";
import { FileMetadata } from "../types/types";
import { useLanguage } from "@/app/contexts/Language";
import { TrashIcon } from "@heroicons/react/24/outline";
import { useCallback, useEffect, useMemo, useState } from "react";
import type { ExistingFile } from "../types/types";
import SearchBar from "./SearchBar";
import { formatLocalDateTime } from "@/app/lib/date_time";

interface NewFileUploadCardProps {
  defaultName?: string;
  fileIndex: number;
  onMetadataChange: (fileIndex: number, metadata: FileMetadata) => void;
  onRemove?: () => void;
  availableFiles: ExistingFile[];
  onSearchFiles?: (query: string) => Promise<ExistingFile[]>;
}

export default function NewFileUploadCard({
  defaultName = "",
  fileIndex,
  onMetadataChange,
  onRemove,
  availableFiles,
  onSearchFiles,
}: NewFileUploadCardProps) {
  const { t } = useLanguage();
  const [recordMode, setRecordMode] = useState<"new" | "update">("new");
  const [targetRecordId, setTargetRecordId] = useState("");
  const [recordSearchInput, setRecordSearchInput] = useState("");
  const [recordSearchQuery, setRecordSearchQuery] = useState("");
  const [isSearching, setIsSearching] = useState(false);
  const [hasSearched, setHasSearched] = useState(false);
  const [description] = useState("");
  const [isTimeSeries, setIsTimeSeries] = useState(false);
  const [metadataFile, setMetadataFile] = useState<File | undefined>(undefined);
  const fileBaseName = (filename: string) => filename.replace(/\.[^/.]+$/, "");
  const [name, setName] = useState(fileBaseName(defaultName));
  const MAX_VISIBLE_FILES = 100;
  const [displayedFiles, setDisplayedFiles] = useState<ExistingFile[]>([]);

  const selectedRecord = useMemo(
    () =>
      displayedFiles.find((f) => String(f.id) === String(targetRecordId)) ??
      availableFiles.find((f) => String(f.id) === String(targetRecordId)),
    [displayedFiles, availableFiles, targetRecordId],
  );

  const filterFilesLocally = useCallback(
    (query: string) => {
      const normalized = query.trim().toLowerCase();
      if (!normalized) {
        return availableFiles.slice(0, MAX_VISIBLE_FILES);
      }

      return availableFiles.filter((f) => {
        const nameValue = (f.name ?? "").toLowerCase();
        const aliasValue = (f.alias ?? "").toLowerCase();
        const descValue = (f.description ?? "").toLowerCase();
        const idValue = String(f.id).toLowerCase();
        return (
          nameValue.includes(normalized) ||
          aliasValue.includes(normalized) ||
          descValue.includes(normalized) ||
          idValue.includes(normalized)
        );
      });
    },
    [availableFiles],
  );

  const handleSearch = useCallback(
    async ({ query }: { query: string; option?: string }) => {
      const trimmedQuery = query.trim();
      setRecordSearchQuery(trimmedQuery);

      if (!trimmedQuery) {
        setHasSearched(false);
        setDisplayedFiles(availableFiles.slice(0, MAX_VISIBLE_FILES));
        return;
      }

      setHasSearched(true);
      setIsSearching(true);
      try {
        if (onSearchFiles) {
          const results = await onSearchFiles(trimmedQuery);
          setDisplayedFiles(results);
        } else {
          setDisplayedFiles(filterFilesLocally(trimmedQuery));
        }
      } finally {
        setIsSearching(false);
      }
    },
    [availableFiles, filterFilesLocally, onSearchFiles],
  );

  useEffect(() => {
    setName(fileBaseName(defaultName));
  }, [defaultName]);

  useEffect(() => {
    if (recordMode === "update" && isTimeSeries) {
      setIsTimeSeries(false);
    }
  }, [recordMode, isTimeSeries]);

  useEffect(() => {
    if (recordMode !== "update") {
      setRecordSearchInput("");
      setRecordSearchQuery("");
      setHasSearched(false);
      setDisplayedFiles(availableFiles.slice(0, MAX_VISIBLE_FILES));
    }
  }, [recordMode, availableFiles]);

  useEffect(() => {
    if (!recordSearchQuery.trim()) {
      setDisplayedFiles(availableFiles.slice(0, MAX_VISIBLE_FILES));
    }
  }, [availableFiles, recordSearchQuery]);

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
    const metadata: FileMetadata = {
      name,
      description,
      isTimeSeries: recordMode === "update" ? false : isTimeSeries,
      recordMode,
      ...(recordMode === "update" && targetRecordId ? { targetRecordId } : {}),
      ...(metadataFile && { metadataFile }),
    };
    onMetadataChange(fileIndex, metadata);
  }, [
    name,
    description,
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
        {`File ${fileIndex + 1}: ${defaultName}`}
      </div>

      <div className="card card-border">
        <div className="card-body w-full space-y-4">
          <div className="flex justify-between">
            <div
              role="radiogroup"
              aria-label="Record mode"
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
                New Record
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
                Update Existing Record
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
                Select Existing Record
              </label>
              <SearchBar
                placeholder="Search files by name, alias, description, or ID"
                value={recordSearchInput}
                onChange={(e) => setRecordSearchInput(e.target.value)}
                onSubmit={handleSearch}
                onClearAll={() => {
                  setRecordSearchQuery("");
                  setRecordSearchInput("");
                  setHasSearched(false);
                  setDisplayedFiles(availableFiles.slice(0, MAX_VISIBLE_FILES));
                }}
                aditionalFilters={false}
              />

              <div className="rounded-lg border border-base-300/70 max-h-40 overflow-y-auto">
                {isSearching ? (
                  <div className="p-3 text-sm text-base-content/70">
                    <span className="loading loading-spinner loading-xs mr-2"></span>
                    Searching files...
                  </div>
                ) : displayedFiles.length === 0 ? (
                  <div className="p-3 text-sm text-base-content/70">
                    {hasSearched ? "No files found." : "No files available."}
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
                              ID {f.id} - Last updated:{" "}
                              {formatLocalDateTime(String(f.lastUpdate))}
                            </p>
                          </div>
                          {selected && (
                            <span className="badge badge-sm badge-outline">
                              Selected
                            </span>
                          )}
                        </div>
                      </button>
                    );
                  })
                )}
              </div>

              {!recordSearchQuery.trim() &&
                availableFiles.length > MAX_VISIBLE_FILES && (
                  <p className="text-xs text-base-content/60">
                    Showing first {MAX_VISIBLE_FILES} files. Use search to
                    narrow results.
                  </p>
                )}

              {selectedRecord && (
                <p className="text-xs text-base-content/70">
                  Selected record:{" "}
                  <span className="font-semibold">{selectedRecord.name}</span>
                </p>
              )}
            </div>
          )}

          {/* Row 1: Time Series toggle + Name input */}
          <div className="grid grid-cols-[auto,1fr] items-center gap-4">
            <div className="flex items-center">
              <span className="label-text mr-2">
                {t.translations.TIMESERIES}
              </span>
              <input
                type="checkbox"
                className="toggle toggle-secondary"
                checked={isTimeSeries}
                disabled={recordMode === "update"}
                onChange={(e) => setIsTimeSeries(e.target.checked)}
              />
              <label className="flex items-center gap-2 flex-1">
                <span className="label-text ml-4">{t.translations.ALIAS}</span>
                <input
                  type="text"
                  className="input input-sm w-full"
                  placeholder="metadata.a"
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                />
              </label>
            </div>
          </div>
          {/* Row 2: Metadata File (optional) */}
          <div className="flex items-center gap-3">
            <span className="label-text shrink-0">Metadata File</span>
            <label className="btn btn-sm btn-outline cursor-pointer">
              {metadataFile ? metadataFile.name : "Choose file (optional)"}
              <input
                type="file"
                className="hidden"
                onChange={(e) => setMetadataFile(e.target.files?.[0])}
              />
            </label>
            {metadataFile && (
              <button
                type="button"
                className="btn btn-xs btn-ghost text-error"
                onClick={() => setMetadataFile(undefined)}
              >
                ✕
              </button>
            )}
          </div>
          {/* Row 3: Description textarea */}
          {/* <div className="grid grid-cols-[auto,1fr] items-start gap-4">
            <div className="flex">
              <span className="label-text mr-2">
                {t.translations.DESCRIPTION}
              </span>
              <textarea
                className="textarea textarea-bordered w-full"
                placeholder="Example: This file contains ..."
                value={description}
                onChange={(e) => setDescription(e.target.value)}
              ></textarea>
            </div> */}
          {/* Row 3: Update Existing */}
          {/* {showUpdate && (
              <fieldset>
                <label className="label">
                  {t.translations.UPDATE_EXISTING}
                  <select
                    className="select select-info select-sm mt-2"
                    value={updateAction}
                    onChange={(e) =>
                      setUpdateAction(e.target.value as "merge" | "overwrite")
                    }
                    required
                  >
                    <option value="" disabled>
                      {t.translations.CHOOSE_OPTION}
                    </option>
                    <option value="nexus">{t.translations.MERGE}</option>
                    <option value="remote-db">
                      {t.translations.OVERWRITE}
                    </option>
                  </select>
                </label>
              </fieldset>
            )}
          </div> */}
        </div>
      </div>
    </div>
  );
}
