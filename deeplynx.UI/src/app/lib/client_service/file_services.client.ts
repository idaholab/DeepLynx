'use client';

import api from './api';
import axios from 'axios';
import { RecordResponseDto } from '../../(home)/types/responseDTOs';
import { getRecord } from './record_services.client';
import { getProjectObjectStorage } from './object_storage_services.client';


const MIME_EXT: Record<string, string> = {
  'application/pdf': 'pdf',
  'application/zip': 'zip',
  'image/png': 'png',
  'image/jpeg': 'jpg',
  'image/gif': 'gif',
  'text/plain': 'txt',
};

function parseFilenameFromCD(cd?: string): string | undefined {
  if (!cd) return;
  const match =
    cd.match(/filename\*?=(?:UTF-8''|")?([^";]+)/i) ??
    cd.match(/filename="?([^"]+)"?/i);
  return match?.[1] ? decodeURIComponent(match[1]) : undefined;
}

function hasExtension(name: string): boolean {
  return /\.[A-Za-z0-9]{2,8}$/.test(name);
}

function sanitizeFilename(name: string): string {
  return name.replace(/[<>:"/\\|?*\x00-\x1F]/g, '_');
}


/**
 * Check if storage type uses pre-signed URL download method
 */
export const isPresignedUrlStorage = (storageType: string): boolean => {
  return storageType === 'azure_object' || storageType === 'aws_s3';
};


/**
 * Get the storage type for a given record
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param recordId - The ID of the record containing the file
 * @returns Promise with the storage type string
 */
export const getStorageType = async (
  organizationId: number,
  projectId: number,
  recordId: number
): Promise<string> => {
  // Fetch the record to get the objectStorageId
  const record = await getRecord(
    organizationId,
    projectId,
    recordId,
    true // hideArchived
  );

  if (!record.objectStorageId) {
    throw new Error('Record does not have an associated object storage');
  }

  // Fetch the object storage to get its type
  const objectStorage = await getProjectObjectStorage(
    organizationId,
    projectId,
    record.objectStorageId,
    true // hideArchived
  );

  return objectStorage.type;
};


/**
 * Download a file via pre-signed URL (browser native download - no memory constraints)
 * Used for Azure and AWS S3 storage types
 */
const downloadViaPresignedUrl = async (
  organizationId: number,
  projectId: number,
  recordId: number,
  recordName?: string | null,
  abortController?: AbortController
): Promise<void> => {
  // Get the SAS/pre-signed URL from backend
  const sasUrlResponse = await api.get<string>(
    `/organizations/${organizationId}/projects/${projectId}/files/${recordId}/url`,
    {
      signal: abortController?.signal,
    }
  );

  const sasUrl = sasUrlResponse.data;

  if (!sasUrl || typeof sasUrl !== 'string') {
    throw new Error('Invalid pre-signed URL received from server');
  }

  // Trigger native browser download
  const a = document.createElement('a');
  a.href = sasUrl;

  if (recordName) {
    a.download = sanitizeFilename(recordName);
  }

  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
};


/**
 * Download a file via blob with progress tracking
 * Used for other storage types (non-Azure, non-AWS S3)
 */
const downloadViaBlob = async (
  organizationId: number,
  projectId: number,
  recordId: number,
  recordName?: string | null,
  onProgress?: (progress: { loaded: number; total: number; percentage: number }) => void,
  abortController?: AbortController
): Promise<void> => {
  let url: string | null = null;
  try {
    const res = await api.get(
      `/organizations/${organizationId}/projects/${projectId}/files/${recordId}`,
      {
        responseType: 'blob',
        signal: abortController?.signal,
        onDownloadProgress: (progressEvent) => {
          if (onProgress && progressEvent.total) {
            const loaded = progressEvent.loaded;
            const total = progressEvent.total;
            const percentage = Math.round((loaded / total) * 100);

            onProgress({ loaded, total, percentage });
          }
        }
      }
    );

    const blob = res.data as Blob;

    let filename =
      parseFilenameFromCD(res.headers['content-disposition']) ||
      recordName?.trim() ||
      'file';

    if (!hasExtension(filename)) {
      const ext = MIME_EXT[blob.type];
      if (ext) filename += `.${ext}`;
    }

    filename = sanitizeFilename(filename);

    url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    a.remove();
  } finally {
    if (url) URL.revokeObjectURL(url);
  }
};

const downloadAppendedFile = async (
  organizationId: number,
  projectId: number,
  recordId: number,
  recordName?: string | null,
  onProgress?: (progress: { loaded: number; total: number; percentage: number; }) => void,
  abortController?: AbortController
): Promise<void> => {
  let url: string | null = null;
  try {
    const res = await api.get(
      `/organizations/${organizationId}/projects/${projectId}/files/${recordId}/appended`,
      {
        responseType: 'blob',
        signal: abortController?.signal,
        onDownloadProgress: (progressEvent) => {
          const loaded = progressEvent.loaded;
          const total = 0;
          const percentage = 0;

          if (onProgress) {
            onProgress({ loaded, total, percentage });
          }
        },
      }
    );

    const blob = res.data as Blob;

    let filename =
      parseFilenameFromCD(res.headers['content-disposition']) ||
      recordName?.trim() ||
      'folder.zip';

    if (!filename.toLowerCase().endsWith('.zip')) {
      filename += '.zip';
    }

    filename = sanitizeFilename(filename);

    url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    a.remove();
  } finally {
    if (url) URL.revokeObjectURL(url);
  }
};


/**
 * Update a file
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param recordId - The ID of the record containing the file
 * @param file - The new file to replace the old one
 * @returns Promise with RecordResponseDto containing updated file information
 */
export const updateFile = async (
  organizationId: number,
  projectId: number,
  recordId: number,
  file: File
): Promise<RecordResponseDto> => {
  try {
    const formData = new FormData();
    formData.append('file', file);

    const res = await api.put(
      `/organizations/${organizationId}/projects/${projectId}/files/${recordId}`,
      formData,
      { headers: { 'Content-Type': 'multipart/form-data' } }
    );
    return res.data;
  } catch (error) {
    console.error(`Error updating file in record ${recordId}:`, error);
    throw error;
  }
};


/**
 * Download a file using the appropriate method based on storage type
 * - For Azure Blob and AWS S3: Uses pre-signed URL (browser native download, no memory constraints)
 * - For other storage types: Uses blob download with progress tracking
 * 
 * This function automatically:
 * 1. Fetches the record to get the objectStorageId
 * 2. Fetches the object storage to get its type
 * 3. Routes to the appropriate download method
 * 
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param recordId - The ID of the record containing the file
 * @param recordName - Optional name for the downloaded file
 * @param onProgress - Optional callback for progress updates (only used for blob downloads)
 * @param abortController - Optional abort controller for download cancelation
 * @returns Promise that resolves when download starts/completes
 */
export const downloadFile = async (
  organizationId: number,
  projectId: number,
  recordId: number,
  recordName?: string | null,
  onProgress?: (progress: { loaded: number; total: number; percentage: number }) => void,
  abortController?: AbortController
): Promise<void> => {
  try {
    // Step 1: Fetch the record to get the objectStorageId
    const record = await getRecord(
      organizationId,
      projectId,
      recordId,
      true // hideArchived
    );

    if (!record.objectStorageId) {
      throw new Error('Record does not have an associated object storage');
    }

    // Step 2: Fetch the object storage to get its type
    const objectStorage = await getProjectObjectStorage(
      organizationId,
      projectId,
      record.objectStorageId,
      true // hideArchived
    );

    // Determine if folder or file by Uri trailing slash or extension
    const isFolder = record.uri?.endsWith('/');

    if (isFolder) {
      await downloadAppendedFile(
        organizationId,
        projectId,
        recordId,
        recordName,
        onProgress,
        abortController
      );
    } else {
      if (isPresignedUrlStorage(objectStorage.type)) {
        await downloadViaPresignedUrl(
          organizationId,
          projectId,
          recordId,
          recordName,
          abortController
        );
      } else {
        await downloadViaBlob(
          organizationId,
          projectId,
          recordId,
          recordName,
          onProgress,
          abortController
        );
      }
    }
  } catch (err: unknown) {
    // Check if it's a cancellation (user aborted)
    if (axios.isAxiosError(err) && err.code === 'ERR_CANCELED') {
      throw err;
    }

    // For blob downloads, try to extract error message from blob response
    if (axios.isAxiosError(err)) {
      const { response } = err;
      if (response?.data instanceof Blob) {
        try {
          const text = await response.data.text();
          console.error('Download failed:', response.status, text || err.message);
        } catch {
          console.error('Download failed:', response.status, err.message);
        }
      } else {
        console.error('Download failed:', response?.status, err.message);
      }
    } else {
      console.error('Download failed:', err);
    }

    throw err;
  }
};


/**
 * Delete a file
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param recordId - The ID of the record containing the file
 * @returns Promise with success message
 */
export const deleteFile = async (
  organizationId: number,
  projectId: number,
  recordId: number
): Promise<{ message: string }> => {
  try {
    const res = await api.delete(
      `/organizations/${organizationId}/projects/${projectId}/files/${recordId}`
    );
    return res.data;
  } catch (error) {
    console.error(`Error deleting file in record ${recordId}:`, error);
    throw error;
  }
};