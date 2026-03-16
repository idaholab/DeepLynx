"use client";
import {
  ChevronLeftIcon,
  ChevronRightIcon,
} from "@heroicons/react/24/outline";
import { useLanguage } from "@/app/contexts/Language";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import { useRouter } from "next/navigation";
import React, { useEffect, useState, useCallback, useMemo } from "react";
import CatalogViewSkeleton from "./skeletons/catalogviewskeleton";
import { HistoricalRecordResponseDto } from "../types/responseDTOs";
import { getRecentlyAddedRecords } from "@/app/lib/client_service/query_services.client";
import { formatLocalDateTime } from "@/app/lib/date_time";

interface Props {
  selectedProjects: string[];
  border?: boolean;
}

const PAGE_SIZE_OPTIONS = [5, 10, 25, 50];

const RecentRecordsCard: React.FC<Props> = ({
  selectedProjects,
  border = true,
}) => {
  const { t } = useLanguage();
  const router = useRouter();
  const { organization } = useOrganizationSession();
  const [recordsPerPage, setRecordsPerPage] = useState(5);
  const [records, setRecords] = useState<HistoricalRecordResponseDto[]>([]);
  const [currentPage, setCurrentPage] = useState(1);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const failedToLoadRecentRecords =
    t.translations.FAILED_TO_LOAD_RECENT_RECORDS;

  type SortOption = "nameAZ" | "nameZA" | "dateNew" | "dateOld";
  const [sortOption, setSortOption] = useState<SortOption>("dateNew");

  const fetchRecentRecords = useCallback(async () => {
    if (
      !organization?.organizationId ||
      !selectedProjects ||
      selectedProjects.length === 0
    ) {
      setRecords([]);
      setCurrentPage(1);
      return;
    }
    setIsLoading(true);
    setError(null);
    try {
      // Convert string[] to number[]
      const projectIds = selectedProjects.map((id) => Number(id));
      const data = await getRecentlyAddedRecords(
        organization.organizationId as number,
        projectIds,
      );
      setRecords(Array.isArray(data) ? data : []);
      setCurrentPage(1);
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
    selectedProjects,
  ]);

  useEffect(() => {
    fetchRecentRecords();
  }, [fetchRecentRecords]);

  useEffect(() => {
    setCurrentPage(1);
  }, [sortOption, recordsPerPage]);

  const handleRecordsPerPageChange = (value: number) => {
    setRecordsPerPage(value);
    setCurrentPage(1);
  };

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

  const totalPages = Math.max(1, Math.ceil(sorted.length / recordsPerPage));
  const startIndex = (currentPage - 1) * recordsPerPage;
  const paginatedRecords = sorted.slice(
    startIndex,
    startIndex + recordsPerPage,
  );

  const handleSortChange = (val: SortOption) => setSortOption(val);

  if (isLoading) return <CatalogViewSkeleton />;

  return (
    <div className={border ? "shadow-md shadow-dynamic-shadow rounded-xl" : ""}>
      {/* Header + Sort */}
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
              <option value="nameAZ">{t.translations.SORT_NAME_A_TO_Z}</option>
              <option value="nameZA">{t.translations.SORT_NAME_Z_TO_A}</option>
              <option value="dateNew">{t.translations.SORT_DATE_NEWEST}</option>
              <option value="dateOld">{t.translations.SORT_DATE_OLDEST}</option>
            </select>
          </div>
        </div>
      </div>

      <div className="divider m-0"></div>

      {/* Error */}
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

      {/* List */}
      <ul className="space-y-1 p-2">
        {paginatedRecords.map((record) => (
          <li
            key={record.id}
            className="border-b border-base-content/40 cursor-pointer hover:bg-base-100/40 p-3 -mx-1 transition-colors"
            onClick={() =>
              router.push(
                `/record?recordId=${record.id}&projectId=${record.projectId}`,
              )
            }
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

      {/* Empty */}
      {!error && paginatedRecords.length === 0 && (
        <div className="text-center py-8 text-base-content/60">
          {t.translations.NO_RECENT_RECORDS}
        </div>
      )}

      {/* Pagination */}
      <div className="flex justify-between">
        <div className="flex items-center gap-1">
          <div className="px-3 py-2 text-md font-semibold text-base-content/50">
            {t.translations.SHOW}
          </div>
          <div className="relative inline-block">
            <select
              className="select"
              defaultValue={recordsPerPage}
              onChange={(e) =>
                handleRecordsPerPageChange(Number(e.target.value))
              }
            >
              {PAGE_SIZE_OPTIONS.map((size) => (
                <option key={size} value={size}>
                  {size}
                </option>
              ))}
            </select>
          </div>
        </div>

        <div className="items-center gap-2 p-4 border-base-300/30">
          <button
            className="btn btn-sm btn-ghost hover:bg-base-200"
            disabled={currentPage === 1}
            onClick={() => setCurrentPage((p) => p - 1)}
          >
            <ChevronLeftIcon className="w-5 h-5 text-base-content/70" />
          </button>
          <span className="px-3 text-sm text-base-content/80 font-medium">
            {t.translations.PAGE} {currentPage} {t.translations.OF} {totalPages}
          </span>
          <button
            className="btn btn-sm btn-ghost hover:bg-base-200"
            disabled={currentPage === totalPages}
            onClick={() => setCurrentPage((p) => p + 1)}
          >
            <ChevronRightIcon className="w-5 h-5 text-base-content/70" />
          </button>
        </div>
      </div>
    </div>
  );
};

export default RecentRecordsCard;
