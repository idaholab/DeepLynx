"use client";

/* -------------------------------------------------------------------------- */
/*                                   Imports                                  */
/* -------------------------------------------------------------------------- */

import React, { useEffect, useMemo, useState } from "react";
import toast from "react-hot-toast";

import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";

import type {
  ProjectResponseDto,
  SensitivityLabelsDto,
  TagResponseDto,
} from "@/app/(home)/types/responseDTOs";

import {
  archiveSensitivityLabelProject,
  createSensitivityLabelProject,
  updateSensitivityLabelProject,
} from "@/app/lib/client_service/sensitivity_labels_services.client";
import {
  archiveTag,
  createTag,
  getAllTags,
  updateTag,
} from "@/app/lib/client_service/tag_services.client";

import ConfirmArchiveLabelModal from "@/app/(home)/organization_management/tag_management/ConfirmArchiveLabelModal";
import ConfirmArchiveTagModal from "@/app/(home)/organization_management/tag_management/ConfirmArchiveTagModal";
import LabelEditModal from "@/app/(home)/organization_management/tag_management/LabelEditModal";
import TagEditModal from "@/app/(home)/organization_management/tag_management/TagEditModal";
import { useLanguage } from "@/app/contexts/Language";
import { AxiosError } from "axios";
import ProjectsSecurityLabels from "./ProjectsSecurityLabels";
import ProjectTagOverviewStrip from "./ProjectTagOverviewStrip";
import ProjectTagsPanel from "./ProjectTagsPanel";

/* -------------------------------------------------------------------------- */
/*                                   Types                                    */
/* -------------------------------------------------------------------------- */

interface Props {
  project: ProjectResponseDto;
  /** From backend: whether org has locked tags */
  orgTagsLocked: boolean;
  initialLabels: SensitivityLabelsDto[];
  refreshLabels: () => Promise<void>;
}

/* -------------------------------------------------------------------------- */
/*                     ProjectTagManagementClient (Tags Only)                 */
/* -------------------------------------------------------------------------- */

const ProjectTagAndLabelManagementClient: React.FC<Props> = ({
  project,
  orgTagsLocked,
  initialLabels,
  refreshLabels,
}) => {
  const { organization } = useOrganizationSession();
  const orgId = organization?.organizationId as number | undefined;
  const projectId = project.id as number;
  const orgLabelsLocked = false;

  /* ------------------------------------------------------------------------ */
  /*                                 Tag State                                */
  /* ------------------------------------------------------------------------ */

  const [tags, setTags] = useState<TagResponseDto[]>([]);
  const [tagsLoading, setTagsLoading] = useState(false);
  const [tagsError, setTagsError] = useState<string | null>(null);
  const [archivingTagId, setArchivingTagId] = useState<number | null>(null);

  const [tagSearch, setTagSearch] = useState("");
  const normalizedTagSearch = tagSearch.trim().toLowerCase();

  const filteredTags = useMemo(
    () =>
      normalizedTagSearch
        ? tags.filter((t) => t.name.toLowerCase().includes(normalizedTagSearch))
        : tags,
    [tags, normalizedTagSearch],
  );

  /* ------------------------------------------------------------------------ */
  /*                               Label State                                */
  /* ------------------------------------------------------------------------ */

  const [labels, setLabels] = useState<SensitivityLabelsDto[]>(initialLabels);
  const [labelsLoading, setLabelsLoading] = useState(false);
  const [labelsError, setLabelsError] = useState<string | null>(null);
  const [archivingLabelId, setArchivingLabelId] = useState<number | null>(null);

  const [labelSearch, setLabelSearch] = useState("");
  const normalizedLabelSearch = labelSearch.trim().toLowerCase();

  const filteredLabels = useMemo(
    () =>
      normalizedLabelSearch
        ? labels.filter(
            (l) =>
              l.name.toLowerCase().includes(normalizedLabelSearch) ||
              l.description?.toLowerCase().includes(normalizedLabelSearch),
          )
        : labels,
    [labels, normalizedLabelSearch],
  );

  /* ------------------------------------------------------------------------ */
  /*                               Modal State                                */
  /* ------------------------------------------------------------------------ */

  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingTag, setEditingTag] = useState<TagResponseDto | null>(null);
  const [nameInput, setNameInput] = useState("");
  const [savingTag, setSavingTag] = useState(false);
  const { t } = useLanguage();
  const [showArchiveModal, setShowArchiveModal] = useState(false);
  const [tagToArchive, setTagToArchive] = useState<TagResponseDto | null>(null);

  const [isLabelModalOpen, setIsLabelModalOpen] = useState(false);
  const [editingLabel, setEditingLabel] = useState<SensitivityLabelsDto | null>(
    null,
  );
  const [labelNameInput, setLabelNameInput] = useState("");
  const [labelDescriptionInput, setLabelDescriptionInput] = useState("");
  const [savingLabel, setSavingLabel] = useState(false);

  const [showArchiveLabelModal, setShowArchiveLabelModal] = useState(false);
  const [labelToArchive, setLabelToArchive] =
    useState<SensitivityLabelsDto | null>(null);

  /* ------------------------------------------------------------------------ */
  /*                               Modal Helpers                              */
  /* ------------------------------------------------------------------------ */

  const resetModalState = () => {
    setEditingTag(null);
    setNameInput("");
    setSavingTag(false);
  };

  const resetLabelModalState = () => {
    setEditingLabel(null);
    setLabelNameInput("");
    setLabelDescriptionInput("");
    setSavingLabel(false);
  };

  const openCreateTagModal = () => {
    resetModalState();
    setIsModalOpen(true);
  };

  const openEditTagModal = (id: number) => {
    resetModalState();
    const found = tags.find((t) => t.id === id) || null;
    if (found) {
      setEditingTag(found);
      setNameInput(found.name);
      setIsModalOpen(true);
    }
  };

  const closeEditCreateModal = () => {
    setIsModalOpen(false);
    resetModalState();
  };

  const openArchiveModal = (tag: TagResponseDto) => {
    setTagToArchive(tag);
    setShowArchiveModal(true);
  };

  const openCreateLabelModal = () => {
    resetLabelModalState();
    setIsLabelModalOpen(true);
  };

  const openEditLabelModal = (id: number) => {
    resetLabelModalState();
    const found = labels.find((l) => l.id === id) || null;
    if (found) {
      setEditingLabel(found);
      setLabelNameInput(found.name);
      setLabelDescriptionInput(found.description ?? "");
      setIsLabelModalOpen(true);
    }
  };

  const closeEditCreateLabelModal = () => {
    setIsLabelModalOpen(false);
    resetLabelModalState();
  };

  const openArchiveLabelModal = (label: SensitivityLabelsDto) => {
    setLabelToArchive(label);
    setShowArchiveLabelModal(true);
  };

  /* ------------------------------------------------------------------------ */
  /*                           Load from Backend (Tags)                       */
  /* ------------------------------------------------------------------------ */

  const loadProjectTags = async () => {
    if (!orgId || !projectId) return;

    try {
      setTagsLoading(true);
      setTagsError(null);

      const dtoList: TagResponseDto[] = await getAllTags(projectId);

      setTags(dtoList.filter((t) => !t.isArchived));
    } catch (error) {
      console.error("Failed to load project tags:", error);
      setTagsError(t.translations.FAILED_TO_LOAD_PROJECT_TAGS);
      toast.error(t.translations.FAILED_TO_LOAD_PROJECT_TAGS);
    } finally {
      setTagsLoading(false);
    }
  };

  useEffect(() => {
    loadProjectTags();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [orgId, projectId]);

  /* ------------------------------------------------------------------------ */
  /*                         Load from Backend (Labels)                       */
  /* ------------------------------------------------------------------------ */

  const loadProjectLabels = async () => {
    if (!orgId || !projectId) return;

    try {
      setLabelsLoading(true);
      setLabelsError(null);
      setLabels(initialLabels.filter((l) => !l.isArchived));
    } catch (error) {
      console.error("Failed to load project labels:", error);
      setLabelsError(t.translations.FAILED_TO_LOAD_PROJECT_LABELS);
      toast.error(t.translations.FAILED_TO_LOAD_PROJECT_LABELS);
    } finally {
      setLabelsLoading(false);
    }
  };

  useEffect(() => {
    loadProjectLabels();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [orgId, projectId]);

  /* ------------------------------------------------------------------------ */
  /*                         Create / Update (Tags Only)                      */
  /* ------------------------------------------------------------------------ */

  const handleSave = async () => {
    if (!nameInput.trim()) return;

    if (!orgId || !projectId) {
      toast.error(t.translations.MISSING_ORG_OR_PROJECT_CONTEXT_UNABLE_TO_SAVE);
      return;
    }

    if (orgTagsLocked) {
      toast.error(t.translations.TAGS_LOCKED_CANNOT_CREATE_OR_EDIT_PROJECT);
      return;
    }

    try {
      setSavingTag(true);

      if (editingTag) {
        // Update existing project tag
        const updatePayload = {
          name: nameInput.trim(),
        };

        const updated = await updateTag(
          projectId,
          editingTag.id,
          updatePayload,
        );

        setTags((prev) => prev.map((t) => (t.id === updated.id ? updated : t)));
        toast.success(t.translations.PROJECT_TAG_UPDATED);
      } else {
        // Create new project tag
        const createPayload = {
          name: nameInput.trim(),
        };

        const created = await createTag(projectId, createPayload);
        setTags((prev) => [...prev, created]);
        toast.success(t.translations.PROJECT_TAG_CREATED);
      }

      closeEditCreateModal();
    } catch (error) {
      console.error("Failed to save project tag:", error);
      toast.error(t.translations.FAILED_TO_SAVE_PROJECT_TAG);
    } finally {
      setSavingTag(false);
    }
  };

  /* ------------------------------------------------------------------------ */
  /*                        Create / Update (Labels)                          */
  /* ------------------------------------------------------------------------ */

  const handleSaveLabel = async () => {
    if (!labelNameInput.trim()) return;

    if (!orgId || !projectId) {
      toast.error(t.translations.MISSING_ORG_OR_PROJECT_CONTEXT_UNABLE_TO_SAVE);
      return;
    }

    if (orgLabelsLocked) {
      toast.error(t.translations.LABELS_LOCKED_CANNOT_CREATE_OR_EDIT_PROJECT);
      return;
    }

    try {
      setSavingLabel(true);

      if (editingLabel) {
        const updated = await updateSensitivityLabelProject(
          projectId,
          editingLabel.id,
          {
            name: labelNameInput.trim(),
            description: labelDescriptionInput.trim() || null,
          },
        );

        setLabels((prev) =>
          prev.map((l) => (l.id === updated.id ? updated : l)),
        );
        await refreshLabels();
        toast.success(t.translations.PROJECT_LABEL_UPDATED);
      } else {
        const created = await createSensitivityLabelProject(projectId, {
          name: labelNameInput.trim(),
          description: labelDescriptionInput.trim() || null,
        });

        setLabels((prev) => [...prev, created]);
        await refreshLabels();
        toast.success(t.translations.PROJECT_LABEL_CREATED);
      }

      closeEditCreateLabelModal();
    } catch (error) {
      console.error("Failed to save project label:", error);
      toast.error(t.translations.FAILED_TO_SAVE_PROJECT_LABEL);
    } finally {
      setSavingLabel(false);
    }
  };

  /* ------------------------------------------------------------------------ */
  /*                           Confirm Archive (Tags)                         */
  /* ------------------------------------------------------------------------ */

  const confirmArchive = async () => {
    if (!tagToArchive || !orgId || !projectId) return;

    if (orgTagsLocked) {
      toast.error(t.translations.TAGS_LOCKED_CANNOT_ARCHIVE_PROJECT);
      return;
    }

    try {
      setArchivingTagId(tagToArchive.id);
      await archiveTag(projectId, tagToArchive.id, true);

      setTags((prev) => prev.filter((t) => t.id !== tagToArchive.id));
      toast.success(
        `${t.translations.TAG} "${tagToArchive.name}" ${t.translations.ARCHIVED}.`,
      );
    } catch (error) {
      console.error("Failed to archive tag:", error);
      toast.error(t.translations.FAILED_TO_ARCHIVE_TAG);
    } finally {
      setArchivingTagId(null);
      setShowArchiveModal(false);
      setTagToArchive(null);
    }
  };

  /* ------------------------------------------------------------------------ */
  /*                          Confirm Archive (Labels)                        */
  /* ------------------------------------------------------------------------ */

  const confirmArchiveLabel = async () => {
    if (!labelToArchive || !orgId || !projectId) return;

    if (orgLabelsLocked) {
      toast.error(t.translations.LABELS_LOCKED_CANNOT_ARCHIVE_PROJECT);
      return;
    }

    try {
      setArchivingLabelId(labelToArchive.id);
      await archiveSensitivityLabelProject(projectId, labelToArchive.id, true);

      setLabels((prev) => prev.filter((l) => l.id !== labelToArchive.id));
      await refreshLabels();
      toast.success(
        `${t.translations.LABEL} "${labelToArchive.name}" ${t.translations.ARCHIVED}.`,
      );
    } catch (error) {
      console.error("Failed to archive label:", error);
      if (
        String((error as AxiosError).response?.data).includes("Cannot archive")
      ) {
        toast.error(t.translations.LABEL_IN_USE);
      } else {
        toast.error(t.translations.FAILED_TO_ARCHIVE_LABEL);
      }
    } finally {
      setArchivingLabelId(null);
      setShowArchiveLabelModal(false);
      setLabelToArchive(null);
    }
  };

  /* ------------------------------------------------------------------------ */
  /*                               Derived Data                               */
  /* ------------------------------------------------------------------------ */

  const inheritedOrganizationTagCount = tags.filter(
    (tag) => !tag.projectId,
  ).length;
  const projectManagedTagCount = tags.filter((tag) => !!tag.projectId).length;
  const totalVisibleTagCount = tags.length;
  const filteredTagCount = filteredTags.length;

  const inheritedOrganizationLabelCount = labels.filter(
    (label) => !label.projectId,
  ).length;
  const projectManagedLabelCount = labels.filter(
    (label) => !!label.projectId,
  ).length;
  const totalVisibleLabelCount = labels.length;
  const filteredLabelCount = filteredLabels.length;

  /* ------------------------------------------------------------------------ */
  /*                               Main Render                                */
  /* ------------------------------------------------------------------------ */

  return (
    <div className="p-6">
      {/* Page Header */}
      <div className="mb-4 border-b border-base-300/50 pb-4">
        <h2 className="text-2xl font-bold text-base-content">
          {t.translations.PROJECT_TAG_MANAGEMENT}
        </h2>
        <p className="text-base-content/70 mt-1 max-w-3xl">
          {t.translations.DEFINE_PROJECT_TAGS_AND_LABELS_DESCRIPTION}
        </p>
      </div>

      {/* Overview Strip */}
      <ProjectTagOverviewStrip
        inheritedOrganizationLabelCount={inheritedOrganizationLabelCount}
        projectManagedLabelCount={projectManagedLabelCount}
        inheritedOrganizationTagCount={inheritedOrganizationTagCount}
        projectManagedTagCount={projectManagedTagCount}
        organizationTagsLocked={orgTagsLocked}
        organizationLabelsLocked={orgLabelsLocked}
      />

      {/* Layout – Tags column only */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        {/* Left side could be empty or future "coming soon" for labels; for now, just tags */}
        <ProjectsSecurityLabels
          labels={labels}
          orgLabelsLocked={orgLabelsLocked}
          labelsLoading={labelsLoading}
          labelsError={labelsError}
          filteredLabels={filteredLabels}
          labelSearch={labelSearch}
          setLabelSearch={setLabelSearch}
          filteredCount={filteredLabelCount}
          labelCount={totalVisibleLabelCount}
          projectId={projectId}
          archivingLabelId={archivingLabelId}
          onCreateLabel={openCreateLabelModal}
          onEditLabel={openEditLabelModal}
          onArchiveClick={openArchiveLabelModal}
        />

        {/* Tags column – project-scoped, respects org lock */}
        <ProjectTagsPanel
          tags={tags}
          orgTagsLocked={orgTagsLocked}
          tagsLoading={tagsLoading}
          tagsError={tagsError}
          filteredTags={filteredTags}
          tagSearch={tagSearch}
          setTagSearch={setTagSearch}
          filteredCount={filteredTagCount}
          tagCount={totalVisibleTagCount}
          projectId={projectId}
          archivingTagId={archivingTagId}
          onCreateTag={openCreateTagModal}
          onEditTag={openEditTagModal}
          onArchiveClick={openArchiveModal}
        />
      </div>

      {/* Edit/Create Tag Modal */}
      <TagEditModal
        isOpen={isModalOpen}
        isSaving={savingTag}
        editingTag={!!editingTag}
        nameInput={nameInput}
        onNameChange={setNameInput}
        onCancel={closeEditCreateModal}
        onSave={handleSave}
      />

      {/* Edit/Create Label Modal */}
      <LabelEditModal
        isOpen={isLabelModalOpen}
        isSaving={savingLabel}
        editingLabel={!!editingLabel}
        nameInput={labelNameInput}
        descriptionInput={labelDescriptionInput}
        onNameChange={setLabelNameInput}
        onDescriptionChange={setLabelDescriptionInput}
        onCancel={closeEditCreateLabelModal}
        onSave={handleSaveLabel}
      />

      {/* Confirm Archive Modal */}
      <ConfirmArchiveTagModal
        isOpen={showArchiveModal}
        tagName={tagToArchive?.name ?? ""}
        onClose={() => {
          setShowArchiveModal(false);
          setTagToArchive(null);
        }}
        onConfirm={confirmArchive}
        loading={archivingTagId === tagToArchive?.id}
      />

      {/* Confirm Archive Label Modal */}
      <ConfirmArchiveLabelModal
        isOpen={showArchiveLabelModal}
        labelName={labelToArchive?.name ?? ""}
        onClose={() => {
          setShowArchiveLabelModal(false);
          setLabelToArchive(null);
        }}
        onConfirm={confirmArchiveLabel}
        loading={archivingLabelId === labelToArchive?.id}
      />
    </div>
  );
};

export default ProjectTagAndLabelManagementClient;
