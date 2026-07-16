import {
  QueryRecordViewResponseDto,
  RecordCollectionResponseDto,
  RecordResponseDto,
  SensitivityLabelsDto,
  TagResponseDto,
} from "@/app/(home)/types/responseDTOs";
import {
  NewCollectionSelectedRecord,
  FacetOption,
  MetadataRow,
} from "../(home)/record_collections/components/recordCollections.types";

/**
 * Escapes special RegExp metacharacters in a string so it can be safely
 * embedded inside `new RegExp(...)` without accidentally treating user-typed
 * characters like `.`, `*`, or `(` as pattern operators.
 * Private to this module — callers should use getHighlightedContent instead.
 */
function escapeRegExp(value: string) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

export function interpolateTemplate(
  template: string,
  values: Record<string, string | number>,
) {
  return Object.entries(values).reduce(
    (result, [key, value]) => result.replaceAll(`{${key}}`, String(value)),
    template,
  );
}

/**
 * Wraps any portion of `text` that matches one of the active search `queries`
 * in a styled <mark> element so users can see why a record appeared in results.
 *
 * Returns an object rather than just ReactNode so that callers can branch on
 * `matched` if needed (e.g. to apply additional styling to the whole field).
 *
 * Why `unknown` for text: record fields coming from the API can be strings,
 * numbers, or null depending on the endpoint. Accepting `unknown` and
 * converting with String() centralises that coercion here rather than at
 * every call site.
 *
 * Why split+test instead of replaceAll: React requires an array of ReactNodes
 * with stable keys, not a raw HTML string, so we split on the match, then
 * wrap matched segments in <mark> and leave non-matched segments as plain
 * strings that React renders as text nodes.
 *
 * The key uses both `part` content and `index` because the same word can
 * appear multiple times in the same string and would otherwise produce
 * duplicate keys.
 */
export function getHighlightedContent(
  text: unknown,
  queries: string[],
): { content: React.ReactNode; matched: boolean } {
  const safeText = String(text ?? "");

  // Only highlight the first matching query to keep rendering simple.
  const match = queries.find((q) =>
    safeText.toLowerCase().includes(q.toLowerCase()),
  );

  if (!match) return { content: safeText, matched: false };

  const regex = new RegExp(`(${escapeRegExp(match)})`, "gi");
  const parts = safeText.split(regex);

  return {
    matched: true,
    content: parts.map((part, index) =>
      regex.test(part) ? (
        <mark
          key={`${part}-${index}`}
          className="rounded bg-warning px-1 text-warning-content"
        >
          {part}
        </mark>
      ) : (
        part
      ),
    ),
  };
}

/**
 * Normalises the `tags` field on a record into a flat string array.
 *
 * Tags are stored as a JSON-serialised value in the database and have
 * accumulated several shapes over time:
 *   - A plain string (legacy): "finance"
 *   - An array of strings: ["finance", "approved"]
 *   - An array of TagResponseDto objects: [{ id: 1, name: "finance" }]
 *   - A single TagResponseDto: { id: 1, name: "finance" }
 *
 * The function defensively handles all of these so the UI never crashes on
 * older records and new records are automatically supported. Returns [] on
 * parse failure so callers can treat it as "no tags" without error handling.
 */
export function parseRecordTags(tags: string | null | undefined) {
  if (!tags) return [];

  try {
    const parsed = JSON.parse(tags);

    // Normalise to array regardless of whether the stored value was a single
    // object or an array.
    const arr = Array.isArray(parsed) ? parsed : [parsed];

    return arr.flatMap((item: TagResponseDto | string) => {
      if (typeof item === "string") return [item];
      if (item && typeof item === "object") {
        // Prefer the canonical `name` field; fall back to any string values
        // in the object for forward-compatibility with schema changes.
        if (typeof item.name === "string") return [item.name];
        return Object.values(item).filter(
          (value): value is string => typeof value === "string",
        );
      }
      return [];
    });
  } catch {
    return [];
  }
}

/**
 * Counts occurrences of each unique value in `values` and returns the result
 * sorted by count descending, then label ascending as a deterministic
 * tiebreaker so facet options don't jump around between renders.
 *
 * Blank / whitespace-only values are intentionally skipped because they
 * cannot be meaningfully filtered on and would clutter the sidebar.
 */
export function countFacet(values: string[]) {
  const counts = new Map<string, number>();

  values.forEach((value) => {
    const trimmed = value.trim();
    if (!trimmed) return;
    counts.set(trimmed, (counts.get(trimmed) ?? 0) + 1);
  });

  return Array.from(counts.entries())
    .map(([label, count]) => ({ label, count }))
    .sort((a, b) => b.count - a.count || a.label.localeCompare(b.label));
}

export function getSelectedRecordLabelNames(
  record: NewCollectionSelectedRecord,
) {
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

export function parseProperties(
  properties?: string | null,
): Record<string, unknown> {
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
      typeof value === "string" ||
      typeof value === "number" ||
      typeof value === "boolean"
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
  if (lower.includes("moderate") || lower.includes("medium"))
    return "badge-warning";
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
    .sort((a, b) =>
      a.name.localeCompare(b.name, undefined, { sensitivity: "base" }),
    );
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

export function mapSearchResultToCollectionRecord(
  record: QueryRecordViewResponseDto,
): RecordResponseDto {
  return {
    id: record.id,
    name: record.name,
    description: record.description,
    uri: record.uri,
    properties: record.properties,
    objectStorageId: record.objectStorageId,
    originalId: record.originalId,
    classId: record.classId,
    className: record.className,
    dataSourceId: record.dataSourceId,
    dataSourceName: record.dataSourceName,
    projectId: record.projectId,
    lastUpdatedAt: record.lastUpdatedAt,
    lastUpdatedBy:
      record.lastUpdatedBy === null || record.lastUpdatedBy === undefined
        ? null
        : String(record.lastUpdatedBy),
    isArchived: record.isArchived,
    fileType: record.fileType,
    fileSize: record.fileSize,
  };
}
