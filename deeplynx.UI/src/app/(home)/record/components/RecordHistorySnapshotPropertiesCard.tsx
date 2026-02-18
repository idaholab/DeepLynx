"use client";

import { HistoricalRecordResponseDto } from "@/app/(home)/types/responseDTOs";
import { formatRecordHistoryDate } from "./RecordHistoryDate";

interface Props {
  title: string;
  snapshot: HistoricalRecordResponseDto | null;
  placeholder: string;
  labels: {
    noSelection: string;
    name: string;
    updated: string;
    dataSource: string;
    archived: string;
    yes: string;
    no: string;
  };
}

export default function RecordHistorySnapshotPropertiesCard({
  title,
  snapshot,
  placeholder,
  labels,
}: Props) {
  return (
    <div className="card bg-base-100 shadow-lg">
      <div className="card-body p-4">
        <h3 className="text-sm font-semibold uppercase tracking-wide opacity-70 mr-4">
          {title}
        </h3>
        {!snapshot ? (
          <p className="text-sm opacity-70">{labels.noSelection}</p>
        ) : (
          <div className="space-y-2 text-sm">
            <div>
              <span className="font-medium">{labels.name} </span>
              <span>{snapshot.name || placeholder}</span>
            </div>
            <div>
              <span className="font-medium">{labels.updated} </span>
              <span>
                {formatRecordHistoryDate(snapshot.lastUpdatedAt, placeholder)}
              </span>
            </div>
            <div className="flex flex-wrap gap-2 pt-1">
              <span className="badge badge-outline">
                {labels.dataSource} {snapshot.dataSourceName || placeholder}
              </span>
              <span className="badge badge-outline">
                {labels.archived} {snapshot.isArchived ? labels.yes : labels.no}
              </span>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
