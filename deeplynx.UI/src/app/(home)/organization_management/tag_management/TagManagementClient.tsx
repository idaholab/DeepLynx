"use client";

/* -------------------------------------------------------------------------- */
/*                                   Imports                                  */
/* -------------------------------------------------------------------------- */

import React, { useEffect, useMemo, useState } from "react";
import toast from "react-hot-toast";

import type {
  ProjectResponseDto,
  TagResponseDto,
} from "@/app/(home)/types/responseDTOs";
import { useLanguage } from "@/app/contexts/Language";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import {
  archiveTagOrg,
  createTagOrg,
  getAllTagsOrg,
  updateTagOrg,
} from "@/app/lib/client_service/tag_services.client";

import ConfirmArchiveTagModal from "./ConfirmArchiveTagModal";
import OrganizationTagOverviewStrip from "./OrganizationTagOverviewStrip";
import OrgTagsPanel from "./OrgTagsPanel";
import TagEditModal from "./TagEditModal";

/* -------------------------------------------------------------------------- */
/*                                   Types                                    */
/* -------------------------------------------------------------------------- */

interface Props {
  projects: ProjectResponseDto[];
}

/* -------------------------------------------------------------------------- */
/*                            TagManagementClient                             */
/* -------------------------------------------------------------------------- */

const TagManagementClient: React.FC<Props> = ({ projects }) => {
  /* ------------------------------------------------------------------------ */
  /*                        Organization / Core Tag State                     */
  /* ------------------------------------------------------------------------ */

  const { organization } = useOrganizationSession();
  const { t } = useLanguage();
  const orgId = organization?.organizationId as number | undefined;

  // Tags loaded from backend
  const [tags, setTags] = useState<TagResponseDto[]>([]);
  const [tagsLocked, setTagsLocked] = useState(false);

  const [tagsLoading, setTagsLoading] = useState(false);
  const [tagsError, setTagsError] = useState<string | null>(null);

  // For archive (soft delete) UX
  const [archivingTagId, setArchivingTagId] = useState<number | null>(null);

  /* ------------------------------------------------------------------------ */
  /*                               Search State                               */
  /* ------------------------------------------------------------------------ */

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

  const [showArchiveModal, setShowArchiveModal] = useState(false);
  const [tagToArchive, setTagToArchive] = useState<TagResponseDto | null>(null);

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
  /*                           Load Tags from Backend                         */
  /* ------------------------------------------------------------------------ */

  const loadOrganizationTags = async () => {
    if (!orgId) return;

    try {
      setTagsLoading(true);
      setTagsError(null);

      const dtoList = await getAllTagsOrg(
        orgId
      );

      setTags(dtoList.items.filter((t) => !t.isArchived));
    } catch (error) {
      console.error("Failed to load organization tags:", error);
      setTagsError(t.translations.FAILED_TO_LOAD_ORGANIZATION_TAGS);
      toast.error(t.translations.FAILED_TO_LOAD_ORGANIZATION_TAGS);
    } finally {
      setTagsLoading(false);
    }
  };

  useEffect(() => {
    loadOrganizationTags();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [orgId]);

  /* ------------------------------------------------------------------------ */
  /*                         Create / Update / Archive                        */
  /* ------------------------------------------------------------------------ */

  const handleSave = async () => {
    if (!nameInput.trim()) return;

    if (!orgId) {
      toast.error(t.translations.NO_ORGANIZATION_SELECTED_UNABLE_TO_SAVE_TAG);
      return;
    }

    try {
      setSavingTag(true);

      if (editingTag) {
        // Update existing tag
        const updatePayload: TagResponseDto = {
          ...editingTag,
          name: nameInput.trim(),
        };

        const updated = await updateTagOrg(orgId, editingTag.id, updatePayload);

        setTags((prev) => prev.map((t) => (t.id === updated.id ? updated : t)));
        toast.success(t.translations.ORGANIZATION_TAG_UPDATED);
      } else {
        // Create new tag
        const createPayload: TagResponseDto = {
          id: 0, // backend should ignore / overwrite
          name: nameInput.trim(),
          projectId: 0, // sentinel for "org-level"
          isArchived: false,
          lastUpdatedAt: null,
          lastUpdatedBy: null,
          archivedAt: null,
        };

        const created = await createTagOrg(orgId, createPayload);
        setTags((prev) => [...prev, created]);
        toast.success(t.translations.ORGANIZATION_TAG_CREATED);
      }

      closeEditCreateModal();
    } catch (error) {
      console.error("Failed to save organization tag:", error);
      toast.error(t.translations.FAILED_TO_SAVE_ORGANIZATION_TAG);
    } finally {
      setSavingTag(false);
    }
  };

  const confirmArchiveTag = async () => {
    if (!tagToArchive || !orgId) return;

    try {
      setArchivingTagId(tagToArchive.id);
      await archiveTagOrg(orgId, tagToArchive.id, true);

      setTags((prev) => prev.filter((t) => t.id !== tagToArchive.id));
      toast.success(
        t.translations.TAG_ARCHIVED_WITH_NAME.replace(
          "{name}",
          tagToArchive.name,
        ),
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

  const organizationTagCount = tags.length;
  const filteredTagCount = filteredTags.length;

  const projectsWithTagsCount = projects.length;

  /* ------------------------------------------------------------------------ */
  /*                               Main Render                                */
  /* ------------------------------------------------------------------------ */

  return (
    <div className="p-6">
      {/* Page Header */}
      <div className="mb-4">
        <h2 className="text-2xl font-bold text-base-content">
          {t.translations.ORGANIZATION_TAG_MANAGEMENT}
        </h2>
        <p className="text-base-content/70 mt-1 max-w-3xl text-sm">
          {t.translations.DEFINE_ORGANIZATION_TAGS_DESCRIPTION}
        </p>
      </div>

      {/* Overview Strip */}
      <OrganizationTagOverviewStrip
        organizationTagCount={organizationTagCount}
        projectsWithTagsCount={projectsWithTagsCount}
        organizationTagsLocked={tagsLocked}
      />

      {/* Tags panel */}
      <div className="max-w-2xl">
        <OrgTagsPanel
          tags={tags}
          tagsLocked={tagsLocked}
          tagsLoading={tagsLoading}
          tagsError={tagsError}
          filteredTags={filteredTags}
          tagSearch={tagSearch}
          setTagSearch={setTagSearch}
          filteredCount={filteredTagCount}
          tagCount={organizationTagCount}
          orgId={orgId}
          archivingTagId={archivingTagId}
          onToggleLock={() => setTagsLocked((prev) => !prev)}
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
        onConfirm={confirmArchiveTag}
        loading={archivingTagId === tagToArchive?.id}
      />
    </div>
  );
};

export default TagManagementClient;
