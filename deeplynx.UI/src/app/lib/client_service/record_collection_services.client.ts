"use client";

import {
  CreateRecordCollectionRequestDto,
  UpdateRecordCollectionRequestDto,
} from "@/app/(home)/types/requestDTOs";
import {
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

export const getAllRecordCollections = async (
  organizationId: number,
  projectId: number,
  hideArchived: boolean = true,
): Promise<RecordCollectionResponseDto[]> => {
  const res = await api.get(recordCollectionsPath(organizationId, projectId), {
    params: { hideArchived },
  });
  return res.data;
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
