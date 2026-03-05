"use client";

import { BulkRecord } from "../../types/bulk_upload_types";
import { useState } from "react";
import { useLanguage } from "@/app/contexts/Language";

function interpolate(
  template: string,
  values: Record<string, string | number>,
): string {
  return Object.entries(values).reduce(
    (result, [key, value]) => result.replace(`{${key}}`, String(value)),
    template,
  );
}

interface RecordPreviewTableProps {
  records: BulkRecord[];
  maxVisible?: number;
}

export default function RecordPreviewTable({
  records,
  maxVisible = 10,
}: RecordPreviewTableProps) {
  const { t } = useLanguage();
  const [showAll, setShowAll] = useState(false);
  const displayedRecords = showAll ? records : records.slice(0, maxVisible);
  const hasMore = records.length > maxVisible;

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between">
        <h4 className="font-semibold text-base-content">
          {interpolate(t.translations.RECORD_PREVIEW_WITH_TOTAL, {
            total: records.length,
          })}
        </h4>
        {hasMore && (
          <button
            onClick={() => setShowAll(!showAll)}
            className="btn btn-ghost btn-xs"
            type="button"
          >
            {showAll
              ? t.translations.SHOW_LESS
              : interpolate(t.translations.SHOW_ALL_WITH_COUNT, {
                  count: records.length,
                })}
          </button>
        )}
      </div>

      <div className="overflow-x-auto max-h-96 border rounded-lg">
        <table className="table table-zebra table-sm">
          <thead className="sticky top-0 bg-base-200 z-10">
            <tr>
              <th className="w-12">#</th>
              <th>{t.translations.NAME}</th>
              <th>{t.translations.DESCRIPTION}</th>
              <th>{t.translations.ORIGINAL_ID}</th>
              <th>{t.translations.CLASS}</th>
              <th>{t.translations.OBJECT_STORAGE}</th>
              <th>{t.translations.TAGS}</th>
            </tr>
          </thead>
          <tbody>
            {displayedRecords.map((record, idx) => (
              <tr key={idx} className="hover">
                <td className="font-mono text-xs">{idx + 1}</td>
                <td
                  className="font-medium max-w-xs truncate"
                  title={record.name}
                >
                  {record.name}
                </td>
                <td
                  className="max-w-xs truncate text-sm"
                  title={record.description}
                >
                  {record.description}
                </td>
                <td className="font-mono text-xs">{record.original_id}</td>
                <td className="text-sm">
                  {record.class_name || record.class_id ? (
                    <span className="badge badge-sm badge-ghost">
                      {record.class_name ||
                        `${t.translations.ID_LABEL} ${record.class_id}`}
                    </span>
                  ) : (
                    <span className="text-base-content/40">-</span>
                  )}
                </td>
                <td className="text-sm">
                  {record.object_storage_id ? (
                    <span className="badge badge-sm badge-info">
                      {record.object_storage_id}
                    </span>
                  ) : (
                    <span className="text-base-content/40">-</span>
                  )}
                </td>
                <td className="text-sm">
                  {record.tags && record.tags.length > 0 ? (
                    <div className="flex flex-wrap gap-1">
                      {record.tags.slice(0, 2).map((tag, i) => (
                        <span key={i} className="badge badge-xs badge-outline">
                          {tag}
                        </span>
                      ))}
                      {record.tags.length > 2 && (
                        <span className="badge badge-xs">
                          +{record.tags.length - 2}
                        </span>
                      )}
                    </div>
                  ) : (
                    <span className="text-base-content/40">-</span>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {!showAll && hasMore && (
        <p className="text-sm text-center text-base-content/60">
          {interpolate(t.translations.SHOWING_RECORDS_RANGE, {
            visible: maxVisible,
            total: records.length,
          })}
        </p>
      )}
    </div>
  );
}
