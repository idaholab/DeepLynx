"use client";

import { XMarkIcon } from "@heroicons/react/24/outline";
import { useLanguage } from "@/app/contexts/Language";

/**
 * Each filter is identified by a stable numeric `id` rather than by its `term`
 * string. This lets users add the same term again after removing it without
 * creating key collisions, and makes removal O(1) via id lookup in the parent.
 */
type Props = {
  activeFilters: Array<{ id: number; term: string }>;
  onRemoveFilter: (id: number) => void;
};

/**
 * Renders the horizontal pill-strip that shows all active search terms and
 * lets users remove them individually.
 *
 * Returns null when there are no active filters so the parent does not need to
 * wrap the component in its own conditional — the empty state simply takes up
 * no space in the layout.
 *
 * Multiple terms are OR-combined by the search layer (any matching term
 * returns the record). The "Matching any term" label makes this behaviour
 * explicit to the user so they are not surprised when a record appears for a
 * term they didn't expect.
 */
export default function ActiveFiltersBar({ activeFilters, onRemoveFilter }: Props) {
  const { t } = useLanguage();

  if (activeFilters.length === 0) return null;

  return (
    <div className="flex flex-wrap items-center gap-2 rounded-box border border-base-300/50 bg-base-100 px-3 py-2 text-sm shadow-sm">
      <span className="font-medium text-base-content/70">
        {t.translations.SEARCH_TERMS}
      </span>

      {activeFilters.map((filter) => (
        <span
          key={filter.id}
          className="badge badge-primary badge-soft gap-1"
        >
          {filter.term}
          <button
            type="button"
            onClick={() => onRemoveFilter(filter.id)}
            aria-label={`Remove ${filter.term}`}
            className="rounded-full hover:bg-primary/15"
          >
            <XMarkIcon className="size-3" />
          </button>
        </span>
      ))}

      {/* Clarifies OR semantics so users understand why records outside their
          expected term can still appear in results. */}
      <span className="text-xs text-base-content/50">
        {t.translations.MATCHING_ANY_TERM}
      </span>
    </div>
  );
}
