// src/app/lib/server_service/record_collection_services.server.ts
import "server-only";
import { apiFetch, asJson } from "./api.server";
import {
    PaginatedRecordCollectionsResponseDto,
    RecordCollectionResponseDto,
    RecordResponseDto,
} from "@/app/(home)/types/responseDTOs";
import { RecordCollectionQueryRequestDto } from "@/app/(home)/types/requestDTOs";

const buildRecordCollectionQueryString = (
    dto?: RecordCollectionQueryRequestDto,
    hideArchived: boolean = true,
) => {
    const params = new URLSearchParams();
    params.set("hideArchived", String(hideArchived));

    if (dto?.search?.trim()) {
        params.set("search", dto.search.trim());
    }

    dto?.sensitivityLabelIds?.forEach((id) =>
        params.append("sensitivityLabelIds", id.toString()),
    );
    dto?.tagIds?.forEach((id) => params.append("tagIds", id.toString()));

    if (dto?.sort) {
        params.set("sort", dto.sort);
    }

    if (dto?.pageNumber) {
        params.set("pageNumber", dto.pageNumber.toString());
    }

    if (dto?.pageSize) {
        params.set("pageSize", dto.pageSize.toString());
    }

    return params.toString();
};

function normalizeRecordCollectionsPage(
    data: unknown,
): PaginatedRecordCollectionsResponseDto {
    if (Array.isArray(data)) {
        return {
            items: data as RecordCollectionResponseDto[],
            pageNumber: 1,
            pageSize: data.length,
            totalCount: data.length,
        };
    }

    const page = (data ?? {}) as {
        items?: RecordCollectionResponseDto[];
        pageNumber?: number;
        pageSize?: number;
        totalCount?: number;
        maxPageSize?: number;
        Items?: RecordCollectionResponseDto[];
        PageNumber?: number;
        PageSize?: number;
        TotalCount?: number;
        MaxPageSize?: number;
    };

    const items = page.items ?? page.Items ?? [];
    return {
        items,
        pageNumber: page.pageNumber ?? page.PageNumber ?? 1,
        pageSize: page.pageSize ?? page.PageSize ?? items.length,
        totalCount: page.totalCount ?? page.TotalCount ?? items.length,
        maxPageSize: page.maxPageSize ?? page.MaxPageSize,
    };
}

export async function getAllRecordCollectionsServer(
    organizationId: number,
    projectId: number,
    dto?: RecordCollectionQueryRequestDto,
    hideArchived = true,
): Promise<PaginatedRecordCollectionsResponseDto> {
    const path =
        `/organizations/${organizationId}/projects/${projectId}/record-collections` +
        `?${buildRecordCollectionQueryString(dto, hideArchived)}`;

    const res = await apiFetch(path);
    return normalizeRecordCollectionsPage(await asJson<unknown>(res));
}

export async function getRecordCollectionByIdServer(
    organizationId: number,
    projectId: number,
    recordCollectionId: number,
    hideArchived = true,
): Promise<RecordCollectionResponseDto | null> {
    let pageNumber = 1;

    while (true) {
        const page = await getAllRecordCollectionsServer(
            organizationId,
            projectId,
            { pageNumber, pageSize: 500 },
            hideArchived,
        );

        const collection = page.items.find((item) => item.id === recordCollectionId);
        if (collection) {
            return collection;
        }

        if (pageNumber * page.pageSize >= page.totalCount) {
            return null;
        }

        pageNumber += 1;
    }
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
