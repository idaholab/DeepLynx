// src/app/(home)/data_catalog/DataCatalogClient.tsx
"use client";

import { RecordTableRow } from "@/app/(home)/types/types";
import Link from "next/link";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
// import { getAllRecordsForMultipleProjects } from "@/app/lib/projects_services.client";
import SearchBar from "@/app/(home)/components/SearchBar";
import { useLanguage } from "@/app/contexts/Language";
import {
  EyeIcon,
  PlusIcon,
  QueueListIcon,
  TableCellsIcon,
} from "@heroicons/react/24/outline";
import AddRecordModal from "../components/AddRecordModal";
import ListView from "../components/ListView";
import ProjectDropdown from "../components/ProjectDropdown";
import RecentRecordsCard from "../components/RecentRecordsCard";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import { fullTextSearch, getMultiProjectRecords } from "@/app/lib/client_service/query_services.client";
import { HistoricalRecordResponseDto, RecordResponseDto } from "../types/responseDTOs";

type Props = {
  initialProjects: { id: string; name: string }[];
  initialSelectedProjects: string[];
  initialSearchTerm: string;
  initialRecords: RecordTableRow[];
};

export default function DataCatalogClient({
  initialProjects,
  initialSelectedProjects,
  initialSearchTerm,
  initialRecords,
}: Props) {
  const { t } = useLanguage();
  const { organization } = useOrganizationSession();

  // Use ref to store initial values to prevent re-renders
  const initialSelectedProjectsRef = useRef(initialSelectedProjects);
  const initialSearchTermRef = useRef(initialSearchTerm);

  // State management
  const [projects] = useState(initialProjects);
  const [selectedProjects, setSelectedProjects] = useState<string[]>(
    initialSelectedProjects
  );
  const [isRecordModalOpen, setIsRecordModalOpen] = useState(false);
  const [isSearching, setIsSearching] = useState(!!initialSearchTerm);

  const [hasInitialSearchRun, setHasInitialSearchRun] = useState(false);

  const [tableData, setTableData] = useState<RecordTableRow[]>(
    initialRecords ?? []
  );
  const [records, setQueriedRecords] = useState<RecordTableRow[]>(
    initialRecords ?? []
  );

  const [searchTerm, setSearchTerm] = useState(initialSearchTerm ?? "");
  const [activeFilters, setActiveFilters] = useState<
    Array<{ id: number; term: string }>
  >([]);
  const [nextFilterId, setNextFilterId] = useState(1);
  const [viewMode, setViewMode] = useState<"list" | "table">("list");
  const [showAll, setShowAll] = useState(
    Boolean(initialSearchTerm) || initialSelectedProjects.length > 0
  );

  // Memoized values
  const selectedProjectsToken = useMemo(
    () => selectedProjects.join("|"),
    [selectedProjects]
  );

  const effectiveProjectIds = useMemo(() => {
    const allIds = projects.map((p) => String(p.id));
    if (
      selectedProjects.length === 0 ||
      selectedProjects.includes("ALL") ||
      selectedProjects.length === projects.length
    ) {
      return allIds;
    }
    return selectedProjects.map(String);
  }, [projects, selectedProjects]);

  // Fetch records for selected projects
  const fetchRecordsForSelection = useCallback(
    async (signal?: AbortSignal) => {
      const idsNum = effectiveProjectIds.map(Number).filter(Number.isFinite);
      if (idsNum.length === 0) {
        setTableData([]);
        return;
      }

      const data = await getMultiProjectRecords(organization?.organizationId as number, idsNum, true);
      const mappedData: RecordTableRow[] = data.map((dto: HistoricalRecordResponseDto) => ({
          ...dto,
          fileType: '',
          timeseries: undefined,
          fileSize: undefined,
          select: false,
          associatedRecords: undefined,
          archivedAt: dto.isArchived ? dto.lastUpdatedAt : null
        }));
      setTableData(mappedData);
      setViewMode("list");
    },
    [effectiveProjectIds]
  );

  // Clear all filters
  const clearAllFilters = useCallback(() => {
    setActiveFilters([]);
    setSearchTerm("");
    setQueriedRecords([]);

    const ctrl = new AbortController();
    fetchRecordsForSelection(ctrl.signal).catch((e: RecordTableRow) => {
      if (e?.name !== "CanceledError" && e?.name !== "AbortError") {
        console.error("Clear all fetch failed:", e);
      }
    });
  }, [fetchRecordsForSelection]);

  // Perform full text search
  const performFullTextSearch = useCallback(
    async (searchValue: string, projectIds: string[]) => {
      try {
        setIsSearching(true);
        const projects = projectIds.map(Number)
        const data = await fullTextSearch(organization?.organizationId as number, searchValue, projects);
        
        if (data) {
          const mappedData: RecordTableRow[] = data.map((dto: HistoricalRecordResponseDto) => ({
          ...dto,
          fileType: '',
          timeseries: undefined,
          fileSize: undefined,
          select: false,
          associatedRecords: undefined,
          archivedAt: dto.isArchived ? dto.lastUpdatedAt : null
        }));
          setQueriedRecords(mappedData);
          setTableData(mappedData);
          setSearchTerm("");
          setViewMode("list");

          // Add to active filters
          const trimmed = searchValue.trim();
          if (trimmed && !activeFilters.some((f) => f.term === trimmed)) {
            setActiveFilters((prev) => [
              ...prev,
              { id: nextFilterId, term: trimmed },
            ]);
            setNextFilterId((n) => n + 1);
          }
        }
      } catch (error) {
        console.error("Failed to perform full text search:", error);
      } finally {
        setIsSearching(false);
      }
    },
    [activeFilters, nextFilterId]
  );

  // Handle search from search bar
  const handleSearch = useCallback(
    async (value: string) => {
      const trimmed = value.trim();
      if (!trimmed || activeFilters.some((f) => f.term === trimmed)) return;

      await performFullTextSearch(trimmed, selectedProjects);
    },
    [activeFilters, selectedProjects, performFullTextSearch]
  );

  // Handle submit from search bar
  const handleSubmit = async () => {
    await performFullTextSearch(searchTerm, selectedProjects);
  };

  // Handle initial search term
  useEffect(() => {
    if (hasInitialSearchRun) return;

    const initialTerm = initialSearchTermRef.current;
    const initialProjects = initialSelectedProjectsRef.current;

    if (initialTerm && initialProjects.length > 0) {
      setHasInitialSearchRun(true);
      performFullTextSearch(initialTerm, initialProjects);
    }
  }, [hasInitialSearchRun, performFullTextSearch]);

  // Fetch records when selection changes (if no active filters)
  useEffect(() => {
    if (activeFilters.length > 0) return;

    const ctrl = new AbortController();
    fetchRecordsForSelection(ctrl.signal).catch((e: RecordTableRow) => {
      if (e?.name !== "CanceledError" && e?.name !== "AbortError") {
        console.error("Fetch on selection change failed:", e);
      }
    });

    return () => ctrl.abort();
  }, [
    activeFilters.length,
    selectedProjectsToken,
    fetchRecordsForSelection,
  ]);

  return (
    <div className="mt-3">
      {/* Header */}
      <div className="flex justify-between items-center bg-base-200/40 pl-12 py-2">
        <div>
          <h1 className="text-2xl font-bold text-info-content">
            {t.translations.DATA_CATALOG}
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

      {/* Search and Actions Bar */}
      <div className="flex justify-between gap-4 mb-4 pt-20 pl-8 w-full box-border">
        <div className="flex flex-col md:w-1/2">
          <SearchBar
            placeholder="Search"
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            onSubmit={handleSubmit}
            activeFilters={activeFilters}
            onRemoveFilter={(id) =>
              setActiveFilters((prev) => prev.filter((f) => f.id !== id))
            }
            onClearAll={clearAllFilters}
            resultCount={tableData.length}
            showResultsMessage={activeFilters.length > 0}
            className="w-full"
          />
        </div>

        <div className="flex gap-4 pr-4">
          <Link
            href="data_catalog/all_records"
            className="btn btn-outline btn-primary text-dynamic-blue border-dynamic-blue hover:text-white hover:bg-dynamic-blue"
            onClick={() => {
              setShowAll(true);
              setViewMode("list");
              clearAllFilters();
            }}
          >
            <EyeIcon className="h-6 w-6" />
            {t.translations.EXPLORE_ALL_RECORDS}
          </Link>

          <button
            onClick={() => setIsRecordModalOpen(true)}
            className="btn btn-primary text-white border-dynamic-blue bg-dynamic-blue"
          >
            <PlusIcon className="size-5" />
            <span>{t.translations.RECORD}</span>
          </button>

          {(activeFilters.length > 0 || showAll) && (
            <div className="flex gap-1">
              <button
                className={`btn btn-sm ${viewMode === "list" ? "btn-primary" : "btn-ghost"
                  }`}
                onClick={() => setViewMode("list")}
              >
                <QueueListIcon className="h-7 w-7" />
              </button>
              <button
                className={`btn btn-sm ${viewMode === "table" ? "btn-primary" : "btn-ghost"
                  }`}
                onClick={() => setViewMode("table")}
              >
                <TableCellsIcon className="h-7 w-7" />
              </button>
            </div>
          )}
        </div>
      </div>

      {/* Main Content */}
      <div className="flex w-full gap-8 p-8 justify-center">
        {isSearching ? (
          <div className="flex flex-col items-center justify-center w-2/3">
            <div className="loading loading-spinner loading-lg"></div>
            <p className="mt-4 text-base-content/70">
              {t.translations.LOADING || "Loading..."}
            </p>
          </div>
        ) : records && records?.length > 0 ? (
          <ListView data={records} />
        ) : (
          records && (
            <div className="w-2/3">
              <RecentRecordsCard selectedProjects={selectedProjects} />
            </div>
          )
        )}
      </div>

      <AddRecordModal
        isOpen={isRecordModalOpen}
        onClose={() => setIsRecordModalOpen(false)}
        initialProjects={projects}
      />
    </div>
  );
}