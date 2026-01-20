// src/app/(home)/record/RecordViewClient.tsx

"use client";
import Tabs from "@/app/(home)/components/Tabs";
import { XMarkIcon } from "@heroicons/react/24/outline";
import Link from "next/link";
import React, { useCallback, useEffect, useMemo, useState } from "react";
import toast from "react-hot-toast";
import PropertyTable from "../components/PropertyTable";
import { RecordResponseDto, TagResponseDto } from "../types/responseDTOs";
import RecordLoading from "./loading";

// Components
import ConfirmationModal from "@/app/(home)/components/ConfirmationModal";
import RelatedRecordsCard, {
  CardColumn,
} from "./components/RelatedRecordsCard";

// Types & Context
import { useLanguage } from "@/app/contexts/Language";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import {
  archiveEdgeByRelationship,
  getEdgeByRelationship,
} from "@/app/lib/client_service/edge_services.client";
import {
  getEdgesByRecord,
  getRecord,
  unattachTagFromRecord,
  updateRecord,
} from "@/app/lib/client_service/record_services.client";
import { getClass } from "@/app/lib/client_service/class_services.client";
import { getAllTagsOrg } from "@/app/lib/client_service/tag_services.client";
import GraphClientPage from "../graph/GraphClientPage";
import {
  ClassResponseDto,
  RelatedRecordsResponseDto,
} from "../types/responseDTOs";
import RecordTagsPanel from "./components/RecordTagsPanel";
import RelatedRecordsCardSkeleton from "./skeletons/RelatedRecordsSkeleton";

// ============= HELPER FUNCTIONS =============
interface PropertyRow {
  label: string;
  value: React.ReactNode;
  editable?: boolean;
  onEdit?: (newValue: string) => void;
  isNested?: boolean;
  nestedRows?: PropertyRow[];
}

/**
 * Converts a nested object structure into PropertyRow format
 * @param obj - The object to parse
 * @param parentKey - Optional parent key for nested properties
 * @returns Array of PropertyRow objects
 */
function parseNestedProperties(
  obj: JSON,
  parentKey: string = ""
): PropertyRow[] {
  if (!obj || typeof obj !== "object") {
    return [];
  }

  return Object.entries(obj).map(([key, value]) => {
    const label = key
      .split("_")
      .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
      .join(" ");

    // Check if value is an object (but not null or array)
    const isNestedObject =
      value !== null && typeof value === "object" && !Array.isArray(value);

    if (isNestedObject) {
      return {
        label,
        value: "", // Won't be displayed for nested objects
        isNested: true,
        nestedRows: parseNestedProperties(value, key),
      };
    } else {
      // Handle arrays and primitive values
      const displayValue = Array.isArray(value)
        ? JSON.stringify(value)
        : String(value);

      return {
        label,
        value: displayValue,
        isNested: false,
      };
    }
  });
}

// ============= TYPE DEFINITIONS =============
interface Props {
  projectId: number;
  recordId: number;
}

interface RelatedRecordViewModel extends RelatedRecordsResponseDto {
  actions: React.JSX.Element;
}

interface ModalState {
  isOpen: boolean;
  type: "relatedRecord" | null;
  nameToRemove: string;
  recordNameToRemove?: string;
  idToRemove: string | null;
  originId: number | null;
  destinationId: number | null;
}

// ============= MAIN COMPONENT =============
export default function RecordViewClient({ projectId, recordId }: Props) {
  const { t } = useLanguage();
  const { organization, hasLoaded } = useOrganizationSession();

  // ============= STATE MANAGEMENT =============
  // Record & Tags State
  const [record, setRecord] = useState<RecordResponseDto | null>(null);
  const [recordClass, setRecordClass] = useState<ClassResponseDto | null>(null);
  const [tags, setTags] = useState<TagResponseDto[]>([]);
  const [selectedTags, setSelectedTags] = useState<TagResponseDto[]>([]);
  const [selectedIds, setSelectedIds] = useState<string[]>([]);

  // Pagination State
  const [originPage, setOriginPage] = useState(1);
  const [destinationPage, setDestinationPage] = useState(1);
  const [pageSize] = useState(20);
  const [hasMoreOrigins, setHasMoreOrigins] = useState(true);
  const [hasMoreDestinations, setHasMoreDestinations] = useState(true);

  // Related Records State
  const [originRecords, setOriginRecords] = useState<RelatedRecordViewModel[]>(
    []
  );
  const [destinationRecords, setDestinationRecords] = useState<
    RelatedRecordViewModel[]
  >([]);
  const [isLoadingOrigins, setIsLoadingOrigins] = useState(false);
  const [isLoadingDestinations, setIsLoadingDestinations] = useState(false);

  // Modal State
  const [modal, setModal] = useState<ModalState>({
    isOpen: false,
    type: null,
    nameToRemove: "",
    recordNameToRemove: "",
    idToRemove: null,
    originId: null,
    destinationId: null,
  });

  // UI State
  const [activeTab, setActiveTab] = useState(0);

  // ============= HANDLERS =============
  const handleUpdateRecord = useCallback(
    async (field: string, value: string, successMessage: string) => {
      if (!organization?.organizationId) return;

      try {
        const update = await updateRecord(
          organization.organizationId as number,
          projectId,
          recordId,
          { [field]: value }
        );
        setRecord((prev) => (prev ? { ...prev, ...update } : update));
        toast.success(successMessage);
      } catch (error) {
        toast.error(`${t.translations.FAILED_TO_UPDATE} ${field}`);
      }
    },
    [
      organization?.organizationId,
      projectId,
      recordId,
      t.translations.FAILED_TO_UPDATE,
    ]
  );

  const resetAllState = useCallback(() => {
    setRecord(null);
    setSelectedTags([]);
    setSelectedIds([]);
    setOriginPage(1);
    setDestinationPage(1);
    setOriginRecords([]);
    setDestinationRecords([]);
    setHasMoreOrigins(true);
    setHasMoreDestinations(true);
  }, []);

  // Create a reusable fetch function
  const fetchRelatedRecords = useCallback(
    async (
      isOrigin: boolean,
      page: number,
      setLoading: (val: boolean) => void,
      setHasMore: (val: boolean) => void,
      setRecords: React.Dispatch<React.SetStateAction<RelatedRecordViewModel[]>>
    ) => {
      if (!recordId || !projectId || !record || !organization?.organizationId)
        return;

      try {
        setLoading(true);

        const edges = await getEdgesByRecord(
          organization.organizationId as number,
          projectId,
          recordId,
          isOrigin,
          page,
          true,
          pageSize
        );

        if (!edges || edges.length === 0) {
          setHasMore(false);
          if (page === 1) {
            setRecords([]);
          }
          setLoading(false);
          return;
        }

        if (edges.length < pageSize) {
          setHasMore(false);
        }

        const viewModels: RelatedRecordViewModel[] = edges
          .filter(
            (edge) => edge.relatedRecordId != null && edge.relatedRecordId > 0
          )
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
                    nameToRemove: edge.relationshipName || t.translations.EDGE,
                    recordNameToRemove: record?.name,
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
          error
        );
        setLoading(false);
      }
    },
    [
      recordId,
      projectId,
      record,
      pageSize,
      t.translations.EDGE,
      organization?.organizationId,
    ]
  );

  const handleCloseModal = () => {
    setModal((prev) => ({ ...prev, isOpen: false }));
  };

  const handleTagSelectionChange = (selected: string[]) => {
    const newTags = tags.filter((tag) => selected.includes(tag.id.toString()));
    setSelectedTags(newTags);
    setSelectedIds(selected);
  };

  const handleConfirmUnlink = async () => {
    if (!organization?.organizationId) return;

    const { type, idToRemove, originId, destinationId } = modal;

    if (type === "relatedRecord" && originId && destinationId) {
      try {
        const edgeExists = await getEdgeByRelationship(
          organization.organizationId as number,
          projectId,
          originId,
          destinationId
        );

        if (edgeExists) {
          await archiveEdgeByRelationship(
            organization.organizationId as number,
            projectId,
            originId,
            destinationId,
            true
          );

          if (originId === recordId) {
            setOriginRecords((prev) =>
              prev.filter((r) => r.relatedRecordId !== Number(idToRemove))
            );
          } else {
            setDestinationRecords((prev) =>
              prev.filter((r) => r.relatedRecordId !== Number(idToRemove))
            );
          }

          toast.success(t.translations.LINK_ARCHIVED_SUCCESS);
        }
      } catch (error) {
        console.error("Error archiving link:", error);
        toast.error(t.translations.FAILED_TO_ARCHIVE_LINK);
      }
    }

    handleCloseModal();
  };

  const handleOpenModal = useCallback(
    (
      id: string,
      name: string,
      recordName: string | undefined,
      type: "relatedRecord"
    ) => {
      setModal({
        isOpen: true,
        type,
        nameToRemove: name,
        recordNameToRemove: recordName,
        idToRemove: id,
        originId: null,
        destinationId: null,
      });
    },
    []
  );

  // ============= EFFECTS =============
  useEffect(() => {
    resetAllState();
  }, [recordId, resetAllState]);

  // Fetch main record data
  useEffect(() => {
    const fetchRecord = async () => {
      if (!recordId || !projectId || !organization?.organizationId) return;

      try {
        const data = await getRecord(
          organization.organizationId as number,
          projectId,
          recordId
        );
        setRecord(data);
        if (data.tags) {
          // Check if tags is a string (JSON) or already an array
          const parsedTags =
            typeof data.tags === "string" ? JSON.parse(data.tags) : data.tags;

          setSelectedTags(parsedTags);
          setSelectedIds(
            parsedTags.map((tag: { id: number | null }) => String(tag.id))
          );
        }
      } catch (error) {
        console.error("Error fetching record:", error);
        toast.error(t.translations.FAILED_TO_FETCH_RECORD);
      }
    };

    fetchRecord();
  }, [
    recordId,
    projectId,
    organization?.organizationId,
    t.translations.FAILED_TO_FETCH_RECORD,
  ]);

  // Fetch class info for the record (if present)
  useEffect(() => {
    if (!record?.classId || !projectId) {
      setRecordClass(null);
      return;
    }

    let cancelled = false;

    const fetchClass = async () => {
      try {
        const data = await getClass(projectId, Number(record.classId), true);
        if (!cancelled) setRecordClass(data);
      } catch (error) {
        console.error("Error fetching class:", error);
        if (!cancelled) setRecordClass(null);
      }
    };

    fetchClass();

    return () => {
      cancelled = true;
    };
  }, [record?.classId, projectId]);

  // Fetch available tags
  useEffect(() => {
    const fetchTags = async () => {
      if (!projectId || !organization?.organizationId) return;

      try {
        const data = await getAllTagsOrg(
          organization.organizationId as number,
          [projectId]
        );
        setTags(data);
      } catch (error) {
        console.error("Error fetching tags:", error);
      }
    };

    fetchTags();
  }, [projectId, organization?.organizationId]);

  // Fetch origin records
  useEffect(() => {
    fetchRelatedRecords(
      true,
      originPage,
      setIsLoadingOrigins,
      setHasMoreOrigins,
      setOriginRecords
    );
  }, [fetchRelatedRecords, originPage]);

  // Fetch destination records
  useEffect(() => {
    fetchRelatedRecords(
      false,
      destinationPage,
      setIsLoadingDestinations,
      setHasMoreDestinations,
      setDestinationRecords
    );
  }, [fetchRelatedRecords, destinationPage]);

  // ============= MEMOIZED VALUES =============
  const systemPropertiesRows = useMemo(() => {
    if (!record) return [];
    return [
      {
        label: t.translations.RECORD_NAME,
        value: record.name,
        editable: true,
        onEdit: (value: string) =>
          handleUpdateRecord("name", value, t.translations.RECORD_NAME_UPDATED),
      },
      {
        label: t.translations.RECORD_DESCRIPTION,
        value: record.description,
        editable: true,
        onEdit: (value: string) =>
          handleUpdateRecord(
            "description",
            value,
            t.translations.RECORD_NAME_UPDATED
          ),
      },
      { label: t.translations.URI, value: record.uri },
      { label: t.translations.ORIGINAL_ID, value: record.originalId },
      { label: t.translations.LAST_UPDATED_AT, value: record.lastUpdatedAt },
    ];
  }, [record, handleUpdateRecord, t.translations]);

  const additionalPropertiesRows = useMemo(() => {
    if (!record?.properties) return [];

    const parsedProperties =
      typeof record.properties === "string"
        ? JSON.parse(record.properties)
        : record.properties;

    return parseNestedProperties(parsedProperties);
  }, [record?.properties]);

  const relatedRecordsColumns: CardColumn<RelatedRecordViewModel>[] = [
    {
      key: "relationshipName",
      label: t.translations.RELATIONSHIP,
      render: (row) => <span>{row.relationshipName || "-"}</span>,
    },
    {
      key: "relatedRecordName",
      label: t.translations.RECORD_NAME,
      render: (row) =>
        row.relatedRecordId ? (
          <Link
            href={`/record?recordId=${row.relatedRecordId}&projectId=${projectId}`}
            className="text-primary hover:text-primary-content hover:underline"
          >
            {row.relatedRecordName ||
              `${t.translations.RECORD_} ${row.relatedRecordId}`}
          </Link>
        ) : (
          <span>{row.relatedRecordName || t.translations.UNKOWN}</span>
        ),
    },
    { key: "actions", label: t.translations.ACTIONS },
  ];

  const handleRemoveTag = async (tagId: number) => {
    if (!organization?.organizationId) return;

    try {
      await unattachTagFromRecord(
        organization.organizationId as number,
        projectId,
        recordId,
        tagId
      );

      setSelectedTags((prev) => prev.filter((t) => t.id !== tagId));
      setSelectedIds((prev) => prev.filter((id) => id !== String(tagId)));

      toast.success("Tag(s) removed!");
    } catch (error) {
      console.error("Error removing tag:", error);
      toast.error(t.translations.FAILED_TO_UPDATE_TAGS);
    }
  };

  // ============= RENDER HELPERS =============
  if (!hasLoaded || !organization) {
    return <RecordLoading />;
  }

  if (!record) {
    return <RecordLoading />;
  }

  const tabs = [
    {
      label: t.translations.RECORD_INFORMATION,
      content: (
        <div className="flex gap-6 mt-4">
          {/* Left Column - Properties */}
          <div className="w-full md:w-1/2 space-y-4">
            <PropertyTable
              title={t.translations.SYSTEM_PROPERTIES}
              rows={systemPropertiesRows}
              download={
                !!record.uri &&
                record.uri.trim().length > 0 &&
                record.uri.toLowerCase() !== "null"
              }
              recordName={record.name}
            />
            <PropertyTable
              title={t.translations.ADDITIONAL_PROPERIES}
              rows={additionalPropertiesRows}
            />
          </div>

          {/* Right Column - Tags & Relations */}
          <div className="flex-1 space-y-4">
            {/* Tags Card */}
            <RecordTagsPanel
              tags={tags}
              selectedTags={selectedTags}
              selectedIds={selectedIds}
              onSelectionChange={handleTagSelectionChange}
              onRemoveTag={handleRemoveTag}
              projectId={projectId}
              recordId={recordId}
              setTags={setTags}
              setSelectedTags={setSelectedTags}
              setSelectedIds={setSelectedIds}
              title={t.translations.TAGS}
            />

            {/* Related Records Card - Origins */}
            {isLoadingOrigins && originPage === 1 ? (
              <RelatedRecordsCardSkeleton rows={6} columns={3} />
            ) : (
              <RelatedRecordsCard
                title={`${t.translations.OUTGOING}${record.name}${t.translations.OUTGOING_ARROW}`}
                columns={relatedRecordsColumns}
                rows={originRecords}
                onLoadMore={() => {
                  if (!isLoadingOrigins && hasMoreOrigins) {
                    setOriginPage((prev) => prev + 1);
                  }
                }}
                isLoading={isLoadingOrigins && originPage > 1}
                hasMore={hasMoreOrigins}
              />
            )}

            {/* Related Records Card - Destinations */}
            {isLoadingDestinations && destinationPage === 1 ? (
              <RelatedRecordsCardSkeleton rows={6} columns={3} />
            ) : (
              <div className="mt-4">
                <RelatedRecordsCard
                  title={`${t.translations.INCOMING}${record.name}${t.translations.INCOMING_ARROW}`}
                  columns={relatedRecordsColumns}
                  rows={destinationRecords}
                  onLoadMore={() => {
                    if (!isLoadingDestinations && hasMoreDestinations) {
                      setDestinationPage((prev) => prev + 1);
                    }
                  }}
                  isLoading={isLoadingDestinations && destinationPage > 1}
                  hasMore={hasMoreDestinations}
                />
              </div>
            )}
          </div>
        </div>
      ),
    },
    {
      label: "Graph",
      content: <GraphClientPage projectId={projectId} recordId={recordId} />,
    },
  ];

  // ============= MAIN RENDER =============
  return (
    <div className="mr-4">
      <div className="bg-base-200/40 pl-12 p-4">
        <h1 className="text-2xl font-bold text-base-content">{record.name}</h1>
        {record.classId && (
          <span className="badge badge-primary">
            {recordClass?.name || <div className="loading size-3" />}
          </span>
        )}
      </div>

      <Tabs
        tabs={tabs}
        className="ml-6 pt-6"
        activeTab={tabs[activeTab].label}
        onTabChange={(label) =>
          setActiveTab(tabs.findIndex((tab) => tab.label === label))
        }
      />

      <ConfirmationModal
        isOpen={modal.isOpen}
        onClose={handleCloseModal}
        onConfirm={handleConfirmUnlink}
        tagName={modal.nameToRemove}
        recordName={modal.recordNameToRemove}
      />
    </div>
  );
}
