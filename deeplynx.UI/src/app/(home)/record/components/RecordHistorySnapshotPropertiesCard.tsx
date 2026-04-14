"use client";

import { HistoricalRecordResponseDto } from "@/app/(home)/types/responseDTOs";
import { useLanguage } from "@/app/contexts/Language";
import { formatRecordHistoryDate } from "./RecordHistoryDate";

interface Props {
  title: string;
  snapshot: HistoricalRecordResponseDto | null;
  placeholder: string;
}

export default function RecordHistorySnapshotPropertiesCard({
  title,
  snapshot,
  placeholder,
}: Props) {
  const { t } = useLanguage();

  return (
    // Snapshot metadata card (selected or comparison side).
    <div className="card bg-base-100 shadow-lg">
      <div className="card-body p-4">
        <h3 className="text-sm font-semibold uppercase tracking-wide opacity-70 mr-4">
          {title}
        </h3>
        {!snapshot ? (
          <p className="text-sm opacity-70">
            {t.translations.RECORD_HISTORY_NO_COMPARISON_VERSION_SELECTED}
          </p>
        ) : (
          <div className="space-y-2 text-sm">
            <div>
              <span className="font-medium">
                {t.translations.RECORD_HISTORY_NAME_LABEL}{" "}
              </span>
              <span>{snapshot.name || placeholder}</span>
            </div>
            <div>
              <span className="font-medium">
                {t.translations.RECORD_HISTORY_UPDATED_LABEL}{" "}
              </span>
              <span>
                {formatRecordHistoryDate(snapshot.lastUpdatedAt, placeholder)}
              </span>
            </div>
            <div className="flex flex-wrap gap-2 pt-1">
              <span className="badge badge-outline">
                {t.translations.RECORD_HISTORY_DATA_SOURCE_LABEL}{" "}
                {snapshot.dataSourceName || placeholder}
              </span>
              <span className="badge badge-outline">
                {t.translations.RECORD_HISTORY_ARCHIVED_LABEL}{" "}
                {snapshot.isArchived ? t.translations.YES : t.translations.NO}
              </span>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
