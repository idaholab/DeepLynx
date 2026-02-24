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
    file_info: payload.fileInfo,
    llm_server_url: payload.llmServerUrl,
    llm_model_name: payload.llmModelName,
    llm_auth_token: payload.llmAuthToken,
    embedding_server_url: payload.embeddingServerUrl,
    embedding_model_name: payload.embeddingModelName,
    embedding_auth_token: payload.embeddingAuthToken,
  };
}

export async function queueInsightUpload(
  payload: InsightUploadPayload,
): Promise<InsightUploadResponse> {
  const response = await fetch("/api/insight/upload", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(toUploadRequestBody(payload)),
  });

  const text = await response.text();
  let body: InsightUploadResponse | { message?: string; details?: string } =
    { results: [] };

  if (text) {
    try {
      body = JSON.parse(text);
    } catch {
      body = { message: text };
    }
  }

  if (!response.ok) {
    const details =
      typeof body === "object" && body !== null && "details" in body
        ? String(body.details ?? "")
        : text;
    throw new Error(details || "Insight upload failed");
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
  let body: InsightIngestionStatusResponse | { message?: string; details?: string } =
    { file_id: recordId, indexed: false, chunk_count: 0, page_count: 0 };

  if (text) {
    try {
      body = JSON.parse(text);
    } catch {
      body = { message: text };
    }
  }

  if (!response.ok) {
    const details =
      typeof body === "object" && body !== null && "details" in body
        ? String(body.details ?? "")
        : text;
    throw new Error(details || "Insight status check failed");
  }

  return body as InsightIngestionStatusResponse;
}
