// src/app/(home)/project_management/[id]/settings/components/EditStorageModal.tsx
"use client";

import { ObjectStorageResponseDto } from "@/app/(home)/types/responseDTOs";

interface StorageFormData {
  name: string;
  config: Record<string, unknown>;
  default: boolean;
}

interface EditStorageModalProps {
  isOpen: boolean;
  onToggle: (value: boolean) => void;
  storageFormData: StorageFormData;
  setStorageFormData: (value: StorageFormData) => void;
  onEdit: () => void;
  setEditingStorage: (value: ObjectStorageResponseDto | null) => void;
}

const EditStorageModal = ({
  isOpen,
  onToggle,
  storageFormData,
  setStorageFormData,
  onEdit,
  setEditingStorage,
}: EditStorageModalProps) => (
  <>
    <input
      type="checkbox"
      id="edit_storage_modal"
      className="modal-toggle"
      checked={isOpen}
      onChange={() => onToggle(!isOpen)}
    />
    <div className="modal" role="dialog">
      <div className="modal-box">
        <h3 className="text-lg font-bold mb-4">Edit Storage</h3>

        <div className="form-control mb-4">
          <label className="label">
            <span className="label-text">Storage Name *</span>
          </label>
          <input
            type="text"
            placeholder="e.g., Primary Storage"
            className="input input-bordered"
            value={storageFormData.name}
            onChange={(e) =>
              setStorageFormData({ ...storageFormData, name: e.target.value })
            }
          />
        </div>

        <div className="form-control mb-4">
          <label className="cursor-pointer label">
            <span className="label-text">Set as default storage</span>
            <input
              type="checkbox"
              className="checkbox checkbox-primary"
              checked={storageFormData.default}
              onChange={(e) =>
                setStorageFormData({
                  ...storageFormData,
                  default: e.target.checked,
                })
              }
            />
          </label>
        </div>

        <div className="modal-action">
          <button
            className="btn"
            onClick={() => {
              onToggle(false);
              setEditingStorage(null);
              setStorageFormData({ name: "", config: {}, default: false });
            }}
          >
            Cancel
          </button>
          <button className="btn btn-primary" onClick={onEdit}>
            Save Changes
          </button>
        </div>
      </div>
      <label className="modal-backdrop" onClick={() => onToggle(false)}>
        Close
      </label>
    </div>
  </>
);

export default EditStorageModal;
