"use client";

export interface InsightSamplingParameters {
  temperature: number;
  maxTokens: number;
  topP: number;
}

export interface InsightQueryPayload {
  question: string;
  fileIds?: number[];
  samplingParameters?: InsightSamplingParameters;
  llmServerUrl?: string;
  llmModelName?: string;
  llmAuthToken?: string;
  embeddingServerUrl?: string;
  embeddingModelName?: string;
  embeddingAuthToken?: string;
}

export interface InsightUploadFileInfo {
  fileId: number;
  fileURI: string;
}

export interface InsightUploadPayload {
  fileInfo: InsightUploadFileInfo[];
  llmServerUrl?: string;
  llmModelName?: string;
  llmAuthToken?: string;
  embeddingServerUrl?: string;
  embeddingModelName?: string;
  embeddingAuthToken?: string;
}

export interface InsightUploadResultItem {
  file_id: number;
  status: "queued" | "error";
  pdf_url?: string;
  file_type?: string;
  queue_name?: string;
  error?: string;
}

export interface InsightUploadResponse {
  results: InsightUploadResultItem[];
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
  llm_server_url?: string;
  llm_model_name?: string;
  llm_auth_token?: string;
  embedding_server_url?: string;
  embedding_model_name?: string;
  embedding_auth_token?: string;
}

interface InsightUploadRequestBody {
  file_info: InsightUploadFileInfo[];
  llm_server_url?: string;
  llm_model_name?: string;
  llm_auth_token?: string;
  embedding_server_url?: string;
  embedding_model_name?: string;
  embedding_auth_token?: string;
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

function toRequestBody(payload: InsightQueryPayload): InsightQueryRequestBody {
  const samplingParameters =
    payload.samplingParameters ?? DEFAULT_SAMPLING_PARAMETERS;

  return {
    question: payload.question,
    file_ids: payload.fileIds,
    sampling_parameters: {
      temperature: samplingParameters.temperature,
      max_tokens: samplingParameters.maxTokens,
      top_p: samplingParameters.topP,
    },
    llm_server_url: payload.llmServerUrl,
    llm_model_name: payload.llmModelName,
    llm_auth_token: payload.llmAuthToken,
    embedding_server_url: payload.embeddingServerUrl,
    embedding_model_name: payload.embeddingModelName,
    embedding_auth_token: payload.embeddingAuthToken,
  };
}

function toUploadRequestBody(
  payload: InsightUploadPayload,
): InsightUploadRequestBody {
  return {
    file_info: payload.fileInfo.map((file) => ({
      ...file,
      fileURI: normalizeInsightFileUri(file.fileURI),
    })),
    llm_server_url: payload.llmServerUrl,
    llm_model_name: payload.llmModelName,
    llm_auth_token: payload.llmAuthToken,
    embedding_server_url: payload.embeddingServerUrl,
    embedding_model_name: payload.embeddingModelName,
    embedding_auth_token: payload.embeddingAuthToken,
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

function parseInsightResponse(text: string): unknown {
  try {
    return text ? JSON.parse(text) : null;
  } catch {
    return text;
  }
}

function extractInsightErrorMessage(value: unknown): string {
  if (typeof value === "string") return value.trim();
  if (!value || typeof value !== "object") return "";

  const payload = value as InsightErrorPayload;
  return (
    extractInsightErrorMessage(payload.details) ||
    extractInsightErrorMessage(payload.detail) ||
    extractInsightErrorMessage(payload.error) ||
    extractInsightErrorMessage(payload.results?.[0]?.error) ||
    extractInsightErrorMessage(payload.message)
  );
}

export async function queueInsightUpload(
  payload: InsightUploadPayload,
): Promise<InsightUploadResponse> {
  const response = await fetch("/api/v1/insight/upload", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(toUploadRequestBody(payload)),
  });

  const text = await response.text();
  const body = parseInsightResponse(text);

  if (!response.ok) {
    throw new Error(
      extractInsightErrorMessage(body) || text || "Insight upload failed",
    );
  }

  return body as InsightUploadResponse;
}

export async function streamInsightQuery(
  payload: InsightQueryPayload,
  onChunk: (chunk: string) => void,
): Promise<string> {
  const response = await fetch("/api/insight/query", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(toRequestBody(payload)),
  });

  if (!response.ok) {
    const errorBody = await response.text();
    throw new Error(errorBody || "Insight query failed");
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
    onChunk(chunk);
  }

  const trailing = decoder.decode();
  if (trailing) {
    fullText += trailing;
    onChunk(trailing);
  }

  return fullText;
}

export async function fetchInsightIngestionStatus(
  recordId: number,
): Promise<InsightIngestionStatusResponse> {
  const response = await fetch(`/api/insight/status/${recordId}`, {
    method: "GET",
    headers: { Accept: "application/json" },
    cache: "no-store",
  });

  const text = await response.text();
  const body = parseInsightResponse(text);

  if (!response.ok) {
    throw new Error(
      extractInsightErrorMessage(body) || text || "Insight status check failed",
    );
  }

  return body as InsightIngestionStatusResponse;
}
