// src/app/lib/filter_services.server.ts
import "server-only";
import { apiErrorFromResponse } from "../api-error";
import { backendApiUrl } from "./backend-api-url.server";

const SERVICE_TOKEN = process.env.SERVICE_TOKEN || "";

/** Always return HeadersInit to avoid union type issues */
function authHeaders(): HeadersInit {
  const h: Record<string, string> = {};
  if (SERVICE_TOKEN) h.Authorization = `Bearer ${SERVICE_TOKEN}`;
  return h;
}

async function asJson<T>(res: Response): Promise<T> {
  if (!res.ok) throw await apiErrorFromResponse(res);
  return (await res.json()) as T;
}

export async function filterRecordsServer<T = unknown>(
  filter: string
): Promise<T> {
  const res = await fetch(backendApiUrl("records/Filter"), {
    method: "POST",
    headers: { ...authHeaders(), "Content-Type": "application/json" },
    body: filter,
    cache: "no-store",
  });
  return asJson<T>(res);
}

export async function queryRecordsServer<T = unknown>(
  query: string
): Promise<T> {
  const sp = new URLSearchParams({ userQuery: query });
  const res = await fetch(backendApiUrl(`records/Filter/?${sp.toString()}`), {
    headers: authHeaders(),
    cache: "no-store",
  });
  return asJson<T>(res);
}
