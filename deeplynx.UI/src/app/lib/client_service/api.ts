// lib/client_service/api.ts

import axios from "axios";
import { getSession } from "next-auth/react";
import type { Session } from "next-auth";
import { getApiErrorMessage } from "../api-error";
import {
  getDefaultNexusApiVersion,
  withNexusApiVersion,
} from "../api-version";

const apiVersion = getDefaultNexusApiVersion();
const apiBaseURL = withNexusApiVersion(
  process.env.NEXT_PUBLIC_API_URL ?? "",
);

const api = axios.create({
  baseURL: apiBaseURL,
});

console.info("[Nexus API] configuration", {
  version: apiVersion,
  baseURL: apiBaseURL,
});

/** Return the intended route without logging query values, headers, or bodies. */
function getRequestRoute(baseURL: string, requestUrl?: string): string {
  const routeWithoutQuery = (requestUrl ?? "").split(/[?#]/, 1)[0];

  if (/^https?:\/\//i.test(routeWithoutQuery)) {
    return routeWithoutQuery;
  }

  return `${baseURL.replace(/\/+$/, "")}/${routeWithoutQuery.replace(/^\/+/, "")}`;
}

// ----------------------------------------------------------------------------
// Single-flight session lookup
//
// Every request runs through this interceptor, and chunked uploads fire several
// requests at once (MAX_CONCURRENT_CHUNKS in file_upload_services.client).
// Calling getSession() on each request independently means that, near the
// access token's expiry, multiple requests trigger a token refresh at the same
// time. With Okta refresh-token rotation that is a race: the first refresh
// succeeds and revokes the (single-use) refresh token, so the others fail with
// invalid_grant, the error clobbers the good session, and the user is signed
// out mid-upload.
//
// To prevent the storm we dedupe concurrent lookups behind a single in-flight
// promise, so at most one refresh is ever running and all concurrent callers
// reuse its result. No value is cached between requests: every request gets a
// fresh, server-validated session (which the server proactively refreshes when
// near expiry), and there is no client-side expiry margin to misconfigure.
// ----------------------------------------------------------------------------

let inflightSession: Promise<Session | null> | null = null;

async function getSessionOnce(): Promise<Session | null> {
  if (!inflightSession) {
    inflightSession = getSession().finally(() => {
      inflightSession = null;
    });
  }

  return inflightSession;
}

// Request interceptor to add token
api.interceptors.request.use(async (config) => {
  console.info("[Nexus API] request", {
    method: (config.method ?? "GET").toUpperCase(),
    route: getRequestRoute(config.baseURL ?? apiBaseURL, config.url),
    version: apiVersion,
  });

  // Skip auth header if frontend authentication is disabled
  const isAuthDisabled =
    process.env.NEXT_PUBLIC_DISABLE_FRONTEND_AUTHENTICATION === "true";

  if (isAuthDisabled) {
    // Don't add authorization header when auth is disabled
    return config;
  }

  // Only get session when auth is enabled
  const session = await getSessionOnce();
  if (session?.tokens?.access_token) {
    config.headers.Authorization = `Bearer ${session.tokens.access_token}`;
  }

  return config;
});

// Axios already parses application/problem+json. Preserve the packet on
// error.response.data while surfacing its detail/title through Error.message.
api.interceptors.response.use(undefined, (error: unknown) => {
  if (axios.isAxiosError(error) && error.response) {
    error.message = getApiErrorMessage(error.response.data, error.message);
  }
  return Promise.reject(error);
});

export default api;
