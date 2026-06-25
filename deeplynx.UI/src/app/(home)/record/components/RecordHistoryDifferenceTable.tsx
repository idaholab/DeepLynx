import React from "react";
import { ChevronDownIcon, ChevronRightIcon } from "@heroicons/react/24/outline";
import { useLanguage } from "@/app/contexts/Language";
import { CompareMode, FlatDifferenceRow } from "./RecordHistoryDifferenceUtils";

interface Props {
  compareMode: CompareMode;
  showOnlyChanges: boolean;
  isUiPending: boolean;
  isLoadingSnapshot: boolean;
  isLoadingComparisonSnapshot: boolean;
  visibleRows: FlatDifferenceRow[];
  expandedRows: Set<string>;
  placeholderValue: string;
  hasMoreRows: boolean;
  visibleRowCount: number;
  totalRowCount: number;
  onShowOnlyChangesChange: (checked: boolean) => void;
  onToggleExpand: (id: string) => void;
  onLoadMore: () => void;
  activeClassNames: Set<string>;
}

export default function RecordHistoryDifferenceTable({
  compareMode,
  showOnlyChanges,
  isUiPending,
  isLoadingSnapshot,
  isLoadingComparisonSnapshot,
  visibleRows,
  expandedRows,
  placeholderValue,
  hasMoreRows,
  visibleRowCount,
  totalRowCount,
  onShowOnlyChangesChange,
  onToggleExpand,
  onLoadMore,
  activeClassNames
}: Props) {
  const { t } = useLanguage();

  function normalizeClassNameWithArchivedLabel(
    className: string | undefined | null,
    activeClassNames: Set<string>
  ): string {
    if (!className) return t.translations.NO_CLASS;

    if (!activeClassNames.has(className.trim())) {
      return `${className} (Archived)`;
    }

    return className;
  }

  return (
    // Difference card: filters, loading states, and expandable difference tree table.
    <div className="card border border-base-300/50 bg-base-100 shadow-sm">
      <div className="card-body p-0">
        {/* Header row with "show only changes" toggle. */}
        <div className="flex flex-wrap items-center justify-between gap-3 px-4 py-3 border-b border-base-300/50">
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
              onChange={(e) => onShowOnlyChangesChange(e.target.checked)}
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

        {/* Snapshot loading indicators. */}
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

        {/* Expandable field-by-field difference tree. */}
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
                visibleRows.map(({ node, depth }, _) => {
                  const hasChildren = node.children.length > 0;
                  const isExpanded = expandedRows.has(node.id);

                  const isClassNameRow = node.field === "record.className";

                  const currentValue = isClassNameRow
                    ? normalizeClassNameWithArchivedLabel(node.current, activeClassNames)
                    : node.current ?? placeholderValue;

                  const compareValue = isClassNameRow
                    ? normalizeClassNameWithArchivedLabel(node.compare, activeClassNames)
                    : node.compare ?? placeholderValue;

                  return (
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
                              onClick={() => onToggleExpand(node.id)}
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
                      <td
                        className={
                          node.changed ? "bg-warning/5 align-top" : "align-top"
                        }
                      >
                        {hasChildren ? (
                          <span className="text-xs opacity-70">
                            {t.translations.RECORD_HISTORY_NESTED_GROUP}
                          </span>
                        ) : (
                          <div className="whitespace-pre-wrap break-all text-xs">
                            {currentValue}
                          </div>
                        )}
                      </td>
                      <td
                        className={
                          node.changed ? "bg-warning/5 align-top" : "align-top"
                        }
                      >
                        {hasChildren ? (
                          <span className="text-xs opacity-70">
                            {t.translations.RECORD_HISTORY_NESTED_GROUP}
                          </span>
                        ) : (
                          <div className="whitespace-pre-wrap break-all text-xs">
                            {compareValue}
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
        {/* Incremental rendering control for very large trees. */}
        {hasMoreRows && (
          <div className="px-4 py-3 border-t border-base-300/50">
            <button
              type="button"
              className="btn btn-outline btn-sm"
              onClick={onLoadMore}
            >
              Load more ({visibleRowCount}/{totalRowCount})
            </button>
          </div>
        )}
      </div>
    </div>
  );
}
