"use client";
import {
  BarsArrowDownIcon,
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

interface Props {
  selectedProjects: string[];
  border?: boolean;
}

const RECORDS_PER_PAGE = 5;

const RecentRecordsCard: React.FC<Props> = ({
  selectedProjects,
  border = true,
}) => {
  const { t } = useLanguage();
  const router = useRouter();
  const { organization } = useOrganizationSession();

  const [records, setRecords] = useState<HistoricalRecordResponseDto[]>([]);
  const [currentPage, setCurrentPage] = useState(1);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

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
      setError("Failed to load recent records.");
      setRecords([]);
    } finally {
      setIsLoading(false);
    }
  }, [organization?.organizationId, selectedProjects]);
  console.log("Records: ", records);
  useEffect(() => {
    fetchRecentRecords();
  }, [fetchRecentRecords]);

  useEffect(() => {
    setCurrentPage(1);
  }, [sortOption]);

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

  const totalPages = Math.max(1, Math.ceil(sorted.length / RECORDS_PER_PAGE));
  const startIndex = (currentPage - 1) * RECORDS_PER_PAGE;
  const paginatedRecords = sorted.slice(
    startIndex,
    startIndex + RECORDS_PER_PAGE,
  );

  const handleSortChange = (val: SortOption) => setSortOption(val);

  const toUtcIsoIfNaive = (input: string) => {
    if (/([zZ]|[+-]\d{2}:\d{2})$/.test(input)) return input;
    return `${input}Z`;
  };

  const formatDate = (dateString: string) => {
    const normalized = toUtcIsoIfNaive(dateString);
    const date = new Date(normalized);

    return date.toLocaleString(undefined, {
      month: "long",
      day: "numeric",
      year: "numeric",
      hour: "numeric",
      minute: "numeric",
      hour12: true,
      timeZoneName: "short",
    });
  };

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
            Sort By
          </div>
          <div className="relative inline-block">
            <BarsArrowDownIcon
              className={`pointer-events-none absolute right-3 top-1/2 -translate-y-1/2 h-5 w-5 transition-colors
                          text-[var(--color-gray-400)]`}
            />
            <select
              value={sortOption}
              onChange={(e) => handleSortChange(e.target.value as SortOption)}
              className={`appearance-none border-2 border-gray-400 square-lg pl-3 pr-9 py-2 text-sm 
                          bg-base-100 font-semibold text-base-content/50 cursor-pointer
                          hover:bg-[var(--color-dynamic-blue)] hover:text-[var(--color-base-content)] focus:ring-2 focus:ring-[var(--color-secondary)]
                          transition-all duration-200 w-44`}
            >
              <option value="nameAZ">Name: A to Z</option>
              <option value="nameZA">Name: Z to A</option>
              <option value="dateNew">Date: Newest</option>
              <option value="dateOld">Date: Oldest</option>
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
            Retry
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
                  {record.className ?? "Unknown"}
                </span>
              </span>

              <span>
                <span className="text-base-content/50">
                  {t.translations.LAST_EDIT}:
                </span>{" "}
                {formatDate(record.lastUpdatedAt)}
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
          {t.translations.NO_RECENT_RECORDS || "No recent records found"}
        </div>
      )}

      {/* Pagination */}
      {totalPages > 1 && (
        <div className="flex justify-end items-center gap-2 p-4 border-base-300/30">
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
      )}
    </div>
  );
};

export default RecentRecordsCard;
