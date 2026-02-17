"use client";

import React, { useEffect, useMemo, useState } from "react";
import toast from "react-hot-toast";
import { HistoricalRecordResponseDto } from "@/app/(home)/types/responseDTOs";
import {
  getHistoricalRecord,
  getRecordHistory,
} from "@/app/lib/client_service/historical_record_services.client";

interface Props {
  organizationId: number;
  projectId: number;
  recordId: number;
}

type CompareMode = "none" | "previous" | "latest";

interface DiffRow {
  field: string;
  current: string;
  compare: string;
  changed: boolean;
}

const PLACEHOLDER = "N/A";

function formatDateTime(value?: string | null): string {
  if (!value) return PLACEHOLDER;
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return date.toLocaleString();
}

function toDateTimeInputValue(value?: string | null): string {
  if (!value) return "";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "";

  const pad = (n: number) => n.toString().padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(
    date.getHours(),
  )}:${pad(date.getMinutes())}`;
}

function parseMaybeJson(value: unknown): unknown {
  if (typeof value !== "string") return value;
  const trimmed = value.trim();
  if (!trimmed) return value;

  const looksLikeJson =
    (trimmed.startsWith("{") && trimmed.endsWith("}")) ||
    (trimmed.startsWith("[") && trimmed.endsWith("]"));

  if (!looksLikeJson) return value;

  try {
    return JSON.parse(trimmed);
  } catch {
    return value;
  }
}

function flattenObject(
  value: unknown,
  prefix: string,
  out: Record<string, string>,
): void {
  if (value === null) {
    out[prefix] = "null";
    return;
  }

  if (typeof value === "undefined") {
    return;
  }

  if (Array.isArray(value)) {
    if (value.length === 0) {
      out[prefix] = "[]";
      return;
    }

    value.forEach((item, index) => {
      flattenObject(item, `${prefix}[${index}]`, out);
    });
    return;
  }

  if (typeof value === "object") {
    const entries = Object.entries(value as Record<string, unknown>);
    if (entries.length === 0) {
      out[prefix] = "{}";
      return;
    }

    entries.forEach(([key, nested]) => {
      flattenObject(nested, `${prefix}.${key}`, out);
    });
    return;
  }

  out[prefix] = String(value);
}

function normalizeRecord(
  record: HistoricalRecordResponseDto | null,
): Record<string, string> {
  if (!record) return {};

  const normalized: Record<string, string> = {};

  const set = (key: string, value: unknown) => {
    if (value === null || typeof value === "undefined") return;
    const stringValue = String(value);
    if (stringValue.length === 0) return;
    normalized[key] = stringValue;
  };

  set("record.id", record.id);
  set("record.name", record.name);
  set("record.description", record.description);
  set("record.uri", record.uri);
  set("record.originalId", record.originalId);
  set("record.classId", record.classId);
  set("record.className", record.className);
  set("record.dataSourceId", record.dataSourceId);
  set("record.dataSourceName", record.dataSourceName);
  set("record.projectId", record.projectId);
  set("record.projectName", record.projectName);
  set("record.objectStorageId", record.objectStorageId);
  set("record.objectStorageName", record.objectStorageName);
  set("record.lastUpdatedAt", record.lastUpdatedAt);
  set("record.lastUpdatedBy", record.lastUpdatedBy);
  set("record.isArchived", record.isArchived);

  const parsedProperties = parseMaybeJson(record.properties);
  if (parsedProperties && typeof parsedProperties === "object") {
    flattenObject(parsedProperties, "properties", normalized);
  } else {
    set("properties", parsedProperties);
  }

  const parsedTags = parseMaybeJson(record.tags);
  if (parsedTags && typeof parsedTags === "object") {
    flattenObject(parsedTags, "tags", normalized);
  } else {
    set("tags", parsedTags);
  }

  return normalized;
}

function prettifyField(field: string): string {
  return field
    .replace(/\./g, " / ")
    .replace(/_/g, " ")
    .replace(/\b\w/g, (c) => c.toUpperCase());
}

function SnapshotMeta({
  title,
  snapshot,
}: {
  title: string;
  snapshot: HistoricalRecordResponseDto | null;
}) {
  return (
    <div className="card bg-base-100 border border-base-300 shadow-sm">
      <div className="card-body p-4">
        <h3 className="text-sm font-semibold uppercase tracking-wide opacity-70">
          {title}
        </h3>
        {!snapshot ? (
          <p className="text-sm opacity-70">No comparison version selected.</p>
        ) : (
          <div className="space-y-2 text-sm">
            <div>
              <span className="font-medium">Name: </span>
              <span>{snapshot.name || PLACEHOLDER}</span>
            </div>
            <div>
              <span className="font-medium">Updated: </span>
              <span>{formatDateTime(snapshot.lastUpdatedAt)}</span>
            </div>
            <div className="flex flex-wrap gap-2 pt-1">
              <span className="badge badge-outline">
                Data Source: {snapshot.dataSourceName || PLACEHOLDER}
              </span>
              <span className="badge badge-outline">
                Class: {snapshot.className || PLACEHOLDER}
              </span>
              <span className="badge badge-outline">
                Archived: {snapshot.isArchived ? "Yes" : "No"}
              </span>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

export default function RecordHistoryTab({
  organizationId,
  projectId,
  recordId,
}: Props) {
  const [history, setHistory] = useState<HistoricalRecordResponseDto[]>([]);
  const [selectedIndex, setSelectedIndex] = useState(0);
  const [selectedSnapshot, setSelectedSnapshot] =
    useState<HistoricalRecordResponseDto | null>(null);
  const [compareMode, setCompareMode] = useState<CompareMode>("previous");
  const [showOnlyChanges, setShowOnlyChanges] = useState(false);
  const [selectedDateInput, setSelectedDateInput] = useState("");
  const [isLoadingHistory, setIsLoadingHistory] = useState(true);
  const [isLoadingSnapshot, setIsLoadingSnapshot] = useState(false);
  const [historyError, setHistoryError] = useState<string | null>(null);

  useEffect(() => {
    if (compareMode === "none") {
      setShowOnlyChanges(false);
    }
  }, [compareMode]);

  useEffect(() => {
    let cancelled = false;

    const fetchHistory = async () => {
      setIsLoadingHistory(true);
      setHistoryError(null);

      try {
        const versions = await getRecordHistory(organizationId, projectId, recordId);
        const sorted = [...versions].sort(
          (a, b) =>
            new Date(a.lastUpdatedAt).getTime() -
            new Date(b.lastUpdatedAt).getTime(),
        );

        if (cancelled) return;

        setHistory(sorted);

        if (sorted.length > 0) {
          const latestIndex = sorted.length - 1;
          setSelectedIndex(latestIndex);
          setSelectedDateInput(toDateTimeInputValue(sorted[latestIndex].lastUpdatedAt));
        } else {
          setSelectedIndex(0);
          setSelectedDateInput("");
          setSelectedSnapshot(null);
        }
      } catch (error) {
        if (cancelled) return;
        console.error("Error fetching record history:", error);
        setHistory([]);
        setHistoryError("Failed to load record history.");
        toast.error("Failed to load record history.");
      } finally {
        if (!cancelled) {
          setIsLoadingHistory(false);
        }
      }
    };

    fetchHistory();

    return () => {
      cancelled = true;
    };
  }, [organizationId, projectId, recordId]);

  useEffect(() => {
    if (history.length === 0) return;
    if (selectedIndex < 0 || selectedIndex > history.length - 1) return;

    const selectedVersion = history[selectedIndex];
    if (!selectedVersion?.lastUpdatedAt) return;

    setSelectedDateInput(toDateTimeInputValue(selectedVersion.lastUpdatedAt));
    setSelectedSnapshot(selectedVersion);

    let cancelled = false;

    const fetchSnapshot = async () => {
      setIsLoadingSnapshot(true);
      try {
        const snapshot = await getHistoricalRecord(
          organizationId,
          projectId,
          recordId,
          selectedVersion.lastUpdatedAt,
          false,
        );
        if (!cancelled) setSelectedSnapshot(snapshot);
      } catch (error) {
        if (cancelled) return;
        console.error("Error fetching snapshot for selected point-in-time:", error);
        setSelectedSnapshot(selectedVersion);
        toast.error("Failed to load selected point-in-time snapshot.");
      } finally {
        if (!cancelled) setIsLoadingSnapshot(false);
      }
    };

    fetchSnapshot();

    return () => {
      cancelled = true;
    };
  }, [history, selectedIndex, organizationId, projectId, recordId]);

  const compareIndex = useMemo(() => {
    if (history.length === 0) return null;

    if (compareMode === "previous") {
      return selectedIndex > 0 ? selectedIndex - 1 : null;
    }

    if (compareMode === "latest") {
      const latestIndex = history.length - 1;
      return selectedIndex === latestIndex ? null : latestIndex;
    }

    return null;
  }, [history.length, compareMode, selectedIndex]);

  const activeSnapshot = selectedSnapshot || history[selectedIndex] || null;
  const comparisonSnapshot =
    compareIndex !== null && compareIndex >= 0 && compareIndex < history.length
      ? history[compareIndex]
      : null;

  const selectedMap = useMemo(
    () => normalizeRecord(activeSnapshot),
    [activeSnapshot],
  );
  const comparisonMap = useMemo(
    () => normalizeRecord(comparisonSnapshot),
    [comparisonSnapshot],
  );

  const diffRows = useMemo<DiffRow[]>(() => {
    const keys = Array.from(
      new Set([...Object.keys(selectedMap), ...Object.keys(comparisonMap)]),
    ).sort((a, b) => a.localeCompare(b));

    return keys.map((key) => {
      const current = selectedMap[key] ?? PLACEHOLDER;
      const compare = comparisonMap[key] ?? PLACEHOLDER;
      const changed = compareMode === "none" ? false : current !== compare;

      return {
        field: key,
        current,
        compare,
        changed,
      };
    });
  }, [selectedMap, comparisonMap, compareMode]);

  const rowsToRender = showOnlyChanges
    ? diffRows.filter((row) => row.changed)
    : diffRows;
  const changedCount = diffRows.filter((row) => row.changed).length;

  const handleDateInputChange = (value: string) => {
    setSelectedDateInput(value);
    if (!value || history.length === 0) return;

    const targetTime = new Date(value).getTime();
    if (Number.isNaN(targetTime)) return;

    let nearestIndex = 0;
    let nearestDiff = Number.POSITIVE_INFINITY;

    history.forEach((item, index) => {
      const itemTime = new Date(item.lastUpdatedAt).getTime();
      const diff = Math.abs(itemTime - targetTime);
      if (diff < nearestDiff) {
        nearestDiff = diff;
        nearestIndex = index;
      }
    });

    setSelectedIndex(nearestIndex);
  };

  if (isLoadingHistory) {
    return (
      <div className="mt-4 card bg-base-100 border border-base-300 shadow-sm">
        <div className="card-body">
          <div className="flex items-center gap-3">
            <span className="loading loading-spinner loading-md" />
            <p>Loading record history...</p>
          </div>
        </div>
      </div>
    );
  }

  if (historyError) {
    return (
      <div className="mt-4 alert alert-error">
        <span>{historyError}</span>
      </div>
    );
  }

  if (history.length === 0) {
    return (
      <div className="mt-4 card bg-base-100 border border-base-300 shadow-sm">
        <div className="card-body">
          <h3 className="card-title">Record History</h3>
          <p className="opacity-80">
            No historical versions were found for this record.
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="mt-4 space-y-4">
      <div className="card bg-base-100 border border-base-300 shadow-sm">
        <div className="card-body gap-4">
          <div className="flex flex-wrap items-end gap-4">
            <div className="form-control min-w-[280px] flex-1">
              <label className="label py-1">
                <span className="label-text font-medium">Select Version</span>
              </label>
              <select
                className="select select-bordered"
                value={selectedIndex}
                onChange={(e) => setSelectedIndex(Number(e.target.value))}
              >
                {history.map((version, index) => (
                  <option key={`${version.id}-${version.lastUpdatedAt}-${index}`} value={index}>
                    {index + 1}. {formatDateTime(version.lastUpdatedAt)}
                  </option>
                ))}
              </select>
            </div>

            <div className="form-control min-w-[260px]">
              <label className="label py-1">
                <span className="label-text font-medium">Jump to Time</span>
              </label>
              <input
                type="datetime-local"
                className="input input-bordered"
                value={selectedDateInput}
                onChange={(e) => handleDateInputChange(e.target.value)}
              />
              <span className="text-xs mt-1 opacity-70">
                Snaps to nearest saved version.
              </span>
            </div>

            <div className="form-control">
              <label className="label py-1">
                <span className="label-text font-medium">Compare Against</span>
              </label>
              <div className="join">
                <button
                  type="button"
                  className={`btn btn-sm join-item ${compareMode === "none" ? "btn-primary" : "btn-outline"}`}
                  onClick={() => setCompareMode("none")}
                >
                  None
                </button>
                <button
                  type="button"
                  className={`btn btn-sm join-item ${compareMode === "previous" ? "btn-primary" : "btn-outline"}`}
                  onClick={() => setCompareMode("previous")}
                >
                  Previous
                </button>
                <button
                  type="button"
                  className={`btn btn-sm join-item ${compareMode === "latest" ? "btn-primary" : "btn-outline"}`}
                  onClick={() => setCompareMode("latest")}
                >
                  Latest
                </button>
              </div>
            </div>

            <div className="ml-auto text-right text-sm">
              <p className="font-medium">Versions: {history.length}</p>
              <p className="opacity-70">
                Changes highlighted: {compareMode === "none" ? 0 : changedCount}
              </p>
            </div>
          </div>

          <div>
            <input
              type="range"
              min={0}
              max={Math.max(history.length - 1, 0)}
              step={1}
              value={selectedIndex}
              onChange={(e) => setSelectedIndex(Number(e.target.value))}
              className="range range-primary range-sm"
            />
            <div className="mt-2 flex justify-between text-xs opacity-70">
              <span>{formatDateTime(history[0]?.lastUpdatedAt)}</span>
              <span>{formatDateTime(history[history.length - 1]?.lastUpdatedAt)}</span>
            </div>
          </div>
        </div>
      </div>

      <div className="grid grid-cols-1 xl:grid-cols-2 gap-4">
        <SnapshotMeta title="Selected Snapshot" snapshot={activeSnapshot} />
        <SnapshotMeta title="Comparison Snapshot" snapshot={comparisonSnapshot} />
      </div>

      <div className="card bg-base-100 border border-base-300 shadow-sm">
        <div className="card-body p-0">
          <div className="flex flex-wrap items-center justify-between gap-3 px-4 py-3 border-b border-base-300">
            <div>
              <h3 className="font-semibold">Version Difference</h3>
              <p className="text-sm opacity-70">
                Changed fields are highlighted so differences stand out.
              </p>
            </div>
            <label className="label cursor-pointer gap-2">
              <input
                type="checkbox"
                className="checkbox checkbox-sm"
                checked={showOnlyChanges}
                disabled={compareMode === "none"}
                onChange={(e) => setShowOnlyChanges(e.target.checked)}
              />
              <span className="label-text">Show only changes</span>
            </label>
          </div>

          {isLoadingSnapshot && (
            <div className="px-4 py-2 text-sm opacity-75">
              <span className="loading loading-spinner loading-xs mr-2" />
              Loading selected snapshot...
            </div>
          )}

          <div className="overflow-auto max-h-[520px]">
            <table className="table table-zebra table-sm">
              <thead>
                <tr>
                  <th className="min-w-[200px]">Field</th>
                  <th className="min-w-[300px]">Selected</th>
                  <th className="min-w-[300px]">Compare</th>
                  <th>Status</th>
                </tr>
              </thead>
              <tbody>
                {rowsToRender.length === 0 ? (
                  <tr>
                    <td colSpan={4} className="text-center py-8 opacity-70">
                      No differences for the selected comparison.
                    </td>
                  </tr>
                ) : (
                  rowsToRender.map((row) => (
                    <tr key={row.field} className={row.changed ? "bg-warning/10" : ""}>
                      <td className="font-medium align-top">{prettifyField(row.field)}</td>
                      <td className={row.changed ? "bg-warning/5" : ""}>
                        <div className="whitespace-pre-wrap break-all text-xs">
                          {row.current}
                        </div>
                      </td>
                      <td className={row.changed ? "bg-warning/5" : ""}>
                        <div className="whitespace-pre-wrap break-all text-xs">
                          {row.compare}
                        </div>
                      </td>
                      <td className="align-top">
                        {row.changed ? (
                          <span className="badge badge-warning badge-sm">
                            Changed
                          </span>
                        ) : (
                          <span className="badge badge-ghost badge-sm">Same</span>
                        )}
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </div>
  );
}
