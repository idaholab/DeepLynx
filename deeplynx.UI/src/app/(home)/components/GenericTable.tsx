"use client";

import { Column } from "@/app/(home)/types/types";
import { useLanguage } from "@/app/contexts/Language";
import {
  ChevronDownIcon,
  ChevronUpIcon,
  FolderIcon,
  LockClosedIcon,
  PresentationChartLineIcon,
  TrashIcon,
  AdjustmentsHorizontalIcon,
} from "@heroicons/react/24/outline";
import React, { useState, useEffect } from "react";
import SearchInput from "./SearchInput";

type PaginationMetadata = {
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages?: number;
};

type FilterConfig = {
  key: string;
  label: string;
  placeholder?: string;
  type?: 'text' | 'select' | 'date' | 'datetime-local';
  options?: { value: string; label: string }[];
};

type GenericTableProps<T extends object> = {
  columns: Column<T>[];
  data: T[];
  title?: string;
  filterPlaceholder?: string;
  isAnyRowSelected?: boolean;
  deleteSelectedRows?: () => void;
  rowsPerPage?: number;
  setRowsPerPage?: React.Dispatch<React.SetStateAction<number>>;
  pageLengthOptions?: number[];
  enablePageLengthChange?: boolean;
  enablePagination?: boolean;
  bordered?: boolean;
  searchBar?: boolean;
  actionButtons?: boolean;
  rowClassName?: string | ((row: T, index: number) => string);
  tableClassName?: string;
  gridView?: boolean;
  backendPagination?: boolean;
  paginationMetadata?: PaginationMetadata;
  onPageChange?: (pageNumber: number) => void;
  onPageSizeChange?: (pageSize: number) => void;
  filters?: FilterConfig[];
  filterValues?: Record<string, string | number | number[] | undefined>;
  onFilterChange?: (filters: Record<string, string | number | number[] | undefined>) => void;
};

const GenericTable = <T extends object>({
  columns,
  data,
  title,
  filterPlaceholder,
  isAnyRowSelected,
  deleteSelectedRows,
  rowsPerPage = 10,
  setRowsPerPage = () => { },
  pageLengthOptions = [10, 25, 50, 100, 500],
  enablePageLengthChange = false,
  enablePagination = false,
  bordered = false,
  searchBar = false,
  actionButtons = false,
  rowClassName,
  tableClassName,
  gridView = false,
  filters = [],
  filterValues = {},
  onFilterChange = () => { },
  backendPagination = false,
  paginationMetadata,
  onPageChange,
  onPageSizeChange,
}: GenericTableProps<T>) => {
  const { t } = useLanguage();
  const [filterText, setFilterText] = useState("");
  const [currentPage, setCurrentPage] = useState(1);
  const [currentDisplayedRows, setCurrentDisplayedRows] = useState(rowsPerPage);

  const [showFilters, setShowFilters] = useState(false);
  const [initialFilters, setInitialFilters] = useState(filterValues);
  const [tempFilters, setTempFilters] = useState<Record<string, string | number | number[] | undefined>>({});

  // Sync local page state with backend pagination metadata
  useEffect(() => {
    if (backendPagination && paginationMetadata) {
      setCurrentPage(paginationMetadata.pageNumber);
    }
  }, [backendPagination, paginationMetadata]);

  // Sync tempFilters with filterValues when they change
  useEffect(() => {
    if (filters.length > 0) {
      setTempFilters(filterValues || {});
    }
  }, [filterValues, filters.length]);

  // Filter data based on the search input
  const filteredData = React.useMemo(() => {
    return (
      data?.filter((row) =>
        columns.some((column) =>
          row[column.data as keyof T]
            ?.toString()
            .toLowerCase()
            .includes(filterText.toLowerCase())
        )
      ) || []
    );
  }, [data, columns, filterText]);

  // State and logic for column sorting
  const [sortConfig, setSortConfig] = useState<{
    key: keyof T;
    direction: "asc" | "desc";
  } | null>(null);

  // Memoize sorted data to avoid unnecessary calculations (only for client-side)
  const sortedData = React.useMemo(() => {
    if (!sortConfig) return filteredData;

    return [...filteredData].sort((a, b) => {
      const aValue = a[sortConfig.key as keyof T];
      const bValue = b[sortConfig.key as keyof T];

      if (aValue === null) return 1;
      if (bValue === null) return -1;

      if (typeof aValue === "string" && typeof bValue === "string") {
        return sortConfig.direction === "asc"
          ? aValue.localeCompare(bValue)
          : bValue.localeCompare(aValue);
      }

      if (typeof aValue === "number" && typeof bValue === "number") {
        return sortConfig.direction === "asc"
          ? aValue - bValue
          : bValue - aValue;
      }

      return 0;
    });
  }, [filteredData, sortConfig]);

  // Calculate total pages for pagination
  const totalPages =
    backendPagination && paginationMetadata
      ? paginationMetadata.totalPages ||
      Math.ceil(paginationMetadata.totalCount / paginationMetadata.pageSize)
      : enablePagination
        ? Math.ceil(filteredData.length / rowsPerPage)
        : 1;

  // Get data for the current page
  const currentData = backendPagination
    ? sortedData // Now includes filtered data from current page
    : enablePagination
      ? sortedData.slice(
        (currentPage - 1) * rowsPerPage,
        currentPage * rowsPerPage
      )
      : sortedData;

  // Handle page click for pagination
  const handlePageClick = (pageNumber: number) => {
    setCurrentPage(pageNumber);
    if (backendPagination && onPageChange) {
      onPageChange(pageNumber);
    }
  };

  // Create pagination buttons
  const createPagination = () => {
    const pagination = [];

    if (totalPages <= 6) {
      for (let i = 1; i <= totalPages; i++) {
        pagination.push(
          <button
            key={i}
            className={`join-item btn ${currentPage === i ? "bg-dynamic-blue text-white" : ""
              }`}
            onClick={() => handlePageClick(i)}
          >
            {i}
          </button>
        );
      }
    } else {
      if (currentPage > 1) {
        pagination.push(
          <button
            key="prev"
            className="join-item btn"
            onClick={() => handlePageClick(currentPage - 1)}
          >
            {t.translations.PREV}
          </button>
        );
      }

      for (let i = 1; i <= Math.min(3, totalPages); i++) {
        pagination.push(
          <button
            key={i}
            className={`join-item btn ${currentPage === i ? "bg-dynamic-blue text-white" : ""
              }`}
            onClick={() => handlePageClick(i)}
          >
            {i}
          </button>
        );
      }

      if (currentPage > 3 && currentPage <= totalPages - 3) {
        pagination.push(
          <span key="ellipsis1" className=" btn join-item btn-disabled">
            ...
          </span>
        );
        pagination.push(
          <button
            key={currentPage}
            className="join-item btn bg-dynamic-blue text-white"
            onClick={() => handlePageClick(currentPage)}
          >
            {currentPage}
          </button>
        );
        pagination.push(
          <span key="ellipsis2" className="btn join-item btn-disabled">
            ...
          </span>
        );
      } else if (currentPage >= 3 || currentPage <= 3) {
        pagination.push(
          <span key="ellipsis" className="btn join-item btn-disabled">
            ...
          </span>
        );
      }

      for (let i = Math.max(totalPages - 2, 4); i <= totalPages; i++) {
        pagination.push(
          <button
            key={i}
            className={`join-item btn ${currentPage === i ? "bg-dynamic-blue text-white" : ""
              }`}
            onClick={() => handlePageClick(i)}
          >
            {i}
          </button>
        );
      }

      if (currentPage < totalPages) {
        pagination.push(
          <button
            key="next"
            className="join-item btn"
            onClick={() => handlePageClick(currentPage + 1)}
          >
            {t.translations.NEXT}
          </button>
        );
      }
    }

    return pagination;
  };

  const handleRowLengthClick = (rowsNumber: number) => {
    setCurrentDisplayedRows(rowsNumber);
    setRowsPerPage(rowsNumber)
    setCurrentPage(1); // Reset to first page when changing rows per page
    if (backendPagination && onPageSizeChange) {
      onPageSizeChange(rowsNumber);
    }
  };

  const rowNumberSelect = () => {
    const rowOptions = [];

    for (let i = 0; i < pageLengthOptions.length; i++) {
      rowOptions.push(
        <button
          key={i}
          className={`join-item btn ${currentDisplayedRows === pageLengthOptions[i]
            ? "bg-dynamic-blue text-white"
            : ""
            }`}
          onClick={() => handleRowLengthClick(pageLengthOptions[i])}
        >
          {pageLengthOptions[i]}
        </button>
      );
    }

    return rowOptions;
  };

  // Show pagination controls if enabled and there's data
  const showPagination =
    enablePagination &&
    (backendPagination
      ? paginationMetadata && paginationMetadata.totalCount > 0
      : filteredData.length > 0);

  // Show page navigation only if there are multiple pages
  const showPageNavigation = backendPagination
    ? paginationMetadata &&
    paginationMetadata.totalCount > paginationMetadata.pageSize
    : filteredData.length > currentDisplayedRows;

  return (
    <div
      className={`overflow-x-auto min-h-[80vh] ${bordered ? "rounded-box border border-base-300" : ""
        } p-4`}
    >
      {title && (
        <h2 className="text-xl font-bold text-base-content">{title}</h2>
      )}
      <div className="my-2 flex justify-between">
        {searchBar && (
          <div className="flex gap-2">
            <SearchInput
              placeholder={filterPlaceholder}
              onChange={(e) => setFilterText(e.target.value)}
            />

            {/* Filter Button & Dropdown */}
            {filters && filters.length > 0 && (
              <div className="relative">
                <button
                  onClick={() => setShowFilters(!showFilters)}
                  className="btn bg-base-100 btn-sm gap-2 py-4.5"
                >
                  <AdjustmentsHorizontalIcon className="size-5" />
                  Filter All Results
                  {Object.keys(filterValues || {}).filter(k => filterValues?.[k]).length > 0 && (
                    <span className="badge bg-dynamic-blue text-white badge-sm border-none">
                      {Object.keys(filterValues || {}).filter(k => filterValues?.[k]).length}
                    </span>
                  )}
                  <ChevronDownIcon className={`size-4 transition-transform ${showFilters ? 'rotate-180' : ''}`} />
                </button>

                {/* Dropdown Panel */}
                {showFilters && (
                  <div className="absolute left-0 mt-2 w-96 bg-base-100 border border-base-300 rounded-lg shadow-lg z-50 p-4">
                    <div className="flex justify-between mb-4">
                      <h3 className="font-semibold text-base-content">Filter Options</h3>
                      <button
                        onClick={() => setShowFilters(false)}
                        className="btn btn-ghost btn-xs btn-circle"
                      >
                        ✕
                      </button>
                    </div>

                    {/* Filter Inputs */}
                    <div className="space-y-3 max-h-full overflow-y-auto pb-4">
                      {filters.map((filter) => {
                        const inputType = filter.type || 'text';

                        return (
                          <div key={filter.key} className="form-control">
                            <label className="label mx-1">
                              <span className="label-text">{filter.label}</span>
                            </label>
                            <input
                              type={inputType}
                              placeholder={filter.placeholder || `Enter ${filter.label.toLowerCase()}...`}
                              value={tempFilters[filter.key]?.toString() || ''}
                              onChange={(e) =>
                                setTempFilters({ ...tempFilters, [filter.key]: e.target.value })
                              }
                              className="input input-md mx-1 bg-base-100 border-base-300 focus:border-dynamic-blue focus:outline-none"
                              id={filter.key}
                            />
                          </div>
                        );
                      })}
                    </div>

                    <div className="flex gap-2 mt-4 pt-4 border-t border-base-300">
                      <button
                        onClick={() => {
                          setTempFilters(initialFilters);
                          onFilterChange?.(initialFilters);
                        }}
                        className="btn btn-ghost hover:border-base-300 btn-sm flex-1"
                      >
                        Reset Filters
                      </button>
                      <button
                        onClick={() => {
                          onFilterChange?.(tempFilters);
                          setShowFilters(false);
                        }}
                        className="btn bg-dynamic-blue text-white border-none hover:bg-dynamic-blue/60 btn-sm flex-1"
                      >
                        Apply Filters
                      </button>
                    </div>
                  </div>
                )}
              </div>
            )}
          </div>
        )}

        {showPagination && enablePageLengthChange && (
          <div className="flex justify-end items-center p-2">
            <p className="text-sm mr-2">Rows:</p>
            <div className="flex join">{rowNumberSelect()}</div>
          </div>
        )}

        {actionButtons && (
          <div className="p-2">
            <button className="mr-2 text-primary hover:text-primary-focus transition-colors">
              <FolderIcon className="size-6" />
            </button>
            <button className="mr-2 text-primary hover:text-primary-focus transition-colors">
              <PresentationChartLineIcon className="size-6" />
            </button>
            <button
              onClick={deleteSelectedRows}
              className={`transition-colors ${!isAnyRowSelected
                ? "text-base-300 cursor-not-allowed"
                : "text-error hover:text-error-focus cursor-pointer"
                }`}
              disabled={!isAnyRowSelected}
            >
              <TrashIcon className="size-6" />
            </button>
          </div>
        )}
      </div>
      <table
        className={`table table-pin-cols ${bordered ? "table-bordered" : ""} ${tableClassName ?? ""
          }`}
      >
        <thead>
          <tr
            className={`text-base-content bg-base-300 ${gridView ? "border" : ""
              }`}
          >
            {columns.map((column, index) => (
              <th
                key={index}
                className={`${gridView ? "border border-base-300 bg-base-200" : ""
                  } ${column.sortable !== false
                    ? "cursor-pointer select-none hover:bg-base-300 transition-colors"
                    : ""
                  } ${column.data === "id" ? "sticky left-0 z-10 bg-base-300" : ""
                  }`}
                onClick={() => {
                  if (column.sortable == false || !column.data) return;
                  const direction =
                    sortConfig?.key === column.data &&
                      sortConfig?.direction === "asc"
                      ? "desc"
                      : "asc";
                  setSortConfig({ key: column.data as keyof T, direction });
                }}
              >
                <div className="flex items-center gap-1">
                  {column.header}
                  {sortConfig?.key === column.data &&
                    column.sortable !== false && (
                      <>
                        {sortConfig?.direction === "asc" ? (
                          <ChevronUpIcon className="size-5" />
                        ) : (
                          <ChevronDownIcon className="size-5" />
                        )}
                      </>
                    )}
                </div>
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {currentData.map((row, rowIndex) => {
            const isPrivate = row["visibility" as keyof T] === "Private";
            const rowId = row["id" as keyof T];
            const key =
              typeof rowId === "string" || typeof rowId === "number"
                ? rowId
                : rowIndex;
            return (
              <tr
                key={rowIndex}
                className={`${typeof rowClassName === "function"
                  ? rowClassName(row, rowIndex)
                  : rowClassName || ""
                  } ${isPrivate
                    ? "opacity-60 cursor-not-allowed"
                    : "hover:bg-base-200 transition-colors"
                  }`}
              >
                {columns.map((column, colIndex) => (
                  <td
                    key={colIndex}
                    className={`text-base-content ${column.data === "id"
                      ? "sticky left-0 z-10 bg-base-100"
                      : ""
                      } ${gridView ? "border border-base-200" : ""}`}
                  >
                    {column.cell
                      ? column.cell(row, rowIndex)
                      : (row[column.data as keyof T] as React.ReactNode)}
                  </td>
                ))}
                {isPrivate && (
                  <td
                    className="text-right pr-4"
                    title="Private - request access"
                  >
                    <LockClosedIcon className="size-6 text-warning" />
                  </td>
                )}
              </tr>
            );
          })}
        </tbody>
      </table>

      <div className="flex justify-between">
        {showPageNavigation && (
          <div className="flex justify-end p-2 items-center">
            <p className="text-sm mr-2">Page:</p>
            <div className="flex join">{createPagination()}</div>
          </div>
        )}
      </div>
    </div>
  );
};

export default GenericTable;
