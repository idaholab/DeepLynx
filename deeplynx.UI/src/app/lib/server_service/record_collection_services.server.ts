// src/app/lib/server_service/record_collection_services.server.ts
import "server-only";
import { apiFetch, asJson } from "./api.server";
import {
    RecordCollectionResponseDto,
    RecordResponseDto,
} from "@/app/(home)/types/responseDTOs";

export async function getAllRecordCollectionsServer(
    organizationId: number,
    projectId: number,
    hideArchived = true,
): Promise<RecordCollectionResponseDto[]> {
    const path =
        `/organizations/${organizationId}/projects/${projectId}/record-collections` +
        `?hideArchived=${hideArchived}`;

    const res = await apiFetch(path);
    return asJson<RecordCollectionResponseDto[]>(res);
}

export async function getRecordsInRecordCollectionServer(
    organizationId: number,
    projectId: number,
    recordCollectionId: number,
    hideArchived = true,
): Promise<RecordResponseDto[]> {
    const path =
        `/organizations/${organizationId}/projects/${projectId}/record-collections/${recordCollectionId}/records` +
        `?hideArchived=${hideArchived}`;

    const res = await apiFetch(path);
    return asJson<RecordResponseDto[]>(res);
}
