"use client";

import React, {
  useEffect,
  useMemo,
  useRef,
  useState,
  useTransition,
} from "react";
import toast from "react-hot-toast";
import { ChevronDownIcon, ChevronRightIcon } from "@heroicons/react/24/outline";
import { HistoricalRecordResponseDto } from "@/app/(home)/types/responseDTOs";
import { useLanguage } from "@/app/contexts/Language";
import {
  getHistoricalRecord,
  getRecordHistory,
} from "@/app/lib/client_service/historical_record_services.client";
import { formatRecordHistoryDate } from "./RecordHistoryDate";
import RecordHistorySnapshotPropertiesCard from "./RecordHistorySnapshotPropertiesCard";

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

interface FlatDiffRow {
  node: DiffTreeNode;
  depth: number;
}

function parseJsonProperties(value: unknown): unknown {
  if (typeof value !== "string") return value;
  const trimmed = value.trim();
  if (!trimmed) return value;

  const JsonCheck =
    (trimmed.startsWith("{") && trimmed.endsWith("}")) ||
    (trimmed.startsWith("[") && trimmed.endsWith("]"));

  if (!JsonCheck) return value;

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

  if (typeof value === "undefined") return;

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

  // Build a flat key/value shape so arbitrary nested values can be diffed
  // with simple string comparisons and grouped later into a tree.
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

  const parsedProperties = parseJsonProperties(record.properties);
  if (parsedProperties && typeof parsedProperties === "object") {
    flattenObject(parsedProperties, "properties", normalized);
  } else {
    set("properties", parsedProperties);
  }

  const parsedTags = parseJsonProperties(record.tags);
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

function flattenVisibleTree(
  nodes: DiffTreeNode[],
  expandedRows: Set<string>,
  depth = 0,
): FlatDiffRow[] {
  const rows: FlatDiffRow[] = [];

  nodes.forEach((node) => {
    rows.push({ node, depth });
    if (node.children.length > 0 && expandedRows.has(node.id)) {
      rows.push(...flattenVisibleTree(node.children, expandedRows, depth + 1));
    }
  });

  return rows;
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
  const [manualCompareUiIndex, setManualCompareUiIndex] = useState(0);
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
  const [sliderIndex, setSliderIndex] = useState(0);
  const [maxRenderedRows, setMaxRenderedRows] = useState(300);
  const [isUiPending, startUiTransition] = useTransition();

  // Cache snapshots by point-in-time to avoid refetching versions users revisit.
  const snapshotCacheRef = useRef<Map<string, HistoricalRecordResponseDto>>(
    new Map(),
  );
  const sliderCommitTimerRef = useRef<ReturnType<typeof setTimeout> | null>(
    null,
  );
  const manualCompareCommitTimerRef = useRef<ReturnType<
    typeof setTimeout
  > | null>(null);

  const clearSliderCommitTimer = () => {
    if (!sliderCommitTimerRef.current) return;
    clearTimeout(sliderCommitTimerRef.current);
    sliderCommitTimerRef.current = null;
  };

  const clearManualCompareCommitTimer = () => {
    if (!manualCompareCommitTimerRef.current) return;
    clearTimeout(manualCompareCommitTimerRef.current);
    manualCompareCommitTimerRef.current = null;
  };

  const resolveManualCompareIndex = (nextIndex: number): number => {
    if (history.length > 1 && nextIndex === selectedIndex) {
      return nextIndex > 0 ? nextIndex - 1 : nextIndex + 1;
    }
    return nextIndex;
  };

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
        // History is shown chronologically; newest entry is selected by default.
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
          setSliderIndex(latestIndex);
          const defaultManualIndex = latestIndex > 0 ? latestIndex - 1 : 0;
          setManualCompareIndex(defaultManualIndex);
          setManualCompareUiIndex(defaultManualIndex);
        } else {
          setSelectedIndex(0);
          setSliderIndex(0);
          setManualCompareIndex(0);
          setManualCompareUiIndex(0);
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
        if (!cancelled) setIsLoadingHistory(false);
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
    setSliderIndex(selectedIndex);
  }, [selectedIndex]);

  useEffect(() => {
    setManualCompareUiIndex(manualCompareIndex);
  }, [manualCompareIndex]);

  useEffect(() => {
    return () => {
      clearSliderCommitTimer();
      clearManualCompareCommitTimer();
    };
  }, []);

  useEffect(() => {
    if (history.length === 0) return;
    if (selectedIndex < 0 || selectedIndex > history.length - 1) return;

    const selectedVersion = history[selectedIndex];
    if (!selectedVersion?.lastUpdatedAt) return;

    // Use list payload immediately for responsive UI, then hydrate with the
    // point-in-time record endpoint (includes full snapshot details).
    setSelectedSnapshot(selectedVersion);

    const snapshotCacheKey = selectedVersion.lastUpdatedAt;
    const cachedSnapshot = snapshotCacheRef.current.get(snapshotCacheKey);
    if (cachedSnapshot) {
      setSelectedSnapshot(cachedSnapshot);
      return;
    }

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
        if (!cancelled) {
          snapshotCacheRef.current.set(snapshotCacheKey, snapshot);
          setSelectedSnapshot(snapshot);
        }
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

    // Same optimistic/hydration approach for manual comparison target.
    setComparisonSnapshotData(compareVersion);
    setComparisonSnapshotIndex(manualCompareIndex);

    const snapshotCacheKey = compareVersion.lastUpdatedAt;
    const cachedSnapshot = snapshotCacheRef.current.get(snapshotCacheKey);
    if (cachedSnapshot) {
      setComparisonSnapshotData(cachedSnapshot);
      setComparisonSnapshotIndex(manualCompareIndex);
      return;
    }

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
          snapshotCacheRef.current.set(snapshotCacheKey, snapshot);
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

    // Translate compare mode into a concrete history index, or null when
    // comparison is not meaningful (e.g. latest vs latest).
    if (compareMode === "previous") {
      return selectedIndex > 0 ? selectedIndex - 1 : null;
    }

    if (compareMode === "latest") {
      const latestIndex = history.length - 1;
      return selectedIndex === latestIndex ? null : latestIndex;
    }

    return manualCompareIndex === selectedIndex ? null : manualCompareIndex;
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

      return {
        field: key,
        current,
        compare,
        changed: current !== compare,
      };
    });
  }, [selectedMap, comparisonMap, placeholderValue]);

  // Convert flattened rows to a nested path tree for collapsible rendering.
  const diffTree = useMemo(() => buildDiffTree(diffRows), [diffRows]);
  const changedOnlyTree = useMemo(
    () => filterTreeForChanges(diffTree),
    [diffTree],
  );
  const treeToRender = useMemo(
    () => (showOnlyChanges ? changedOnlyTree : diffTree),
    [showOnlyChanges, changedOnlyTree, diffTree],
  );
  const changedCount = useMemo(
    () => diffRows.filter((row) => row.changed).length,
    [diffRows],
  );
  const flatRows = useMemo(
    () => flattenVisibleTree(treeToRender, expandedRows),
    [treeToRender, expandedRows],
  );
  const visibleRows = useMemo(
    () => flatRows.slice(0, maxRenderedRows),
    [flatRows, maxRenderedRows],
  );
  const hasMoreRows = visibleRows.length < flatRows.length;

  const versionOptions = useMemo(
    () =>
      history.map((version, index) => (
        <option
          key={`${version.id}-${version.lastUpdatedAt}-${index}`}
          value={index}
        >
          {index + 1}.{" "}
          {formatRecordHistoryDate(version.lastUpdatedAt, placeholderValue)}
        </option>
      )),
    [history, placeholderValue],
  );
  const manualVersionOptions = useMemo(
    () =>
      history.map((version, index) => (
        <option
          key={`manual-${version.id}-${version.lastUpdatedAt}-${index}`}
          value={index}
        >
          {index + 1}.{" "}
          {formatRecordHistoryDate(version.lastUpdatedAt, placeholderValue)}
        </option>
      )),
    [history, placeholderValue],
  );

  useEffect(() => {
    setMaxRenderedRows(300);
  }, [
    showOnlyChanges,
    activeSnapshot?.lastUpdatedAt,
    comparisonSnapshot?.lastUpdatedAt,
  ]);

  useEffect(() => {
    setExpandedRows((prev) => {
      if (prev.size > 0) return prev;
      const defaults = new Set<string>();
      diffTree.forEach((node) => defaults.add(node.id));
      return defaults;
    });
  }, [diffTree]);

  const toggleExpand = (id: string) => {
    setExpandedRows((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
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
                onChange={(e) => {
                  const nextIndex = Number(e.target.value);
                  startUiTransition(() => setSelectedIndex(nextIndex));
                }}
              >
                {versionOptions}
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
                  onClick={() =>
                    startUiTransition(() => setCompareMode("previous"))
                  }
                >
                  {t.translations.RECORD_HISTORY_PREVIOUS}
                </button>
                <button
                  type="button"
                  className={`btn btn-sm join-item ${compareMode === "latest" ? "btn-primary" : "btn-outline"}`}
                  onClick={() =>
                    startUiTransition(() => setCompareMode("latest"))
                  }
                >
                  {t.translations.RECORD_HISTORY_LATEST}
                </button>
                <button
                  type="button"
                  className={`btn btn-sm join-item ${compareMode === "manual" ? "btn-primary" : "btn-outline"}`}
                  onClick={() =>
                    startUiTransition(() => setCompareMode("manual"))
                  }
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
              value={sliderIndex}
              onChange={(e) => {
                const nextIndex = Number(e.target.value);
                setSliderIndex(nextIndex);
                clearSliderCommitTimer();
                sliderCommitTimerRef.current = setTimeout(() => {
                  startUiTransition(() => setSelectedIndex(nextIndex));
                  sliderCommitTimerRef.current = null;
                }, 180);
              }}
              onMouseUp={() => {
                clearSliderCommitTimer();
                if (sliderIndex !== selectedIndex) {
                  startUiTransition(() => setSelectedIndex(sliderIndex));
                }
              }}
              onTouchEnd={() => {
                clearSliderCommitTimer();
                if (sliderIndex !== selectedIndex) {
                  startUiTransition(() => setSelectedIndex(sliderIndex));
                }
              }}
              onBlur={() => {
                clearSliderCommitTimer();
                if (sliderIndex !== selectedIndex) {
                  startUiTransition(() => setSelectedIndex(sliderIndex));
                }
              }}
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
                    value={manualCompareUiIndex}
                    onChange={(e) => {
                      const nextIndex = Number(e.target.value);
                      const resolvedIndex =
                        resolveManualCompareIndex(nextIndex);
                      setManualCompareUiIndex(resolvedIndex);
                      clearManualCompareCommitTimer();
                      manualCompareCommitTimerRef.current = setTimeout(() => {
                        startUiTransition(() =>
                          setManualCompareIndex(resolvedIndex),
                        );
                        manualCompareCommitTimerRef.current = null;
                      }, 150);
                    }}
                    onBlur={() => {
                      clearManualCompareCommitTimer();
                      if (manualCompareUiIndex !== manualCompareIndex) {
                        startUiTransition(() => {
                          setManualCompareIndex(manualCompareUiIndex);
                        });
                      }
                    }}
                  >
                    {manualVersionOptions}
                  </select>
                </div>
              </div>
            )}
          </div>
        </div>
      </div>

      <div className="grid grid-cols-1 xl:grid-cols-2 gap-4">
        <RecordHistorySnapshotPropertiesCard
          title={t.translations.RECORD_HISTORY_SELECTED_SNAPSHOT}
          snapshot={activeSnapshot}
          placeholder={placeholderValue}
          labels={{
            noSelection:
              t.translations.RECORD_HISTORY_NO_COMPARISON_VERSION_SELECTED,
            name: t.translations.RECORD_HISTORY_NAME_LABEL,
            updated: t.translations.RECORD_HISTORY_UPDATED_LABEL,
            dataSource: t.translations.RECORD_HISTORY_DATA_SOURCE_LABEL,
            archived: t.translations.RECORD_HISTORY_ARCHIVED_LABEL,
            yes: t.translations.YES,
            no: t.translations.NO,
          }}
        />
        <RecordHistorySnapshotPropertiesCard
          title={t.translations.RECORD_HISTORY_COMPARISON_SNAPSHOT}
          snapshot={comparisonSnapshot}
          placeholder={placeholderValue}
          labels={{
            noSelection:
              t.translations.RECORD_HISTORY_NO_COMPARISON_VERSION_SELECTED,
            name: t.translations.RECORD_HISTORY_NAME_LABEL,
            updated: t.translations.RECORD_HISTORY_UPDATED_LABEL,
            dataSource: t.translations.RECORD_HISTORY_DATA_SOURCE_LABEL,
            archived: t.translations.RECORD_HISTORY_ARCHIVED_LABEL,
            yes: t.translations.YES,
            no: t.translations.NO,
          }}
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
                onChange={(e) =>
                  startUiTransition(() => setShowOnlyChanges(e.target.checked))
                }
              />
              <span className="label-text">
                {t.translations.RECORD_HISTORY_SHOW_ONLY_CHANGES}
              </span>
            </label>
          </div>
          {isUiPending && (
            <div className="px-4 py-2 text-xs opacity-70">
              {t.translations.LOADING}
            </div>
          )}

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
                {visibleRows.length === 0 ? (
                  <tr>
                    <td colSpan={4} className="text-center py-8 opacity-70">
                      {
                        t.translations
                          .RECORD_HISTORY_NO_DIFFERENCES_FOR_SELECTED_COMPARISON
                      }
                    </td>
                  </tr>
                ) : (
                  visibleRows.map(({ node, depth }) => {
                    const hasChildren = node.children.length > 0;
                    const isExpanded = expandedRows.has(node.id);
                    return (
                      <tr
                        key={node.id}
                        className={node.changed ? "bg-warning/10" : ""}
                      >
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
                                    : t.translations
                                        .RECORD_HISTORY_COLLAPSED}{" "}
                                  ({node.leafCount}{" "}
                                  {t.translations.RECORD_HISTORY_FIELDS})
                                </div>
                              )}
                            </div>
                          </div>
                        </td>
                        <td
                          className={
                            node.changed
                              ? "bg-warning/5 align-top"
                              : "align-top"
                          }
                        >
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
                        <td
                          className={
                            node.changed
                              ? "bg-warning/5 align-top"
                              : "align-top"
                          }
                        >
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
                  })
                )}
              </tbody>
            </table>
          </div>
          {hasMoreRows && (
            <div className="px-4 py-3 border-t border-base-300">
              <button
                type="button"
                className="btn btn-outline btn-sm"
                onClick={() => setMaxRenderedRows((prev) => prev + 300)}
              >
                Load more ({visibleRows.length}/{flatRows.length})
              </button>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
