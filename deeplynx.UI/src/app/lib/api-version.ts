// src/app/lib/api-version.ts

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

/**
 * Return the environment-selected API version.
 * NEXT_PUBLIC_API_VERSION must be set by the environment (dev, test, and
 * production all enforce this at build time) — there is no hardcoded fallback.
 *
 * Exception: CI_PREFLIGHT builds only verify that the app compiles and never
 * ship or run against real traffic, so they're allowed a placeholder version
 * instead of failing the build.
 */
export function getDefaultNexusApiVersion(): string {
  const version = process.env.NEXT_PUBLIC_API_VERSION;

  if (!version) {
    if (process.env.CI_PREFLIGHT === "true") {
      return "v2";
    }

    throw new Error(
      "NEXT_PUBLIC_API_VERSION is not set. This must be provided by the build environment.",
    );
  }

  return normalizeNexusApiVersion(version);
}

/** Normalize an environment-provided origin or API URL to its /api base path. */
export function getNexusApiBaseUrl(baseUrl: string): string {
  const base = baseUrl.trim().replace(/\/+$/, "");

  if (VERSIONED_API_SUFFIX.test(base)) {
    return base.replace(VERSIONED_API_SUFFIX, "/api");
  }

  if (/\/api$/i.test(base)) {
    return base;
  }

  return `${base}/api`;
}

/**
 * Compose an API version with an environment-provided origin or /api base path.
 * Host-only and legacy versioned base URLs remain supported during migration.
 * The entire UI targets the single version selected by the environment.
 */
export function withNexusApiVersion(baseUrl: string): string {
  return `${getNexusApiBaseUrl(baseUrl)}/${getDefaultNexusApiVersion()}`;
}

/** Append an endpoint path to an already-versioned Nexus API base URL. */
export function appendNexusApiPath(apiBaseUrl: string, path: string): string {
  const base = apiBaseUrl.trim().replace(/\/+$/, "");
  return `${base}${path.startsWith("/") ? "" : "/"}${path}`;
}

/** Build the Scalar documentation URL, whose version follows /api/scalar. */
export function getNexusScalarUrl(baseUrl: string): string {
  return `${getNexusApiBaseUrl(baseUrl)}/scalar/${getDefaultNexusApiVersion()}`;
}
