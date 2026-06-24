import NewCollectionClient from "./NewCollectionClient";
import { getRecordCollectionsRouteContext } from "../serverContext";

export const metadata = { title: "Record Collections" };

export default async function Page() {
  const { organizationId, projectId } = await getRecordCollectionsRouteContext();

  return (
    <NewCollectionClient
      organizationId={organizationId}
      projectId={projectId}
    />
  );
}
