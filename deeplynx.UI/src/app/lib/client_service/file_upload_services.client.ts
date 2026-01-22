// src/app/lib/file_upload_services.client.ts

import { RecordResponseDto } from "@/app/(home)/types/responseDTOs";
import { UploadFileArgs, ChunkedUploadSession, ChunkUploadOptions, ChunkedUploadOptions, UploadProgressEvent } from "@/app/(home)/types/types";
import api from "./api";

// ============================================================================
// CONSTANTS
// ============================================================================

const CHUNK_THRESHOLD = 500 * 1024 * 1024; // 500MB threshold to determine regular or chunked upload
const MAX_CONCURRENT_CHUNKS = 4;            // Upload 4 chunks simultaneously
const MAX_RETRIES = 3;                      // Retry failed chunks up to 3 times

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
    args: Omit<UploadFileArgs, "file"> & { files: File[] }
) {
    const { files, ...rest } = args;
    return Promise.allSettled(files.map((file) => uploadFile({ ...rest, file })));
}

// Store AbortController for cancellation
let currentUploadAbortController: AbortController | null = null;

/**
 * Cancel ongoing upload
 */
export function cancelCurrentUpload(): void {
  if (currentUploadAbortController) {
    currentUploadAbortController.abort();
    currentUploadAbortController = null;
    console.log("Upload cancelled by user");
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
}: UploadFileArgs) {
    const form = new FormData();
    form.append("file", file, file.name ?? "upload.bin");
    
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
  }: UploadFileArgs) {
  let uploadId: string | null = null;
  currentUploadAbortController = new AbortController();

  try {
    const session = await startChunkedUpload({
      organizationId,
      projectId,
      dataSourceId,
      objectStorageId,
      fileName: file.name,
      fileSize: file.size,
    });

    uploadId = session.uploadId;

    const chunks = splitFileIntoChunks(file, session.chunkSize);

    // Verify the chunk count matches backend expectation
    if (chunks.length !== session.totalChunks) {
      console.warn(
          `Chunk count mismatch: frontend calculated ${chunks.length}, ` +
          `backend expected ${session.totalChunks}. Using backend value.`
      );
    }

    await uploadChunksInBatches(chunks, uploadId, {
      organizationId,
      projectId,
      dataSourceId,
      objectStorageId,
    }, onProgress);

    const result = await completeChunkedUpload({
      organizationId,
      projectId,
      dataSourceId,
      objectStorageId,
      uploadId,
      fileName: file.name,
      totalChunks: session.totalChunks,
    });

    onProgress?.({
      percentComplete: 100,
      chunksCompleted: chunks.length,
      totalChunks: chunks.length,
      currentBatch: Math.ceil(chunks.length / MAX_CONCURRENT_CHUNKS),
      uploadId,
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
    currentUploadAbortController = null;
  }
}

async function startChunkedUpload(
  options: ChunkedUploadOptions
): Promise<ChunkedUploadSession> {
  const { organizationId, projectId, fileName, fileSize, dataSourceId, objectStorageId } = options;

  const params: Record<string, number | string> = {};
  if (dataSourceId != null) params.dataSourceId = dataSourceId;
  if (objectStorageId != null) params.objectStorageId = objectStorageId;

  const { data } = await api.post<ChunkedUploadSession>(
    `/organizations/${organizationId}/projects/${projectId}/files/upload/start`,
    { fileName, fileSize },
    { params }
  );

  console.log(`Started chunked upload: ${data.uploadId} (${data.totalChunks} chunks)`);

  return data;
}

function splitFileIntoChunks(file: File, chunkSize: number): Blob[] {
  const chunks: Blob[] = [];
  let offset = 0;

  while (offset < file.size) {
    const end = Math.min(offset + chunkSize, file.size);
    chunks.push(file.slice(offset, end));
    offset = end;
  }

  return chunks;
}

async function uploadChunksInBatches(
    chunks: Blob[],
    uploadId: string,
    options: {
      organizationId: number | string;
      projectId: number | string;
      dataSourceId?: number | string;
      objectStorageId?: number | string;
    },
    onProgress?: (progress: UploadProgressEvent) => void
) {
    let chunksCompleted = 0;
    const totalChunks = chunks.length;

    for (let i = 0; i < chunks.length; i += MAX_CONCURRENT_CHUNKS) {

      // CHECK IF ABORTED
      if (currentUploadAbortController?.signal.aborted) {
        throw new DOMException('Upload cancelled', 'AbortError');
      }
      const batch = chunks.slice(i, i + MAX_CONCURRENT_CHUNKS);
      const currentBatch = Math.floor(i / MAX_CONCURRENT_CHUNKS) + 1;
  
      // Upload batch in parallel
      const batchPromises = batch.map((chunk, batchIndex) => {
        const chunkNumber = i + batchIndex;
        return uploadSingleChunk({
          ...options,
          uploadId,
          chunk,
          chunkNumber,
        });
      });
  
      // Wait for batch to complete
      await Promise.all(batchPromises);
  
      // Update after batch completes
      chunksCompleted += batch.length;
      const percentComplete = (chunksCompleted / totalChunks) * 100;
  
      onProgress?.({
        percentComplete: Math.round(percentComplete * 10) / 10,
        chunksCompleted,
        totalChunks,
        currentBatch,
        uploadId,
      });
  
      console.log(
        `Uploaded batch ${currentBatch}: ${chunksCompleted} / ${totalChunks} chunks (${percentComplete.toFixed(1)}%)`
      );
    }
}

async function uploadSingleChunk(options: ChunkUploadOptions): Promise<void> {
  const { organizationId, projectId, dataSourceId, objectStorageId, uploadId, chunk, chunkNumber } = options;

  for (let attempt = 1; attempt <= MAX_RETRIES; attempt++) {
    if (currentUploadAbortController?.signal.aborted) {
      throw new DOMException('Upload cancelled', 'AbortError');
    }
    try {
      const form = new FormData();
      form.append("chunk", chunk);
      form.append("uploadId", uploadId);
      form.append("chunkNumber", String(chunkNumber));

      const params: Record<string, number | string> = {};
      if (dataSourceId != null) params.dataSourceId = dataSourceId;
      if (objectStorageId != null) params.objectStorageId = objectStorageId;

      await api.post(
          `/organizations/${organizationId}/projects/${projectId}/files/upload/chunk`,
          form,
          {
            params,
            signal: currentUploadAbortController?.signal,
          }
      );

      return;
    } catch (error) {
      if (currentUploadAbortController?.signal.aborted) {
        throw new DOMException('Upload cancelled', 'AbortError');
      }

      if (error instanceof DOMException && error.name === 'AbortError') {
        throw error;
      }

      console.warn(`Chunk ${chunkNumber} failed (attempt ${attempt}/${MAX_RETRIES})`);

      if (attempt === MAX_RETRIES) {
        throw new Error(`Chunk ${chunkNumber} failed after ${MAX_RETRIES} attempts`);
      }

      await sleep(1000 * attempt);
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
}): Promise<RecordResponseDto> {
  const { organizationId, projectId, dataSourceId, objectStorageId, uploadId, fileName, totalChunks } = options;

  const params: Record<string, number | string> = {};
  if (dataSourceId != null) params.dataSourceId = dataSourceId;
  if (objectStorageId != null) params.objectStorageId = objectStorageId;

  const { data } = await api.post<RecordResponseDto>(
    `/organizations/${organizationId}/projects/${projectId}/files/upload/complete`,
    { uploadId, fileName, totalChunks },
    { params }
  );

  console.log(`Completed chunked upload: ${uploadId}`);

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

    console.log(`Cancelled chunked upload: ${uploadId}`);
}

// ============================================================================
// UTILITIES
// ============================================================================

function sleep(ms: number): Promise<void> {
    return new Promise((resolve) => setTimeout(resolve, ms));
}