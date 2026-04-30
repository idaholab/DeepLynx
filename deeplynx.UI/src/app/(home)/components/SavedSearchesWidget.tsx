"use client";

import { useEffect, useRef, useState, useCallback, useMemo } from "react";
import { useRouter } from "next/navigation";
import {
  FunnelIcon,
  MagnifyingGlassIcon,
  PlayIcon,
  ChevronDownIcon,
  XMarkIcon,
} from "@heroicons/react/24/outline";
import { CheckIcon } from "@heroicons/react/20/solid";
import { format, formatDistanceToNow } from "date-fns";

import { useLanguage } from "@/app/contexts/Language";
import { useProjectSession } from "@/app/contexts/ProjectSessionProvider";
import { getSavedSearches } from "@/app/lib/client_service/saved_search_services.client";
import {
  SavedSearchFilterRequest,
  SearchConditionDto,
  ExecuteSavedSearchRequest,
} from "@/app/(home)/types/requestDTOs";
import {
  SavedSearchesResponseDto,
  ProjectResponseDto,
} from "@/app/(home)/types/responseDTOs";
import ProjectDropdown from "@/app/(home)/components/ProjectDropdown";

// ─── Types ────────────────────────────────────────────────────────────────────

type Mode = "project" | "org";

type Props = {
  mode: Mode;
  /** Required in org mode — list of all projects in the org */
  projects?: ProjectResponseDto[];
};

// ─── Helpers ──────────────────────────────────────────────────────────────────

function operatorLabel(op: string): string {
  const map: Record<string, string> = {
    "=": "=",
    "<": "<",
    ">": ">",
    LIKE: "contains",
    KEY_VALUE: "key/value",
  };
  return map[op] ?? op;
}

const EMPTY_FILTERS: SavedSearchFilterRequest = {
  name: "",
  textSearch: "",
  lastUpdatedBefore: undefined,
  lastUpdatedAfter: undefined,
};

// ─── Sub-components ───────────────────────────────────────────────────────────

function ConnectorBadge({ connector }: { connector: string }) {
  const isOr = connector === "OR";
  return (
    <span
      className={`text-[10px] font-bold tracking-wide px-1.5 py-0.5 rounded border ${
        isOr
          ? "bg-warning/10 text-warning border-warning/30"
          : "bg-info/10 text-info border-info/30"
      }`}
    >
      {connector}
    </span>
  );
}

function FilterPill({ filter }: { filter: SearchConditionDto }) {
  const isKV = filter.operator === "KEY_VALUE";

  return (
    <span className="inline-flex items-center gap-1 text-[11px] bg-base-200 text-base-content/80 border border-base-300 px-2 py-0.5 rounded whitespace-nowrap font-medium">
      <span className="text-base-content/60">{filter.filter}</span>
      <span className="text-base-content/40 font-normal">
        {operatorLabel(filter.operator)}
      </span>
      {isKV ? (
        <span className={filter.json ? "" : "italic text-base-content/30"}>
          {filter.json || "null"}
        </span>
      ) : (
        <span className={filter.value ? "" : "italic text-base-content/30"}>
          {filter.value || "null"}
        </span>
      )}
    </span>
  );
}

function QueryBreakdown({ query }: { query: SavedSearchesResponseDto }) {
  console.log("QueryBreakdown full query object:", query);
  console.log("query.query:", query.query);
  console.log("query.query.filter:", query.query?.filter);
  console.log("query.query.textSearch:", query.query?.textSearch);
  return (
    <div className="flex flex-col gap-1.5">
      {query.query.textSearch && (
        <div className="flex items-center gap-1.5">
          <span className="text-[10px] font-bold tracking-wide bg-secondary/10 text-secondary border border-secondary/30 px-1.5 py-0.5 rounded">
            TEXT
          </span>
          <span className="text-[11px] bg-base-200 text-base-content/80 border border-base-300 px-2 py-0.5 rounded italic">
            "{query.query.textSearch}"
          </span>
        </div>
      )}
      {(query.query.filter ?? []).map((f, i) => (
        <div key={i} className="flex items-center gap-1.5 flex-wrap">
          {f.connector ? (
            <ConnectorBadge connector={f.connector} />
          ) : query.query.textSearch ? (
            <ConnectorBadge connector="AND" />
          ) : (
            <span className="w-8" />
          )}
          <FilterPill filter={f} />
        </div>
      ))}
    </div>
  );
}

function FilterPanel({
  filters,
  onChange,
  onClose,
}: {
  filters: SavedSearchFilterRequest;
  onChange: (f: SavedSearchFilterRequest) => void;
  onClose: () => void;
}) {
  const [local, setLocal] = useState<SavedSearchFilterRequest>(filters);
  const set = <K extends keyof SavedSearchFilterRequest>(
    key: K,
    val: SavedSearchFilterRequest[K],
  ) => setLocal((prev) => ({ ...prev, [key]: val }));

  return (
    <div className="absolute top-[calc(100%+6px)] right-0 z-50 bg-base-100 border border-base-300 rounded-xl shadow-xl w-72 p-4">
      <div className="flex justify-between items-center mb-3">
        <span className="text-sm font-semibold text-base-content">
          Filter Searches
        </span>
        <button onClick={onClose} className="btn btn-ghost btn-xs btn-circle">
          <XMarkIcon className="w-3.5 h-3.5" />
        </button>
      </div>

      <div className="flex flex-col gap-3">
        {/* TextSearch */}
        <div>
          <label className="text-[11px] font-semibold text-base-content/50 uppercase tracking-wide block mb-1">
            Text Search Term
          </label>
          <input
            value={local.textSearch ?? ""}
            onChange={(e) => set("textSearch", e.target.value)}
            placeholder="Contains text search..."
            className="input input-sm input-bordered w-full bg-base-200"
          />
        </div>

        {/* Date range */}
        <div>
          <label className="text-[11px] font-semibold text-base-content/50 uppercase tracking-wide block mb-1">
            Last Updated
          </label>
          <div className="flex gap-2 items-center">
            <div className="flex-1">
              <p className="text-[11px] text-base-content/40 mb-1">After</p>
              <input
                type="date"
                value={
                  local.lastUpdatedAfter
                    ? format(new Date(local.lastUpdatedAfter), "yyyy-MM-dd")
                    : ""
                }
                onChange={(e) =>
                  set(
                    "lastUpdatedAfter",
                    e.target.value ? new Date(e.target.value) : undefined,
                  )
                }
                className="input input-xs input-bordered w-full bg-base-200"
              />
            </div>
            <span className="text-base-content/20 mt-4">—</span>
            <div className="flex-1">
              <p className="text-[11px] text-base-content/40 mb-1">Before</p>
              <input
                type="date"
                value={
                  local.lastUpdatedBefore
                    ? format(new Date(local.lastUpdatedBefore), "yyyy-MM-dd")
                    : ""
                }
                onChange={(e) =>
                  set(
                    "lastUpdatedBefore",
                    e.target.value ? new Date(e.target.value) : undefined,
                  )
                }
                className="input input-xs input-bordered w-full bg-base-200"
              />
            </div>
          </div>
        </div>
      </div>

      <div className="flex gap-2 mt-4 pt-3 border-t border-base-300">
        <button
          onClick={() => {
            setLocal(EMPTY_FILTERS);
            onChange(EMPTY_FILTERS);
          }}
          className="btn btn-ghost btn-sm flex-1"
        >
          Clear
        </button>
        <button
          onClick={() => {
            onChange(local);
            onClose();
          }}
          className="btn btn-primary btn-sm flex-1"
        >
          Apply
        </button>
      </div>
    </div>
  );
}

// ─── Active Filter Chip ───────────────────────────────────────────────────────

function FilterChip({
  label,
  onRemove,
}: {
  label: string;
  onRemove: () => void;
}) {
  return (
    <span className="inline-flex items-center gap-1 bg-primary/10 border border-primary/20 text-primary text-[11px] font-medium px-2 py-0.5 rounded-md">
      {label}
      <button
        onClick={onRemove}
        className="hover:text-primary/60 transition-colors"
      >
        <XMarkIcon className="w-3 h-3" />
      </button>
    </span>
  );
}

// ─── Main Widget ──────────────────────────────────────────────────────────────

export default function SavedSearchesWidget({ mode, projects = [] }: Props) {
  const { t } = useLanguage();
  const router = useRouter();
  const { project } = useProjectSession();
  const { projectId } = project ?? {};

  const isOrg = mode === "org";

  // Data state
  const [savedSearches, setSavedSearches] = useState<
    SavedSearchesResponseDto[]
  >([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // UI state
  const [textQuery, setTextQuery] = useState("");
  const [selectedProjectIds, setSelectedProjectIds] = useState<string[]>([]);
  const [expanded, setExpanded] = useState<number | null>(null);
  const [running, setRunning] = useState<number | null>(null);
  const [runDone, setRunDone] = useState<number | null>(null);
  const [filterOpen, setFilterOpen] = useState(false);
  const [appliedFilters, setAppliedFilters] =
    useState<SavedSearchFilterRequest>(EMPTY_FILTERS);

  const filterRef = useRef<HTMLDivElement>(null);

  // Close filter panel on outside click
  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (filterRef.current && !filterRef.current.contains(e.target as Node)) {
        setFilterOpen(false);
      }
    };
    document.addEventListener("mousedown", handler);
    return () => document.removeEventListener("mousedown", handler);
  }, []);

  const fetchSearches = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await getSavedSearches(appliedFilters);
      setSavedSearches(data);
    } catch {
      setError("Failed to load saved searches.");
    } finally {
      setLoading(false);
    }
  }, [appliedFilters]);

  useEffect(() => {
    fetchSearches();
  }, [fetchSearches]);

  // Client-side text filter on top of server filters
  const displayed = savedSearches.filter((s) => {
    const q = textQuery.toLowerCase();
    return (
      !q ||
      s.name.toLowerCase().includes(q) ||
      (s.query.textSearch ?? "").toLowerCase().includes(q)
    );
  });

  // Run a saved search → redirect to data catalog
  const handleRun = (search: SavedSearchesResponseDto, e: React.MouseEvent) => {
    e.stopPropagation();
    setRunning(search.id);

    const params = new URLSearchParams();
    if (isOrg) {
      if (selectedProjectIds.length > 0) {
        selectedProjectIds.forEach((id) => params.append("projectIds", id));
      }
    } else {
      if (projectId) params.set("fromProject", projectId.toString());
    }
    params.set("savedQueryId", search.id.toString());

    setTimeout(() => {
      setRunning(null);
      setRunDone(search.id);
      router.push(`/data_catalog/all_records?${params.toString()}`);
    }, 600);

    setTimeout(() => setRunDone(null), 2500);
  };

  const mappedProjects = useMemo(
  () => projects.map((p) => ({ id: p.id.toString(), name: p.name })),
  [projects],
    );

  // Active filter count for badge
  const activeFilterCount = Object.entries(appliedFilters).filter(
    ([, v]) => v !== "" && v !== undefined && v !== null,
  ).length;

  // ── Render ──────────────────────────────────────────────────────────────────
  return (
    <div className="card bg-base-200/30 border border-base-300/50 shadow-sm">
      <div className="card-body">
        {/* Header */}
        <div className="flex justify-between items-start mb-1">
          <h2 className="text-xl font-semibold text-base-content">
            Saved Queries
          </h2>
          <a
            href="data_catalog/query_builder"
            className="btn btn-secondary btn-sm"
          >
            Visit
          </a>
        </div>

        {/* Toolbar */}
        <div className="flex flex-wrap gap-2 items-center mt-3">
          {/* Search input + filter button joined */}
          <div className="flex flex-1 min-w-36">
            <label className="input input-sm input-bordered flex items-center gap-2 flex-1 bg-base-100">
              <MagnifyingGlassIcon className="w-3.5 h-3.5 text-base-content/40 shrink-0" />
              <input
                type="text"
                value={textQuery}
                onChange={(e) => setTextQuery(e.target.value)}
                placeholder="Find saved queries by name..."
                className="grow bg-transparent outline-none text-sm"
              />
            </label>
            <div ref={filterRef} className="relative ml-1">
              <button
                onClick={() => setFilterOpen((o) => !o)}
                className={`btn btn-sm gap-1.5 h-full ${
                  activeFilterCount > 0
                    ? "btn-primary btn-outline"
                    : "btn-ghost border border-base-300"
                }`}
              >
                <FunnelIcon className=" w-3.5 h-3.5" />
                Filters
                {activeFilterCount > 0 && (
                  <span className="badge badge-primary badge-sm text-[10px] px-1.5">
                    {activeFilterCount}
                  </span>
                )}
              </button>

              {filterOpen && (
                <FilterPanel
                  filters={appliedFilters}
                  onChange={setAppliedFilters}
                  onClose={() => setFilterOpen(false)}
                />
              )}
            </div>
          </div>

          {/* Project selector — org mode only */}
          {isOrg && projects.length > 0 && (
            <ProjectDropdown
                projects={mappedProjects}
                onSelectionChange={setSelectedProjectIds}
                />
          )}
        </div>

        {/* Active filter chips */}
        {activeFilterCount > 0 && (
          <div className="flex flex-wrap gap-1.5 mt-2">
            {appliedFilters.name && (
              <FilterChip
                label={`Name: ${appliedFilters.name}`}
                onRemove={() => setAppliedFilters((f) => ({ ...f, name: "" }))}
              />
            )}
            {appliedFilters.textSearch && (
              <FilterChip
                label={`Text: "${appliedFilters.textSearch}"`}
                onRemove={() =>
                  setAppliedFilters((f) => ({ ...f, textSearch: "" }))
                }
              />
            )}
            {appliedFilters.lastUpdatedAfter && (
              <FilterChip
                label={`After ${format(new Date(appliedFilters.lastUpdatedAfter), "MMM d, yyyy")}`}
                onRemove={() =>
                  setAppliedFilters((f) => ({
                    ...f,
                    lastUpdatedAfter: undefined,
                  }))
                }
              />
            )}
            {appliedFilters.lastUpdatedBefore && (
              <FilterChip
                label={`Before ${format(new Date(appliedFilters.lastUpdatedBefore), "MMM d, yyyy")}`}
                onRemove={() =>
                  setAppliedFilters((f) => ({
                    ...f,
                    lastUpdatedBefore: undefined,
                  }))
                }
              />
            )}
          </div>
        )}

        {/* Content */}
        <div className="mt-3 rounded-lg overflow-hidden border border-base-300/50">
          {/* Loading */}
          {loading && (
            <div className="bg-base-100 p-8 flex justify-center">
              <span className="loading loading-spinner loading-sm text-primary" />
            </div>
          )}

          {/* Error */}
          {!loading && error && (
            <div className="bg-base-100 p-6 text-center">
              <p className="text-sm text-error">{error}</p>
              <button
                onClick={fetchSearches}
                className="btn btn-ghost btn-xs mt-2"
              >
                Retry
              </button>
            </div>
          )}

          {/* Empty */}
          {!loading && !error && displayed.length === 0 && (
            <div className="bg-base-100 p-10 text-center">
              <MagnifyingGlassIcon className="w-8 h-8 text-base-content/20 mx-auto mb-2" />
              <p className="text-sm font-medium text-base-content/50">
                No saved searches found
              </p>
              <p className="text-xs text-base-content/30 mt-1">
                Try adjusting your filters or search term
              </p>
            </div>
          )}

          {/* Results */}
          {!loading &&
            !error &&
            displayed.map((s, idx) => {
              const isExpanded = expanded === s.id;
              const isRunning = running === s.id;
              const isDone = runDone === s.id;
              const isLast = idx === displayed.length - 1;

              return (
                <div
                  key={s.id}
                  onClick={() => setExpanded(isExpanded ? null : s.id)}
                  className={`bg-base-100 cursor-pointer transition-colors hover:bg-base-200/50 ${
                    !isLast ? "border-b border-base-300/50" : ""
                  }`}
                >
                  <div className="flex items-center gap-3 px-4 py-3.5">
                    {/* Icon */}
                    <div className="w-8 h-8 rounded-lg bg-primary/10 flex items-center justify-center shrink-0">
                      <MagnifyingGlassIcon className="w-4 h-4 text-primary" />
                    </div>

                    {/* Name + meta */}
                    <div className="flex-1 min-w-0">
                      <p className="text-sm font-medium text-base-content truncate">
                        {s.name}
                      </p>
                      <div className="flex items-center flex-wrap gap-1.5 mt-0.5">
                        <span className="text-xs text-base-content/40">
                          {s.query.filter?.length ?? 0} filter
                          {(s.query.filter?.length ?? 0) !== 1 ? "s" : ""}
                        </span>
                        {s.query.textSearch && (
                          <>
                            <span className="text-base-content/20">·</span>
                            <span className="text-xs text-base-content/40">
                              text search
                            </span>
                          </>
                        )}
                        <span className="text-base-content/20">·</span>
                        <span className="text-xs text-base-content/40">
                          updated{" "}
                          {formatDistanceToNow(new Date(s.lastUpdatedAt), {
                            addSuffix: true,
                          })}
                        </span>
                      </div>
                    </div>

                    {/* Run button + chevron */}
                    <div className="flex items-center gap-2 shrink-0">
                      <button
                        onClick={(e) => handleRun(s, e)}
                        className={`btn btn-xs gap-1 ${
                          isDone
                            ? "btn-success btn-outline"
                            : isRunning
                              ? "btn-ghost btn-disabled"
                              : "btn-primary"
                        }`}
                      >
                        {isDone ? (
                          <>
                            <CheckIcon className="w-3 h-3" /> Done
                          </>
                        ) : isRunning ? (
                          <>
                            <span className="loading loading-spinner loading-xs" />{" "}
                            Running
                          </>
                        ) : (
                          <>
                            <PlayIcon className="w-3 h-3" /> Run
                          </>
                        )}
                      </button>
                      <ChevronDownIcon
                        className={`w-4 h-4 text-base-content/30 transition-transform ${
                          isExpanded ? "rotate-180" : ""
                        }`}
                      />
                    </div>
                  </div>

                  {/* Expanded query breakdown */}
                  {isExpanded && s && (
                    <div
                      className="px-4 pb-4 pt-1 pl-[3.75rem] border-t border-dashed border-base-300/60"
                      onClick={(e) => e.stopPropagation()}
                    >
                      <p className="text-[11px] font-semibold text-base-content/30 uppercase tracking-wider mb-2">
                        Query Breakdown
                      </p>
                      <QueryBreakdown query={s} />
                    </div>
                  )}
                </div>
              );
            })}
        </div>

        {/* Footer */}
        {!loading && !error && (
          <div className="flex justify-between items-center mt-3">
            <span className="text-xs text-base-content/40">
              Showing {displayed.length} of {savedSearches.length}
            </span>
            <div className="flex items-center gap-2 text-xs text-base-content/40">
              <button className="btn btn-ghost btn-xs">‹</button>
              Page 1 of 1<button className="btn btn-ghost btn-xs">›</button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
