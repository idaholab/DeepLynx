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
import type { CreateRelationshipRequestDto } from "@/app/(home)/types/requestDTOs";
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

type LayoutMode = "tabs";

type Selection =
  | { kind: "class"; id: number }
  | { kind: "relationship"; id: number }
  | null;

interface DataSchemaProps {
  mode: LayoutMode;
}

const modeDescriptions: Record<LayoutMode, string> = {
  tabs:
    "Creation and management of classes and relationships to assign to records and edges",
};

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

export default function DataSchema({ mode }: DataSchemaProps) {
  const { project } = useProjectSession();

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
  const [isArchivingClass, setIsArchivingClass] = useState(false);
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
        const classData = await getAllClasses(projectId, false);

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
        const relationshipData = await getAllRelationships(projectId, false);

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

  const selectedRelationship = useMemo(
    () =>
      selection?.kind === "relationship"
        ? relationships.find((item) => item.id === selection.id) ?? null
        : null,
    [relationships, selection],
  );

  useEffect(() => {
    if (!selectedClass) return;

    setClassDraft({
      name: selectedClass.name,
      description: selectedClass.description ?? "",
      uuid: selectedClass.uuid ?? "",
    });
  }, [selectedClass]);

  useEffect(() => {
    if (!selectedRelationship) return;

    setRelationshipDraft({
      name: selectedRelationship.name,
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
      toast.success("Class created.");
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
      toast.success("Class updated.");
    } catch (error) {
      console.error("Failed to update class from data schema:", error);
      toast.error("Failed to update class.");
    } finally {
      setIsUpdatingClass(false);
    }
  };

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
      toast.success(shouldArchive ? "Class archived." : "Class restored.");
    } catch (error) {
      console.error("Failed to archive class from data schema:", error);
      toast.error("Failed to update class archive state.");
    } finally {
      setIsArchivingClass(false);
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
      toast.success("Relationship created.");
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
          origin_id: relationshipDraft.originId ?? undefined,
          destination_id: relationshipDraft.destinationId ?? undefined,
        },
      );

      setRelationships((previous) =>
        previous.map((item) =>
          item.id === selectedRelationship.id ? updatedRelationship : item,
        ),
      );
      toast.success("Relationship updated.");
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
        shouldArchive ? "Relationship archived." : "Relationship restored.",
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
            <h2 className="text-lg font-semibold text-base-content">Classes</h2>
            <p className="text-sm text-base-content/60">
              Create, review, archive, and refine record classes.
            </p>
          </div>
          <div className="flex gap-2">
            <input
              className="input input-bordered input-sm w-full xl:w-60"
              placeholder="Search classes"
              value={classSearch}
              onChange={(event) => setClassSearch(event.target.value)}
            />
            <button
              className="btn btn-primary btn-sm"
              onClick={openCreateClassModal}
            >
              <PlusIcon className="h-4 w-4" />
              New
            </button>
          </div>
        </div>

        {filteredClasses.length === 0 ? (
          emptyState(
            classSearch.trim()
              ? "No classes match the current filter."
              : "No classes were found in the database.",
          )
        ) : (
          <div
            className={`overflow-x-auto rounded-lg border border-base-300/50 ${
              filteredClasses.length > 5 ? "max-h-[22rem] overflow-y-auto" : ""
            }`}
          >
            <table className="table">
              <thead className="bg-base-200">
                <tr>
                  <th className="sticky top-0 z-10 bg-base-200">Name</th>
                  <th className="sticky top-0 z-10 bg-base-200">Status</th>
                  <th className="sticky top-0 z-10 bg-base-200">Updated</th>
                </tr>
              </thead>
              <tbody>
                {filteredClasses.map((item) => {
                  const isSelected =
                    selection?.kind === "class" && selection.id === item.id;

                  return (
                    <tr
                      key={item.id}
                      className={`cursor-pointer transition-colors ${
                        isSelected ? "bg-primary/10" : "hover"
                      }`}
                      onClick={() => focusClass(item.id)}
                    >
                      <td>
                        <div className="font-medium">{item.name}</div>
                      </td>
                      <td>
                        <span className={statusClass(item.isArchived)}>
                          {item.isArchived ? "Archived" : "Active"}
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
              Relationships
            </h2>
            <p className="text-sm text-base-content/60">
              Define which classes can connect and how that edge should read.
            </p>
          </div>
          <div className="flex gap-2">
            <input
              className="input input-bordered input-sm w-full xl:w-60"
              placeholder="Search relationships"
              value={relationshipSearch}
              onChange={(event) => setRelationshipSearch(event.target.value)}
            />
            <button
              className="btn btn-primary btn-sm"
              onClick={openCreateRelationshipModal}
              disabled={classes.length === 0}
            >
              <PlusIcon className="h-4 w-4" />
              New
            </button>
          </div>
        </div>

        {filteredRelationships.length === 0 ? (
          emptyState(
            relationshipSearch.trim()
              ? "No relationships match the current filter."
              : "No relationships were found in the database.",
          )
        ) : (
          <div
            className={`overflow-x-auto rounded-lg border border-base-300/50 ${
              filteredRelationships.length > 5
                ? "max-h-[22rem] overflow-y-auto"
                : ""
            }`}
          >
            <table className="table">
              <thead className="bg-base-200">
                <tr>
                  <th className="sticky top-0 z-10 bg-base-200">Name</th>
                  <th className="sticky top-0 z-10 bg-base-200">Direction</th>
                  <th className="sticky top-0 z-10 bg-base-200">Status</th>
                </tr>
              </thead>
              <tbody>
                {filteredRelationships.map((item) => {
                  const isSelected =
                    selection?.kind === "relationship" &&
                    selection.id === item.id;

                  return (
                    <tr
                      key={item.id}
                      className={`cursor-pointer transition-colors ${
                        isSelected ? "bg-primary/10" : "hover"
                      }`}
                      onClick={() => focusRelationship(item.id)}
                    >
                      <td>
                        <div className="font-medium">{item.name}</div>
                      </td>
                      <td className="text-sm text-base-content/70">
                        {(item.originId && classLookup.get(item.originId)) ||
                          "Unassigned"}
                        {" -> "}
                        {(item.destinationId &&
                          classLookup.get(item.destinationId)) ||
                          "Unassigned"}
                      </td>
                      <td>
                        <span className={statusClass(item.isArchived)}>
                          {item.isArchived ? "Archived" : "Active"}
                        </span>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
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
              Class Inspector
            </h3>
            <p className="text-sm text-base-content/60">
              Edit the selected class, then compare its linked relationships.
            </p>
          </div>
          <span className={statusClass(selectedClass.isArchived)}>
            {selectedClass.isArchived ? "Archived" : "Active"}
          </span>
        </div>

        <div className="grid gap-4 md:grid-cols-2">
          <label className="form-control">
            <span className="mb-2 block text-sm font-medium text-base-content">
              Name
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
            Description
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
              Last Updated
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
            {isUpdatingClass ? "Updating..." : "Update"}
          </button>
          <button
            className="btn btn-outline btn-warning btn-sm"
            onClick={toggleArchiveClass}
            disabled={isArchivingClass}
          >
            <ArchiveBoxIcon className="h-4 w-4" />
            {isArchivingClass
              ? "Updating..."
              : selectedClass.isArchived
                ? "Restore"
                : "Archive"}
          </button>
        </div>
      </div>
    </div>
  ) : (
    emptyState("Select a class to inspect and edit it.")
  );

  const relationshipInspector = selectedRelationship ? (
    <div className="card border border-base-300/50 bg-base-100 shadow-sm">
      <div className="card-body gap-4">
        <div className="flex items-start justify-between gap-3">
          <div>
            <h3 className="text-lg font-semibold text-base-content">
              Relationship Inspector
            </h3>
            <p className="text-sm text-base-content/60">
              Mirror the add-edge workflow by defining which classes the edge can span.
            </p>
          </div>
          <span className={statusClass(selectedRelationship.isArchived)}>
            {selectedRelationship.isArchived ? "Archived" : "Active"}
          </span>
        </div>

        <div className="grid gap-4 md:grid-cols-2">
          <label className="form-control">
            <span className="mb-2 block text-sm font-medium text-base-content">
              Name
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
              UUID
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
              Origin Class
            </span>
            <select
              className="select select-bordered"
              value={relationshipDraft.originId ?? ""}
              onChange={(event) =>
                setRelationshipDraft((previous) => ({
                  ...previous,
                  originId: event.target.value
                    ? Number(event.target.value)
                    : null,
                }))
              }
            >
              <option value="">Unassigned</option>
              {classes.map((item) => (
                <option key={item.id} value={item.id}>
                  {item.name}
                </option>
              ))}
            </select>
          </label>
          <label className="form-control">
            <span className="mb-2 block text-sm font-medium text-base-content">
              Destination Class
            </span>
            <select
              className="select select-bordered"
              value={relationshipDraft.destinationId ?? ""}
              onChange={(event) =>
                setRelationshipDraft((previous) => ({
                  ...previous,
                  destinationId: event.target.value
                    ? Number(event.target.value)
                    : null,
                }))
              }
            >
              <option value="">Unassigned</option>
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
            Description
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
              Origin to Destination
            </div>
            <div className="mt-1 text-sm font-medium">
              {(relationshipDraft.originId &&
                classLookup.get(relationshipDraft.originId)) ||
                "Unassigned"}
              {" -> "}
              {(relationshipDraft.destinationId &&
                classLookup.get(relationshipDraft.destinationId)) ||
                "Unassigned"}
            </div>
          </div>
          <div className="rounded-lg border border-base-300/50 bg-base-200/50 p-3">
            <div className="text-xs uppercase tracking-wide text-base-content/60">
              Last Updated
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
            {isUpdatingRelationship ? "Updating..." : "Update"}
          </button>
          <button
            className="btn btn-outline btn-warning btn-sm"
            onClick={toggleArchiveRelationship}
            disabled={isArchivingRelationship}
          >
            <ArchiveBoxIcon className="h-4 w-4" />
            {isArchivingRelationship
              ? "Updating..."
              : selectedRelationship.isArchived
                ? "Restore"
                : "Archive"}
          </button>
        </div>
      </div>
    </div>
  ) : (
    emptyState("Select a relationship to inspect and edit it.")
  );

  const boardPanel = (
    <div className="card border border-base-300/50 bg-base-100 shadow-sm">
      <div className="card-body gap-4">
        <div>
          <h2 className="text-lg font-semibold text-base-content">
            Relationship Flow
          </h2>
          <p className="text-sm text-base-content/60">
            A more visual alternative for defining allowed class-to-class edges.
          </p>
        </div>

        <div className="space-y-3">
          {filteredRelationships.length === 0
            ? emptyState("No relationship flows are available.")
            : filteredRelationships.map((item) => {
                const isSelected =
                  selection?.kind === "relationship" &&
                  selection.id === item.id;

                return (
                  <button
                    key={item.id}
                    type="button"
                    className={`w-full rounded-xl border p-4 text-left transition ${
                      isSelected
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
                        {item.isArchived ? "Archived" : "Active"}
                      </span>
                    </div>

                    <div className="mt-4 flex flex-wrap items-center gap-2 text-sm">
                      <span className="badge badge-outline">
                        {(item.originId && classLookup.get(item.originId)) ||
                          "Origin"}
                      </span>
                      <ArrowsRightLeftIcon className="h-4 w-4 text-base-content/50" />
                      <span className="badge badge-primary badge-outline">
                        {item.name}
                      </span>
                      <ArrowsRightLeftIcon className="h-4 w-4 text-base-content/50" />
                      <span className="badge badge-outline">
                        {(item.destinationId &&
                          classLookup.get(item.destinationId)) ||
                          "Destination"}
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
      activeTab={activeTab}
      onTabChange={setActiveTab}
      tabs={[
        {
          label: "Classes",
          content: (
            <div className="mt-4 grid gap-6 xl:grid-cols-[1.2fr_1fr]">
              {classesPanel}
              {classInspector}
            </div>
          ),
        },
        {
          label: "Relationships",
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
              Data Schema
            </p>
            <h1 className="break-words text-2xl font-bold text-base-content sm:text-3xl">
              Data Schema
            </h1>
            <p className="mt-3 max-w-4xl text-base-content/70">
              {modeDescriptions[mode]}
            </p>
          </div>
        </div>
      </section>

      <section className="mx-auto w-full max-w-7xl space-y-6 px-3 py-5 sm:px-6 lg:px-8">
        {contentByMode[mode]}
      </section>
      
      {isCreateClassModalOpen ? (
        <ModalShell
          title="Create Class"
          description="Capture the core class definition."
          onClose={() => setIsCreateClassModalOpen(false)}
        >
          <div className="space-y-5 px-6 py-5">
            <div className="grid gap-4 md:grid-cols-2">
              <label className="form-control">
                <span className="label-text mb-2 text-sm font-medium">
                  Class Name
                </span>
                <input
                  className="input input-bordered"
                  placeholder="Example: Asset"
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
                  UUID
                </span>
                <input
                  className="input input-bordered"
                  placeholder="Optional, auto-generated if left blank"
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
                Description
              </span>
              <textarea
                className="textarea textarea-bordered min-h-28"
                placeholder="Explain what records belong to this class and how teams will use it."
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
              Cancel
            </button>
            <button
              type="button"
              className="btn btn-primary"
              onClick={handleCreateClass}
              disabled={!newClassDraft.name.trim() || isCreatingClass}
            >
              {isCreatingClass ? "Creating..." : "Create Class"}
            </button>
          </div>
        </ModalShell>
      ) : null}

      {isCreateRelationshipModalOpen ? (
        <ModalShell
          title="Create Relationship"
          description="Set the edge label and choose the classes it connects."
          onClose={() => setIsCreateRelationshipModalOpen(false)}
        >
          <div className="space-y-5 px-6 py-5">
            <div className="grid gap-4 md:grid-cols-2">
              <label className="form-control">
                <span className="label-text mb-2 text-sm font-medium">
                  Relationship Name
                </span>
                <input
                  className="input input-bordered"
                  placeholder="Example: Installed In"
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
                  UUID
                </span>
                <input
                  className="input input-bordered"
                  placeholder="Optional, auto-generated if left blank"
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
                  Origin Class
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
                  <option value="">Select a class</option>
                  {classes.map((item) => (
                    <option key={item.id} value={item.id}>
                      {item.name}
                    </option>
                  ))}
                </select>
              </label>
              <label className="form-control">
                <span className="label-text mb-2 text-sm font-medium">
                  Destination Class
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
                  <option value="">Select a class</option>
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
                Description
              </span>
              <textarea
                className="textarea textarea-bordered min-h-28 mb-4"
                placeholder="Describe when this relationship should be assigned between records."
                value={newRelationshipDraft.description}
                onChange={(event) =>
                  setNewRelationshipDraft((previous) => ({
                    ...previous,
                    description: event.target.value,
                  }))
                }
              />
            </label>

            <div className="rounded-xl border border-base-300/50 bg-base-200/40 p-4 text-sm text-base-content/70">
              Preview:
              <span className="ml-2 font-medium text-base-content">
                {(newRelationshipDraft.originId &&
                  classLookup.get(newRelationshipDraft.originId)) ||
                  "Origin"}
                {" -> "}
                {newRelationshipDraft.name.trim() || "Relationship"}
                {" -> "}
                {(newRelationshipDraft.destinationId &&
                  classLookup.get(newRelationshipDraft.destinationId)) ||
                  "Destination"}
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
              Cancel
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
                ? "Creating..."
                : "Create Relationship"}
            </button>
          </div>
        </ModalShell>
      ) : null}
    </main>
  );
}
