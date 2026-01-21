// src/app/(home)/project_management/[id]/settings/ProjectSettings.tsx
"use client";

import { useState, useEffect } from "react";
import toast from "react-hot-toast";
import { useProjectSession } from "@/app/contexts/ProjectSessionProvider";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import {
  archiveProject,
  getProjectLogoUrl,
  removeProjectLogo,
  uploadProjectLogo,
} from "@/app/lib/client_service/projects_services.client";
import {
  getAllProjectObjectStorages,
  getDefaultProjectObjectStorage,
  setDefaultProjectObjectStorage,
  createProjectObjectStorage,
  updateProjectObjectStorage,
  deleteProjectObjectStorage,
  archiveProjectObjectStorage,
} from "@/app/lib/client_service/object_storage_services.client";
import ArchiveDelete from "@/app/(home)/components/ArchiveDelete";
import {
  ProjectResponseDto,
  ObjectStorageResponseDto,
} from "@/app/(home)/types/responseDTOs";
import {
  CreateObjectStorageRequestDto,
  UpdateObjectStorageRequestDto,
} from "@/app/(home)/types/requestDTOs";
import { useLanguage } from "@/app/contexts/Language";
import {
  ExclamationTriangleIcon,
  InformationCircleIcon,
  CircleStackIcon,
  PlusIcon,
  PencilIcon,
  TrashIcon,
  ArchiveBoxIcon,
  ArchiveBoxXMarkIcon,
} from "@heroicons/react/24/outline";

interface ProjectSettingsProps {
  project: ProjectResponseDto | null;
}

type StorageTab = "default" | "manage";

const ProjectSettings = ({ project }: ProjectSettingsProps) => {
  const { clearProject } = useProjectSession();
  const { organization } = useOrganizationSession();
  const { t } = useLanguage();
  const [logoPreview, setLogoPreview] = useState<string | null>(null);
  const [logoFile, setLogoFile] = useState<File | null>(null);
  const [isUploading, setIsUploading] = useState(false);
  const [isCheckingLogo, setIsCheckingLogo] = useState(true);

  // Storage states
  const [activeTab, setActiveTab] = useState<StorageTab>("default");
  const [availableStorages, setAvailableStorages] = useState<
    ObjectStorageResponseDto[]
  >([]);
  const [defaultStorage, setDefaultStorage] =
    useState<ObjectStorageResponseDto | null>(null);
  const [selectedStorageId, setSelectedStorageId] = useState<number | null>(
    null,
  );
  const [isLoadingStorages, setIsLoadingStorages] = useState(true);
  const [isSavingStorage, setIsSavingStorage] = useState(false);

  // Create/Edit modal states
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [isEditModalOpen, setIsEditModalOpen] = useState(false);
  const [editingStorage, setEditingStorage] =
    useState<ObjectStorageResponseDto | null>(null);
  const [storageType, setStorageType] = useState<string>("filesystem");
  const [storageFormData, setStorageFormData] = useState({
    name: "",
    config: {},
    default: false,
  });

  // Storage config fields based on type
  const [filesystemPath, setFilesystemPath] = useState("");
  const [s3Endpoint, setS3Endpoint] = useState("");
  const [s3AccessKey, setS3AccessKey] = useState("");
  const [s3SecretKey, setS3SecretKey] = useState("");
  const [s3BucketName, setS3BucketName] = useState("");
  const [s3Region, setS3Region] = useState("us-east-1");

  // Delete/Archive modal states
  const [deleteStorageId, setDeleteStorageId] = useState<number | null>(null);
  const [archiveStorageId, setArchiveStorageId] = useState<number | null>(null);
  const [archiveAction, setArchiveAction] = useState<boolean>(true);

  // Load existing logo on mount
  useEffect(() => {
    const loadExistingLogo = async () => {
      if (!project?.id) {
        setIsCheckingLogo(false);
        return;
      }

      try {
        setIsCheckingLogo(true);
        const logoUrl = await getProjectLogoUrl(project.id as number);

        if (logoUrl) {
          setLogoPreview(logoUrl);
        }
      } catch (error) {
        console.error("Error checking for existing logo:", error);
      } finally {
        setIsCheckingLogo(false);
      }
    };

    loadExistingLogo();
  }, [project?.id]);

  // Load available storages and default storage
  const loadStorages = async () => {
    if (!organization?.organizationId || !project?.id) {
      setIsLoadingStorages(false);
      return;
    }

    try {
      setIsLoadingStorages(true);

      // Fetch all available storages for the project
      const storages = await getAllProjectObjectStorages(
        organization.organizationId as number,
        project.id as number,
        false, // Don't hide archived storages
      );
      setAvailableStorages(storages);

      // Fetch the current default storage
      try {
        const defaultStorageData = await getDefaultProjectObjectStorage(
          organization.organizationId as number,
          project.id as number,
        );
        setDefaultStorage(defaultStorageData);
        setSelectedStorageId(defaultStorageData.id as number);
      } catch (error) {
        console.log("No default storage set yet");
        setDefaultStorage(null);
        setSelectedStorageId(null);
      }
    } catch (error) {
      console.error("Error loading storages:", error);
      toast.error("Failed to load storage configurations");
    } finally {
      setIsLoadingStorages(false);
    }
  };

  useEffect(() => {
    loadStorages();
  }, [organization?.organizationId, project?.id]);

  const handleLogoChange = (fileList: FileList | null) => {
    if (!fileList || fileList.length === 0) return;

    const file = fileList[0];

    // Validate file type
    if (!file.type.startsWith("image/")) {
      toast.error(t.translations.PLEASE_UPLOAD_VALID_IMAGE);
      return;
    }

    // Validate file size (max 5MB)
    const maxSize = 5 * 1024 * 1024; // 5MB in bytes
    if (file.size > maxSize) {
      toast.error(t.translations.FILE_SIZE_MUST_BE_5MB);
      return;
    }

    setLogoFile(file);
    const previewUrl = URL.createObjectURL(file);
    setLogoPreview(previewUrl);
  };

  const handleUploadLogo = async () => {
    if (!organization?.organizationId || !project?.id || !logoFile) {
      toast.error(t.translations.NO_FILE_SELECTED);
      return;
    }

    try {
      setIsUploading(true);

      const result = await uploadProjectLogo({
        organizationId: organization.organizationId as number,
        projectId: project.id as number,
        file: logoFile,
      });

      // Add timestamp to force browser to reload the image
      setLogoPreview(`${result.logoUrl}?t=${Date.now()}`);
      setLogoFile(null);
      toast.success(t.translations.LOGO_UPLOADED_SUCCESSFULLY);
    } catch (error) {
      console.error("Failed to upload logo:", error);
      toast.error(
        error instanceof Error
          ? error.message
          : t.translations.FAILED_TO_UPLOAD_LOGO,
      );
    } finally {
      setIsUploading(false);
    }
  };

  const handleRemoveLogo = async () => {
    if (!organization?.organizationId || !project?.id) return;

    try {
      await removeProjectLogo({
        organizationId: organization.organizationId as number,
        projectId: project.id as number,
      });

      setLogoFile(null);
      setLogoPreview(null);
      toast.success(t.translations.LOGO_REMOVED_SECCESSFULLY);
    } catch (error) {
      console.error("Failed to remove logo:", error);
      toast.error(t.translations.FAILED_TO_REMOVE_LOGO);
    }
  };

  const handleCancelSelection = async () => {
    setLogoFile(null);

    // Restore previous logo if it exists
    if (project?.id) {
      const logoUrl = await getProjectLogoUrl(project.id as number);
      setLogoPreview(logoUrl);
    } else {
      setLogoPreview(null);
    }
  };

  const handleSaveDefaultStorage = async () => {
    if (!organization?.organizationId || !project?.id || !selectedStorageId) {
      toast.error("Please select a storage location");
      return;
    }

    // Check if the selected storage is already the default
    if (defaultStorage?.id === selectedStorageId) {
      toast.error("This storage is already set as default");
      return;
    }

    try {
      setIsSavingStorage(true);

      await setDefaultProjectObjectStorage(
        organization.organizationId as number,
        project.id as number,
        selectedStorageId,
      );

      // Update the default storage state
      const updatedDefault = availableStorages.find(
        (s) => s.id === selectedStorageId,
      );
      if (updatedDefault) {
        setDefaultStorage(updatedDefault);
      }

      toast.success("Default storage location updated successfully");
    } catch (error) {
      console.error("Failed to set default storage:", error);
      toast.error(
        error instanceof Error
          ? error.message
          : "Failed to update default storage",
      );
    } finally {
      setIsSavingStorage(false);
    }
  };

  const resetStorageForm = () => {
    setStorageFormData({ name: "", config: {}, default: false });
    setStorageType("filesystem");
    setFilesystemPath("");
    setS3Endpoint("");
    setS3AccessKey("");
    setS3SecretKey("");
    setS3BucketName("");
    setS3Region("us-east-1");
  };

  const handleCreateStorage = async () => {
    if (!organization?.organizationId || !project?.id) return;

    if (!storageFormData.name.trim()) {
      toast.error("Storage name is required");
      return;
    }

    // Build config based on storage type
    let config: any = {};

    if (storageType === "filesystem") {
      if (!filesystemPath.trim()) {
        toast.error("Filesystem path is required");
        return;
      }
      config = {
        mountPath: filesystemPath,
      };
    } else if (storageType === "azure_blob") {
      if (!s3Endpoint.trim() || !s3BucketName.trim()) {
        toast.error("All Azure Blob fields are required");
        return;
      }
      config = {
        azureConnectionString: s3Endpoint,
        containerName: s3BucketName,
      };
    } else if (storageType === "aws_s3") {
      // TODO: Waiting for backend to finalize AWS S3 config structure
      toast.error("AWS S3 storage configuration is not yet implemented");
      return;
    }

    try {
      const dto: CreateObjectStorageRequestDto = {
        name: storageFormData.name,
        config: config,
      };

      console.log("Creating storage with DTO:", JSON.stringify(dto, null, 2));
      console.log("Config object:", JSON.stringify(config, null, 2));

      await createProjectObjectStorage(
        organization.organizationId as number,
        project.id as number,
        dto,
        storageFormData.default,
      );

      toast.success("Storage created successfully");
      setIsCreateModalOpen(false);
      resetStorageForm();
      loadStorages();
    } catch (error) {
      console.error("Failed to create storage:", error);
      console.error("Error details:", error);
      toast.error(
        error instanceof Error ? error.message : "Failed to create storage",
      );
    }
  };

  const handleEditStorage = async () => {
    if (!organization?.organizationId || !project?.id || !editingStorage)
      return;
    if (editingStorage.isArchived) {
      toast.error("Archived storages cannot be edited");
      return;
    }

    if (!storageFormData.name.trim()) {
      toast.error("Storage name is required");
      return;
    }

    try {
      const dto: UpdateObjectStorageRequestDto = {
        name: storageFormData.name,
        default: storageFormData.default,
      };

      await updateProjectObjectStorage(
        organization.organizationId as number,
        project.id as number,
        editingStorage.id as number,
        dto,
      );

      toast.success("Storage updated successfully");
      setIsEditModalOpen(false);
      setEditingStorage(null);
      setStorageFormData({ name: "", config: {}, default: false });
      loadStorages();
    } catch (error) {
      console.error("Failed to update storage:", error);
      toast.error(
        error instanceof Error ? error.message : "Failed to update storage",
      );
    }
  };

  const handleDeleteStorage = async () => {
    if (!organization?.organizationId || !project?.id || !deleteStorageId)
      return;

    try {
      await deleteProjectObjectStorage(
        organization.organizationId as number,
        project.id as number,
        deleteStorageId,
      );

      toast.success("Storage deleted successfully");
      setDeleteStorageId(null);
      loadStorages();
    } catch (error) {
      console.error("Failed to delete storage:", error);
      toast.error(
        error instanceof Error ? error.message : "Failed to delete storage",
      );
    }
  };

  const handleArchiveStorage = async () => {
    if (!organization?.organizationId || !project?.id || !archiveStorageId)
      return;

    try {
      await archiveProjectObjectStorage(
        organization.organizationId as number,
        project.id as number,
        archiveStorageId,
        archiveAction,
      );

      toast.success(
        `Storage ${archiveAction ? "archived" : "unarchived"} successfully`,
      );
      setArchiveStorageId(null);
      loadStorages();
    } catch (error) {
      console.error("Failed to archive/unarchive storage:", error);
      toast.error(
        error instanceof Error ? error.message : "Failed to archive storage",
      );
    }
  };

  const openEditModal = (storage: ObjectStorageResponseDto) => {
    if (storage.isArchived) {
      return;
    }
    setEditingStorage(storage);
    setStorageFormData({
      name: storage.name,
      config: {},
      default: storage.default,
    });
    setIsEditModalOpen(true);
  };

  if (isCheckingLogo || isLoadingStorages) {
    return (
      <div className="p-6 flex items-center justify-center min-h-[400px]">
        <span className="loading loading-spinner loading-lg"></span>
      </div>
    );
  }

  if (!project) {
    return (
      <div className="p-6">
        <div className="alert alert-warning">
          <ExclamationTriangleIcon className="size-6" />
          <span>{t.translations.NO_PROJECT_SELECTED}</span>
        </div>
      </div>
    );
  }

  return (
    <div className="p-6">
      <div className="mx-auto space-y-6">
        {/* Page Header */}
        <div className="border-b border-base-300 pb-4">
          <h2 className="text-2xl font-bold text-base-content">
            {t.translations.PROJECT_SETTINGS}
          </h2>
          <p className="text-base-content/70 text-sm mt-1">
            {t.translations.CONFIGURE_BRANDING_AND_MANAGE_YOUR_PROJECT}
          </p>
        </div>

        {/* Two-column layout for Logo and Storage */}
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
          {/* Logo Section */}
          <div className="card bg-base-100 border border-primary/40 shadow-sm">
            <div className="card-body">
              <h3 className="card-title text-lg mb-4">
                {t.translations.PROJECT_LOGO}
              </h3>

              <div className="flex items-start gap-6 mb-6">
                {/* Logo Preview */}
                <div className="avatar">
                  <div className="w-32 h-32 rounded-xl bg-base-200 flex items-center justify-center overflow-hidden border-2 border-base-300">
                    {logoPreview ? (
                      <img
                        src={logoPreview}
                        alt="Project Logo"
                        className="object-contain w-full h-full p-2"
                        onError={() => {
                          setLogoPreview(null);
                        }}
                      />
                    ) : (
                      <div className="text-center p-4">
                        <span className="text-base-content/40 text-sm">
                          {t.translations.NO_LOGO}
                        </span>
                      </div>
                    )}
                  </div>
                </div>

                {/* Logo Controls */}
                <div className="flex flex-col gap-3 flex-1">
                  <div>
                    <span className="font-semibold text-lg block">
                      {project?.name || "Project"}
                    </span>
                    <span className="text-sm text-base-content/60">
                      {t.translations.PROJECT_LOGO}
                    </span>
                  </div>

                  <div className="flex flex-wrap gap-2">
                    <label className="btn btn-sm btn-primary">
                      {logoFile ? "Change Logo" : "Select Logo"}
                      <input
                        type="file"
                        accept=".png,.jpg,.jpeg,.svg,.webp"
                        className="hidden"
                        onChange={(e) => handleLogoChange(e.target.files)}
                      />
                    </label>

                    {logoFile && (
                      <>
                        <button
                          type="button"
                          className="btn btn-sm btn-success"
                          onClick={handleUploadLogo}
                          disabled={isUploading}
                        >
                          {isUploading && (
                            <span className="loading loading-spinner loading-xs" />
                          )}
                          {t.translations.UPLOAD}
                        </button>

                        <button
                          type="button"
                          className="btn btn-sm btn-ghost"
                          onClick={handleCancelSelection}
                          disabled={isUploading}
                        >
                          {t.translations.CANCEL}
                        </button>
                      </>
                    )}

                    {logoPreview && !logoFile && (
                      <label
                        htmlFor="remove_project_logo"
                        className="btn btn-sm btn-error btn-outline"
                      >
                        {t.translations.REMOVE_LOGO}
                      </label>
                    )}
                  </div>

                  {logoFile && (
                    <div className="alert alert-info">
                      <InformationCircleIcon className="size-5" />
                      <span className="text-sm">
                        {t.translations.CLICK_UPLOAD_TO_SAVE_YOUR_CHANGES}
                      </span>
                    </div>
                  )}

                  <div className="text-xs text-base-content/60 bg-base-200 p-3 rounded-lg">
                    <p className="font-semibold mb-1">
                      {t.translations.LOGO_GUIDLINES}:
                    </p>
                    <ul className="list-disc list-inside space-y-1">
                      <li>
                        {
                          t.translations
                            .REPLACES_THE_FOLDER_ICON_NEXT_TO_THE_PROJECT_NAME
                        }
                      </li>
                      <li>
                        {
                          t.translations
                            .RECOMMENDED_PNG_WITH_TRANSPARENT_BACKGROUND
                        }
                      </li>
                      <li>{t.translations.OPTIMAL_SIZE_FOR_LOGO}</li>
                      <li>{t.translations.FILE_SIZE_MUST_BE_5MB}</li>
                      <li>{t.translations.SUPPORTED_FORMATS_FOR_LOGO}</li>
                    </ul>
                  </div>
                </div>
              </div>
            </div>
          </div>

          {/* Storage Settings Section with Tabs */}
          <div className="card bg-base-100 border border-primary/40 shadow-sm">
            <div className="card-body">
              <div className="flex justify-between">
                <div className="flex items-center gap-2 mb-4">
                  <CircleStackIcon className="w-6 h-6 text-primary" />
                  <h3 className="card-title text-lg">
                    {t.translations.STORAGE_SETTINGS}
                  </h3>
                </div>
                {/* Save Button */}
                {selectedStorageId &&
                  selectedStorageId !== defaultStorage?.id && (
                    <button
                      className="btn btn-primary btn-sm mt-2 ml-4"
                      onClick={handleSaveDefaultStorage}
                      disabled={isSavingStorage}
                    >
                      {isSavingStorage && (
                        <span className="loading loading-spinner loading-xs" />
                      )}
                      Save Default Storage
                    </button>
                  )}
              </div>

              {/* Tabs */}
              <div role="tablist" className="tabs tabs-bordered mb-4 gap-4">
                <button
                  role="tab"
                  className={`tab ${activeTab === "default" ? "tab-active" : ""}`}
                  onClick={() => setActiveTab("default")}
                >
                  Default Storage
                </button>
                <button
                  role="tab"
                  className={`tab ${activeTab === "manage" ? "tab-active" : ""}`}
                  onClick={() => setActiveTab("manage")}
                >
                  Manage Storages
                </button>
              </div>

              {/* Tab Content */}
              {activeTab === "default" && (
                <div>
                  <p className="text-sm text-base-content/70 mb-4">
                    {t.translations.SET_DEFAULT_UNMOUNTED_OBJECT_STORAGE}
                  </p>

                  <div className="form-control">
                    <label className="label">
                      <span className="label-text font-semibold mr-4 mb-6">
                        {t.translations.DEFAULT_UNMOUNT_STORAGE}
                      </span>
                    </label>

                    {availableStorages.length === 0 ? (
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
                        <span>
                          No storage locations available. Create one in the
                          "Manage Storages" tab.
                        </span>
                      </div>
                    ) : (
                      <>
                        <select
                          className="select select-bordered"
                          value={selectedStorageId || ""}
                          onChange={(e) =>
                            setSelectedStorageId(Number(e.target.value))
                          }
                        >
                          <option value="" disabled>
                            Select a storage location
                          </option>
                          {availableStorages.map((storage) => (
                            <option key={storage.id} value={storage.id}>
                              {storage.name}
                              {storage.default ? " (Current Default)" : ""}
                              {storage.isArchived ? " [Archived]" : ""}
                            </option>
                          ))}
                        </select>

                        {/* Current Default Display */}
                        {defaultStorage && (
                          <div>
                            <label className="label">
                              <span className="label-text-alt text-base-content/60">
                                This will be the default storage for data
                                sources in this project
                              </span>
                            </label>
                            <div className="alert alert-info mt-3">
                              <svg
                                xmlns="http://www.w3.org/2000/svg"
                                fill="none"
                                viewBox="0 0 24 24"
                                className="stroke-current shrink-0 w-6 h-6"
                              >
                                <path
                                  strokeLinecap="round"
                                  strokeLinejoin="round"
                                  strokeWidth="2"
                                  d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"
                                />
                              </svg>
                              <span className="text-sm">
                                Current default:{" "}
                                <strong>{defaultStorage.name}</strong>
                              </span>
                            </div>
                          </div>
                        )}
                      </>
                    )}
                  </div>
                </div>
              )}

              {activeTab === "manage" && (
                <div>
                  <div className="flex justify-between items-center mb-4">
                    <p className="text-sm text-base-content/70">
                      Create, edit, and manage your storage locations
                    </p>
                    <button
                      className="btn btn-primary btn-sm"
                      onClick={() => {
                        resetStorageForm();
                        setIsCreateModalOpen(true);
                      }}
                    >
                      <PlusIcon className="w-4 h-4" />
                      Create Storage
                    </button>
                  </div>

                  {/* Storage List */}
                  <div className="space-y-2 max-h-96 overflow-y-auto">
                    {availableStorages.length === 0 ? (
                      <div className="text-center py-8 text-base-content/60">
                        <CircleStackIcon className="w-12 h-12 mx-auto mb-2 opacity-50" />
                        <p>No storages created yet</p>
                        <p className="text-sm">
                          Click "Create Storage" to add one
                        </p>
                      </div>
                    ) : (
                      availableStorages.map((storage) => (
                        <div
                          key={storage.id}
                          className={`card bg-base-200 border ${storage.isArchived ? "border-warning/30 opacity-60" : "border-base-300"}`}
                        >
                          <div className="card-body p-4">
                            <div className="flex items-start justify-between">
                              <div className="flex-1">
                                <div className="flex items-center gap-2 mb-1">
                                  <h4 className="font-semibold">
                                    {storage.name}
                                  </h4>
                                  {storage.default && (
                                    <span className="badge badge-primary badge-sm">
                                      Default
                                    </span>
                                  )}
                                  {storage.isArchived && (
                                    <span className="badge badge-warning badge-sm">
                                      Archived
                                    </span>
                                  )}
                                </div>
                                <p className="text-xs text-base-content/60">
                                  Type: {storage.type || "N/A"}
                                </p>
                                <p className="text-xs text-base-content/60">
                                  Last updated:{" "}
                                  {new Date(
                                    storage.lastUpdatedAt,
                                  ).toLocaleDateString()}
                                </p>
                              </div>

                              <div className="flex gap-1">
                                <button
                                  className="btn btn-ghost btn-xs disabled:cursor-not-allowed"
                                  onClick={() => openEditModal(storage)}
                                  disabled={storage.isArchived}
                                  title={
                                    storage.isArchived
                                      ? "Archived storages cannot be edited"
                                      : "Edit"
                                  }
                                >
                                  <PencilIcon className="w-4 h-4" />
                                </button>
                                <button
                                  className="btn btn-ghost btn-xs"
                                  onClick={() => {
                                    setArchiveStorageId(storage.id as number);
                                    setArchiveAction(!storage.isArchived);
                                  }}
                                  title={
                                    storage.isArchived ? "Unarchive" : "Archive"
                                  }
                                >
                                  {storage.isArchived ? (
                                    <ArchiveBoxXMarkIcon className="w-4 h-4" />
                                  ) : (
                                    <ArchiveBoxIcon className="w-4 h-4" />
                                  )}
                                </button>
                                <button
                                  className="btn btn-ghost btn-xs text-error"
                                  onClick={() =>
                                    setDeleteStorageId(storage.id as number)
                                  }
                                  title="Delete"
                                >
                                  <TrashIcon className="w-4 h-4" />
                                </button>
                              </div>
                            </div>
                          </div>
                        </div>
                      ))
                    )}
                  </div>
                </div>
              )}
            </div>
          </div>
        </div>

        {/* Archive Project Section */}
        <div className="mt-8">
          <ArchiveDelete
            actionType="archive"
            itemType="Project"
            itemName={project?.name || ""}
            onConfirm={async () => {
              if (organization && project) {
                await archiveProject(
                  organization.organizationId as number,
                  project.id as number,
                  true,
                );
              }
              clearProject();
              window.location.href = "/";
            }}
          />
        </div>
      </div>

      {/* Create Storage Modal */}
      <input
        type="checkbox"
        id="create_storage_modal"
        className="modal-toggle"
        checked={isCreateModalOpen}
        onChange={() => setIsCreateModalOpen(!isCreateModalOpen)}
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

          {/* Filesystem Config */}
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

          {/* S3/MinIO Config */}
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

          {/* Azure Blob Config */}
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
                setIsCreateModalOpen(false);
                resetStorageForm();
              }}
            >
              Cancel
            </button>
            <button className="btn btn-primary" onClick={handleCreateStorage}>
              Create
            </button>
          </div>
        </div>
        <label
          className="modal-backdrop"
          onClick={() => setIsCreateModalOpen(false)}
        >
          Close
        </label>
      </div>

      {/* Edit Storage Modal */}
      <input
        type="checkbox"
        id="edit_storage_modal"
        className="modal-toggle"
        checked={isEditModalOpen}
        onChange={() => setIsEditModalOpen(!isEditModalOpen)}
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
                setIsEditModalOpen(false);
                setEditingStorage(null);
                setStorageFormData({ name: "", config: {}, default: false });
              }}
            >
              Cancel
            </button>
            <button className="btn btn-primary" onClick={handleEditStorage}>
              Save Changes
            </button>
          </div>
        </div>
        <label
          className="modal-backdrop"
          onClick={() => setIsEditModalOpen(false)}
        >
          Close
        </label>
      </div>

      {/* Delete Storage Modal */}
      <input
        type="checkbox"
        id="delete_storage_modal"
        className="modal-toggle"
        checked={deleteStorageId !== null}
        onChange={() => setDeleteStorageId(null)}
      />
      <div className="modal" role="dialog">
        <div className="modal-box">
          <h3 className="text-lg font-bold text-error">Delete Storage</h3>
          <p className="py-4">
            Are you sure you want to delete this storage? This action cannot be
            undone.
          </p>
          <div className="modal-action">
            <button className="btn" onClick={() => setDeleteStorageId(null)}>
              Cancel
            </button>
            <button className="btn btn-error" onClick={handleDeleteStorage}>
              Delete
            </button>
          </div>
        </div>
        <label
          className="modal-backdrop"
          onClick={() => setDeleteStorageId(null)}
        >
          Close
        </label>
      </div>

      {/* Archive/Unarchive Storage Modal */}
      <input
        type="checkbox"
        id="archive_storage_modal"
        className="modal-toggle"
        checked={archiveStorageId !== null}
        onChange={() => setArchiveStorageId(null)}
      />
      <div className="modal" role="dialog">
        <div className="modal-box">
          <h3 className="text-lg font-bold">
            {archiveAction ? "Archive" : "Unarchive"} Storage
          </h3>
          <p className="py-4">
            Are you sure you want to {archiveAction ? "archive" : "unarchive"}{" "}
            this storage?
          </p>
          <div className="modal-action">
            <button className="btn" onClick={() => setArchiveStorageId(null)}>
              Cancel
            </button>
            <button className="btn btn-warning" onClick={handleArchiveStorage}>
              {archiveAction ? "Archive" : "Unarchive"}
            </button>
          </div>
        </div>
        <label
          className="modal-backdrop"
          onClick={() => setArchiveStorageId(null)}
        >
          Close
        </label>
      </div>

      {/* Remove Logo Modal */}
      <input
        type="checkbox"
        id="remove_project_logo"
        className="modal-toggle"
      />
      <div className="modal" role="dialog">
        <div className="modal-box">
          <h3 className="text-lg font-bold">{t.translations.REMOVE_LOGO}</h3>
          <p className="py-4">
            {t.translations.ARE_YOU_SURE_YOU_WANT_TO_REMOVE_LOGO_FROM_PROJECT}
          </p>
          <div className="modal-action">
            <label htmlFor="remove_project_logo" className="btn">
              {t.translations.CANCEL}
            </label>
            <label
              htmlFor="remove_project_logo"
              className="btn btn-outline btn-secondary"
              onClick={handleRemoveLogo}
            >
              {t.translations.REMOVE}
            </label>
          </div>
        </div>
      </div>
    </div>
  );
};

export default ProjectSettings;
