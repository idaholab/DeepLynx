import { useLanguage } from "@/app/contexts/Language";
import { ChevronDownIcon } from "@heroicons/react/24/outline";
import React, { ReactNode, useEffect, useState } from "react";
import PaginationControls from "./PaginationControls";
import { useLocalPagination } from "../../hooks/useLocalPagination";
import { ExpandableTableColumn } from "../types/types";
import SortSelect from "./SortSelect";
import { useSortedItems } from "../hooks/useSortedItems";
import type { SortOption } from "../hooks/useSortedItems";

interface ExpandableTableProps<T> {
  data: T[];
  columns: ExpandableTableColumn<T>[];
  renderExpandedContent: (row: T, onClose: () => void) => ReactNode;
  onExplore: (row: T) => void;
  getRowId: (row: T) => string | number | undefined;
  sortOptions?: SortOption<T>[];
  defaultSortValue?: string;
}

const DATA_CELL_CLASS =
  "text-base-content first:rounded-l-lg last:rounded-r-lg border-b-4 border-base-100";
const COLLAPSED_ROW_CLASS =
  "bg-base-200/30 hover:bg-base-300/60 transition-colors shadow shadow-dynamic-shadow";

export function ExpandableTable<T>({
  data,
  columns,
  renderExpandedContent,
  onExplore,
  getRowId,
  sortOptions,
  defaultSortValue,
}: ExpandableTableProps<T>) {
  const { t } = useLanguage();

  // View state
  const [expandedRowId, setExpandedRowId] = useState<string | number | null>(
    null,
  );
  const {
    sortValue,
    setSortValue,
    sortedItems: sortedData,
  } = useSortedItems({
    items: data,
    sortOptions,
    defaultSortValue,
  });

  const {
    currentPage,
    pageSize,
    paginatedItems: paginatedRows,
    resetPagination,
    setCurrentPage,
    setPageSize,
    startIndex,
    totalPages,
  } = useLocalPagination({
    items: sortedData,
    initialPageSize: 5,
  });

  // Keep expansion and page state valid as the visible dataset changes.
  useEffect(() => {
    setExpandedRowId(null);
  }, [currentPage, pageSize, sortValue]);

  useEffect(() => {
    resetPagination();
  }, [resetPagination, sortValue]);

  useEffect(() => {
    if (expandedRowId === null) return;

    const rowStillExists = sortedData.some((row, index) => {
      const rowId = getRowId(row) ?? index;
      return rowId === expandedRowId;
    });

    if (!rowStillExists) {
      setExpandedRowId(null);
    }
  }, [expandedRowId, getRowId, sortedData]);

  // Row interaction handlers
  const toggleRow = (rowId: string | number) => {
    setExpandedRowId((currentRowId) => (currentRowId === rowId ? null : rowId));
  };

  const closeExpanded = () => setExpandedRowId(null);

  // Render helpers
  const renderHeader = () => {
    if (expandedRowId !== null) {
      return null;
    }

    return (
      <thead>
        <tr>
          {columns.map((column, index) => (
            <th key={index} className="text-base-content font-semibold">
              {column.header}
            </th>
          ))}
          <th></th>
        </tr>
      </thead>
    );
  };

  const renderExpandedRow = (row: T, rowId: string | number) => (
    <tr>
      <td colSpan={columns.length + 2} className="p-0">
        <div className="overflow-visible transition-all duration-500 ease-in-out max-h-[1000px] opacity-100">
          <div
            className="card bg-base-200 border border-base-300/30 p-6 rounded-box shadow-lg shadow-dynamic-shadow"
            data-tour={`project-row-${rowId}-expanded`}
          >
            {renderExpandedContent(row, closeExpanded)}
          </div>
        </div>
      </td>
    </tr>
  );

  const renderCollapsedRow = (row: T, rowId: string | number) => (
    <tr className={COLLAPSED_ROW_CLASS}>
      {columns.map((column, index) => {
        const shouldTriggerExpand = column.isExpandTrigger?.(row) ?? false;

        return (
          <td
            key={index}
            className={`${DATA_CELL_CLASS} ${shouldTriggerExpand ? "cursor-pointer" : ""}`}
            onClick={shouldTriggerExpand ? () => toggleRow(rowId) : undefined}
          >
            {column.data(row)}
          </td>
        );
      })}

      <td className="border-b-4 border-base-100">
        <button
          className="btn btn-sm btn-outline btn-secondary hover:btn-secondary mr-3"
          onClick={() => onExplore(row)}
        >
          {t.translations.EXPLORE}
        </button>
      </td>

      <td className="rounded-r-lg border-b-4 border-base-100 text-right">
        <button
          onClick={() => toggleRow(rowId)}
          aria-label="Expand row"
          aria-expanded={expandedRowId === rowId}
          className="p-1 rounded-lg hover:bg-base-300/50 transition-colors"
          data-tour={`project-row-${rowId}-toggle`}
        >
          <ChevronDownIcon className="size-6 text-base-content/60 hover:text-base-content transition-colors" />
        </button>
      </td>
    </tr>
  );

  return (
    <div>
      {sortOptions?.length ? (
        <SortSelect
          value={sortValue}
          onChange={setSortValue}
          options={sortOptions}
          containerClassName="flex items-center justify-end gap-1 mb-4"
        />
      ) : null}
      <table className="table w-full">
        {renderHeader()}

        <tbody>
          {paginatedRows.map((row, index) => {
            const globalIndex = startIndex + index;
            const rowId = getRowId(row) ?? globalIndex;

            return (
              <React.Fragment key={rowId}>
                {expandedRowId === rowId
                  ? renderExpandedRow(row, rowId)
                  : renderCollapsedRow(row, rowId)}
              </React.Fragment>
            );
          })}
        </tbody>
      </table>

      {/* Pagination controls */}
      <PaginationControls
        currentPage={currentPage}
        pageSize={pageSize}
        totalPages={totalPages}
        onPageChange={setCurrentPage}
        onPageSizeChange={setPageSize}
      />
    </div>
  );
}
