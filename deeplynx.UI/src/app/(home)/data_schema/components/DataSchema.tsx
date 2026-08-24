"use client";

import React, { useEffect, useMemo, useState } from "react";
import toast from "react-hot-toast";
import {
  ArchiveBoxIcon,
  ArrowsRightLeftIcon,
  PencilSquareIcon,
  PlusIcon,
  XMarkIcon,
} from "@heroicons/react/24/outline";

import Tabs from "@/app/(home)/components/Tabs";
import type { CreateRelationshipRequestDto, CustomQueryRequestDto } from "@/app/(home)/types/requestDTOs";
import type {
  ClassResponseDto,
  RelationshipResponseDto,
} from "@/app/(home)/types/responseDTOs";
import { useProjectSession } from "@/app/contexts/ProjectSessionProvider";
import { formatLocalDateTime } from "@/app/lib/date_time";
import {
  archiveClass,
  createClass,
  getAllClasses,
  updateClass,
} from "@/app/lib/client_service/class_services.client";
import {
  archiveRelationship,
  createRelationship,
  getAllRelationships,
  updateRelationship,
} from "@/app/lib/client_service/relationship_services.client";
import { useLanguage } from "@/app/contexts/Language";

import ArchiveClassModal from "./ArchiveClassModal";
import { queryBuilder } from "@/app/lib/client_service/query_services.client";
import PaginationControls from "@/app/(home)/components/PaginationControls";
import { useLocalPagination } from "@/app/hooks/useLocalPagination";

type LayoutMode = "tabs";

type Selection =
  | { kind: "class"; id: number }
  | { kind: "relationship"; id: number }
  | null;

interface DataSchemaProps {
  mode: LayoutMode;
  organizationId: number | undefined;
}

function statusClass(isArchived: boolean) {
  return isArchived
    ? "badge badge-outline badge-warning"
    : "badge badge-outline badge-success";
}

function emptyState(message: string) {
  return (
    <div className="rounded-lg border border-dashed border-base-300/50 bg-base-200/30 px-4 py-10 text-center text-sm text-base-content/60">
      {message}
    </div>
  );
}

function ModalShell({
  title,
  description,
  onClose,
  children,
}: {
  title: string;
  description: string;
  onClose: () => void;
  children: React.ReactNode;
}) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
      <div className="w-full max-w-2xl rounded-2xl border border-base-300/50 bg-base-100 shadow-2xl">
        <div className="flex items-start justify-between gap-4 border-b border-base-300/50 px-6 py-5">
          <div>
            <h3 className="text-xl font-semibold text-base-content">{title}</h3>
            <p className="mt-1 text-sm text-base-content/65">{description}</p>
          </div>
          <button
            type="button"
            className="btn btn-ghost btn-sm btn-circle"
            onClick={onClose}
            aria-label="Close modal"
          >
            <XMarkIcon className="h-5 w-5" />
          </button>
        </div>
        {children}
      </div>
    </div>
  );
}

export default function DataSchema({ mode, organizationId }: DataSchemaProps) {
  const { project } = useProjectSession();
  const { t } = useLanguage();

  const [classes, setClasses] = useState<ClassResponseDto[]>([]);
  const [relationships, setRelationships] = useState<RelationshipResponseDto[]>(
    [],
  );
  const [selection, setSelection] = useState<Selection>(null);
  const [activeTab, setActiveTab] = useState("Classes");
  const [classSearch, setClassSearch] = useState("");
  const [relationshipSearch, setRelationshipSearch] = useState("");

  const [classDraft, setClassDraft] = useState({
    name: "",
    description: "",
    uuid: "",
  });
  const [isCreateClassModalOpen, setIsCreateClassModalOpen] = useState(false);
  const [isCreatingClass, setIsCreatingClass] = useState(false);
  const [isUpdatingClass, setIsUpdatingClass] = useState(false);
  const [isArchiveModalOpen, setIsArchiveModalOpen] = useState(false);
  const [isArchivingClass, setIsArchivingClass] = useState(false);
  const [archiveClassId, setArchiveClassId] = useState<number | null>(null);
  const [archiveClassAction, setArchiveClassAction] = useState<boolean>(true);
  const [recordsNumber, setRecordsNumber] = useState<number>();
  const [cachedRecordsCount, setCachedRecordsCount] = useState<Record<number, number>>({});
  const [relationshipCount, setRelationshipCount] = useState<number>();
  const [newClassDraft, setNewClassDraft] = useState({
    name: "",
    description: "",
    uuid: "",
  });
  const [relationshipDraft, setRelationshipDraft] = useState({
    name: "",
    description: "",
    uuid: "",
    originId: null as number | null,
    destinationId: null as number | null,
  });
  const [isCreateRelationshipModalOpen, setIsCreateRelationshipModalOpen] =
    useState(false);
  const [isCreatingRelationship, setIsCreatingRelationship] = useState(false);
  const [isUpdatingRelationship, setIsUpdatingRelationship] = useState(false);
  const [isArchivingRelationship, setIsArchivingRelationship] = useState(false);
  const [newRelationshipDraft, setNewRelationshipDraft] = useState({
    name: "",
    description: "",
    uuid: "",
    originId: null as number | null,
    destinationId: null as number | null,
  });

  const projectId = project?.projectId ? Number(project.projectId) : null;

  useEffect(() => {
    let cancelled = false;

    const loadClasses = async () => {
      if (!projectId) {
        setClasses([]);
        return;
      }

      try {
        const { items: classData } = await getAllClasses(projectId, false);

        if (cancelled) return;

        setClasses(classData);
      } catch (error) {
        console.error("Failed to load classes for data schema:", error);
        if (!cancelled) {
          setClasses([]);
        }
      }
    };

    loadClasses();

    return () => {
      cancelled = true;
    };
  }, [projectId]);

  useEffect(() => {
    let cancelled = false;

    const loadRelationships = async () => {
      if (!projectId) {
        setRelationships([]);
        return;
      }

      try {
        const { items: relationshipData } = await getAllRelationships(projectId, false);

        if (cancelled) return;

        setRelationships(relationshipData);
      } catch (error) {
        console.error("Failed to load relationships for data schema:", error);
        if (!cancelled) {
          setRelationships([]);
        }
      }
    };

    loadRelationships();

    return () => {
      cancelled = true;
    };
  }, [projectId]);

  useEffect(() => {
    if (!selection) {
      if (classes[0]) {
        setSelection({ kind: "class", id: classes[0].id });
      } else if (relationships[0]) {
        setSelection({ kind: "relationship", id: relationships[0].id });
      }
      return;
    }

    if (
      selection.kind === "class" &&
      !classes.some((item) => item.id === selection.id)
    ) {
      if (classes[0]) setSelection({ kind: "class", id: classes[0].id });
      else if (relationships[0]) {
        setSelection({ kind: "relationship", id: relationships[0].id });
      } else setSelection(null);
    }

    if (
      selection.kind === "relationship" &&
      !relationships.some((item) => item.id === selection.id)
    ) {
      if (relationships[0]) {
        setSelection({ kind: "relationship", id: relationships[0].id });
      } else if (classes[0]) {
        setSelection({ kind: "class", id: classes[0].id });
      } else setSelection(null);
    }
  }, [classes, relationships, selection]);

  const selectedClass = useMemo(
    () =>
      selection?.kind === "class"
        ? classes.find((item) => item.id === selection.id) ?? null
        : null,
    [classes, selection],
  );

  const selectedRelationship = useMemo(() => {

    const foundRelationship =
      selection?.kind === "relationship"
        ? relationships.find((item) => item.id === selection.id) ?? null
        : null;

    return foundRelationship;
  }, [relationships, selection]);

  useEffect(() => {
    if (!selectedClass) return;

    setClassDraft({
      name: selectedClass.name,
      description: selectedClass.description ?? "",
      uuid: selectedClass.uuid ?? "",
    });
  }, [selectedClass]);

  useEffect(() => {
    if (!selectedRelationship) {
      setRelationshipDraft({
        name: "Unassigned",
        description: "",
        uuid: "",
        originId: null,
        destinationId: null,
      });
      return;
    }

    setRelationshipDraft({
      name: selectedRelationship.name ?? "Unassigned",
      description: selectedRelationship.description ?? "",
      uuid: selectedRelationship.uuid ?? "",
      originId: selectedRelationship.originId ?? null,
      destinationId: selectedRelationship.destinationId ?? null,
    });

  }, [selectedRelationship]);

  const classLookup = useMemo(
    () => new Map(classes.map((item) => [item.id, item.name])),
    [classes],
  );

  const filteredClasses = useMemo(() => {
    const query = classSearch.trim().toLowerCase();
    if (!query) return classes;

    return classes.filter(
      (item) =>
        item.name.toLowerCase().includes(query) ||
        (item.description ?? "").toLowerCase().includes(query),
    );
  }, [classSearch, classes]);

  const filteredRelationships = useMemo(() => {
    const query = relationshipSearch.trim().toLowerCase();
    if (!query) return relationships;

    return relationships.filter((item) => {
      const originName = item.originId ? classLookup.get(item.originId) : "";
      const destinationName = item.destinationId
        ? classLookup.get(item.destinationId)
        : "";

      return (
        item.name.toLowerCase().includes(query) ||
        (item.description ?? "").toLowerCase().includes(query) ||
        originName?.toLowerCase().includes(query) ||
        destinationName?.toLowerCase().includes(query)
      );
    });
  }, [classLookup, relationshipSearch, relationships]);

  const {
    currentPage: classPage,
    pageSize: classPageSize,
    paginatedItems: paginatedClasses,
    resetPagination: resetClassPagination,
    setCurrentPage: setClassPage,
    setPageSize: setClassPageSize,
    totalPages: classTotalPages,
  } = useLocalPagination({
    items: filteredClasses,
    initialPageSize: 5,
  });

  useEffect(() => {
    resetClassPagination();
  }, [classSearch, resetClassPagination]);

  const {
    currentPage: relationshipPage,
    pageSize: relationshipPageSize,
    paginatedItems: paginatedRelationships,
    resetPagination: resetRelationshipPagination,
    setCurrentPage: setRelationshipPage,
    setPageSize: setRelationshipPageSize,
    totalPages: relationshipTotalPages,
  } = useLocalPagination({
    items: filteredRelationships,
    initialPageSize: 5,
  });

  useEffect(() => {
    resetRelationshipPagination();
  }, [relationshipSearch, resetRelationshipPagination]);

  const relationshipCountForClass = (classId: number) =>
    relationships.filter(
      (item) => item.originId === classId || item.destinationId === classId,
    ).length;

  const focusClass = (id: number) => {
    setSelection({ kind: "class", id });
    setActiveTab("Classes");
  };

  const focusRelationship = (id: number) => {
    setSelection({ kind: "relationship", id });
    setActiveTab("Relationships");
  };

  const openCreateClassModal = () => {
    setNewClassDraft({
      name: "",
      description: "",
      uuid: "",
    });
    setIsCreateClassModalOpen(true);
  };

  const handleCreateClass = async () => {
    if (!projectId) {
      toast.error("Select a project before creating a class.");
      return;
    }

    const normalizedName = newClassDraft.name.trim();
    if (!normalizedName) {
      toast.error("Class name is required.");
      return;
    }

    try {
      setIsCreatingClass(true);
      const dto = {
        name: normalizedName,
        description: newClassDraft.description.trim(),
        uuid: newClassDraft.uuid.trim() || undefined,
      };

      const createdClass = await createClass(projectId, dto);

      setClasses((previous) => [createdClass, ...previous]);
      setSelection({ kind: "class", id: createdClass.id });
      setActiveTab("Classes");
      setNewClassDraft({
        name: "",
        description: "",
        uuid: "",
      });
      setIsCreateClassModalOpen(false);
      toast.success(t.translations.CLASS_CREATED);
    } catch (error) {
      console.error("Failed to create class from data schema:", error);
      toast.error("Failed to create class.");
    } finally {
      setIsCreatingClass(false);
    }
  };

  const handleSaveClass = async () => {
    if (!projectId || !selectedClass) return;

    try {
      setIsUpdatingClass(true);
      const updatedClass = await updateClass(projectId, selectedClass.id, {
        name: classDraft.name.trim() || selectedClass.name,
        description:
          classDraft.description.trim() || (selectedClass.description ?? ""),
      });

      setClasses((previous) =>
        previous.map((item) =>
          item.id === selectedClass.id ? updatedClass : item,
        ),
      );
      toast.success(t.translations.CLASS_UPDATED_SUCCESSFULLY);
    } catch (error) {
      console.error("Failed to update class from data schema:", error);
      toast.error("Failed to update class.");
    } finally {
      setIsUpdatingClass(false);
    }
  };

  const getRecordsNumber = async (classId: number) => {
    if (!projectId || !organizationId) {
      return 0;
    }
    const cachedCount = cachedRecordsCount[classId];
    // Only trigger the API call the first time, store the record count for reuse
    if (cachedCount !== undefined) {
      setRecordsNumber(cachedCount);
      return cachedCount;
    } else {
      const query: CustomQueryRequestDto = { filter: "class_id", operator: "=", value: String(classId) };
      const records = await queryBuilder(organizationId, [query], [projectId]);

      const count = records.filter(record => record.classId === classId).length;
      setCachedRecordsCount(prev => ({
        ...prev,
        [classId]: count,
      }));

      setRecordsNumber(count);
      return count;
    }
  }

  const toggleArchiveClass = async () => {
    if (!projectId || !selectedClass) return;

    const shouldArchive = !selectedClass.isArchived;

    try {
      setIsArchivingClass(true);
      await archiveClass(projectId, selectedClass.id, shouldArchive);

      setClasses((previous) =>
        previous.map((item) =>
          item.id === selectedClass.id
            ? {
              ...item,
              isArchived: shouldArchive,
              lastUpdatedAt: new Date().toISOString(),
            }
            : item,
        ),
      );
      toast.success(
        shouldArchive
          ? t.translations.CLASS_ARCHIVED
          : t.translations.CLASS_RESTORED
      );
    } catch (error) {
      console.error("Failed to archive class from data schema:", error);
      toast.error("Failed to update class archive state.");
    } finally {
      setArchiveClassId(null);
      setIsArchivingClass(false);
      setIsArchiveModalOpen(false);
    }
  };

  const openCreateRelationshipModal = () => {
    if (classes.length === 0) return;

    setNewRelationshipDraft({
      name: "",
      description: "",
      uuid: "",
      originId: null,
      destinationId: null,
    });
    setIsCreateRelationshipModalOpen(true);
  };

  const handleCreateRelationship = async () => {
    if (!projectId) {
      toast.error("Select a project before creating a relationship.");
      return;
    }

    const normalizedName = newRelationshipDraft.name.trim();
    if (!normalizedName) {
      toast.error("Relationship name is required.");
      return;
    }

    try {
      setIsCreatingRelationship(true);
      const dto: CreateRelationshipRequestDto = {
        name: normalizedName,
        description: newRelationshipDraft.description.trim(),
        uuid: newRelationshipDraft.uuid.trim() || undefined,
        origin_id: newRelationshipDraft.originId ?? undefined,
        destination_id: newRelationshipDraft.destinationId ?? undefined,
      };

      const createdRelationship = await createRelationship(projectId, dto);

      setRelationships((previous) => [createdRelationship, ...previous]);
      setSelection({ kind: "relationship", id: createdRelationship.id });
      setActiveTab("Relationships");
      setNewRelationshipDraft({
        name: "",
        description: "",
        uuid: "",
        originId: null,
        destinationId: null,
      });
      setIsCreateRelationshipModalOpen(false);
      toast.success(t.translations.RELATIONSHIP_CREATED);
    } catch (error) {
      console.error("Failed to create relationship from data schema:", error);
      toast.error("Failed to create relationship.");
    } finally {
      setIsCreatingRelationship(false);
    }
  };

  const handleSaveRelationship = async () => {
    if (!projectId || !selectedRelationship) return;

    try {
      setIsUpdatingRelationship(true);
      const updatedRelationship = await updateRelationship(
        projectId,
        selectedRelationship.id,
        {
          name: relationshipDraft.name.trim() || selectedRelationship.name,
          description:
            relationshipDraft.description.trim() ||
            (selectedRelationship.description ?? ""),
          origin_id: relationshipDraft.originId ?? null,
          destination_id: relationshipDraft.destinationId ?? null,
        },
      );

      setRelationships((previous) =>
        previous.map((item) =>
          item.id === selectedRelationship.id ? updatedRelationship : item,
        ),
      );
      toast.success(t.translations.RELATIONSHIP_UPDATED);
    } catch (error) {
      console.error("Failed to update relationship from data schema:", error);
      toast.error("Failed to update relationship.");
    } finally {
      setIsUpdatingRelationship(false);
    }
  };

  const toggleArchiveRelationship = async () => {
    if (!projectId || !selectedRelationship) return;

    const shouldArchive = !selectedRelationship.isArchived;

    try {
      setIsArchivingRelationship(true);
      await archiveRelationship(projectId, selectedRelationship.id, shouldArchive);

      setRelationships((previous) =>
        previous.map((item) =>
          item.id === selectedRelationship.id
            ? {
              ...item,
              isArchived: shouldArchive,
              lastUpdatedAt: new Date().toISOString(),
            }
            : item,
        ),
      );
      toast.success(
        shouldArchive
          ? t.translations.RELATIONSHIP_ARCHIVED
          : t.translations.RELATIONSHIP_RESTORED
      );
    } catch (error) {
      console.error("Failed to archive relationship from data schema:", error);
      toast.error("Failed to update relationship archive state.");
    } finally {
      setIsArchivingRelationship(false);
    }
  };

  const classesPanel = (
    <div className="card border border-base-300/50 bg-base-100 shadow-sm">
      <div className="card-body gap-4">
        <div className="flex flex-col gap-3 xl:flex-row xl:items-center xl:justify-between">
          <div>
            <h2 className="text-lg font-semibold text-base-content">{t.translations.CLASSES}</h2>
            <p className="text-sm text-base-content/60">
              {t.translations.CREATE_REVIEW_ARCHIVE_AND_REFINE_RECORD_CLASSES}
            </p>
          </div>
          <div className="flex gap-2">
            <input
              className="input input-bordered input-sm w-full xl:w-60"
              placeholder={t.translations.SEARCH_CLASSES}
              value={classSearch}
              onChange={(event) => setClassSearch(event.target.value)}
            />
            <button
              className="btn btn-primary btn-sm"
              onClick={openCreateClassModal}
            >
              <PlusIcon className="h-4 w-4" />
              {t.translations.NEW}
            </button>
          </div>
        </div>

        {filteredClasses.length === 0 ? (
          emptyState(
            classSearch.trim()
              ? t.translations.NO_CLASSES_MATCH_CURRENT_FILTER
              : t.translations.NO_CLASSES_FOUND_IN_DATABASE,
          )
        ) : (
          <>
            <div className="overflow-x-auto rounded-lg border border-base-300/50">
              <table className="table">
                <thead className="bg-base-200">
                  <tr>
                    <th className="sticky top-0 z-10 bg-base-200">{t.translations.NAME}</th>
                    <th className="sticky top-0 z-10 bg-base-200">{t.translations.STATUS}</th>
                    <th className="sticky top-0 z-10 bg-base-200">{t.translations.UPDATED}</th>
                  </tr>
                </thead>
                <tbody>
                  {paginatedClasses.map((item) => {
                    const isSelected =
                      selection?.kind === "class" && selection.id === item.id;

                    return (
                      <tr
                        key={item.id}
                        className={`cursor-pointer transition-colors ${isSelected ? "bg-primary/10" : "hover"
                          }`}
                        onClick={() => focusClass(item.id)}
                      >
                        <td>
                          <div className="font-medium">{item.name}</div>
                        </td>
                        <td>
                          <span className={statusClass(item.isArchived)}>
                            {item.isArchived ? t.translations.ARCHIVED : t.translations.ACTIVE}
                          </span>
                        </td>
                        <td className="text-sm text-base-content/70">
                          {formatLocalDateTime(item.lastUpdatedAt ?? item.createdat)}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>

            <PaginationControls
              currentPage={classPage}
              pageSize={classPageSize}
              totalPages={classTotalPages}
              onPageChange={setClassPage}
              onPageSizeChange={setClassPageSize}
            />
          </>
        )}
      </div>
    </div>
  );

  const relationshipsPanel = (
    <div className="card border border-base-300/50 bg-base-100 shadow-sm">
      <div className="card-body gap-4">
        <div className="flex flex-col gap-3 xl:flex-row xl:items-center xl:justify-between">
          <div>
            <h2 className="text-lg font-semibold text-base-content">
              {t.translations.RELATIONSHIPS}
            </h2>
            <p className="text-sm text-base-content/60">
              {t.translations.DEFINE_WHICH_CLASSES_CAN_CONNECT_AND_HOW_EDGE_SHOULD_READ}
            </p>
          </div>
          <div className="flex gap-2">
            <input
              className="input input-bordered input-sm w-full xl:w-60"
              placeholder={t.translations.SEARCH_RELATIONSHIPS}
              value={relationshipSearch}
              onChange={(event) => setRelationshipSearch(event.target.value)}
            />
            <button
              className="btn btn-primary btn-sm"
              onClick={openCreateRelationshipModal}
              disabled={classes.length === 0}
            >
              <PlusIcon className="h-4 w-4" />
              {t.translations.NEW}
            </button>
          </div>
        </div>

        {filteredRelationships.length === 0 ? (
          emptyState(
            relationshipSearch.trim()
              ? t.translations.NO_RELATIONSHIPS_MATCH_CURRENT_FILTER
              : t.translations.NO_RELATIONSHIPS_FOUND_IN_DATABASE,
          )
        ) : (
          <>
            <div className="overflow-x-auto rounded-lg border border-base-300/50">
              <table className="table">
                <thead className="bg-base-200">
                  <tr>
                    <th className="sticky top-0 z-10 bg-base-200">{t.translations.NAME}</th>
                    <th className="sticky top-0 z-10 bg-base-200">{t.translations.DIRECTION}</th>
                    <th className="sticky top-0 z-10 bg-base-200">{t.translations.STATUS}</th>
                  </tr>
                </thead>
                <tbody>
                  {paginatedRelationships.map((item) => {
                    const isSelected =
                      selection?.kind === "relationship" &&
                      selection.id === item.id;

                    return (
                      <tr
                        key={item.id}
                        className={`cursor-pointer transition-colors ${isSelected ? "bg-primary/10" : "hover"
                          }`}
                        onClick={() => focusRelationship(item.id)}
                      >
                        <td>
                          <div className="font-medium">{item.name}</div>
                        </td>
                        <td className="text-sm text-base-content/70">
                          {(item.originId && classLookup.get(item.originId)) ||
                            t.translations.UNASSIGNED}
                          {" -> "}
                          {(item.destinationId &&
                            classLookup.get(item.destinationId)) ||
                            t.translations.UNASSIGNED}
                        </td>
                        <td>
                          <span className={statusClass(item.isArchived)}>
                            {item.isArchived ? t.translations.ARCHIVED_BADGE : t.translations.ACTIVE}
                          </span>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>

            <PaginationControls
              currentPage={relationshipPage}
              pageSize={relationshipPageSize}
              totalPages={relationshipTotalPages}
              onPageChange={setRelationshipPage}
              onPageSizeChange={setRelationshipPageSize}
            />
          </>
        )}
      </div>
    </div>
  );

  const classInspector = selectedClass ? (
    <div className="card border border-base-300/50 bg-base-100 shadow-sm">
      <div className="card-body gap-4">
        <div className="flex items-start justify-between gap-3">
          <div>
            <h3 className="text-lg font-semibold text-base-content">
              {t.translations.CLASS_INSPECTOR}
            </h3>
            <p className="text-sm text-base-content/60">
              {t.translations.EDIT_SELECTED_CLASS_COMPARE_LINKED_RELATIONSHIPS}
            </p>
          </div>
          <span className={statusClass(selectedClass.isArchived)}>
            {selectedClass.isArchived
              ? t.translations.ARCHIVED_BADGE
              : t.translations.ACTIVE}
          </span>
        </div>

        <div className="grid gap-4 md:grid-cols-2">
          <label className="form-control">
            <span className="mb-2 block text-sm font-medium text-base-content">
              {t.translations.NAME}
            </span>
            <input
              className="input input-bordered"
              value={classDraft.name}
              onChange={(event) =>
                setClassDraft((previous) => ({
                  ...previous,
                  name: event.target.value,
                }))
              }
            />
          </label>
          <label className="form-control">
            <span className="mb-2 block text-sm font-medium text-base-content">
              UUID
            </span>
            <input
              className="input input-bordered bg-base-200/50"
              value={classDraft.uuid}
              readOnly
            />
          </label>
        </div>

        <label className="form-control">
          <span className="mb-2 block text-sm font-medium text-base-content">
            {t.translations.DESCRIPTION}
          </span>
          <textarea
            className="textarea textarea-bordered min-h-28"
            value={classDraft.description}
            onChange={(event) =>
              setClassDraft((previous) => ({
                ...previous,
                description: event.target.value,
              }))
            }
          />
        </label>

        <div className="grid gap-3">
          <div className="rounded-lg border border-base-300/50 bg-base-200/50 p-3">
            <div className="text-xs uppercase tracking-wide text-base-content/60">
              {t.translations.LAST_UPDATED}
            </div>
            <div className="mt-1 text-sm font-medium">
              {formatLocalDateTime(
                selectedClass.lastUpdatedAt ?? selectedClass.createdat,
              )}
            </div>
          </div>
        </div>

        <div className="flex flex-wrap gap-2">
          <button
            className="btn btn-primary btn-sm"
            onClick={handleSaveClass}
            disabled={isUpdatingClass}
          >
            <PencilSquareIcon className="h-4 w-4" />
            {isUpdatingClass ? t.translations.UPDATING : t.translations.UPDATE}
          </button>
          <button
            className="btn btn-outline btn-warning btn-sm"
            onClick={async () => {
              const relationships = relationshipCountForClass(selectedClass.id);
              const records = await getRecordsNumber(selectedClass.id);
              if (relationships == 0 && records == 0 || selectedClass.isArchived) {
                // no warning, just toggle archive/unarchive
                await toggleArchiveClass();
              }
              else {
                // warn about relationships and records
                setRelationshipCount(relationships);
                setIsArchiveModalOpen(true);
                setArchiveClassId(selectedClass.id);
                setArchiveClassAction(!selectedClass.isArchived);
              }
            }}
            disabled={isArchivingClass}
          >
            <ArchiveBoxIcon className="h-4 w-4" />
            {isArchivingClass
              ? t.translations.UPDATING
              : selectedClass.isArchived
                ? t.translations.RESTORE
                : t.translations.ARCHIVE}
          </button>
        </div>
      </div>
    </div>
  ) : (
    emptyState(t.translations.SELECT_CLASS_TO_INSPECT_AND_EDIT)
  );

  const relationshipInspector = selectedRelationship ? (
    <div className="card border border-base-300/50 bg-base-100 shadow-sm">
      <div className="card-body gap-4">
        <div className="flex items-start justify-between gap-3">
          <div>
            <h3 className="text-lg font-semibold text-base-content">
              {t.translations.RELATIONSHIP_INSPECTOR}
            </h3>
            <p className="text-sm text-base-content/60">
              {t.translations.MIRROR_ADD_EDGE_WORKFLOW}
            </p>
          </div>
          <span className={statusClass(selectedRelationship.isArchived)}>
            {selectedRelationship.isArchived
              ? t.translations.ARCHIVED_BADGE
              : t.translations.ACTIVE}
          </span>
        </div>

        <div className="grid gap-4 md:grid-cols-2">
          <label className="form-control">
            <span className="mb-2 block text-sm font-medium text-base-content">
              {t.translations.NAME}
            </span>
            <input
              className="input input-bordered"
              value={relationshipDraft.name}
              onChange={(event) =>
                setRelationshipDraft((previous) => ({
                  ...previous,
                  name: event.target.value,
                }))
              }
            />
          </label>
          <label className="form-control">
            <span className="mb-2 block text-sm font-medium text-base-content">
              {t.translations.UUID}
            </span>
            <input
              className="input input-bordered bg-base-200/50"
              value={relationshipDraft.uuid}
              readOnly
            />
          </label>
        </div>

        <div className="grid gap-4 md:grid-cols-2">
          <label className="form-control">
            <span className="mb-2 block text-sm font-medium text-base-content">
              {t.translations.ORIGIN_CLASS}
            </span>
            <select
              className="select select-bordered"
              value={relationshipDraft.originId ?? ""}
              onChange={(event) => {
                const newValue = event.target.value ? Number(event.target.value) : null;
                setRelationshipDraft((previous) => ({
                  ...previous,
                  originId: newValue,
                }));
              }}
            >
              <option value="">{t.translations.UNASSIGNED}</option>
              {classes.map((item) => (
                <option key={item.id} value={item.id}>
                  {item.name}
                </option>
              ))}
            </select>
          </label>
          <label className="form-control">
            <span className="mb-2 block text-sm font-medium text-base-content">
              {t.translations.DESTINATION_CLASS}
            </span>
            <select
              className="select select-bordered"
              value={relationshipDraft.destinationId ?? ""}
              onChange={(event) => {
                const newValue = event.target.value ? Number(event.target.value) : null;
                setRelationshipDraft((previous) => ({
                  ...previous,
                  destinationId: newValue,
                }));
              }}
            >
              <option value="">{t.translations.UNASSIGNED}</option>
              {classes.map((item) => (
                <option key={item.id} value={item.id}>
                  {item.name}
                </option>
              ))}
            </select>
          </label>
        </div>

        <label className="form-control">
          <span className="mb-2 block text-sm font-medium text-base-content">
            {t.translations.DESCRIPTION}
          </span>
          <textarea
            className="textarea textarea-bordered min-h-28"
            value={relationshipDraft.description}
            onChange={(event) =>
              setRelationshipDraft((previous) => ({
                ...previous,
                description: event.target.value,
              }))
            }
          />
        </label>

        <div className="grid gap-3 md:grid-cols-2">
          <div className="rounded-lg border border-base-300/50 bg-base-200/50 p-3">
            <div className="text-xs uppercase tracking-wide text-base-content/60">
              {t.translations.ORIGIN_TO_DESTINATION}
            </div>
            <div className="mt-1 text-sm font-medium">
              {(relationshipDraft.originId &&
                classLookup.get(relationshipDraft.originId)) ||
                t.translations.UNASSIGNED}
              {" -> "}
              {(relationshipDraft.destinationId &&
                classLookup.get(relationshipDraft.destinationId)) ||
                t.translations.UNASSIGNED}
            </div>
          </div>
          <div className="rounded-lg border border-base-300/50 bg-base-200/50 p-3">
            <div className="text-xs uppercase tracking-wide text-base-content/60">
              {t.translations.LAST_UPDATED}
            </div>
            <div className="mt-1 text-sm font-medium">
              {formatLocalDateTime(selectedRelationship.lastUpdatedAt)}
            </div>
          </div>
        </div>

        <div className="flex flex-wrap gap-2">
          <button
            className="btn btn-primary btn-sm"
            onClick={handleSaveRelationship}
            disabled={isUpdatingRelationship}
          >
            <PencilSquareIcon className="h-4 w-4" />
            {isUpdatingRelationship ? t.translations.UPDATING : t.translations.UPDATE}
          </button>
          <button
            className="btn btn-outline btn-warning btn-sm"
            onClick={toggleArchiveRelationship}
            disabled={isArchivingRelationship}
          >
            <ArchiveBoxIcon className="h-4 w-4" />
            {isArchivingRelationship
              ? t.translations.UPDATING
              : selectedRelationship.isArchived
                ? t.translations.RESTORE
                : t.translations.ARCHIVE}
          </button>
        </div>
      </div>
    </div>
  ) : (
    emptyState(t.translations.SELECT_RELATIONSHIP_TO_INSPECT_AND_EDIT)
  );

  const boardPanel = (
    <div className="card border border-base-300/50 bg-base-100 shadow-sm">
      <div className="card-body gap-4">
        <div>
          <h2 className="text-lg font-semibold text-base-content">
            {t.translations.RELATIONSHIP_FLOW}
          </h2>
          <p className="text-sm text-base-content/60">
            {t.translations.RELATIONSHIP_FLOW_DESCRIPTION}
          </p>
        </div>

        <div className="space-y-3">
          {filteredRelationships.length === 0
            ? emptyState(t.translations.NO_RELATIONSHIP_FLOWS_AVAILABLE)
            : filteredRelationships.map((item) => {
              const isSelected =
                selection?.kind === "relationship" &&
                selection.id === item.id;

              return (
                <button
                  key={item.id}
                  type="button"
                  className={`w-full rounded-xl border p-4 text-left transition ${isSelected
                    ? "border-primary bg-primary/10"
                    : "border-base-300/50 bg-base-100 hover:border-primary/40 hover:bg-base-200/40"
                    }`}
                  onClick={() => focusRelationship(item.id)}
                >
                  <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
                    <div>
                      <div className="font-semibold text-base-content">
                        {item.name}
                      </div>
                      <div className="mt-1 text-sm text-base-content/60">
                        {item.description}
                      </div>
                    </div>
                    <span className={statusClass(item.isArchived)}>
                      {item.isArchived
                        ? t.translations.ARCHIVED_BADGE
                        : t.translations.ACTIVE}
                    </span>
                  </div>

                  <div className="mt-4 flex flex-wrap items-center gap-2 text-sm">
                    <span className="badge badge-outline">
                      {(item.originId && classLookup.get(item.originId)) ||
                        t.translations.ORIGIN}
                    </span>
                    <ArrowsRightLeftIcon className="h-4 w-4 text-base-content/50" />
                    <span className="badge badge-primary badge-outline">
                      {item.name}
                    </span>
                    <ArrowsRightLeftIcon className="h-4 w-4 text-base-content/50" />
                    <span className="badge badge-outline">
                      {(item.destinationId &&
                        classLookup.get(item.destinationId)) ||
                        t.translations.DESTINATION}
                    </span>
                  </div>
                </button>
              );
            })}
        </div>
      </div>
    </div>
  );

  const splitContent = (
    <div className="grid gap-6 xl:grid-cols-2">
      <div className="space-y-6">
        {classesPanel}
        {classInspector}
      </div>
      <div className="space-y-6">
        {relationshipsPanel}
        {relationshipInspector}
      </div>
    </div>
  );

  const tabContent = (
    <Tabs
      activeTab={
        activeTab === "Classes"
          ? t.translations.CLASSES
          : t.translations.RELATIONSHIPS
      }
      onTabChange={(tab) =>
        setActiveTab(tab === t.translations.CLASSES ? "Classes" : "Relationships")
      }
      tabs={[
        {
          label: t.translations.CLASSES,
          content: (
            <div className="mt-4 grid gap-6 xl:grid-cols-[1.2fr_1fr]">
              {classesPanel}
              {classInspector}
            </div>
          ),
        },
        {
          label: t.translations.RELATIONSHIPS,
          content: (
            <div className="mt-4 grid gap-6 xl:grid-cols-[1.2fr_1fr]">
              {relationshipsPanel}
              {relationshipInspector}
            </div>
          ),
        },
      ]}
    />
  );

  const boardContent = (
    <div className="grid gap-6 xl:grid-cols-[1fr_1.2fr_1fr]">
      {classesPanel}
      {boardPanel}
      <div>{selection?.kind === "relationship" ? relationshipInspector : classInspector}</div>
    </div>
  );

  const contentByMode = {
    split: splitContent,
    tabs: tabContent,
    board: boardContent,
  } as const;

  return (
    <main className="min-h-screen bg-base-200/30">
      <section className="border-b border-base-300/50 bg-base-100">
        <div className="mx-auto flex w-full max-w-7xl flex-col gap-5 px-3 py-5 sm:px-6 lg:px-8">
          <div>
            <p className="text-xs font-semibold uppercase tracking-wide text-base-content/60">
              {t.translations.DATA_SCHEMA}
            </p>
            <h1 className="break-words text-2xl font-bold text-base-content sm:text-3xl">
              {t.translations.DATA_SCHEMA}
            </h1>
            <p className="mt-3 max-w-4xl text-base-content/70">
              {t.translations.DATA_SCHEMA_DESCRIPTION}
            </p>
          </div>
        </div>
      </section>

      <section className="mx-auto w-full max-w-7xl space-y-6 px-3 py-5 sm:px-6 lg:px-8">
        {contentByMode[mode]}
      </section>

      {isCreateClassModalOpen ? (
        <ModalShell
          title={t.translations.CREATE_CLASS}
          description={t.translations.CAPTURE_CORE_CLASS_DEFINITION}
          onClose={() => setIsCreateClassModalOpen(false)}
        >
          <div className="space-y-5 px-6 py-5">
            <div className="grid gap-4 md:grid-cols-2">
              <label className="form-control">
                <span className="label-text mb-2 text-sm font-medium">
                  {t.translations.CLASS_NAME}
                </span>
                <input
                  className="input input-bordered"
                  placeholder={t.translations.EXAMPLE_ASSET}
                  value={newClassDraft.name}
                  onChange={(event) =>
                    setNewClassDraft((previous) => ({
                      ...previous,
                      name: event.target.value,
                    }))
                  }
                />
              </label>
              <label className="form-control">
                <span className="label-text mb-2 text-sm font-medium">
                  {t.translations.UUID}
                </span>
                <input
                  className="input input-bordered"
                  placeholder={t.translations.OPTIONAL_AUTO_GENERATED_IF_LEFT_BLANK}
                  value={newClassDraft.uuid}
                  onChange={(event) =>
                    setNewClassDraft((previous) => ({
                      ...previous,
                      uuid: event.target.value,
                    }))
                  }
                />
              </label>
            </div>

            <label className="form-control">
              <span className="mb-2 block text-sm font-medium text-base-content">
                {t.translations.DESCRIPTION}
              </span>
              <textarea
                className="textarea textarea-bordered min-h-28"
                placeholder={t.translations.CLASS_DESCRIPTION_HELP}
                value={newClassDraft.description}
                onChange={(event) =>
                  setNewClassDraft((previous) => ({
                    ...previous,
                    description: event.target.value,
                  }))
                }
              />
            </label>

          </div>

          <div className="flex justify-end gap-3 border-t border-base-200 px-6 py-4">
            <button
              type="button"
              className="btn btn-ghost"
              onClick={() => setIsCreateClassModalOpen(false)}
              disabled={isCreatingClass}
            >
              {t.translations.CANCEL}
            </button>
            <button
              type="button"
              className="btn btn-primary"
              onClick={handleCreateClass}
              disabled={!newClassDraft.name.trim() || isCreatingClass}
            >
              {isCreatingClass ? t.translations.CREATING : t.translations.CREATE_CLASS}
            </button>
          </div>
        </ModalShell>
      ) : null}

      {isCreateRelationshipModalOpen ? (
        <ModalShell
          title={t.translations.CREATE_RELATIONSHIP}
          description={t.translations.CREATE_RELATIONSHIP_DESCRIPTION}
          onClose={() => setIsCreateRelationshipModalOpen(false)}
        >
          <div className="space-y-5 px-6 py-5">
            <div className="grid gap-4 md:grid-cols-2">
              <label className="form-control">
                <span className="label-text mb-2 text-sm font-medium">
                  {t.translations.RELATIONSHIP_NAME}
                </span>
                <input
                  className="input input-bordered"
                  placeholder={t.translations.EXAMPLE_INSTALLED_IN}
                  value={newRelationshipDraft.name}
                  onChange={(event) =>
                    setNewRelationshipDraft((previous) => ({
                      ...previous,
                      name: event.target.value,
                    }))
                  }
                />
              </label>
              <label className="form-control">
                <span className="label-text mb-2 text-sm font-medium">
                  {t.translations.UUID}
                </span>
                <input
                  className="input input-bordered"
                  placeholder={t.translations.OPTIONAL_AUTO_GENERATED_IF_LEFT_BLANK}
                  value={newRelationshipDraft.uuid}
                  onChange={(event) =>
                    setNewRelationshipDraft((previous) => ({
                      ...previous,
                      uuid: event.target.value,
                    }))
                  }
                />
              </label>
            </div>

            <div className="grid gap-4 md:grid-cols-2">
              <label className="form-control">
                <span className="label-text mb-2 text-sm font-medium">
                  {t.translations.ORIGIN_CLASS}
                </span>
                <select
                  className="select select-bordered"
                  value={newRelationshipDraft.originId ?? ""}
                  onChange={(event) =>
                    setNewRelationshipDraft((previous) => ({
                      ...previous,
                      originId: event.target.value
                        ? Number(event.target.value)
                        : null,
                    }))
                  }
                >
                  <option value="">{t.translations.SELECT_CLASS}</option>
                  {classes.map((item) => (
                    <option key={item.id} value={item.id}>
                      {item.name}
                    </option>
                  ))}
                </select>
              </label>
              <label className="form-control">
                <span className="label-text mb-2 text-sm font-medium">
                  {t.translations.DESTINATION_CLASS}
                </span>
                <select
                  className="select select-bordered"
                  value={newRelationshipDraft.destinationId ?? ""}
                  onChange={(event) =>
                    setNewRelationshipDraft((previous) => ({
                      ...previous,
                      destinationId: event.target.value
                        ? Number(event.target.value)
                        : null,
                    }))
                  }
                >
                  <option value="">{t.translations.SELECT_CLASS}</option>
                  {classes.map((item) => (
                    <option key={item.id} value={item.id}>
                      {item.name}
                    </option>
                  ))}
                </select>
              </label>
            </div>

            <label className="form-control">
              <span className="mb-2 block text-sm font-medium text-base-content">
                {t.translations.DESCRIPTION}
              </span>
              <textarea
                className="textarea textarea-bordered min-h-28 mb-4"
                placeholder={t.translations.RELATIONSHIP_DESCRIPTION_HELP}
                value={newRelationshipDraft.description}
                onChange={(event) =>
                  setNewRelationshipDraft((previous) => ({
                    ...previous,
                    description: event.target.value,
                  }))
                }
              />
            </label>

            <div className="rounded-xl border border-base-300 bg-base-200/40 p-4 text-sm text-base-content/70">
              {t.translations.PREVIEW}:
              <span className="ml-2 font-medium text-base-content">
                {(newRelationshipDraft.originId &&
                  classLookup.get(newRelationshipDraft.originId)) ||
                  t.translations.ORIGIN}
                {" -> "}
                {newRelationshipDraft.name.trim() || t.translations.RELATIONSHIP}
                {" -> "}
                {(newRelationshipDraft.destinationId &&
                  classLookup.get(newRelationshipDraft.destinationId)) ||
                  t.translations.DESTINATION}
              </span>
            </div>
          </div>

          <div className="flex justify-end gap-3 border-t border-base-200 px-6 py-4">
            <button
              type="button"
              className="btn btn-ghost"
              onClick={() => setIsCreateRelationshipModalOpen(false)}
              disabled={isCreatingRelationship}
            >
              {t.translations.CANCEL}
            </button>
            <button
              type="button"
              className="btn btn-primary"
              onClick={handleCreateRelationship}
              disabled={
                isCreatingRelationship ||
                !newRelationshipDraft.name.trim()
              }
            >
              {isCreatingRelationship
                ? t.translations.CREATING
                : t.translations.CREATE_RELATIONSHIP}
            </button>
          </div>
        </ModalShell>
      ) : null}
      <ArchiveClassModal
        isOpen={isArchiveModalOpen !== false}
        onToggle={(value) => {
          setArchiveClassId(value ? archiveClassId : null);
          setIsArchiveModalOpen(false);
        }}
        archiveAction={archiveClassAction}
        onArchive={toggleArchiveClass}
        recordsForClass={recordsNumber}
        relationshipCount={relationshipCount}
      />
    </main>
  );
}