"use client";

import { useLanguage } from "@/app/contexts/Language";
import {
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

  return (
    <div className="flex justify-between">
      <div className="flex items-center gap-1">
        <div className="px-3 py-2 text-md font-semibold text-base-content/50">
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

      <div className="flex items-center gap-2 p-4 border-base-300/30">
        <button
          className="btn btn-sm btn-ghost hover:bg-base-200"
          disabled={currentPage === 1}
          onClick={() => onPageChange(currentPage - 1)}
        >
          <ChevronLeftIcon className="w-5 h-5 text-base-content/70" />
        </button>
        <span className="px-3 text-sm text-base-content/80 font-medium">
          {t.translations.PAGE} {currentPage} {t.translations.OF} {totalPages}
        </span>
        <button
          className="btn btn-sm btn-ghost hover:bg-base-200"
          disabled={currentPage === totalPages}
          onClick={() => onPageChange(currentPage + 1)}
        >
          <ChevronRightIcon className="w-5 h-5 text-base-content/70" />
        </button>
      </div>
    </div>
  );
}
