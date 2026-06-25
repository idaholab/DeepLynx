"use client";

import PaginationControls from "@/app/(home)/components/PaginationControls";
import SearchInput from "@/app/(home)/components/SearchInput";
import { useLanguage } from "@/app/contexts/Language";
import { formatLocalDateTime } from "@/app/lib/date_time";
import React from "react";
import { NewCollectionSelectedRecord } from "./recordCollections.types";
import { interpolateTemplate } from "@/app/lib/record_helpers";

type Props = {
  title: string;
  shownCount: number;
  totalCount: number;
  searchTerm: string;
  setSearchTerm: React.Dispatch<React.SetStateAction<string>>;
  records: NewCollectionSelectedRecord[];
  emptyMessage: string;
  currentPage: number;
  setCurrentPage: React.Dispatch<React.SetStateAction<number>>;
  pageCount: number;
  pageSize: number;
  pageSizeOptions: number[];
  onPageSizeChange: (pageSize: number) => void;
};

export default function SelectedRecordsPreviewPanel({
  title,
  shownCount,
  totalCount,
  searchTerm,
  setSearchTerm,
  records,
  emptyMessage,
  currentPage,
  setCurrentPage,
  pageCount,
  pageSize,
  pageSizeOptions,
  onPageSizeChange,
}: Props) {
  const { t } = useLanguage();

  return (
    <div className="mt-2 rounded-2xl border border-base-300/50 bg-base-100 p-4">
      <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
        <div>
          <h3 className="font-semibold text-base-content">{title}</h3>
          <p className="text-sm text-base-content/70">
            {interpolateTemplate(
              t.translations.RECORD_COLLECTIONS_SELECTED_RECORDS_SHOWN,
              { shown: shownCount, total: totalCount },
            )}
          </p>
        </div>
        <SearchInput
          className="min-w-0 lg:w-72"
          placeholder={t.translations.RECORD_COLLECTIONS_SEARCH_SELECTED_RECORDS}
          value={searchTerm}
          size="sm"
          onChange={(event) => setSearchTerm(event.target.value)}
        />
      </div>

      <div className="mt-4 max-h-48 overflow-auto rounded-xl border border-base-300/50">
        <table className="table table-sm">
          <thead>
            <tr>
              <th>{t.translations.RECORD}</th>
              <th>{t.translations.RECORD_COLLECTIONS_CLASS}</th>
              <th>{t.translations.RECORD_COLLECTIONS_SOURCE}</th>
              <th>{t.translations.RECORD_COLLECTIONS_UPDATED}</th>
            </tr>
          </thead>
          <tbody>
            {records.length ? (
              records.map((record) => (
                <tr key={record.id}>
                  <td className="font-medium">
                    {record.name ?? t.translations.RECORD_COLLECTIONS_UNNAMED_RECORD}
                  </td>
                  <td>{record.className ?? t.translations.RECORD_COLLECTIONS_UNCLASSIFIED}</td>
                  <td>{record.dataSourceName ?? t.translations.UNKNOWN}</td>
                  <td>
                    {record.lastUpdatedAt
                      ? formatLocalDateTime(record.lastUpdatedAt)
                      : t.translations.RECORD_COLLECTIONS_NOT_UPDATED}
                  </td>
                </tr>
              ))
            ) : (
              <tr>
                <td colSpan={4}>{emptyMessage}</td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {shownCount > pageSize ? (
        <div className="mt-3 flex flex-col gap-3 text-sm sm:flex-row sm:items-center sm:justify-between">
          <span className="text-base-content/70">
            {`${t.translations.SHOWING} ${(currentPage - 1) * pageSize + 1}-${Math.min(
              currentPage * pageSize,
              shownCount,
            )} ${t.translations.OF} ${shownCount}`}
          </span>
          <PaginationControls
            currentPage={currentPage}
            pageSize={pageSize}
            totalPages={pageCount}
            pageSizeOptions={pageSizeOptions}
            onPageChange={setCurrentPage}
            onPageSizeChange={onPageSizeChange}
          />
        </div>
      ) : null}
    </div>
  );
}
