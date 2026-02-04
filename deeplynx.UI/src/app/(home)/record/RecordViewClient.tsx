// src/app/(home)/record/RecordViewClient.tsx

"use client";
import Tabs from "@/app/(home)/components/Tabs";
import { PencilIcon, PlusIcon, XMarkIcon } from "@heroicons/react/24/outline";
import Link from "next/link";
import React, { useCallback, useEffect, useMemo, useState } from "react";
import toast from "react-hot-toast";
import PropertyTable from "../components/PropertyTable";
import {
  HistoricalRecordResponseDto,
  TagResponseDto,
} from "../types/responseDTOs";
import RecordLoading from "./loading";

// Components
import ConfirmationModal from "@/app/(home)/components/ConfirmationModal";
import RelatedRecordsCard, {
  CardColumn,
} from "./components/RelatedRecordsCard";

// Types & Context
import { useLanguage } from "@/app/contexts/Language";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import { getClass } from "@/app/lib/client_service/class_services.client";
import {
  archiveEdgeByRelationship,
  createEdge,
  getEdgeByRelationship,
} from "@/app/lib/client_service/edge_services.client";
import {
  fullTextSearch,
  getHistoricalRecord,
} from "@/app/lib/client_service/query_services.client";
import {
  getEdgesByRecord,
  unattachTagFromRecord,
  updateRecord,
} from "@/app/lib/client_service/record_services.client";
import { getAllTagsOrg } from "@/app/lib/client_service/tag_services.client";
import GraphClientPage from "../graph/GraphClientPage";
import {
  ClassResponseDto,
  RelatedRecordsResponseDto,
} from "../types/responseDTOs";
import AdditionalPropertiesEditor from "./components/AdditionalPropertiesEditor";
import RecordTagsPanel from "./components/RecordTagsPanel";
import RelatedRecordsCardSkeleton from "./skeletons/RelatedRecordsSkeleton";

import {
  createClass,
  getAllClasses,
} from "@/app/lib/client_service/class_services.client";
import ClassSelectorModal from "./components/ClassSelectorModal";
import type { RecordSearchResult } from "./components/AddEdgeModal";
import AddEdgeModal from "./components/AddEdgeModal";

// ============= HELPER FUNCTIONS =============
interface PropertyRow {
  label: string;
  value: React.ReactNode;
  editable?: boolean;
  onEdit?: (newValue: string) => void;
  isNested?: boolean;
  nestedRows?: PropertyRow[];
}

function parseNestedProperties(
  obj: JSON,
  parentKey: string = "",
): PropertyRow[] {
  if (!obj || typeof obj !== "object") {
    return [];
  }

  return Object.entries(obj).map(([key, value]) => {
    const label = key
      .split("_")
      .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
      .join(" ");

    const isNestedObject =
      value !== null && typeof value === "object" && !Array.isArray(value);

    if (isNestedObject) {
      return {
        label,
        value: "",
        isNested: true,
        nestedRows: parseNestedProperties(value, key),
      };
    } else {
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
  recordNameToRemove?: string | null;
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
  const [record, setRecord] = useState<HistoricalRecordResponseDto | null>(
    null,
  );
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
    [],
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

  const [isPropertiesEditorOpen, setIsPropertiesEditorOpen] = useState(false);
  const [isSavingProperties, setIsSavingProperties] = useState(false);

  const [isClassModalOpen, setIsClassModalOpen] = useState(false);
  const [availableClasses, setAvailableClasses] = useState<ClassResponseDto[]>(
    [],
  );
  const [isLoadingClasses, setIsLoadingClasses] = useState(false);

  // Add Edge Modal State
  const [isAddEdgeModalOpen, setIsAddEdgeModalOpen] = useState(false);
  const [edgeDirection, setEdgeDirection] = useState<"outgoing" | "incoming">(
    "outgoing",
  );
  const [edgeRelationship, setEdgeRelationship] = useState("RELATED_TO");

  // ============= HANDLERS =============
  const handleUpdateRecord = useCallback(
    async (field: string, value: string, successMessage: string) => {
      if (!organization?.organizationId) return;

      try {
        const update = await updateRecord(
          organization.organizationId as number,
          projectId,
          recordId,
          { [field]: value },
        );
        setRecord((prev) => {
          if (!prev) return prev;

          return {
            ...prev,
            name: update.name ?? prev.name,
            description: update.description ?? prev.description,
            uri: update.uri ?? prev.uri,
            originalId: update.originalId ?? prev.originalId,
            classId: update.classId ?? prev.classId,
            dataSourceId: update.dataSourceId ?? prev.dataSourceId,
            projectId: update.projectId ?? prev.projectId,
            lastUpdatedAt: update.lastUpdatedAt ?? prev.lastUpdatedAt,
            lastUpdatedBy: update.lastUpdatedBy ?? prev.lastUpdatedBy,
            isArchived: update.isArchived ?? prev.isArchived,
            objectStorageId: update.objectStorageId ?? prev.objectStorageId,
          };
        });
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
    ],
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

  const handleSaveProperties = useCallback(
    async (newProperties: any) => {
      if (!organization?.organizationId) return;

      try {
        setIsSavingProperties(true);

        const update = await updateRecord(
          organization.organizationId as number,
          projectId,
          recordId,
          { properties: newProperties },
        );

        setRecord((prev) => {
          if (!prev) return prev;
          return {
            ...prev,
            properties:
              typeof update.properties === "string"
                ? update.properties
                : JSON.stringify(update.properties),
          };
        });

        toast.success(t.translations.PROPERTIES_UPDATED_SUCCESSFULLY);
        setIsPropertiesEditorOpen(false);
      } catch (error) {
        console.error("Error updating properties:", error);
        toast.error(t.translations.FAILED_TO_UPDATE_PROPERTIES);
      } finally {
        setIsSavingProperties(false);
      }
    },
    [
      organization?.organizationId,
      projectId,
      recordId,
      t.translations.PROPERTIES_UPDATED_SUCCESSFULLY,
      t.translations.FAILED_TO_UPDATE_PROPERTIES,
    ],
  );

  const fetchRelatedRecords = useCallback(
    async (
      isOrigin: boolean,
      page: number,
      setLoading: (val: boolean) => void,
      setHasMore: (val: boolean) => void,
      setRecords: React.Dispatch<
        React.SetStateAction<RelatedRecordViewModel[]>
      >,
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
          pageSize,
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
            (edge) => edge.relatedRecordId != null && edge.relatedRecordId > 0,
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
          error,
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
    ],
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
          destinationId,
        );

        if (edgeExists) {
          await archiveEdgeByRelationship(
            organization.organizationId as number,
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
      type: "relatedRecord",
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
    [],
  );

  const handleClassUpdate = async (class_id: number) => {
    if (!organization?.organizationId) return;

    try {
      const update = await updateRecord(
        organization.organizationId as number,
        projectId,
        recordId,
        { class_id },
      );

      setRecord((prev) => {
        if (!prev) return prev;
        return {
          ...prev,
          classId: update.classId ?? prev.classId,
        };
      });

      toast.success(t.translations.CLASS_UPDATED_SUCCESSFULLY);
    } catch (error) {
      console.error("Error updating class: ", error);
      toast.error(t.translations.FAILED_TO_UPDATE_CLASS);
    }
  };

  const handleCreateClass = async (name: string, description?: string) => {
    if (!organization?.organizationId) return;

    try {
      const newClass = await createClass(projectId, {
        name,
        description: description ?? "",
      });

      setAvailableClasses((prev) => [...prev, newClass]);

      await handleClassUpdate(newClass.id);

      toast.success(t.translations.CLASS_CREATED_AND_APPLIED);
      setIsClassModalOpen(false);
    } catch (error) {
      console.error("Error creating class: ", error);
      toast.error(t.translations.FAILED_TO_CREATE_CLASS);
      throw error;
    }
  };

  // Handler to search records for AddEdgeModal
  const handleSearchRecords = async (
    query: string,
    option?: string,
  ): Promise<RecordSearchResult[]> => {
    if (!organization?.organizationId) return [];

    try {
      const results = await fullTextSearch(
        organization.organizationId as number,
        query,
        [projectId],
      );

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
        t.translations.FAILED_TO_SEARCH_RECORDS || "Failed to search records",
      );
      return [];
    }
  };

  // Handler to create relationships from AddEdgeModal
  const handleCreateRelationships = async (data: {
    records: any[];
    relationship: string;
    direction: "outgoing" | "incoming";
  }) => {
    if (!organization?.organizationId) return;

    try {
      const promises = data.records.map(async (targetRecord) => {
        const origin_id =
          data.direction === "outgoing" ? recordId : targetRecord.id;
        const destination_id =
          data.direction === "outgoing" ? targetRecord.id : recordId;

        return createEdge(
          organization.organizationId as number,
          projectId,
          record?.dataSourceId as number,
          {
            origin_id,
            destination_id,
            relationship_name: data.relationship,
          },
        );
      });

      await Promise.all(promises);

      toast.success(
        `${t.translations.CREATED || "Created"} ${data.records.length} ${
          t.translations.RELATIONSHIP || "relationship"
        }${data.records.length > 1 ? "s" : ""}!`,
      );

      // Refresh the related records
      setOriginPage(1);
      setDestinationPage(1);
      setOriginRecords([]);
      setDestinationRecords([]);
      setHasMoreOrigins(true);
      setHasMoreDestinations(true);
    } catch (error) {
      console.error("Error creating relationships:", error);
      toast.error(
        t.translations.FAILED_TO_CREATE_RELATIONSHIPS ||
          "Failed to create relationships",
      );
      throw error;
    }
  };

  // ============= EFFECTS =============
  useEffect(() => {
    resetAllState();
  }, [recordId, resetAllState]);

  // Fetch main record data
  useEffect(() => {
    const fetchRecord = async () => {
      if (!recordId || !projectId || !organization?.organizationId) return;

      try {
        const data = await getHistoricalRecord(
          organization.organizationId as number,
          projectId,
          recordId,
          null,
          true,
        );
        setRecord(data);
        if (data.tags) {
          const parsedTags =
            typeof data.tags === "string" ? JSON.parse(data.tags) : data.tags;

          setSelectedTags(parsedTags);
          setSelectedIds(
            parsedTags.map((tag: { id: number | null }) => String(tag.id)),
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
          [projectId],
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
      setOriginRecords,
    );
  }, [fetchRelatedRecords, originPage]);

  // Fetch destination records
  useEffect(() => {
    fetchRelatedRecords(
      false,
      destinationPage,
      setIsLoadingDestinations,
      setHasMoreDestinations,
      setDestinationRecords,
    );
  }, [fetchRelatedRecords, destinationPage]);

  useEffect(() => {
    const fetchClass = async () => {
      if (!projectId || !organization?.organizationId) return;

      try {
        setIsLoadingClasses(true);
        const data = await getAllClasses(projectId, true);
        setAvailableClasses(data);
      } catch (error) {
        console.error("Error fetching classes: ", error);
        toast.error(t.translations.FAILED_TO_FETCH_CLASSES);
      } finally {
        setIsLoadingClasses(false);
      }
    };

    fetchClass();
  }, [
    projectId,
    organization?.organizationId,
    t.translations.FAILED_TO_FETCH_CLASSES,
  ]);

  // ============= MEMOIZED VALUES =============
  const systemPropertiesRows = useMemo(() => {
    if (!record) return [];
    return [
      { label: t.translations.RECORD_ID, value: record.id },
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
            t.translations.RECORD_NAME_UPDATED,
          ),
      },
      { label: t.translations.URI, value: record.uri },
      {
        label: t.translations.ORIGINAL_ID,
        value: record.originalId,
        editable: true,
      },
      { label: t.translations.LAST_UPDATED_AT, value: record.lastUpdatedAt },
      { label: t.translations.DATA_SOURCE, value: record.dataSourceName },
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
        tagId,
      );

      setSelectedTags((prev) => prev.filter((t) => t.id !== tagId));
      setSelectedIds((prev) => prev.filter((id) => id !== String(tagId)));

      toast.success(t.translations.TAGS_REMOVED);
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
              onEditProperties={() => setIsPropertiesEditorOpen(true)}
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
                relationship="outgoing"
                onAddRelationship={() => {
                  setIsAddEdgeModalOpen(true);
                }}
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
                  relationship="incoming"
                  onAddRelationship={() => {
                    setIsAddEdgeModalOpen(true);
                  }}
                />
              </div>
            )}
          </div>
        </div>
      ),
    },
    {
      label: t.translations.GRAPH,
      content: <GraphClientPage projectId={projectId} recordId={recordId} />,
    },
  ];

  // ============= MAIN RENDER =============
  return (
    <div className="mr-4">
      <div className="bg-base-200/40 pl-12 p-4">
        <h1 className="text-2xl font-bold text-base-content">{record.name}</h1>
        {record.classId ? (
          <div className="flex gap-2 py-auto items-center">
            <span className="badge badge-primary">
              {recordClass?.name || <div className="loading size-3" />}
            </span>
            <button
              onClick={() => setIsClassModalOpen(true)}
              className="btn btn-ghost btn-xs btn-circle"
              title={t.translations.EDIT_CLASS}
            >
              <PencilIcon className="size-4" />
            </button>
          </div>
        ) : (
          <button
            onClick={() => setIsClassModalOpen(true)}
            className="btn btn-sm btn-outline mt-2"
          >
            <PlusIcon className="w-4 h-4 mr-1" />
            {t.translations.ADD_CLASS || "Add Class"}
          </button>
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

      <AdditionalPropertiesEditor
        isOpen={isPropertiesEditorOpen}
        onClose={() => setIsPropertiesEditorOpen(false)}
        properties={
          record?.properties
            ? typeof record.properties === "string"
              ? JSON.parse(record.properties)
              : record.properties
            : {}
        }
        onSave={handleSaveProperties}
        isSaving={isSavingProperties}
      />

      <ClassSelectorModal
        isOpen={isClassModalOpen}
        onClose={() => setIsClassModalOpen(false)}
        currentClassId={record?.classId ?? null}
        onClassUpdate={handleClassUpdate}
        availableClasses={availableClasses}
        onCreateClass={handleCreateClass}
        isLoading={isLoadingClasses}
      />

      <AddEdgeModal
        isOpen={isAddEdgeModalOpen}
        onClose={() => setIsAddEdgeModalOpen(false)}
        currentRecord={{
          id: record.id,
          name: record.name,
          description: record.description,
          dataSourceName: record.dataSourceName,
        }}
        relationship={edgeRelationship}
        direction={edgeDirection}
        projectId={projectId}
        organizationId={organization?.organizationId as number}
        onSearchRecords={handleSearchRecords}
        onCreateRelationships={handleCreateRelationships}
        dataSourceId={record?.dataSourceId}
      />
    </div>
  );
}
