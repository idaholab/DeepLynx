// src/app/(home)/organization_management/groups/GroupEditModal.tsx
"use client";

import React from "react";
import { useLanguage } from "@/app/contexts/Language";

interface GroupEditModalProps {
  isOpen: boolean;
  isSaving: boolean;
  editingGroup: boolean;
  nameInput: string;
  descriptionInput: string;
  onNameChange: (value: string) => void;
  onDescriptionChange: (value: string) => void;
  onCancel: () => void;
  onSave: () => void;
}

const GroupEditModal: React.FC<GroupEditModalProps> = ({
  isOpen,
  isSaving,
  editingGroup,
  nameInput,
  descriptionInput,
  onNameChange,
  onDescriptionChange,
  onCancel,
  onSave,
}) => {
  const { t } = useLanguage();
  if (!isOpen) return null;

  return (
    <div className="modal modal-open">
      <div className="modal-box max-w-lg">
        <h3 className="mb-4 text-xl font-bold">
          {editingGroup
            ? t.translations.EDIT_GROUP
            : t.translations.CREATE_NEW_GROUP}
        </h3>

        <div className="form-control mb-4">
          <label className="label">
            <span className="label-text font-semibold">
              {t.translations.GROUP_NAME_STAR}
            </span>
          </label>
          <input
            type="text"
            placeholder={t.translations.EG_ENGINEERING_TEAM}
            className="input input-bordered w-full"
            value={nameInput}
            onChange={(e) => onNameChange(e.target.value)}
            disabled={isSaving}
          />
        </div>

        <div className="form-control mb-2">
          <label className="label">
            <span className="label-text font-semibold">
              {t.translations.DESCRIPTION}
            </span>
          </label>
          <textarea
            placeholder={t.translations.BRIEF_DESCRIPTION}
            className="textarea textarea-bordered w-full"
            rows={3}
            value={descriptionInput}
            onChange={(e) => onDescriptionChange(e.target.value)}
            disabled={isSaving}
          />
        </div>

        <div className="modal-action">
          <button className="btn btn-ghost" onClick={onCancel} disabled={isSaving}>
            {t.translations.CANCEL}
          </button>
          <button
            className="btn btn-primary"
            onClick={onSave}
            disabled={isSaving || !nameInput.trim()}
          >
            {isSaving ? (
              <span className="loading loading-spinner loading-sm" />
            ) : (
              t.translations.SAVE
            )}
          </button>
        </div>
      </div>
      <button
        type="button"
        className="modal-backdrop"
        onClick={onCancel}
        aria-label={t.translations.CANCEL}
      />
    </div>
  );
};

export default GroupEditModal;
