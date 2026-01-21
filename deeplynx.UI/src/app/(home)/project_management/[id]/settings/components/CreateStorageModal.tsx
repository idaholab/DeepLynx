// src/app/(home)/project_management/[id]/settings/components/CreateStorageModal.tsx
"use client";

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
}: CreateStorageModalProps) => (
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
        <h3 className="text-lg font-bold mb-4">Create Storage</h3>

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
          <label className="label">
            <span className="label-text">Storage Type *</span>
          </label>
          <select
            className="select select-bordered"
            value={storageType}
            onChange={(e) => setStorageType(e.target.value)}
          >
            <option value="filesystem">Filesystem</option>
            <option value="aws_s3">AWS S3 (Coming Soon)</option>
            <option value="azure_blob">Azure Blob Storage</option>
          </select>
        </div>

        {storageType === "filesystem" && (
          <div className="form-control mb-4">
            <label className="label">
              <span className="label-text">Filesystem Path *</span>
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
                Absolute path where files will be stored
              </span>
            </label>
          </div>
        )}

        {storageType === "aws_s3" && (
          <div className="alert alert-warning">
            <svg
              xmlns="http://www.w3.org/2000/svg"
              className="stroke-current shrink-0 h-6 w-6"
              fill="none"
              viewBox="0 0 24 24"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth="2"
                d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"
              />
            </svg>
            <div>
              <p className="font-semibold">AWS S3 Configuration Coming Soon</p>
              <p className="text-sm">
                The backend configuration for AWS S3 storage is currently being
                finalized.
              </p>
            </div>
          </div>
        )}

        {storageType === "azure_blob" && (
          <>
            <div className="form-control mb-4">
              <label className="label">
                <span className="label-text">Connection String *</span>
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
                <span className="label-text">Container Name *</span>
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
              onResetForm();
            }}
          >
            Cancel
          </button>
          <button className="btn btn-primary" onClick={onCreate}>
            Create
          </button>
        </div>
      </div>
      <label className="modal-backdrop" onClick={() => onToggle(false)}>
        Close
      </label>
    </div>
  </>
);

export default CreateStorageModal;
