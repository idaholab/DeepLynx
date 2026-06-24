"use client";

import { useLanguage } from "@/app/contexts/Language";
import { formatLocalDateTime } from "@/app/lib/date_time";
import React from "react";

type Row = {
  key: React.Key;
  leadingCell?: React.ReactNode;
  name: React.ReactNode;
  className: React.ReactNode;
  sourceName: React.ReactNode;
  updatedAt?: string | null;
  actionCell?: React.ReactNode;
};

type Props = {
  rows: Row[];
  emptyMessage: string;
  maxHeightClassName?: string;
  pinnedHeader?: boolean;
  leadingHeaderCell?: React.ReactNode;
  actionHeaderCell?: React.ReactNode;
};

export default function CollectionRecordSearchResultsTable({
  rows,
  emptyMessage,
  maxHeightClassName = "max-h-72",
  pinnedHeader = true,
  leadingHeaderCell,
  actionHeaderCell,
}: Props) {
  const { t } = useLanguage();
  const showLeadingColumn = rows.some((row) => row.leadingCell !== undefined);
  const showActionColumn = rows.some((row) => row.actionCell !== undefined);
  const columnCount =
    4 + (showLeadingColumn ? 1 : 0) + (showActionColumn ? 1 : 0);

  return (
    <div
      className={`mt-4 overflow-auto rounded-xl border border-base-300 bg-base-100 ${maxHeightClassName}`}
    >
      <table className={`table table-sm ${pinnedHeader ? "table-pin-rows" : ""}`}>
        <thead className={pinnedHeader ? "bg-base-100" : undefined}>
          <tr>
            {showLeadingColumn ? <th>{leadingHeaderCell ?? null}</th> : null}
            <th>{t.translations.RECORD}</th>
            <th>{t.translations.RECORD_COLLECTIONS_CLASS}</th>
            <th>{t.translations.RECORD_COLLECTIONS_SOURCE}</th>
            <th>{t.translations.RECORD_COLLECTIONS_UPDATED}</th>
            {showActionColumn ? <th>{actionHeaderCell ?? null}</th> : null}
          </tr>
        </thead>
        <tbody>
          {rows.length ? (
            rows.map((row) => (
              <tr key={row.key}>
                {showLeadingColumn ? <td>{row.leadingCell ?? null}</td> : null}
                <td className="font-medium">{row.name}</td>
                <td>{row.className}</td>
                <td>{row.sourceName}</td>
                <td>
                  {row.updatedAt
                    ? formatLocalDateTime(row.updatedAt)
                    : t.translations.RECORD_COLLECTIONS_NOT_UPDATED}
                </td>
                {showActionColumn ? (
                  <td className="text-right">{row.actionCell ?? null}</td>
                ) : null}
              </tr>
            ))
          ) : (
            <tr>
              <td colSpan={columnCount}>{emptyMessage}</td>
            </tr>
          )}
        </tbody>
      </table>
    </div>
  );
}
