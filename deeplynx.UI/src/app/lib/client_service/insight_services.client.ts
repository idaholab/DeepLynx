"use client";
import api from "./api";

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

export interface FetchInsightEndpointHealthArgs {
  organizationId: number;
  projectId: number;
  modelConfigId?: number | null;
  modelType: "llm" | "vlm" | "embedding";
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

export interface InsightEndpointHealthResponse {
  reachable: boolean;
  model_available: boolean;
  latency_ms?: number | null;
  model_metadata?: Record<string, unknown> | null;
  detail?: string | null;
}

export type InsightModelHealthState = {
  isChecking: boolean;
  response: InsightEndpointHealthResponse | null;
  error: string | null;
}

export type InsightEndpointHealthByRole = {
  query: InsightModelHealthState;
  upload: InsightModelHealthState;
  embedding: InsightModelHealthState;
};

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
  try {
    const res = await api.post<InsightUploadResponse>(
      `/organizations/${uploadRequest.organizationId}/projects/${uploadRequest.projectId}/insight/upload`,
      toInsightUploadRequestBody(uploadRequest),
      {
        params: {
          vlmModelConfigId: uploadRequest.vlmModelConfigId,
          embeddingModelConfigId: uploadRequest.embeddingModelConfigId,
        },
      },
    );
    return res.data;
  } catch (error: any) {
    const message =
      extractInsightErrorMessage(error?.response?.data) ||
      error?.message ||
      "Insight upload failed";
    throw new Error(message);
  }
}


export async function streamInsightQuery(
  queryRequest: StreamInsightQueryArgs,
  onResponseChunk: (chunk: string) => void,
): Promise<string> {
  let lastLength = 0;

  try {
    const res = await api.post<string>(
      `/organizations/${queryRequest.organizationId}/projects/${queryRequest.projectId}/insight/query`,
      toInsightQueryRequestBody(queryRequest),
      {
        params: {
          languageModelConfigId: queryRequest.languageModelConfigId,
          embeddingModelConfigId: queryRequest.embeddingModelConfigId,
        },
        responseType: "text",
        // Browser (XHR) adapter: onDownloadProgress fires as bytes arrive,
        // and xhr.responseText accumulates everything received so far. We
        // diff against what's already been emitted to reconstruct chunks,
        // approximating the old fetch()+ReadableStream behavior. The
        // controller streams "text/plain" chunks (see Query action), which
        // this is compatible with.
        onDownloadProgress: (progressEvent: any) => {
          const xhr = progressEvent?.event?.target as
            | XMLHttpRequest
            | undefined;
          const responseText: string = xhr?.responseText ?? "";
          if (responseText.length > lastLength) {
            const chunk = responseText.slice(lastLength);
            lastLength = responseText.length;
            onResponseChunk(chunk);
          }
        },
      },
    );

    return typeof res.data === "string" ? res.data : String(res.data ?? "");
  } catch (error: any) {
    const message =
      extractInsightErrorMessage(error?.response?.data) ||
      error?.message ||
      "Insight query failed";
    throw new Error(message);
  }
}

export async function fetchInsightIngestionStatus(
  statusRequest: FetchInsightStatusArgs,
): Promise<InsightIngestionStatusResponse> {
  try {
    const res = await api.get<InsightIngestionStatusResponse>(
      `/organizations/${statusRequest.organizationId}/projects/${statusRequest.projectId}/insight/ingestion_status/${statusRequest.fileId}`,
    );
    return res.data;
  } catch (error: any) {
    const message =
      extractInsightErrorMessage(error?.response?.data) ||
      error?.message ||
      "Insight status check failed";
    throw new Error(message);
  }
}

export async function fetchInsightEndpointHealth(
  healthRequest: FetchInsightEndpointHealthArgs,
): Promise<InsightEndpointHealthResponse> {
  try {
    const res = await api.post<InsightEndpointHealthResponse>(
      `/organizations/${healthRequest.organizationId}/projects/${healthRequest.projectId}/insight/endpoint_health`,
      {
        modelConfigId: healthRequest.modelConfigId ?? null,
        modelType: healthRequest.modelType,
      },
    );
    return res.data;
  } catch (error: any) {
    const message =
      extractInsightErrorMessage(error?.response?.data) ||
      error?.message ||
      "Insight endpoint health check failed";
    throw new Error(message);
  }
}