"use client";

import React from "react";
import { useLanguage } from "@/app/contexts/Language";
import type {
  NamedInsightOption,
  ProjectInsightFiltersState,
} from "./projectInsight.types";

interface ProjectInsightFiltersProps {
  filters: ProjectInsightFiltersState;
  onChange: (patch: Partial<ProjectInsightFiltersState>) => void;
  onClear: () => void;
  classes: NamedInsightOption[];
  tags: NamedInsightOption[];
  totalRecords: number;
  embeddedRecords: number;
  pendingRecords: number;
}

type ChecklistProps = {
  title: string;
  options: NamedInsightOption[];
  selectedIds: number[];
  onToggle: (id: number) => void;
  emptyText: string;
};

function ChecklistFilter({
  title,
  options,
  selectedIds,
  onToggle,
  emptyText,
}: ChecklistProps) {
  return (
    <div className="rounded-box border border-base-300/70 bg-base-100">
      <div className="flex items-center justify-between border-b border-base-300/60 px-4 py-3">
        <h3 className="text-sm font-semibold text-base-content">{title}</h3>
        {selectedIds.length > 0 && (
          <span className="badge badge-outline badge-primary">
            {selectedIds.length}
          </span>
        )}
      </div>

      {options.length === 0 ? (
        <p className="px-4 py-4 text-sm text-base-content/60">{emptyText}</p>
      ) : (
        <div className="max-h-44 space-y-2 overflow-y-auto px-4 py-3">
          {options.map((option) => (
            <label
              key={option.id}
              className="flex cursor-pointer items-start gap-3 rounded-lg px-2 py-2 transition hover:bg-base-200/70"
            >
              <input
                type="checkbox"
                className="checkbox checkbox-sm mt-0.5"
                checked={selectedIds.includes(option.id)}
                onChange={() => onToggle(option.id)}
              />
              <span className="text-sm text-base-content">{option.name}</span>
            </label>
          ))}
        </div>
      )}
    </div>
  );
}

export default function ProjectInsightFilters({
  filters,
  onChange,
  onClear,
  classes,
  tags,
  totalRecords,
  embeddedRecords,
  pendingRecords,
}: ProjectInsightFiltersProps) {
  const { t } = useLanguage();

  const stats = [
    {
      label: t.translations.PROJECT_INSIGHT_FILTERS_RECORDS,
      value: totalRecords,
    },
    {
      label: t.translations.PROJECT_INSIGHT_EMBEDDED_TITLE,
      value: embeddedRecords,
    },
    {
      label: t.translations.PROJECT_INSIGHT_PENDING_TITLE,
      value: pendingRecords,
    },
  ];

  return (
    <section className="card bg-base-100 border border-base-300/60 shadow-lg">
      <div className="card-body gap-4 p-5 lg:p-6">
        <div className="flex items-start justify-between gap-3">
          <div>
            <h2 className="text-xl font-semibold text-base-content">
              {t.translations.PROJECT_INSIGHT_FILTERS}
            </h2>
            <p className="mt-1 text-sm text-base-content/70">
              {t.translations.PROJECT_INSIGHT_FILTERS_DESCRIPTION}
            </p>
          </div>
          <button
            type="button"
            className="btn btn-sm btn-ghost"
            onClick={onClear}
          >
            {t.translations.CLEAR_ALL}
          </button>
        </div>

        <div className="grid gap-3 sm:grid-cols-3">
          {stats.map((stat) => (
            <div
              key={stat.label}
              className="rounded-box border border-base-300/70 bg-base-200/70 px-4 py-3"
            >
              <div className="text-xs uppercase tracking-wide text-base-content/60">
                {stat.label}
              </div>
              <div className="mt-1 text-2xl font-semibold text-base-content">
                {stat.value}
              </div>
            </div>
          ))}
        </div>

        <div className="grid gap-4 xl:grid-cols-2">
          <ChecklistFilter
            title={t.translations.CLASS}
            options={classes}
            selectedIds={filters.classIds}
            onToggle={(id) =>
              onChange({
                classIds: filters.classIds.includes(id)
                  ? filters.classIds.filter((value) => value !== id)
                  : [...filters.classIds, id],
              })
            }
            emptyText={t.translations.PROJECT_INSIGHT_NO_FILTER_OPTIONS}
          />
          <ChecklistFilter
            title={t.translations.TAGS}
            options={tags}
            selectedIds={filters.tagIds}
            onToggle={(id) =>
              onChange({
                tagIds: filters.tagIds.includes(id)
                  ? filters.tagIds.filter((value) => value !== id)
                  : [...filters.tagIds, id],
              })
            }
            emptyText={t.translations.PROJECT_INSIGHT_NO_FILTER_OPTIONS}
          />
        </div>
      </div>
    </section>
  );
}
