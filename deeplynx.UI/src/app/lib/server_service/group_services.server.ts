// src/app/lib/group_services.server.ts

import "server-only"
import { GroupResponseDto, PaginatedResponse } from "../../(home)/types/responseDTOs";
import { apiFetch, asJson } from "./api.server";

export async function getAllGroupsServer(
    organizationId: number,
    hideArchived: boolean = true,
    pageNumber: number = 1,
    pageSize: number = -1
): Promise<PaginatedResponse<GroupResponseDto>> {
    const params = new URLSearchParams();
    params.append('hideArchived', String(hideArchived));
    params.append('pageNumber', String(pageNumber));
    params.append('pageSize', String(pageSize));

    const res = await apiFetch(
        `/organizations/${organizationId}/groups?${params.toString()}`
    );
    return asJson<PaginatedResponse<GroupResponseDto>>(res);
}