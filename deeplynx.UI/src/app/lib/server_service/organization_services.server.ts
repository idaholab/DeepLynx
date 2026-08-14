// src/app/lib/projects_services.server.ts
import "server-only";
import { OrganizationResponseDto, PaginatedResponse } from "../../(home)/types/responseDTOs";
import { apiFetch, asJson } from "./api.server";

export async function getAllOrganizationsServer(
    hideArchived: boolean = true,
    pageNumber: number = 1,
    pageSize: number = -1
): Promise<PaginatedResponse<OrganizationResponseDto>> {
    const params = new URLSearchParams();
    params.append('hideArchived', String(hideArchived));
    params.append('pageNumber', String(pageNumber));
    params.append('pageSize', String(pageSize));

    const res = await apiFetch(`/organizations?${params.toString()}`);
    return asJson<PaginatedResponse<OrganizationResponseDto>>(res);
}

export async function getAllOrganizationsForUserServer(
    hideArchived: boolean = true,
    pageNumber: number = 1,
    pageSize: number = -1
): Promise<PaginatedResponse<OrganizationResponseDto>> {
    const params = new URLSearchParams();
    params.append('hideArchived', String(hideArchived));
    params.append('pageNumber', String(pageNumber));
    params.append('pageSize', String(pageSize));

    const res = await apiFetch(`/organizations/user?${params.toString()}`);
    return asJson<PaginatedResponse<OrganizationResponseDto>>(res);
}

export async function getOrganizationServer(
    organizationId: number,
    hideArchived: boolean = true
): Promise<OrganizationResponseDto> {
    const params = new URLSearchParams();
    params.append('hideArchived', String(hideArchived));

    const res = await apiFetch(`/organizations/${organizationId}?${params.toString()}`);
    return asJson<OrganizationResponseDto>(res);
}