"use client";

export interface InsightSamplingParameters {
  temperature: number;
  maxTokens: number;
  topP: number;
}

export interface InsightUploadFileInfo {
  fileId: number;
  fileUri: string;
}

export interface QueueInsightUploadArgs {
  organizationId: number;
  projectId: number;
  fileInfo: InsightUploadFileInfo[];
  vlmModelConfigId?: number;
  embeddingModelConfigId?: number;
}

export interface StreamInsightQueryArgs {
  organizationId: number;
  projectId: number;
  question: string;
  fileIds?: number[];
  samplingParameters?: InsightSamplingParameters;
  languageModelConfigId?: number;
  embeddingModelConfigId?: number;
}

export interface FetchInsightStatusArgs {
  organizationId: number;
  projectId: number;
  fileId: number;
}

export interface InsightUploadResponse {
  message?: string;
}

export interface InsightIngestionStatusResponse {
  file_id: number;
  indexed: boolean;
  chunk_count: number;
  page_count: number;
}

interface InsightQueryRequestBody {
  question: string;
  file_ids?: number[];
  sampling_parameters: {
    temperature: number;
    max_tokens: number;
    top_p: number;
  };
}

interface InsightUploadRequestBody {
  fileInfo: Array<{
    fileId: number;
    fileUri: string;
  }>;
}

const DEFAULT_SAMPLING_PARAMETERS: InsightSamplingParameters = {
  temperature: 0.1,
  maxTokens: 1024,
  topP: 0.9,
};

type InsightErrorPayload = {
  message?: unknown;
  details?: unknown;
  detail?: unknown;
  error?: unknown;
  results?: Array<{ error?: unknown }>;
};

function toInsightQueryRequestBody(
  queryRequest: StreamInsightQueryArgs,
): InsightQueryRequestBody {
  const samplingParameters =
    queryRequest.samplingParameters ?? DEFAULT_SAMPLING_PARAMETERS;

  return {
    question: queryRequest.question,
    file_ids: queryRequest.fileIds,
    sampling_parameters: {
      temperature: samplingParameters.temperature,
      max_tokens: samplingParameters.maxTokens,
      top_p: samplingParameters.topP,
    },
  };
}

function toInsightUploadRequestBody(
  uploadRequest: QueueInsightUploadArgs,
): InsightUploadRequestBody {
  return {
    fileInfo: uploadRequest.fileInfo.map((file) => ({
      fileId: file.fileId,
      fileUri: normalizeInsightFileUri(file.fileUri),
    })),
  };
}

function normalizeInsightFileUri(fileUri: string): string {
  const trimmed = fileUri.trim();
  if (!trimmed) return trimmed;

  if (/^[a-z][a-z0-9+.-]*:\/\//i.test(trimmed)) {
    return trimmed;
  }

  if (trimmed.startsWith("/data/")) {
    return trimmed;
  }

  if (trimmed.startsWith("org_")) {
    return `/data/${trimmed}`;
  }

  const orgPathIndex = trimmed.indexOf("/org_");
  if (orgPathIndex >= 0) {
    return `/data${trimmed.slice(orgPathIndex)}`;
  }

  return trimmed;
}

function parseJsonOrTextResponseBody(responseText: string): unknown {
  try {
    return responseText ? JSON.parse(responseText) : null;
  } catch {
    return responseText;
  }
}

function extractInsightErrorMessage(value: unknown): string {
  if (typeof value === "string") return value.trim();
  if (!value || typeof value !== "object") return "";

  const errorPayload = value as InsightErrorPayload;
  return (
    extractInsightErrorMessage(errorPayload.details) ||
    extractInsightErrorMessage(errorPayload.detail) ||
    extractInsightErrorMessage(errorPayload.error) ||
    extractInsightErrorMessage(errorPayload.results?.[0]?.error) ||
    extractInsightErrorMessage(errorPayload.message)
  );
}

function appendOptionalNumberParam(
  queryParams: URLSearchParams,
  paramName: string,
  paramValue?: number,
) {
  if (typeof paramValue === "number" && Number.isFinite(paramValue)) {
    queryParams.set(paramName, String(paramValue));
  }
}

export async function queueInsightUpload(
  uploadRequest: QueueInsightUploadArgs,
): Promise<InsightUploadResponse> {
  const queryParams = new URLSearchParams({
    organizationId: String(uploadRequest.organizationId),
    projectId: String(uploadRequest.projectId),
  });
  appendOptionalNumberParam(
    queryParams,
    "vlmModelConfigId",
    uploadRequest.vlmModelConfigId,
  );
  appendOptionalNumberParam(
    queryParams,
    "embeddingModelConfigId",
    uploadRequest.embeddingModelConfigId,
  );

  const response = await fetch(`/api/insight/upload?${queryParams.toString()}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(toInsightUploadRequestBody(uploadRequest)),
  });

  const responseText = await response.text();
  const responseBody = parseJsonOrTextResponseBody(responseText);

  if (!response.ok) {
    throw new Error(
      extractInsightErrorMessage(responseBody) ||
        responseText ||
        "Insight upload failed",
    );
  }

  return responseBody as InsightUploadResponse;
}

export async function streamInsightQuery(
  queryRequest: StreamInsightQueryArgs,
  onResponseChunk: (chunk: string) => void,
): Promise<string> {
  const queryParams = new URLSearchParams({
    organizationId: String(queryRequest.organizationId),
    projectId: String(queryRequest.projectId),
  });
  appendOptionalNumberParam(
    queryParams,
    "languageModelConfigId",
    queryRequest.languageModelConfigId,
  );
  appendOptionalNumberParam(
    queryParams,
    "embeddingModelConfigId",
    queryRequest.embeddingModelConfigId,
  );

  const response = await fetch(`/api/insight/query?${queryParams.toString()}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(toInsightQueryRequestBody(queryRequest)),
  });

  if (!response.ok) {
    const errorResponseText = await response.text();
    throw new Error(errorResponseText || "Insight query failed");
  }

  if (!response.body) {
    throw new Error("Insight response stream is unavailable");
  }

  const reader = response.body.getReader();
  const decoder = new TextDecoder();
  let fullText = "";

  while (true) {
    const { done, value } = await reader.read();
    if (done) break;
    if (!value) continue;

    const chunk = decoder.decode(value, { stream: true });
    if (!chunk) continue;

    fullText += chunk;
    onResponseChunk(chunk);
  }

  const trailing = decoder.decode();
  if (trailing) {
    fullText += trailing;
    onResponseChunk(trailing);
  }

  return fullText;
}

export async function fetchInsightIngestionStatus(
  statusRequest: FetchInsightStatusArgs,
): Promise<InsightIngestionStatusResponse> {
  const response = await fetch(
    `/api/insight/status/${statusRequest.fileId}?organizationId=${statusRequest.organizationId}&projectId=${statusRequest.projectId}`,
    {
      method: "GET",
      headers: { Accept: "application/json" },
      cache: "no-store",
    },
  );

  const responseText = await response.text();
  const responseBody = parseJsonOrTextResponseBody(responseText);

  if (!response.ok) {
    throw new Error(
      extractInsightErrorMessage(responseBody) ||
        responseText ||
        "Insight status check failed",
    );
  }

  return responseBody as InsightIngestionStatusResponse;
}
