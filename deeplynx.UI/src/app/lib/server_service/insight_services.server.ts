import "server-only";

const DEFAULT_INSIGHT_API_URL = "http://localhost:5009";

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
