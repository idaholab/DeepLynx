"use client";

/* -------------------------------------------------------------------------- */
/*                                   Imports                                  */
/* -------------------------------------------------------------------------- */

import React, { useEffect, useMemo, useState } from "react";
import toast from "react-hot-toast";

import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";

import type {
  ProjectResponseDto,
  TagResponseDto,
} from "@/app/(home)/types/responseDTOs";

import {
  archiveTag,
  createTag,
  getAllTags,
  updateTag,
} from "@/app/lib/client_service/tag_services.client";

import ConfirmArchiveTagModal from "@/app/(home)/organization_management/tag_management/ConfirmArchiveTagModal";
import TagEditModal from "@/app/(home)/organization_management/tag_management/TagEditModal";
import { useLanguage } from "@/app/contexts/Language";
import ProjectTagOverviewStrip from "./ProjectTagOverviewStrip";
import ProjectTagsPanel from "./ProjectTagsPanel";

/* -------------------------------------------------------------------------- */
/*                                   Types                                    */
/* -------------------------------------------------------------------------- */

interface Props {
  project: ProjectResponseDto;
  /** From backend: whether org has locked tags */
  orgTagsLocked: boolean;
}

/* -------------------------------------------------------------------------- */
/*                     ProjectTagManagementClient (Tags Only)                 */
/* -------------------------------------------------------------------------- */

const ProjectTagAndLabelManagementClient: React.FC<Props> = ({
  project,
  orgTagsLocked,
}) => {
  const { organization } = useOrganizationSession();
  const orgId = organization?.organizationId as number | undefined;
  const projectId = project.id as number;

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
  /*                               Modal State                                */
  /* ------------------------------------------------------------------------ */

  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingTag, setEditingTag] = useState<TagResponseDto | null>(null);
  const [nameInput, setNameInput] = useState("");
  const [savingTag, setSavingTag] = useState(false);
  const { t } = useLanguage();
  const [showArchiveModal, setShowArchiveModal] = useState(false);
  const [tagToArchive, setTagToArchive] = useState<TagResponseDto | null>(null);

  /* ------------------------------------------------------------------------ */
  /*                               Modal Helpers                              */
  /* ------------------------------------------------------------------------ */

  const resetModalState = () => {
    setEditingTag(null);
    setNameInput("");
    setSavingTag(false);
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

  /* ------------------------------------------------------------------------ */
  /*                           Load from Backend (Tags)                       */
  /* ------------------------------------------------------------------------ */

  const loadProjectTags = async () => {
    if (!orgId || !projectId) return;

    try {
      setTagsLoading(true);
      setTagsError(null);

      const dtoList = await getAllTags(projectId);

      setTags(dtoList.items.filter((t) => !t.isArchived));
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
  /*                               Derived Data                               */
  /* ------------------------------------------------------------------------ */

  const inheritedOrganizationTagCount = tags.filter(
    (tag) => !tag.projectId,
  ).length;
  const projectManagedTagCount = tags.filter((tag) => !!tag.projectId).length;
  const totalVisibleTagCount = tags.length;
  const filteredTagCount = filteredTags.length;

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
          {t.translations.DEFINE_PROJECT_TAGS_DESCRIPTION}
        </p>
      </div>

      {/* Overview Strip */}
      <ProjectTagOverviewStrip
        inheritedOrganizationTagCount={inheritedOrganizationTagCount}
        projectManagedTagCount={projectManagedTagCount}
      />

      {/* Tags panel – project-scoped, respects org lock */}
      <div className="max-w-2xl">
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
    </div>
  );
};

export default ProjectTagAndLabelManagementClient;
