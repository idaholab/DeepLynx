import CollectionDetailsClient from "./CollectionDetailsClient";
import { notFound } from "next/navigation";
import {
  getRecordCollectionByIdServer,
  getRecordsInRecordCollectionServer,
} from "@/app/lib/server_service/record_collection_services.server";
import { getRecordCollectionsRouteContext } from "../serverContext";

export const metadata = { title: "Record Collections" };

type Props = {
  params: Promise<{ collectionId: string }>;
};

export default async function Page({ params }: Props) {
  const { collectionId } = await params;
  const parsedCollectionId = Number(collectionId);
  if (!Number.isFinite(parsedCollectionId)) {
    return notFound();
  }

  const { organizationId, projectId } = await getRecordCollectionsRouteContext();
  const initialCollection = await getRecordCollectionByIdServer(
    organizationId,
    projectId,
    parsedCollectionId,
  );

  if (!initialCollection) {
    return notFound();
  }

  const initialCollectionRecords = await getRecordsInRecordCollectionServer(
    organizationId,
    projectId,
    parsedCollectionId,
  );

  return (
    <CollectionDetailsClient
      organizationId={organizationId}
      projectId={projectId}
      initialCollection={initialCollection}
      initialCollectionRecords={initialCollectionRecords}
    />
  );
}
