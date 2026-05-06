// src/app/(home)/project_management/[id]/settings/ProjectSettings.tsx
"use client";

import { useState, useEffect, useCallback } from "react";
import Image from "next/image";
import toast from "react-hot-toast";
import { useProjectSession } from "@/app/contexts/ProjectSessionProvider";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import {
  archiveProject,
  getProjectLogoUrl,
  removeProjectLogo,
  updateProject,
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
import ProjectSettingsLeftColumn from "./components/ProjectSettingsLeftColumn";
import StorageSettingsSection from "./components/StorageSettingsSection";
import CreateStorageModal from "./components/CreateStorageModal";
import EditStorageModal from "./components/EditStorageModal";
import DeleteStorageModal from "./components/DeleteStorageModal";
import ArchiveStorageModal from "./components/ArchiveStorageModal";
import RemoveLogoModal from "./components/RemoveLogoModal";
import { useLanguage } from "@/app/contexts/Language";
import { ExclamationTriangleIcon } from "@heroicons/react/24/outline";

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
  const [azureEndpoint, setAzureEndpoint] = useState("");
  const [azureBucketName, setAzureBucketName] = useState("");

  // Delete/Archive modal states
  const [deleteStorageId, setDeleteStorageId] = useState<number | null>(null);
  const [archiveStorageId, setArchiveStorageId] = useState<number | null>(null);
  const [archiveAction, setArchiveAction] = useState<boolean>(true);

  const [bannerText, setBannerText] = useState<string>("");
  const [originalBannerText, setOriginalBannerText] = useState<string>("");
  const [isSavingBanner, setIsSavingBanner] = useState(false);

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

  useEffect(() => {
    if (project?.banner !== undefined) {
      const banner = project.banner || "";
      setBannerText(banner);
      setOriginalBannerText(banner);
    }
  }, [project?.banner]);

  const handleSaveBanner = async () => {
    if (!organization?.banner || !project?.id) {
      toast.error(t.translations.NO_PROJECT_SELECTED);
      return;
    }

    if (bannerText === originalBannerText) {
      toast.custom(
        <div className="text-info">
          <ExclamationTriangleIcon className="size-4" />
          {t.translations.NO_CHANGES_TO_SAVE}
        </div>,
      );
      return;
    }

    if (bannerText.length > 50) {
      toast.error(t.translations.BANNER_TEXT_MUST_BE_50_CHARACTERS_OR_LESS);
      return;
    }

    try {
      setIsSavingBanner(true);

      await updateProject(
        organization.organizationId as number,
        project.id as number,
        {
          organizationId: organization.organizationId as number,
          banner: bannerText.trim() || null,
        },
      );

      setOriginalBannerText(bannerText);

      toast.success(t.translations.BANNER_UPDATED_SUCCESSFULLY);
    } catch (error) {
      console.error("Failed to update Project Banner: ", error);
      toast.error(
        error instanceof Error
          ? error.message
          : t.translations.FAILED_TO_UPDATE_BANNER,
      );
    } finally {
      setIsSavingBanner(false);
    }
  };

  const handleCancelBanner = () => {
    setBannerText(originalBannerText);
    toast.custom(
      <div className="text-info">
        <ExclamationTriangleIcon className="size-4" />
        {t.translations.CHANGES_DISCARDED}
      </div>,
    );
  };

  // Load available storages and default storage
  const loadStorages = useCallback(async () => {
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
        setDefaultStorage(null);
        setSelectedStorageId(null);
      }
    } catch (error) {
      console.error("Error loading storages:", error);
      toast.error(t.translations.FAILED_TO_LOAD_STORAGE_CONFIGURATIONS);
    } finally {
      setIsLoadingStorages(false);
    }
  }, [
    organization?.organizationId,
    project?.id,
    t.translations.FAILED_TO_LOAD_STORAGE_CONFIGURATIONS,
  ]);

  useEffect(() => {
    loadStorages();
  }, [loadStorages]);

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
      toast.success(t.translations.LOGO_REMOVED_SUCCESSFULLY);
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
      toast.error(t.translations.PLEASE_SELECT_A_STORAGE_LOCATION);
      return;
    }

    // Check if the selected storage is already the default
    if (defaultStorage?.id === selectedStorageId) {
      toast.error(t.translations.THIS_STORAGE_IS_ALREADY_SET_AS_DEFAULT);
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

      toast.success(
        t.translations.DEFAULT_STORAGE_LOCATION_UPDATED_SUCCESSFULLY,
      );
    } catch (error) {
      console.error("Failed to set default storage:", error);
      toast.error(
        error instanceof Error
          ? error.message
          : t.translations.FAILED_TO_UPDATE_DEFAULT_STORAGE,
      );
    } finally {
      setIsSavingStorage(false);
    }
  };

  const resetStorageForm = () => {
    setStorageFormData({ name: "", config: {}, default: false });
    setStorageType("filesystem");
    setFilesystemPath("");
    setAzureEndpoint("");
    setAzureBucketName("");
  };

  const handleCreateStorage = async () => {
    if (!organization?.organizationId || !project?.id) return;

    if (!storageFormData.name.trim()) {
      toast.error(t.translations.STORAGE_NAME_IS_REQUIRED);
      return;
    }

    // Build config based on storage type
    let config: Record<string, unknown> = {};

    if (storageType === "filesystem") {
      if (!filesystemPath.trim()) {
        toast.error(t.translations.FILESYSTEM_PATH_IS_REQUIRED);
        return;
      }
      config = {
        mountPath: filesystemPath,
      };
    } else if (storageType === "azure_blob") {
      if (!azureEndpoint.trim() || !azureBucketName.trim()) {
        toast.error(t.translations.ALL_AZURE_BLOB_FIELDS_ARE_REQUIRED);
        return;
      }
      config = {
        azureObjectConfig: {
          azureConnectionString: azureEndpoint,
          azureContainerName: azureBucketName,
        }
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

      await createProjectObjectStorage(
        organization.organizationId as number,
        project.id as number,
        dto,
        storageFormData.default,
      );

      toast.success(t.translations.STORAGE_CREATED_SUCCESSFULLY);
      setIsCreateModalOpen(false);
      resetStorageForm();
      loadStorages();
    } catch (error) {
      console.error("Failed to create storage:", error);
      console.error("Error details:", error);
      toast.error(
        error instanceof Error
          ? error.message
          : t.translations.FAILED_TO_CREATE_STORAGE,
      );
    }
  };

  const handleEditStorage = async () => {
    if (!organization?.organizationId || !project?.id || !editingStorage)
      return;
    if (editingStorage.isArchived) {
      toast.error(t.translations.ARCHIVED_STORAGE_CANNOT_BE_EDITED);
      return;
    }

    if (!storageFormData.name.trim()) {
      toast.error(t.translations.STORAGE_NAME_IS_REQUIRED);
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

      toast.success(t.translations.STORAGE_UPDATED_SUCCESSFULLY);
      setIsEditModalOpen(false);
      setEditingStorage(null);
      setStorageFormData({ name: "", config: {}, default: false });
      loadStorages();
    } catch (error) {
      console.error("Failed to update storage:", error);
      toast.error(
        error instanceof Error
          ? error.message
          : t.translations.FAILED_TO_UPDATE_STORAGE,
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

      toast.success(t.translations.STORAGE_DELETE_SUCCESSFULLY);
      setDeleteStorageId(null);
      loadStorages();
    } catch (error) {
      console.error("Failed to delete storage:", error);
      toast.error(
        error instanceof Error
          ? error.message
          : t.translations.FAILED_TO_DELETE_STORAGE,
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
        `${t.translations.STORAGE} ${archiveAction ? t.translations.ARCHIVE : t.translations.UNARCHIVE} ${t.translations.SUCCESSFULLY}`,
      );
      setArchiveStorageId(null);
      loadStorages();
    } catch (error) {
      console.error("Failed to archive/unarchive storage:", error);
      toast.error(
        error instanceof Error
          ? error.message
          : t.translations.FAILED_TO_ARCHIVE_STORAGE,
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
          <ProjectSettingsLeftColumn
            project={project}
            logoPreview={logoPreview}
            logoFile={logoFile}
            isUploading={isUploading}
            bannerText={bannerText}
            setBannerText={setBannerText}
            isSavingBanner={isSavingBanner}
            originalBannerText={originalBannerText}
            onSaveBanner={handleSaveBanner}
            onCancelBanner={handleCancelBanner}
            onLogoChange={handleLogoChange}
            onUploadLogo={handleUploadLogo}
            onCancelSelection={handleCancelSelection}
            onLogoError={() => setLogoPreview(null)}
            t={t}
          />

          {/* Storage Settings Section with Tabs */}
          <div className="self-start">
            <StorageSettingsSection
              activeTab={activeTab}
              onChangeTab={setActiveTab}
              availableStorages={availableStorages}
              selectedStorageId={selectedStorageId}
              onSelectStorage={setSelectedStorageId}
              defaultStorage={defaultStorage}
              isSavingStorage={isSavingStorage}
              onSaveDefaultStorage={handleSaveDefaultStorage}
              onCreateStorage={() => {
                resetStorageForm();
                setIsCreateModalOpen(true);
              }}
              onEditStorage={openEditModal}
              onToggleArchive={(storage) => {
                setArchiveStorageId(storage.id as number);
                setArchiveAction(!storage.isArchived);
              }}
              onDeleteStorage={(storageId) => setDeleteStorageId(storageId)}
              t={t}
            />
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

      <CreateStorageModal
        isOpen={isCreateModalOpen}
        onToggle={setIsCreateModalOpen}
        storageType={storageType}
        setStorageType={setStorageType}
        storageFormData={storageFormData}
        setStorageFormData={setStorageFormData}
        filesystemPath={filesystemPath}
        setFilesystemPath={setFilesystemPath}
        azureEndpoint={azureEndpoint}
        setAzureEndpoint={setAzureEndpoint}
        azureBucketName={azureBucketName}
        setAzureBucketName={setAzureBucketName}
        onCreate={handleCreateStorage}
        onResetForm={resetStorageForm}
      />
      <EditStorageModal
        isOpen={isEditModalOpen}
        onToggle={setIsEditModalOpen}
        storageFormData={storageFormData}
        setStorageFormData={setStorageFormData}
        onEdit={handleEditStorage}
        setEditingStorage={setEditingStorage}
      />
      <DeleteStorageModal
        isOpen={deleteStorageId !== null}
        onToggle={(value) => setDeleteStorageId(value ? deleteStorageId : null)}
        onDelete={handleDeleteStorage}
      />
      <ArchiveStorageModal
        isOpen={archiveStorageId !== null}
        onToggle={(value) =>
          setArchiveStorageId(value ? archiveStorageId : null)
        }
        archiveAction={archiveAction}
        onArchive={handleArchiveStorage}
      />
      <RemoveLogoModal onRemoveLogo={handleRemoveLogo} t={t} />
    </div>
  );
};

export default ProjectSettings;
