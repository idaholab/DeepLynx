import React from "react";

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
  if (!isOpen) return null;

  const disabled = !nameInput.trim() || isSaving;

  return (
    <div className="modal modal-open">
      <div className="modal-box max-w-md">
        <h3 className="font-bold text-lg mb-2">
          {editingLabel ? "Edit Label" : "Create Label"}
        </h3>
        <p className="text-xs text-base-content/70 mb-4">
          Define an organization-level security label. Projects inherit this
          label and can use it across their assets.
        </p>

        <div className="space-y-4">
          <div className="form-control">
            <label className="label">
              <span className="label-text font-semibold">
                Label Name <span className="text-error">*</span>
              </span>
            </label>
            <input
              type="text"
              className="input input-bordered input-sm"
              placeholder="e.g., CUI, ITAR, Public"
              value={nameInput}
              onChange={(e) => onNameChange(e.target.value)}
            />
          </div>

          <div className="form-control">
            <label className="label">
              <span className="label-text font-semibold">Description</span>
            </label>
            <textarea
              className="textarea textarea-bordered textarea-sm"
              placeholder="Optional description for this label"
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
            Cancel
          </button>
          <button
            type="button"
            className="btn btn-primary btn-sm"
            disabled={disabled}
            onClick={onSave}
          >
            {isSaving
              ? "Saving..."
              : editingLabel
              ? "Save Label"
              : "Create Label"}
          </button>
        </div>
      </div>
      <div className="modal-backdrop" onClick={onCancel} />
    </div>
  );
};

export default LabelEditModal;
