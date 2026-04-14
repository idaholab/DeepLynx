// src/app/(home)/project_management/[id]/settings/components/ProjectLogoSection.tsx
"use client";

import { InformationCircleIcon } from "@heroicons/react/24/outline";
import { ProjectResponseDto } from "@/app/(home)/types/responseDTOs";
import Image from "next/image";

interface ProjectLogoSectionProps {
  project: ProjectResponseDto;
  logoPreview: string | null;
  logoFile: File | null;
  isUploading: boolean;
  bannerText: string;
  setBannerText: (value: string) => void;
  isSavingBanner: boolean;
  originalBannerText: string;
  onSaveBanner: () => void;
  onCancelBanner: () => void;
  onLogoChange: (fileList: FileList | null) => void;
  onUploadLogo: () => void;
  onCancelSelection: () => void;
  onLogoError: () => void;
  t: { translations: Record<string, string> };
}

const ProjectSettingsLeftColumn = ({
  project,
  logoPreview,
  logoFile,
  isUploading,
  bannerText,
  setBannerText,
  isSavingBanner,
  originalBannerText,
  onSaveBanner,
  onCancelBanner,
  onLogoChange,
  onUploadLogo,
  onCancelSelection,
  onLogoError,
  t,
}: ProjectLogoSectionProps) => (
  <div className="card bg-base-100 border border-primary/40 shadow-sm">
    <div className="card-body">
      <h3 className="card-title text-lg mb-4">{t.translations.PROJECT_LOGO}</h3>

      <div className="flex items-start gap-6 mb-6">
        <div className="avatar">
          <div className="w-32 h-32 rounded-xl bg-base-200 flex items-center justify-center overflow-hidden border-2 border-base-300">
            {logoPreview ? (
              <Image
                src={logoPreview}
                alt="Project Logo"
                width={128}
                height={128}
                className="object-contain w-full h-full p-2"
                onError={() => {
                  onLogoError();
                }}
                unoptimized
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
                onChange={(e) => onLogoChange(e.target.files)}
              />
            </label>

            {logoFile && (
              <>
                <button
                  type="button"
                  className="btn btn-sm btn-success"
                  onClick={onUploadLogo}
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
                  onClick={onCancelSelection}
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
        </div>
      </div>
    </div>

    {/* <div className="divider px-4"></div> */}

    {/* Banner */}
    {/* <div className="card bg-base-100 shadow-sm">
      <div className="card-body">
        <h3 className="card-title text-lg mb-4">
          {t.translations.PROJECT_WARNING_BANNER}
        </h3>

        <div className="form-control">
          <div>
            <label className="label mr-4">
              <span className="label-text font-semibold">
                {t.translations.BANNER_TEXT}
              </span>
            </label>
            <textarea
              className="textarea textarea-bordered min-h-20"
              placeholder={t.translations.BANNER_EXAMPLE_CUI}
              value={bannerText}
              onChange={(e) => setBannerText(e.target.value)}
              disabled={isSavingBanner}
              maxLength={240}
            />
          </div>

          <label className="label">
            <span className="label-text-alt text-base-content/60">
              {
                t.translations
                  .DISPLAY_BENEATH_THE_TOP_HEADER_FOR_ALL_PAGES_IN_PROJECT
              }
            </span>
            <span
              className={`label-text-alt mt-4 ${bannerText.length > 50 ? "text-error" : "text-base-content/40"}`}
            >
              {bannerText.length} / 50
            </span>
          </label>
        </div> */}

    {/* Action Buttons */}
    {/* <div className="flex gap-2 mt-4">
          <button
            type="button"
            className="btn btn-primary btn-sm"
            onClick={onSaveBanner}
            disabled={
              isSavingBanner ||
              bannerText === originalBannerText ||
              bannerText.length > 240
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
              onClick={onCancelBanner}
              disabled={isSavingBanner}
            >
              {t.translations.CANCEL}
            </button>
          )}
        </div>
      </div>
    </div> */}
  </div>
);

export default ProjectSettingsLeftColumn;
