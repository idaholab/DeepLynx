// app/(home)/(routes)/data_catalog/all_records/page.tsx
import { cookies } from "next/headers";
import { ProjectResponseDto } from "../../types/responseDTOs";
import { RecordTableRow } from "../../types/types";
import AllRecordsClient from "./AllRecordsClient";
import { getAllProjectsServer } from "@/app/lib/server_service/projects_services.server";
import { auth } from "../../../../../auth";
import { fullTextSearchServer, getMultiProjectRecordsServer } from "@/app/lib/server_service/query_services.server";
import { mapDtoToRecordTableRow } from "./mapDtoToRecordTableRow";

export default async function Page({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const params = await searchParams;
  const fromProject =
    typeof params.fromProject === "string" ? params.fromProject : "";
  const initialSearch = typeof params.search === "string" ? params.search : "";
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

  // Keep SSR for projects (fast initial render, no client flash)
  const projects = (await getAllProjectsServer(
    organizationId as number,
  )) as ProjectResponseDto[];
  const initialProjects = projects.map((p) => ({
    id: String(p.id),
    name: p.name,
  }));
  const projArray = initialProjects.map(proj => Number(proj.id))

  const initialRecordsDto = initialSearch ? await fullTextSearchServer(
    Number(organizationId),
    initialSearch,
    projArray,
  ) : await getMultiProjectRecordsServer(Number(organizationId), projArray, false);
  ;

  const initialRecords: RecordTableRow[] = initialRecordsDto.map(mapDtoToRecordTableRow);

  // Let the client fetch records after mount based on the dropdown selection
  const initialSelectedProjects = fromProject ? [fromProject] : [];

  return (
    <AllRecordsClient
      initialProjects={initialProjects}
      initialSelectedProjects={initialSelectedProjects}
      initialSearchTerm={initialSearch}
      initialRecords={initialRecords}
    />
  );
}
