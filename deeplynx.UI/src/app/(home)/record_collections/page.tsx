import RecordCollectionsClient from "./RecordCollectionsClient";
import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { PaginatedRecordCollectionsResponseDto } from "../types/responseDTOs";
import { getAllRecordCollectionsServer } from "@/app/lib/server_service/record_collection_services.server";
import { COLLECTIONS_DASHBOARD_PER_PAGE } from "./components/recordCollections.constants";

export const metadata = { title: "Record Collections" };

export default async function Page() {
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

  if (!projectId) {
    redirect("/");
  }

  let recordCollectionsPage: PaginatedRecordCollectionsResponseDto = {
    items: [],
    pageNumber: 1,
    pageSize: 10,
    totalCount: 0,
    totalPages: 0,
    hasPrevious: false,
    hasNext: false,
  };

  recordCollectionsPage = await getAllRecordCollectionsServer(
    Number(organizationId),
    Number(projectId),
    {
      pageNumber: 1,
      pageSize: COLLECTIONS_DASHBOARD_PER_PAGE,
      sort: "updatedDesc",
    },
  );

  return (
    <RecordCollectionsClient
      organizationId={Number(organizationId)}
      projectId={Number(projectId)}
      initialRecordCollectionsPage={recordCollectionsPage}
    />
  );
}
