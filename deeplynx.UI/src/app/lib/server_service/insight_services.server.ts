import "server-only";
import { auth } from "../../../../auth";

type SessionWithNestedAccessToken = {
  tokens?: { access_token?: unknown };
};
type SessionWithDirectAccessToken = { accessToken?: unknown };
type InsightAuthSession = SessionWithNestedAccessToken &
  SessionWithDirectAccessToken;

type InsightJsonResult = {
  upstreamResponse: Response;
  responseBody: unknown;
};

type InsightModelConfigQuery = {
  languageModelConfigId?: number;
  embeddingModelConfigId?: number;
  vlmModelConfigId?: number;
};

const BASE = (process.env.BACKEND_BASE_URL ?? "").replace(/\/+$/, "");

function extractAccessToken(session: unknown): string | null {
  if (typeof session !== "object" || session === null) return null;

  const nestedTokenContainer = (session as SessionWithNestedAccessToken).tokens;
  if (
    typeof nestedTokenContainer === "object" &&
    nestedTokenContainer !== null
  ) {
    const accessToken = nestedTokenContainer.access_token;
    if (typeof accessToken === "string") return accessToken;
  }

  const directAccessToken = (session as SessionWithDirectAccessToken)
    .accessToken;
  return typeof directAccessToken === "string" ? directAccessToken : null;
}

async function getAuthHeaders(contentType = true): Promise<HeadersInit> {
  const session: InsightAuthSession | null = await auth().catch(() => null);
  const token =
    extractAccessToken(session) ??
    process.env.BACKEND_SERVICE_TOKEN ??
    process.env.SERVICE_TOKEN ??
    "";

  return {
    Accept: "application/json",
    ...(contentType ? { "Content-Type": "application/json" } : {}),
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
  };
}

function buildInsightUrl(
  organizationId: number,
  projectId: number,
  path: string,
  query?: Record<string, number | undefined>,
) {
  const qs = new URLSearchParams();
  Object.entries(query ?? {}).forEach(([key, value]) => {
    if (value !== undefined) qs.set(key, String(value));
  });

  const suffix = qs.toString() ? `?${qs.toString()}` : "";
  return `${BASE}/organizations/${organizationId}/projects/${projectId}/insight${path}${suffix}`;
}

async function parseJsonOrTextResponseBody(response: Response) {
  const text = await response.text();
  if (!text) return null;
  try {
    return JSON.parse(text);
  } catch {
    return { message: text };
  }
}

export function getInsightErrorMessage(
  fallbackMessage: string,
  error: unknown,
): string {
  return error instanceof Error ? error.message : fallbackMessage;
}

export async function queueInsightUpload(
  organizationId: number,
  projectId: number,
  requestBody: unknown,
  modelConfigQuery?: Pick<
    InsightModelConfigQuery,
    "vlmModelConfigId" | "embeddingModelConfigId"
  >,
): Promise<InsightJsonResult> {
  const upstreamResponse = await fetch(
    buildInsightUrl(organizationId, projectId, "/upload", modelConfigQuery),
    {
      method: "POST",
      headers: await getAuthHeaders(),
      body: JSON.stringify(requestBody),
      cache: "no-store",
    },
  );

  return {
    upstreamResponse,
    responseBody: await parseJsonOrTextResponseBody(upstreamResponse.clone()),
  };
}

export async function streamInsightQuery(
  organizationId: number,
  projectId: number,
  requestBody: unknown,
  modelConfigQuery?: Pick<
    InsightModelConfigQuery,
    "languageModelConfigId" | "embeddingModelConfigId"
  >,
): Promise<Response> {
  return fetch(
    buildInsightUrl(organizationId, projectId, "/query", modelConfigQuery),
    {
      method: "POST",
      headers: await getAuthHeaders(),
      body: JSON.stringify(requestBody),
      cache: "no-store",
    },
  );
}

export async function fetchInsightIngestionStatus(
  organizationId: number,
  projectId: number,
  fileId: number,
): Promise<InsightJsonResult> {
  const upstreamResponse = await fetch(
    buildInsightUrl(organizationId, projectId, `/ingestion_status/${fileId}`),
    { method: "GET", headers: await getAuthHeaders(false), cache: "no-store" },
  );

  return {
    upstreamResponse,
    responseBody: await parseJsonOrTextResponseBody(upstreamResponse.clone()),
  };
}

export async function queueInsightEmbedStrings(
  organizationId: number,
  projectId: number,
  embeddingModelConfigId?: number,
): Promise<InsightJsonResult> {
  const upstreamResponse = await fetch(
    buildInsightUrl(organizationId, projectId, "/embed_strings", {
      embeddingModelConfigId,
    }),
    { method: "POST", headers: await getAuthHeaders(false), cache: "no-store" },
  );

  return {
    upstreamResponse,
    responseBody: await parseJsonOrTextResponseBody(upstreamResponse.clone()),
  };
}

export async function fetchInsightEndpointHealth(
    organizationId: number,
    projectId: number,
    requestBody: unknown,
): Promise<InsightJsonResult> {
  const upstreamResponse = await fetch(
      buildInsightUrl(organizationId, projectId, "/endpoint_health"),
      {
        method: "POST", headers: await getAuthHeaders(), body: JSON.stringify(requestBody), cache: "no-store",
      },
  );
  
  return {
    upstreamResponse,
    responseBody: await parseJsonOrTextResponseBody(upstreamResponse.clone()),
  };
}