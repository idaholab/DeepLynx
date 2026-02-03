// app/(home)/(routes)/timeseries_viewer/page.tsx
import { cookies } from "next/headers";
import { auth } from "../../../../auth";
import { HistoricalRecordResponseDto, ProjectResponseDto } from "../types/responseDTOs";
import { RecordTableRow } from "../types/types";
import { getAllProjectsServer } from "@/app/lib/server_service/projects_services.server";
import TimeseriesViewerClient from "./TimeseriesViewerClient";
import { queryBuilder } from "@/app/lib/client_service/query_services.client";
import { redirect } from "next/navigation";
import { CustomQueryRequestDto } from "../types/requestDTOs";
import { useProjectResources } from "../upload_center/hooks/useProjectResources";
import { queryBuilderServer } from "@/app/lib/server_service/query_services.server";

export default async function Page({
    searchParams,
}: {
    searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
    const params = await searchParams;
    // Get organization from cookies
    const cookieStore = await cookies();
    const orgSessionCookie = cookieStore.get("organizationSession");

    if (!orgSessionCookie) {
        redirect("/select-org");
    }

    let organizationId: string | number | undefined;
    try {
        const orgSession = JSON.parse(orgSessionCookie.value);
        organizationId = orgSession.organizationId;
    } catch (e) {
        console.error("Failed to parse organization session:", e);
        redirect("/select-org");
    }

    // Get initial project from project session cookie
    const projectSessionCookie = cookieStore.get("projectSession");
    let projectId: number | undefined;

    if (projectSessionCookie) {
        try {
            const projectSession = JSON.parse(projectSessionCookie.value);
            projectId = projectSession.projectId;
        } catch (e) {
            console.error("Failed to parse project session cookie:", e);
        }
    }

    //Using query builder to grab Timeseries files for now
    const dto: CustomQueryRequestDto = {
        filter: "class_name",
        operator: '=',
        value: "Timeseries"
    }

    let availableFiles: HistoricalRecordResponseDto[] = [];

    try {
        const result = await queryBuilderServer(
            Number(organizationId),
            [dto],
            [Number(projectId)]
        );
        availableFiles = result;
    } catch (err) {
        console.error("Failed to grab timeseries files:", err);
    }

    return (
        <TimeseriesViewerClient
            timeseriesFiles={availableFiles}
        />
    );
}
