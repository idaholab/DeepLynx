"use client";

import {
  CreateRecordCollectionRequestDto,
  RecordCollectionQueryRequestDto,
  UpdateRecordCollectionRequestDto,
} from "@/app/(home)/types/requestDTOs";
import {
  PaginatedRecordCollectionsResponseDto,
  RecordCollectionResponseDto,
  RecordResponseDto,
} from "@/app/(home)/types/responseDTOs";
import api from "./api";

const recordCollectionsPath = (organizationId: number, projectId: number) =>
  `/organizations/${organizationId}/projects/${projectId}/record-collections`;

const buildSensitivityLabelParams = (sensitivityLabelIds?: number[]) => {
  const params = new URLSearchParams();
  sensitivityLabelIds?.forEach((id) =>
    params.append("sensitivityLabelIds", String(id)),
  );
  return params;
};

const buildRecordCollectionQueryParams = (
  dto?: RecordCollectionQueryRequestDto,
  hideArchived: boolean = true,
) => {
  const params = new URLSearchParams();
  params.set("hideArchived", String(hideArchived));

  if (dto?.search?.trim()) {
    params.set("search", dto.search.trim());
  }

  dto?.sensitivityLabelIds?.forEach((id) =>
    params.append("sensitivityLabelIds", String(id)),
  );
  dto?.tagIds?.forEach((id) => params.append("tagIds", String(id)));

  if (dto?.sort) {
    params.set("sort", dto.sort);
  }

  if (dto?.pageNumber) {
    params.set("pageNumber", String(dto.pageNumber));
  }

  if (dto?.pageSize) {
    params.set("pageSize", String(dto.pageSize));
  }

  return params;
};

const normalizeRecordCollectionsPage = (
  data: unknown,
): PaginatedRecordCollectionsResponseDto => {
  if (!data || typeof data !== "object" || Array.isArray(data)) {
    throw new Error(
      "Invalid record collections response: expected paginated object payload",
    );
  }

  const page = data as {
    items?: RecordCollectionResponseDto[];
    pageNumber?: number;
    pageSize?: number;
    totalCount?: number;
    totalPages?: number;
    hasPrevious?: boolean;
    hasNext?: boolean;
    Items?: RecordCollectionResponseDto[];
    PageNumber?: number;
    PageSize?: number;
    TotalCount?: number;
    TotalPages?: number;
    HasPrevious?: boolean;
    HasNext?: boolean;
  };

  const items = page.items ?? page.Items;
  const pageNumber = page.pageNumber ?? page.PageNumber;
  const pageSize = page.pageSize ?? page.PageSize;
  const totalCount = page.totalCount ?? page.TotalCount;
  const totalPages = page.totalPages ?? page.TotalPages;
  const hasPrevious = page.hasPrevious ?? page.HasPrevious;
  const hasNext = page.hasNext ?? page.HasNext;

  if (!Array.isArray(items)) {
    throw new Error(
      "Invalid record collections response: items must be an array",
    );
  }

  if (
    typeof pageNumber !== "number" ||
    typeof pageSize !== "number" ||
    typeof totalCount !== "number" ||
    typeof totalPages !== "number" ||
    typeof hasPrevious !== "boolean" ||
    typeof hasNext !== "boolean"
  ) {
    throw new Error(
      "Invalid record collections response: pagination metadata is malformed",
    );
  }

  return {
    items,
    pageNumber,
    pageSize,
    totalCount,
    totalPages,
    hasPrevious,
    hasNext,
  };
};

export const getAllRecordCollections = async (
  organizationId: number,
  projectId: number,
  dto?: RecordCollectionQueryRequestDto,
  hideArchived: boolean = true,
) : Promise<PaginatedRecordCollectionsResponseDto> => {
  const res = await api.get(recordCollectionsPath(organizationId, projectId), {
    params: buildRecordCollectionQueryParams(dto, hideArchived),
  });
  return normalizeRecordCollectionsPage(res.data);
};

export const createRecordCollection = async (
  organizationId: number,
  projectId: number,
  dto: CreateRecordCollectionRequestDto,
  sensitivityLabelIds?: number[],
): Promise<RecordCollectionResponseDto> => {
  const res = await api.post(
    recordCollectionsPath(organizationId, projectId),
    dto,
    { params: buildSensitivityLabelParams(sensitivityLabelIds) },
  );
  return res.data;
};

export const updateRecordCollection = async (
  organizationId: number,
  projectId: number,
  recordCollectionId: number,
  dto: UpdateRecordCollectionRequestDto,
): Promise<RecordCollectionResponseDto> => {
  const res = await api.put(
    `${recordCollectionsPath(organizationId, projectId)}/${recordCollectionId}`,
    dto,
  );
  return res.data;
};

export const archiveRecordCollection = async (
  organizationId: number,
  projectId: number,
  recordCollectionId: number,
  archive: boolean,
): Promise<{ message: string }> => {
  const res = await api.patch(
    `${recordCollectionsPath(organizationId, projectId)}/${recordCollectionId}`,
    null,
    { params: { archive } },
  );
  return res.data;
};

export const getRecordsInRecordCollection = async (
  organizationId: number,
  projectId: number,
  recordCollectionId: number,
  hideArchived: boolean = true,
): Promise<RecordResponseDto[]> => {
  const res = await api.get(
    `${recordCollectionsPath(organizationId, projectId)}/${recordCollectionId}/records`,
    { params: { hideArchived } },
  );
  return res.data;
};

export const addRecordsToRecordCollection = async (
  organizationId: number,
  projectId: number,
  recordCollectionId: number,
  recordIds: number[],
): Promise<{ message: string }> => {
  const res = await api.post(
    `${recordCollectionsPath(organizationId, projectId)}/${recordCollectionId}/records`,
    recordIds,
  );
  return res.data;
};

export const removeRecordsFromRecordCollection = async (
  organizationId: number,
  projectId: number,
  recordCollectionId: number,
  recordIds: number[],
): Promise<{ message: string }> => {
  const res = await api.put(
    `${recordCollectionsPath(organizationId, projectId)}/${recordCollectionId}/records`,
    recordIds,
  );
  return res.data;
};

export const attachTagToRecordCollection = async (
  organizationId: number,
  projectId: number,
  recordCollectionId: number,
  tagId: number,
): Promise<{ message: string }> => {
  const res = await api.post(
    `${recordCollectionsPath(organizationId, projectId)}/${recordCollectionId}/tags/${tagId}`,
  );
  return res.data;
};

export const unattachTagFromRecordCollection = async (
  organizationId: number,
  projectId: number,
  recordCollectionId: number,
  tagId: number,
): Promise<{ message: string }> => {
  const res = await api.delete(
    `${recordCollectionsPath(organizationId, projectId)}/${recordCollectionId}/tags/${tagId}`,
  );
  return res.data;
};

export const attachSensitivityLabelToRecordCollection = async (
  organizationId: number,
  projectId: number,
  recordCollectionId: number,
  sensitivityLabelId: number,
): Promise<{ message: string }> => {
  const res = await api.post(
    `${recordCollectionsPath(organizationId, projectId)}/${recordCollectionId}/sensitivity-labels/${sensitivityLabelId}`,
  );
  return res.data;
};

export const unattachSensitivityLabelFromRecordCollection = async (
  organizationId: number,
  projectId: number,
  recordCollectionId: number,
  sensitivityLabelId: number,
): Promise<{ message: string }> => {
  const res = await api.delete(
    `${recordCollectionsPath(organizationId, projectId)}/${recordCollectionId}/sensitivity-labels/${sensitivityLabelId}`,
  );
  return res.data;
};
