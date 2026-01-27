"use client";

import { BulkRecord } from "../../types/bulk_upload_types";
import { useState } from "react";

interface RecordPreviewTableProps {
  records: BulkRecord[];
  maxVisible?: number;
}

export default function RecordPreviewTable({
  records,
  maxVisible = 10,
}: RecordPreviewTableProps) {
  const [showAll, setShowAll] = useState(false);
  const displayedRecords = showAll ? records : records.slice(0, maxVisible);
  const hasMore = records.length > maxVisible;

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between">
        <h4 className="font-semibold text-base-content">
          Record Preview ({records.length} total)
        </h4>
        {hasMore && (
          <button
            onClick={() => setShowAll(!showAll)}
            className="btn btn-ghost btn-xs"
            type="button"
          >
            {showAll ? "Show Less" : `Show All (${records.length})`}
          </button>
        )}
      </div>

      <div className="overflow-x-auto max-h-96 border rounded-lg">
        <table className="table table-zebra table-sm">
          <thead className="sticky top-0 bg-base-200 z-10">
            <tr>
              <th className="w-12">#</th>
              <th>Name</th>
              <th>Description</th>
              <th>Original ID</th>
              <th>Class</th>
              <th>Object Storage</th>
              <th>Tags</th>
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
                      {record.class_name || `ID: ${record.class_id}`}
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
          Showing {maxVisible} of {records.length} records
        </p>
      )}
    </div>
  );
}
