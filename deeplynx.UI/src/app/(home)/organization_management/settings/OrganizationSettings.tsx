// src/app/(home)/organization_management/settings/OrganizationSettings.tsx
"use client";

import { useState, useEffect, useCallback } from "react";
import toast from "react-hot-toast";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import {
  InformationCircleIcon,
  ExclamationTriangleIcon,
} from "@heroicons/react/24/outline";
import {
  uploadOrganizationLogo,
  removeOrganizationLogo,
  updateOrganization,
  fetchOrganizationLogo,
  getOrganization,
} from "@/app/lib/client_service/organization_services.client";
import StorageSettingsSection from "@/app/(home)/project_management/[id]/settings/components/StorageSettingsSection";
import CreateStorageModal from "@/app/(home)/organization_management/settings/components/CreateStorageModal";
import EditStorageModal from "@/app/(home)/organization_management/settings/components/EditStorageModal";
import DeleteStorageModal from "@/app/(home)/project_management/[id]/settings/components/DeleteStorageModal";
import ArchiveStorageModal from "@/app/(home)/project_management/[id]/settings/components/ArchiveStorageModal";
import { useLanguage } from "@/app/contexts/Language";
import Image from "next/image";
import OrganizationInsightModelTemplateSection from "./components/OrganizationInsightModelTemplateSection";
import {
  ORGANIZATION_THEMES,
  resolveOrganizationTheme,
} from "@/app/lib/themes/organizationTheme";
import { applyOrganizationTheme } from "@/app/lib/themes/themeMode";
import { isInsightHidden } from "@/app/lib/feature_flags";
import { archiveOrganizationObjectStorage, createOrganizationObjectStorage, deleteOrganizationObjectStorage, getAllOrganizationObjectStorages, getDefaultOrganizationObjectStorage, setDefaultOrganizationObjectStorage, updateOrganizationObjectStorage } from "@/app/lib/client_service/object_storage_services.client";
import { ObjectStorageResponseDto } from "../../types/responseDTOs";
import { CreateObjectStorageRequestDto, UpdateObjectStorageRequestDto, UpdateOrganizationRequestDto } from "../../types/requestDTOs";


type StorageTab = "default" | "manage";

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
  createContainerPerProject: boolean;
  existingContainer?: boolean;
}

const OrganizationSettings = () => {
  const { organization, setOrganization } = useOrganizationSession();
  const { t } = useLanguage();

  const themeLabels: Record<string, string> = {
    default: t.translations.ORGANIZATION_THEME_DEFAULT,
    nric: t.translations.ORGANIZATION_THEME_NRIC,
    nord: t.translations.ORGANIZATION_THEME_NORD,
    emerald: t.translations.ORGANIZATION_THEME_EMERALD,
  };

  // Logo states
  const [logoPreview, setLogoPreview] = useState<string | null>(null);
  const [logoFile, setLogoFile] = useState<File | null>(null);
  const [isUploading, setIsUploading] = useState(false);
  const [isCheckingLogo, setIsCheckingLogo] = useState(true);

  // Banner states
  const [bannerText, setBannerText] = useState<string>("");
  const [originalBannerText, setOriginalBannerText] = useState<string>("");
  const [isSavingBanner, setIsSavingBanner] = useState(false);

  // Theme states
  const [selectedThemeName, setSelectedThemeName] = useState("default");
  const [originalThemeName, setOriginalThemeName] = useState("default");
  const [isSavingTheme, setIsSavingTheme] = useState(false);
  const [themeToast, setThemeToast] = useState<{
    message: string;
    type: "success" | "error" | "info";
  } | null>(null);

  // Storage states
  // Storage config fields based on type
  const [createContainerPerProject, setCreateContainerPerProject] = useState(false);
  const [existingContainer, setExistingContainer] = useState(false);

  const [activeStorageTab, setActiveStorageTab] =
    useState<StorageTab>("default");
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

  // Create/Edit storage modal states
  const [isCreateStorageModalOpen, setIsCreateStorageModalOpen] =
    useState(false);
  const [isEditStorageModalOpen, setIsEditStorageModalOpen] = useState(false);
  const [editingStorage, setEditingStorage] =
    useState<ObjectStorageResponseDto | null>(null);
  const [storageType, setStorageType] = useState<string>("filesystem");
  const [storageFormData, setStorageFormData] = useState<StorageFormData>({
    name: "",
    config: {},
    default: false,
    createContainerPerProject: false,
    existingContainer: false
  });

  // Storage config fields based on type
  const [filesystemPath, setFilesystemPath] = useState("");
  const [azureEndpoint, setAzureEndpoint] = useState("");
  const [azureBucketName, setAzureBucketName] = useState("");

  // Delete/Archive storage modal states
  const [deleteStorageId, setDeleteStorageId] = useState<number | null>(null);
  const [archiveStorageId, setArchiveStorageId] = useState<number | null>(
    null,
  );
  const [archiveAction, setArchiveAction] = useState<boolean>(true);

  // File Transfer states
  const [disableFileTransfer, setDisableFileTransfer] = useState(false);
  const [originalDisableFileTransfer, setOriginalDisableFileTransfer] =
    useState(false);
  const [isSavingFileTransfer, setIsSavingFileTransfer] = useState(false);

  // Load existing logo on mount
  useEffect(() => {
    const loadExistingLogo = async () => {
      if (!organization?.organizationId) {
        setIsCheckingLogo(false);
        setLogoPreview(null);
        return;
      }

      try {
        setIsCheckingLogo(true);
        const { blobUrl } = await fetchOrganizationLogo(
          organization.organizationId as number,
        );

        setLogoPreview(blobUrl);
      } catch (error) {
        console.error("Error checking for existing logo:", error);
      } finally {
        setIsCheckingLogo(false);
      }
    };

    loadExistingLogo();

    return () => {
      if (logoPreview) {
        URL.revokeObjectURL(logoPreview);
      }
    };
  }, [organization?.organizationId]);

  const handleLogoChange = (fileList: FileList | null) => {
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

    // Validate file size (max 5MB)
    const maxSize = 5 * 1024 * 1024; // 5MB in bytes
    if (file.size > maxSize) {
      toast.error(t.translations.FILE_SIZE_MUST_BE_5MB);
      return;
    }

    if (!organization?.organizationId) {
      toast.error("Organization is not loaded.");
      return;
    }

    try {
      // Revoke the previous object URL if it exists
      if (logoPreview) {
        URL.revokeObjectURL(logoPreview);
      }

      // Create and set new preview URL
      const previewUrl = URL.createObjectURL(file);

      setOrganization({
        ...organization,
        logoUrl: previewUrl!,
      });

      setLogoPreview(previewUrl);
      setLogoFile(file);

      toast.success(t.translations.LOGO_SELECTED_SUCCESSFULLY);

    } catch (error) {
      console.error("Failed to process selected logo:", error);
      toast.error(t.translations.FAILED_TO_UPLOAD_LOGO);
    }
  };

  const handleUploadLogo = async () => {
    if (!organization?.organizationId || !logoFile) {
      toast.error(t.translations.NO_FILE_SELECTED);
      return;
    }

    try {
      setIsUploading(true);

      await uploadOrganizationLogo({
        organizationId: organization.organizationId as number,
        file: logoFile,
      });

      const { blobUrl } = await fetchOrganizationLogo(
        organization.organizationId as number,
      );

      setOrganization({
        ...organization,
        logoUrl: blobUrl!,
      });

      setLogoPreview(blobUrl);
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
    if (!organization?.organizationId) return;

    try {
      await removeOrganizationLogo({
        organizationId: organization.organizationId as number
      });

      setOrganization({
        ...organization,
        logoUrl: undefined,
      });

      if (logoPreview) {
        URL.revokeObjectURL(logoPreview);
      }
      setLogoFile(null);
      setLogoPreview(null);

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

    if (!organization?.organizationId) {
      setLogoPreview(null);
      return;
    }

    try {
      const { blobUrl } = await fetchOrganizationLogo(
        organization.organizationId as number,
      )

      setLogoPreview(blobUrl);
    } catch (error) {
      console.error("Failed to restore previous logo:", error);
      setLogoPreview(null);
    }
  };

  useEffect(() => {
    async function loadOrgSettings() {
      if (!organization?.organizationId) return;

      try {
        const orgData = await getOrganization(organization.organizationId as number);
        const objectStorage = await getDefaultOrganizationObjectStorage(organization.organizationId as number);
        setDefaultStorage(objectStorage);
        setCreateContainerPerProject(orgData.createContainerPerProject ?? false);
      } catch (error) {
        console.error("Failed to load organization settings", error);
      }
    }

    loadOrgSettings();
  }, [organization?.organizationId]);

  // Load available storages and default storage for the organization
  const loadStorages = useCallback(async () => {
    if (!organization?.organizationId) {
      setIsLoadingStorages(false);
      return;
    }

    try {
      setIsLoadingStorages(true);

      // Fetch all available storages for the organization
      const storages = await getAllOrganizationObjectStorages(
        organization.organizationId as number,
        false, // Don't hide archived storages
      );

      // Fetch the current default storage
      try {
        const defaultStorageData = await getDefaultOrganizationObjectStorage(
          organization.organizationId as number,
        );
        const orgDefaultStorage =
          storages.find((storage) => storage.default) ?? null;
        const effectiveDefaultStorage =
          orgDefaultStorage ?? defaultStorageData;

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
      console.error("Error loading organization storages:", error);
      toast.error(t.translations.FAILED_TO_LOAD_STORAGE_CONFIGURATIONS);
    } finally {
      setIsLoadingStorages(false);
    }
  }, [
    organization?.organizationId,
    t.translations.FAILED_TO_LOAD_STORAGE_CONFIGURATIONS,
  ]);

  useEffect(() => {
    loadStorages();
  }, [loadStorages]);

  const handleSaveDefaultStorage = async () => {
    if (!organization?.organizationId || !selectedStorageId) {
      toast.error(t.translations.PLEASE_SELECT_A_STORAGE_LOCATION);
      return;
    }

    if (defaultStorage?.id === selectedStorageId) {
      toast.error(t.translations.THIS_STORAGE_IS_ALREADY_SET_AS_DEFAULT);
      return;
    }

    try {
      setIsSavingStorage(true);

      await setDefaultOrganizationObjectStorage(
        organization.organizationId as number,
        selectedStorageId,
      );

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
      console.error("Failed to set default organization storage:", error);
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
    setStorageFormData({ name: "", config: {}, default: false, createContainerPerProject: false, existingContainer: false });
    setStorageType("filesystem");
    setFilesystemPath("");
    setAzureEndpoint("");
    setAzureBucketName("");
  };

  const handleCreateStorage = async () => {
    if (!organization?.organizationId) return;

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

      const updateOrganizationDto: UpdateOrganizationRequestDto = {
        createContainerPerProject: storageFormData.createContainerPerProject,
      };

      const createdStorage = await createOrganizationObjectStorage(
        organization.organizationId as number,
        dto,
        storageFormData.default,
      );

      await updateOrganization(
        organization.organizationId as number,
        updateOrganizationDto
      );

      setCreateContainerPerProject(storageFormData.createContainerPerProject);
      setExistingContainer(storageFormData.existingContainer as boolean);

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
      setIsCreateStorageModalOpen(false);
      resetStorageForm();
    } catch (error) {
      console.error("Failed to create organization storage:", error);
      toast.error(
        error instanceof Error
          ? error.message
          : t.translations.FAILED_TO_CREATE_STORAGE,
      );
    }
  };

  const handleEditStorage = async () => {
    if (!organization?.organizationId || !editingStorage) return;
    if (editingStorage.isArchived) {
      toast.error(t.translations.ARCHIVED_STORAGE_CANNOT_BE_EDITED);
      return;
    }

    if (!storageFormData.name.trim()) {
      toast.error(t.translations.STORAGE_NAME_IS_REQUIRED);
      return;
    }

    try {
      const updateObjectStorageDto: UpdateObjectStorageRequestDto = {
        name: storageFormData.name,
        default: storageFormData.default,
        existingContainer: storageFormData.existingContainer,
      };

      const updateOrganizationDto: UpdateOrganizationRequestDto = {
        createContainerPerProject: storageFormData.createContainerPerProject,
      };

      await updateOrganizationObjectStorage(
        organization.organizationId as number,
        editingStorage.id as number,
        updateObjectStorageDto,
      );

      await updateOrganization(
        organization.organizationId as number,
        updateOrganizationDto
      );
      setCreateContainerPerProject(storageFormData.createContainerPerProject);
      setExistingContainer(storageFormData.existingContainer as boolean);

      toast.success(t.translations.STORAGE_UPDATED_SUCCESSFULLY);
      setIsEditStorageModalOpen(false);
      setEditingStorage(null);
      setStorageFormData({ name: "", config: {}, default: false, createContainerPerProject: false, existingContainer: false });
      loadStorages();
    } catch (error) {
      console.error("Failed to update organization storage:", error);
      toast.error(
        error instanceof Error
          ? error.message
          : t.translations.FAILED_TO_UPDATE_STORAGE,
      );
    }
  };

  const handleDeleteStorage = async () => {
    if (!organization?.organizationId || !deleteStorageId) return;

    try {
      await deleteOrganizationObjectStorage(
        organization.organizationId as number,
        deleteStorageId,
      );

      toast.success(t.translations.STORAGE_DELETE_SUCCESSFULLY);
      setDeleteStorageId(null);
      loadStorages();
    } catch (error) {
      console.error("Failed to delete organization storage:", error);
      toast.error(
        error instanceof Error
          ? error.message
          : t.translations.FAILED_TO_DELETE_STORAGE,
      );
    }
  };

  const handleArchiveStorage = async () => {
    if (!organization?.organizationId || !archiveStorageId) return;

    try {
      await archiveOrganizationObjectStorage(
        organization.organizationId as number,
        archiveStorageId,
        archiveAction,
      );

      toast.success(
        `${t.translations.STORAGE} ${archiveAction ? t.translations.ARCHIVE : t.translations.UNARCHIVE} ${t.translations.SUCCESSFULLY}`,
      );
      setArchiveStorageId(null);
      loadStorages();
    } catch (error) {
      console.error("Failed to archive/unarchive organization storage:", error);
      toast.error(
        error instanceof Error
          ? error.message
          : t.translations.FAILED_TO_ARCHIVE_STORAGE,
      );
    }
  };

  const openEditStorageModal = (storage: ObjectStorageResponseDto) => {
    if (storage.isArchived) {
      return;
    }
    setEditingStorage(storage);
    setStorageFormData({
      name: storage.name,
      config: {},
      default: storage.default,
      createContainerPerProject: createContainerPerProject,
      existingContainer: existingContainer,
    });
    setIsEditStorageModalOpen(true);
  };

  // Syncs Theme from session
  useEffect(() => {
    const themeName = resolveOrganizationTheme(organization?.themeName);
    setSelectedThemeName(themeName);
    setOriginalThemeName(themeName);
  }, [organization?.themeName]);

  // Theme Handler
  const handleSaveTheme = async () => {
    if (!organization?.organizationId) {
      setThemeToast({ message: t.translations.NO_ORG_SELECTED, type: "error" });
      return;
    }

    if (selectedThemeName === originalThemeName) {
      setThemeToast({
        message: t.translations.NO_CHANGES_TO_SAVE,
        type: "info",
      });
      return;
    }

    try {
      setIsSavingTheme(true);

      const updateOrg = await updateOrganization(
        organization?.organizationId as number,
        { theme: selectedThemeName },
      );

      const newThemeName = resolveOrganizationTheme(updateOrg.theme);

      setOriginalThemeName(newThemeName);
      applyOrganizationTheme(newThemeName);

      setOrganization({
        ...organization,
        themeName: newThemeName,
      });
      setThemeToast({
        message: t.translations.THEME_UPDATE_SUCCESS,
        type: "success",
      });
    } catch (error) {
      console.error("Failed to update Organization theme: ", error);
      setThemeToast({
        message: t.translations.FAILED_TO_UPDATE_THEME,
        type: "error",
      });
    } finally {
      setIsSavingTheme(false);
    }
  };

  useEffect(() => {
    if (!themeToast) return;

    const timeout = window.setTimeout(() => {
      setThemeToast(null);
    }, 3000);

    return () => window.clearTimeout(timeout);
  }, [themeToast]);

  useEffect(() => {
    if (organization?.banner !== undefined) {
      const banner = organization.banner || "";
      setBannerText(banner);
      setOriginalBannerText(banner);
    }
  }, [organization?.banner]);

  useEffect(() => {
    const disabled = !!organization?.disableFileTransfer;
    setDisableFileTransfer(disabled);
    setOriginalDisableFileTransfer(disabled);
  }, [organization?.disableFileTransfer]);

  const handleSaveFileTransfer = async () => {
    if (!organization?.organizationId) {
      toast.error(t.translations.NO_ORG_SELECTED);
      return;
    }

    try {
      setIsSavingFileTransfer(true);

      await updateOrganization(organization.organizationId as number, {
        disableFileTransfer,
      });

      setOriginalDisableFileTransfer(disableFileTransfer);
      setOrganization({
        ...organization,
        disableFileTransfer,
      });

      toast.success(
        disableFileTransfer
          ? t.translations.FILE_TRANSFER_DISABLED_SUCCESSFULLY ||
          "File transfer disabled for this organization"
          : t.translations.FILE_TRANSFER_ENABLED_SUCCESSFULLY ||
          "File transfer enabled for this organization",
      );
    } catch (error) {
      console.error("Failed to update file transfer setting: ", error);
      toast.error(
        error instanceof Error
          ? error.message
          : t.translations.FAILED_TO_UPDATE_FILE_TRANSFER_SETTING,
      );
    } finally {
      setIsSavingFileTransfer(false);
    }
  };

  const handleCancelFileTransfer = () => {
    setDisableFileTransfer(originalDisableFileTransfer);
    toast.custom(
      <div className="text-info">
        <ExclamationTriangleIcon className="size-4" />
        {t.translations.CHANGES_DISCARDED}
      </div>,
    );
  };


  const handleSaveBanner = async () => {
    if (!organization?.organizationId) {
      toast.error(t.translations.NO_ORG_SELECTED);
      return;
    }

    if (bannerText === originalBannerText) {
      toast.custom(
        <div className="text-info">
          <ExclamationTriangleIcon className="size-4" />
          {t.translations.NO_CHANGES_TO_SAVE}
        </div>,
      );
    }

    if (bannerText.length > 50) {
      toast.error(t.translations.BANNER_TEXT_MUST_BE_50_CHARACTERS_OR_LESS);
      return;
    }

    try {
      setIsSavingBanner(true);

      await updateOrganization(organization.organizationId as number, {
        banner: bannerText.trim() || null,
      });

      setOriginalBannerText(bannerText);

      toast.success(t.translations.BANNER_UPDATED_SUCCESSFULLY);
    } catch (error) {
      console.error("Failed to update banner: ", error);
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

  if (isCheckingLogo) {
    return (
      <div className="p-6 flex items-center justify-center min-h-[400px]">
        <span className="loading loading-spinner loading-lg"></span>
      </div>
    );
  }

  return (
    <div className="p-6">
      <div className="mx-auto">
        <div className="mb-6">
          <h2 className="text-2xl font-bold text-base-content">
            {t.translations.ORGANIZATION_SETTINGS}
          </h2>
          <p className="text-base-content/70 text-sm mt-1">
            {t.translations.ORGANIZATION_SETTINGS_DESCRIPTION}
          </p>
        </div>

        {/* Two-column layout */}
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
          {/* LEFT COLUMN */}
          <div className="flex flex-col gap-6">
            {/* ============================================================ */}
            {/*                          LOGO CARD                           */}
            {/* ============================================================ */}
            <div className="card bg-base-100 border border-primary/40 shadow-sm">
              <div className="card-body">
                <h3 className="card-title text-lg mb-4">
                  {t.translations.BRANDING_AND_BANNER}
                </h3>

                {/* Logo Section - ACTIVE */}
                <div className="flex items-start gap-4 mb-6">
                  <div className="avatar">
                    <div className="w-24 h-24 rounded-xl bg-base-200 flex items-center justify-center overflow-hidden border-2 border-base-300 relative">
                      {logoPreview ? (
                        <Image
                          src={logoPreview}
                          alt={t.translations.ORGANIZATION_LOGO}
                          fill
                          sizes="96px"
                          className="object-contain p-2"
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

                  <div className="flex flex-col gap-2 flex-1">
                    <span className="font-semibold text-base">
                      {organization?.organizationName ||
                        t.translations.ORGANIZATION}
                    </span>

                    <div className="flex flex-wrap gap-2">
                      <label className="btn btn-sm btn-primary">
                        {logoFile
                          ? t.translations.CHANGE_LOGO
                          : t.translations.SELECT_LOGO}
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
                          htmlFor="remove_logo"
                          className="btn btn-sm btn-error btn-outline"
                        >
                          {t.translations.REMOVE_LOGO}
                        </label>
                      )}
                    </div>

                    <p className="text-xs text-base-content/60">
                      {t.translations.APPEAR_ON_TOP_RIGHT_NEXT_TO_ORG_NAME}
                    </p>
                  </div>
                </div>

                {/* Banner Text Section */}
                <div className="divider"></div>
                <div className="relative">
                  <div className="form-control">
                    <label className="label mr-6">
                      <span className="label-text font-semibold flex items-center gap-2">
                        {t.translations.ORGANIZATION_WARNING_BANNER}
                      </span>
                    </label>
                    <textarea
                      className="textarea textarea-bordered min-h-20"
                      placeholder={t.translations.BANNER_EXAMPLE_CUI}
                      value={bannerText}
                      onChange={(e) => setBannerText(e.target.value)}
                      disabled={isSavingBanner}
                      maxLength={50}
                    />
                    <label className="label">
                      <span className="label-text-alt text-base-content/60">
                        {
                          t.translations
                            .DISPLAY_BENEATH_THE_TOP_HEADER_FOR_ALL_PAGES_IN_ORG
                        }
                      </span>
                      <span
                        className={`label-text-alt mt-6 ${bannerText.length > 50 ? "text-error" : "text-base-content/40"}`}
                      >
                        {bannerText.length} / 50
                      </span>
                    </label>
                  </div>

                  {/* Action Buttons */}
                  <div className="flex gap-2 mt-4">
                    <button
                      type="button"
                      className="btn btn-primary btn-sm"
                      onClick={handleSaveBanner}
                      disabled={
                        isSavingBanner ||
                        bannerText === originalBannerText ||
                        bannerText.length > 50
                      }
                    >
                      {isSavingBanner && (
                        <span className="loading loading-spinner loading-xs" />
                      )}
                      {t.translations.SAVE}
                    </button>

                    {bannerText !== originalBannerText && (
                      <button
                        type="button"
                        className="btn btn-ghost btn-sm"
                        onClick={handleCancelBanner}
                        disabled={isSavingBanner}
                      >
                        {t.translations.CANCEL}
                      </button>
                    )}
                  </div>
                </div>
                <div className="divider" />

                <div className="space-y-4">
                  <div>
                    <h3 className="card-title text-lg mb-4">
                      {t.translations.ORGANIZATION_THEME}
                    </h3>
                    <p className="text-sm text-base-content/60 mt-1">
                      {t.translations.ORGANIZATION_THEME_DESCRIPTION}
                    </p>
                  </div>
                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                    {ORGANIZATION_THEMES.map((theme) => {
                      const selected = selectedThemeName === theme.id;

                      return (
                        <button
                          key={theme.id}
                          type="button"
                          className={`rounded-lg border p-4 text-left transition ${selected
                            ? "border-primary bg-primary/10"
                            : "border-base-300 bg-base-100 hover:border-primary/40 hover:bg-base-200/40"
                            }`}
                          onClick={() => setSelectedThemeName(theme.id)}
                          disabled={isSavingTheme}
                        >
                          <div className="flex items-center justify-between gap-3">
                            <span className="font-medium text-base-content">
                              {themeLabels[theme.id] ?? theme.label}
                            </span>

                            <div className="flex gap-1">
                              {theme.swatches.map((color) => (
                                <span
                                  className="h-5 w-5 rounded-full border border-base-300"
                                  key={color}
                                  style={{ backgroundColor: color }}
                                />
                              ))}
                            </div>
                          </div>
                        </button>
                      );
                    })}
                  </div>

                  <div className="flex gap-2">
                    <button
                      className="btn btn-primary btn-sm"
                      type="button"
                      onClick={handleSaveTheme}
                      disabled={
                        isSavingTheme || selectedThemeName === originalThemeName
                      }
                    >
                      {isSavingTheme && (
                        <span className="loading loading-spinner loading-xs" />
                      )}
                      {t.translations.SAVE}
                    </button>

                    {selectedThemeName !== originalThemeName && (
                      <button
                        className="btn btn-ghost btn-sm"
                        type="button"
                        onClick={() => setSelectedThemeName(originalThemeName)}
                        disabled={isSavingTheme}
                      >
                        {t.translations.CANCEL}
                      </button>
                    )}
                  </div>
                </div>

                <div className="divider" />

                <div className="space-y-4">
                  <div>
                    <h3 className="card-title text-lg mb-2">
                      {t.translations.FILE_TRANSFER}
                    </h3>
                    <p className="text-sm text-base-content/60 mt-1">
                      {t.translations.FILE_TRANSFER_DESCRIPTION}
                    </p>
                  </div>

                  <div className="form-control">
                    <label className="cursor-pointer label flex items-center justify-start w-fit gap-3">
                      <input
                        type="checkbox"
                        className="checkbox checkbox-primary"
                        checked={disableFileTransfer}
                        disabled={isSavingFileTransfer}
                        onChange={(e) =>
                          setDisableFileTransfer(e.target.checked)
                        }
                      />
                      <span className="label-text font-semibold">
                        {t.translations.DISABLE_FILE_TRANSFER}
                      </span>
                    </label>
                    <span className="text-xs text-base-content/60 mt-1">
                      {t.translations.DISABLE_FILE_TRANSFER_HELPER}
                    </span>
                  </div>

                  <div className="flex gap-2">
                    <button
                      type="button"
                      className="btn btn-primary btn-sm"
                      onClick={handleSaveFileTransfer}
                      disabled={
                        isSavingFileTransfer ||
                        disableFileTransfer === originalDisableFileTransfer
                      }
                    >
                      {isSavingFileTransfer && (
                        <span className="loading loading-spinner loading-xs" />
                      )}
                      {t.translations.SAVE}
                    </button>

                    {disableFileTransfer !== originalDisableFileTransfer && (
                      <button
                        type="button"
                        className="btn btn-ghost btn-sm"
                        onClick={handleCancelFileTransfer}
                        disabled={isSavingFileTransfer}
                      >
                        {t.translations.CANCEL}
                      </button>
                    )}
                  </div>
                </div>
              </div>
            </div>
          </div>

          {/* RIGHT COLUMN */}
          <div className="flex flex-col gap-6">
            {/* ============================================================ */}
            {/*                     STORAGE SETTINGS                        */}
            {/* ============================================================ */}
            {isLoadingStorages ? (
              <div className="card bg-base-100 border border-base-300/50 shadow-sm">
                <div className="card-body items-center justify-center py-10">
                  <span className="loading loading-spinner loading-md" />
                </div>
              </div>
            ) : (
              <StorageSettingsSection
                scope="organization"
                organizationId={organization?.organizationId ?? undefined}
                activeTab={activeStorageTab}
                onChangeTab={setActiveStorageTab}
                availableStorages={availableStorages}
                selectedStorageId={selectedStorageId}
                onSelectStorage={setSelectedStorageId}
                defaultStorage={defaultStorage}
                isSavingStorage={isSavingStorage}
                onSaveDefaultStorage={handleSaveDefaultStorage}
                onCreateStorage={() => {
                  resetStorageForm();
                  setIsCreateStorageModalOpen(true);
                }}
                onEditStorage={openEditStorageModal}
                onToggleArchive={(storage) => {
                  setArchiveStorageId(storage.id as number);
                  setArchiveAction(!storage.isArchived);
                }}
                onDeleteStorage={(storageId) => setDeleteStorageId(storageId)}
                t={t}
              />
            )}

            {!isInsightHidden() && (
              <OrganizationInsightModelTemplateSection
                organizationId={
                  organization?.organizationId as number | undefined
                }
              />
            )}
          </div>

          {!isInsightHidden() && (
            <OrganizationInsightModelTemplateSection
              organizationId={
                organization?.organizationId as number | undefined
              }
            />
          )}
        </div>
      </div>

      {/* Info Banner at Bottom */}
      <div className="alert alert-info mt-6">
        <InformationCircleIcon className="h-6 w-6" />
        <div>
          <div className="font-bold">
            {t.translations.ADDITIONAL_SETTINGS_COMING_SOON}
          </div>
          <div className="text-sm">
            {
              t.translations
                .STORAGE_CONFIGURATION_AND_ADDITIONAL_ORG_MANAGEMENT_IN_DEVELOPMENT
            }
          </div>
        </div>
      </div>

      {/* Remove Logo Modal */}
      <input type="checkbox" id="remove_logo" className="modal-toggle" />
      <div className="modal" role="dialog">
        <div className="modal-box">
          <h3 className="text-lg font-bold">{t.translations.REMOVE_LOGO}</h3>
          <p className="py-4">
            {t.translations.ARE_YOU_SURE_TO_REMOVE_LOGO_FROM_ORG}
          </p>
          <div className="modal-action">
            <label htmlFor="remove_logo" className="btn">
              {t.translations.CANCEL}
            </label>
            <label
              htmlFor="remove_logo"
              className="btn btn-outline btn-secondary"
              onClick={handleRemoveLogo}
            >
              {t.translations.REMOVE}
            </label>
          </div>
        </div>
      </div>
      {
        themeToast && (
          <div className="toast toast-bottom toast-end">
            <div className={`alert alert-${themeToast.type}`}>
              {themeToast.message}
            </div>
          </div>
        )
      }

      <CreateStorageModal
        isOpen={isCreateStorageModalOpen}
        onToggle={setIsCreateStorageModalOpen}
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
        isOpen={isEditStorageModalOpen}
        onToggle={setIsEditStorageModalOpen}
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
    </div >
  );
};

export default OrganizationSettings;