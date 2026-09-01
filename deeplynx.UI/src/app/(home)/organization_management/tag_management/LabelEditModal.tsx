"use client";

import React from "react";
import { useLanguage } from "@/app/contexts/Language";

export const RECORD_ACTIONS = [
  "read record",
  "write record",
  "update record",
  "delete record",
] as const;

export const FILE_ACTIONS = [
  "download file",
  "upload file",
  "update file",
  "delete file",
] as const;

interface Props {
  isOpen: boolean;
  isSaving: boolean;
  editingLabel: boolean;
  nameInput: string;
  descriptionInput: string;
  selectedActions: Set<string>;
  onNameChange: (value: string) => void;
  onDescriptionChange: (value: string) => void;
  onToggleAction: (action: string) => void;
  onCancel: () => void;
  onSave: () => void;
  permissionsLoading?: boolean;
}

const LabelEditModal: React.FC<Props> = ({
  isOpen,
  isSaving,
  editingLabel,
  nameInput,
  descriptionInput,
  selectedActions,
  onNameChange,
  onDescriptionChange,
  onToggleAction,
  onCancel,
  onSave,
  permissionsLoading = false,
}) => {
  const { t } = useLanguage();

  const actionLabel = (action: string): string => {
    const key = `PERMISSION_${action.toUpperCase().replace(" ", "_")}` as keyof typeof t.translations;
    return (t.translations[key] as string | undefined) ?? action;
  };

  if (!isOpen) return null;

  const disabled = !nameInput.trim() || isSaving;

  return (
    <div className="modal modal-open">
      <div className="modal-box max-w-lg">
        <h3 className="font-bold text-lg mb-2">
          {editingLabel ? t.translations.EDIT_LABEL : t.translations.CREATE_LABEL}
        </h3>
        <p className="text-xs text-base-content/70 mb-4">
          {t.translations.DEFINE_ORGANIZATION_LEVEL_SENSITIVITY_LABEL_DESCRIPTION}
        </p>

        <div className="space-y-4">
          <div className="form-control flex flex-col">
            <label className="label">
              <span className="label-text font-semibold">
                {t.translations.LABEL_NAME} <span className="text-error">*</span>
              </span>
            </label>
            <input
              type="text"
              className="input input-bordered input-sm w-full"
              placeholder={t.translations.LABEL_NAME_PLACEHOLDER}
              value={nameInput}
              onChange={(e) => onNameChange(e.target.value)}
            />
          </div>

          <div className="form-control flex flex-col">
            <label className="label">
              <span className="label-text font-semibold">{t.translations.DESCRIPTION}</span>
            </label>
            <textarea
              className="textarea textarea-bordered textarea-sm w-full"
              placeholder={t.translations.OPTIONAL_DESCRIPTION_FOR_THIS_LABEL}
              rows={3}
              value={descriptionInput}
              onChange={(e) => onDescriptionChange(e.target.value)}
            />
          </div>

          <div className="form-control">
            <label className="label">
              <span className="label-text font-semibold">
                {t.translations.PERMISSIONS}
              </span>
              {permissionsLoading && (
                <span className="loading loading-spinner loading-xs" />
              )}
            </label>
            <div className="grid grid-cols-2 gap-4 rounded-box border border-base-300 p-4">
              <div>
                <p className="mb-1 text-xs font-bold uppercase tracking-wide text-base-content/55">
                  {t.translations.RECORD_PERMISSIONS}
                </p>
                <div className="space-y-1">
                  {RECORD_ACTIONS.map((action) => (
                    <label
                      key={action}
                      className="flex cursor-pointer items-center gap-2"
                    >
                      <input
                        type="checkbox"
                        className="checkbox checkbox-primary checkbox-sm"
                        checked={selectedActions.has(action)}
                        disabled={permissionsLoading}
                        onChange={() => onToggleAction(action)}
                      />
                      <span className="text-sm">{actionLabel(action)}</span>
                    </label>
                  ))}
                </div>
              </div>
              <div>
                <p className="mb-1 text-xs font-bold uppercase tracking-wide text-base-content/55">
                  {t.translations.FILE_PERMISSIONS}
                </p>
                <div className="space-y-1">
                  {FILE_ACTIONS.map((action) => (
                    <label
                      key={action}
                      className="flex cursor-pointer items-center gap-2"
                    >
                      <input
                        type="checkbox"
                        className="checkbox checkbox-primary checkbox-sm"
                        checked={selectedActions.has(action)}
                        disabled={permissionsLoading}
                        onChange={() => onToggleAction(action)}
                      />
                      <span className="text-sm">{actionLabel(action)}</span>
                    </label>
                  ))}
                </div>
              </div>
            </div>
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
