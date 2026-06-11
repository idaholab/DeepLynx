"use client";

import { FunnelIcon, XMarkIcon } from "@heroicons/react/24/outline";

import { FacetOption } from "./recordCollections.types";

type Props = {
  selectedSensitivityFilters: string[];
  onToggleSensitivityFilter: (value: string) => void;
  filteredSensitivityFacetOptions: FacetOption[];
  sensitivityFacetQuery: string;
  onSensitivityFacetQueryChange: (value: string) => void;
  selectedTagFilters: string[];
  onToggleTagFilter: (value: string) => void;
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
  return (
    <aside>
      <div className="rounded-box border border-base-300 bg-base-100 shadow-sm">
        <div className="flex items-center justify-between border-b border-base-300 px-4 py-3">
          <div className="flex items-center gap-2 font-semibold">
            <FunnelIcon className="size-4 text-primary" />
            Filter by
          </div>
          {activeFacetCount > 0 ? (
            <button
              type="button"
              className="btn btn-xs btn-ghost"
              onClick={onClearFacetFilters}
            >
              <XMarkIcon className="size-3" />
              Clear
            </button>
          ) : null}
        </div>

        <div className="divide-y divide-base-300">
          <div className="collapse collapse-arrow rounded-none">
            <input type="checkbox" defaultChecked />
            <div className="collapse-title min-h-0 px-4 py-3 text-sm font-semibold">
              Sensitivity Labels
            </div>
            <div className="collapse-content px-4 pb-4">
              <input
                type="search"
                className="input input-sm mb-3 w-full"
                placeholder="Search labels"
                value={sensitivityFacetQuery}
                onChange={(event) =>
                  onSensitivityFacetQueryChange(event.target.value)
                }
              />
              <div className="max-h-64 space-y-2 overflow-auto pr-1">
                {filteredSensitivityFacetOptions.length ? (
                  filteredSensitivityFacetOptions.map((option) => (
                    <label
                      key={option.label}
                      className="flex cursor-pointer items-center justify-between gap-3 text-sm"
                    >
                      <span className="flex min-w-0 items-center gap-2">
                        <input
                          type="checkbox"
                          className="checkbox checkbox-primary checkbox-xs"
                          checked={selectedSensitivityFilters.includes(option.label)}
                          onChange={() => onToggleSensitivityFilter(option.label)}
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
                    No sensitivity labels match.
                  </p>
                )}
              </div>
            </div>
          </div>

          <div className="collapse collapse-arrow rounded-none">
            <input type="checkbox" defaultChecked />
            <div className="collapse-title min-h-0 px-4 py-3 text-sm font-semibold">
              Tags
            </div>
            <div className="collapse-content px-4 pb-4">
              <input
                type="search"
                className="input input-sm mb-3 w-full"
                placeholder="Search tags"
                value={tagFacetQuery}
                onChange={(event) => onTagFacetQueryChange(event.target.value)}
              />
              <div className="max-h-64 space-y-2 overflow-auto pr-1">
                {filteredTagFacetOptions.length ? (
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
                      <span className="text-xs text-base-content/50">
                        {option.count}
                      </span>
                    </label>
                  ))
                ) : (
                  <p className="text-xs text-base-content/50">
                    No tags match.
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
