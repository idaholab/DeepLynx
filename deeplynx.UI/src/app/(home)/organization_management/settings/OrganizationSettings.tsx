// src/app/(home)/organization_management/settings/OrganizationSettings.tsx
"use client";

import { useState, useEffect, JSX } from "react";
import toast from "react-hot-toast";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import {
  ChartBarIcon,
  Squares2X2Icon,
  EyeIcon,
  LockClosedIcon,
} from "@heroicons/react/24/outline";
import {
  getOrganizationLogoUrl,
  uploadOrganizationLogo,
  removeOrganizationLogo,
} from "@/app/lib/client_service/organization_services.client";
import { useLanguage } from "@/app/contexts/Language";
import React from "react";
import Image from "next/image";

/* -------------------------------------------------------------------------- */
/*                           Service Interface                                */
/* -------------------------------------------------------------------------- */

interface Service {
  id: string;
  name: string;
  icon: JSX.Element;
  description: string;
  status: "connected" | "disconnected";
}

const OrganizationSettings = () => {
  const { organization } = useOrganizationSession();
  const { t } = useLanguage();
  const [logoPreview, setLogoPreview] = useState<string | null>(null);
  const [logoFile, setLogoFile] = useState<File | null>(null);
  const [isUploading, setIsUploading] = useState(false);
  const [isCheckingLogo, setIsCheckingLogo] = useState(true);

  // Placeholder states (disabled)
  const [bannerText, setBannerText] = useState(
    t.translations.THIS_ORG_SPACE_MAY_CONTAIN_SENSATIVE_DATA,
  );
  const [storageLocation, setStorageLocation] = useState<string>("org-default");
  const [expandedService, setExpandedService] = useState<string | null>(null);

  // TODO: This is just place fillers for now
  const services: Service[] = [
    {
      id: "insight",
      name: "Insight",
      icon: <ChartBarIcon className="w-5 h-5" />,
      description: "Advanced analytics and reporting",
      status: "disconnected",
    },
    {
      id: "lattice",
      name: "Lattice",
      icon: <Squares2X2Icon className="w-5 h-5" />,
      description: "Data mesh and federation",
      status: "disconnected",
    },
    {
      id: "visualize",
      name: "Visualize",
      icon: <EyeIcon className="w-5 h-5" />,
      description: "3D visualization and rendering",
      status: "disconnected",
    },
  ];

  // Load existing logo on mount
  useEffect(() => {
    const loadExistingLogo = async () => {
      if (!organization?.organizationId) {
        setIsCheckingLogo(false);
        return;
      }

      try {
        setIsCheckingLogo(true);
        const logoUrl = await getOrganizationLogoUrl(
          organization.organizationId as number,
        );

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
  }, [organization?.organizationId]);

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
    if (!organization?.organizationId || !logoFile) {
      toast.error(t.translations.NO_FILE_SELECTED);
      return;
    }

    try {
      setIsUploading(true);

      const result = await uploadOrganizationLogo({
        organizationId: organization.organizationId as number,
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
    if (!organization?.organizationId) return;

    try {
      await removeOrganizationLogo({
        organizationId: organization.organizationId as number,
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
    if (organization?.organizationId) {
      const logoUrl = await getOrganizationLogoUrl(
        organization.organizationId as number,
      );
      setLogoPreview(logoUrl);
    } else {
      setLogoPreview(null);
    }
  };

  const toggleService = (serviceId: string) => {
    setExpandedService(expandedService === serviceId ? null : serviceId);
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
                          alt="Organization Logo"
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
                      {organization?.organizationName || "Organization"}
                    </span>

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

                {/* Banner Text Section - DISABLED/COMING SOON */}
                <div className="divider"></div>
                <div className="relative opacity-50 pointer-events-none">
                  <div className="form-control">
                    <label className="label">
                      <span className="label-text font-semibold flex items-center gap-2">
                        {t.translations.ORGANIZATION_WARNING_BANNER}
                        <span className="badge badge-sm badge-warning">
                          {t.translations.COMING_SOON}
                        </span>
                      </span>
                    </label>
                    <textarea
                      className="textarea textarea-bordered min-h-20"
                      placeholder={t.translations.BANNER_EXAMPLE_CUI}
                      value={bannerText}
                      onChange={(e) => setBannerText(e.target.value)}
                      disabled
                    />
                    <label className="label">
                      <span className="label-text-alt text-base-content/60">
                        {
                          t.translations
                            .DISPLAY_BENEATH_THE_TOP_HEADER_FOR_ALL_PAGES_IN_ORG
                        }
                      </span>
                      <span className="label-text-alt text-base-content/40">
                        {bannerText.length} / 240
                      </span>
                    </label>
                  </div>

                  {/* Lock icon overlay */}
                  <div className="absolute top-0 right-0 mt-2 mr-2">
                    <LockClosedIcon className="w-5 h-5 text-warning" />
                  </div>
                </div>
              </div>
            </div>
          </div>

          {/* RIGHT COLUMN */}
          <div className="flex flex-col gap-6">
            {/* ============================================================ */}
            {/*               STORAGE SETTINGS (COMING SOON)                 */}
            {/* ============================================================ */}
            <div className="card bg-base-100 border border-base-300/50 shadow-sm opacity-60">
              <div className="card-body">
                <div className="flex items-center justify-between mb-4">
                  <h3 className="card-title text-lg flex items-center gap-2">
                    {t.translations.STORAGE_SETTINGS}
                    <span className="badge badge-warning badge-sm">
                      {t.translations.COMING_SOON}
                    </span>
                  </h3>
                  <LockClosedIcon className="w-5 h-5 text-warning" />
                </div>
                <p className="text-sm text-base-content/70 mb-4">
                  {t.translations.SET_DEFAULT_UNMOUNTED_OBJECT_STORAGE}
                </p>

                <div className="form-control mb-4 pointer-events-none">
                  <label className="label">
                    <span className="label-text font-semibold">
                      {t.translations.DEFAULT_UNMOUNT_STORAGE}
                    </span>
                  </label>
                  <select
                    className="select select-bordered"
                    value={storageLocation}
                    onChange={(e) => setStorageLocation(e.target.value)}
                    disabled
                  >
                    <option value="org-default">
                      {t.translations.ORGANIZATION_DEFAULT}
                    </option>
                    <option value="s3-west">S3 — us-west-2</option>
                    <option value="s3-east">S3 — us-east-1</option>
                    <option value="local-cluster">Local Cluster Storage</option>
                  </select>
                  <label className="label">
                    <span className="label-text-alt text-base-content/60">
                      {t.translations.USE_DEFAULT_DATA_STORAGE_FOR_NEW_PROJECTS}
                    </span>
                  </label>
                </div>
              </div>
            </div>

            {/* ============================================================ */}
            {/*           ECOSYSTEM SERVICES (COMING SOON)                   */}
            {/* ============================================================ */}
            <div className="card bg-base-100 border border-base-300/50 shadow-sm opacity-60">
              <div className="card-body">
                <div className="flex items-center justify-between mb-4">
                  <h3 className="card-title text-lg flex items-center gap-2">
                    {t.translations.DEEPLYNX_ECOSYSTEM_SERVICES}
                    <span className="badge badge-warning badge-sm">
                      {t.translations.COMING_SOON}
                    </span>
                  </h3>
                  <LockClosedIcon className="w-5 h-5 text-warning" />
                </div>
                <p className="text-sm text-base-content/70 mb-4">
                  {t.translations.CONFIGURE_AUTHENTICATION_CONNECTED_SERVICE}
                </p>

                {/* Service Cards - Disabled */}
                <div className="space-y-2 pointer-events-none">
                  {services.map((service) => (
                    <div
                      key={service.id}
                      className="collapse collapse-arrow border border-base-300 bg-base-100"
                    >
                      <input
                        type="checkbox"
                        checked={expandedService === service.id}
                        onChange={() => toggleService(service.id)}
                        disabled
                      />
                      <div className="collapse-title">
                        <div className="flex items-center gap-4">
                          <div className="p-2 bg-base-200 rounded-lg">
                            {service.icon}
                          </div>
                          <div className="flex-1">
                            <div className="flex items-center gap-2 mb-1">
                              <h4 className="font-bold text-base-content">
                                {service.name}
                              </h4>
                              <div className="badge badge-sm badge-ghost">
                                {t.translations.NOT_CONNECTED}
                              </div>
                            </div>
                            <p className="text-sm text-base-content/70">
                              {service.description}
                            </p>
                          </div>
                        </div>
                      </div>

                      <div className="collapse-content">
                        <div className="pt-4 text-center">
                          <p className="text-base-content/60 mb-4 text-sm">
                            {
                              t.translations
                                .SERVICE_CONFIG_WILL_BE_AVAILABLE_SOON
                            }
                          </p>
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            </div>
          </div>
        </div>

        {/* Info Banner at Bottom */}
        <div className="alert alert-info mt-6">
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
            ></path>
          </svg>
          <div>
            <div className="font-bold">Additional Settings Coming Soon</div>
            <div className="text-sm">
              Banner text, storage configuration, and ecosystem service
              management features are currently in development.
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
    </div>
  );
};

export default OrganizationSettings;
