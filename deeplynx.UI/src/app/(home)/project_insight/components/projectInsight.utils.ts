"use client";

import type {
  ClassResponseDto,
  DataSourceResponseDto,
  RecordResponseDto,
} from "@/app/(home)/types/responseDTOs";
import type {
  NamedInsightOption,
  ProjectInsightRecord,
  ProjectInsightStatus,
} from "./projectInsight.types";

const INSIGHT_SUPPORTED_FILE_TYPES = new Set(["pdf", "txt", "html", "htm"]);

function normalizeFileType(value?: string | null): string | null {
  if (!value) return null;
  const normalized = value.trim().toLowerCase().replace(/^\./, "");
  return normalized.length > 0 ? normalized : null;
}

function getFileExtensionFromValue(value?: string | null): string | null {
  if (!value) return null;
  const trimmed = value.trim();
  if (!trimmed) return null;

  try {
    const url = new URL(trimmed);
    return getFileExtensionFromValue(url.pathname);
  } catch {
    // Not a URL, continue parsing as a local path.
  }

  const withoutQueryOrHash = trimmed.split(/[?#]/, 1)[0];
  const lastPathSegment =
    withoutQueryOrHash.split("/").pop() ?? withoutQueryOrHash;
  const extensionIndex = lastPathSegment.lastIndexOf(".");

  if (extensionIndex <= 0 || extensionIndex === lastPathSegment.length - 1) {
    return null;
  }

  return normalizeFileType(lastPathSegment.slice(extensionIndex + 1));
}

function resolveInsightFileType(
  fileType?: string | null,
  uri?: string | null,
  name?: string | null,
): string | null {
  return (
    normalizeFileType(fileType) ??
    getFileExtensionFromValue(uri) ??
    getFileExtensionFromValue(name)
  );
}

function normalizeNamedOptions(value: unknown): NamedInsightOption[] {
  if (!value) return [];

  const rawItems = (() => {
    if (Array.isArray(value)) return value;
    if (typeof value === "string") {
      try {
        const parsed = JSON.parse(value);
        return Array.isArray(parsed) ? parsed : [];
      } catch {
        return [];
      }
    }
    return [];
  })();

  return rawItems.flatMap((item) => {
    if (!item || typeof item !== "object") return [];

    const candidate = item as { id?: unknown; name?: unknown };
    if (typeof candidate.id !== "number" || typeof candidate.name !== "string") {
      return [];
    }

    return [{ id: candidate.id, name: candidate.name }];
  });
}

export function mapProjectInsightRecords(
  records: RecordResponseDto[],
  classes: ClassResponseDto[],
  dataSources: DataSourceResponseDto[],
): ProjectInsightRecord[] {
  const classMap = new Map(classes.map((item) => [item.id, item.name]));
  const dataSourceMap = new Map(dataSources.map((item) => [item.id, item.name]));

  return records.flatMap((record) => {
    if (record.id == null) return [];

    const resolvedFileType = resolveInsightFileType(
      record.fileType,
      record.uri,
      record.name,
    );

    return [
      {
        id: Number(record.id),
        name: record.name,
        description: record.description?.trim() ?? "",
        uri: record.uri?.trim() || null,
        fileType: resolvedFileType,
        classId:
          typeof record.classId === "number" ? Number(record.classId) : null,
        className:
          (typeof record.classId === "number"
            ? classMap.get(Number(record.classId))
            : null) ?? "",
        dataSourceId:
          typeof record.dataSourceId === "number"
            ? Number(record.dataSourceId)
            : null,
        dataSourceName:
          (typeof record.dataSourceId === "number"
            ? dataSourceMap.get(Number(record.dataSourceId))
            : null) ?? "",
        tags: normalizeNamedOptions(record.tags),
        labels: normalizeNamedOptions(record.labels),
        lastUpdatedAt: record.lastUpdatedAt ?? null,
        isArchived: Boolean(record.isArchived),
        isInsightSupported:
          resolvedFileType !== null &&
          INSIGHT_SUPPORTED_FILE_TYPES.has(resolvedFileType),
      },
    ];
  });
}

export function matchesInsightFilters(
  record: ProjectInsightRecord,
  filters: {
    classIds: number[];
    tagIds: number[];
  },
): boolean {
  const { classIds, tagIds } = filters;

  if (classIds.length > 0 && (!record.classId || !classIds.includes(record.classId))) {
    return false;
  }

  if (
    tagIds.length > 0 &&
    !tagIds.every((id) => record.tags.some((tag) => tag.id === id))
  ) {
    return false;
  }
 
  return true;
}

export function getProjectInsightStatus(
  record: ProjectInsightRecord,
  statusMap: Record<number, ProjectInsightStatus>,
): ProjectInsightStatus {
  if (!record.isInsightSupported) {
    return { state: "unsupported" };
  }

  return statusMap[record.id] ?? { state: "checking" };
}
