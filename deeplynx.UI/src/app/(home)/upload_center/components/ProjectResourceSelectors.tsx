"use client";

import { useLanguage } from "@/app/contexts/Language";
import {
  ProjectResponseDto,
  DataSourceResponseDto,
  ObjectStorageResponseDto,
} from "../../types/responseDTOs";
import {
  CheckCircleIcon,
  ExclamationCircleIcon,
} from "@heroicons/react/24/outline";

interface ProjectResourceSelectorsProps {
  // Projects
  projects: ProjectResponseDto[];
  projectId: string;
  setProjectId: (id: string) => void;
  isLoadingProjects: boolean;

  // Data Sources
  dataSources: DataSourceResponseDto[];
  dataSourceId: string;
  setDataSourceId: (id: string) => void;
  isLoadingDataSources: boolean;

  // Object Storage
  objectStorage: ObjectStorageResponseDto[];
  objectStorageId: string;
  setObjectstorageId: (id: string) => void;
  isLoadingObjectStorage: boolean;

  // Context
  hasOrganization: boolean;
  uploadMode: "file" | "bulk";
}

export default function ProjectResourceSelectors({
  projects,
  projectId,
  setProjectId,
  isLoadingProjects,
  dataSources,
  dataSourceId,
  setDataSourceId,
  isLoadingDataSources,
  objectStorage,
  objectStorageId,
  setObjectstorageId,
  isLoadingObjectStorage,
  hasOrganization,
  uploadMode,
}: ProjectResourceSelectorsProps) {
  const { t } = useLanguage();
  const selectClassName = "select select-info select-sm mt-2 w-full";

  const projectPlaceholder = !hasOrganization
    ? t.translations.SELECT_AN_ORGANIZATION_FIRST
    : isLoadingProjects
      ? t.translations.LOADING_PROJECTS
      : t.translations.PROJECT;

  const dataSourcePlaceholder = !projectId
    ? t.translations.SELECT_A_PROJECT_FIRST
    : isLoadingDataSources
      ? t.translations.LOADING_DATA_SOURCES
      : t.translations.DATA_SOURCES;

  const objectStoragePlaceholder = !projectId
    ? t.translations.SELECT_A_PROJECT_FIRST
    : isLoadingObjectStorage
      ? t.translations.LOADING_OBJECT_STORAGES
      : t.translations.OBJECT_STORAGES;

  const renderStatus = (
    isLoading: boolean,
    isComplete: boolean,
    isRequired = true,
  ) => {
    if (isLoading) {
      return <span className="loading loading-spinner loading-xs"></span>;
    }

    if (isComplete) {
      return <CheckCircleIcon className="size-6 text-success stroke-3" />;
    }

    if (!isRequired) {
      return <span className="badge badge-ghost badge-xs">-</span>;
    }

    return <ExclamationCircleIcon className="size-6 text-error stroke-3" />;
  };

  return (
    <fieldset className="grid gap-3 md:grid-cols-3">
      {/* Project Selector */}
      <label className="label flex min-w-0 flex-col items-start rounded-xl border border-base-300/50 bg-base-200/10 p-3">
        <span className="flex w-full items-center justify-between gap-2">
          <span className="label-text text-base-content font-semibold">
            {t.translations.PROJECT_SELECTOR_LABEL}
          </span>
          {renderStatus(isLoadingProjects, !!projectId)}
        </span>
        <select
          value={projectId}
          onChange={(e) => setProjectId(e.target.value)}
          className={selectClassName}
          required
          disabled={!hasOrganization || isLoadingProjects}
        >
          <option value="" disabled>
            {projectPlaceholder}
          </option>
          {projects.map((p) => (
            <option key={p.id} value={p.id}>
              {p.name}
            </option>
          ))}
        </select>
        {(!hasOrganization || isLoadingProjects) && (
          <span className="mt-2 text-xs text-base-content/70">
            {projectPlaceholder}
          </span>
        )}
      </label>

      {/* Data Source Selector */}
      <label className="label flex min-w-0 flex-col items-start rounded-xl border border-base-300/50 bg-base-200/10 p-3">
        <span className="flex w-full items-center justify-between gap-2">
          <span className="label-text text-base-content font-semibold">
            {t.translations.DATA_SOURCE}
          </span>
          {renderStatus(isLoadingDataSources, !!dataSourceId)}
        </span>
        <select
          value={dataSourceId}
          onChange={(e) => setDataSourceId(e.target.value)}
          className={selectClassName}
          required
          disabled={!projectId || isLoadingDataSources}
        >
          <option value="" disabled>
            {dataSourcePlaceholder}
          </option>
          {dataSources.map((d) => (
            <option key={d.id} value={String(d.id)}>
              {d.name}
            </option>
          ))}
        </select>
        {(!projectId || isLoadingDataSources) && (
          <span className="mt-2 text-xs text-base-content/70">
            {dataSourcePlaceholder}
          </span>
        )}
      </label>

      {/* Object Storage Selector */}
      <label className="label flex min-w-0 flex-col items-start rounded-xl border border-base-300/50 bg-base-200/10 p-3">
        <span className="flex w-full items-center justify-between gap-2">
          <span className="label-text text-base-content font-semibold">
            {t.translations.STORAGE_DESTINATION}
          </span>
          {renderStatus(
            isLoadingObjectStorage,
            uploadMode === "bulk" || !!objectStorageId,
            uploadMode === "file",
          )}
        </span>
        <select
          value={objectStorageId}
          onChange={(e) => setObjectstorageId(e.target.value)}
          className={selectClassName}
          required={uploadMode === "file"}
          disabled={
            !projectId || isLoadingObjectStorage || uploadMode === "bulk"
          }
        >
          <option value="" disabled>
            {objectStoragePlaceholder}
          </option>
          {objectStorage.map((object) => (
            <option key={object.id} value={String(object.id)}>
              {object.name}
            </option>
          ))}
        </select>
      </label>
    </fieldset>
  );
}
