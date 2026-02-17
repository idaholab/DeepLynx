// src/app/(home)/record/RecordViewClient.tsx

"use client";
import Tabs from "@/app/(home)/components/Tabs";
import { PencilIcon, PlusIcon } from "@heroicons/react/24/outline";
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
  getHistoricalRecord,
  unattachTagFromRecord,
  updateRecord,
} from "@/app/lib/client_service/record_services.client";
import {
  getAllTags,
  getAllTagsOrg,
} from "@/app/lib/client_service/tag_services.client";
import GraphClientPage from "../graph/components/GraphClientPage";
import { ClassResponseDto } from "../types/responseDTOs";
import AdditionalPropertiesEditor from "./components/AdditionalPropertiesEditor";
import RecordHistoryTab from "./components/RecordHistoryTab";
import RecordTagsPanel from "./components/RecordTagsPanel";
import RelatedRecordsCardSkeleton from "./skeletons/RelatedRecordsSkeleton";

import {
  createClass,
  getAllClasses,
} from "@/app/lib/client_service/class_services.client";
import ClassSelectorModal from "./components/ClassSelectorModal";
import AddEdgeModal from "./components/AddEdgeModal";
import {
  RelatedRecordViewModel,
  useRecordRelationships,
} from "./hooks/useRecordRelationships";

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

  // UI State
  const [activeTab, setActiveTab] = useState(0);

  const [isPropertiesEditorOpen, setIsPropertiesEditorOpen] = useState(false);
  const [isSavingProperties, setIsSavingProperties] = useState(false);

  const [isClassModalOpen, setIsClassModalOpen] = useState(false);
  const [availableClasses, setAvailableClasses] = useState<ClassResponseDto[]>(
    [],
  );
  const [isLoadingClasses, setIsLoadingClasses] = useState(false);

  const {
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
    edgeRelationship,
    handleSearchRecords,
    handleCreateRelationships,
    resetRelationshipState,
    loadMoreOrigins,
    loadMoreDestinations,
    openAddEdgeModal,
  } = useRecordRelationships({
    organizationId: organization?.organizationId,
    projectId,
    recordId,
    recordName: record?.name,
    recordDataSourceId: record?.dataSourceId,
    translations: t.translations,
  });

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
    resetRelationshipState();
  }, [resetRelationshipState]);

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

  const handleTagSelectionChange = (selected: string[]) => {
    const newTags = tags.filter((tag) => selected.includes(tag.id.toString()));
    setSelectedTags(newTags);
    setSelectedIds(selected);
  };

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

  // ============= EFFECTS =============
  useEffect(() => {
    resetAllState();
  }, [recordId, resetAllState]);

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

  useEffect(() => {
    const fetchTags = async () => {
      if (!projectId || !organization?.organizationId) return;

      try {
        const data = await getAllTags(projectId);
        setTags(data);
      } catch (error) {
        console.error("Error fetching tags:", error);
      }
    };

    fetchTags();
  }, [projectId, organization?.organizationId]);

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
        onEdit: (value: string) =>
          handleUpdateRecord(
            "original_id",
            value,
            t.translations.ORIGINAL_ID_UPDATED,
          ),
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
          <span>{row.relatedRecordName || t.translations.UNKNOWN}</span>
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
              title={t.translations.ADDITIONAL_PROPERTIES}
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
                title={`${t.translations.OUTGOING_}${record.name}${t.translations.OUTGOING_ARROW}`}
                columns={relatedRecordsColumns}
                rows={originRecords}
                onLoadMore={loadMoreOrigins}
                isLoading={isLoadingOrigins && originPage > 1}
                hasMore={hasMoreOrigins}
                onAddRelationship={() => openAddEdgeModal("outgoing")}
              />
            )}

            {/* Related Records Card - Destinations */}
            {isLoadingDestinations && destinationPage === 1 ? (
              <RelatedRecordsCardSkeleton rows={6} columns={3} />
            ) : (
              <div className="mt-4">
                <RelatedRecordsCard
                  title={`${t.translations.INCOMING_}${record.name}${t.translations.INCOMING_ARROW}`}
                  columns={relatedRecordsColumns}
                  rows={destinationRecords}
                  onLoadMore={loadMoreDestinations}
                  isLoading={isLoadingDestinations && destinationPage > 1}
                  hasMore={hasMoreDestinations}
                  onAddRelationship={() => openAddEdgeModal("incoming")}
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
    {
      label:
        (t.translations as Record<string, string>).RECORD_HISTORY ||
        "Record History",
      content: (
        <RecordHistoryTab
          organizationId={organization.organizationId as number}
          projectId={projectId}
          recordId={recordId}
        />
      ),
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
      />
    </div>
  );
}
