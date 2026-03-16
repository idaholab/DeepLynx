import { useLanguage } from "@/app/contexts/Language";
import { ChevronDownIcon } from "@heroicons/react/24/outline";
import React, { ReactNode, useEffect, useState } from "react";
import PaginationControls from "./PaginationControls";
import { useLocalPagination } from "../../hooks/useLocalPagination";

type ExpandableTableColumn<T> = {
  header: string;
  data: (row: T) => ReactNode;
  isExpandTrigger?: (row: T) => boolean;
};

interface ExpandableTableProps<T> {
  data: T[];
  columns: ExpandableTableColumn<T>[];
  renderExpandedContent: (row: T, onClose: () => void) => ReactNode;
  onExplore: (row: T) => void;
  getRowId: (row: T) => string | number | undefined;
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
}: ExpandableTableProps<T>) {
  const { t } = useLanguage();

  // View state
  const [expandedIndex, setExpandedIndex] = useState<number | null>(null);
  const {
    currentPage,
    pageSize,
    paginatedItems: paginatedRows,
    setCurrentPage,
    setPageSize,
    startIndex,
    totalPages,
  } = useLocalPagination({
    items: data,
    initialPageSize: 5,
  });

  // Keep expansion and page state valid as the visible dataset changes.
  useEffect(() => {
    setExpandedIndex(null);
  }, [currentPage, pageSize]);

  // Row interaction handlers
  const toggleRow = (index: number) => {
    setExpandedIndex((currentIndex) => (currentIndex === index ? null : index));
  };

  const closeExpanded = () => setExpandedIndex(null);

  // Render helpers
  const renderHeader = () => {
    if (expandedIndex !== null) {
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
        <div className="overflow-hidden transition-all duration-500 ease-in-out max-h-[1000px] opacity-100">
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

  const renderCollapsedRow = (
    row: T,
    globalIndex: number,
    rowId: string | number,
  ) => (
    <tr className={COLLAPSED_ROW_CLASS}>
      {columns.map((column, index) => {
        const shouldTriggerExpand = column.isExpandTrigger?.(row) ?? false;

        return (
          <td
            key={index}
            className={`${DATA_CELL_CLASS} ${shouldTriggerExpand ? "cursor-pointer" : ""}`}
            onClick={
              shouldTriggerExpand ? () => toggleRow(globalIndex) : undefined
            }
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
          onClick={() => toggleRow(globalIndex)}
          aria-label="Expand row"
          aria-expanded={expandedIndex === globalIndex}
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
      <table className="table w-full">
        {renderHeader()}

        <tbody>
          {paginatedRows.map((row, index) => {
            const globalIndex = startIndex + index;
            const rowId = getRowId(row) ?? globalIndex;

            return (
              <React.Fragment key={globalIndex}>
                {expandedIndex === globalIndex
                  ? renderExpandedRow(row, rowId)
                  : renderCollapsedRow(row, globalIndex, rowId)}
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
