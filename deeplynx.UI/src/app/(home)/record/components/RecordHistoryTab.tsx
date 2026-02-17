"use client";

import React, { useEffect, useMemo, useState } from "react";
import toast from "react-hot-toast";
import { HistoricalRecordResponseDto } from "@/app/(home)/types/responseDTOs";
import { ChevronDownIcon, ChevronRightIcon } from "@heroicons/react/24/outline";
import { useLanguage } from "@/app/contexts/Language";
import {
  getHistoricalRecord,
  getRecordHistory,
} from "@/app/lib/client_service/historical_record_services.client";

interface Props {
  organizationId: number;
  projectId: number;
  recordId: number;
}

type CompareMode = "previous" | "latest" | "manual";

interface DiffRow {
  field: string;
  current: string;
  compare: string;
  changed: boolean;
}

interface DiffTreeNode {
  id: string;
  label: string;
  changed: boolean;
  current?: string;
  compare?: string;
  children: DiffTreeNode[];
  leafCount: number;
  isLeaf: boolean;
}

function formatDateTime(
  value?: string | null,
  placeholder: string = "N/A",
): string {
  if (!value) return placeholder;
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return date.toLocaleString();
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

function parseFieldSegments(field: string): string[] {
  const matches = field.match(/([^[.\]]+)|(\[\d+\])/g);
  return matches ?? [field];
}

function prettifySegment(segment: string): string {
  if (segment.startsWith("[") && segment.endsWith("]")) return segment;
  return prettifyField(segment);
}

function buildDiffTree(rows: DiffRow[]): DiffTreeNode[] {
  const roots = new Map<string, DiffTreeNode>();

  const ensureChild = (
    container: Map<string, DiffTreeNode> | DiffTreeNode,
    segment: string,
    id: string,
  ) => {
    if (container instanceof Map) {
      if (!container.has(id)) {
        container.set(id, {
          id,
          label: prettifySegment(segment),
          changed: false,
          children: [],
          leafCount: 0,
          isLeaf: false,
        });
      }
      return container.get(id)!;
    }

    let child = container.children.find((c) => c.id === id);
    if (!child) {
      child = {
        id,
        label: prettifySegment(segment),
        changed: false,
        children: [],
        leafCount: 0,
        isLeaf: false,
      };
      container.children.push(child);
    }
    return child;
  };

  rows.forEach((row) => {
    const segments = parseFieldSegments(row.field);
    let currentPath = "";
    let currentNode: DiffTreeNode | null = null;

    segments.forEach((segment, index) => {
      currentPath = currentPath ? `${currentPath}.${segment}` : segment;

      if (index === 0) {
        currentNode = ensureChild(roots, segment, currentPath);
      } else if (currentNode) {
        currentNode = ensureChild(currentNode, segment, currentPath);
      }

      if (index === segments.length - 1 && currentNode) {
        currentNode.current = row.current;
        currentNode.compare = row.compare;
        currentNode.changed = row.changed;
        currentNode.isLeaf = true;
      }
    });
  });

  const finalize = (node: DiffTreeNode): DiffTreeNode => {
    if (node.children.length === 0) {
      return {
        ...node,
        leafCount: 1,
        isLeaf: true,
      };
    }

    const finalizedChildren = node.children
      .map(finalize)
      .sort((a, b) => a.label.localeCompare(b.label));
    const leafCount = finalizedChildren.reduce(
      (sum, child) => sum + child.leafCount,
      0,
    );
    const changed = finalizedChildren.some((child) => child.changed);

    return {
      ...node,
      children: finalizedChildren,
      changed,
      leafCount,
      isLeaf: false,
    };
  };

  return Array.from(roots.values())
    .map(finalize)
    .sort((a, b) => a.label.localeCompare(b.label));
}

function filterTreeForChanges(nodes: DiffTreeNode[]): DiffTreeNode[] {
  return nodes
    .map((node) => {
      if (node.isLeaf) return node.changed ? node : null;

      const filteredChildren = filterTreeForChanges(node.children);
      if (filteredChildren.length === 0 && !node.changed) return null;

      return {
        ...node,
        children: filteredChildren,
      };
    })
    .filter((node): node is DiffTreeNode => node !== null);
}

function SnapshotMeta({
  title,
  snapshot,
  t,
  placeholder,
}: {
  title: string;
  snapshot: HistoricalRecordResponseDto | null;
  t: { translations: Record<string, string> };
  placeholder: string;
}) {
  return (
    <div className="card bg-base-100 shadow-lg">
      <div className="card-body p-4">
        <h3 className="text-sm font-semibold uppercase tracking-wide opacity-70 mr-4">
          {title}
        </h3>
        {!snapshot ? (
          <p className="text-sm opacity-70">
            {t.translations.RECORD_HISTORY_NO_COMPARISON_VERSION_SELECTED}
          </p>
        ) : (
          <div className="space-y-2 text-sm">
            <div>
              <span className="font-medium">
                {t.translations.RECORD_HISTORY_NAME_LABEL}{" "}
              </span>
              <span>{snapshot.name || placeholder}</span>
            </div>
            <div>
              <span className="font-medium">
                {t.translations.RECORD_HISTORY_UPDATED_LABEL}{" "}
              </span>
              <span>{formatDateTime(snapshot.lastUpdatedAt, placeholder)}</span>
            </div>
            <div className="flex flex-wrap gap-2 pt-1">
              <span className="badge badge-outline">
                {t.translations.RECORD_HISTORY_DATA_SOURCE_LABEL}{" "}
                {snapshot.dataSourceName || placeholder}
              </span>
              <span className="badge badge-outline">
                {t.translations.RECORD_HISTORY_ARCHIVED_LABEL}{" "}
                {snapshot.isArchived ? t.translations.YES : t.translations.NO}
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
  const { t } = useLanguage();
  const placeholderValue = t.translations.RECORD_HISTORY_NOT_AVAILABLE || "N/A";
  const [history, setHistory] = useState<HistoricalRecordResponseDto[]>([]);
  const [selectedIndex, setSelectedIndex] = useState(0);
  const [selectedSnapshot, setSelectedSnapshot] =
    useState<HistoricalRecordResponseDto | null>(null);
  const [compareMode, setCompareMode] = useState<CompareMode>("previous");
  const [manualCompareIndex, setManualCompareIndex] = useState(0);
  const [comparisonSnapshotData, setComparisonSnapshotData] =
    useState<HistoricalRecordResponseDto | null>(null);
  const [comparisonSnapshotIndex, setComparisonSnapshotIndex] = useState<
    number | null
  >(null);
  const [showOnlyChanges, setShowOnlyChanges] = useState(false);
  const [isLoadingHistory, setIsLoadingHistory] = useState(true);
  const [isLoadingSnapshot, setIsLoadingSnapshot] = useState(false);
  const [isLoadingComparisonSnapshot, setIsLoadingComparisonSnapshot] =
    useState(false);
  const [historyError, setHistoryError] = useState<string | null>(null);
  const [expandedRows, setExpandedRows] = useState<Set<string>>(new Set());

  useEffect(() => {
    let cancelled = false;

    const fetchHistory = async () => {
      setIsLoadingHistory(true);
      setHistoryError(null);

      try {
        const versions = await getRecordHistory(
          organizationId,
          projectId,
          recordId,
        );
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
          const defaultManualIndex = latestIndex > 0 ? latestIndex - 1 : 0;
          setManualCompareIndex(defaultManualIndex);
        } else {
          setSelectedIndex(0);
          setManualCompareIndex(0);
          setSelectedSnapshot(null);
          setComparisonSnapshotData(null);
          setComparisonSnapshotIndex(null);
        }
      } catch (error) {
        if (cancelled) return;
        console.error("Error fetching record history:", error);
        setHistory([]);
        setHistoryError(t.translations.FAILED_TO_LOAD_RECORD_HISTORY);
        toast.error(t.translations.FAILED_TO_LOAD_RECORD_HISTORY);
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
  }, [
    organizationId,
    projectId,
    recordId,
    t.translations.FAILED_TO_LOAD_RECORD_HISTORY,
  ]);

  useEffect(() => {
    if (history.length === 0) return;
    if (selectedIndex < 0 || selectedIndex > history.length - 1) return;

    const selectedVersion = history[selectedIndex];
    if (!selectedVersion?.lastUpdatedAt) return;

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
        console.error(
          "Error fetching snapshot for selected point-in-time:",
          error,
        );
        setSelectedSnapshot(selectedVersion);
        toast.error(
          t.translations.FAILED_TO_LOAD_SELECTED_POINT_IN_TIME_SNAPSHOT,
        );
      } finally {
        if (!cancelled) setIsLoadingSnapshot(false);
      }
    };

    fetchSnapshot();

    return () => {
      cancelled = true;
    };
  }, [
    history,
    selectedIndex,
    organizationId,
    projectId,
    recordId,
    t.translations.FAILED_TO_LOAD_SELECTED_POINT_IN_TIME_SNAPSHOT,
  ]);

  useEffect(() => {
    if (history.length === 0) return;
    if (manualCompareIndex < 0 || manualCompareIndex > history.length - 1)
      return;

    const compareVersion = history[manualCompareIndex];
    if (!compareVersion?.lastUpdatedAt) return;

    setComparisonSnapshotData(compareVersion);
    setComparisonSnapshotIndex(manualCompareIndex);

    let cancelled = false;

    const fetchComparisonSnapshot = async () => {
      setIsLoadingComparisonSnapshot(true);
      try {
        const snapshot = await getHistoricalRecord(
          organizationId,
          projectId,
          recordId,
          compareVersion.lastUpdatedAt,
          false,
        );
        if (!cancelled) {
          setComparisonSnapshotData(snapshot);
          setComparisonSnapshotIndex(manualCompareIndex);
        }
      } catch (error) {
        if (cancelled) return;
        console.error("Error fetching comparison snapshot:", error);
        setComparisonSnapshotData(compareVersion);
        setComparisonSnapshotIndex(manualCompareIndex);
        toast.error(t.translations.FAILED_TO_LOAD_COMPARISON_SNAPSHOT);
      } finally {
        if (!cancelled) setIsLoadingComparisonSnapshot(false);
      }
    };

    fetchComparisonSnapshot();

    return () => {
      cancelled = true;
    };
  }, [
    history,
    manualCompareIndex,
    organizationId,
    projectId,
    recordId,
    t.translations.FAILED_TO_LOAD_COMPARISON_SNAPSHOT,
  ]);

  const compareIndex = useMemo(() => {
    if (history.length === 0) return null;

    if (compareMode === "previous") {
      return selectedIndex > 0 ? selectedIndex - 1 : null;
    }

    if (compareMode === "latest") {
      const latestIndex = history.length - 1;
      return selectedIndex === latestIndex ? null : latestIndex;
    }

    if (compareMode === "manual") {
      return manualCompareIndex === selectedIndex ? null : manualCompareIndex;
    }

    return null;
  }, [history.length, compareMode, selectedIndex, manualCompareIndex]);

  const activeSnapshot = selectedSnapshot || history[selectedIndex] || null;
  const comparisonSnapshot =
    compareIndex !== null && compareIndex >= 0 && compareIndex < history.length
      ? comparisonSnapshotIndex === compareIndex
        ? comparisonSnapshotData || history[compareIndex]
        : history[compareIndex]
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
      const current = selectedMap[key] ?? placeholderValue;
      const compare = comparisonMap[key] ?? placeholderValue;
      const changed = current !== compare;

      return {
        field: key,
        current,
        compare,
        changed,
      };
    });
  }, [selectedMap, comparisonMap, compareMode, placeholderValue]);

  const diffTree = useMemo(() => buildDiffTree(diffRows), [diffRows]);
  const treeToRender = useMemo(
    () => (showOnlyChanges ? filterTreeForChanges(diffTree) : diffTree),
    [showOnlyChanges, diffTree],
  );
  const changedCount = diffRows.filter((row) => row.changed).length;

  useEffect(() => {
    setExpandedRows((prev) => {
      if (prev.size > 0) return prev;
      const defaults = new Set<string>();
      diffTree.forEach((node) => {
        defaults.add(node.id);
      });
      return defaults;
    });
  }, [diffTree]);

  const toggleExpand = (id: string) => {
    setExpandedRows((prev) => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  };

  const renderTreeRows = (
    nodes: DiffTreeNode[],
    depth = 0,
  ): React.ReactNode[] => {
    return nodes.flatMap((node) => {
      const hasChildren = node.children.length > 0;
      const isExpanded = expandedRows.has(node.id);
      const row = (
        <tr key={node.id} className={node.changed ? "bg-warning/10" : ""}>
          <td className="align-top">
            <div
              className="flex items-start gap-2"
              style={{ paddingLeft: `${depth * 1.25}rem` }}
            >
              {hasChildren ? (
                <button
                  type="button"
                  className="mt-0.5 rounded p-0.5 hover:bg-base-200"
                  onClick={() => toggleExpand(node.id)}
                >
                  {isExpanded ? (
                    <ChevronDownIcon className="h-4 w-4" />
                  ) : (
                    <ChevronRightIcon className="h-4 w-4" />
                  )}
                </button>
              ) : (
                <span className="inline-block w-5" />
              )}
              <div>
                <div className="font-medium">{node.label}</div>
                {hasChildren && (
                  <div className="text-xs opacity-70">
                    {isExpanded
                      ? t.translations.RECORD_HISTORY_EXPANDED
                      : t.translations.RECORD_HISTORY_COLLAPSED}{" "}
                    ({node.leafCount} {t.translations.RECORD_HISTORY_FIELDS})
                  </div>
                )}
              </div>
            </div>
          </td>
          <td className={node.changed ? "bg-warning/5 align-top" : "align-top"}>
            {hasChildren ? (
              <span className="text-xs opacity-70">
                {t.translations.RECORD_HISTORY_NESTED_GROUP}
              </span>
            ) : (
              <div className="whitespace-pre-wrap break-all text-xs">
                {node.current ?? placeholderValue}
              </div>
            )}
          </td>
          <td className={node.changed ? "bg-warning/5 align-top" : "align-top"}>
            {hasChildren ? (
              <span className="text-xs opacity-70">
                {t.translations.RECORD_HISTORY_NESTED_GROUP}
              </span>
            ) : (
              <div className="whitespace-pre-wrap break-all text-xs">
                {node.compare ?? placeholderValue}
              </div>
            )}
          </td>
          <td className="align-top">
            {node.changed ? (
              <span className="badge badge-warning badge-sm whitespace-nowrap leading-none">
                {hasChildren
                  ? t.translations.RECORD_HISTORY_CHANGED_SUBTREE
                  : t.translations.RECORD_HISTORY_CHANGED}
              </span>
            ) : (
              <span className="badge badge-ghost badge-sm whitespace-nowrap leading-none">
                {t.translations.RECORD_HISTORY_SAME}
              </span>
            )}
          </td>
        </tr>
      );

      if (hasChildren && isExpanded) {
        return [row, ...renderTreeRows(node.children, depth + 1)];
      }

      return [row];
    });
  };

  if (isLoadingHistory) {
    return (
      <div className="mt-4 card bg-base-100 shadow-lg">
        <div className="card-body">
          <div className="flex items-center gap-3">
            <span className="loading loading-spinner loading-md" />
            <p>{t.translations.LOADING_RECORD_HISTORY}</p>
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
      <div className="mt-4 card bg-base-100 shadow-lg">
        <div className="card-body">
          <h3 className="card-title">{t.translations.RECORD_HISTORY}</h3>
          <p className="opacity-80">
            {t.translations.NO_HISTORICAL_VERSIONS_FOUND_FOR_RECORD}
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="mt-4 space-y-4">
      <div className="card bg-base-100 shadow-lg">
        <div className="card-body gap-4">
          <div className="flex flex-wrap items-end gap-4">
            <div className="form-control min-w-[280px] flex-1">
              <label className="label py-1 mr-2">
                <span className="label-text font-medium">
                  {t.translations.RECORD_HISTORY_SELECT_VERSION}
                </span>
              </label>
              <select
                className="select select-bordered"
                value={selectedIndex}
                onChange={(e) => setSelectedIndex(Number(e.target.value))}
              >
                {history.map((version, index) => (
                  <option
                    key={`${version.id}-${version.lastUpdatedAt}-${index}`}
                    value={index}
                  >
                    {index + 1}.{" "}
                    {formatDateTime(version.lastUpdatedAt, placeholderValue)}
                  </option>
                ))}
              </select>
            </div>

            <div className="form-control">
              <label className="label py-1">
                <span className="label-text font-medium mr-2">
                  {t.translations.RECORD_HISTORY_COMPARE_AGAINST}
                </span>
              </label>
              <div className="join">
                <button
                  type="button"
                  className={`btn btn-sm join-item ${compareMode === "previous" ? "btn-primary" : "btn-outline"}`}
                  onClick={() => setCompareMode("previous")}
                >
                  {t.translations.RECORD_HISTORY_PREVIOUS}
                </button>
                <button
                  type="button"
                  className={`btn btn-sm join-item ${compareMode === "latest" ? "btn-primary" : "btn-outline"}`}
                  onClick={() => setCompareMode("latest")}
                >
                  {t.translations.RECORD_HISTORY_LATEST}
                </button>
                <button
                  type="button"
                  className={`btn btn-sm join-item ${compareMode === "manual" ? "btn-primary" : "btn-outline"}`}
                  onClick={() => setCompareMode("manual")}
                >
                  {t.translations.RECORD_HISTORY_MANUAL}
                </button>
              </div>
            </div>

            <div className="ml-auto text-right text-sm">
              <p className="font-medium">
                {t.translations.RECORD_HISTORY_VERSIONS}: {history.length}
              </p>
              <p className="opacity-70">
                {t.translations.RECORD_HISTORY_CHANGES_HIGHLIGHTED}:{" "}
                {changedCount}
              </p>
            </div>
          </div>

          <div className="flex justify-between">
            <input
              type="range"
              min={0}
              max={Math.max(history.length - 1, 0)}
              step={1}
              value={selectedIndex}
              onChange={(e) => setSelectedIndex(Number(e.target.value))}
              className="range range-primary range-sm"
            />

            {compareMode === "manual" && (
              <div className="flex justify-end">
                <div className="form-control flex mr-4">
                  <label className="label py-1 mr-4">
                    <span className="label-text font-medium">
                      {t.translations.RECORD_HISTORY_MANUAL_COMPARE_VERSION}
                    </span>
                  </label>
                  <select
                    className="select select-bordered"
                    value={manualCompareIndex}
                    onChange={(e) => {
                      const nextIndex = Number(e.target.value);
                      if (history.length > 1 && nextIndex === selectedIndex) {
                        const fallbackIndex =
                          nextIndex > 0 ? nextIndex - 1 : nextIndex + 1;
                        setManualCompareIndex(fallbackIndex);
                        return;
                      }
                      setManualCompareIndex(nextIndex);
                    }}
                  >
                    {history.map((version, index) => (
                      <option
                        key={`manual-${version.id}-${version.lastUpdatedAt}-${index}`}
                        value={index}
                      >
                        {index + 1}.{" "}
                        {formatDateTime(
                          version.lastUpdatedAt,
                          placeholderValue,
                        )}
                        {index === selectedIndex
                          ? ` (${t.translations.RECORD_HISTORY_CURRENTLY_SELECTED})`
                          : ""}
                      </option>
                    ))}
                  </select>
                </div>
              </div>
            )}
          </div>
        </div>
      </div>

      <div className="grid grid-cols-1 xl:grid-cols-2 gap-4">
        <SnapshotMeta
          title={t.translations.RECORD_HISTORY_SELECTED_SNAPSHOT}
          snapshot={activeSnapshot}
          t={t}
          placeholder={placeholderValue}
        />
        <SnapshotMeta
          title={t.translations.RECORD_HISTORY_COMPARISON_SNAPSHOT}
          snapshot={comparisonSnapshot}
          t={t}
          placeholder={placeholderValue}
        />
      </div>

      <div className="card bg-base-100 shadow-lg">
        <div className="card-body p-0">
          <div className="flex flex-wrap items-center justify-between gap-3 px-4 py-3 border-b border-base-300">
            <div>
              <h3 className="font-semibold">
                {t.translations.RECORD_HISTORY_VERSION_DIFFERENCE}
              </h3>
            </div>
            <label className="label cursor-pointer gap-2">
              <input
                type="checkbox"
                className="checkbox checkbox-sm"
                checked={showOnlyChanges}
                onChange={(e) => setShowOnlyChanges(e.target.checked)}
              />
              <span className="label-text">
                {t.translations.RECORD_HISTORY_SHOW_ONLY_CHANGES}
              </span>
            </label>
          </div>

          {isLoadingSnapshot && (
            <div className="px-4 py-2 text-sm opacity-75">
              <span className="loading loading-spinner loading-xs mr-2" />
              {t.translations.RECORD_HISTORY_LOADING_SELECTED_SNAPSHOT}
            </div>
          )}
          {isLoadingComparisonSnapshot && compareMode === "manual" && (
            <div className="px-4 py-2 text-sm opacity-75">
              <span className="loading loading-spinner loading-xs mr-2" />
              {t.translations.RECORD_HISTORY_LOADING_COMPARISON_SNAPSHOT}
            </div>
          )}

          <div className="overflow-auto max-h-[520px]">
            <table className="table table-zebra table-sm">
              <thead>
                <tr>
                  <th className="min-w-[200px]">
                    {t.translations.RECORD_HISTORY_FIELD}
                  </th>
                  <th className="min-w-[300px]">
                    {t.translations.RECORD_HISTORY_SELECTED}
                  </th>
                  <th className="min-w-[300px]">
                    {t.translations.RECORD_HISTORY_COMPARE}
                  </th>
                  <th>{t.translations.RECORD_HISTORY_STATUS}</th>
                </tr>
              </thead>
              <tbody>
                {treeToRender.length === 0 ? (
                  <tr>
                    <td colSpan={4} className="text-center py-8 opacity-70">
                      {
                        t.translations
                          .RECORD_HISTORY_NO_DIFFERENCES_FOR_SELECTED_COMPARISON
                      }
                    </td>
                  </tr>
                ) : (
                  renderTreeRows(treeToRender)
                )}
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </div>
  );
}
