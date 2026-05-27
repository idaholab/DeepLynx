// src/app/(home)/record/RecordViewClient.tsx

"use client";
import Tabs from "@/app/(home)/components/Tabs";
import { BetaBadge } from "@/app/(home)/components/BetaBadge";
import {
  ArrowTopRightOnSquareIcon,
  PencilIcon,
  PlusIcon,
  SparklesIcon,
} from "@heroicons/react/24/outline";
import Link from "next/link";
import React, {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from "react";
import { useRouter } from "next/navigation";
import toast from "react-hot-toast";
import {
  HistoricalRecordResponseDto,
  SensitivityLabelsDto,
  TagResponseDto,
} from "../types/responseDTOs";
import PropertyTable from "./components/PropertyTable";
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
  createClass,
  getAllClasses,
  getClass,
} from "@/app/lib/client_service/class_services.client";
import {
  getHistoricalRecord,
  getRecord,
  unattachSensitivityLabelFromRecord,
  unattachTagFromRecord,
  updateRecord,
} from "@/app/lib/client_service/record_services.client";
import { getAllSensitivityLabelsProject } from "@/app/lib/client_service/sensitivity_labels_services.client";
import { getAllTags } from "@/app/lib/client_service/tag_services.client";
import { formatLocalDateTime } from "@/app/lib/date_time";
import { isInsightSupportedFileType } from "@/app/lib/insight_file_support";
import GraphClientPage from "../graph/GraphClientPage";
import { ClassResponseDto } from "../types/responseDTOs";
import AddEdgeModal from "./components/AddEdgeModal";
import AdditionalPropertiesEditor from "./components/AdditionalPropertiesEditor";
import ClassSelectorModal from "./components/ClassSelectorModal";
import RecordHistoryTab from "./components/RecordHistoryTab";
import RecordInsightChat from "./components/RecordInsightChat";
import RecordTagsPanel from "./components/RecordTagsPanel";
import {
  RelatedRecordViewModel,
  useRecordRelationships,
} from "./hooks/useRecordRelationships";
import RelatedRecordsCardSkeleton from "./skeletons/RelatedRecordsSkeleton";
import {
  triggerLatticeExtraction,
  getEmbeddingStatus,
  queueOntologyEmbeddings,
} from "@/app/lib/client_service/lattice_services.client";
import { EmbeddingStatusResponseDTO } from "@/app/(home)/types/latticeDTOs";
import {
  fetchInsightIngestionStatus,
  queueInsightUpload,
} from "@/app/lib/client_service/insight_services.client";

// ============= HELPER FUNCTIONS =============
interface PropertyRow {
  label: string;
  value: React.ReactNode;
  editable?: boolean;
  onEdit?: (newValue: string) => void;
  isNested?: boolean;
  nestedRows?: PropertyRow[];
}

type MinimalSelectionItem = { id: number | null };

function parseMaybeJsonArray<T>(value?: string | T[] | null): T[] {
  if (!value) return [];
  return typeof value === "string" ? JSON.parse(value) : value;
}

function mapSelectedIds(items: MinimalSelectionItem[]): string[] {
  return items.filter((item) => item.id != null).map((item) => String(item.id));
}

function parseNestedProperties(obj: JSON): PropertyRow[] {
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
        nestedRows: parseNestedProperties(value),
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
  const router = useRouter();

  // ============= STATE MANAGEMENT =============
  // Record & Tags State
  const [record, setRecord] = useState<HistoricalRecordResponseDto | null>(
    null,
  );
  const [recordFileType, setRecordFileType] = useState<string | null>(null);
  const [recordClass, setRecordClass] = useState<ClassResponseDto | null>(null);
  const [tags, setTags] = useState<TagResponseDto[]>([]);
  const [selectedTags, setSelectedTags] = useState<TagResponseDto[]>([]);
  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const [labels, setLabels] = useState<SensitivityLabelsDto[]>([]);
  const [selectedLabels, setSelectedLabels] = useState<SensitivityLabelsDto[]>(
    [],
  );
  const [selectedLabelIds, setSelectedLabelIds] = useState<string[]>([]);

  // UI State
  const [activeTab, setActiveTab] = useState(0);

  const [isPropertiesEditorOpen, setIsPropertiesEditorOpen] = useState(false);
  const [isSavingProperties, setIsSavingProperties] = useState(false);

  const [isClassModalOpen, setIsClassModalOpen] = useState(false);
  const [availableClasses, setAvailableClasses] = useState<ClassResponseDto[]>(
    [],
  );
  const [isLoadingClasses, setIsLoadingClasses] = useState(false);
  const [isTriggeringLatticeExtraction, setIsTriggeringLatticeExtraction] =
    useState(false);
  const [isCheckingLatticeReadiness, setIsCheckingLatticeReadiness] =
    useState(false);
  const [isRecordInsightEmbedded, setIsRecordInsightEmbedded] = useState(false);
  const [isQueuingInsightUpload, setIsQueuingInsightUpload] = useState(false);
  const [latticeMode, setLatticeMode] = useState<"strict" | "discovery">(
    "discovery",
  );
  const [ontologyStatus, setOntologyStatus] =
    useState<EmbeddingStatusResponseDTO | null>(null);
  const [isLoadingOntologyStatus, setIsLoadingOntologyStatus] = useState(false);
  const [isQueuingOntologyEmbeddings, setIsQueuingOntologyEmbeddings] =
    useState(false);
  const [ontologyPollTrigger, setOntologyPollTrigger] = useState(0);
  const ontologyPollRef = useRef<ReturnType<typeof setInterval> | null>(null);

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

  // ============= RECORD UPDATE HANDLERS =============
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
    setRecordFileType(null);
    setSelectedTags([]);
    setSelectedIds([]);
    setSelectedLabels([]);
    setSelectedLabelIds([]);
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

  // ============= TAG/LABEL SELECTION HANDLERS =============
  const handleTagSelectionChange = (selected: string[]) => {
    const newTags = tags.filter((tag) => selected.includes(tag.id.toString()));
    setSelectedTags(newTags);
    setSelectedIds(selected);
  };

  const handleLabelSelectionChange = (selected: string[]) => {
    const newLabels = labels.filter((label) =>
      selected.includes(label.id.toString()),
    );
    setSelectedLabels(newLabels);
    setSelectedLabelIds(selected);
  };

  // ============= CLASS HANDLERS =============
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

  // ============= INITIAL/RESET EFFECTS =============
  useEffect(() => {
    resetAllState();
  }, [recordId, resetAllState]);

  useEffect(() => {
    setActiveTab(0);
  }, [recordId]);

  // ============= DATA LOADING EFFECTS =============
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
        const historicalTags = parseMaybeJsonArray<{
          id: number | null;
          name: string;
        }>(data.tags);
        const historicalLabels = parseMaybeJsonArray<{
          id: number | null;
          name: string;
        }>(data.labels);
        setSelectedIds(mapSelectedIds(historicalTags));
        setSelectedLabelIds(mapSelectedIds(historicalLabels));

        // Pull live record attachments as source of truth for current tag/label links.
        const liveRecord = await getRecord(
          organization.organizationId as number,
          projectId,
          recordId,
          true,
        );

        setRecordFileType(liveRecord.fileType ?? null);
        setSelectedIds(mapSelectedIds(liveRecord.tags ?? []));
        setSelectedLabelIds(mapSelectedIds(liveRecord.labels ?? []));
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

  // ============= DERIVED SELECTION STATE =============
  useEffect(() => {
    setSelectedTags(
      tags.filter((tag) => selectedIds.includes(tag.id.toString())),
    );
  }, [tags, selectedIds]);

  useEffect(() => {
    const fetchLabels = async () => {
      if (!projectId || !organization?.organizationId) return;

      try {
        const data = await getAllSensitivityLabelsProject(projectId, true);
        setLabels(data);
      } catch (error) {
        console.error("Error fetching labels:", error);
      }
    };

    fetchLabels();
  }, [projectId, organization?.organizationId]);

  useEffect(() => {
    setSelectedLabels(
      labels.filter((label) => selectedLabelIds.includes(label.id.toString())),
    );
  }, [labels, selectedLabelIds]);

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

  // helper function to format file size
  const formatFileSize = (bytes: number | null | undefined): string => {
    if (bytes == null) return "-";
    if (bytes === 0) return "0 bytes";
    const k = 1024;
    const sizes = ["Bytes", "KB", "MB", "GB", "TB"];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return `${parseFloat((bytes / Math.pow(k, i)).toFixed(2))} ${sizes[i]}`;
  };

  // ============= MEMOIZED VALUES =============
  const systemPropertiesRows = useMemo(() => {
    if (!record) return [];

    const isDownloadable =
      !!record.uri &&
      record.uri.trim().length > 0 &&
      record.uri.toLowerCase() !== "null";

    return [
      { label: t.translations.RECORD_ID, value: record.id },
      {
        label: t.translations.RECORD_NAME,
        value: record.name,
        editable: true,
        onEdit: (value: string) =>
          handleUpdateRecord("name", value, t.translations.RECORD_NAME_UPDATED),
        maxCharacterLimit: 100,
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
        maxCharacterLimit: 250
      },
      {
        label: t.translations.URI,
        value: record.uri,
        copyValue: record.uri ?? undefined,
        copyTooltipLabel: t.translations.COPY_URI,
        copyAriaLabel: t.translations.COPY_RECORD_URI,
        idleIconClassName: "size-6 text-base-content/70",
        copiedIconClassName: "size-6 text-success",
      },
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
      // {
      //   label: t.translations.FILE_SIZE,
      //   value: formatFileSize(record.fileSize)
      // },
      ...(isDownloadable
        ? [
          {
            label: t.translations.FILE_SIZE || "File Size",
            value: formatFileSize(record.fileSize),
          },
        ]
        : []),
      {
        label: t.translations.LAST_UPDATED_AT,
        value: formatLocalDateTime(record.lastUpdatedAt),
      },
      {
        label: t.translations.DATA_SOURCE,
        value: record.dataSourceName,
      },
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

  const handleRemoveLabel = async (labelId: number) => {
    if (!organization?.organizationId) return;

    try {
      await unattachSensitivityLabelFromRecord(
        organization.organizationId as number,
        projectId,
        recordId,
        labelId,
      );

      setSelectedLabels((prev) => prev.filter((l) => l.id !== labelId));
      setSelectedLabelIds((prev) =>
        prev.filter((id) => id !== String(labelId)),
      );

      toast.success(t.translations.SENSITIVITY_LABEL_REMOVED);
    } catch (error) {
      console.error("Error removing sensitivity label:", error);
      toast.error(t.translations.FAILED_TO_UPDATE_SENSITIVITY_LABELS);
    }
  };

  const handleTriggerLatticeExtraction = useCallback(async () => {
    if (!organization?.organizationId || !record?.dataSourceId) {
      toast.error(t.translations.LATTICE_UNABLE_TO_START_ANALYSIS);
      return;
    }

    if (latticeMode === "strict" && !isRecordInsightEmbedded) {
      toast.error(t.translations.LATTICE_STRICT_REQUIRES_EMBEDDING);
      return;
    }

    try {
      setIsTriggeringLatticeExtraction(true);

      const result = await triggerLatticeExtraction(
        organization.organizationId as number,
        projectId,
        recordId,
        {
          data_source_id: record.dataSourceId,
          mode: latticeMode,
        },
      );

      const params = new URLSearchParams({
        extractionId: String(result.extraction_id),
        projectId: String(projectId),
        organizationId: String(organization!.organizationId),
      });
      router.push(`/lattice/decisions?${params.toString()}`);
    } catch (error: any) {
      if (error?.response?.status === 400) {
        toast(t.translations.LATTICE_EMBEDDINGS_GENERATING, { icon: "⏳" });
      } else {
        console.error("Error triggering Lattice extraction:", error);
        toast.error(t.translations.LATTICE_FAILED_TO_START_ANALYSIS);
      }
    } finally {
      setIsTriggeringLatticeExtraction(false);
    }
  }, [
    organization?.organizationId,
    projectId,
    recordId,
    record?.dataSourceId,
    isRecordInsightEmbedded,
    latticeMode,
    router,
  ]);

  const handleQueueInsightUpload = useCallback(async () => {
    const uri = record?.uri?.trim();
    if (!uri || !organization?.organizationId) return;
    setIsQueuingInsightUpload(true);
    try {
      await queueInsightUpload({
        organizationId: organization.organizationId as number,
        projectId,
        fileInfo: [{ fileId: recordId, fileUri: uri }],
      });
      toast.success(t.translations.LATTICE_QUEUED_SUCCESS);
    } catch {
      toast.error(t.translations.LATTICE_QUEUE_FAILED);
    } finally {
      setIsQueuingInsightUpload(false);
    }
  }, [organization?.organizationId, projectId, record?.uri, recordId]);

  const handleQueueOntologyEmbeddings = useCallback(async () => {
    if (!organization?.organizationId) return;
    setIsQueuingOntologyEmbeddings(true);
    try {
      await queueOntologyEmbeddings(
        organization.organizationId as number,
        projectId,
      );
      toast.success(t.translations.LATTICE_ONTOLOGY_QUEUED_SUCCESS);
      setOntologyPollTrigger((n) => n + 1);
    } catch {
      toast.error(t.translations.LATTICE_ONTOLOGY_QUEUE_FAILED);
    } finally {
      setIsQueuingOntologyEmbeddings(false);
    }
  }, [
    organization?.organizationId,
    projectId,
    t.translations.LATTICE_ONTOLOGY_QUEUED_SUCCESS,
    t.translations.LATTICE_ONTOLOGY_QUEUE_FAILED,
  ]);

  const recordEmbedPollRef = useRef<ReturnType<typeof setInterval> | null>(
    null,
  );

  useEffect(() => {
    if (!organization?.organizationId || !projectId || !recordId) return;

    const POLL_INTERVAL_MS = 5000;
    let cancelled = false;
    let isInitial = true;

    const checkLatticeReadiness = async () => {
      try {
        if (isInitial) setIsCheckingLatticeReadiness(true);

        const status = await fetchInsightIngestionStatus({
          organizationId: organization.organizationId as number,
          projectId,
          fileId: recordId,
        });

        if (cancelled) return;

        setIsRecordInsightEmbedded(status.indexed);

        if (status.indexed && recordEmbedPollRef.current) {
          clearInterval(recordEmbedPollRef.current);
          recordEmbedPollRef.current = null;
        }
      } catch (error) {
        if (cancelled) return;
        console.error("Failed to check Insight embedding status:", error);
        if (isInitial) setIsRecordInsightEmbedded(false);
      } finally {
        if (!cancelled && isInitial) {
          setIsCheckingLatticeReadiness(false);
          isInitial = false;
        }
      }
    };

    void checkLatticeReadiness();

    recordEmbedPollRef.current = setInterval(() => {
      void checkLatticeReadiness();
    }, POLL_INTERVAL_MS);

    return () => {
      cancelled = true;
      if (recordEmbedPollRef.current) {
        clearInterval(recordEmbedPollRef.current);
        recordEmbedPollRef.current = null;
      }
    };
  }, [organization?.organizationId, projectId, recordId]);

  useEffect(() => {
    if (!organization?.organizationId || !projectId) return;

    const POLL_INTERVAL_MS = 5000;

    const fetchStatus = async () => {
      try {
        setIsLoadingOntologyStatus(true);
        const status = await getEmbeddingStatus(
          organization.organizationId as number,
          projectId,
        );
        setOntologyStatus(status);

        const classesDone =
          status.class_count === 0 ||
          status.embedded_class_count >= status.class_count;
        const relsDone =
          status.relationship_count === 0 ||
          status.embedded_relationship_count >= status.relationship_count;
        if (classesDone && relsDone && ontologyPollRef.current) {
          clearInterval(ontologyPollRef.current);
          ontologyPollRef.current = null;
        }
      } catch (error) {
        console.error("Failed to fetch ontology embedding status:", error);
      } finally {
        setIsLoadingOntologyStatus(false);
      }
    };

    void fetchStatus();

    ontologyPollRef.current = setInterval(() => {
      void fetchStatus();
    }, POLL_INTERVAL_MS);

    return () => {
      if (ontologyPollRef.current) {
        clearInterval(ontologyPollRef.current);
        ontologyPollRef.current = null;
      }
    };
  }, [organization?.organizationId, projectId, ontologyPollTrigger]);

  // ============= RENDER HELPERS =============
  if (!hasLoaded || !organization) {
    return <RecordLoading />;
  }

  if (!record) {
    return <RecordLoading />;
  }

  const isDownloadable =
    !!record.uri &&
    record.uri.trim().length > 0 &&
    record.uri.toLowerCase() !== "null";

  const isInsightSupported = isInsightSupportedFileType(
    recordFileType,
    record?.uri,
    record?.name,
  );

  const hasLatticeRecordRequirements =
    isInsightSupported &&
    !!record.dataSourceId &&
    !!record.uri &&
    record.uri.trim().length > 0 &&
    record.uri.toLowerCase() !== "null";

  const ontologyReady =
    ontologyStatus !== null &&
    (ontologyStatus.class_count === 0 ||
      ontologyStatus.embedded_class_count >= ontologyStatus.class_count) &&
    (ontologyStatus.relationship_count === 0 ||
      ontologyStatus.embedded_relationship_count >=
      ontologyStatus.relationship_count);

  const canTriggerLatticeExtract =
    hasLatticeRecordRequirements &&
    isRecordInsightEmbedded &&
    (latticeMode === "discovery" || ontologyReady);

  const tabs = [
    {
      label: t.translations.RECORD_INFORMATION,
      content: (
        <div className="flex flex-col xl:flex-row gap-6 mt-4">
          {/* Left Column - Properties */}
          <div className="w-full xl:w-1/2 space-y-4 pl-2">
            <PropertyTable
              title={t.translations.SYSTEM_PROPERTIES}
              rows={systemPropertiesRows}
              download={isDownloadable}
              recordName={record.name}
            />
            <PropertyTable
              title={t.translations.ADDITIONAL_PROPERTIES}
              rows={additionalPropertiesRows}
              onEditProperties={() => setIsPropertiesEditorOpen(true)}
            />
          </div>

          {/* Right Column - Tags & Relations */}
          <div className="w-full xl:w-1/2 space-y-4">
            {isInsightSupported ? (
              <RecordInsightChat
                organizationId={
                  organization?.organizationId
                    ? Number(organization.organizationId)
                    : undefined
                }
                projectId={projectId}
                recordId={record.id}
                recordName={record.name}
                recordUri={record.uri}
                onEmbeddingStatusChange={setIsRecordInsightEmbedded}
              />
            ) : null}

            {/* Tags Card */}
            <RecordTagsPanel
              tags={tags}
              selectedTags={selectedTags}
              selectedIds={selectedIds}
              labels={labels}
              selectedLabels={selectedLabels}
              selectedLabelIds={selectedLabelIds}
              onSelectionChange={handleTagSelectionChange}
              onRemoveTag={handleRemoveTag}
              onLabelSelectionChange={handleLabelSelectionChange}
              onRemoveLabel={handleRemoveLabel}
              projectId={projectId}
              recordId={recordId}
              setTags={setTags}
              setSelectedTags={setSelectedTags}
              setSelectedIds={setSelectedIds}
              setLabels={setLabels}
              setSelectedLabels={setSelectedLabels}
              setSelectedLabelIds={setSelectedLabelIds}
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
    {
      label: t.translations.LATTICE_PAGE_TITLE,
      displayLabel: (
        <span className="inline-flex items-center gap-2">
          {t.translations.LATTICE_PAGE_TITLE}
          <BetaBadge size="xs" />
        </span>
      ),
      content: (
        <div className="mt-4 flex flex-col lg:flex-row gap-8 lg:gap-12 p-6">
          {/* Left: About Lattice */}
          <div className="lg:w-2/5 space-y-5">
            <div className="flex items-center gap-2">
              <h2 className="text-lg font-semibold">
                {t.translations.LATTICE_WIDGET_TITLE}
              </h2>
            </div>
            <p className="text-sm text-base-content/60 leading-relaxed">
              {t.translations.LATTICE_WIDGET_DESCRIPTION}
            </p>
            <div className="space-y-3">
              <p className="text-xs font-semibold uppercase tracking-wide text-base-content/40">
                {t.translations.LATTICE_HOW_IT_WORKS}
              </p>
              <ol className="space-y-3">
                {[
                  t.translations.LATTICE_STEP_EMBED,
                  t.translations.LATTICE_STEP_MODE,
                  t.translations.LATTICE_STEP_TRIGGER,
                  t.translations.LATTICE_STEP_DECIDE,
                ].map((step, i) => (
                  <li
                    key={i}
                    className="flex gap-3 text-sm text-base-content/70"
                  >
                    <span className="size-5 rounded-full bg-base-300 flex items-center justify-center text-xs font-bold shrink-0 mt-0.5">
                      {i + 1}
                    </span>
                    {step}
                  </li>
                ))}
              </ol>
            </div>
          </div>

          {/* Right: Controls */}
          <div className="flex-1 space-y-5">
            {!isInsightSupported ? (
              <div className="alert alert-warning">
                <SparklesIcon className="size-5 shrink-0" />
                <span>{t.translations.LATTICE_UNSUPPORTED_FILE_TOOLTIP}</span>
              </div>
            ) : (
              <>
                {!isRecordInsightEmbedded && !isCheckingLatticeReadiness && (
                  <div className="alert alert-warning">
                    <span className="flex-1 text-sm">
                      {t.translations.LATTICE_NOT_EMBEDDED_WARNING}
                    </span>
                    <button
                      type="button"
                      className="btn btn-warning btn-sm shrink-0"
                      onClick={handleQueueInsightUpload}
                      disabled={isQueuingInsightUpload}
                    >
                      {isQueuingInsightUpload ? (
                        <span className="loading loading-spinner loading-sm" />
                      ) : (
                        t.translations.LATTICE_QUEUE_FOR_EMBEDDING
                      )}
                    </button>
                  </div>
                )}

                {/* Ontology Embedding Status */}
                <div className="space-y-2">
                  <div className="flex items-center justify-between">
                    <p className="text-sm font-medium">
                      {t.translations.LATTICE_ONTOLOGY_STATUS_TITLE}
                    </p>
                    {isLoadingOntologyStatus && !ontologyStatus && (
                      <span className="loading loading-spinner loading-xs text-base-content/40" />
                    )}
                  </div>
                  {ontologyStatus && (
                    <div className="rounded-lg border border-base-300 divide-y divide-base-300 text-sm">
                      {ontologyStatus.class_count === 0 &&
                        ontologyStatus.relationship_count === 0 ? (
                        <p className="px-4 py-3 text-base-content/50 text-xs">
                          {t.translations.LATTICE_ONTOLOGY_NO_SCHEMA}
                        </p>
                      ) : (
                        <>
                          <div className="flex items-center justify-between px-4 py-2">
                            <span className="text-base-content/70">
                              {t.translations.LATTICE_ONTOLOGY_CLASSES}
                            </span>
                            <span
                              className={
                                ontologyStatus.embedded_class_count ===
                                  ontologyStatus.class_count
                                  ? "text-success font-medium"
                                  : "text-warning font-medium"
                              }
                            >
                              {ontologyStatus.embedded_class_count}{" "}
                              {t.translations.LATTICE_ONTOLOGY_EMBEDDED_OF}{" "}
                              {ontologyStatus.class_count}{" "}
                              {t.translations.LATTICE_ONTOLOGY_EMBEDDED_LABEL}
                            </span>
                          </div>
                          <div className="flex items-center justify-between px-4 py-2">
                            <span className="text-base-content/70">
                              {t.translations.LATTICE_ONTOLOGY_RELATIONSHIPS}
                            </span>
                            <span
                              className={
                                ontologyStatus.embedded_relationship_count ===
                                  ontologyStatus.relationship_count
                                  ? "text-success font-medium"
                                  : "text-warning font-medium"
                              }
                            >
                              {ontologyStatus.embedded_relationship_count}{" "}
                              {t.translations.LATTICE_ONTOLOGY_EMBEDDED_OF}{" "}
                              {ontologyStatus.relationship_count}{" "}
                              {t.translations.LATTICE_ONTOLOGY_EMBEDDED_LABEL}
                            </span>
                          </div>
                        </>
                      )}
                    </div>
                  )}
                  {ontologyStatus &&
                    (ontologyStatus.class_count > 0 ||
                      ontologyStatus.relationship_count > 0) && (
                      <div className="flex items-start justify-between gap-3">
                        {!ontologyReady && (
                          <p className="text-xs text-base-content/50 leading-relaxed flex-1">
                            {t.translations.LATTICE_ONTOLOGY_NOT_READY}
                          </p>
                        )}
                        <button
                          type="button"
                          className="btn btn-outline btn-xs shrink-0 ml-auto"
                          onClick={handleQueueOntologyEmbeddings}
                          disabled={isQueuingOntologyEmbeddings}
                        >
                          {isQueuingOntologyEmbeddings ? (
                            <span className="loading loading-spinner loading-xs" />
                          ) : (
                            t.translations.LATTICE_QUEUE_ONTOLOGY_EMBEDDINGS
                          )}
                        </button>
                      </div>
                    )}
                </div>

                <div className="space-y-2">
                  <p className="text-sm font-medium">
                    {t.translations.LATTICE_MODE_HEADER}
                  </p>
                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                    <button
                      type="button"
                      onClick={() => setLatticeMode("discovery")}
                      disabled={isTriggeringLatticeExtraction}
                      className={`rounded-xl border-2 p-4 text-left transition-colors ${latticeMode === "discovery"
                        ? "border-primary bg-primary/5"
                        : "border-base-300 hover:border-base-content/30"
                        }`}
                    >
                      <p className="font-semibold text-sm">
                        {t.translations.LATTICE_DISCOVERY}
                      </p>
                      <p className="mt-1 text-xs text-base-content/60 leading-relaxed">
                        {t.translations.LATTICE_DISCOVERY_TOOLTIP}
                      </p>
                    </button>
                    <button
                      type="button"
                      onClick={() => setLatticeMode("strict")}
                      disabled={isTriggeringLatticeExtraction}
                      className={`rounded-xl border-2 p-4 text-left transition-colors ${latticeMode === "strict"
                        ? "border-primary bg-primary/5"
                        : "border-base-300 hover:border-base-content/30"
                        }`}
                    >
                      <p className="font-semibold text-sm">
                        {t.translations.LATTICE_STRICT}
                      </p>
                      <p className="mt-1 text-xs text-base-content/60 leading-relaxed">
                        {t.translations.LATTICE_STRICT_TOOLTIP}
                      </p>
                    </button>
                  </div>
                </div>

                <div className="flex flex-wrap items-center gap-3">
                  <button
                    type="button"
                    className="btn btn-primary btn-sm"
                    onClick={handleTriggerLatticeExtraction}
                    disabled={
                      isTriggeringLatticeExtraction ||
                      isCheckingLatticeReadiness ||
                      !canTriggerLatticeExtract
                    }
                  >
                    {isTriggeringLatticeExtraction ? (
                      <>
                        <span className="loading loading-spinner loading-sm" />{" "}
                        {t.translations.STARTING}…
                      </>
                    ) : isCheckingLatticeReadiness ? (
                      <>
                        <span className="loading loading-spinner loading-sm" />{" "}
                        {t.translations.CHECKING}…
                      </>
                    ) : (
                      <>
                        {t.translations.LATTICE_EXTRACT}{" "}
                        <ArrowTopRightOnSquareIcon className="size-4" />
                      </>
                    )}
                  </button>
                  <Link
                    href={`/lattice/decisions?projectId=${projectId}&organizationId=${organization.organizationId}`}
                    className="btn btn-ghost btn-sm"
                  >
                    {t.translations.LATTICE_VIEW_EXTRACTIONS}
                    <ArrowTopRightOnSquareIcon className="size-4" />
                  </Link>
                </div>
              </>
            )}
          </div>
        </div>
      ),
    },
  ];

  // ============= MAIN RENDER =============
  return (
    <main className="min-h-screen bg-base-200/30">
      <section className="border-b border-base-300 bg-base-100">
        <div className="mx-auto flex w-full max-w-7xl flex-col gap-5 px-3 py-5 sm:px-6 lg:px-8">
          <div className="flex flex-col gap-4 xl:flex-row xl:items-end xl:justify-between">
            <div className="min-w-0">
              <p className="text-xs font-semibold uppercase tracking-wide text-base-content/60">
                {t.translations.RECORD}
              </p>
              <h1 className="break-words text-2xl font-bold text-base-content sm:text-3xl">
                {record.name}
              </h1>
              {record.classId ? (
                <div className="mt-3 flex flex-wrap items-center gap-2">
                  <span className="badge badge-primary h-auto min-h-6 whitespace-normal break-words px-3 py-1 text-center leading-tight">
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
                  className="btn btn-sm btn-outline mt-3"
                >
                  <PlusIcon className="w-4 h-4 mr-1" />
                  {t.translations.ADD_CLASS || "Add Class"}
                </button>
              )}
            </div>
          </div>
        </div>
      </section>

      <section className="mx-auto w-full max-w-7xl px-3 py-5 sm:px-6 lg:px-8">
        <Tabs
          tabs={tabs}
          className="mx-0"
          activeTab={tabs[activeTab].label}
          onTabChange={(label) =>
            setActiveTab(tabs.findIndex((tab) => tab.label === label))
          }
          rightAction={null}
        />
      </section>
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
    </main>
  );
}
