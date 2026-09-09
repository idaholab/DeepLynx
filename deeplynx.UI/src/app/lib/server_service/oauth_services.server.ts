// src/app/lib/oauth_services.server.ts
import "server-only";
import { OauthApplicationResponseDto, PaginatedResponse } from "../../(home)/types/responseDTOs";
import { apiFetch, asJson } from "./api.server";

export async function getAllOauthApplicationsServer(
    pageNumber: number = 1,
    pageSize: number = -1,
    hideArchived: boolean = true
): Promise<PaginatedResponse<OauthApplicationResponseDto>> {
    const params = new URLSearchParams();
    params.append('pageNumber', String(pageNumber));
    params.append('pageSize', String(pageSize));
    params.append('hideArchived', String(hideArchived));

    const res = await apiFetch(`/oauth/applications?${params.toString()}`);
    return asJson<PaginatedResponse<OauthApplicationResponseDto>>(res);
}