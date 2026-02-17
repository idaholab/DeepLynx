"use client";
import {
  QueryBuilderQuery,
} from "@/app/(home)/types/types";
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
  BoltIcon
} from "@heroicons/react/24/outline";
import { useEffect, useMemo, useState } from "react";
import { DatePicker } from "@/app/(home)/components/DatePicker";
import ProjectDropdown from "@/app/(home)/components/ProjectDropdown";
import {
  ClassResponseDto,
  DataSourceResponseDto,
  HistoricalRecordResponseDto,
  TagResponseDto,
} from "@/app/(home)/types/responseDTOs";
import { getAllClassesOrg } from "@/app/lib/client_service/class_services.client";
import { getAllDataSourcesOrg } from "@/app/lib/client_service/data_source_services.client";
import { getAllTagsOrg } from "@/app/lib/client_service/tag_services.client";
import { fullTextSearch, queryBuilder } from "@/app/lib/client_service/query_services.client";
import RecordSearchList from "@/app/(home)/components/RecordSearchList";
import { useLanguage } from "@/app/contexts/Language";

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
};

const FILTER_TYPES = [
  { icon: Squares2X2Icon, label: 'Class', value: 'class_name', color: 'primary' },
  { icon: TagIcon, label: 'Tags', value: 'tags', color: 'success' },
  { icon: CircleStackIcon, label: 'Data Source', value: 'data_source_name', color: 'secondary' },
  { icon: CalendarIcon, label: 'Time Range', value: 'last_updated_at', color: 'warning' },
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
}: SearchBarProps) {
  const { t } = useLanguage();
  return (
    <div className="p-6 bg-base-200 rounded-lg">
      <div className="relative">
        <MagnifyingGlassIcon className="absolute left-4 top-1/2 -translate-y-1/2 w-5 h-5 text-base-content/40" />
        <input
          type="text"
          value={searchTerm}
          onChange={(e) => onSearchChange(e.target.value)}
          onKeyDown={(e) => e.key === 'Enter' && onSearch()}
          placeholder="Search across all records..."
          className="input input-bordered w-full pl-12 pr-4"
        />
      </div>

      <div className="flex items-center justify-between mt-4">
        <div className="flex items-center gap-3">
          <button
            onClick={onToggleFilters}
            className={`btn btn-sm gap-2 ${showFilters ? 'btn-primary' : 'btn-ghost'
              }`}
          >
            <FunnelIcon className="w-4 h-4" />
            {t.translations.ADDITIONAL_FILTERS}
            {activeFilterCount > 0 && (
              <span className="badge badge-sm">
                {activeFilterCount}
              </span>
            )}
          </button>

          {activeFilterCount > 0 && (
            <button
              onClick={onClearAll}
              className="btn btn-sm btn-ghost btn-error gap-2"
            >
              <XMarkIcon className="w-4 h-4" />
              {t.translations.DELETE_ALL_FILTERS}
            </button>
          )}
        </div>

        <button
          onClick={onSearch}
          disabled={!canSearch}
          className="btn btn-primary btn-sm gap-2"
        >
          <BoltIcon className="w-4 h-4" />
          {t.translations.SEARCH_RECORDS}
        </button>
      </div>
    </div>
  );
}

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
}: FilterRowProps) {
  const getFilterIcon = (field: string) => {
    const type = FILTER_TYPES.find(t => t.value === field);
    return type ? type.icon : FunnelIcon;
  };

  const getFilterColor = (field: string) => {
    const type = FILTER_TYPES.find(t => t.value === field);
    return type ? type.color : 'base-content';
  };

  const getFilteredOperators = () => {
    if (row.query.filter === "properties") return ["KEY_VALUE"];
    if (row.query.filter === "last_updated_at") return ["<", ">", "="];
    if (["class_name", "original_id", "data_source_name", "tags"].includes(row.query.filter)) {
      return operators.filter(op => op !== "<" && op !== ">" && op !== "KEY_VALUE");
    }
    return operators;
  };

  const Icon = getFilterIcon(row.query.filter);
  const color = getFilterColor(row.query.filter);
  const { t } = useLanguage();

  return (
    <div className="card bg-base-200 border border-base-300 hover:border-primary/50 transition-colors">
      <div className="card-body p-4">
        <div className="flex items-start gap-3">
          {/* Connector */}
          {showConnector && (
            <div className="pt-1">
              <select
                className="select select-sm select-bordered font-semibold"
                value={row.query.connector ?? ""}
                onChange={(e) =>
                  onUpdate(row.id, {
                    query: { ...row.query, connector: e.target.value },
                  })
                }
              >
                <option value="" disabled>{t.translations.CONNECTOR}</option>
                {connectors.map((opt) => (
                  <option key={opt} value={opt}>{opt}</option>
                ))}
              </select>
            </div>
          )}

          {/* Main Filter Row */}
          <div className="flex-1 grid grid-cols-12 gap-3">
            {/* Field Selector */}
            <div className="col-span-4">
              <div className="relative">
                {row.query.filter && (
                  <div className={`absolute left-3 top-1/2 -translate-y-1/2 p-1 bg-${color}/10 rounded`}>
                    <Icon className={`w-3 h-3 text-${color}`} />
                  </div>
                )}
                <select
                  className="select select-sm select-bordered w-full pl-10 appearance-none"
                  value={row.query.filter}
                  onChange={(e) => {
                    onUpdate(row.id, {
                      query: { ...row.query, filter: e.target.value },
                    });
                    onFieldChange(e.target.value);
                  }}
                >
                  <option value="" disabled>{t.translations.FILTER}</option>
                  {filters.map((opt) => (
                    <option key={opt.name} value={opt.value}>{opt.name}</option>
                  ))}
                </select>
                <ChevronDownIcon className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-base-content/40 pointer-events-none" />
              </div>
            </div>

            {/* Operator */}
            <div className="col-span-3">
              <select
                className="select select-sm select-bordered w-full"
                value={row.query.operator}
                onChange={(e) =>
                  onUpdate(row.id, {
                    query: { ...row.query, operator: e.target.value },
                  })
                }
              >
                <option value="" disabled>{t.translations.OPERATOR}</option>
                {getFilteredOperators().map((opt) => (
                  <option key={opt} value={opt}>{opt}</option>
                ))}
              </select>
            </div>

            {/* Value Input */}
            <ValueInput
              row={row}
              classes={classes}
              datasources={datasources}
              tags={tags}
              isLoadingClasses={isLoadingClasses}
              isLoadingDataSources={isLoadingDataSources}
              isLoadingTags={isLoadingTags}
              onUpdate={onUpdate}
            />
          </div>

          {/* Delete Button */}
          {index > 0 && (
            <button
              onClick={() => onRemove(row.id)}
              className="btn btn-ghost btn-sm btn-error"
            >
              <TrashIcon className="w-4 h-4" />
            </button>
          )}
        </div>
      </div>
    </div>
  );
}

interface ValueInputProps {
  row: QueryBuilderQuery;
  classes: ClassResponseDto[];
  datasources: DataSourceResponseDto[];
  tags: TagResponseDto[];
  isLoadingClasses: boolean;
  isLoadingDataSources: boolean;
  isLoadingTags: boolean;
  onUpdate: (id: string, patch: Partial<QueryBuilderQuery>) => void;
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
}: ValueInputProps) {
  const baseInputClass = "input input-sm input-bordered";
  const { t } = useLanguage();
  // Time Range - DatePicker
  if (row.query.filter === "last_updated_at") {
    return (
      <div className="col-span-5">
        <DatePicker
          row={row}
          onChange={(dateTime: string) =>
            onUpdate(row.id, {
              query: { ...row.query, value: dateTime },
            })
          }
        />
      </div>
    );
  }

  // Properties - Key/Value inputs
  if (row.query.filter === "properties") {
    return (
      <div className="col-span-5 grid grid-cols-2 gap-2">
        <input
          type="text"
          placeholder="Key"
          value={row.query.jsonKey ?? ""}
          onChange={(e) =>
            onUpdate(row.id, {
              query: { ...row.query, jsonKey: e.target.value },
            })
          }
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
          className={`${baseInputClass} w-full`}
        />
      </div>
    );
  }

  // Original ID - Always text input
  if (row.query.filter === "original_id") {
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
          className={`${baseInputClass} w-full`}
        />
      </div>
    );
  }

  // For class_name, data_source_name, and tags:
  // Use dropdown if operator is '=', text input if operator is 'LIKE'
  if (["class_name", "data_source_name", "tags"].includes(row.query.filter)) {
    // Text input for LIKE operator (free-form search)
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
            className={`${baseInputClass} w-full`}
          />
        </div>
      );
    } else {
      // Dropdown for '=' operator and others
      return (
        <div className="col-span-5">
          <select
            className={`select select-sm select-bordered w-full`}
            value={row.query.value}
            onChange={(e) =>
              onUpdate(row.id, {
                query: { ...row.query, value: e.target.value },
              })
            }
            disabled={
              (row.query.filter === "class_name" && isLoadingClasses) ||
              (row.query.filter === "data_source_name" && isLoadingDataSources) ||
              (row.query.filter === "tags" && isLoadingTags)
            }
          >
            <option value="" disabled>{t.translations.VALUE}</option>
            {row.query.filter === "class_name" ? (
              classes.length ? (
                classes.map((opt) => (
                  <option key={opt.id} value={opt.name}>{opt.name}</option>
                ))
              ) : (
                <option disabled value="">
                  {isLoadingClasses ? "Loading classes..." : "No classes found"}
                </option>
              )
            ) : row.query.filter === "data_source_name" ? (
              datasources.length ? (
                datasources.map((opt) => (
                  <option key={opt.id} value={opt.name}>{opt.name}</option>
                ))
              ) : (
                <option disabled value="">
                  {isLoadingDataSources ? "Loading datasources..." : "No datasources found"}
                </option>
              )
            ) : row.query.filter === "tags" ? (
              tags.length ? (
                tags.map((opt) => (
                  <option key={opt.id} value={opt.name}>{opt.name}</option>
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

  // Default fallback (shouldn't reach here normally)
  return null;
}

function EmptyResultsState({ }) {
  const { t } = useLanguage();

  return (
    <div className="card bg-base-100">
      <div className="card-body">
        <div className="text-center py-16">
          <div className="w-16 h-16 bg-base-200 rounded-full flex items-center justify-center mx-auto mb-4">
            <CircleStackIcon className="w-8 h-8 text-base-content/40" />
          </div>
          <h4 className="text-lg font-semibold mb-2">{t.translations.NO_RECORDS}</h4>
          <p className="text-sm text-base-content/60 max-w-md mx-auto">
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

function useFilterData(organizationId: number, selectedProjects: string[], hasLoaded: boolean, currentProjectId: string) {
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
  organizationId
}: Props) {
  const locale = "en";
  const t = translations[locale].translations;

  // State
  const [projects] = useState(initialProjects);
  const [selectedProjects, setSelectedProjects] = useState<string[]>(initialSelectedProjects);
  const [records, setQueriedRecords] = useState<HistoricalRecordResponseDto[] | null>();
  const [searchTerm, setSearchTerm] = useState(initialSearchTerm ?? "");
  const [showFilters, setShowFilters] = useState(true);
  const [rows, setRows] = useState<QueryBuilderQuery[]>([emptyRow()]);

  const { project, hasLoaded } = useProjectSession();

  // Computed values
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
    () => rows.filter(r => r.query.filter !== "").length,
    [rows]
  );

  // Custom hook for filter data
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

  // Row management
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

  const hasValidQueries = (): boolean => {
    const queryDtos = rows.map((r) => r.query);
    return queryDtos.some((query) => {
      return (
        query.filter !== "" ||
        query.operator !== "" ||
        query.value !== "" ||
        query.jsonKey !== "" ||
        query.jsonValue !== ""
      );
    });
  };

  const handleSubmit = async () => {
    try {
      const queryDtos = rows.map((r) => r.query);
      const projects = selectedProjects.map(Number);

      if (hasValidQueries()) {
        const data = await queryBuilder(organizationId, queryDtos, projects, searchTerm);
        if (data) setQueriedRecords(data);
      } else {
        const data = await fullTextSearch(organizationId, searchTerm, projects);
        if (data) setQueriedRecords(data);
      }
    } catch (error) {
      console.error("Failed to send query", error);
    }
  };

  const handleFieldChange = async (field: string) => {
    const projects = selectedProjects.map(Number);

    if (field === "class_name") {
      try {
        const data = await getAllClassesOrg(organizationId, projects);
        setClasses(data);
      } catch (err) {
        console.error("Failed to fetch classes:", err);
      }
    } else if (field === "data_source_name") {
      try {
        const data = await getAllDataSourcesOrg(organizationId, projects);
        setDataSources(data);
      } catch (err) {
        console.error("Failed to fetch datasources:", err);
      }
    } else if (field === "tags") {
      try {
        const data = await getAllTagsOrg(organizationId, projects);
        setTags(data);
      } catch (err) {
        console.error("Failed to fetch tags:", err);
      }
    }
  };

  return (
    <div className="min-h-screen bg-base-100">
      {/* Header */}
      <div className="bg-base-200 px-12 py-6">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-2xl font-bold mb-2">
              {t.DATA_CATALOG}
            </h1>
            <ProjectDropdown
              projects={projects}
              onSelectionChange={setSelectedProjects}
              defaultSelected={
                initialSelectedProjects.length
                  ? initialSelectedProjects
                  : undefined
              }
            />
          </div>
        </div>
      </div>

      {/* Main Content */}
      <div className="px-8 py-8">
        <div className="max-w-7xl mx-auto">
          {/* Search Section */}
          <div className="card bg-base-100 rounded-lg mb-6">
            <SearchBar
              searchTerm={searchTerm}
              onSearchChange={setSearchTerm}
              onSearch={handleSubmit}
              showFilters={showFilters}
              onToggleFilters={() => setShowFilters(!showFilters)}
              activeFilterCount={activeFilterCount}
              onClearAll={reset}
              canSearch={!!searchTerm || hasValidQueries()}
            />

            {/* Filters Section */}
            {showFilters && (
              <div className="rounded-lg shadow-xl mt-2 bg-base-200 p-6">
                <div className="mb-4">
                  <h3 className="text-sm font-bold uppercase tracking-wider mb-1">
                    {t.SELECT_FILTERS}
                  </h3>
                  <p className="text-xs text-base-content/60">Build complex queries by combining multiple conditions</p>
                </div>

                <div className="space-y-3">
                  {rows.map((row, idx) => (
                    <FilterRow
                      key={row.id}
                      row={row}
                      index={idx}
                      showConnector={idx > 0 || !!searchTerm.trim()}
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
                    />
                  ))}
                </div>

                <div className="flex items-center gap-3 mt-4">
                  <button
                    onClick={addRow}
                    className="btn btn-sm btn-ghost gap-2"
                  >
                    <PlusIcon className="w-4 h-4" />
                    {t.FILTER}
                  </button>
                </div>
              </div>
            )}
          </div>

          {/* Results Section */}
          {records && records.length > 0 ? (
            <RecordSearchList data={records} />
          ) : (
            records && <EmptyResultsState />
          )}
        </div>
      </div>
    </div>
  );
}
