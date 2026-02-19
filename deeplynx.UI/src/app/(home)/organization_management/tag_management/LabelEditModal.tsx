"use client";

import React from "react";
import { useLanguage } from "@/app/contexts/Language";

interface Props {
  isOpen: boolean;
  isSaving: boolean;
  editingLabel: boolean;
  nameInput: string;
  descriptionInput: string;
  onNameChange: (value: string) => void;
  onDescriptionChange: (value: string) => void;
  onCancel: () => void;
  onSave: () => void;
}

const LabelEditModal: React.FC<Props> = ({
  isOpen,
  isSaving,
  editingLabel,
  nameInput,
  descriptionInput,
  onNameChange,
  onDescriptionChange,
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
          {editingLabel ? t.translations.EDIT_LABEL : t.translations.CREATE_LABEL}
        </h3>
        <p className="text-xs text-base-content/70 mb-4">
          {t.translations.DEFINE_ORGANIZATION_LEVEL_SENSITIVATY_LABEL_DESCRIPTION}
        </p>

        <div className="space-y-4">
          <div className="form-control">
            <label className="label">
              <span className="label-text font-semibold">
                {t.translations.LABEL_NAME} <span className="text-error">*</span>
              </span>
            </label>
            <input
              type="text"
              className="input input-bordered input-sm"
              placeholder={t.translations.LABEL_NAME_PLACEHOLDER}
              value={nameInput}
              onChange={(e) => onNameChange(e.target.value)}
            />
          </div>

          <div className="form-control">
            <label className="label">
              <span className="label-text font-semibold">{t.translations.DESCRIPTION}</span>
            </label>
            <textarea
              className="textarea textarea-bordered textarea-sm"
              placeholder={t.translations.OPTIONAL_DESCRIPTION_FOR_THIS_LABEL}
              rows={3}
              value={descriptionInput}
              onChange={(e) => onDescriptionChange(e.target.value)}
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
              : editingLabel
                ? t.translations.SAVE_LABEL
                : t.translations.CREATE_LABEL}
          </button>
        </div>
      </div>
      <div className="modal-backdrop" onClick={onCancel} />
    </div>
  );
};

export default LabelEditModal;
