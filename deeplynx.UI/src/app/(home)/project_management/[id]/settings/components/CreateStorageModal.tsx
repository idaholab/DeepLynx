// src/app/(home)/project_management/[id]/settings/components/CreateStorageModal.tsx
"use client";

import { useLanguage } from "@/app/contexts/Language";
import { ExclamationTriangleIcon } from "@heroicons/react/24/outline";

interface StorageFormData {
  name: string;
  config: Record<string, unknown>;
  default: boolean;
}

interface CreateStorageModalProps {
  isOpen: boolean;
  onToggle: (value: boolean) => void;
  storageType: string;
  setStorageType: (value: string) => void;
  storageFormData: StorageFormData;
  setStorageFormData: (value: StorageFormData) => void;
  filesystemPath: string;
  setFilesystemPath: (value: string) => void;
  s3Endpoint: string;
  setS3Endpoint: (value: string) => void;
  s3BucketName: string;
  setS3BucketName: (value: string) => void;
  onCreate: () => void;
  onResetForm: () => void;
}

const CreateStorageModal = ({
  isOpen,
  onToggle,
  storageType,
  setStorageType,
  storageFormData,
  setStorageFormData,
  filesystemPath,
  setFilesystemPath,
  s3Endpoint,
  setS3Endpoint,
  s3BucketName,
  setS3BucketName,
  onCreate,
  onResetForm,
}: CreateStorageModalProps) => {
  const { t } = useLanguage();
  return (
    <>
      <input
        type="checkbox"
        id="create_storage_modal"
        className="modal-toggle"
        checked={isOpen}
        onChange={() => onToggle(!isOpen)}
      />
      <div className="modal" role="dialog">
        <div className="modal-box max-w-2xl">
          <h3 className="text-lg font-bold mb-4">
            {t.translations.CREATE_STORAGE}
          </h3>

          <div className="form-control mb-4">
            <label className="label">
              <span className="label-text required">
                {t.translations.STORAGE_NAME}
              </span>
            </label>
            <input
              type="text"
              placeholder={t.translations.PRIMARY_STORAGE_PLACEHOLDER}
              className="input input-bordered"
              value={storageFormData.name}
              onChange={(e) =>
                setStorageFormData({ ...storageFormData, name: e.target.value })
              }
            />
          </div>

          <div className="form-control mb-4">
            <label className="label">
              <span className="label-text">
                {t.translations.STORAGE_TYPE} *
              </span>
            </label>
            <select
              className="select select-bordered"
              value={storageType}
              onChange={(e) => setStorageType(e.target.value)}
            >
              <option value="filesystem">{t.translations.FILESYSTEM}</option>
              <option value="aws_s3">
                {t.translations.AWS_S3} (Coming Soon)
              </option>
              <option value="azure_blob">
                {t.translations.AZURE_BLOB_STORAGE}
              </option>
            </select>
          </div>

          {storageType === "filesystem" && (
            <div className="form-control mb-4">
              <label className="label">
                <span className="label-text">
                  {t.translations.FILESYSTEM_PATH} *
                </span>
              </label>
              <input
                type="text"
                placeholder="/path/to/storage"
                className="input input-bordered"
                value={filesystemPath}
                onChange={(e) => setFilesystemPath(e.target.value)}
              />
              <label className="label">
                <span className="label-text-alt">
                  {t.translations.ABSOLUTE_PATH_WHERE_FILES_WILL_BE_STORED}
                </span>
              </label>
            </div>
          )}

          {storageType === "aws_s3" && (
            <div className="alert alert-warning">
              <ExclamationTriangleIcon className="h-6 w-6 text-yellow-500" />
              <div>
                <p className="font-semibold">
                  AWS S3 Configuration Coming Soon
                </p>
                <p className="text-sm">
                  The backend configuration for AWS S3 storage is currently
                  being finalized.
                </p>
              </div>
            </div>
          )}

          {storageType === "azure_blob" && (
            <>
              <div className="form-control mb-4">
                <label className="label">
                  <span className="label-text">
                    {t.translations.CONNECTION_STRING} *
                  </span>
                </label>
                <input
                  type="text"
                  placeholder="DefaultEndpointsProtocol=https;AccountName=..."
                  className="input input-bordered"
                  value={s3Endpoint}
                  onChange={(e) => setS3Endpoint(e.target.value)}
                />
              </div>

              <div className="form-control mb-4">
                <label className="label">
                  <span className="label-text">
                    {t.translations.CONTAINER_NAME} *
                  </span>
                </label>
                <input
                  type="text"
                  placeholder="my-container"
                  className="input input-bordered"
                  value={s3BucketName}
                  onChange={(e) => setS3BucketName(e.target.value)}
                />
              </div>
            </>
          )}

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
                onResetForm();
              }}
            >
              {t.translations.CANCEL}
            </button>
            <button className="btn btn-primary" onClick={onCreate}>
              {t.translations.CREATE}
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

export default CreateStorageModal;
