"use client";

import { useCallback, useEffect, useMemo, useState } from "react";

type UseLocalPaginationOptions<T> = {
  items: T[];
  initialPageSize?: number;
};

export function useLocalPagination<T>({
  items,
  initialPageSize = 5,
}: UseLocalPaginationOptions<T>) {
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSizeState] = useState(initialPageSize);

  const totalPages = Math.max(1, Math.ceil(items.length / pageSize));
  const startIndex = (currentPage - 1) * pageSize;

  const paginatedItems = useMemo(
    () => items.slice(startIndex, startIndex + pageSize),
    [items, pageSize, startIndex],
  );

  useEffect(() => {
    setCurrentPage((previousPage) => Math.min(previousPage, totalPages));
  }, [totalPages]);

  const setPageSize = useCallback((nextPageSize: number) => {
    setPageSizeState(nextPageSize);
    setCurrentPage(1);
  }, []);

  const resetPagination = useCallback(() => {
    setCurrentPage(1);
  }, []);

  return {
    currentPage,
    pageSize,
    paginatedItems,
    resetPagination,
    setCurrentPage,
    setPageSize,
    startIndex,
    totalPages,
  };
}
