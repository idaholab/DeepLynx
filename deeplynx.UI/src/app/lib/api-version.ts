export const DEFAULT_NEXUS_API_VERSION = "v1" as const;

const API_VERSION_PATTERN = /^v[1-9]\d*$/i;
const VERSIONED_API_SUFFIX = /\/api\/(v[1-9]\d*)$/i;

/** Return a normalized API version, rejecting malformed URL segments. */
export function normalizeNexusApiVersion(version: string): string {
  const normalizedVersion = version.trim().toLowerCase();

  if (!API_VERSION_PATTERN.test(normalizedVersion)) {
    throw new Error(
      `Invalid Nexus API version "${version}"; expected "v" followed by a positive integer`,
    );
  }

  return normalizedVersion;
}

/** Return the environment-selected API version, with v1 as a safe fallback. */
export function getDefaultNexusApiVersion(): string {
  return normalizeNexusApiVersion(
    process.env.NEXT_PUBLIC_API_VERSION || DEFAULT_NEXUS_API_VERSION,
  );
}

/**
 * Compose an API version with an environment-provided origin or /api base path.
 * Host-only and legacy versioned base URLs remain supported during migration.
 * Passing version lets an individual service explicitly target another API
 * contract without changing the configured default.
 */
export function withNexusApiVersion(
  baseUrl: string,
  version: string = getDefaultNexusApiVersion(),
): string {
  const base = baseUrl.trim().replace(/\/+$/, "");
  const normalizedVersion = normalizeNexusApiVersion(version);

  if (VERSIONED_API_SUFFIX.test(base)) {
    return base.replace(VERSIONED_API_SUFFIX, `/api/${normalizedVersion}`);
  }

  if (/\/api$/i.test(base)) {
    return `${base}/${normalizedVersion}`;
  }

  return `${base}/api/${normalizedVersion}`;
}
