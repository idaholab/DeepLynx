"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { getAllRecords } from "@/app/lib/client_service/record_services.client";
import { fullTextSearchPaginated } from "@/app/lib/client_service/query_services.client";
import type { ExistingFile } from "../../types/types";

function mapRecordToExistingFile(record: {
  id?: number | string | null;
  name?: string | null;
  description?: string | null;
  lastUpdatedAt?: string | null;
  lastUpdatedBy?: string | number | null;
  dataSourceName?: string | null;
}): ExistingFile | null {
  if (record.id == null) return null;
  return {
    id: String(record.id),
    name: record.name?.trim() || String(record.id),
    description: record.description ?? undefined,
    lastUpdate: record.lastUpdatedAt ?? undefined,
    updatedBy:
      record.lastUpdatedBy != null ? String(record.lastUpdatedBy) : undefined,
    dataSource: record.dataSourceName ?? undefined,
  };
}

function dedupeExistingFiles(files: ExistingFile[]): ExistingFile[] {
  const map = new Map<string, ExistingFile>();
  for (const file of files) {
    map.set(String(file.id), file);
  }
  return Array.from(map.values());
}

/**
 * Record picker for the "Update Existing Record" flow.
 * Browsing (no search query) is paginated on the backend, since the
 * unfiltered list can be huge. A search re-runs the (comparatively slow)
 * full-text query once per query change and paginates the returned set
 * on the client, so paging through search results doesn't re-run the search.
 */
export function useRecordPicker(
  organizationId: number | undefined,
  projectId: number | undefined,
  enabled: boolean,
  initialPageSize: number = 10,
) {
  const [browseRecords, setBrowseRecords] = useState<ExistingFile[]>([]);
  const [browseTotalCount, setBrowseTotalCount] = useState(0);
  const [searchResults, setSearchResults] = useState<ExistingFile[]>([]);
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSizeState] = useState(initialPageSize);
  const [searchQuery, setSearchQueryState] = useState("");
  const [isLoading, setIsLoading] = useState(false);
  const [requestFailed, setRequestFailed] = useState(false);

  const isSearching = searchQuery.trim().length > 0;
  const totalCount = isSearching ? searchResults.length : browseTotalCount;
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  const records = useMemo(() => {
    if (!isSearching) return browseRecords;
    const startIndex = (currentPage - 1) * pageSize;
    return searchResults.slice(startIndex, startIndex + pageSize);
  }, [isSearching, browseRecords, searchResults, currentPage, pageSize]);

  const resetPagination = useCallback(() => {
    setCurrentPage(1);
  }, []);

  // Runs the full-text search once per query and keeps every match in
  // memory; page turns during a search only slice this array.
  useEffect(() => {
    if (!enabled || !organizationId || !projectId || !isSearching) {
      setSearchResults([]);
      return;
    }

    let cancelled = false;
    setIsLoading(true);
    setRequestFailed(false);

    fullTextSearchPaginated(
      organizationId,
      searchQuery.trim(),
      [projectId],
      1,
      -1,
      true,
    )
      .then((response) => {
        if (cancelled) return;
        setSearchResults(
          dedupeExistingFiles(
            response.items
              .map((record) => mapRecordToExistingFile(record))
              .filter((record): record is ExistingFile => record !== null),
          ),
        );
      })
      .catch((e) => {
        if (cancelled) return;
        console.error("Failed to search records for record picker:", e);
        setRequestFailed(true);
        setSearchResults([]);
        resetPagination();
      })
      .finally(() => {
        if (!cancelled) setIsLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [enabled, organizationId, projectId, searchQuery, isSearching, resetPagination]);

  // Backend-paginated fetch for plain browsing (no search query).
  useEffect(() => {
    if (!enabled || !organizationId || !projectId || isSearching) {
      if (!isSearching) {
        setBrowseRecords([]);
        setBrowseTotalCount(0);
      }
      return;
    }

    let cancelled = false;
    setIsLoading(true);
    setRequestFailed(false);

    getAllRecords(organizationId, projectId, {
      hideArchived: true,
      pageNumber: currentPage,
      pageSize,
    })
      .then((response) => {
        if (cancelled) return;
        setBrowseRecords(
          dedupeExistingFiles(
            response.items
              .map((record) => mapRecordToExistingFile(record))
              .filter((record): record is ExistingFile => record !== null),
          ),
        );
        setBrowseTotalCount(response.totalCount);
      })
      .catch((e) => {
        if (cancelled) return;
        console.error("Failed to fetch records for record picker:", e);
        setRequestFailed(true);
        setBrowseRecords([]);
        setBrowseTotalCount(0);
        resetPagination();
      })
      .finally(() => {
        if (!cancelled) setIsLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [
    enabled,
    organizationId,
    projectId,
    isSearching,
    currentPage,
    pageSize,
    resetPagination,
  ]);

  useEffect(() => {
    setCurrentPage((previousPage) => Math.min(previousPage, totalPages));
  }, [totalPages]);

  const setPageSize = useCallback(
    (nextPageSize: number) => {
      const validPageSize =
        Number.isFinite(nextPageSize) && nextPageSize > 0
          ? nextPageSize
          : initialPageSize;

      setPageSizeState(validPageSize);
      setCurrentPage(1);
    },
    [initialPageSize],
  );

  const setSearchQuery = useCallback((nextQuery: string) => {
    setSearchQueryState(nextQuery);
    setCurrentPage(1);
  }, []);

  return {
    records,
    totalCount,
    totalPages,
    currentPage,
    setCurrentPage,
    pageSize,
    setPageSize,
    searchQuery,
    setSearchQuery,
    isLoading,
    requestFailed,
  };
}
