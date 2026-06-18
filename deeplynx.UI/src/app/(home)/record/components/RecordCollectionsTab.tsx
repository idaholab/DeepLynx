"use client";

import React, {
  useEffect,
  useMemo,
  useRef,
  useState,
  useTransition,
} from "react";
import toast from "react-hot-toast";
import { HistoricalRecordResponseDto, RecordCollectionResponseDto } from "@/app/(home)/types/responseDTOs";
import { useLanguage } from "@/app/contexts/Language";
import {
  getHistoricalRecord,
  getRecordHistory,
} from "@/app/lib/client_service/historical_record_services.client";
import { formatRecordHistoryDate } from "./RecordHistoryDate";
import RecordHistoryControls from "./RecordHistoryControls";
import RecordHistoryDifferenceTable from "./RecordHistoryDifferenceTable";
import {
  buildDifferenceTree,
  CompareMode,
  DifferenceRow,
  flattenVisibleTree,
  filterTreeForChanges,
  normalizeRecord,
} from "./RecordHistoryDifferenceUtils";
import RecordHistorySnapshotPropertiesCard from "./RecordHistorySnapshotPropertiesCard";

interface Props {
  organizationId: number;
  projectId: number;
  recordId: number;
}

export default function RecordCollectionsTab({
  organizationId,
  projectId,
  recordId,
}: Props) {
  const { t } = useLanguage();
  const placeholderValue = t.translations.RECORD_HISTORY_NOT_AVAILABLE || "N/A";

  // Source data + user selection state.
  const [collections, setCollections] = useState<RecordCollectionResponseDto[]>([]);
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
  const [isLoadingCollections, setIsLoadingCollections] = useState(true);
  const [isLoadingSnapshot, setIsLoadingSnapshot] = useState(false);
  const [isLoadingComparisonSnapshot, setIsLoadingComparisonSnapshot] =
    useState(false);
  const [collectionError, setCollectionError] = useState<string | null>(null);
  const [expandedRows, setExpandedRows] = useState<Set<string>>(new Set());
  const [sliderIndex, setSliderIndex] = useState(0);
  const [maxRenderedRows, setMaxRenderedRows] = useState(300);
  const [isUiPending, startUiTransition] = useTransition();

  // Runtime caches + debounce timers for high-frequency interactions.
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
    // Load all versions for this record and initialize default selections.
    let cancelled = false;

    const fetchHistory = async () => {
      setIsLoadingCollections(true);
      setCollectionError(null);

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

        setCollections(sorted);

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
        console.error("Error fetching record collections:", error);
        setHistory([]);
        setCollectionError(t.translations.FAILED_TO_LOAD_RECORD_COLLECTIONS);
        toast.error(t.translations.FAILED_TO_LOAD_RECORD_COLLECTIONS);
      } finally {
        if (!cancelled) setIsLoadingCollections(false);
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
    // Keep slider UI in sync when selected version changes elsewhere.
    setSliderIndex(selectedIndex);
  }, [selectedIndex]);

  useEffect(() => {
    // Keep manual compare select UI in sync with committed compare index.
    setManualCompareUiIndex(manualCompareIndex);
  }, [manualCompareIndex]);

  useEffect(() => {
    // Cleanup pending timers when component unmounts.
    return () => {
      clearSliderCommitTimer();
      clearManualCompareCommitTimer();
    };
  }, []);

  useEffect(() => {
    // Fetch selected snapshot details (with optimistic local fallback + cache).
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
    // Fetch manual comparison snapshot details (with optimistic fallback + cache).
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
    // Resolve compare mode to a concrete index in the history array.
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

  const differenceRows = useMemo<DifferenceRow[]>(() => {
    // Generate field-by-field difference rows from normalized snapshot maps.
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

  // Build the tree model used by the expandable difference table.
  const differenceTree = useMemo(
    () => buildDifferenceTree(differenceRows),
    [differenceRows],
  );
  const changedOnlyDifferenceTree = useMemo(
    () => filterTreeForChanges(differenceTree),
    [differenceTree],
  );
  const differenceTreeToRender = useMemo(
    () => (showOnlyChanges ? changedOnlyDifferenceTree : differenceTree),
    [showOnlyChanges, changedOnlyDifferenceTree, differenceTree],
  );
  const changedCount = useMemo(
    () => differenceRows.filter((row) => row.changed).length,
    [differenceRows],
  );
  const flatRows = useMemo(
    () => flattenVisibleTree(differenceTreeToRender, expandedRows),
    [differenceTreeToRender, expandedRows],
  );
  const visibleRows = useMemo(
    () => flatRows.slice(0, maxRenderedRows),
    [flatRows, maxRenderedRows],
  );
  const hasMoreRows = visibleRows.length < flatRows.length;

  // Precompute large select option lists to avoid rebuilding on every render.
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
          {index === selectedIndex
            ? ` (${t.translations.RECORD_HISTORY_CURRENTLY_SELECTED})`
            : ""}
        </option>
      )),
    [
      history,
      placeholderValue,
      selectedIndex,
      t.translations.RECORD_HISTORY_CURRENTLY_SELECTED,
    ],
  );

  useEffect(() => {
    // Reset row cap when comparison context changes.
    setMaxRenderedRows(300);
  }, [
    showOnlyChanges,
    activeSnapshot?.lastUpdatedAt,
    comparisonSnapshot?.lastUpdatedAt,
  ]);

  useEffect(() => {
    // Expand top-level groups by default on first difference tree load.
    setExpandedRows((prev) => {
      if (prev.size > 0) return prev;
      const defaults = new Set<string>();
      differenceTree.forEach((node) => defaults.add(node.id));
      return defaults;
    });
  }, [differenceTree]);

  const toggleExpand = (id: string) => {
    // Toggle expansion for a single difference subtree.
    setExpandedRows((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  if (isLoadingCollections) {
    // Initial loading state.
    return (
      <div className="mt-4 card bg-base-100 shadow-lg">
        <div className="card-body">
          <div className="flex items-center gap-3">
            <span className="loading loading-spinner loading-md" />
            <p>{t.translations.LOADING_RECORD_COLLECTIONS}</p>
          </div>
        </div>
      </div>
    );
  }

  if (collectionError) {
    // API error state.
    return (
      <div className="mt-4 alert alert-error">
        <span>{collectionError}</span>
      </div>
    );
  }

  if (history.length === 0) {
    // Empty history state.
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
    // Main history comparison UI.
    <div className="mt-4 space-y-4 p-2">
      <RecordHistoryControls
        selectedIndex={selectedIndex}
        compareMode={compareMode}
        manualCompareUiIndex={manualCompareUiIndex}
        sliderIndex={sliderIndex}
        historyLength={history.length}
        changedCount={changedCount}
        versionOptions={versionOptions}
        manualVersionOptions={manualVersionOptions}
        onSelectedIndexChange={(nextIndex) =>
          startUiTransition(() => setSelectedIndex(nextIndex))
        }
        onCompareModeChange={(mode) =>
          startUiTransition(() => setCompareMode(mode))
        }
        onSliderChange={(nextIndex) => {
          setSliderIndex(nextIndex);
          clearSliderCommitTimer();
          sliderCommitTimerRef.current = setTimeout(() => {
            startUiTransition(() => setSelectedIndex(nextIndex));
            sliderCommitTimerRef.current = null;
          }, 180);
        }}
        onSliderCommit={() => {
          clearSliderCommitTimer();
          if (sliderIndex !== selectedIndex) {
            startUiTransition(() => setSelectedIndex(sliderIndex));
          }
        }}
        onManualCompareUiChange={(nextIndex) => {
          const resolvedIndex = resolveManualCompareIndex(nextIndex);
          setManualCompareUiIndex(resolvedIndex);
          clearManualCompareCommitTimer();
          manualCompareCommitTimerRef.current = setTimeout(() => {
            startUiTransition(() => setManualCompareIndex(resolvedIndex));
            manualCompareCommitTimerRef.current = null;
          }, 150);
        }}
        onManualCompareBlur={() => {
          clearManualCompareCommitTimer();
          if (manualCompareUiIndex !== manualCompareIndex) {
            startUiTransition(() => {
              setManualCompareIndex(manualCompareUiIndex);
            });
          }
        }}
      />

      <div className="grid grid-cols-1 xl:grid-cols-2 gap-4">
        <RecordHistorySnapshotPropertiesCard
          title={t.translations.RECORD_HISTORY_SELECTED_SNAPSHOT}
          snapshot={activeSnapshot}
          placeholder={placeholderValue}
        />
        <RecordHistorySnapshotPropertiesCard
          title={t.translations.RECORD_HISTORY_COMPARISON_SNAPSHOT}
          snapshot={comparisonSnapshot}
          placeholder={placeholderValue}
        />
      </div>

      <RecordHistoryDifferenceTable
        compareMode={compareMode}
        showOnlyChanges={showOnlyChanges}
        isUiPending={isUiPending}
        isLoadingSnapshot={isLoadingSnapshot}
        isLoadingComparisonSnapshot={isLoadingComparisonSnapshot}
        visibleRows={visibleRows}
        expandedRows={expandedRows}
        placeholderValue={placeholderValue}
        hasMoreRows={hasMoreRows}
        visibleRowCount={visibleRows.length}
        totalRowCount={flatRows.length}
        onShowOnlyChangesChange={(checked) =>
          startUiTransition(() => setShowOnlyChanges(checked))
        }
        onToggleExpand={toggleExpand}
        onLoadMore={() => setMaxRenderedRows((prev) => prev + 300)}
      />
    </div>
  );
}
