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
  archiveTagOrg,
  createTagOrg,
  getAllTagsOrg,
  updateTagOrg,
} from "@/app/lib/client_service/tag_services.client";

import TagOverviewStrip from "@/app/(home)/organization_management/tag_management/TagOverviewStrip";
import ConfirmArchiveTagModal from "@/app/(home)/organization_management/tag_management/ConfirmArchiveTagModal";
import TagEditModal from "@/app/(home)/organization_management/tag_management/TagEditModal";
import ProjectTagsPanel from "./ProjectTagsPanel";
import ProjectLabelsComingSoonCard from "./ProjectLabelsComingSoonCard";
import { useLanguage } from "@/app/contexts/Language";

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
  const projectId = project.id;

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

      const dtoList: TagResponseDto[] = await getAllTagsOrg(
        orgId,
        [projectId as number],
        true, // hide archived by default
      );

      setTags(dtoList.filter((t) => !t.isArchived));
    } catch (error) {
      console.error("Failed to load project tags:", error);
      setTagsError("Failed to load project tags.");
      toast.error("Failed to load project tags.");
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
      toast.error("Missing organization or project context. Unable to save.");
      return;
    }

    if (orgTagsLocked) {
      toast.error(
        "Tags are locked at the organization level. Cannot create or edit project tags.",
      );
      return;
    }

    try {
      setSavingTag(true);

      if (editingTag) {
        // Update existing project tag
        const updatePayload = {
          name: nameInput.trim(),
        };

        const updated = await updateTagOrg(orgId, editingTag.id, updatePayload);

        setTags((prev) => prev.map((t) => (t.id === updated.id ? updated : t)));
        toast.success("Project tag updated.");
      } else {
        // Create new project tag
        const createPayload = {
          name: nameInput.trim(),
        };

        const created = await createTagOrg(orgId, createPayload);
        setTags((prev) => [...prev, created]);
        toast.success("Project tag created.");
      }

      closeEditCreateModal();
    } catch (error) {
      console.error("Failed to save project tag:", error);
      toast.error("Failed to save project tag.");
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
      toast.error(
        "Tags are locked at the organization level. Cannot archive project tags.",
      );
      return;
    }

    try {
      setArchivingTagId(tagToArchive.id);
      await archiveTagOrg(orgId, tagToArchive.id, true);

      setTags((prev) => prev.filter((t) => t.id !== tagToArchive.id));
      toast.success(`Tag "${tagToArchive.name}" archived.`);
    } catch (error) {
      console.error("Failed to archive tag:", error);
      toast.error("Failed to archive tag.");
    } finally {
      setArchivingTagId(null);
      setShowArchiveModal(false);
      setTagToArchive(null);
    }
  };

  /* ------------------------------------------------------------------------ */
  /*                               Derived Data                               */
  /* ------------------------------------------------------------------------ */

  const tagCount = tags.length;
  const filteredTagCount = filteredTags.length;

  // Labels not supported yet at the project level
  const labelCount = 0;
  const projectsWithLabels = 0;

  // For this project context, 1 if this project has tags, else 0
  const projectsWithTags = tagCount > 0 ? 1 : 0;

  /* ------------------------------------------------------------------------ */
  /*                               Main Render                                */
  /* ------------------------------------------------------------------------ */

  return (
    <div className="p-6">
      {/* Page Header */}
      <div className="mb-4 border-b border-base-300 pb-4">
        <h2 className="text-2xl font-bold text-base-content">
          {t.translations.PROJECT_TAG_MANAGEMENT}
        </h2>
        <p className="text-base-content/70 mt-1 max-w-3xl">
          {
            t.translations
              .DEFINE_PROJECT_TAGS_FOR_CLASSIFICATION_WORKFLOWS_AND_SEARCH
          }
        </p>
      </div>

      {/* Overview Strip (reuses org component, labels fixed to zero for now) */}
      <TagOverviewStrip
        labelCount={labelCount}
        projectsWithLabels={projectsWithLabels}
        tagCount={tagCount}
        projectsWithTags={projectsWithTags}
        tagsLocked={orgTagsLocked}
        labelsLocked={false}
      />

      {/* Layout – Tags column only */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        {/* Left side could be empty or future "coming soon" for labels; for now, just tags */}
        <ProjectLabelsComingSoonCard />

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
          tagCount={tagCount}
          projectId={projectId as number}
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
