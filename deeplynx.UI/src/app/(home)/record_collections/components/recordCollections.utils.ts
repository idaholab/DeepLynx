import {
  RecordCollectionResponseDto,
  SensitivityLabelsDto,
} from "../../types/responseDTOs";
import { countFacet, parseRecordTags } from "@/app/lib/record_helpers";
import {
  FacetOption,
  MetadataRow,
  NewCollectionSelectedRecord,
} from "./recordCollections.types";

export function getSelectedRecordLabelNames(record: NewCollectionSelectedRecord) {
  if (record.fullRecord?.labels?.length) {
    return record.fullRecord.labels.map((label) => label.name);
  }
  return parseRecordTags(record.labels);
}

export function getSelectedRecordTagNames(record: NewCollectionSelectedRecord) {
  if (record.fullRecord?.tags?.length) {
    return record.fullRecord.tags.map((tag) => tag.name);
  }
  return parseRecordTags(record.tags);
}

export function buildAlphabeticalFacetOptions(values: string[]): FacetOption[] {
  return countFacet(values).sort((a, b) =>
    a.label.localeCompare(b.label, undefined, { sensitivity: "base" }),
  );
}

export function parseProperties(properties?: string | null): Record<string, unknown> {
  if (!properties) return {};

  try {
    const parsed = JSON.parse(properties);
    return typeof parsed === "object" && parsed !== null ? parsed : {};
  } catch {
    return {};
  }
}

export function getMetadataRows(properties?: string | null): MetadataRow[] {
  return Object.entries(parseProperties(properties)).map(([label, value]) => ({
    label,
    value:
      typeof value === "string" || typeof value === "number" || typeof value === "boolean"
        ? String(value)
        : JSON.stringify(value),
  }));
}

export function getSensitivity(collection: RecordCollectionResponseDto) {
  return collection.labels?.[0]?.name ?? "Unlabeled";
}

export function getSensitivityClass(label: string) {
  const lower = label.toLowerCase();
  if (lower.includes("high")) return "badge-error";
  if (lower.includes("moderate") || lower.includes("medium")) return "badge-warning";
  if (lower.includes("low")) return "badge-success";
  return "badge-outline";
}

export function mergeDraftEntities<T extends { id: number; name: string }>(
  baseline: T[] | undefined,
  draft: T[] | undefined,
  refreshed: T[] | undefined,
): T[] {
  const baselineItems = baseline ?? [];
  const draftItems = draft ?? [];
  const refreshedItems = refreshed ?? [];

  const itemMap = new Map<number, T>();
  [...baselineItems, ...draftItems, ...refreshedItems].forEach((item) => {
    itemMap.set(item.id, item);
  });

  const baselineIds = new Set(baselineItems.map((item) => item.id));
  const draftIds = new Set(draftItems.map((item) => item.id));
  const resultIds = new Set(refreshedItems.map((item) => item.id));

  draftIds.forEach((id) => {
    if (!baselineIds.has(id)) resultIds.add(id);
  });

  baselineIds.forEach((id) => {
    if (!draftIds.has(id)) resultIds.delete(id);
  });

  return Array.from(resultIds)
    .map((id) => itemMap.get(id))
    .filter((item): item is T => Boolean(item))
    .sort((a, b) => a.name.localeCompare(b.name, undefined, { sensitivity: "base" }));
}

export function deriveSelectedRecordMetadata(params: {
  availableLabels: SensitivityLabelsDto[];
  selectedRecords: NewCollectionSelectedRecord[];
  selectedRecordLabelTally: FacetOption[];
  selectedRecordTagTally: FacetOption[];
}) {
  const {
    availableLabels,
    selectedRecords,
    selectedRecordLabelTally,
    selectedRecordTagTally,
  } = params;
  const selectedRecordLabelIds = new Set<number>();
  const selectedRecordLabelNames = new Set<string>();

  selectedRecords.forEach((record) => {
    record.fullRecord?.labels?.forEach((label) => {
      if (label.id !== null) selectedRecordLabelIds.add(label.id);
      selectedRecordLabelNames.add(label.name.toLowerCase());
    });
  });

  selectedRecordLabelTally.forEach((label) =>
    selectedRecordLabelNames.add(label.label.toLowerCase()),
  );

  availableLabels
    .filter((label) => selectedRecordLabelNames.has(label.name.toLowerCase()))
    .forEach((label) => selectedRecordLabelIds.add(label.id));

  return {
    labelIds: Array.from(selectedRecordLabelIds),
    tagNames: selectedRecordTagTally.map((tag) => tag.label),
  };
}
