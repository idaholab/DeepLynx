"use client";

import { useLanguage } from "@/app/contexts/Language";
import {
  ProjectResponseDto,
  DataSourceResponseDto,
  ObjectStorageResponseDto,
} from "../../types/responseDTOs";

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

  return (
    <fieldset className="space-x-4">
      {/* Project Selector */}
      <label className="label flex-col items-start text-base-content font-bold">
        <span className="label-text mb-1">
          Select a project
          {isLoadingProjects && (
            <span className="loading loading-spinner loading-xs ml-2"></span>
          )}
        </span>
        <select
          value={projectId}
          onChange={(e) => setProjectId(e.target.value)}
          className="select select-info select-sm mt-2"
          required
          disabled={!hasOrganization || isLoadingProjects}
        >
          <option value="" disabled>
            {!hasOrganization
              ? "Select an organization first"
              : isLoadingProjects
              ? "Loading projects..."
              : t.translations.PROJECT}
          </option>
          {projects.map((p) => (
            <option key={p.id} value={p.id}>
              {p.name}
            </option>
          ))}
        </select>
      </label>

      {/* Data Source Selector */}
      <label className="label flex-col items-start text-base-content font-bold">
        <span className="label-text mb-1">
          {t.translations.DATA_SOURCE}
          {isLoadingDataSources && (
            <span className="loading loading-spinner loading-xs ml-2"></span>
          )}
        </span>
        <select
          value={dataSourceId}
          onChange={(e) => setDataSourceId(e.target.value)}
          className="select select-info select-sm mt-2"
          required
          disabled={!projectId || isLoadingDataSources}
        >
          <option value="" disabled>
            {!projectId
              ? "Select a project first"
              : isLoadingDataSources
              ? "Loading data sources..."
              : "Data Sources"}
          </option>
          {dataSources.map((d) => (
            <option key={d.id} value={String(d.id)}>
              {d.name}
            </option>
          ))}
        </select>
      </label>

      {/* Object Storage Selector */}
      <label className="label flex-col items-start text-base-content font-bold">
        <span className="label-text mb-1">
          {t.translations.STORAGE_DESTINATION}
          {isLoadingObjectStorage && (
            <span className="loading loading-spinner loading-xs ml-2"></span>
          )}
        </span>
        <select
          value={objectStorageId}
          onChange={(e) => setObjectstorageId(e.target.value)}
          className="select select-info select-sm mt-2"
          required={uploadMode === "file"}
          disabled={
            !projectId || isLoadingObjectStorage || uploadMode === "bulk"
          }
        >
          <option value="" disabled>
            {!projectId
              ? "Select a project first"
              : isLoadingObjectStorage
              ? "Loading object storages..."
              : "Object storages"}
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
