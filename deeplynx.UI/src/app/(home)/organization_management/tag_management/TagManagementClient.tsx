"use client";

/* -------------------------------------------------------------------------- */
/*                                   Imports                                  */
/* -------------------------------------------------------------------------- */

import React, { useEffect, useMemo, useState } from "react";
import toast from "react-hot-toast";

import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import { useLanguage } from "@/app/contexts/Language";
import {
  getAllTagsOrg,
  createTagOrg,
  archiveTagOrg,
  updateTagOrg,
} from "@/app/lib/client_service/tag_services.client";
import {
  getAllSensitivityLabelsOrg,
  createSensitivityLabelsOrg,
  updateSensitivityLabelOrg,
  archiveSensitivityLabelOrg,
} from "@/app/lib/client_service/sensitivity_labels_services.client";
import type {
  ProjectResponseDto,
  TagResponseDto,
  SensitivityLabelsDto,
} from "@/app/(home)/types/responseDTOs";

import ConfirmArchiveTagModal from "./ConfirmArchiveTagModal";
import OrganizationTagOverviewStrip from "./OrganizationTagOverviewStrip";
import SecurityLabelsOrg from "./SecurityLabelsOrg";
import OrgTagsPanel from "./OrgTagsPanel";
import TagEditModal from "./TagEditModal";
import LabelEditModal from "./LabelEditModal";
import ConfirmArchiveLabelModal from "./ConfirmArchiveLabelModal";
import { AxiosError } from "axios";

/* -------------------------------------------------------------------------- */
/*                                   Types                                    */
/* -------------------------------------------------------------------------- */

interface Props {
  projects: ProjectResponseDto[];
}

type ModalMode = "tag";

/* -------------------------------------------------------------------------- */
/*                       TagManagementClientOption3                           */
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

  // Labels loaded from backend
  const [labels, setLabels] = useState<SensitivityLabelsDto[]>([]);
  const [labelsLocked, setLabelsLocked] = useState(false);

  const [labelsLoading, setLabelsLoading] = useState(false);
  const [labelsError, setLabelsError] = useState<string | null>(null);

  const [archivingLabelId, setArchivingLabelId] = useState<number | null>(null);

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
  const [modalMode, setModalMode] = useState<ModalMode>("tag");
  const [editingTag, setEditingTag] = useState<TagResponseDto | null>(null);
  const [nameInput, setNameInput] = useState("");
  const [savingTag, setSavingTag] = useState(false);

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
    setModalMode("tag");
    setIsModalOpen(true);
  };

  const openEditTagModal = (id: number) => {
    resetModalState();
    setModalMode("tag");
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

  const closeLabelModal = () => {
    setIsLabelModalOpen(false);
    resetLabelModalState();
  };

  const openArchiveLabelModal = (label: SensitivityLabelsDto) => {
    setLabelToArchive(label);
    setShowArchiveLabelModal(true);
  };

  /* ------------------------------------------------------------------------ */
  /*                           Load Tags from Backend                         */
  /* ------------------------------------------------------------------------ */

  const loadOrganizationTags = async () => {
    if (!orgId) return;

    try {
      setTagsLoading(true);
      setTagsError(null);

      const dtoList: TagResponseDto[] = await getAllTagsOrg(
        orgId,
        undefined,
        true, // hide archived by default
      );

      setTags(dtoList.filter((t) => !t.isArchived));
    } catch (error) {
      console.error("Failed to load organization tags:", error);
      setTagsError(t.translations.FAILED_TO_LOAD_ORGANIZATION_TAGS);
      toast.error(t.translations.FAILED_TO_LOAD_ORGANIZATION_TAGS);
    } finally {
      setTagsLoading(false);
    }
  };

  const loadOrganizationLabels = async () => {
    if (!orgId) return;

    try {
      setLabelsLoading(true);
      setLabelsError(null);

      const dtoList: SensitivityLabelsDto[] = await getAllSensitivityLabelsOrg(
        orgId,
        undefined,
        true, // hide archived by default
      );

      setLabels(dtoList.filter((l) => !l.isArchived));
    } catch (error) {
      console.error("Failed to load organization labels:", error);
      setLabelsError(t.translations.FAILED_TO_LOAD_ORGANIZATION_LABELS);
      toast.error(t.translations.FAILED_TO_LOAD_ORGANIZATION_LABELS);
    } finally {
      setLabelsLoading(false);
    }
  };

  useEffect(() => {
    loadOrganizationTags();
    loadOrganizationLabels();
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

    if (modalMode !== "tag") return;

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

  const handleSaveLabel = async () => {
    if (!labelNameInput.trim()) return;

    if (!orgId) {
      toast.error(t.translations.NO_ORGANIZATION_SELECTED_UNABLE_TO_SAVE_LABEL);
      return;
    }

    try {
      setSavingLabel(true);

      if (editingLabel) {
        const updated = await updateSensitivityLabelOrg(
          orgId,
          editingLabel.id,
          {
            name: labelNameInput.trim(),
            description: labelDescriptionInput.trim() || null,
          },
        );

        setLabels((prev) =>
          prev.map((l) => (l.id === updated.id ? updated : l)),
        );
        toast.success(t.translations.ORGANIZATION_LABEL_UPDATED);
      } else {
        const created = await createSensitivityLabelsOrg(orgId, {
          name: labelNameInput.trim(),
          description: labelDescriptionInput.trim() || null,
        });

        setLabels((prev) => [...prev, created]);
        toast.success(t.translations.ORGANIZATION_LABEL_CREATED);
      }

      closeLabelModal();
    } catch (error) {
      console.error("Failed to save organization label:", error);
      toast.error(t.translations.FAILED_TO_SAVE_ORGANIZATION_LABEL);
    } finally {
      setSavingLabel(false);
    }
  };

  const confirmArchiveTag = async () => {
    if (!tagToArchive || !orgId) return;

    try {
      setArchivingTagId(tagToArchive.id);
      await archiveTagOrg(orgId, tagToArchive.id, true);

      setTags((prev) => prev.filter((t) => t.id !== tagToArchive.id));
      toast.success(
        t.translations.TAG_ARCHIVED_WITH_NAME.replace("{name}", tagToArchive.name),
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

  const confirmArchiveLabel = async () => {
    if (!labelToArchive || !orgId) return;

    try {
      setArchivingLabelId(labelToArchive.id);
      await archiveSensitivityLabelOrg(orgId, labelToArchive.id, true);

      setLabels((prev) => prev.filter((l) => l.id !== labelToArchive.id));
      toast.success(
        t.translations.LABEL_ARCHIVED_WITH_NAME.replace("{name}", labelToArchive.name),
      );
    } catch (error) {
      console.error("Failed to archive label:", error);
      if (String((error as AxiosError).response?.data).includes("Cannot archive")) {
        toast.error(t.translations.LABEL_IN_USE)
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

  const organizationTagCount = tags.length;
  const filteredTagCount = filteredTags.length;

  const sensitivityLabelCount = labels.length;
  const filteredLabelCount = filteredLabels.length;

  const projectsWithSensitivityLabelsCount = projects.length;
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
          {t.translations.DEFINE_ORGANIZATION_TAGS_AND_SENSITIVITY_LABELS_DESCRIPTION}
        </p>
      </div>

      {/* Overview Strip */}
      <OrganizationTagOverviewStrip
        sensitivityLabelCount={sensitivityLabelCount}
        projectsWithSensitivityLabelsCount={
          projectsWithSensitivityLabelsCount
        }
        organizationTagCount={organizationTagCount}
        projectsWithTagsCount={projectsWithTagsCount}
        organizationTagsLocked={tagsLocked}
        organizationLabelsLocked={labelsLocked}
      />

      {/* Two-Column Layout */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        {/* Labels column – Coming soon */}
        <SecurityLabelsOrg
          labels={labels}
          labelsLocked={labelsLocked}
          labelsLoading={labelsLoading}
          labelsError={labelsError}
          filteredLabels={filteredLabels}
          labelSearch={labelSearch}
          setLabelSearch={setLabelSearch}
          filteredCount={filteredLabelCount}
          labelCount={sensitivityLabelCount}
          orgId={orgId}
          archivingLabelId={archivingLabelId}
          onToggleLock={() => setLabelsLocked((prev) => !prev)}
          onCreateLabel={openCreateLabelModal}
          onEditLabel={openEditLabelModal}
          onArchiveClick={openArchiveLabelModal}
        />

        {/* Tags column – Fully functional */}
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
        isOpen={isModalOpen && modalMode === "tag"}
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
        onCancel={closeLabelModal}
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
        onConfirm={confirmArchiveTag}
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

export default TagManagementClient;
