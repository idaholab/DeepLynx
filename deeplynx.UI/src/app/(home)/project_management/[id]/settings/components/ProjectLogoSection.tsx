// src/app/(home)/project_management/[id]/settings/components/ProjectLogoSection.tsx
"use client";

import Image from "next/image";
import { InformationCircleIcon } from "@heroicons/react/24/outline";
import { ProjectResponseDto } from "@/app/(home)/types/responseDTOs";

interface ProjectLogoSectionProps {
  project: ProjectResponseDto;
  logoPreview: string | null;
  logoFile: File | null;
  isUploading: boolean;
  onLogoChange: (fileList: FileList | null) => void;
  onUploadLogo: () => void;
  onCancelSelection: () => void;
  onLogoError: () => void;
  t: { translations: Record<string, string> };
}

const ProjectLogoSection = ({
  project,
  logoPreview,
  logoFile,
  isUploading,
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
                loader={({ src }) => src}
                src={logoPreview}
                alt="Project Logo"
                width={128}
                height={128}
                className="object-contain w-full h-full p-2"
                unoptimized
                onError={() => {
                  onLogoError();
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
                {t.translations.RECOMMENDED_PNG_WITH_TRANSPARENT_BACKGROUND}
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
);

export default ProjectLogoSection;
