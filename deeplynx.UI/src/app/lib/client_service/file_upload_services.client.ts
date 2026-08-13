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
const MAX_CONCURRENT_CHUNKS = 4;            // Upload 4 chunks simultaneously
const MAX_RETRIES = 3;                      // Retry failed chunks up to 3 times
const DEFAULT_MAX_CONCURRENT_FILES = 5;
const MIN_CHUNK_SIZE = 256 * 1024;          // Floor so retries can't shrink to near-zero progress
const INITIAL_CHUNK_SIZE = 4 * 1024 * 1024; // Smaller than ceiling so chunks have room to ramp
const TARGET_CHUNK_DURATION_S = 15;         // Aim for each chunk to take roughly this long
const CHUNK_TIMEOUT_MS = 60_000;            // Per-chunk timeout
const MAX_TIMEOUT_RETRIES = 2;              // Retry the same claimed range at most twice after timeouts
const SIZING_EMA_ALPHA = 0.3;               // Smooths the PER-WORKER sample used to decide next chunk size
const DISPLAY_EMA_ALPHA = 0.35;             // Smooths the AGGREGATE sample used for the user-facing speed
const PROGRESS_TICK_MS = 400;               // How often to sample aggregate bytes for display
const SPEED_STALE_AFTER_MS = 5_000;

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
// CHUNKED UPLOAD (> 500MB) — fixed concurrency (4 workers), adaptive size
// ============================================================================

// Shared state claimed from by all 4 workers for a single file's upload.
class ChunkClaimState {
  cursor = 0;                 // next byte offset not yet claimed
  nextChunkNumber = 0;        // next chunk index not yet claimed
  bytesCompleted = 0;         // sum of bytes from confirmed chunks only
  chunksCompleted = 0;        // number of chunks confirmed by the server
  chunkSize: number;
  sizingThroughputEMA: number | null = null;
  failed: Error | DOMException | null = null;

  constructor(public totalBytes: number, initialChunkSize: number, public maxChunkSize: number) {
    this.chunkSize = clampChunkSize(initialChunkSize, maxChunkSize);
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
    this.chunksCompleted += 1;

    const throughput = bytes / (elapsedMs / 1000);
    this.sizingThroughputEMA =
      this.sizingThroughputEMA == null
        ? throughput
        : SIZING_EMA_ALPHA * throughput +
        (1 - SIZING_EMA_ALPHA) * this.sizingThroughputEMA;

    const desiredChunkSize = clampChunkSize(
      (this.sizingThroughputEMA ?? throughput) * TARGET_CHUNK_DURATION_S,
      this.maxChunkSize,
    );

    // Grow conservatively so one unusually fast sample cannot cause a huge jump.
    this.chunkSize = Math.min(
      desiredChunkSize,
      this.chunkSize * 2,
      this.maxChunkSize,
    );
  }

  onChunkTimeout() {
    // Shared shrink: affects whatever the next claim (by any of the 4 workers) will use.
    this.chunkSize = clampChunkSize(this.chunkSize / 2, this.maxChunkSize);
  }
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

    const maxChunkSize = Math.max(MIN_CHUNK_SIZE, session.chunkSize);
    const initialChunkSize = Math.min(INITIAL_CHUNK_SIZE, maxChunkSize);

    const chunksSent = await uploadChunksConcurrentAdaptive(
      file,
      uploadId,
      initialChunkSize,
      maxChunkSize,
      { organizationId, projectId, dataSourceId, objectStorageId },
      abortController.signal,
      onProgress
    );

    // All bytes have been uploaded. The backend may now take some time
    // to assemble/finalize the uploaded chunks.
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
      isFinalizing: true,
    });

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

async function uploadChunksConcurrentAdaptive(
  file: File,
  uploadId: string,
  initialChunkSize: number,
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
  const state = new ChunkClaimState(file.size, initialChunkSize, maxChunkSize);
  const speedSampler = new AggregateSpeedSampler();

  const reportProgress = (speedBytesPerSec: number) => {
    const remainingBytes = state.totalBytes - state.bytesCompleted;
    const estimatedRemainingChunks = state.chunkSize > 0 ? Math.ceil(remainingBytes / state.chunkSize) : 0;
    onProgress?.({
      percentComplete: Math.round((state.bytesCompleted / state.totalBytes) * 1000) / 10,
      chunksCompleted: state.chunksCompleted,
      totalChunks: state.chunksCompleted + estimatedRemainingChunks,
      currentBatch: state.nextChunkNumber,
      uploadId,
      bytesUploaded: state.bytesCompleted,
      totalBytes: state.totalBytes,
      chunkSize: state.chunkSize,
      speedBytesPerSec,
    });
  };

  const tickInterval = setInterval(() => {
    const speed = speedSampler.sample(state.bytesCompleted);
    reportProgress(speed);
  }, PROGRESS_TICK_MS);

  const worker = async (): Promise<void> => {
    while (true) {
      if (parentSignal.aborted) throw new DOMException("Upload cancelled", "AbortError");
      if (state.failed) return;

      const claim = state.claimNext();
      if (!claim) return;

      const chunk = file.slice(claim.offset, claim.offset + claim.size);
      const success = await uploadClaimWithRetry(
        { ...options, uploadId, chunk, chunkNumber: claim.chunkNumber },
        state,
        parentSignal
      );

      if (!success) return;
    }
  };

  try {
    const workers = Array.from({ length: MAX_CONCURRENT_CHUNKS }, () => worker());
    await Promise.all(workers);
  } finally {
    clearInterval(tickInterval);
  }

  if (parentSignal.aborted) {
    throw new DOMException("Upload cancelled", "AbortError");
  }
  if (state.failed) {
    throw state.failed;
  }

  reportProgress(0);

  return state.nextChunkNumber;
}

// Uploads ONE claimed byte range with its own timeout/retry handling, independent of what the other 3 workers are doing concurrently.
async function uploadClaimWithRetry(
  args: {
    organizationId: number | string;
    projectId: number | string;
    dataSourceId?: number | string;
    objectStorageId?: number | string;
    uploadId: string;
    chunk: Blob;
    chunkNumber: number;
  },
  state: ChunkClaimState,
  parentSignal: AbortSignal
): Promise<boolean> {
  const chunk = args.chunk;
  let timeoutAttempts = 0;
  let retryAttempts = 0;

  while (true) {
    if (parentSignal.aborted || state.failed) return false;

    const chunkController = new AbortController();
    const onParentAbort = () => chunkController.abort("cancelled");
    parentSignal.addEventListener("abort", onParentAbort);
    const timeoutId = setTimeout(
      () => chunkController.abort("timeout"),
      CHUNK_TIMEOUT_MS,
    );

    const startedAt = performance.now();

    try {
      const form = new FormData();
      form.append("chunk", chunk);
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

      state.onChunkSuccess(chunk.size, performance.now() - startedAt);
      return true;
    } catch (error) {
      if (parentSignal.aborted) return false;

      const timedOut = chunkController.signal.reason === "timeout";

      if (timedOut) {
        timeoutAttempts += 1;

        // Only future claims adapt. This already-claimed byte range stays immutable and is 
        // retried with the exact same blob so no bytes can be skipped.
        state.onChunkTimeout();

        console.warn(
          `Chunk ${args.chunkNumber} timed out after ${CHUNK_TIMEOUT_MS}ms; ` +
          `retrying the same ${chunk.size}-byte range. ` +
          `Future chunks will use approximately ${state.chunkSize} bytes.`
        );

        if (timeoutAttempts >= MAX_TIMEOUT_RETRIES) {
          state.failed = new Error(
            `Chunk ${args.chunkNumber} timed out after ${MAX_TIMEOUT_RETRIES} attempts.`
          );
          return false;
        }

        continue;
      }

      retryAttempts += 1;
      console.warn(
        `Chunk ${args.chunkNumber} failed (attempt ${retryAttempts}/${MAX_RETRIES})`
      );

      if (retryAttempts >= MAX_RETRIES) {
        state.failed =
          error instanceof Error
            ? error
            : new Error(
              `Chunk ${args.chunkNumber} failed after ${MAX_RETRIES} attempts`
            );
        return false;
      }

      await sleep(1000 * retryAttempts);
      continue;
    } finally {
      clearTimeout(timeoutId);
      parentSignal.removeEventListener("abort", onParentAbort);
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

class AggregateSpeedSampler {
  private lastBytes = 0;
  private lastTime = performance.now();
  private lastProgressTime = performance.now();
  private smoothedSpeed: number | null = null;

  sample(currentBytes: number): number {
    const now = performance.now();

    if (currentBytes === this.lastBytes) {
      if (
        now - this.lastProgressTime >
        SPEED_STALE_AFTER_MS
      ) {
        return 0;
      }

      return this.smoothedSpeed ?? 0;
    }

    const dtSeconds =
      (now - this.lastTime) / 1000;

    if (dtSeconds <= 0) {
      return this.smoothedSpeed ?? 0;
    }

    const instantSpeed =
      (currentBytes - this.lastBytes) / dtSeconds;

    this.smoothedSpeed =
      this.smoothedSpeed == null
        ? instantSpeed
        : DISPLAY_EMA_ALPHA * instantSpeed +
        (1 - DISPLAY_EMA_ALPHA) *
        this.smoothedSpeed;

    this.lastBytes = currentBytes;
    this.lastTime = now;
    this.lastProgressTime = now;

    return this.smoothedSpeed;
  }
}

function clampChunkSize(size: number, maxChunkSize: number): number {
  return Math.min(maxChunkSize, Math.max(MIN_CHUNK_SIZE, Math.round(size)));
}