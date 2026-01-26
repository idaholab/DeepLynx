// src/app/(home)/project_management/[id]/settings/components/EditStorageModal.tsx
"use client";

import { ObjectStorageResponseDto } from "@/app/(home)/types/responseDTOs";
import { useLanguage } from "@/app/contexts/Language";

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
}: EditStorageModalProps) => {
  const { t } = useLanguage();
  return (
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
          <h3 className="text-lg font-bold mb-4">
            {t.translations.EDIT_STORAGE}
          </h3>

          <div className="form-control mb-4">
            <label className="label">
              <span className="label-text">
                {t.translations.STORAGE_NAME} *
              </span>
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
              <span className="label-text">
                {t.translations.SET_AS_DEFAULT_STORAGE}
              </span>
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
              {t.translations.CANCEL}
            </button>
            <button className="btn btn-primary" onClick={onEdit}>
              {t.translations.SAVE_CHANGES}
            </button>
          </div>
        </div>
        <label className="modal-backdrop" onClick={() => onToggle(false)}>
          {t.translations.CLOSE}
        </label>
      </div>
    </>
  );
};

export default EditStorageModal;
