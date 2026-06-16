import CollectionDetailsClient from "./CollectionDetailsClient";
import { cookies } from "next/headers";
import { notFound, redirect } from "next/navigation";
import {
  getAllRecordCollectionsServer,
  getRecordsInRecordCollectionServer,
} from "@/app/lib/server_service/record_collection_services.server";
import { RecordResponseDto } from "@/app/(home)/types/responseDTOs";

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

  const collections = await getAllRecordCollectionsServer(
    Number(organizationId),
    Number(projectId),
  );
  const initialCollection = collections.find(
    (collection) => collection.id === parsedCollectionId,
  );

  if (!initialCollection) {
    return notFound();
  }

  let initialCollectionRecords: RecordResponseDto[] = [];
  try {
    initialCollectionRecords = await getRecordsInRecordCollectionServer(
      Number(organizationId),
      Number(projectId),
      parsedCollectionId,
    );
  } catch (error) {
    console.error("Failed to load initial collection records:", error);
  }

  return (
    <CollectionDetailsClient
      organizationId={Number(organizationId)}
      projectId={Number(projectId)}
      initialCollection={initialCollection}
      initialCollectionRecords={initialCollectionRecords}
    />
  );
}
