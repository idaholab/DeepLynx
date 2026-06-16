"use client";

import { useCallback, useEffect, useState } from "react";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import { QueryRecordViewResponseDto } from "@/app/(home)/types/responseDTOs";
import { getRecordsPaginated } from "../lib/client_service/query_services.client";

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
  const [pageSize, setPageSizeState] = useState(initialPageSize);
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

  const setPageSize = useCallback((nextPageSize: number) => {
    setPageSizeState(nextPageSize);
    setCurrentPage(1);
  }, []);

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
