"use client";

import React from "react";
import { useLanguage } from "@/app/contexts/Language";

interface Props {
  isOpen: boolean;
  isSaving: boolean;
  editingTag: boolean;
  nameInput: string;
  onNameChange: (value: string) => void;
  onCancel: () => void;
  onSave: () => void;
}

const TagEditModal: React.FC<Props> = ({
  isOpen,
  isSaving,
  editingTag,
  nameInput,
  onNameChange,
  onCancel,
  onSave,
}) => {
  const { t } = useLanguage();

  if (!isOpen) return null;

  const disabled = !nameInput.trim() || isSaving;

  return (
    <div className="modal modal-open">
      <div className="modal-box max-w-md">
        <h3 className="font-bold text-lg mb-2">
          {editingTag ? t.translations.EDIT_TAG : t.translations.CREATE_TAG}
        </h3>
        <p className="text-xs text-base-content/70 mb-4">
          {t.translations.DEFINE_ORGANIZATION_LEVEL_TAG_DESCRIPTION}
        </p>

        <div className="space-y-4">
          <div className="form-control">
            <label className="label">
              <span className="label-text font-semibold">
                {t.translations.TAG_NAME} <span className="text-error">*</span>
              </span>
            </label>
            <input
              type="text"
              className="input input-bordered input-sm"
              placeholder={t.translations.TAG_NAME_PLACEHOLDER}
              value={nameInput}
              onChange={(e) => onNameChange(e.target.value)}
            />
          </div>
        </div>

        <div className="modal-action">
          <button
            type="button"
            className="btn btn-ghost btn-sm"
            onClick={onCancel}
          >
            {t.translations.CANCEL}
          </button>
          <button
            type="button"
            className="btn btn-primary btn-sm"
            disabled={disabled}
            onClick={onSave}
          >
            {isSaving
              ? t.translations.SAVING
              : editingTag
                ? t.translations.SAVE_TAG
                : t.translations.CREATE_TAG}
          </button>
        </div>
      </div>
      <div className="modal-backdrop" onClick={onCancel} />
    </div>
  );
};

export default TagEditModal;
