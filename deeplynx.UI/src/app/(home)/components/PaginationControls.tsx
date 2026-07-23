"use client";

import { useEffect, useState } from "react";
import { useLanguage } from "@/app/contexts/Language";
import {
  ChevronDoubleLeftIcon,
  ChevronDoubleRightIcon,
  ChevronLeftIcon,
  ChevronRightIcon,
} from "@heroicons/react/24/outline";

type PaginationControlsProps = {
  currentPage: number;
  pageSize: number;
  totalPages: number;
  pageSizeOptions?: number[];
  onPageChange: (page: number) => void;
  onPageSizeChange: (pageSize: number) => void;
};

export const DEFAULT_PAGE_SIZE_OPTIONS = [5, 10, 25, 50];

export default function PaginationControls({
  currentPage,
  pageSize,
  totalPages,
  pageSizeOptions = DEFAULT_PAGE_SIZE_OPTIONS,
  onPageChange,
  onPageSizeChange,
}: PaginationControlsProps) {
  const { t } = useLanguage();
  const boundedTotalPages = Math.max(1, totalPages);
  const [pageInput, setPageInput] = useState(String(currentPage));

  useEffect(() => {
    setPageInput(String(currentPage));
  }, [currentPage]);

  const goToPage = () => {
    const requestedPage = Number(pageInput);

    if (!Number.isFinite(requestedPage)) {
      setPageInput(String(currentPage));
      return;
    }

    const nextPage = Math.min(
      Math.max(1, Math.trunc(requestedPage)),
      boundedTotalPages,
    );

    setPageInput(String(nextPage));

    if (nextPage !== currentPage) {
      onPageChange(nextPage);
    }
  };

  const isFirstPage = currentPage <= 1;
  const isLastPage = currentPage >= boundedTotalPages;

  return (
    <div className="@container flex flex-row flex-nowrap items-center justify-between gap-1 min-w-0 w-full">
      <div className="flex items-center gap-1 shrink-0 min-w-0">
        <div className="hidden @sm:block px-2 py-2 text-md font-semibold text-base-content/50 whitespace-nowrap">
          {t.translations.SHOW}
        </div>
        <div className="relative inline-block">
          <select
            className="select"
            value={pageSize}
            onChange={(e) => onPageSizeChange(Number(e.target.value))}
          >
            {pageSizeOptions.map((size) => (
              <option key={size} value={size}>
                {size}
              </option>
            ))}
          </select>
        </div>
      </div>

      <div className="flex flex-nowrap items-center justify-end gap-1 py-2 border-base-300/30 shrink-0 min-w-0">
        <button
          type="button"
          aria-label="First page"
          title="First page"
          className="btn btn-sm btn-square btn-ghost hover:bg-base-200"
          disabled={isFirstPage}
          onClick={() => onPageChange(1)}
        >
          <ChevronDoubleLeftIcon className="w-5 h-5 text-base-content/70" />
        </button>
        <button
          type="button"
          aria-label="Previous page"
          title="Previous page"
          className="btn btn-sm btn-square btn-ghost hover:bg-base-200"
          disabled={isFirstPage}
          onClick={() => onPageChange(currentPage - 1)}
        >
          <ChevronLeftIcon className="w-5 h-5 text-base-content/70" />
        </button>

        <div className="flex items-center gap-1 px-1 text-sm font-medium text-base-content/80 whitespace-nowrap">
          <span className="hidden @sm:inline">{t.translations.PAGE}</span>
          <input
            type="number"
            aria-label="Go to page"
            className="input input-sm input-bordered w-16 text-center"
            min={1}
            max={boundedTotalPages}
            value={pageInput}
            onChange={(e) => setPageInput(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === "Enter") {
                e.preventDefault();
                goToPage();
              }
            }}
          />
          <span>
            {t.translations.OF} {boundedTotalPages}
          </span>
        </div>

        <button
          type="button"
          aria-label="Next page"
          title="Next page"
          className="btn btn-sm btn-square btn-ghost hover:bg-base-200"
          disabled={isLastPage}
          onClick={() => onPageChange(currentPage + 1)}
        >
          <ChevronRightIcon className="w-5 h-5 text-base-content/70" />
        </button>
        <button
          type="button"
          aria-label="Last page"
          title="Last page"
          className="btn btn-sm btn-square btn-ghost hover:bg-base-200"
          disabled={isLastPage}
          onClick={() => onPageChange(boundedTotalPages)}
        >
          <ChevronDoubleRightIcon className="w-5 h-5 text-base-content/70" />
        </button>
      </div>
    </div>
  );
}
