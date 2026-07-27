// src/app/(home)/organization_management/settings/OrganizationSettings.tsx
"use client";

import { useState, useEffect, useCallback } from "react";
import toast from "react-hot-toast";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import {
  LockClosedIcon,
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
import { CreateObjectStorageRequestDto, UpdateObjectStorageRequestDto } from "../../types/requestDTOs";


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
  const [isSavingStorage, setIsSavingStorage] = useState(false);
  const [defaultStorage, setDefaultStorage] =
    useState<ObjectStorageResponseDto | null>(null);


  // Storage config fields based on type
  const [azureConnectionString, setAzureConnectionString] = useState("");
  const [createContainerPerProject, setCreateContainerPerProject] = useState(false);
  const [isTouched, setIsTouched] = useState(false);

  const onAzureConnectionStringChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setAzureConnectionString(e.target.value);
    setIsTouched(true);
  };

  const onCreateContainerToggle = (checked: boolean) => {
    setCreateContainerPerProject(checked);
    setIsTouched(true);
    console.log("isTouched: " + isTouched)
  };

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
        console.log("orgData.createContainerPerProject: " + orgData.createContainerPerProject)
        setIsTouched(false);
      } catch (error) {
        console.error("Failed to load organization settings", error);
      }
    }

    loadOrgSettings();
  }, [organization?.organizationId]);


  const handleSave = async () => {
    const isCreatingStorage = !defaultStorage;

    if (isCreatingStorage && !azureConnectionString.trim()) {
      toast.error("Azure connection string is required.");
      return;
    }

    try {
      setIsSavingStorage(true);

      const dto2 = { createContainerPerProject };
      await updateOrganization(organization?.organizationId as number, dto2);

      if (isCreatingStorage || azureConnectionString.trim()) {
        const dto = {
          name: "Default Organization Storage",
          config: {
            azureObjectConfig: {
              azureConnectionString: azureConnectionString.trim(),
              azureContainerName: "default-container",
            },
          },
          default: true,
        };

        if (isCreatingStorage) {
          const created = await createOrganizationObjectStorage(
            organization?.organizationId as number,
            dto,
          );
          setDefaultStorage(created);
        } else {
          const updated = await updateOrganizationObjectStorage(
            organization?.organizationId as number,
            defaultStorage.id as number,
            dto,
          );
          setDefaultStorage(updated);
        }
      }

      toast.success("Settings saved successfully.");
    } catch (error) {
      console.error("Failed to save settings:", error);
      toast.error("Failed to save settings.");
    } finally {
      setIsSavingStorage(false);
    }
  };

  const handleReset = () => {
    setAzureConnectionString("");
    setCreateContainerPerProject(false);
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
              </div>
            </div>
          </div>

          {/* RIGHT COLUMN */}
          <div className="flex flex-col gap-6">
            <div className="card bg-base-100 border border-base-300/50 shadow-sm">
              <div className="card-body">
                <h3 className="card-title text-lg flex items-center gap-2">{t.translations.STORAGE_SETTINGS}</h3>

                <div className="form-control mb-6">
                  <span className="label-text font-semibold">
                    {t.translations.AZURE_DEFAULT_CONNECTION_STRING}
                  </span>
                  <input
                    id="azureConnectionString"
                    type="password"
                    className="input input-bordered w-full"
                    placeholder={t.translations.ENTER_AZURE_CONNECTION_STRING}
                    value={azureConnectionString}
                    onChange={onAzureConnectionStringChange}
                    disabled={isSavingStorage}
                  />
                </div>

                <div className="form-control mb-4">
                  <span className="text font-semibold mr-2">
                    {t.translations.CREATE_CONTAINER_PER_PROJECT}
                  </span>
                  <input
                    type="checkbox"
                    checked={createContainerPerProject}
                    onChange={(e) => onCreateContainerToggle(e.target.checked)}
                    className="toggle toggle-primary"
                    disabled={isSavingStorage}
                  />
                </div>

                <div className="flex justify-end gap-4">
                  <button
                    className="btn btn-outline"
                    onClick={handleReset}
                    disabled={isSavingStorage}
                  >
                    {t.translations.CANCEL}
                  </button>
                  <button
                    className="btn btn-primary"
                    onClick={handleSave}
                    disabled={isSavingStorage || !isTouched}
                  >
                    {isSavingStorage && <span className="loading loading-spinner loading-xs mr-2" />}
                    {t.translations.SAVE}
                  </button>
                </div>
              </div>
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
      {themeToast && (
        <div className="toast toast-bottom toast-end">
          <div className={`alert alert-${themeToast.type}`}>
            {themeToast.message}
          </div>
        </div>
      )}
    </div>
  );
};

export default OrganizationSettings;
