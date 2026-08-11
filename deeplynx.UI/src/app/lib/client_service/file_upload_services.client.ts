// src/app/lib/file_upload_services.client.ts

import { RecordResponseDto } from "@/app/(home)/types/responseDTOs";
import {
  UploadFileArgs,
  ChunkedUploadSession,
  ChunkUploadOptions,
  ChunkedUploadOptions,
  UploadProgressEvent,
} from "@/app/(home)/types/types";
import api from "./api";
import { CreateRecordFileUploadRequestDto } from "@/app/(home)/types/requestDTOs";

// ============================================================================
// CONSTANTS
// ============================================================================

export const CHUNK_THRESHOLD = 500 * 1024 * 1024; // 500MB threshold to determine regular or chunked upload
const MAX_RETRIES = 3;                      // Retry failed chunks up to 3 times
const DEFAULT_MAX_CONCURRENT_FILES = 5;
const MIN_CHUNK_SIZE = 256 * 1024;          // floor so retries can't shrink to near-zero progress
const TARGET_CHUNK_DURATION_S = 15;         // aim for each chunk to take roughly this long
const CHUNK_TIMEOUT_MS = 60_000;            // per-chunk timeout; ~half the reference tool's ~120s proxy budget
const TIMEOUT_MAX_SHRINKS = 6;              // safety cap on repeated timeout->shrink->retry cycles
const EMA_ALPHA = 0.3;                      // smoothing factor for throughput estimate

export type BatchUploadProgressEvent = {
  completed: number;
  total: number;
  succeeded: number;
  failed: number;
};

// ============================================================================
// PUBLIC API
// ============================================================================

/**
 * Uploads a file - automatically uses chunking for files > 500MB
 */
export async function uploadFile(args: UploadFileArgs) {
  if (!args.organizationId || !args.projectId || !args.file) {
    throw new Error("organizationId, projectId, and file are required");
  }

  // Automatically choose upload method based on file size
  if (args.file.size > CHUNK_THRESHOLD) {
    return uploadFileChunked(args);
  } else {
    return uploadFileRegular(args);
  }
}

export async function uploadFilesBatch(
  args: Omit<UploadFileArgs, "file" | "onProgress"> & {
    files: File[];
    maxConcurrentFiles?: number;
    onProgress?: (progress: BatchUploadProgressEvent) => void;
  }
) {
  const {
    files,
    maxConcurrentFiles = DEFAULT_MAX_CONCURRENT_FILES,
    onProgress,
    ...rest
  } = args;

  if (files.length === 0) return [];

  const maxConcurrency = Math.max(
    1,
    Math.min(maxConcurrentFiles, files.length),
  );
  const results: PromiseSettledResult<RecordResponseDto>[] = Array(files.length);
  let nextFileIndex = 0;
  let completed = 0;
  let succeeded = 0;
  let failed = 0;

  const runWorker = async (): Promise<void> => {
    while (true) {
      const currentIndex = nextFileIndex++;
      if (currentIndex >= files.length) return;

      const file = files[currentIndex];

      try {
        const value = await uploadFile({ ...rest, file });
        results[currentIndex] = { status: "fulfilled", value };
        succeeded += 1;
      } catch (reason) {
        results[currentIndex] = { status: "rejected", reason };
        failed += 1;
      } finally {
        completed += 1;
        onProgress?.({
          completed,
          total: files.length,
          succeeded,
          failed,
        });
      }
    }
  };

  const workers = Array.from({ length: maxConcurrency }, () => runWorker());
  await Promise.all(workers);

  return results;
}

// Store AbortController for cancellation
let currentUploadAbortController: AbortController | null = null;

/**
 * Cancel ongoing upload
 */
export function cancelCurrentUpload(): void {
  if (currentUploadAbortController) {
    currentUploadAbortController.abort();
    // Don't set to null here - let the upload function handle cleanup
  }
}

// ============================================================================
// REGULAR UPLOAD (< 500MB)
// ============================================================================

async function uploadFileRegular({
  organizationId,
  projectId,
  file,
  dataSourceId,
  objectStorageId,
  name,
  description,
  properties,
  tags,
  originalId,
  classId,
  metadataFile,
}: UploadFileArgs) {
  const form = new FormData();
  form.append("file", file, file.name ?? "upload.bin");

  if (metadataFile) form.append("metadata", metadataFile, metadataFile.name);
  if (name) form.append("name", name);
  if (description) form.append("description", description);

  if (properties) {
    form.append(
      "properties",
      typeof properties === "string" ? properties : JSON.stringify(properties)
    );
  }

  if (tags && tags.length > 0) {
    form.append("tags", JSON.stringify(tags));
  }

  if (originalId) form.append("originalId", originalId);
  if (classId != null) form.append("classId", String(classId));

  const params: Record<string, number | string> = {};
  if (dataSourceId != null) params.dataSourceId = dataSourceId;
  if (objectStorageId != null) params.objectStorageId = objectStorageId;

  const { data } = await api.post<RecordResponseDto>(
    `/organizations/${organizationId}/projects/${projectId}/files`,
    form,
    { params }
  );

  return data;
}

// ============================================================================
// CHUNKED UPLOAD (>= 500MB)
// ============================================================================

async function uploadFileChunked({
  file,
  organizationId,
  projectId,
  dataSourceId,
  objectStorageId,
  onProgress,
  metadataFile
}: UploadFileArgs) {
  let uploadId: string | null = null;

  // Create and store the AbortController locally and globally
  const abortController = new AbortController();
  currentUploadAbortController = abortController;

  try {
    const session = await startChunkedUpload({
      organizationId,
      projectId,
      dataSourceId,
      objectStorageId,
      fileName: file.name,
      fileSize: file.size,
      metadataFile
    });

    uploadId = session.uploadId;

    const maxChunkSize = Math.max(MIN_CHUNK_SIZE, session.chunkSize);

    const chunksSent = await uploadChunksSequentialAdaptive(
      file,
      uploadId,
      maxChunkSize,
      { organizationId, projectId, dataSourceId, objectStorageId },
      abortController.signal,
      onProgress
    );

    const result = await completeChunkedUpload({
      organizationId,
      projectId,
      dataSourceId,
      objectStorageId,
      uploadId,
      fileName: file.name,
      totalChunks: chunksSent,
      metadataFile
    });

    onProgress?.({
      percentComplete: 100,
      chunksCompleted: chunksSent,
      totalChunks: chunksSent,
      currentBatch: chunksSent,
      uploadId,
      bytesUploaded: file.size,
      totalBytes: file.size,
      chunkSize: maxChunkSize,
      speedBytesPerSec: 0,
    });

    return result;
  } catch (error) {
    // Check if this is an abort error
    if (error instanceof DOMException && error.name === 'AbortError') {
    }

    if (uploadId) {
      await cancelChunkedUpload({
        organizationId,
        projectId,
        dataSourceId,
        objectStorageId,
        uploadId,
      }).catch((err) => {
        console.error("Failed to cancel upload:", err);
      });
    }
    throw error;
  } finally {
    // Only clear if this is still the current controller
    if (currentUploadAbortController === abortController) {
      currentUploadAbortController = null;
    }
  }
}

async function startChunkedUpload(
  options: ChunkedUploadOptions
): Promise<ChunkedUploadSession> {
  const { organizationId, projectId, fileName, fileSize, dataSourceId, objectStorageId, metadataFile } = options;

  const params: Record<string, number | string> = {};
  if (dataSourceId != null) params.dataSourceId = dataSourceId;
  if (objectStorageId != null) params.objectStorageId = objectStorageId;
  const metadata = metadataFile
    ? await parseMetadataFile(metadataFile)
    : undefined;

  const { data } = await api.post<ChunkedUploadSession>(
    `/organizations/${organizationId}/projects/${projectId}/files/upload/start`,
    { fileName, fileSize, metadata },
    { params }
  );

  return data;
}

// ---- Sequential adaptive upload loop ---------------------------------------
// Replaces the old splitFileIntoChunks + uploadChunksInBatches pair. Chunks
// are sliced on-the-fly since size can change chunk-to-chunk; there is no
// fixed chunk array up front.

async function uploadChunksSequentialAdaptive(
  file: File,
  uploadId: string,
  maxChunkSize: number,
  options: {
    organizationId: number | string;
    projectId: number | string;
    dataSourceId?: number | string;
    objectStorageId?: number | string;
  },
  parentSignal: AbortSignal,
  onProgress?: (progress: UploadProgressEvent) => void
): Promise<number> {
  let offset = 0;
  let chunkSize = maxChunkSize;
  let chunkNumber = 0;
  let throughputEMA: number | null = null;

  while (offset < file.size) {
    if (parentSignal.aborted) {
      throw new DOMException("Upload cancelled", "AbortError");
    }

    const end = Math.min(offset + chunkSize, file.size);
    const chunk = file.slice(offset, end);

    const { success, elapsedMs, shrunkTo } = await uploadSingleChunkAdaptive(
      { ...options, uploadId, chunk, chunkNumber, maxChunkSize },
      chunkSize,
      parentSignal
    );

    if (!success) {
      // Non-timeout failure exhausted its retries inside
      // uploadSingleChunkAdaptive and threw; this branch shouldn't be
      // reached, but guard anyway rather than looping forever.
      throw new Error(`Chunk ${chunkNumber} failed and could not be recovered.`);
    }

    if (shrunkTo != null) {
      // A timeout occurred and the chunk was retried at a smaller size;
      // adopt that smaller size going forward rather than immediately
      // trying to grow back, so a flaky patch of network doesn't
      // oscillate the size on every request.
      chunkSize = shrunkTo;
      throughputEMA = null; // discard stale throughput history after a shrink
    } else {
      const throughput = chunk.size / (elapsedMs / 1000);
      throughputEMA = updateEMA(throughputEMA, throughput);
      chunkSize = nextChunkSizeFromThroughput(throughputEMA, chunkSize, maxChunkSize);
    }

    offset = end;
    chunkNumber += 1;

    const remainingBytes = file.size - offset;
    // totalChunks is an ESTIMATE that will fluctuate as chunkSize adapts —
    // do not treat it as a stable denominator. Prefer bytesUploaded/totalBytes
    // for UI that needs a stable, monotonic progress measure.
    const estimatedRemainingChunks = chunkSize > 0 ? Math.ceil(remainingBytes / chunkSize) : 0;

    onProgress?.({
      percentComplete: Math.round((offset / file.size) * 1000) / 10,
      chunksCompleted: chunkNumber,
      totalChunks: chunkNumber + estimatedRemainingChunks,
      currentBatch: chunkNumber,
      uploadId,
      bytesUploaded: offset,
      totalBytes: file.size,
      chunkSize,
      speedBytesPerSec: throughputEMA ?? 0,
    });
  }

  return chunkNumber;
}

// Uploads one chunk with a timeout. On timeout, shrinks the chunk and
// retries the SAME byte range at the smaller size (up to TIMEOUT_MAX_SHRINKS
// times). On non-timeout failure (5xx, network error), retries the same
// byte range at the same size with linear backoff (existing MAX_RETRIES
// behavior), matching prior behavior for that failure class.
async function uploadSingleChunkAdaptive(
  args: {
    organizationId: number | string;
    projectId: number | string;
    dataSourceId?: number | string;
    objectStorageId?: number | string;
    uploadId: string;
    chunk: Blob;
    chunkNumber: number;
    maxChunkSize: number;
  },
  initialChunkSize: number,
  parentSignal: AbortSignal
): Promise<{ success: boolean; elapsedMs: number; shrunkTo: number | null }> {
  let currentChunk = args.chunk;
  let currentSize = initialChunkSize;
  let shrinkAttempts = 0;
  let retryAttempts = 0;
  let didShrink = false;

  while (true) {
    if (parentSignal.aborted) {
      throw new DOMException("Upload cancelled", "AbortError");
    }

    // Combine the parent (user-initiated cancel) signal with a per-chunk
    // timeout signal so either one can abort this specific request.
    const chunkController = new AbortController();
    const onParentAbort = () => chunkController.abort("cancelled");
    parentSignal.addEventListener("abort", onParentAbort);
    const timeoutId = setTimeout(() => chunkController.abort("timeout"), CHUNK_TIMEOUT_MS);

    const startedAt = performance.now();
    try {
      const form = new FormData();
      form.append("chunk", currentChunk);
      form.append("uploadId", args.uploadId);
      form.append("chunkNumber", String(args.chunkNumber));

      const params: Record<string, number | string> = {};
      if (args.dataSourceId != null) params.dataSourceId = args.dataSourceId;
      if (args.objectStorageId != null) params.objectStorageId = args.objectStorageId;

      await api.post(
        `/organizations/${args.organizationId}/projects/${args.projectId}/files/upload/chunk`,
        form,
        { params, signal: chunkController.signal }
      );

      clearTimeout(timeoutId);
      parentSignal.removeEventListener("abort", onParentAbort);
      return {
        success: true,
        elapsedMs: performance.now() - startedAt,
        shrunkTo: didShrink ? currentSize : null,
      };
    } catch (error) {
      clearTimeout(timeoutId);
      parentSignal.removeEventListener("abort", onParentAbort);

      if (parentSignal.aborted) {
        throw new DOMException("Upload cancelled", "AbortError");
      }

      const timedOut = chunkController.signal.reason === "timeout";

      if (timedOut) {
        shrinkAttempts += 1;
        if (shrinkAttempts > TIMEOUT_MAX_SHRINKS) {
          throw new Error(
            `Chunk ${args.chunkNumber} kept timing out even after shrinking ${TIMEOUT_MAX_SHRINKS} times.`
          );
        }
        currentSize = clampChunkSize(currentSize / 2, args.maxChunkSize);
        // Re-slice the same starting byte at the new, smaller size.
        // NOTE: caller passed `chunk` already sliced at the old size, so we
        // re-derive from it — this only works because Blob.slice on an
        // already-sliced chunk still refers to the correct underlying bytes.
        currentChunk = currentChunk.slice(0, currentSize);
        didShrink = true;
        console.warn(`Chunk ${args.chunkNumber} timed out after ${CHUNK_TIMEOUT_MS}ms; retrying at ${currentSize} bytes.`);
        continue;
      }

      // Non-timeout failure: same retry/backoff behavior as before.
      retryAttempts += 1;
      console.warn(`Chunk ${args.chunkNumber} failed (attempt ${retryAttempts}/${MAX_RETRIES})`);
      if (retryAttempts >= MAX_RETRIES) {
        throw new Error(`Chunk ${args.chunkNumber} failed after ${MAX_RETRIES} attempts`);
      }
      await sleep(1000 * retryAttempts);
      continue;
    }
  }
}

async function completeChunkedUpload(options: {
  organizationId: number | string;
  projectId: number | string;
  dataSourceId?: number | string;
  objectStorageId?: number | string;
  uploadId: string;
  fileName: string;
  totalChunks: number;
  metadataFile?: File | undefined;
}): Promise<RecordResponseDto> {
  const { organizationId, projectId, dataSourceId, objectStorageId, uploadId, fileName, totalChunks, metadataFile } = options;

  const params: Record<string, number | string> = {};
  if (dataSourceId != null) params.dataSourceId = dataSourceId;
  if (objectStorageId != null) params.objectStorageId = objectStorageId;
  const metadata = metadataFile
    ? await parseMetadataFile(metadataFile)
    : undefined;

  const { data } = await api.post<RecordResponseDto>(
    `/organizations/${organizationId}/projects/${projectId}/files/upload/complete`,
    { uploadId, fileName, totalChunks, metadata },
    { params }
  );

  return data;
}

export async function cancelChunkedUpload(options: {
  organizationId: number | string;
  projectId: number | string;
  dataSourceId?: number | string;
  objectStorageId?: number | string;
  uploadId: string;
}): Promise<void> {
  const { organizationId, projectId, dataSourceId, objectStorageId, uploadId } = options;

  const params: Record<string, number | string> = {};
  if (dataSourceId != null) params.dataSourceId = dataSourceId;
  if (objectStorageId != null) params.objectStorageId = objectStorageId;

  await api.delete(
    `/organizations/${organizationId}/projects/${projectId}/files/upload/${uploadId}`,
    { params }
  );
}

// ============================================================================
// UTILITIES
// ============================================================================
function clampChunkSize(size: number, maxChunkSize: number): number {
  return Math.min(maxChunkSize, Math.max(MIN_CHUNK_SIZE, Math.round(size)));
}

function updateEMA(prevEMA: number | null, sample: number, alpha = EMA_ALPHA): number {
  if (prevEMA == null || !isFinite(prevEMA)) return sample;
  return alpha * sample + (1 - alpha) * prevEMA;
}

function nextChunkSizeFromThroughput(
  throughputBytesPerSec: number | null,
  fallback: number,
  maxChunkSize: number
): number {
  if (!throughputBytesPerSec || !isFinite(throughputBytesPerSec)) return fallback;
  return clampChunkSize(throughputBytesPerSec * TARGET_CHUNK_DURATION_S, maxChunkSize);
}

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

async function parseMetadataFile(file?: File | null): Promise<CreateRecordFileUploadRequestDto | undefined> {
  if (!file) return undefined;

  const metadataJson = await file.text();
  if (!metadataJson.trim()) {
    throw new Error("Metadata file is empty or contains no content.");
  }

  try {
    return JSON.parse(metadataJson) as CreateRecordFileUploadRequestDto;
  } catch {
    throw new Error("Failed to parse metadata file.")
  }
}