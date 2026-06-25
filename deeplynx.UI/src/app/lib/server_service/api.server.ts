// src/app/lib/api-client.server.ts
import "server-only";
import { cache } from "react";
import { auth } from "../../../../auth";

/**
 * Request-scoped memoized session getter.
 *
 * A single request can call auth() many times (once per apiFetch, plus any
 * direct calls), and the jwt callback in auth.ts refreshes the access token
 * near expiry. Without memoization, concurrent calls within a request can each
 * trigger a refresh against the same single-use Okta refresh token — the same
 * rotation race that signs users out on the client.
 *
 * React cache() memoizes per request, so all callers within one request share a
 * single auth() computation (and at most one refresh). It is intentionally NOT
 * a module-level singleton: that would be shared across every concurrent
 * request/user in the Node process and could leak one user's session to
 * another. cache() is request-scoped, so it isolates users correctly.
 */
export const getServerSession = cache(() => auth());

/** ----- Strict env handling (lazy) ----- */
let _BASE: string | null = null;

function getBase(): string {
  if (_BASE) return _BASE;

  const v = process.env.BACKEND_BASE_URL;
  if (!v) throw new Error("[ENV] BACKEND_BASE_URL is not set");
  if (!/^https?:\/\//.test(v)) {
    throw new Error(`[ENV] BACKEND_BASE_URL must start with http(s):// (got "${v}")`);
  }
  _BASE = v.replace(/\/+$/, ""); // strip trailing slash
  return _BASE;
}

const SERVICE_TOKEN = process.env.BACKEND_SERVICE_TOKEN ?? process.env.SERVICE_TOKEN ?? "";

/** ----- Session helpers ----- */
type SessionShapeA = { tokens?: { access_token?: unknown } };
type SessionShapeB = { accessToken?: unknown };
type MaybeSession = SessionShapeA & SessionShapeB;

function extractAccessToken(x: unknown): string | null {
  if (typeof x !== "object" || x === null) return null;

  const maybeTokens = (x as SessionShapeA).tokens;
  if (typeof maybeTokens === "object" && maybeTokens !== null) {
    const at = (maybeTokens as { access_token?: unknown }).access_token;
    if (typeof at === "string") return at;
  }

  const at2 = (x as SessionShapeB).accessToken;
  if (typeof at2 === "string") return at2;

  return null;
}

async function getBearer(): Promise<string | null> {
  const session: unknown = await getServerSession().catch(() => null);
  const userToken = extractAccessToken(session);
  return userToken ?? (SERVICE_TOKEN || null);
}

async function authHeaders(): Promise<HeadersInit> {
  const token = await getBearer();
  return {
    Accept: "application/json",
    "Content-Type": "application/json",
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
  };
}

/** Small fetch wrapper with detailed error logging */
export async function apiFetch(path: string, init: RequestInit = {}) {
  const url = `${getBase()}${path.startsWith("/") ? "" : "/"}${path}`;
  const headers = {
    ...(await authHeaders()),
    ...(init.headers || {}),
  };

  const res = await fetch(url, {
    ...init,
    headers,
    cache: "no-store",
  });

  if (!res.ok) {
    const body = await res.text().catch(() => "<no response body>");
    console.error("[API ERROR]", {
      method: init.method || "GET",
      url,
      status: res.status,
      statusText: res.statusText,
      respHeaders: Object.fromEntries(res.headers.entries()),
      body: body.slice(0, 2000),
    });
    throw new Error(`API ${init.method || "GET"} ${path} -> ${res.status}`);
  }
  return res;
}

export async function asJson<T>(res: Response): Promise<T> {
  return (await res.json()) as T;
}