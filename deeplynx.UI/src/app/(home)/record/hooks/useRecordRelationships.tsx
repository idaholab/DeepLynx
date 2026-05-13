import React, { useCallback, useEffect, useState } from "react";
import { XMarkIcon } from "@heroicons/react/24/outline";
import toast from "react-hot-toast";

import {
  archiveEdgeByRelationship,
  createEdge,
  getEdgeByRelationship,
  updateEdgeByRelationship,
} from "@/app/lib/client_service/edge_services.client";
import { fullTextSearch } from "@/app/lib/client_service/query_services.client";
import { getEdgesByRecord } from "@/app/lib/client_service/record_services.client";
import type { RelatedRecordsResponseDto } from "../../types/responseDTOs";
import type { RecordSearchResult } from "../components/AddEdgeModal";

export interface RelatedRecordViewModel extends RelatedRecordsResponseDto {
  actions: React.JSX.Element;
}

export interface ModalState {
  isOpen: boolean;
  type: "relatedRecord" | null;
  nameToRemove: string;
  recordNameToRemove?: string | null;
  idToRemove: string | null;
  originId: number | null;
  destinationId: number | null;
}

interface UseRecordRelationshipsParams {
  organizationId?: number | string;
  projectId: number;
  recordId: number;
  recordName?: string | null;
  recordDataSourceId?: number | string | null;
  translations: Record<string, string>;
}

const PAGE_SIZE = 20;

export function useRecordRelationships({
  organizationId,
  projectId,
  recordId,
  recordName,
  recordDataSourceId,
  translations,
}: UseRecordRelationshipsParams) {
  const [originPage, setOriginPage] = useState(1);
  const [destinationPage, setDestinationPage] = useState(1);
  const [hasMoreOrigins, setHasMoreOrigins] = useState(true);
  const [hasMoreDestinations, setHasMoreDestinations] = useState(true);

  const [originRecords, setOriginRecords] = useState<RelatedRecordViewModel[]>(
    [],
  );
  const [destinationRecords, setDestinationRecords] = useState<
    RelatedRecordViewModel[]
  >([]);
  const [isLoadingOrigins, setIsLoadingOrigins] = useState(false);
  const [isLoadingDestinations, setIsLoadingDestinations] = useState(false);

  const [modal, setModal] = useState<ModalState>({
    isOpen: false,
    type: null,
    nameToRemove: "",
    recordNameToRemove: "",
    idToRemove: null,
    originId: null,
    destinationId: null,
  });

  const [isAddEdgeModalOpen, setIsAddEdgeModalOpen] = useState(false);
  const [edgeDirection, setEdgeDirection] = useState<"outgoing" | "incoming">(
    "outgoing",
  );
  const [edgeRelationship, setEdgeRelationship] = useState("");

  const resetRelationshipState = useCallback(() => {
    setOriginPage(1);
    setDestinationPage(1);
    setOriginRecords([]);
    setDestinationRecords([]);
    setHasMoreOrigins(true);
    setHasMoreDestinations(true);
    setModal((prev) => ({ ...prev, isOpen: false }));
    setIsAddEdgeModalOpen(false);
  }, []);

  const fetchRelatedRecords = useCallback(
    async (
      isOrigin: boolean,
      page: number,
      setLoading: (val: boolean) => void,
      setHasMore: (val: boolean) => void,
      setRecords: React.Dispatch<React.SetStateAction<RelatedRecordViewModel[]>>,
    ) => {
      if (!recordId || !projectId || organizationId == null) return;

      try {
        setLoading(true);

        const edges = await getEdgesByRecord(
          Number(organizationId),
          projectId,
          recordId,
          isOrigin,
          page,
          true,
          PAGE_SIZE,
        );

        if (!edges || edges.length === 0) {
          setHasMore(false);
          if (page === 1) {
            setRecords([]);
          }
          setLoading(false);
          return;
        }

        if (edges.length < PAGE_SIZE) {
          setHasMore(false);
        }

        const viewModels: RelatedRecordViewModel[] = edges
          .filter((edge) => edge.relatedRecordId != null && edge.relatedRecordId > 0)
          .map((edge) => ({
            relatedRecordName: edge.relatedRecordName,
            relatedRecordId: edge.relatedRecordId,
            relatedRecordProjectId: edge.relatedRecordProjectId,
            relationshipName: edge.relationshipName,
            actions: (
              <XMarkIcon
                className="w-5 h-5 cursor-pointer text-error hover:text-error-content"
                onClick={() => {
                  setModal({
                    isOpen: true,
                    type: "relatedRecord",
                    nameToRemove: edge.relationshipName || translations.EDGE,
                    recordNameToRemove: recordName,
                    idToRemove: edge.relatedRecordId!.toString(),
                    originId: isOrigin ? recordId : edge.relatedRecordId!,
                    destinationId: isOrigin ? edge.relatedRecordId! : recordId,
                  });
                }}
              />
            ),
          }));

        if (page === 1) {
          setRecords(viewModels);
        } else {
          setRecords((prev) => [...prev, ...viewModels]);
        }

        setLoading(false);
      } catch (error) {
        console.error(
          `Error fetching ${isOrigin ? "origin" : "destination"} records:`,
          error,
        );
        setLoading(false);
      }
    },
    [organizationId, projectId, recordId, recordName, translations.EDGE],
  );

  useEffect(() => {
    fetchRelatedRecords(
      true,
      originPage,
      setIsLoadingOrigins,
      setHasMoreOrigins,
      setOriginRecords,
    );
  }, [fetchRelatedRecords, originPage]);

  useEffect(() => {
    fetchRelatedRecords(
      false,
      destinationPage,
      setIsLoadingDestinations,
      setHasMoreDestinations,
      setDestinationRecords,
    );
  }, [fetchRelatedRecords, destinationPage]);

  const loadMoreOrigins = useCallback(() => {
    if (!isLoadingOrigins && hasMoreOrigins) {
      setOriginPage((prev) => prev + 1);
    }
  }, [hasMoreOrigins, isLoadingOrigins]);

  const loadMoreDestinations = useCallback(() => {
    if (!isLoadingDestinations && hasMoreDestinations) {
      setDestinationPage((prev) => prev + 1);
    }
  }, [hasMoreDestinations, isLoadingDestinations]);

  const openAddEdgeModal = useCallback((direction: "outgoing" | "incoming") => {
    setEdgeDirection(direction);
    setIsAddEdgeModalOpen(true);
  }, []);

  const handleCloseModal = useCallback(() => {
    setModal((prev) => ({ ...prev, isOpen: false }));
  }, []);

  const handleConfirmUnlink = useCallback(async () => {
    if (organizationId == null) return;

    const { type, idToRemove, originId, destinationId } = modal;

    if (type === "relatedRecord" && originId && destinationId) {
      try {
        const edgeExists = await getEdgeByRelationship(
          Number(organizationId),
          projectId,
          originId,
          destinationId,
        );

        if (edgeExists) {
          await archiveEdgeByRelationship(
            Number(organizationId),
            projectId,
            originId,
            destinationId,
            true,
          );

          if (originId === recordId) {
            setOriginRecords((prev) =>
              prev.filter((r) => r.relatedRecordId !== Number(idToRemove)),
            );
          } else {
            setDestinationRecords((prev) =>
              prev.filter((r) => r.relatedRecordId !== Number(idToRemove)),
            );
          }

          toast.success(translations.LINK_ARCHIVED_SUCCESS);
        }
      } catch (error) {
        console.error("Error archiving link:", error);
        toast.error(translations.FAILED_TO_ARCHIVE_LINK);
      }
    }

    handleCloseModal();
  }, [
    handleCloseModal,
    modal,
    organizationId,
    projectId,
    recordId,
    translations.FAILED_TO_ARCHIVE_LINK,
    translations.LINK_ARCHIVED_SUCCESS,
  ]);

  const handleSearchRecords = useCallback(
    async (query: string, option?: string): Promise<RecordSearchResult[]> => {
      if (organizationId == null) return [];

      try {
        const results = await fullTextSearch(Number(organizationId), query, [
          projectId,
        ]);

        return results.map((record) => ({
          id: Number(record.id),
          name: record.name ?? String(record.id),
          description: record.description ?? undefined,
          className: record.className ?? undefined,
          dataSourceName: record.dataSourceName ?? undefined,
          originalId: record.originalId ?? undefined,
          uri: record.uri ?? undefined,
        }));
      } catch (error) {
        console.error("Error searching records:", error);
        toast.error(
          translations.FAILED_TO_SEARCH_RECORDS || "Failed to search records",
        );
        return [];
      }
    },
    [organizationId, projectId, translations.FAILED_TO_SEARCH_RECORDS],
  );

  const handleCreateRelationships = useCallback(
    async (data: {
      records: RecordSearchResult[];
      relationshipId: number;
      relationshipName: string;
      direction: "outgoing" | "incoming";
    }) => {
      if (organizationId == null) {
        const message =
          translations.FAILED_TO_CREATE_RELATIONSHIPS ||
          "Failed to create relationships";
        toast.error(message);
        throw new Error("Organization ID is required to create relationships");
      }

      if (recordDataSourceId == null) {
        const message =
          translations.FAILED_TO_CREATE_RELATIONSHIPS ||
          "Failed to create relationships";
        toast.error(message);
        throw new Error("Record data source ID is required to create relationships");
      }

      try {
        const promises = data.records.map(async (targetRecord) => {
          const origin_id =
            data.direction === "outgoing" ? recordId : targetRecord.id;
          const destination_id =
            data.direction === "outgoing" ? targetRecord.id : recordId;

          try {
            return await createEdge(
              Number(organizationId),
              projectId,
              Number(recordDataSourceId),
              {
                origin_id,
                destination_id,
                relationship_id: data.relationshipId,
                relationship_name: data.relationshipName,
              },
            );
          } catch (error) {
            const errorMessage =
              error instanceof Error
                ? error.message
                : typeof error === "string"
                  ? error
                  : JSON.stringify(error);

            if (!errorMessage.includes("unique_edge_record_ids")) {
              throw error;
            }

            const existingEdge = await getEdgeByRelationship(
              Number(organizationId),
              projectId,
              origin_id,
              destination_id,
              false,
            );

            if (existingEdge.isArchived) {
              await archiveEdgeByRelationship(
                Number(organizationId),
                projectId,
                origin_id,
                destination_id,
                false,
              );
            }

            if (existingEdge.relationshipId !== data.relationshipId) {
              return updateEdgeByRelationship(
                Number(organizationId),
                projectId,
                origin_id,
                destination_id,
                { relationshipId: data.relationshipId },
              );
            }

            return existingEdge;
          }
        });

        await Promise.all(promises);

        toast.success(
          `${translations.CREATED || "Created"} ${data.records.length} ${
            translations.RELATIONSHIP || "relationship"
          }${data.records.length > 1 ? "s" : ""}!`,
        );

        const newRelationships: RelatedRecordViewModel[] = data.records.map(
          (targetRecord) => ({
            relatedRecordName: targetRecord.name,
            relatedRecordId: targetRecord.id,
            relatedRecordProjectId: projectId,
            relationshipName: data.relationshipName,
            actions: (
              <XMarkIcon
                className="w-5 h-5 cursor-pointer text-error hover:text-error-content"
                onClick={() => {
                  setModal({
                    isOpen: true,
                    type: "relatedRecord",
                    nameToRemove: data.relationshipName || translations.EDGE,
                    recordNameToRemove: recordName,
                    idToRemove: targetRecord.id.toString(),
                    originId:
                      data.direction === "outgoing" ? recordId : targetRecord.id,
                    destinationId:
                      data.direction === "outgoing" ? targetRecord.id : recordId,
                  });
                }}
              />
            ),
          }),
        );

        if (data.direction === "outgoing") {
          setOriginRecords((prev) => [...newRelationships, ...prev]);
        } else {
          setDestinationRecords((prev) => [...newRelationships, ...prev]);
        }
      } catch (error) {
        console.error("Error creating relationships:", error);
        toast.error(
          translations.FAILED_TO_CREATE_RELATIONSHIPS ||
            "Failed to create relationships",
        );
        throw error;
      }
    },
    [
      organizationId,
      projectId,
      recordDataSourceId,
      recordId,
      recordName,
      translations.CREATED,
      translations.EDGE,
      translations.FAILED_TO_CREATE_RELATIONSHIPS,
      translations.RELATIONSHIP,
    ],
  );

  return {
    originPage,
    destinationPage,
    hasMoreOrigins,
    hasMoreDestinations,
    originRecords,
    destinationRecords,
    isLoadingOrigins,
    isLoadingDestinations,
    modal,
    handleCloseModal,
    handleConfirmUnlink,
    isAddEdgeModalOpen,
    setIsAddEdgeModalOpen,
    edgeDirection,
    setEdgeDirection,
    edgeRelationship,
    setEdgeRelationship,
    handleSearchRecords,
    handleCreateRelationships,
    resetRelationshipState,
    loadMoreOrigins,
    loadMoreDestinations,
    openAddEdgeModal,
  };
}
