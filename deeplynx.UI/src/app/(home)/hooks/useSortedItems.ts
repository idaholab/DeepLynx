"use client";

import { useCallback, useEffect, useMemo, useState } from "react";

export type SortOptionConfig<TValue extends string = string> = {
  value: TValue;
  label: string;
};

export type SortOption<T, TValue extends string = string> =
  SortOptionConfig<TValue> & {
    compare: (a: T, b: T) => number;
  };

type UseSortedItemsOptions<T, TValue extends string = string> = {
  items: T[];
  sortOptions?: SortOption<T, TValue>[];
  defaultSortValue?: TValue;
};

export function useSortedItems<T, TValue extends string = string>({
  items,
  sortOptions,
  defaultSortValue,
}: UseSortedItemsOptions<T, TValue>) {
  const [sortValue, setSortValueState] = useState<TValue | "">(
    defaultSortValue ?? sortOptions?.[0]?.value ?? "",
  );

  useEffect(() => {
    if (!sortOptions?.length) return;

    const nextSortValue = defaultSortValue ?? sortOptions[0].value;
    const hasValidSortValue = sortOptions.some(
      (option) => option.value === sortValue,
    );

    if (!hasValidSortValue) {
      setSortValueState(nextSortValue);
    }
  }, [defaultSortValue, sortOptions, sortValue]);

  const sortedItems = useMemo(() => {
    if (!sortOptions?.length) return items;

    const activeSort =
      sortOptions.find((option) => option.value === sortValue) ??
      sortOptions[0];

    return [...items].sort(activeSort.compare);
  }, [items, sortOptions, sortValue]);

  const setSortValue = useCallback((value: TValue) => {
    setSortValueState(value);
  }, []);

  return {
    sortValue,
    setSortValue,
    sortedItems,
  };
}
