// app/(home)/(routes)/data_catalog/page.tsx
import { cookies } from "next/headers";
import { auth } from "../../../../auth";
import { ProjectResponseDto } from "../types/responseDTOs";
import { RecordTableRow } from "../types/types";
import { getAllProjectsServer } from "@/app/lib/server_service/projects_services.server";
import TimeseriesViewerClient from "./TimeseriesViewerClient";

export default async function Page({
    searchParams,
}: {
    searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
    const params = await searchParams;
    const fromProject =
        typeof params.fromProject === "string" ? params.fromProject : "";

    // Get organization ID - prioritize cookie over session for real-time updates
    const cookieStore = await cookies();
    const orgSessionCookie = cookieStore.get("organizationSession");

    let organizationId: number | undefined;

    if (orgSessionCookie) {
        try {
            const orgSession = JSON.parse(orgSessionCookie.value);
            organizationId = orgSession.organizationId;
        } catch (e) {
            console.error("Failed to parse organization cookie:", e);
            // Fallback to session if cookie parsing fails
            const session = await auth();
            organizationId = session?.user?.organizationId;
        }
    } else {
        // No cookie, fallback to session
        const session = await auth();
        organizationId = session?.user?.organizationId;
    }

    // Fetch projects filtered by organization
    const projects = (await getAllProjectsServer(
        organizationId as number
    )) as ProjectResponseDto[];
    const initialProjects = projects.map((p) => ({
        id: String(p.id),
        name: p.name,
    }));

    const initialSelectedProjects = fromProject ? [fromProject] : [];

    return (
        <TimeseriesViewerClient
            initialProjects={initialProjects}
            initialSelectedProjects={initialSelectedProjects}
        />
    );
}
