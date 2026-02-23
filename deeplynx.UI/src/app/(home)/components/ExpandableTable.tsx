// src/app/(home)/components/ExpandedProjectCard.tsx
import { useLanguage } from "@/app/contexts/Language";
import {
  ChevronDownIcon,
  ChevronLeftIcon,
  ChevronRightIcon,
} from "@heroicons/react/24/outline";
import React, { useState, ReactNode, useEffect } from "react";

interface translationsProps<T> {
  data: T[];
  columns: {
    header: string;
    data: (row: T) => ReactNode;
  }[];
  renderExpandedContent: (row: T, onClose: () => void) => ReactNode;
  onExplore: (row: T) => void;
  getRowId: (row: T) => string | number | undefined;
}

const RECORDS_PER_PAGE = 5;

export function ExpandableTable<T>({
  data,
  columns,
  renderExpandedContent,
  onExplore,
  getRowId,
}: translationsProps<T>) {
  const [expandedIndex, setExpandedIndex] = useState<number | null>(null);
  const [currentPage, setCurrentPage] = useState(1);
  const { t } = useLanguage();

  const toggleRow = (index: number) => {
    setExpandedIndex(expandedIndex === index ? null : index);
  };

  const closeExpanded = () => setExpandedIndex(null);

  const totalPages = Math.ceil(data.length / RECORDS_PER_PAGE);
  const startIndex = (currentPage - 1) * RECORDS_PER_PAGE;
  const paginatedRecords = data.slice(
    startIndex,
    startIndex + RECORDS_PER_PAGE
  );

  useEffect(() => {
    setExpandedIndex(null);
  }, [currentPage]);

  return (
    <div>
      <table className="table w-full">
        {expandedIndex === null && (
          <thead>
            <tr>
              {columns.map((col, i) => (
                <th key={i} className="text-base-content font-semibold">
                  {col.header}
                </th>
              ))}
              <th></th>
            </tr>
          </thead>
        )}

        <tbody>
          {paginatedRecords.map((row, index) => {
            const globalIndex = startIndex + index;
            const rowid = getRowId(row) ?? globalIndex;
            return (
              <React.Fragment key={globalIndex}>
                {expandedIndex === globalIndex ? (
                  <tr>
                    <td colSpan={columns.length + 2} className="p-0">
                      <div className="overflow-hidden transition-all duration-500 ease-in-out max-h-[1000px] opacity-100">
                        <div
                          className="card bg-base-200 border border-base-300/30 p-6 rounded-box shadow-lg shadow-dynamic-shadow"
                          data-tour={`project-row-${rowid}-expanded`}
                        >
                          {renderExpandedContent(row, closeExpanded)}
                        </div>
                      </div>
                    </td>
                  </tr>
                ) : (
                  <tr className="bg-base-200/30 hover:bg-base-300/60 transition-colors shadow shadow-dynamic-shadow">
                    {columns.map((col, i) => (
                      <td
                        key={i}
                        className="text-base-content first:rounded-l-lg last:rounded-r-lg border-b-4 border-base-100"
                      >
                        {col.data(row)}
                      </td>
                    ))}
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
                        data-tour={`project-row-${rowid}-toggle`}
                      >
                        <ChevronDownIcon className="size-6 text-base-content/60 hover:text-base-content transition-colors" />
                      </button>
                    </td>
                  </tr>
                )}
              </React.Fragment>
            );
          })}
        </tbody>
      </table>

      {/* Pagination Controls */}
      {totalPages > 1 && (
        <div className="flex justify-end items-center gap-2 mt-4">
          <button
            className="btn btn-sm btn-ghost hover:bg-base-200"
            disabled={currentPage === 1}
            onClick={() => setCurrentPage((prev) => prev - 1)}
          >
            <ChevronLeftIcon className="size-5 text-base-content/70" />
          </button>
          <span className="px-3 text-sm text-base-content/80 font-medium">
            {t.translations.PAGE} {currentPage} {t.translations.OF} {totalPages}
          </span>
          <button
            className="btn btn-sm btn-ghost hover:bg-base-200"
            disabled={currentPage === totalPages}
            onClick={() => setCurrentPage((prev) => prev + 1)}
          >
            <ChevronRightIcon className="size-5 text-base-content/70" />
          </button>
        </div>
      )}
    </div>
  );
}
