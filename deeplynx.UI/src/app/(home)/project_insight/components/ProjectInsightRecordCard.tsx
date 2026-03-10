"use client";

import Link from "next/link";
import React from "react";
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

  return (
    <article className="card h-full border border-base-300/70 bg-base-100 shadow-sm transition hover:-translate-y-0.5 hover:shadow-md">
      <div className="card-body gap-4 p-5">
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0">
            <div className="flex items-center gap-2">
              <h3 className="truncate text-lg font-semibold text-base-content">
                {record.name}
              </h3>
              {status.state === "embedded" && (
                <span className="badge badge-success badge-outline">
                  {t.translations.PROJECT_INSIGHT_READY_BADGE}
                </span>
              )}
            </div>
            <p className="mt-1 text-sm text-base-content/70">
              ID {record.id}
            </p>
          </div>

          {selectable && onToggle && (
            <input
              type="checkbox"
              className="checkbox checkbox-primary mt-1"
              checked={checked}
              onChange={() => onToggle(record.id)}
            />
          )}
        </div>

        {record.description && (
          <p className="line-clamp-3 text-sm text-base-content/80">
            {record.description}
          </p>
        )}

        <div className="grid gap-3 sm:grid-cols-2">
          {record.className && (
            <div className="rounded-box bg-base-200/80 px-3 py-2">
              <div className="text-xs uppercase tracking-wide text-base-content/60">
                {t.translations.CLASS}
              </div>
              <div className="mt-1 text-sm font-medium text-base-content">
                {record.className}
              </div>
            </div>
          )}

          {record.dataSourceName && (
            <div className="rounded-box bg-base-200/80 px-3 py-2">
              <div className="text-xs uppercase tracking-wide text-base-content/60">
                {t.translations.DATA_SOURCE}
              </div>
              <div className="mt-1 text-sm font-medium text-base-content">
                {record.dataSourceName}
              </div>
            </div>
          )}

          {record.fileType && (
            <div className="rounded-box bg-base-200/80 px-3 py-2">
              <div className="text-xs uppercase tracking-wide text-base-content/60">
                {t.translations.DATA_TYPE}
              </div>
              <div className="mt-1 text-sm font-medium uppercase text-base-content">
                {record.fileType}
              </div>
            </div>
          )}

          {record.lastUpdatedAt && (
            <div className="rounded-box bg-base-200/80 px-3 py-2">
              <div className="text-xs uppercase tracking-wide text-base-content/60">
                {t.translations.LAST_UPDATED}
              </div>
              <div className="mt-1 text-sm font-medium text-base-content">
                {formatLocalDateTime(record.lastUpdatedAt)}
              </div>
            </div>
          )}
        </div>

        {(record.tags.length > 0 || record.labels.length > 0) && (
          <div className="space-y-3">
            {record.tags.length > 0 && (
              <div>
                <div className="mb-2 text-xs uppercase tracking-wide text-base-content/60">
                  {t.translations.TAGS}
                </div>
                <div className="flex flex-wrap gap-2">
                  {record.tags.map((tag) => (
                    <span
                      key={`${record.id}-tag-${tag.id}`}
                      className="badge badge-outline badge-secondary"
                    >
                      {tag.name}
                    </span>
                  ))}
                </div>
              </div>
            )}

            {record.labels.length > 0 && (
              <div>
                <div className="mb-2 text-xs uppercase tracking-wide text-base-content/60">
                  {t.translations.SENSITIVITY_LABELS}
                </div>
                <div className="flex flex-wrap gap-2">
                  {record.labels.map((label) => (
                    <span
                      key={`${record.id}-label-${label.id}`}
                      className="badge badge-outline"
                    >
                      {label.name}
                    </span>
                  ))}
                </div>
              </div>
            )}
          </div>
        )}

        <div className="mt-auto flex items-center justify-between gap-3">
          <p className="text-sm text-base-content/70">
            {helperText ?? " "}
          </p>

          <Link
            href={`/record?recordId=${record.id}&projectId=${projectId}`}
            className="btn btn-sm btn-ghost"
          >
            {t.translations.VISIT}
          </Link>
        </div>
      </div>
    </article>
  );
}
