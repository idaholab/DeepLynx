"use client";

import { useLanguage } from "@/app/contexts/Language";
import { FunnelIcon, XMarkIcon } from "@heroicons/react/24/outline";

import { FacetOption } from "./recordCollections.types";

type Props = {
  selectedSensitivityFilters: number[];
  onToggleSensitivityFilter: (value: number) => void;
  filteredSensitivityFacetOptions: FacetOption[];
  sensitivityFacetQuery: string;
  onSensitivityFacetQueryChange: (value: string) => void;
  selectedTagFilters: number[];
  onToggleTagFilter: (value: number) => void;
  filteredTagFacetOptions: FacetOption[];
  tagFacetQuery: string;
  onTagFacetQueryChange: (value: string) => void;
  activeFacetCount: number;
  onClearFacetFilters: () => void;
};

export default function FilterSidebar({
  selectedSensitivityFilters,
  onToggleSensitivityFilter,
  filteredSensitivityFacetOptions,
  sensitivityFacetQuery,
  onSensitivityFacetQueryChange,
  selectedTagFilters,
  onToggleTagFilter,
  filteredTagFacetOptions,
  tagFacetQuery,
  onTagFacetQueryChange,
  activeFacetCount,
  onClearFacetFilters,
}: Props) {
  const { t } = useLanguage();

  return (
    <aside>
      <div className="rounded-box border border-base-300/50 bg-base-100 shadow-sm">
        <div className="flex items-center justify-between border-b border-base-200 px-4 py-3">
          <div className="flex items-center gap-2 font-semibold">
            <FunnelIcon className="size-4 text-primary" />
            {t.translations.FILTER_BY}
          </div>
          {activeFacetCount > 0 ? (
            <button
              type="button"
              className="btn btn-xs btn-ghost"
              onClick={onClearFacetFilters}
            >
              <XMarkIcon className="size-3" />
              {t.translations.CLEAR}
            </button>
          ) : null}
        </div>

        <div className="divide-y divide-base-200">
          <div className="collapse collapse-arrow rounded-none">
            <input type="checkbox" defaultChecked />
            <div className="collapse-title min-h-0 px-4 py-3 text-sm font-semibold">
              {t.translations.SENSITIVITY_LABELS}
            </div>
            <div className="collapse-content px-4 pb-4">
              <input
                type="search"
                className="input input-sm mb-3 w-full"
                placeholder={t.translations.SEARCH_LABELS}
                value={sensitivityFacetQuery}
                onChange={(event) =>
                  onSensitivityFacetQueryChange(event.target.value)
                }
              />
              <div className="max-h-64 space-y-2 overflow-auto pr-1">
                {filteredSensitivityFacetOptions.length ? (
                  filteredSensitivityFacetOptions.map((option) => (
                    <label
                      key={option.id ?? option.label}
                      className="flex cursor-pointer items-center justify-between gap-3 text-sm"
                    >
                      <span className="flex min-w-0 items-center gap-2">
                        <input
                          type="checkbox"
                          className="checkbox checkbox-primary checkbox-xs"
                          checked={
                            option.id !== undefined &&
                            selectedSensitivityFilters.includes(option.id)
                          }
                          onChange={() => {
                            if (option.id !== undefined) {
                              onToggleSensitivityFilter(option.id);
                            }
                          }}
                        />
                        <span className="truncate">{option.label}</span>
                      </span>
                      <span className="text-xs text-base-content/50">
                        {option.count}
                      </span>
                    </label>
                  ))
                ) : (
                  <p className="text-xs text-base-content/50">
                    {t.translations.RECORD_COLLECTIONS_NO_SENSITIVITY_LABELS_MATCH}
                  </p>
                )}
              </div>
            </div>
          </div>

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
                onChange={(event) => onTagFacetQueryChange(event.target.value)}
              />
              <div className="max-h-64 space-y-2 overflow-auto pr-1">
                {filteredTagFacetOptions.length ? (
                  filteredTagFacetOptions.map((option) => (
                    <label
                      key={option.id ?? option.label}
                      className="flex cursor-pointer items-center justify-between gap-3 text-sm"
                    >
                      <span className="flex min-w-0 items-center gap-2">
                        <input
                          type="checkbox"
                          className="checkbox checkbox-primary checkbox-xs"
                          checked={
                            option.id !== undefined &&
                            selectedTagFilters.includes(option.id)
                          }
                          onChange={() => {
                            if (option.id !== undefined) {
                              onToggleTagFilter(option.id);
                            }
                          }}
                        />
                        <span className="truncate">{option.label}</span>
                      </span>
                      <span className="text-xs text-base-content/50">
                        {option.count}
                      </span>
                    </label>
                  ))
                ) : (
                  <p className="text-xs text-base-content/50">
                    {t.translations.NO_TAGS_MATCH_SEARCH}
                  </p>
                )}
              </div>
            </div>
          </div>
        </div>
      </div>
    </aside>
  );
}
