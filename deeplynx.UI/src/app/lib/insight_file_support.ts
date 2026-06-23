export const INSIGHT_SUPPORTED_FILE_TYPES = new Set([
  "pdf",
  "txt",
  "html",
  "htm",
  "png",
  "jpg",
  "jpeg",
  "webp",
]);

export function normalizeInsightFileType(
  value?: string | null,
): string | null {
  if (!value) return null;

  const normalized = value.trim().toLowerCase().replace(/^\./, "");
  return normalized.length > 0 ? normalized : null;
}

export function getInsightFileExtension(
  value?: string | null,
): string | null {
  if (!value) return null;

  const trimmed = value.trim();
  if (!trimmed) return null;

  try {
    const url = new URL(trimmed);
    return getInsightFileExtension(url.pathname);
  } catch {
    // Not a URL, continue parsing as a path or filename.
  }

  const withoutQueryOrHash = trimmed.split(/[?#]/, 1)[0];
  const lastPathSegment =
    withoutQueryOrHash.split("/").pop() ?? withoutQueryOrHash;
  const extensionIndex = lastPathSegment.lastIndexOf(".");

  if (extensionIndex <= 0 || extensionIndex === lastPathSegment.length - 1) {
    return null;
  }

  return normalizeInsightFileType(lastPathSegment.slice(extensionIndex + 1));
}

export function resolveInsightFileType(
  fileType?: string | null,
  uri?: string | null,
  name?: string | null,
): string | null {
  return (
    normalizeInsightFileType(fileType) ??
    getInsightFileExtension(uri) ??
    getInsightFileExtension(name)
  );
}

export function isInsightSupportedFileType(
  fileType?: string | null,
  uri?: string | null,
  name?: string | null,
): boolean {
  const resolvedFileType = resolveInsightFileType(fileType, uri, name);
  return (
    resolvedFileType !== null &&
    INSIGHT_SUPPORTED_FILE_TYPES.has(resolvedFileType)
  );
}
