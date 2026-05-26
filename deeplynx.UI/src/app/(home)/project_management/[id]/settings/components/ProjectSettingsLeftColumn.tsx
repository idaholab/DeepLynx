// src/app/(home)/project_management/[id]/settings/components/ProjectLogoSection.tsx
"use client";

import { InformationCircleIcon } from "@heroicons/react/24/outline";
import { ProjectResponseDto } from "@/app/(home)/types/responseDTOs";
import Image from "next/image";
import ArchiveDelete from "@/app/(home)/components/ArchiveDelete";
import ProjectSettingsTable from "./ProjectSettingsTable";
import { useCallback, useMemo } from "react";
import { formatLocalDateTime } from "@/app/lib/date_time";
import toast from "react-hot-toast";
import { OrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import { updateProject } from "@/app/lib/client_service/projects_services.client";

interface ProjectLogoSectionProps {
  organization: OrganizationSession | null;
  project: ProjectResponseDto | null;
  setProject: React.Dispatch<React.SetStateAction<ProjectResponseDto | null>>;
  logoPreview: string | null;
  logoFile: File | null;
  isUploading: boolean;
  onLogoChange: (fileList: FileList | null) => void;
  onUploadLogo: () => void;
  onCancelSelection: () => void;
  onLogoError: () => void;
  onArchiveProject: () => void | Promise<void>;
  t: { translations: Record<string, string> };
}

interface informationTableProps {
  organization: OrganizationSession | null;
  project: ProjectResponseDto | null;
  t: { translations: Record<string, string> }; 
  setProject: React.Dispatch<React.SetStateAction<ProjectResponseDto | null>>;
}

function projectInformationTable({ organization, project, t, setProject }: informationTableProps) {  
  const handleUpdateProject = useCallback(
    async (field: string, value: string, successMessage: string) => {
      if (!organization?.organizationId) return;

      try {
        const update = await updateProject(
          organization.organizationId as number,
          project?.id as number,
          { [field]: value, organizationId: Number(organization.organizationId) },
        );
        setProject((prev) => {
          if (!prev) return prev;

          return {
            ...prev,
            name: update.name ?? prev.name,
            description: update.description ?? prev.description,
            abbreviation: update.abbreviation ?? prev.abbreviation,
            lastUpdatedAt: update.lastUpdatedAt ?? prev.lastUpdatedAt,
            lastUpdatedBy: update.lastUpdatedBy ?? prev.lastUpdatedBy,
            isArchived: update.isArchived ?? prev.isArchived,
            organizationId: update.organizationId ?? prev.organizationId,
            banner: update.banner ?? prev.banner,
          };
        });
        toast.success(successMessage);
      } catch (error) {
        toast.error(`${t.translations.FAILED_TO_UPDATE} ${field}`);
      }
    },
    [
      organization?.organizationId,
      project?.id,
      t.translations.FAILED_TO_UPDATE,
    ],
  );
  
  const projectInfoRows = useMemo(() => {
    if (!project) return [];
    return [
      { key: t.translations.PROJECT_ID, value: project.id },
      { 
        key: t.translations.PROJECT_NAME,
        value: project.name, 
        editable: true, 
        onEdit: (value: string) => 
          handleUpdateProject(
            "name", 
            value, 
            t.translations.PROJECT_NAME_UPDATED),
        maxCharacters: 50,
      },
      {
        key: t.translations.PROJECT_DESCRIPTION,
        value: project.description,
        editable: true,
        onEdit: (value: string) => 
          handleUpdateProject(
            "description", 
            value, 
            t.translations.PROJECT_DESCRIPTION_UPDATED),
          maxCharacters: 250,
      },
      {
        key: t.translations.LAST_UPDATED_AT,
        value: formatLocalDateTime(String(project.lastUpdatedAt))
      }
    ];
  }, [project, handleUpdateProject, t.translations])
  return (
    <ProjectSettingsTable projectRows={ projectInfoRows }/>
  );
}

const ProjectSettingsLeftColumn = ({
  organization,
  project,
  setProject,
  logoPreview,
  logoFile,
  isUploading,
  onLogoChange,
  onUploadLogo,
  onCancelSelection,
  onLogoError,
  onArchiveProject,
  t,
}: ProjectLogoSectionProps) => (
  <div className="card self-start bg-base-100 border border-primary/40 shadow-sm">
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

      {/* Main Project Settings */}
      <div className="border-t border-base-300 pt-6 pb-6">
        <h3 className="card-title text-lg mb-4">
          {t.translations.MAIN_PROJECT_SETTINGS} 
        </h3>
        <div>
          {projectInformationTable({organization, project, t, setProject})}
        </div>
      </div>

      <div className="border-t border-base-300 pt-6">
        <ArchiveDelete
          actionType="archive"
          itemType="Project"
          itemName={project?.name || ""}
          onConfirm={onArchiveProject}
        />
      </div>
    </div> 
    
  </div>
);

export default ProjectSettingsLeftColumn;
