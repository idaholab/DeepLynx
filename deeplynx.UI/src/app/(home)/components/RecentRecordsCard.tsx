"use client";
import { useLanguage } from "@/app/contexts/Language";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import { useRouter } from "next/navigation";
import React, { useEffect, useState, useCallback, useMemo } from "react";
import CatalogViewSkeleton from "./skeletons/catalogviewskeleton";
import { HistoricalRecordResponseDto } from "../types/responseDTOs";
import { getRecentlyAddedRecords } from "@/app/lib/client_service/query_services.client";
import { formatLocalDateTime } from "@/app/lib/date_time";
import PaginationControls from "./PaginationControls";
import { useLocalPagination } from "@/app/hooks/useLocalPagination";

interface Props {
  selectedProjects: string[];
  border?: boolean;
}

type SortOption = "nameAZ" | "nameZA" | "dateNew" | "dateOld";

const RecentRecordsCard: React.FC<Props> = ({
  selectedProjects,
  border = true,
}) => {
  const { t } = useLanguage();
  const router = useRouter();
  const { organization } = useOrganizationSession();

  // View state
  const [records, setRecords] = useState<HistoricalRecordResponseDto[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [sortOption, setSortOption] = useState<SortOption>("dateNew");
  const failedToLoadRecentRecords =
    t.translations.FAILED_TO_LOAD_RECENT_RECORDS;

  const sortOptions: { value: SortOption; label: string }[] = [
    { value: "nameAZ", label: t.translations.SORT_NAME_A_TO_Z },
    { value: "nameZA", label: t.translations.SORT_NAME_Z_TO_A },
    { value: "dateNew", label: t.translations.SORT_DATE_NEWEST },
    { value: "dateOld", label: t.translations.SORT_DATE_OLDEST },
  ];

  // Sort the fetched records before handing them to pagination.
  const sorted = useMemo(() => {
    const arr = [...records];
    arr.sort((a, b) => {
      const dateA = new Date(a.lastUpdatedAt).getTime();
      const dateB = new Date(b.lastUpdatedAt).getTime();

      switch (sortOption) {
        case "nameAZ":
          return (a.name ?? "").localeCompare(b.name ?? "", undefined, {
            sensitivity: "base",
          });
        case "nameZA":
          return (b.name ?? "").localeCompare(a.name ?? "", undefined, {
            sensitivity: "base",
          });
        case "dateNew":
          return dateB - dateA;
        case "dateOld":
          return dateA - dateB;
        default:
          return 0;
      }
    });
    return arr;
  }, [records, sortOption]);

  // Local pagination state for the sorted dataset.
  const {
    currentPage,
    pageSize,
    paginatedItems: paginatedRecords,
    resetPagination,
    setCurrentPage,
    setPageSize,
    totalPages,
  } = useLocalPagination({
    items: sorted,
    initialPageSize: 5,
  });

  // Fetch recent records for the selected projects and organization.
  const fetchRecentRecords = useCallback(async () => {
    if (
      !organization?.organizationId ||
      !selectedProjects ||
      selectedProjects.length === 0
    ) {
      setRecords([]);
      resetPagination();
      return;
    }

    setIsLoading(true);
    setError(null);

    try {
      const projectIds = selectedProjects.map((id) => Number(id));
      const data = await getRecentlyAddedRecords(
        organization.organizationId as number,
        projectIds,
      );
      setRecords(Array.isArray(data) ? data : []);
      resetPagination();
    } catch (e) {
      console.error("Failed to fetch recent records:", e);
      setError(failedToLoadRecentRecords);
      setRecords([]);
    } finally {
      setIsLoading(false);
    }
  }, [
    failedToLoadRecentRecords,
    organization?.organizationId,
    resetPagination,
    selectedProjects,
  ]);

  useEffect(() => {
    fetchRecentRecords();
  }, [fetchRecentRecords]);

  // Reset back to the first page whenever the sort order changes.
  useEffect(() => {
    resetPagination();
  }, [resetPagination, sortOption]);

  const handleSortChange = (val: SortOption) => setSortOption(val);
  const handleRecordClick = (record: HistoricalRecordResponseDto) => {
    router.push(`/record?recordId=${record.id}&projectId=${record.projectId}`);
  };

  if (isLoading) return <CatalogViewSkeleton />;

  return (
    <div className={border ? "shadow-md shadow-dynamic-shadow rounded-xl" : ""}>
      {/* Header and sort controls */}
      <div className="flex items-center justify-between p-4">
        <h2 className="text-lg font-semibold text-base-content">
          {t.translations.RECENTLY_ADDED_RECORDS}
        </h2>

        <div className="flex items-center gap-1">
          <div className="px-3 py-2 text-md font-semibold text-base-content/50">
            {t.translations.SORT_BY}
          </div>
          <div className="relative inline-block">
            <select
              value={sortOption}
              onChange={(e) => handleSortChange(e.target.value as SortOption)}
              className="select"
            >
              {sortOptions.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </div>
        </div>
      </div>

      <div className="divider m-0"></div>

      {/* Error state */}
      {error && (
        <div className="p-4 text-error flex items-center justify-between">
          <span>{error}</span>
          <button
            className="btn btn-sm btn-outline"
            onClick={fetchRecentRecords}
          >
            {t.translations.RETRY}
          </button>
        </div>
      )}

      {/* Paginated records list */}
      <ul className="space-y-1 p-2">
        {paginatedRecords.map((record) => (
          <li
            key={record.id}
            className="border-b border-base-content/40 cursor-pointer hover:bg-base-100/40 p-3 -mx-1 transition-colors"
            onClick={() => handleRecordClick(record)}
          >
            <div className="font-medium text-base-content mb-2">
              {record.name}
            </div>

            <div className="text-sm text-base-content/60 flex flex-wrap gap-x-4 gap-y-1">
              <span className="flex items-center gap-1">
                <span>{t.translations.CLASS}: </span>
                <span className="badge badge-sm badge-secondary">
                  {record.className ?? t.translations.UNKNOWN}
                </span>
              </span>

              <span>
                <span className="text-base-content/50">
                  {t.translations.LAST_EDIT}:
                </span>{" "}
                {formatLocalDateTime(record.lastUpdatedAt)}
              </span>

              <span>
                <span className="text-base-content/50">
                  {t.translations.PROJECT}:
                </span>{" "}
                {record.projectName}
              </span>

              <span>
                <span className="text-base-content/50">
                  {t.translations.DATA_SOURCE}:
                </span>{" "}
                {record.dataSourceName}
              </span>
            </div>
          </li>
        ))}
      </ul>

      {/* Empty state */}
      {!error && paginatedRecords.length === 0 && (
        <div className="text-center py-8 text-base-content/60">
          {t.translations.NO_RECENT_RECORDS}
        </div>
      )}

      {/* Shared pagination controls */}
      <PaginationControls
        currentPage={currentPage}
        pageSize={pageSize}
        totalPages={totalPages}
        onPageChange={setCurrentPage}
        onPageSizeChange={setPageSize}
      />
    </div>
  );
};

export default RecentRecordsCard;
