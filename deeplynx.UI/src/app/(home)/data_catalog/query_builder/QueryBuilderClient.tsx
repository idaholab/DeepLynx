"use client";

import { useRouter, usePathname } from "next/navigation";
import { QueryBuilderQuery } from "@/app/(home)/types/types";
import { useProjectSession } from "@/app/contexts/ProjectSessionProvider";
import { translations } from "@/app/lib/translations";
import {
  MagnifyingGlassIcon,
  PlusIcon,
  XMarkIcon,
  FunnelIcon,
  CalendarIcon,
  TagIcon,
  CircleStackIcon,
  Squares2X2Icon,
  TrashIcon,
  ChevronDownIcon,
  BoltIcon,
  BookmarkIcon,
} from "@heroicons/react/24/outline";
import SaveSearchModal from "@/app/(home)/components/SaveSearchModal";
import { useEffect, useMemo, useState, useRef } from "react";
import { DatePicker } from "@/app/(home)/components/DatePicker";
import ProjectDropdown from "@/app/(home)/components/ProjectDropdown";
import {
  ClassResponseDto,
  DataSourceResponseDto,
  QueryRecordViewResponseDto,
  TagResponseDto,
} from "@/app/(home)/types/responseDTOs";
import { getAllClassesOrg } from "@/app/lib/client_service/class_services.client";
import { getAllDataSourcesOrg } from "@/app/lib/client_service/data_source_services.client";
import { getAllTagsOrg } from "@/app/lib/client_service/tag_services.client";
import { fullTextSearch, queryBuilder } from "@/app/lib/client_service/query_services.client";
import {
  getSavedSearchById,
  executeSavedSearch,
  saveSearch,
} from "@/app/lib/client_service/saved_search_services.client";
import RecordSearchList from "@/app/(home)/components/RecordSearchList";
import { useLanguage } from "@/app/contexts/Language";
import SavedSearchesWidget from "@/app/(home)/components/SavedSearchesWidget";

// ============================================================================
// Types & Constants
// ============================================================================

type Props = {
  initialProjects: { id: string; name: string }[];
  initialSelectedProjects: string[];
  initialSearchTerm: string;
  connectors?: string[];
  filters?: { name: string; value: string }[];
  operators?: string[];
  values?: string[];
  organizationId: number;
  /** When provided the component immediately executes this saved search on mount */
  savedSearchId?: number;
};

const FILTER_TYPES = [
  { icon: Squares2X2Icon, label: "Class", value: "class_name", color: "primary" },
  { icon: TagIcon, label: "Tags", value: "tags", color: "success" },
  { icon: CircleStackIcon, label: "Data Source", value: "data_source_name", color: "secondary" },
  { icon: CalendarIcon, label: "Time Range", value: "last_updated_at", color: "warning" },
] as const;

const newId = () => Math.random().toString(36).slice(2, 10);
const emptyRow = (): QueryBuilderQuery => ({
  id: newId(),
  query: {
    connector: "",
    filter: "",
    operator: "",
    value: "",
    jsonKey: "",
    jsonValue: "",
  },
});

// ============================================================================
// Sub-Components
// ============================================================================

interface SearchBarProps {
  searchTerm: string;
  onSearchChange: (value: string) => void;
  onSearch: () => void;
  showFilters: boolean;
  onToggleFilters: () => void;
  activeFilterCount: number;
  onClearAll: () => void;
  canSearch: boolean;
  isLoadingSavedSearch: boolean;
  onSaveSearch: () => void;
}

function SearchBar({
  searchTerm,
  onSearchChange,
  onSearch,
  showFilters,
  onToggleFilters,
  activeFilterCount,
  onClearAll,
  canSearch,
  isLoadingSavedSearch,
  onSaveSearch,
}: SearchBarProps) {
  const { t } = useLanguage();

  return (
    <div className="bg-base-200 rounded-t-lg p-4 border border-b-0 border-base-content/10">
      <div className="relative">
        <MagnifyingGlassIcon className="absolute text-base-content/40 left-4 top-1/2 -translate-y-1/2 w-5 h-5" />
        <input
          type="text"
          value={searchTerm}
          onChange={(e) => onSearchChange(e.target.value)}
          onKeyDown={(e) => e.key === "Enter" && onSearch()}
          placeholder="Search across all records..."
          className="input input-bordered w-full pl-12 pr-4 bg-base-100 text-base-content placeholder:text-base-content/40 focus:outline-primary"
        />
      </div>

      <div className="flex items-center justify-between mt-4">
        <div className="flex items-center gap-3">
          <button
            onClick={onToggleFilters}
            className={`btn btn-sm gap-2 ${
              showFilters
                ? "btn-primary"
                : "btn-ghost border border-base-content/20 hover:border-base-content/40"
            }`}
          >
            <FunnelIcon className="w-4 h-4" />
            {t.translations.ADDITIONAL_FILTERS}
            {activeFilterCount > 0 && (
              <span className="badge badge-neutral badge-sm">{activeFilterCount}</span>
            )}
          </button>

          {activeFilterCount > 0 && (
            <button
              onClick={onClearAll}
              className="text-xs gap-1 flex hover:underline"
            >
              <XMarkIcon className="w-4 h-4" />
              Clear all filters
            </button>
          )}
        </div>

        <div className="flex items-center gap-2">
          <button
            onClick={onSaveSearch}
            disabled={!canSearch || isLoadingSavedSearch}
            className="btn btn-sm btn-ghost border border-base-content/20 hover:border-base-content/40 gap-2"
          >
            <BookmarkIcon className="w-4 h-4" />
            Save Search
          </button>

          <button
            onClick={onSearch}
            disabled={!canSearch || isLoadingSavedSearch}
            className="btn btn-primary btn-sm gap-2"
          >
            {isLoadingSavedSearch ? (
              <span className="loading loading-spinner loading-xs" />
            ) : (
              <BoltIcon className="w-4 h-4" />
            )}
            {t.translations.SEARCH_RECORDS}
          </button>
        </div>
      </div>
    </div>
  );
}

// ---- FilterRow --------------------------------------------------------------

interface FilterRowProps {
  row: QueryBuilderQuery;
  index: number;
  showConnector: boolean;
  connectors: string[];
  filters: { name: string; value: string }[];
  operators: string[];
  classes: ClassResponseDto[];
  datasources: DataSourceResponseDto[];
  tags: TagResponseDto[];
  isLoadingClasses: boolean;
  isLoadingDataSources: boolean;
  isLoadingTags: boolean;
  onUpdate: (id: string, patch: Partial<QueryBuilderQuery>) => void;
  onRemove: (id: string) => void;
  onFieldChange: (field: string) => void;
  onSearch: () => void;
}

function FilterRow({
  row,
  index,
  showConnector,
  connectors,
  filters,
  operators,
  classes,
  datasources,
  tags,
  isLoadingClasses,
  isLoadingDataSources,
  isLoadingTags,
  onUpdate,
  onRemove,
  onFieldChange,
  onSearch,
}: FilterRowProps) {
  const getFilterIcon = (field: string) => {
    const type = FILTER_TYPES.find((t) => t.value === field);
    return type ? type.icon : FunnelIcon;
  };

  const getFilterColor = (field: string) => {
    const type = FILTER_TYPES.find((t) => t.value === field);
    return type ? type.color : "base-content";
  };

  const getFilteredOperators = () => {
    if (row.query.filter === "properties") return ["KEY_VALUE"];
    if (row.query.filter === "last_updated_at") return ["<", ">", "="];
    if (
      ["class_name", "original_id", "data_source_name", "tags"].includes(
        row.query.filter
      )
    ) {
      return operators.filter(
        (op) => op !== "<" && op !== ">" && op !== "KEY_VALUE"
      );
    }
    return operators;
  };

  const Icon = getFilterIcon(row.query.filter);
  const color = getFilterColor(row.query.filter);
  const { t } = useLanguage();

  const handleEnterSearch = (
      e: React.KeyboardEvent<HTMLInputElement | HTMLSelectElement>
  ) => {
    if (e.key === "Enter" && !e.nativeEvent.isComposing) {
      e.preventDefault();
      onSearch();
    }
  };

  return (
    <div className="card bg-base-100 border border-base-content/10 hover:border-primary/40 transition-colors shadow-sm">
      <div className="card-body p-4">
        <div className="flex items-start gap-3">
          {showConnector && (
            <div className="pt-1">
              <select
                className="select select-sm select-bordered bg-base-100 text-base-content font-semibold"
                value={row.query.connector ?? ""}
                onChange={(e) =>
                  onUpdate(row.id, {
                    query: { ...row.query, connector: e.target.value },
                  })
                }
              >
                <option value="" disabled>
                  {t.translations.CONNECTOR}
                </option>
                {connectors.map((opt) => (
                  <option key={opt} value={opt}>
                    {opt}
                  </option>
                ))}
              </select>
            </div>
          )}

          <div className="flex-1 grid grid-cols-12 gap-3">
            <div className="col-span-4">
              <div className="relative">
                {row.query.filter && (
                  <div className="absolute left-3 top-1/2 -translate-y-1/2 p-1 rounded opacity-70">
                    <Icon className={`w-3 h-3 text-${color}`} />
                  </div>
                )}
                <select
                  className="select select-sm select-bordered w-full pl-10 appearance-none bg-base-100 text-base-content"
                  value={row.query.filter}
                  onChange={(e) => {
                    onUpdate(row.id, {
                      query: { ...row.query, filter: e.target.value },
                    });
                    onFieldChange(e.target.value);
                  }}
                  onKeyDown={handleEnterSearch}
                >
                  <option value="" disabled>
                    {t.translations.FILTER}
                  </option>
                  {filters.map((opt) => (
                    <option key={opt.name} value={opt.value}>
                      {opt.name}
                    </option>
                  ))}
                </select>
              </div>
            </div>

            <div className="col-span-3">
              <select
                className="select select-sm select-bordered w-full bg-base-100 text-base-content"
                value={row.query.operator}
                onChange={(e) =>
                  onUpdate(row.id, {
                    query: { ...row.query, operator: e.target.value },
                  })
                }
                onKeyDown={handleEnterSearch}
              >
                <option value="" disabled>
                  {t.translations.OPERATOR}
                </option>
                {getFilteredOperators().map((opt) => (
                  <option key={opt} value={opt}>
                    {opt}
                  </option>
                ))}
              </select>
            </div>

            <ValueInput
              row={row}
              classes={classes}
              datasources={datasources}
              tags={tags}
              isLoadingClasses={isLoadingClasses}
              isLoadingDataSources={isLoadingDataSources}
              isLoadingTags={isLoadingTags}
              onUpdate={onUpdate}
              onSearch={onSearch}
            />
          </div>

          {index > 0 && (
            <button
              onClick={() => onRemove(row.id)}
              className="btn btn-ghost btn-sm text-error hover:bg-error/10"
            >
              <TrashIcon className="w-4 h-4" />
            </button>
          )}
        </div>
      </div>
    </div>
  );
}

// ---- ValueInput -------------------------------------------------------------

interface ValueInputProps {
  row: QueryBuilderQuery;
  classes: ClassResponseDto[];
  datasources: DataSourceResponseDto[];
  tags: TagResponseDto[];
  isLoadingClasses: boolean;
  isLoadingDataSources: boolean;
  isLoadingTags: boolean;
  onUpdate: (id: string, patch: Partial<QueryBuilderQuery>) => void;
  onSearch: () => void;
}

function ValueInput({
  row,
  classes,
  datasources,
  tags,
  isLoadingClasses,
  isLoadingDataSources,
  isLoadingTags,
  onUpdate,
  onSearch,
}: ValueInputProps) {
  const baseInputClass =
    "input input-sm input-bordered bg-base-100 text-base-content placeholder:text-base-content/40";
  const { t } = useLanguage();

  const handleEnterSearch = (
      e: React.KeyboardEvent<HTMLInputElement | HTMLSelectElement>
  ) => {
    if (e.key === "Enter" && !e.nativeEvent.isComposing) {
      e.preventDefault();
      onSearch();
    }
  };
  
  if (row.query.filter === "last_updated_at") {
    return (
      <div className="col-span-5">
        <DatePicker
          row={row}
          onChange={(dateTime: string) =>
            onUpdate(row.id, { query: { ...row.query, value: dateTime } })
          }
          onKeyDown={handleEnterSearch}
        />
      </div>
    );
  }

  if (row.query.filter === "properties") {
    return (
      <div className="col-span-5 grid grid-cols-2 gap-2">
        <input
          type="text"
          placeholder="Key"
          value={row.query.jsonKey ?? ""}
          onChange={(e) =>
            onUpdate(row.id, { query: { ...row.query, jsonKey: e.target.value } })
          }
          onKeyDown={handleEnterSearch}
          className={`${baseInputClass} w-full`}
        />
        <input
          type="text"
          placeholder="Value"
          value={row.query.jsonValue ?? ""}
          onChange={(e) =>
            onUpdate(row.id, {
              query: { ...row.query, jsonValue: e.target.value },
            })
          }
          onKeyDown={handleEnterSearch}
          className={`${baseInputClass} w-full`}
        />
      </div>
    );
  }

  if (row.query.filter === "original_id") {
    return (
      <div className="col-span-5">
        <input
          type="text"
          placeholder={t.translations.VALUE}
          value={row.query.value ?? ""}
          onChange={(e) =>
            onUpdate(row.id, { query: { ...row.query, value: e.target.value } })
          }
          onKeyDown={handleEnterSearch}
          className={`${baseInputClass} w-full`}
        />
      </div>
    );
  }

  if (["class_name", "data_source_name", "tags"].includes(row.query.filter)) {
    if (row.query.operator === "LIKE") {
      return (
        <div className="col-span-5">
          <input
            type="text"
            placeholder={t.translations.VALUE}
            value={row.query.value ?? ""}
            onChange={(e) =>
              onUpdate(row.id, {
                query: { ...row.query, value: e.target.value },
              })
            }
            onKeyDown={handleEnterSearch}
            className={`${baseInputClass} w-full`}
          />
        </div>
      );
    } else {
      return (
        <div className="col-span-5">
          <select
            className="select select-sm select-bordered w-full bg-base-100 text-base-content"
            value={row.query.value}
            onChange={(e) =>
              onUpdate(row.id, {
                query: { ...row.query, value: e.target.value },
              })
            }
            onKeyDown={handleEnterSearch}
            disabled={
              (row.query.filter === "class_name" && isLoadingClasses) ||
              (row.query.filter === "data_source_name" && isLoadingDataSources) ||
              (row.query.filter === "tags" && isLoadingTags)
            }
          >
            <option value="" disabled>
              {t.translations.VALUE}
            </option>
            {row.query.filter === "class_name" ? (
              classes.length ? (
                classes.map((opt) => (
                  <option key={opt.id} value={opt.name}>
                    {opt.name}
                  </option>
                ))
              ) : (
                <option disabled value="">
                  {isLoadingClasses ? "Loading classes..." : "No classes found"}
                </option>
              )
            ) : row.query.filter === "data_source_name" ? (
              datasources.length ? (
                datasources.map((opt) => (
                  <option key={opt.id} value={opt.name}>
                    {opt.name}
                  </option>
                ))
              ) : (
                <option disabled value="">
                  {isLoadingDataSources
                    ? "Loading datasources..."
                    : "No datasources found"}
                </option>
              )
            ) : row.query.filter === "tags" ? (
              tags.length ? (
                tags.map((opt) => (
                  <option key={opt.id} value={opt.name}>
                    {opt.name}
                  </option>
                ))
              ) : (
                <option disabled value="">
                  {isLoadingTags ? "Loading tags..." : "No tags found"}
                </option>
              )
            ) : null}
          </select>
        </div>
      );
    }
  }

  return (
    <div className="col-span-5">
      <input
        type="text"
        placeholder={t.translations.VALUE}
        value={row.query.value ?? ""}
        onChange={(e) =>
          onUpdate(row.id, { query: { ...row.query, value: e.target.value } })
        }
        onKeyDown={handleEnterSearch}
        className={`${baseInputClass} w-full`}
      />
    </div>
  );
}

// ---- EmptyResultsState ------------------------------------------------------

function EmptyResultsState() {
  const { t } = useLanguage();

  return (
    <div className="card bg-base-200 border border-base-content/10">
      <div className="card-body">
        <div className="text-center py-16">
          <div className="w-16 h-16 bg-base-300 rounded-full flex items-center justify-center mx-auto mb-4">
            <CircleStackIcon className="w-8 h-8 text-base-content/40" />
          </div>
          <h4 className="text-lg font-semibold text-base-content mb-2">
            {t.translations.NO_RECORDS}
          </h4>
          <p className="text-sm text-base-content/50 max-w-md mx-auto">
            Try adjusting your search terms or filters
          </p>
        </div>
      </div>
    </div>
  );
}

// ============================================================================
// Custom Hooks
// ============================================================================

function useFilterData(
  organizationId: number,
  selectedProjects: string[],
  hasLoaded: boolean,
  currentProjectId: string
) {
  const [classes, setClasses] = useState<ClassResponseDto[]>([]);
  const [datasources, setDataSources] = useState<DataSourceResponseDto[]>([]);
  const [tags, setTags] = useState<TagResponseDto[]>([]);
  const [isLoadingClasses, setIsLoadingClasses] = useState(false);
  const [isLoadingDataSources, setIsLoadingDataSources] = useState(false);
  const [isLoadingTags, setIsLoadingTags] = useState(false);

  useEffect(() => {
    if (!hasLoaded || !currentProjectId) return;

    const projects = selectedProjects.map(Number);

    const loadClasses = async () => {
      try {
        setIsLoadingClasses(true);
        const data = await getAllClassesOrg(organizationId, projects);
        setClasses(data);
      } catch (error) {
        console.error("Failed to fetch classes:", error);
        setClasses([]);
      } finally {
        setIsLoadingClasses(false);
      }
    };

    const loadDataSources = async () => {
      try {
        setIsLoadingDataSources(true);
        const data = await getAllDataSourcesOrg(organizationId, projects);
        setDataSources(data);
      } catch (error) {
        console.error("Failed to fetch datasources:", error);
        setDataSources([]);
      } finally {
        setIsLoadingDataSources(false);
      }
    };

    const loadTags = async () => {
      try {
        setIsLoadingTags(true);
        const data = await getAllTagsOrg(organizationId, projects);
        setTags(data);
      } catch (error) {
        console.error("Failed to fetch tags:", error);
        setTags([]);
      } finally {
        setIsLoadingTags(false);
      }
    };

    loadClasses();
    loadDataSources();
    loadTags();
  }, [hasLoaded, currentProjectId, selectedProjects, organizationId]);

  return {
    classes,
    datasources,
    tags,
    isLoadingClasses,
    isLoadingDataSources,
    isLoadingTags,
    setClasses,
    setDataSources,
    setTags,
  };
}

// ============================================================================
// Main Component
// ============================================================================

export default function QueryBuilderClient({
  initialProjects,
  initialSelectedProjects,
  initialSearchTerm,
  connectors = ["AND", "OR"],
  filters = [
    { name: "Class", value: "class_name" },
    { name: "Tag", value: "tags" },
    { name: "Original Data ID", value: "original_id" },
    { name: "Time Range", value: "last_updated_at" },
    { name: "Data Source", value: "data_source_name" },
    { name: "Properties", value: "properties" },
  ],
  operators = ["=", "<", ">", "LIKE", "KEY_VALUE"],
  values = [],
  organizationId,
  savedSearchId,
}: Props) {
  const locale = "en";
  const t = translations[locale].translations;

  // ---- State ----------------------------------------------------------------
  const [projects] = useState(initialProjects);
  const [selectedProjects, setSelectedProjects] = useState<string[]>(initialSelectedProjects);
  const [records, setQueriedRecords] = useState<QueryRecordViewResponseDto[] | null>();
  const [searchTerm, setSearchTerm] = useState(initialSearchTerm ?? "");
  const [showFilters, setShowFilters] = useState(true);
  const [rows, setRows] = useState<QueryBuilderQuery[]>([emptyRow()]);
  const [isLoadingSavedSearch, setIsLoadingSavedSearch] = useState(false);
  const [savedSearchesOpen, setSavedSearchesOpen] = useState(false);

  // Save-search modal state
  const [saveModalOpen, setSaveModalOpen] = useState(false);
  const [saveAlias, setSaveAlias] = useState("");
  const [isSaving, setIsSaving] = useState(false);
  const [savedSearchesKey, setSavedSearchesKey] = useState(0);

  const router = useRouter();
  const pathname = usePathname();
  const hasCleanedParams = useRef(false);

  const { project, hasLoaded } = useProjectSession();

  // ---- Computed values ------------------------------------------------------
  const currentProjectId = useMemo<string>(() => {
    const firstProjectId = projects.length > 0 ? String(projects[0].id) : "";
    if (
      selectedProjects.length === 0 ||
      selectedProjects.includes("ALL") ||
      selectedProjects.length === projects.length
    ) {
      return firstProjectId;
    }
    return String(selectedProjects[0]);
  }, [projects, selectedProjects]);

  const activeFilterCount = useMemo(
    () => rows.filter((r) => r.query.filter !== "").length,
    [rows]
  );

  // ---- Filter data hook -----------------------------------------------------
  const {
    classes,
    datasources,
    tags,
    isLoadingClasses,
    isLoadingDataSources,
    isLoadingTags,
    setClasses,
    setDataSources,
    setTags,
  } = useFilterData(organizationId, selectedProjects, hasLoaded, currentProjectId);

  useEffect(() => {
    if (hasCleanedParams.current) return;
    if (!savedSearchId) return;
    if (isLoadingSavedSearch) return;

    hasCleanedParams.current = true;
    router.replace(pathname, { scroll: false });
  }, [isLoadingSavedSearch, savedSearchId, pathname, router]);

  // ---- Execute saved search on mount ----------------------------------------
  useEffect(() => {
    if (!savedSearchId || !hasLoaded) return;

    const run = async () => {
      try {
        setIsLoadingSavedSearch(true);

        // Use initialSelectedProjects directly — it's the stable prop value
        // from the URL params, not the potentially-not-yet-updated state
        const projectIds =
          initialSelectedProjects.length === 0 || initialSelectedProjects.includes("ALL")
            ? projects.map((p) => Number(p.id))
            : initialSelectedProjects.map(Number);

        const savedSearch = await getSavedSearchById(savedSearchId);

        if (savedSearch.query?.textSearch) {
          setSearchTerm(savedSearch.query.textSearch);
        }

        if (savedSearch.query?.filter && savedSearch.query.filter.length > 0) {
          const populatedRows: QueryBuilderQuery[] = savedSearch.query.filter.map(
            (condition) => {
              let jsonKey = "";
              let jsonValue = "";
              if (condition.filter === "properties" && condition.json) {
                const separatorIndex = condition.json.indexOf("::");
                if (separatorIndex !== -1) {
                  jsonKey = condition.json.slice(0, separatorIndex);
                  jsonValue = condition.json.slice(separatorIndex + 2);
                } else {
                  jsonKey = condition.json;
                }
              }
              return {
                id: newId(),
                query: {
                  connector: condition.connector ?? "",
                  filter: condition.filter ?? "",
                  operator: condition.operator ?? "",
                  value: condition.value ?? "",
                  jsonKey,
                  jsonValue,
                },
              };
            }
          );
          setRows(populatedRows);
          setShowFilters(true);
        }

        const data = await executeSavedSearch(savedSearchId, organizationId, projectIds);
        setQueriedRecords(data);
      } catch (error) {
        console.error("Failed to execute saved search:", error);
      } finally {
        setIsLoadingSavedSearch(false);
      }
    };

    run();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [savedSearchId, hasLoaded]);

  // ---- Row management -------------------------------------------------------
  const addRow = () => setRows((r) => [...r, emptyRow()]);
  const removeRow = (id: string) =>
    setRows((r) => (r.length > 1 ? r.filter((x) => x.id !== id) : r));
  const updateRow = (id: string, patch: Partial<QueryBuilderQuery>) =>
    setRows((r) => r.map((row) => (row.id === id ? { ...row, ...patch } : row)));

  const reset = () => {
    setRows([emptyRow()]);
    setQueriedRecords(null);
    setSearchTerm("");
  };

  const hasValidQueries = (): boolean =>
    rows.map((r) => r.query).some(
      (q) =>
        q.filter !== "" ||
        q.operator !== "" ||
        q.value !== "" ||
        q.jsonKey !== "" ||
        q.jsonValue !== ""
    );

  // ---- Handlers -------------------------------------------------------------
  const handleSubmit = async () => {
  try {
    const queryDtos = rows.map((r) => r.query);
    
    const projectIds =
      selectedProjects.length === 0 ||
      selectedProjects.includes("ALL") ||
      selectedProjects.length === projects.length
        ? projects.map((p) => Number(p.id))
        : selectedProjects.map(Number);

    if (hasValidQueries()) {
      const data = await queryBuilder(organizationId, queryDtos, projectIds, searchTerm);
      if (data) setQueriedRecords(data);
    } else {
      const data = await fullTextSearch(organizationId, searchTerm, projectIds);
      if (data) setQueriedRecords(data);
    }
    } catch (error) {
      console.error("Failed to send query", error);
    }
  };

  const handleFieldChange = async (field: string) => {
    const projectIds = selectedProjects.map(Number);
    if (field === "class_name") {
      try {
        setClasses(await getAllClassesOrg(organizationId, projectIds));
      } catch (err) {
        console.error("Failed to fetch classes:", err);
      }
    } else if (field === "data_source_name") {
      try {
        setDataSources(await getAllDataSourcesOrg(organizationId, projectIds));
      } catch (err) {
        console.error("Failed to fetch datasources:", err);
      }
    } else if (field === "tags") {
      try {
        setTags(await getAllTagsOrg(organizationId, projectIds));
      } catch (err) {
        console.error("Failed to fetch tags:", err);
      }
    }
  };

  const handleSaveSearch = async () => {
  if (!saveAlias.trim()) return;
  try {
    setIsSaving(true);
    const queryDtos = rows
      .filter((r) => r.query.filter !== "")
      .map((r) => r.query);
    await saveSearch(queryDtos, searchTerm || undefined, saveAlias.trim());
    setSaveModalOpen(false);
    setSaveAlias("");
    setSavedSearchesKey((k) => k + 1); // 👈 add this
  } catch (error) {
    console.error("Failed to save search:", error);
  } finally {
    setIsSaving(false);
  }
};

  // ---- Render ---------------------------------------------------------------
  return (
    <main className="min-h-screen bg-base-200/30 text-base-content">
      {/* Header */}
      <section className="border-b border-base-300 bg-base-100">
        <div className="mx-auto flex w-full max-w-7xl flex-col gap-5 px-3 py-5 sm:px-6 lg:px-8">
          <div className="space-y-3">
            <div>
              <p className="text-xs font-semibold uppercase tracking-wide text-base-content/60">
                {t.DATA_CATALOG}
              </p>
              <h1 className="text-2xl font-bold text-base-content sm:text-3xl">
                {t.SEARCH_RECORDS}
              </h1>
            </div>
            <ProjectDropdown
              projects={projects}
              onSelectionChange={(newProjects) => {
                setSelectedProjects(newProjects);
                if (typeof window !== "undefined" && window.location.search) {
                  router.replace(pathname, { scroll: false });
                }
              }}
              defaultSelected={
                initialSelectedProjects.length ? initialSelectedProjects : undefined
              }
            />
          </div>
        </div>
      </section>

      {/* Main Content */}
      <section className="mx-auto w-full max-w-7xl px-3 py-5 sm:px-6 lg:px-8">
        <div>

          {/* Saved Searches Collapsible Bar */}
          <div className="mb-2">
            <button
              onClick={() => setSavedSearchesOpen((o) => !o)}
              className="w-full flex items-center justify-between px-6 p-3 text-sm font-medium rounded-lg bg-base-200 border border-base-content/10 text-base-content/70 hover:text-base-content hover:border-base-content/20 transition-all group"
            >
              <div className="flex items-center gap-2">
                <BookmarkIcon className="w-4 h-4" />
                <span className="font-semibold tracking-wide text-xs uppercase">
                  Saved Searches
                </span>
              </div>
              <div className="flex items-center gap-2 text-base-content/40 text-xs">
                <span>{savedSearchesOpen ? "Hide" : "Show"}</span>
                <ChevronDownIcon
                  className={`w-4 h-4 transition-transform duration-200 ${
                    savedSearchesOpen ? "rotate-180" : ""
                  }`}
                />
              </div>
            </button>

            <div
              className={`transition-all duration-300 ease-in-out overflow-hidden ${
                savedSearchesOpen ? "max-h-[800px] opacity-100" : "max-h-0 opacity-0"
              }`}
            >
              <div className="px-6 py-4 border border-t-0 border-base-content/10 rounded-b-lg bg-base-100 h-full">
                <SavedSearchesWidget key={savedSearchesKey} scope="catalog" projects={[]} />
              </div>
            </div>
          </div>

          {/* Saved-search loading banner */}
          {isLoadingSavedSearch && (
            <div className="flex items-center justify-center gap-3 py-3 rounded-lg bg-primary/10 text-primary text-sm font-medium mb-4">
              <span className="loading loading-spinner loading-xs" />
              Running saved search…
            </div>
          )}

          {/* Search + Filters */}
          <div className="mb-6">
            <SearchBar
              searchTerm={searchTerm}
              onSearchChange={setSearchTerm}
              onSearch={handleSubmit}
              showFilters={showFilters}
              onToggleFilters={() => setShowFilters(!showFilters)}
              activeFilterCount={activeFilterCount}
              onClearAll={reset}
              canSearch={!!searchTerm || hasValidQueries()}
              isLoadingSavedSearch={isLoadingSavedSearch}
              onSaveSearch={() => setSaveModalOpen(true)}
            />

            {showFilters && (
              <div className="rounded-b-lg border border-t-0 border-base-content/10 bg-base-200 p-6 mt-0">
                <div className="mb-4">
                  <h3 className="text-sm font-bold uppercase tracking-wider text-base-content mb-1">
                    {t.SELECT_FILTERS}
                  </h3>
                  <p className="text-xs text-base-content/50">
                    Build complex queries by combining multiple conditions
                  </p>
                </div>

                <div className="space-y-3">
                  {rows.map((row, idx) => (
                    <FilterRow
                      key={row.id}
                      row={row}
                      index={idx}
                      showConnector={idx > 0}
                      connectors={connectors}
                      filters={filters}
                      operators={operators}
                      classes={classes}
                      datasources={datasources}
                      tags={tags}
                      isLoadingClasses={isLoadingClasses}
                      isLoadingDataSources={isLoadingDataSources}
                      isLoadingTags={isLoadingTags}
                      onUpdate={updateRow}
                      onRemove={removeRow}
                      onFieldChange={handleFieldChange}
                      onSearch={handleSubmit}
                    />
                  ))}
                </div>

                <div className="flex items-center gap-3 mt-4 pt-3 border-t border-base-content/10">
                  <button
                    onClick={addRow}
                    className="btn btn-sm btn-ghost border border-base-content/20 hover:border-base-content/40 gap-2"
                  >
                    <PlusIcon className="w-4 h-4" />
                    {t.FILTER}
                  </button>
                </div>
              </div>
            )}
          </div>

          {/* Results */}
          {records && records.length > 0 ? (
            <RecordSearchList data={records} />
          ) : (
            records && <EmptyResultsState />
          )}
        </div>
      </section>

      {/* Save Search Modal */}
      <SaveSearchModal
        isOpen={saveModalOpen}
        isSaving={isSaving}
        alias={saveAlias}
        onAliasChange={setSaveAlias}
        onSave={handleSaveSearch}
        onClose={() => {
          setSaveModalOpen(false);
          setSaveAlias("");
        }}
      />
    </main>
  );
}
