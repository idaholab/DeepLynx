import RecordCollectionsClient from "./RecordCollectionsClient";
import { getAllRecordCollectionsServer } from "@/app/lib/server_service/record_collection_services.server";
import { COLLECTIONS_DASHBOARD_PER_PAGE } from "./components/recordCollections.constants";
import { getRecordCollectionsRouteContext } from "./serverContext";

export const metadata = { title: "Record Collections" };

export default async function Page() {
  const { organizationId, projectId } = await getRecordCollectionsRouteContext();
  const recordCollectionsPage = await getAllRecordCollectionsServer(
    organizationId,
    projectId,
    {
      pageNumber: 1,
      pageSize: COLLECTIONS_DASHBOARD_PER_PAGE,
      sort: "updatedDesc",
    },
  );

  return (
    <RecordCollectionsClient
      organizationId={organizationId}
      projectId={projectId}
      initialRecordCollectionsPage={recordCollectionsPage}
    />
  );
}
