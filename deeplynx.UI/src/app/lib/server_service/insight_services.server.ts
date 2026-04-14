import "server-only";

const DEFAULT_INSIGHT_API_URL = "http://localhost:5009";

type InsightJsonResult = {
  upstreamResponse: Response;
  responseBody: unknown;
};

export function getInsightBaseUrl(): string {
  const raw = process.env.INSIGHT_API_URL || DEFAULT_INSIGHT_API_URL;
  return raw.replace(/\/+$/, "");
}

export function getInsightUrl(path: string): string {
  const normalizedPath = path.startsWith("/") ? path : `/${path}`;
  return `${getInsightBaseUrl()}${normalizedPath}`;
}

export async function fetchInsight(
  path: string,
  init: RequestInit = {},
): Promise<Response> {
  return fetch(getInsightUrl(path), {
    ...init,
    cache: "no-store",
  });
}

export async function uploadInsightDocument(
  body: unknown,
): Promise<InsightJsonResult> {
  const upstreamResponse = await fetchInsight("/upload_document", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });

  const responseBody = await parseInsightBody(upstreamResponse);
  return { upstreamResponse, responseBody };
}

export async function queryInsight(body: unknown): Promise<Response> {
  return fetchInsight("/query", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Accept: "text/plain",
    },
    body: JSON.stringify(body),
  });
}

export async function fetchInsightIngestionStatus(
  recordId: number,
): Promise<InsightJsonResult> {
  const upstreamResponse = await fetchInsight(`/ingestion_status/${recordId}`, {
    method: "GET",
    headers: { Accept: "application/json" },
  });

  const responseBody = await parseInsightBody(upstreamResponse);
  return { upstreamResponse, responseBody };
}

export async function parseInsightBody(response: Response): Promise<unknown> {
  const responseText = await response.text();

  if (!responseText) {
    return null;
  }

  try {
    return JSON.parse(responseText);
  } catch {
    return { message: responseText };
  }
}

export function getInsightErrorMessage(
  fallbackMessage: string,
  error: unknown,
): string {
  return error instanceof Error ? error.message : fallbackMessage;
}
