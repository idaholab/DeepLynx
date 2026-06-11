// src/app/lib/server_service/record_collection_services.server.ts
import "server-only";
import { apiFetch, asJson } from "./api.server";
import { RecordCollectionResponseDto } from "@/app/(home)/types/responseDTOs";

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