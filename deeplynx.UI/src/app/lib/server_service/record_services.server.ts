// src/app/lib/record_services.server.ts
import "server-only";
import { PaginatedResponse, RecordResponseDto } from "../../(home)/types/responseDTOs";
import { UpdateRecordRequestDto } from "../../(home)/types/requestDTOs";
import { apiErrorFromResponse } from "../api-error";
import { backendApiUrl } from "./backend-api-url.server";

const SERVICE_TOKEN = process.env.SERVICE_TOKEN || "";

/** Always return a HeadersInit (avoid union types) */
function authHeaders(): HeadersInit {
  const h: Record<string, string> = {};
  if (SERVICE_TOKEN) h.Authorization = `Bearer ${SERVICE_TOKEN}`;
  return h;
}

async function asJson<T>(res: Response): Promise<T> {
  if (!res.ok) throw await apiErrorFromResponse(res);
  return (await res.json()) as T;
}

/** ===== Server-safe calls (no browser cookies; safe in build/prerender) ===== */

export async function getAllRecordsServer(
  organizationId: number,
  projectId: number,
  dataSourceId?: number,
  fileType?: string,
  hideArchived: boolean = true,
  pageNumber: number = 1,
  pageSize: number = -1
): Promise<PaginatedResponse<RecordResponseDto>> {
  const params = new URLSearchParams();
  if (dataSourceId !== undefined) {
    params.append("dataSourceId", String(dataSourceId));
  }
  if (fileType) params.append('fileType', fileType);
  params.append('hideArchived', String(hideArchived));
  params.append('pageNumber', String(pageNumber));
  params.append('pageSize', String(pageSize));

  const res = await fetch(
    backendApiUrl(
      `organizations/${organizationId}/projects/${projectId}/records?${params.toString()}`,
    ),
    {
      headers: authHeaders(),
      cache: "no-store",
    }
  );
  return asJson<PaginatedResponse<RecordResponseDto>>(res);
}

export async function getRecordServer(
  organizationId: number,
  projectId: number,
  recordId: number,
  hideArchived: boolean = true
): Promise<RecordResponseDto> {
  const params = new URLSearchParams();
  params.append('hideArchived', String(hideArchived));

  const res = await fetch(
    backendApiUrl(
      `organizations/${organizationId}/projects/${projectId}/records/${recordId}?${params.toString()}`,
    ),
    {
      headers: authHeaders(),
      cache: "no-store",
    }
  );
  return asJson<RecordResponseDto>(res);
}

export async function getRecordsByTagsServer(
  organizationId: number,
  projectId: number,
  tagIds: number[],
  hideArchived: boolean = true,
  pageNumber: number = 1,
  pageSize: number = -1
): Promise<PaginatedResponse<RecordResponseDto>> {
  const params = new URLSearchParams();
  tagIds.forEach((tagId) => params.append("tagIds", tagId.toString()));
  params.append("hideArchived", hideArchived.toString());
  params.append("pageNumber", pageNumber.toString());
  params.append("pageSize", pageSize.toString());

  const res = await fetch(
    backendApiUrl(
      `organizations/${organizationId}/projects/${projectId}/records/by-tags?${params.toString()}`,
    ),
    {
      headers: authHeaders(),
      cache: "no-store",
    }
  );
  return asJson<PaginatedResponse<RecordResponseDto>>(res);
}

export async function updateRecordServer(
  organizationId: number,
  projectId: number,
  recordId: number,
  updateData: UpdateRecordRequestDto
): Promise<RecordResponseDto> {
  const res = await fetch(
    backendApiUrl(
      `organizations/${organizationId}/projects/${projectId}/records/${recordId}`,
    ),
    {
      method: "PUT",
      headers: { ...authHeaders(), "Content-Type": "application/json" },
      body: JSON.stringify(updateData),
      cache: "no-store",
    }
  );
  return asJson<RecordResponseDto>(res);
}

export async function deleteRecordServer(
  organizationId: number,
  projectId: number,
  recordId: number
): Promise<{ message: string }> {
  const res = await fetch(
    backendApiUrl(
      `organizations/${organizationId}/projects/${projectId}/records/${recordId}`,
    ),
    {
      method: "DELETE",
      headers: authHeaders(),
      cache: "no-store",
    }
  );
  return asJson<{ message: string }>(res);
}

export async function archiveRecordServer(
  organizationId: number,
  projectId: number,
  recordId: number,
  archive: boolean
): Promise<{ message: string }> {
  const params = new URLSearchParams();
  params.append('archive', String(archive));

  const res = await fetch(
    backendApiUrl(
      `organizations/${organizationId}/projects/${projectId}/records/${recordId}?${params.toString()}`,
    ),
    {
      method: "PATCH",
      headers: authHeaders(),
      cache: "no-store",
    }
  );
  return asJson<{ message: string }>(res);
}
