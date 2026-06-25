"use client";

import { useCallback, useEffect, useState } from "react";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import { QueryRecordViewResponseDto } from "@/app/(home)/types/responseDTOs";
import { getRecordsPaginated } from "../lib/client_service/query_services.client";

const RECORDS_PAGE_SIZE_SESSION_KEY = "deeplynx.records.pageSize";

function getStoredPageSize(fallbackPageSize: number) {
  if (typeof window === "undefined") return fallbackPageSize;

  try {
    const storedPageSize = Number(
      window.sessionStorage.getItem(RECORDS_PAGE_SIZE_SESSION_KEY),
    );

    return Number.isFinite(storedPageSize) && storedPageSize > 0
      ? storedPageSize
      : fallbackPageSize;
  } catch {
    return fallbackPageSize;
  }
}

function storePageSize(pageSize: number) {
  if (typeof window === "undefined") return;

  try {
    window.sessionStorage.setItem(
      RECORDS_PAGE_SIZE_SESSION_KEY,
      String(pageSize),
    );
  } catch {
    // Session storage can be unavailable in private/restricted browser modes.
  }
}

/**
 * Handles paginating records details
 * @param selectedProjects - The projects to access records from
 * @param initialSortBy - The initial record sorting strategy
 * @param initialPageSize - The initial number of records to show
 * @returns Paginated records
 */
export function useRecordsPaginated(
  selectedProjects: string[],
  initialSortBy: string = "dateNew",
  initialPageSize: number = 5,
) {
  const { organization } = useOrganizationSession();

  const [records, setRecords] = useState<QueryRecordViewResponseDto[]>([]);
  const [totalRecords, setTotalRecords] = useState(0);

  const [sortBy, setSortBy] = useState(initialSortBy);
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSizeState] = useState(() =>
    getStoredPageSize(initialPageSize),
  );
  const totalPages = Math.max(1, Math.ceil(totalRecords / pageSize));

  const [isLoading, setIsLoading] = useState(false);
  const [requestFailed, setRequestFailed] = useState(false);

  const resetPagination = useCallback(() => {
    setCurrentPage(1);
  }, []);

  // Fetch recent records for the selected projects and organization.
  const fetchRecords = useCallback(async () => {
    if (
      !organization?.organizationId ||
      !selectedProjects ||
      selectedProjects.length === 0
    ) {
      setRecords([]);
      setTotalRecords(0);
      return;
    }

    setIsLoading(true);
    setRequestFailed(false);

    try {
      const projectIds = selectedProjects.map((id) => Number(id));
      const data = await getRecordsPaginated(
        organization.organizationId as number,
        projectIds,
        sortBy,
        currentPage,
        pageSize,
      );
      setRecords(Array.isArray(data.items) ? data.items : []);
      setTotalRecords(data.totalCount);
    } catch (e) {
      console.error("Failed to fetch recent records:", e);
      setRequestFailed(true);
      setRecords([]);
      setTotalRecords(0);
      resetPagination();
    } finally {
      setIsLoading(false);
    }
  }, [
    organization?.organizationId,
    sortBy,
    resetPagination,
    selectedProjects,
    currentPage,
    pageSize,
  ]);

  useEffect(() => {
    fetchRecords();
  }, [fetchRecords]);

  useEffect(() => {
    setCurrentPage((previousPage) => Math.min(previousPage, totalPages));
  }, [totalPages]);

  const setPageSize = useCallback(
    (nextPageSize: number) => {
      const validPageSize =
        Number.isFinite(nextPageSize) && nextPageSize > 0
          ? nextPageSize
          : initialPageSize;

      storePageSize(validPageSize);
      setPageSizeState(validPageSize);
      setCurrentPage(1);
    },
    [initialPageSize],
  );

  return {
    records,
    totalRecords,
    totalPages,
    sortBy,
    setSortBy,
    currentPage,
    setCurrentPage,
    pageSize,
    setPageSize,
    isLoading,
    requestFailed,
    fetchRecords,
  };
}
