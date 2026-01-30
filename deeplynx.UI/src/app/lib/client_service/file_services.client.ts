'use client';

import api from './api';
import axios from 'axios';
import { RecordResponseDto } from '../../(home)/types/responseDTOs';


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
 * Download a file
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param recordId - The ID of the record containing the file
 * @param recordName - Optional name for the downloaded file
 * @returns Promise that resolves when download completes
 */
export const downloadFile = async (
  organizationId: number,
  projectId: number,
  recordId: number,
  recordName?: string | null
): Promise<void> => {
  let url: string | null = null;
  try {
    const res = await api.get(
      `/organizations/${organizationId}/projects/${projectId}/files/${recordId}`,
      { responseType: 'blob' }
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
  } catch (err: unknown) {
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
  } finally {
    if (url) URL.revokeObjectURL(url);
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
