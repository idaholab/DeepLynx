"use client";

import Link from "next/link";
import React from "react";
import { DocumentTextIcon } from "@heroicons/react/24/outline";
import { useLanguage } from "@/app/contexts/Language";
import { formatLocalDateTime } from "@/app/lib/date_time";
import type {
  ProjectInsightRecord,
  ProjectInsightStatus,
} from "./projectInsight.types";

interface ProjectInsightRecordCardProps {
  record: ProjectInsightRecord;
  status: ProjectInsightStatus;
  projectId: number;
  selectable?: boolean;
  checked?: boolean;
  onToggle?: (recordId: number) => void;
}

export default function ProjectInsightRecordCard({
  record,
  status,
  projectId,
  selectable = false,
  checked = false,
  onToggle,
}: ProjectInsightRecordCardProps) {
  const { t } = useLanguage();
  const statusLabel =
    status.state === "embedded"
      ? t.translations.PROJECT_INSIGHT_READY_BADGE
      : status.state === "checking"
        ? t.translations.PROJECT_INSIGHT_STATUS_CHECKING
        : status.state === "not_embedded"
          ? t.translations.PROJECT_INSIGHT_STATUS_PENDING
          : status.state === "queued"
            ? t.translations.PROJECT_INSIGHT_STATUS_QUEUED
            : status.state === "processing"
              ? t.translations.PROJECT_INSIGHT_STATUS_PROCESSING
              : status.state === "unsupported"
                ? t.translations.PROJECT_INSIGHT_STATUS_UNSUPPORTED
                : t.translations.PROJECT_INSIGHT_STATUS_ERROR;
  const statusBadgeClass =
    status.state === "embedded"
      ? "badge-success"
      : status.state === "queued" || status.state === "processing"
        ? "badge-warning"
        : status.state === "error"
          ? "badge-error"
          : status.state === "not_embedded"
            ? "badge-secondary"
            : "badge-ghost";
  const helperText =
    status.state === "checking"
      ? t.translations.PROJECT_INSIGHT_STATUS_CHECKING
      : status.state === "not_embedded"
        ? t.translations.PROJECT_INSIGHT_STATUS_PENDING
        : status.state === "queued"
          ? t.translations.PROJECT_INSIGHT_STATUS_QUEUED
          : status.state === "processing"
            ? t.translations.PROJECT_INSIGHT_STATUS_PROCESSING
            : status.state === "unsupported"
              ? t.translations.PROJECT_INSIGHT_STATUS_UNSUPPORTED
              : status.state === "error"
                ? status.error?.trim() ||
                  t.translations.PROJECT_INSIGHT_STATUS_ERROR
                : null;
  const metadataItems: Array<{ label: string; value: string }> = [
    record.className
      ? { label: t.translations.CLASS, value: record.className }
      : null,
    record.dataSourceName
      ? { label: t.translations.DATA_SOURCE, value: record.dataSourceName }
      : null,
    record.fileType
      ? {
          label: t.translations.DATA_TYPE,
          value: record.fileType.toUpperCase(),
        }
      : null,
    record.lastUpdatedAt
      ? {
          label: t.translations.LAST_UPDATED,
          value: formatLocalDateTime(record.lastUpdatedAt),
        }
      : null,
  ].flatMap((item) => (item ? [item] : []));

  return (
    <article className="rounded-xl border border-base-300/60 bg-base-100 px-3 py-3 transition hover:border-base-300 hover:bg-base-200/20">
      <div className="flex items-start gap-2">
        <DocumentTextIcon className="size-6 shrink-0 text-slate-500" />

        <div className="min-w-0 flex-1">
          <div className="flex items-start gap-3">
            <div className="min-w-0 flex-1">
              <div className="flex items-start gap-2">
                <div className="min-w-0 flex-1">
                  <h3 className="truncate text-sm font-semibold text-base-content sm:text-base">
                    {record.name}
                  </h3>
                </div>
                <Link
                  href={`/record?recordId=${record.id}&projectId=${projectId}`}
                  className="btn btn-ghost btn-xs shrink-0"
                >
                  {t.translations.VISIT}
                </Link>
              </div>

              <div className="mt-1 flex flex-wrap items-center gap-2">
                <span className="badge badge-outline badge-xs">
                  ID {record.id}
                </span>
                <span className={`badge badge-xs ${statusBadgeClass}`}>
                  {statusLabel}
                </span>
              </div>
            </div>

            <div className="flex shrink-0 items-center gap-2">
              {selectable && onToggle && (
                <input
                  type="checkbox"
                  className="checkbox checkbox-primary checkbox-sm"
                  checked={checked}
                  onChange={() => onToggle(record.id)}
                />
              )}
            </div>
          </div>

          {metadataItems.length > 0 && (
            <div className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-xs text-base-content/65">
              {metadataItems.map((item) => (
                <span key={`${record.id}-${item.label}`}>
                  <span className="font-semibold text-base-content/75">
                    {item.label}:
                  </span>{" "}
                  {item.value}
                </span>
              ))}
            </div>
          )}

          {(record.tags.length > 0 || record.labels.length > 0) && (
            <div className="mt-2 flex flex-wrap gap-1.5">
              {record.tags.map((tag) => (
                <span
                  key={`${record.id}-tag-${tag.id}`}
                  className="badge badge-outline badge-secondary badge-xs"
                >
                  {tag.name}
                </span>
              ))}
              {record.labels.map((label) => (
                <span
                  key={`${record.id}-label-${label.id}`}
                  className="badge badge-outline badge-xs"
                >
                  {label.name}
                </span>
              ))}
            </div>
          )}

          {helperText && (
            <p className="mt-2 text-xs text-base-content/60">{helperText}</p>
          )}
        </div>
      </div>
    </article>
  );
}
