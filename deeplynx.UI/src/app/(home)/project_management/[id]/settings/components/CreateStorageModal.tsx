// src/app/(home)/project_management/[id]/settings/components/CreateStorageModal.tsx
"use client";

import { useLanguage } from "@/app/contexts/Language";
import {
  ChevronDownIcon,
  ChevronUpIcon,
  ExclamationTriangleIcon,
  StarIcon,
} from "@heroicons/react/24/outline";
import { useState } from "react";
import toast from "react-hot-toast";

interface StorageFormData {
  name: string;
  config: Record<string, any>;
  default: boolean;
  existingContainer?: boolean;
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
  azureEndpoint: string;
  setAzureEndpoint: (value: string) => void;
  azureBucketName: string;
  setAzureBucketName: (value: string) => void;
  onCreate: () => void;
  onCreateFromProjectName: () => void;
  isCreatingFromProjectName?: boolean;
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
  azureEndpoint,
  setAzureEndpoint,
  azureBucketName,
  setAzureBucketName,
  onCreate,
  onCreateFromProjectName,
  isCreatingFromProjectName = false,
  onResetForm,
}: CreateStorageModalProps) => {
  const { t } = useLanguage();

  const [isFilePathDisabled, setIsFilePathDisabled] = useState(false);
  const [isManualSectionOpen, setIsManualSectionOpen] = useState(false);

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

          {!(!isManualSectionOpen && storageType === "azure_object") && (
            <div className="form-control mb-4 w-full md:w-2/3">
              <label className="label">
                <span className="label-text required">
                  {t.translations.STORAGE_NAME}
                </span>
              </label>
              <input
                type="text"
                placeholder={t.translations.PRIMARY_STORAGE_PLACEHOLDER}
                className="input input-bordered w-full"
                value={storageFormData.name}
                onChange={(e) =>
                  setStorageFormData({ ...storageFormData, name: e.target.value })
                }
              />
            </div>
          )}

          <div className="form-control mb-4 w-full md:w-2/3">
            <label className="label">
              <span className="label-text">
                {t.translations.STORAGE_TYPE} *
              </span>
            </label>
            <select
              className="select select-bordered w-full"
              value={storageType}
              onChange={(e) => setStorageType(e.target.value)}
            >
              <option value="filesystem">{t.translations.FILESYSTEM}</option>
              <option value="aws_s3">
                {t.translations.AWS_S3} (t.translations.COMING_SOON)
              </option>
              <option value="azure_object">
                {t.translations.AZURE_BLOB_STORAGE}
              </option>
            </select>
          </div>

          {storageType === "filesystem" && (
            <div className="form-control mb-4 w-full md:w-2/3">
              <label className="label">
                <span className="label-text">
                  {t.translations.FILESYSTEM_PATH} *
                </span>
              </label>
              <input
                type="text"
                placeholder="/path/to/storage"
                className="input input-bordered w-full"
                value={filesystemPath}
                onChange={(e) => setFilesystemPath(e.target.value)}
              />
              <label className="label">
                <span className="text-xs text-base-content/60">
                  {t.translations.ABSOLUTE_PATH_WHERE_FILES_WILL_BE_STORED}
                </span>
              </label>

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

            </div>
          )}

          {storageType === "aws_s3" && (
            <div>
              <div className="alert alert-warning">
                <ExclamationTriangleIcon className="h-6 w-6 text-yellow-500" />
                <div>
                  <p className="font-semibold">
                    {t.translations.AWS_S3} (t.translations.COMING_SOON)
                  </p>
                  <p className="text-sm">
                    {t.translations.BACKEND_CONFIG_AWS}
                  </p>
                </div>
              </div>

              <div className="form-control mt-4">
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
            </div>
          )}

          {storageType === "azure_object" && (
            <>
              {!isManualSectionOpen && (
                <div className="rounded-lg border border-primary/30 bg-primary/5 p-4 mb-4 flex flex-col gap-4">
                  <div className="flex items-center gap-4">
                    <div className="shrink-0 flex items-center justify-center w-10 h-10 rounded-full bg-primary/10">
                      <StarIcon className="w-5 h-5 text-primary" />
                    </div>
                    <div className="flex-1">
                      <p className="text-xs font-semibold text-primary uppercase tracking-wide">
                        {t.translations.RECOMMENDED}
                      </p>
                      <p className="font-semibold">
                        {t.translations.USE_ORGANIZATION_STORAGE}
                      </p>
                      <p className="text-sm text-base-content/70">
                        {t.translations.CREATE_PROJECT_CONTAINER_HELPER}
                      </p>
                    </div>


                  </div>

                  <div className="form-control mb-2 w-full md:w-2/3">
                    <label className="label">
                      <span className="label-text">
                        {t.translations.CONTAINER_NAME}
                      </span>
                    </label>
                    <input
                      type="text"
                      placeholder="my-container"
                      className="input input-bordered w-full"
                      value={azureBucketName}
                      onChange={(e) => setAzureBucketName(e.target.value)}
                    />
                  </div>

                  {/* Existing Container Checkbox */}
                  <div className="form-control mb-2 w-full md:w-2/3">
                    <label className="cursor-pointer label flex items-center gap-2">
                      <span className="label-text">{t.translations.USE_EXISTING_CONTAINER}</span>
                      <input
                        type="checkbox"
                        className="checkbox checkbox-primary"
                        checked={storageFormData.existingContainer || false}
                        onChange={(e) =>
                          setStorageFormData({
                            ...storageFormData,
                            existingContainer: e.target.checked,
                          })
                        }
                      />
                    </label>
                  </div>

                  {/* New File Path Input */}
                  <div className="form-control mb-2">
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
                  <div className="form-control mb-2">
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

                  <button
                    className="btn btn-primary btn-sm shrink-0"
                    onClick={onCreateFromProjectName}
                    disabled={isCreatingFromProjectName}
                  >
                    {isCreatingFromProjectName && (
                      <span className="loading loading-spinner loading-xs" />
                    )}
                    {t.translations.CREATE_PROJECT_CONTAINER}
                  </button>
                </div>
              )}


              <button
                type="button"
                className="btn btn-ghost btn-sm w-full text-primary mb-4"
                onClick={() => setIsManualSectionOpen(!isManualSectionOpen)}
              >
                {t.translations.CONNECT_STORAGE_MANUALLY_INSTEAD}
                {isManualSectionOpen ? (
                  <ChevronUpIcon className="w-4 h-4" />
                ) : (
                  <ChevronDownIcon className="w-4 h-4" />
                )}
              </button>
            </>
          )}

          {storageType === "azure_object" && isManualSectionOpen && (
            <>
              <div className="form-control mb-4 w-full md:w-2/3">
                <label className="label">
                  <span className="label-text">
                    {t.translations.CONNECTION_STRING} *
                  </span>
                </label>
                <input
                  type="text"
                  placeholder="DefaultEndpointsProtocol=https;AccountName=..."
                  className="input input-bordered w-full"
                  value={azureEndpoint}
                  onChange={(e) => setAzureEndpoint(e.target.value)}
                />
              </div>

              <div className="form-control mb-4 w-full md:w-2/3">
                <label className="label">
                  <span className="label-text">
                    {t.translations.CONTAINER_NAME} *
                  </span>
                </label>
                <input
                  type="text"
                  placeholder="my-container"
                  className="input input-bordered w-full"
                  value={azureBucketName}
                  onChange={(e) => setAzureBucketName(e.target.value)}
                />
              </div>

              {/* Existing Container Checkbox */}
              <div className="form-control mb-4 w-full md:w-2/3">
                <label className="cursor-pointer label flex items-center gap-2">
                  <span className="label-text">{t.translations.USE_EXISTING_CONTAINER}</span>
                  <input
                    type="checkbox"
                    className="checkbox checkbox-primary"
                    checked={storageFormData.existingContainer || false}
                    onChange={(e) =>
                      setStorageFormData({
                        ...storageFormData,
                        existingContainer: e.target.checked,
                      })
                    }
                  />
                </label>
              </div>

              {/* New File Path Input */}
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
            </>
          )}

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
            {!(!isManualSectionOpen && storageType === "azure_object") && (
              <button
                className="btn btn-primary"
                onClick={onCreate}
                disabled={isCreatingFromProjectName}
              >
                {t.translations.CREATE}
              </button>
            )}
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