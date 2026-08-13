import { loadEnvConfig } from "@next/env";
import { withNexusApiVersion } from "@/app/lib/api-version";

loadEnvConfig(process.cwd());

function requireEnv(name: string): string {
  const value = process.env[name];
  if (!value) {
    throw new Error(`Missing required env var: ${name} — check your .env files`);
  }
  return value;
}

export const TEST_API_BASE_URL = withNexusApiVersion(
  requireEnv("BACKEND_BASE_URL"),
);

export function testApiUrl(path: string): string {
  return `${TEST_API_BASE_URL}${path.startsWith("/") ? "" : "/"}${path}`;
}
