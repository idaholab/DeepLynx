export type ApiProblemDetails = {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  errors?: Record<string, string[] | string>;
};

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}

/** Extract a useful message from RFC 7807 and legacy Nexus error packets. */
export function getApiErrorMessage(
  body: unknown,
  fallback: string,
): string {
  if (typeof body === "string" && body.trim()) return body;
  if (!isObject(body)) return fallback;

  for (const key of ["detail", "message", "error_description"]) {
    const value = body[key];
    if (typeof value === "string" && value.trim()) return value;
  }

  if (isObject(body.errors)) {
    const validationMessages = Object.values(body.errors).flatMap((value) =>
      Array.isArray(value) ? value : [value],
    );
    const message = validationMessages.find(
      (value): value is string => typeof value === "string" && Boolean(value.trim()),
    );
    if (message) return message;
  }

  for (const key of ["title", "error"]) {
    const value = body[key];
    if (typeof value === "string" && value.trim()) return value;
  }

  return fallback;
}

export class ApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly body: unknown,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

export async function apiErrorFromResponse(
  response: Response,
): Promise<ApiError> {
  const text = await response.text().catch(() => "");
  let body: unknown = text || null;

  if (text) {
    try {
      body = JSON.parse(text);
    } catch {
      // A legacy endpoint may return plain text.
    }
  }

  const fallback = `API request failed with status ${response.status}`;
  return new ApiError(getApiErrorMessage(body, fallback), response.status, body);
}
