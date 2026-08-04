// src/app/(home)/project_management/[id]/settings/ProjectSettings.tsx
"use client";

import { useState, useEffect, useCallback } from "react";
import { v4 as uuidv4 } from 'uuid';
import toast from "react-hot-toast";
import { useProjectSession } from "@/app/contexts/ProjectSessionProvider";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import {
  archiveProject,
  fetchProjectLogo,
  removeProjectLogo,
  updateProject,
  uploadProjectLogo,
} from "@/app/lib/client_service/projects_services.client";
import {
  getAllProjectObjectStorages,
  getDefaultProjectObjectStorage,
  setDefaultProjectObjectStorage,
  createProjectObjectStorage,
  createProjectAzureContainer,
  updateProjectObjectStorage,
  deleteProjectObjectStorage,
  archiveProjectObjectStorage,
} from "@/app/lib/client_service/object_storage_services.client";
import {
  ProjectResponseDto,
  ObjectStorageResponseDto,
} from "@/app/(home)/types/responseDTOs";
import {
  CreateObjectStorageRequestDto,
  UpdateObjectStorageRequestDto,
  UpdateProjectRequestDto,
} from "@/app/(home)/types/requestDTOs";
import ProjectSettingsLeftColumn from "./components/ProjectSettingsLeftColumn";
import StorageSettingsSection from "./components/StorageSettingsSection";
import ProjectInsightModelTemplateSection from "./components/ProjectInsightModelTemplateSection";
import CreateStorageModal from "./components/CreateStorageModal";
import EditStorageModal from "./components/EditStorageModal";
import DeleteStorageModal from "./components/DeleteStorageModal";
import ArchiveStorageModal from "./components/ArchiveStorageModal";
import RemoveLogoModal from "./components/RemoveLogoModal";
import { useLanguage } from "@/app/contexts/Language";
import { ExclamationTriangleIcon } from "@heroicons/react/24/outline";
import { isInsightHidden } from "@/app/lib/feature_flags";

interface ProjectSettingsProps {
  project: ProjectResponseDto | null;
  setProject: React.Dispatch<React.SetStateAction<ProjectResponseDto | null>>;
}

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

type StorageTab = "default" | "manage";

const ProjectSettings = ({ project, setProject }: ProjectSettingsProps) => {
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
  const [isCreatingAzureContainer, setIsCreatingAzureContainer] =
    useState(false);

  // Create/Edit modal states
  const [existingContainer, setExistingContainer] = useState(false);
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [isEditModalOpen, setIsEditModalOpen] = useState(false);
  const [editingStorage, setEditingStorage] =
    useState<ObjectStorageResponseDto | null>(null);
  const [storageType, setStorageType] = useState<string>("filesystem");
  const [storageFormData, setStorageFormData] = useState<StorageFormData>({
    name: "",
    config: {},
    default: false,
    existingContainer: false
  });

  // Storage config fields based on type
  const [filesystemPath, setFilesystemPath] = useState("");
  const [azureEndpoint, setAzureEndpoint] = useState("");
  const [azureBucketName, setAzureBucketName] = useState("");

  // Delete/Archive modal states
  const [deleteStorageId, setDeleteStorageId] = useState<number | null>(null);
  const [archiveStorageId, setArchiveStorageId] = useState<number | null>(null);
  const [archiveAction, setArchiveAction] = useState<boolean>(true);

  // Load existing logo on mount
  useEffect(() => {
    const loadExistingLogo = async () => {
      if (!project?.id || !organization?.organizationId) {
        setIsCheckingLogo(false);
        setLogoPreview(null);
        return;
      }

      try {
        setIsCheckingLogo(true);

        const { blobUrl } = await fetchProjectLogo(
          organization.organizationId as number,
          project.id as number
        );

        setLogoPreview(blobUrl);

      } catch (error) {
        console.error("Error checking for existing logo:", error);
        setLogoPreview(null);
      } finally {
        setIsCheckingLogo(false);
      }
    };

    loadExistingLogo();

    // Cleanup to revoke blob URL on unmount
    return () => {
      if (logoPreview) {
        URL.revokeObjectURL(logoPreview);
      }
    };
  }, [project?.id, organization?.organizationId]);


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

      // Fetch the current default storage
      try {
        const defaultStorageData = await getDefaultProjectObjectStorage(
          organization.organizationId as number,
          project.id as number,
        );
        const projectDefaultStorage =
          storages.find(
            (storage) =>
              storage.default &&
              Number(storage.projectId) === Number(project.id),
          ) ?? null;
        const effectiveDefaultStorage =
          projectDefaultStorage ?? defaultStorageData;

        setDefaultStorage(effectiveDefaultStorage);
        setSelectedStorageId(effectiveDefaultStorage.id as number);
        setAvailableStorages(
          storages.map((storage) => ({
            ...storage,
            default:
              String(storage.id) === String(effectiveDefaultStorage.id),
          })),
        );
      } catch (error) {
        setDefaultStorage(null);
        setSelectedStorageId(null);
        setAvailableStorages(storages);
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

  const handleLogoChange = async (fileList: FileList | null) => {
    if (!fileList || fileList.length === 0) return;

    const file = fileList[0];

    const allowedTypes = [
      "image/png",
      "image/jpeg",
      "image/jpg",
      "image/webp",
      "image/gif",
      "image/svg+xml",
    ];

    if (!allowedTypes.includes(file.type)) {
      toast.error(t.translations.PLEASE_UPLOAD_VALID_IMAGE);
      return;
    }

    const maxSize = 5 * 1024 * 1024;
    if (file.size > maxSize) {
      toast.error(t.translations.FILE_SIZE_MUST_BE_5MB);
      return;
    }

    if (!organization?.organizationId || !project?.id) {
      toast.error("Organization or project is not loaded.");
      return;
    }

    try {
      // Revoke the previous object URL if it exists
      if (logoPreview) {
        URL.revokeObjectURL(logoPreview);
      }

      // Create and set new preview URL
      const previewUrl = URL.createObjectURL(file);
      setLogoPreview(previewUrl);
      setLogoFile(file);

      toast.success(t.translations.LOGO_SELECTED_SUCCESSFULLY);

    } catch (error) {
      console.error("Failed to process selected logo:", error);
      toast.error(t.translations.FAILED_TO_UPLOAD_LOGO);
    }
  };

  const handleUploadLogo = async () => {
    if (!organization?.organizationId || !project?.id || !logoFile) {
      toast.error(t.translations.NO_FILE_SELECTED);
      return;
    }

    try {
      setIsUploading(true);

      await uploadProjectLogo({
        organizationId: organization.organizationId as number,
        projectId: project.id as number,
        file: logoFile,
      });

      const { blobUrl } = await fetchProjectLogo(
        organization.organizationId as number,
        project.id as number
      );

      setLogoPreview(blobUrl);
      setLogoFile(null);
      toast.success(t.translations.LOGO_UPLOADED_SUCCESSFULLY);
    } catch (error) {
      console.error("Failed to upload logo:", error);
      toast.error(
        error instanceof Error
          ? error.message
          : t.translations.FAILED_TO_UPLOAD_LOGO
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
        projectId: project.id as number
      });

      // Revoke preview URL and reset preview and file states
      if (logoPreview) {
        URL.revokeObjectURL(logoPreview);
      }
      setLogoPreview(null);
      setLogoFile(null);

      toast.success(t.translations.LOGO_REMOVED_SUCCESSFULLY);
    } catch (error) {
      console.error("Failed to remove logo:", error);
      toast.error(t.translations.FAILED_TO_REMOVE_LOGO);
    }
  };

  const handleCancelSelection = async () => {
    if (logoPreview) {
      URL.revokeObjectURL(logoPreview);
    }

    setLogoFile(null);

    if (!project?.id || !organization?.organizationId) {
      setLogoPreview(null);
      return;
    }

    try {
      const { blobUrl } = await fetchProjectLogo(
        organization.organizationId as number,
        project.id as number
      )

      setLogoPreview(blobUrl);
    } catch (error) {
      console.error("Failed to restore previous logo:", error);
      setLogoPreview(null);
    }
  };


  const handleSaveDefaultStorage = async () => {
    if (!organization?.organizationId || !project?.id || !selectedStorageId) {
      toast.error(t.translations.PLEASE_SELECT_A_STORAGE_LOCATION);
      return;
    }

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
        setDefaultStorage({ ...updatedDefault, default: true });
        setAvailableStorages((currentStorages) =>
          currentStorages.map((storage) => ({
            ...storage,
            default: String(storage.id) === String(selectedStorageId),
          })),
        );
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
    setStorageFormData({ name: "", config: {}, default: false, existingContainer: false });
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
    } else if (storageType === "azure_object") {
      if (!azureEndpoint.trim() || !azureBucketName.trim()) {
        toast.error(t.translations.ALL_AZURE_BLOB_FIELDS_ARE_REQUIRED);
        return;
      }
      const containerName = storageFormData.existingContainer
        ? azureBucketName
        : uniqueContainerNameFromString(azureBucketName);
      config = {
        azureObjectConfig: {
          azureConnectionString: azureEndpoint,
          azureContainerName: containerName,
          existingContainer: storageFormData.existingContainer || false
        },
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
        default: storageFormData.default,
      };

      const createdStorage = await createProjectObjectStorage(
        organization.organizationId as number,
        project.id as number,
        dto,
        storageFormData.default,
      );

      const projectRequestDto: UpdateProjectRequestDto = {
        organizationId: organization.organizationId as number,
        filePath: storageFormData.config.AzureObjectConfig?.AzureFilePath
      };

      await updateProject(
        organization.organizationId as number,
        project.id as number,
        projectRequestDto
      )

      setExistingContainer(storageFormData.existingContainer as boolean)

      const storageForList = {
        ...createdStorage,
        default: storageFormData.default || createdStorage.default,
      };

      setAvailableStorages((currentStorages) => {
        const existingStorage = currentStorages.some(
          (storage) => String(storage.id) === String(storageForList.id),
        );
        const nextStorages = existingStorage
          ? currentStorages.map((storage) =>
            String(storage.id) === String(storageForList.id)
              ? storageForList
              : storage,
          )
          : [...currentStorages, storageForList];

        if (!storageForList.default) {
          return nextStorages;
        }

        return nextStorages.map((storage) => ({
          ...storage,
          default: String(storage.id) === String(storageForList.id),
        }));
      });

      if (storageForList.default) {
        setDefaultStorage(storageForList);
        setSelectedStorageId(storageForList.id as number);
      }

      toast.success(t.translations.STORAGE_CREATED_SUCCESSFULLY);
      setIsCreateModalOpen(false);
      resetStorageForm();
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

  const handleCreateAzureContainer = async () => {
    if (!organization?.organizationId || !project?.id) return;

    try {
      setIsCreatingAzureContainer(true);

      var containerName = uniqueContainerNameFromString(azureBucketName) ?? null

      const createdStorage = await createProjectAzureContainer(
        organization.organizationId as number,
        project.id as number,
        storageFormData.existingContainer as boolean,
        "azure_object",
        containerName,
      );

      const projectRequestDto: UpdateProjectRequestDto = {
        organizationId: organization.organizationId as number,
        filePath: storageFormData.config.AzureObjectConfig?.AzureFilePath
      };

      await updateProject(
        organization.organizationId as number,
        project.id as number,
        projectRequestDto
      )

      setExistingContainer(storageFormData.existingContainer as boolean)

      const shouldSetAsDefault = storageFormData.default;
      let storageForList = createdStorage;

      if (shouldSetAsDefault) {
        await setDefaultProjectObjectStorage(
          organization.organizationId as number,
          project.id as number,
          createdStorage.id as number,
        );
        storageForList = { ...createdStorage, default: true };
      }

      setAvailableStorages((currentStorages) => {
        const existingStorage = currentStorages.some(
          (storage) => String(storage.id) === String(storageForList.id),
        );
        const nextStorages = existingStorage
          ? currentStorages.map((storage) =>
            String(storage.id) === String(storageForList.id)
              ? storageForList
              : storage,
          )
          : [...currentStorages, storageForList];

        if (!storageForList.default) {
          return nextStorages;
        }

        return nextStorages.map((storage) => ({
          ...storage,
          default: String(storage.id) === String(storageForList.id),
        }));
      });

      if (storageForList.default) {
        setDefaultStorage(storageForList);
        setSelectedStorageId(storageForList.id as number);
      }

      toast.success(t.translations.STORAGE_CREATED_SUCCESSFULLY);
      setIsCreateModalOpen(false);
      resetStorageForm();
    } catch (error) {
      console.error("Failed to create Azure container from project name:", error);
      toast.error(
        error instanceof Error
          ? error.message
          : t.translations.FAILED_TO_CREATE_STORAGE,
      );
    } finally {
      setIsCreatingAzureContainer(false);
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
      const objectStorageDto: UpdateObjectStorageRequestDto = {
        name: storageFormData.name,
        default: storageFormData.default,
        existingContainer: storageFormData.existingContainer
      };

      const projectRequestDto: UpdateProjectRequestDto = {
        organizationId: organization.organizationId as number,
        filePath: storageFormData.config.AzureObjectConfig?.AzureFilePath
      };

      if (editingStorage.projectId != null) {
        await updateProjectObjectStorage(
          organization.organizationId as number,
          project.id as number,
          editingStorage.id as number,
          objectStorageDto,
        );
      }

      await updateProject(
        organization.organizationId as number,
        project.id as number,
        projectRequestDto
      )

      setExistingContainer(storageFormData.existingContainer as boolean)

      toast.success(t.translations.STORAGE_UPDATED_SUCCESSFULLY);
      setIsEditModalOpen(false);
      setEditingStorage(null);
      setStorageFormData({ name: "", config: {}, default: false, existingContainer: false });
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
      existingContainer: existingContainer
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


  function uniqueContainerNameFromString(inputString: string): string {
    const maxContainerNameLength = 63;
    const guidLength = 36;
    const separatorLength = 1;
    const maxInputStringLength = maxContainerNameLength - guidLength - separatorLength;

    let truncatedInputString = inputString.length > maxInputStringLength
      ? inputString.substring(0, maxInputStringLength)
      : inputString;

    truncatedInputString = truncatedInputString
      .toLowerCase()
      .split('')
      .filter(c => /[a-z0-9-]/.test(c))
      .join('');

    const guid = uuidv4();

    return `${truncatedInputString}-${guid}`.toLowerCase();
  }

  return (
    <div className="p-6">
      <div className="mx-auto space-y-6">
        {/* Page Header */}
        <div className="border-b border-base-300/50 pb-4">
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
            organization={organization}
            project={project}
            setProject={setProject}
            logoPreview={logoPreview}
            logoFile={logoFile}
            isUploading={isUploading}
            onLogoChange={handleLogoChange}
            onUploadLogo={handleUploadLogo}
            onCancelSelection={handleCancelSelection}
            onLogoError={() => setLogoPreview(null)}
            onArchiveProject={async () => {
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
            t={t}
          />

          {/* Storage Settings Section with Tabs */}
          <div className="self-start space-y-6">
            <StorageSettingsSection
              activeTab={activeTab}
              onChangeTab={setActiveTab}
              projectId={project.id}
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
            {!isInsightHidden() && (
              <ProjectInsightModelTemplateSection
                organizationId={
                  organization?.organizationId as number | undefined
                }
                projectId={project.id as number | undefined}
              />
            )}
          </div>
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
        onCreateFromProjectName={handleCreateAzureContainer}
        isCreatingFromProjectName={isCreatingAzureContainer}
        onResetForm={resetStorageForm}
      />
      <EditStorageModal
        isOpen={isEditModalOpen}
        onToggle={setIsEditModalOpen}
        storageFormData={storageFormData}
        setStorageFormData={setStorageFormData}
        onEdit={handleEditStorage}
        editingStorage={editingStorage}
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