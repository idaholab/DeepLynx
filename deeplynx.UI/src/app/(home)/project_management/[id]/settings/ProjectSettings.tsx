// src/app/(home)/project_management/[id]/settings/ProjectSettings.tsx
"use client";

import { useState, useEffect } from "react";
import Image from "next/image";
import toast from "react-hot-toast";
import { useProjectSession } from "@/app/contexts/ProjectSessionProvider";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import {
  archiveProject,
  getProjectLogoUrl,
  removeProjectLogo,
  uploadProjectLogo,
} from "@/app/lib/client_service/projects_services.client";
import ArchiveDelete from "@/app/(home)/components/ArchiveDelete";
import { ProjectResponseDto } from "@/app/(home)/types/responseDTOs";
import { useLanguage } from "@/app/contexts/Language";
import {
  ExclamationTriangleIcon,
  InformationCircleIcon,
} from "@heroicons/react/24/outline";

interface ProjectSettingsProps {
  project: ProjectResponseDto | null;
}

const ProjectSettings = ({ project }: ProjectSettingsProps) => {
  const { clearProject } = useProjectSession();
  const { organization } = useOrganizationSession();
  const { t } = useLanguage();
  const [logoPreview, setLogoPreview] = useState<string | null>(null);
  const [logoFile, setLogoFile] = useState<File | null>(null);
  const [isUploading, setIsUploading] = useState(false);
  const [isCheckingLogo, setIsCheckingLogo] = useState(true);

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

  if (isCheckingLogo) {
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

        {/* Logo Section */}
        <div className="card bg-base-100 border border-primary/40 shadow-sm max-w-[40%]">
          <div className="card-body">
            <h3 className="card-title text-lg mb-4">
              {t.translations.PROJECT_LOGO}
            </h3>

            <div className="flex items-start gap-6 mb-6">
              {/* Logo Preview */}
              <div className="avatar">
                <div className="w-32 h-32 rounded-xl bg-base-200 flex items-center justify-center overflow-hidden border-2 border-base-300 relative">
                  {logoPreview ? (
                    <Image
                      src={logoPreview}
                      alt="Project Logo"
                      fill
                      sizes="128px"
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
                    <InformationCircleIcon className="size-6" />
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
