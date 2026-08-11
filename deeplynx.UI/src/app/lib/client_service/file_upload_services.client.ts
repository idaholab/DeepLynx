// src/app/lib/file_upload_services.client.ts

import { RecordResponseDto } from "@/app/(home)/types/responseDTOs";
import {
  UploadFileArgs,
  ChunkedUploadSession,
  ChunkedUploadOptions,
  UploadProgressEvent,
} from "@/app/(home)/types/types";
import api from "./api";
import { CreateRecordFileUploadRequestDto } from "@/app/(home)/types/requestDTOs";

// ============================================================================
// CONSTANTS
// ============================================================================

export const CHUNK_THRESHOLD = 500 * 1024 * 1024; // 500MB threshold to determine regular or chunked upload
const DEFAULT_MAX_CONCURRENT_FILES = 5;

// ---- Chunked upload config --------------------------------------------------
// Concurrency is FIXED at 4 workers for the whole upload — this is not
// adaptive. Only chunk size adapts, based on a throughput estimate shared
// across all 4 workers. This is a deliberate simplification versus a fully
// adaptive pool: if 4 concurrent chunks turns out to be systematically too
// many for real-world connections (many chunks timing out/erroring around
// the same time, not just occasional ones), that's a sign this fixed value
// needs revisiting — but the tradeoff was chosen to avoid the added
// complexity and untested tuning of also making pool size adaptive.
const CHUNK_CONCURRENCY = 4;
const MIN_CHUNK_SIZE = 256 * 1024;          // floor so retries can't shrink to near-zero progress
const TARGET_CHUNK_DURATION_S = 15;         // aim for each chunk to take roughly this long
const CHUNK_TIMEOUT_MS = 60_000;            // per-chunk timeout; ~half the reference tool's ~120s proxy budget
const TIMEOUT_MAX_SHRINKS = 6;              // safety cap on repeated timeout->shrink->retry cycles for one claim
const MAX_RETRIES = 3;                      // non-timeout failure retries per chunk, same offset/size
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
// REGULAR UPLOAD (< 500MB) — unchanged
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
// CHUNKED UPLOAD (>= 500MB) — fixed concurrency (4 workers), adaptive size
// ============================================================================

// Shared state claimed from by all 4 workers for a single file's upload.
// Safe under JS's single-threaded execution: claimNext() never awaits
// between reading and updating the cursor, so two workers can't claim
// overlapping byte ranges.
class ChunkClaimState {
  cursor = 0;                 // next byte offset not yet claimed
  nextChunkNumber = 0;        // next chunk index not yet claimed
  bytesCompleted = 0;         // sum of bytes from CONFIRMED (server-acked) chunks only
  chunkSize: number;
  throughputEMA: number | null = null;
  failed: Error | DOMException | null = null;

  constructor(public totalBytes: number, initialChunkSize: number, public maxChunkSize: number) {
    this.chunkSize = Math.max(MIN_CHUNK_SIZE, initialChunkSize);
  }

  claimNext(): { offset: number; size: number; chunkNumber: number } | null {
    if (this.cursor >= this.totalBytes) return null;
    const size = Math.min(this.chunkSize, this.totalBytes - this.cursor);
    const offset = this.cursor;
    const chunkNumber = this.nextChunkNumber;
    this.cursor += size;
    this.nextChunkNumber += 1;
    return { offset, size, chunkNumber };
  }

  onChunkSuccess(bytes: number, elapsedMs: number) {
    this.bytesCompleted += bytes;
    const throughput = bytes / (elapsedMs / 1000);
    this.throughputEMA =
      this.throughputEMA == null ? throughput : EMA_ALPHA * throughput + (1 - EMA_ALPHA) * this.throughputEMA;
    this.chunkSize = clampChunkSize((this.throughputEMA ?? throughput) * TARGET_CHUNK_DURATION_S, this.maxChunkSize);
  }

  onChunkTimeout() {
    // Shared shrink: affects whatever the NEXT claim (by any of the 4
    // workers) will use. The worker that actually timed out also shrinks
    // its own in-flight retry separately — see uploadClaimWithRetry.
    this.chunkSize = clampChunkSize(this.chunkSize / 2, this.maxChunkSize);
  }
}

function clampChunkSize(size: number, maxChunkSize: number): number {
  return Math.min(maxChunkSize, Math.max(MIN_CHUNK_SIZE, Math.round(size)));
}

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

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

    // ASSUMPTION (needs backend confirmation): session.chunkSize is an
    // INITIAL/MAXIMUM size, not a fixed contract, and chunkNumber only
    // needs to be numerically correct for server-side reassembly — the
    // backend does NOT need chunks to arrive in chunkNumber order. With 4
    // concurrent workers, requests WILL complete out of order. If the
    // backend assembles strictly by arrival order rather than by the
    // chunkNumber field, this breaks and must fall back to sequential.
    const maxChunkSize = Math.max(MIN_CHUNK_SIZE, session.chunkSize);

    const chunksSent = await uploadChunksConcurrentAdaptive(
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
      totalChunks: chunksSent, // actual count sent, may differ from session.totalChunks
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

// ---- Concurrent adaptive upload: fixed pool of CHUNK_CONCURRENCY workers,
// all claiming from one shared cursor, all reporting into one shared
// throughput estimate that sets chunk size for whoever claims next.

async function uploadChunksConcurrentAdaptive(
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
  const state = new ChunkClaimState(file.size, maxChunkSize, maxChunkSize);

  const reportProgress = () => {
    const remainingBytes = state.totalBytes - state.bytesCompleted;
    const estimatedRemainingChunks = state.chunkSize > 0 ? Math.ceil(remainingBytes / state.chunkSize) : 0;
    // totalChunks is an ESTIMATE that fluctuates as chunkSize adapts — do
    // not treat it as a stable denominator. bytesUploaded/totalBytes is the
    // stable measure.
    onProgress?.({
      percentComplete: Math.round((state.bytesCompleted / state.totalBytes) * 1000) / 10,
      chunksCompleted: state.nextChunkNumber, // claimed, not necessarily all confirmed yet
      totalChunks: state.nextChunkNumber + estimatedRemainingChunks,
      currentBatch: state.nextChunkNumber,
      uploadId,
      bytesUploaded: state.bytesCompleted,
      totalBytes: state.totalBytes,
      chunkSize: state.chunkSize,
      speedBytesPerSec: state.throughputEMA ?? 0,
    });
  };

  const worker = async (): Promise<void> => {
    while (true) {
      if (parentSignal.aborted) throw new DOMException("Upload cancelled", "AbortError");
      if (state.failed) return;

      const claim = state.claimNext();
      if (!claim) return; // nothing left to claim — this worker is done

      const chunk = file.slice(claim.offset, claim.offset + claim.size);
      const success = await uploadClaimWithRetry(
        { ...options, uploadId, chunk, chunkNumber: claim.chunkNumber, maxChunkSize },
        state,
        parentSignal
      );

      if (!success) return; // state.failed is set; other workers will notice and stop too

      reportProgress();
    }
  };

  const workers = Array.from({ length: CHUNK_CONCURRENCY }, () => worker());
  await Promise.all(workers);

  if (parentSignal.aborted) {
    throw new DOMException("Upload cancelled", "AbortError");
  }
  if (state.failed) {
    throw state.failed;
  }

  return state.nextChunkNumber;
}

// Uploads ONE claimed byte range with its own timeout/retry handling,
// independent of what the other 3 workers are doing concurrently.
async function uploadClaimWithRetry(
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
  state: ChunkClaimState,
  parentSignal: AbortSignal
): Promise<boolean> {
  let currentChunk = args.chunk;
  let shrinkAttempts = 0;
  let retryAttempts = 0;

  while (true) {
    if (parentSignal.aborted || state.failed) return false;

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
      state.onChunkSuccess(currentChunk.size, performance.now() - startedAt);
      return true;
    } catch (error) {
      clearTimeout(timeoutId);
      parentSignal.removeEventListener("abort", onParentAbort);

      if (parentSignal.aborted) return false;

      const timedOut = chunkController.signal.reason === "timeout";

      if (timedOut) {
        shrinkAttempts += 1;
        state.onChunkTimeout(); // shrinks the SHARED size for future claims too
        if (shrinkAttempts > TIMEOUT_MAX_SHRINKS) {
          state.failed = new Error(
            `Chunk ${args.chunkNumber} kept timing out even after shrinking ${TIMEOUT_MAX_SHRINKS} times.`
          );
          return false;
        }
        const newSize = clampChunkSize(currentChunk.size / 2, args.maxChunkSize);
        // Re-slice THIS worker's in-flight attempt too — not just the
        // shared estimate — so the retry itself uses the smaller size
        // rather than resending the same oversized chunk.
        currentChunk = currentChunk.slice(0, newSize);
        console.warn(`Chunk ${args.chunkNumber} timed out after ${CHUNK_TIMEOUT_MS}ms; retrying at ${newSize} bytes.`);
        continue;
      }

      retryAttempts += 1;
      console.warn(`Chunk ${args.chunkNumber} failed (attempt ${retryAttempts}/${MAX_RETRIES})`);
      if (retryAttempts >= MAX_RETRIES) {
        state.failed = error instanceof Error ? error : new Error(`Chunk ${args.chunkNumber} failed after ${MAX_RETRIES} attempts`);
        return false;
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