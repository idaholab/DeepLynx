"use client";

import { ObjectStorageResponseDto } from "@/app/(home)/types/responseDTOs";
import { useLanguage } from "@/app/contexts/Language";
import { useState } from "react";
import toast from "react-hot-toast";

interface AzureObjectConfig {
  AzureFilePath?: string;
}

interface StorageConfig {
  AzureObjectConfig?: AzureObjectConfig;
}
interface StorageFormData {
  name: string;
  config: StorageConfig;
  default: boolean;
  existingContainer?: boolean;
}

interface EditStorageModalProps {
  isOpen: boolean;
  onToggle: (value: boolean) => void;
  storageFormData: StorageFormData;
  setStorageFormData: (value: StorageFormData) => void;
  onEdit: () => void;
  editingStorage: ObjectStorageResponseDto | null;
  setEditingStorage: (value: ObjectStorageResponseDto | null) => void;
}

const EditStorageModal = ({
  isOpen,
  onToggle,
  storageFormData,
  setStorageFormData,
  onEdit,
  editingStorage,
  setEditingStorage,
}: EditStorageModalProps) => {
  const { t } = useLanguage();

  const [isFilePathDisabled, setIsFilePathDisabled] = useState(false);

  // Helper to safely get or set nested AzureFilePath
  const getAzureFilePath = () =>
    storageFormData.config.AzureObjectConfig?.AzureFilePath ?? "";

  const validateAzureFilePath = (filePath: string): boolean => {
    const filePathRegex = /^[a-zA-Z0-9/]*$/;
    return filePathRegex.test(filePath);
  };

  const setAzureFilePath = (value: string) => {
    if (!validateAzureFilePath(value)) {
      toast.error(t.translations.INVALID_FILE_PATH);
      return;
    }
    setStorageFormData({
      ...storageFormData,
      config: {
        ...storageFormData.config,
        AzureObjectConfig: {
          ...(storageFormData.config.AzureObjectConfig ?? {}),
          AzureFilePath: value,
        },
      },
    });
  };

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

          {/* Storage Name */}
          <div className="form-control mb-4">
            <label className="label">
              <span className="label-text">{t.translations.STORAGE_NAME} *</span>
            </label>
            <input
              type="text"
              placeholder="e.g., Primary Storage"
              className="input input-bordered"
              value={storageFormData.name}
              disabled={editingStorage?.projectId == null}
              onChange={(e) =>
                setStorageFormData({ ...storageFormData, name: e.target.value })
              }
            />
          </div>

          {/* Azure File Path */}
          <div className="form-control mb-4">
            <label className="label">
              <span className="label-text mr-2">{t.translations.FILE_PATH}</span>
            </label>
            <input
              type="text"
              placeholder="e.g., path/to/container/folder"
              className="input input-bordered"
              value={getAzureFilePath()}
              disabled={isFilePathDisabled}
              onChange={(e) => setAzureFilePath(e.target.value)}
            />
          </div>

          {/* No File Pathing Checkbox */}
          <div className="form-control mb-4">
            <label className="cursor-pointer label flex items-center space-x-2">
              <span>{t.translations.NO_FILE_PATHING}</span>
              <input
                type="checkbox"
                checked={isFilePathDisabled}
                onChange={(e) => {
                  const checked = e.target.checked;
                  setIsFilePathDisabled(checked);
                  if (checked) {
                    setAzureFilePath("/");
                  } else {
                    setAzureFilePath("");
                  }
                }}
                className="checkbox checkbox-primary"
              />

            </label>
          </div>

          {/* Set as Default Storage */}
          <div className="form-control mb-4">
            <label className="cursor-pointer label">
              <span className="label-text">{t.translations.SET_AS_DEFAULT_STORAGE}</span>
              <input
                type="checkbox"
                className="checkbox checkbox-primary"
                checked={storageFormData.default}
                disabled={editingStorage?.projectId == null}
                onChange={(e) =>
                  setStorageFormData({
                    ...storageFormData,
                    default: e.target.checked,
                  })
                }
              />
            </label>
          </div>

          {/* Actions */}
          <div className="modal-action">
            <button
              className="btn"
              onClick={() => {
                onToggle(false);
                setEditingStorage(null);
                setStorageFormData({ name: "", config: {}, default: false });
                setIsFilePathDisabled(false);
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