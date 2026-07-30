import "server-only";
import { withNexusApiVersion } from "../api-version";

let backendApiBaseUrl: string | null = null;

export function getBackendApiBaseUrl(): string {
  if (backendApiBaseUrl) return backendApiBaseUrl;

  const configuredUrl = process.env.BACKEND_BASE_URL;
  if (!configuredUrl) {
    throw new Error("[ENV] BACKEND_BASE_URL is not set");
  }
  if (!/^https?:\/\//.test(configuredUrl)) {
    throw new Error(
      `[ENV] BACKEND_BASE_URL must start with http(s):// (got "${configuredUrl}")`,
    );
  }

  backendApiBaseUrl = withNexusApiVersion(configuredUrl);
  return backendApiBaseUrl;
}

export function backendApiUrl(path: string): string {
  return `${getBackendApiBaseUrl()}${path.startsWith("/") ? "" : "/"}${path}`;
}
