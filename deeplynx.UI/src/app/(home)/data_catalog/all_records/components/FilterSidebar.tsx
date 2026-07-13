"use client";

import { FunnelIcon, XMarkIcon } from "@heroicons/react/24/outline";
import { useLanguage } from "@/app/contexts/Language";
import { RecordTableRow } from "@/app/(home)/types/types";

/**
 * Exported so AllRecordsClient can type its own statusFilter state without
 * importing from a separate shared-types file.
 */
export type RecordStatusFilter = "all" | "active" | "archived";

type FacetOption = { label: string; count?: number };

/**
 * Why projectScopedRecords is passed instead of just pre-computed counts:
 * The status section (All / Active / Archived) shows counts derived from the
 * project-scoped list BEFORE facet filters are applied. If we passed in the
 * already-filtered scopedRecords, clicking "Archived" would show "0 active"
 * because active records would already be hidden — making the radio buttons
 * feel broken. Passing the pre-filter list keeps counts stable as the user
 * toggles filters.
 *
 * Facet options (class & tag) are pre-computed and pre-sliced by the parent
 * because the parent controls FACET_LIMIT and the search query state.
 */
type Props = {
  projectScopedRecords?: RecordTableRow[];
  statusFilter: RecordStatusFilter;
  onStatusFilterChange: (value: RecordStatusFilter) => void;
  selectedClassFilters: string[];
  onToggleClassFilter: (value: string) => void;
  filteredClassFacetOptions: FacetOption[];
  classFacetQuery: string;
  onClassFacetQueryChange: (value: string) => void;
  selectedTagFilters: string[];
  onToggleTagFilter: (value: string) => void;
  filteredTagFacetOptions: FacetOption[];
  tagFacetQuery: string;
  onTagFacetQueryChange: (value: string) => void;
  /** Total number of active facet selections across all filter groups. */
  activeFacetCount: number;
  onClearFacetFilters: () => void;
};

/**
 * Left-hand filter sidebar for the All Records catalog page.
 *
 * Contains three collapsible filter groups:
 *  - Status   — single-select radio (All / Active / Archived)
 *  - Class    — multi-select checkboxes with a search input, capped at FACET_LIMIT entries
 *  - Tags     — multi-select checkboxes with a search input, capped at FACET_LIMIT entries
 *
 * All filter state is lifted to AllRecordsClient so that clearing or changing
 * the project dropdown can reset filters without needing cross-component refs.
 *
 * The "Clear" button only renders when at least one facet filter is active so
 * it doesn't distract users who haven't filtered anything yet.
 *
 * The sidebar is sticky on large screens (lg:sticky lg:top-4) so it remains
 * visible while the user scrolls through the record list.
 */
export default function FilterSidebar({
  projectScopedRecords,
  statusFilter,
  onStatusFilterChange,
  selectedClassFilters,
  onToggleClassFilter,
  filteredClassFacetOptions,
  classFacetQuery,
  onClassFacetQueryChange,
  selectedTagFilters,
  onToggleTagFilter,
  filteredTagFacetOptions,
  tagFacetQuery,
  onTagFacetQueryChange,
  activeFacetCount,
  onClearFacetFilters,
}: Props) {
  const { t } = useLanguage();

  const statusOptions = [
    {
      label: t.translations.ALL,
      value: "all" as const,
      count: projectScopedRecords?.length,
    },
    {
      label: t.translations.ACTIVE,
      value: "active" as const,
      count: projectScopedRecords?.filter((r) => !r.isArchived).length,
    },
    {
      label: t.translations.ARCHIVED_BADGE,
      value: "archived" as const,
      count: projectScopedRecords?.filter((r) => r.isArchived).length,
    },
  ];

  return (
    <aside>
      <div className="rounded-box border border-base-300/50 bg-base-100 shadow-sm">
        <div className="flex items-center justify-between border-b border-base-200 px-4 py-3">
          <div className="flex items-center gap-2 font-semibold">
            <FunnelIcon className="size-4 text-primary" />
            {t.translations.FILTER_BY}
          </div>
          {activeFacetCount > 0 && (
            <button
              type="button"
              className="btn btn-xs btn-ghost"
              onClick={onClearFacetFilters}
            >
              <XMarkIcon className="size-3" />
              {t.translations.CLEAR}
            </button>
          )}
        </div>

        <div className="divide-y divide-base-200">
          {/* ── Class ──────────────────────────────────────────────────────── */}
          {/*
           * Multi-select checkboxes so users can view records from several
           * classes at once (e.g. "Sensor" + "Asset"). The search input
           * filters the displayed options client-side; it does NOT trigger a
           * new API call. Options are capped at FACET_LIMIT by the parent.
           */}
          <div className="collapse collapse-arrow rounded-none">
            <input type="checkbox" defaultChecked />
            <div className="collapse-title min-h-0 px-4 py-3 text-sm font-semibold">
              {t.translations.CLASS}
            </div>
            <div className="collapse-content px-4 pb-4">
              <input
                type="search"
                className="input input-sm mb-3 w-full"
                placeholder={t.translations.SEARCH_CLASSES}
                value={classFacetQuery}
                onChange={(e) => onClassFacetQueryChange(e.target.value)}
              />
              <div className="max-h-64 space-y-2 overflow-auto pr-1">
                {filteredClassFacetOptions.map((option) => (
                  <label
                    key={option.label}
                    className="flex cursor-pointer items-center justify-between gap-3 text-sm"
                  >
                    <span className="flex min-w-0 items-center gap-2">
                      <input
                        type="checkbox"
                        className="checkbox checkbox-primary checkbox-xs"
                        checked={selectedClassFilters.includes(option.label)}
                        onChange={() => onToggleClassFilter(option.label)}
                      />
                      <span className="truncate">{option.label}</span>
                    </span>
                    {option.count !== undefined && (
                      <span className="text-xs text-base-content/50">
                        {option.count}
                      </span>
                    )}
                  </label>
                ))}
              </div>
            </div>
          </div>

          {/* ── Tags ───────────────────────────────────────────────────────── */}
          {/*
           * Tag filtering uses OR semantics within the tag group — a record
           * matches if it has ANY of the selected tags, not ALL of them.
           * This matches user expectation when browsing by category.
           */}
          <div className="collapse collapse-arrow rounded-none">
            <input type="checkbox" defaultChecked />
            <div className="collapse-title min-h-0 px-4 py-3 text-sm font-semibold">
              {t.translations.TAGS}
            </div>
            <div className="collapse-content px-4 pb-4">
              <input
                type="search"
                className="input input-sm mb-3 w-full"
                placeholder={t.translations.SEARCH_TAGS}
                value={tagFacetQuery}
                onChange={(e) => onTagFacetQueryChange(e.target.value)}
              />
              <div className="max-h-64 space-y-2 overflow-auto pr-1">
                {filteredTagFacetOptions.length === 0 ? (
                  <p className="text-xs text-base-content/50">
                    {t.translations.NO_TAGS_MATCH_SEARCH}
                  </p>
                ) : (
                  filteredTagFacetOptions.map((option) => (
                    <label
                      key={option.label}
                      className="flex cursor-pointer items-center justify-between gap-3 text-sm"
                    >
                      <span className="flex min-w-0 items-center gap-2">
                        <input
                          type="checkbox"
                          className="checkbox checkbox-primary checkbox-xs"
                          checked={selectedTagFilters.includes(option.label)}
                          onChange={() => onToggleTagFilter(option.label)}
                        />
                        <span className="truncate">{option.label}</span>
                      </span>
                      {option.count !== undefined && (
                        <span className="text-xs text-base-content/50">
                          {option.count}
                        </span>
                      )}
                    </label>
                  ))
                )}
              </div>
            </div>
          </div>
        </div>
      </div>
    </aside>
  );
}
