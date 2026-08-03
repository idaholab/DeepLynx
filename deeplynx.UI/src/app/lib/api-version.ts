/**
 * The Nexus API version used by the UI.
 *
 * Changing this constant is an API-contract cutover. In particular, moving to
 * v2 requires the UI's non-2xx handling to support RFC 7807 ProblemDetails.
 */
export const NEXUS_API_VERSION = "v1" as const;
export const NEXUS_API_PATH = `/api/${NEXUS_API_VERSION}`;

const VERSIONED_API_SUFFIX = /\/api\/(v[^/]+)$/i;

/**
 * Compose the reviewed API version with an environment-provided origin/base
 * path. A trailing /api/v1 is accepted for backwards-compatible deployments,
 * but a different embedded version is rejected instead of silently overriding
 * the code-level pin.
 */
export function withNexusApiVersion(baseUrl: string): string {
  const base = baseUrl.trim().replace(/\/+$/, "");
  const embeddedVersion = base.match(VERSIONED_API_SUFFIX)?.[1];

  if (embeddedVersion) {
    if (embeddedVersion.toLowerCase() !== NEXUS_API_VERSION) {
      throw new Error(
        `Nexus API URL embeds ${embeddedVersion}; this consumer is pinned to ${NEXUS_API_VERSION}`,
      );
    }
    return base;
  }

  return `${base}${NEXUS_API_PATH}`;
}
