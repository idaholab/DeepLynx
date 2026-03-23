"use client";

import type {
  NamedInsightOption,
  ProjectInsightFiltersState,
  ProjectInsightRecord,
  ProjectInsightStatus,
} from "./projectInsight.types";

export type ProjectInsightTabKey = "library" | "pending";

export type TabFilterState = ProjectInsightFiltersState & {
  searchQuery: string;
};

export type ActiveFilterPill = {
  id: string;
  label: string;
  type: "class" | "tag";
  optionId: number;
};

export const EMPTY_TAB_FILTER_STATE: TabFilterState = {
  classIds: [],
  tagIds: [],
  searchQuery: "",
};

export function withTokens(
  template: string,
  values: Record<string, string | number>,
): string {
  return Object.entries(values).reduce(
    (result, [key, value]) => result.replaceAll(`{${key}}`, String(value)),
    template,
  );
}

export function sortNamedOptions<T extends { id: number; name: string }>(
  items: T[],
): NamedInsightOption[] {
  return [...items]
    .filter((item) => item.name.trim().length > 0)
    .map((item) => ({ id: item.id, name: item.name }))
    .sort((left, right) => left.name.localeCompare(right.name));
}

export function matchesMetadataSearch(
  record: ProjectInsightRecord,
  normalizedQuery: string,
): boolean {
  if (!normalizedQuery) return true;

  const haystack = [
    record.id,
    record.name,
    record.description,
    record.className,
    record.dataSourceName,
    record.fileType,
    ...record.tags.map((tag) => tag.name),
  ]
    .filter(Boolean)
    .join(" ")
    .toLowerCase();

  return haystack.includes(normalizedQuery);
}

export function getStatusFromError(error: unknown): ProjectInsightStatus {
  const message = error instanceof Error ? error.message : "";

  if (/404|not found|not indexed|missing/i.test(message)) {
    return { state: "not_embedded" };
  }

  return {
    state: "error",
    error: message,
  };
}

export function buildActiveFilterPills(
  filters: ProjectInsightFiltersState,
  classOptions: NamedInsightOption[],
  tagOptions: NamedInsightOption[],
): ActiveFilterPill[] {
  return [
    ...classOptions
      .filter((option) => filters.classIds.includes(option.id))
      .map((option) => ({
        id: `class-${option.id}`,
        label: option.name,
        type: "class" as const,
        optionId: option.id,
      })),
    ...tagOptions
      .filter((option) => filters.tagIds.includes(option.id))
      .map((option) => ({
        id: `tag-${option.id}`,
        label: option.name,
        type: "tag" as const,
        optionId: option.id,
      })),
  ];
}
