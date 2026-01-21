"use client";

import { useState, useEffect, useCallback } from "react";
import { getAllDataSources } from "@/app/lib/client_service/data_source_services.client";
import { getAllObjectStorages } from "@/app/lib/client_service/object_storage_services.client";
import { getAllProjects } from "@/app/lib/client_service/projects_services.client";
import {
  DataSourceResponseDto,
  ObjectStorageResponseDto,
  ProjectResponseDto,
} from "../../types/responseDTOs";

export function useProjectResources(organizationId?: number) {
  // Projects
  const [projects, setProjects] = useState<ProjectResponseDto[]>([]);
  const [projectId, setProjectId] = useState<string>("");
  const [isLoadingProjects, setIsLoadingProjects] = useState(false);
  
  // Data Sources
  const [dataSources, setDataSources] = useState<DataSourceResponseDto[]>([]);
  const [dataSourceId, setDataSourceId] = useState<string>("");
  const [isLoadingDataSources, setIsLoadingDataSources] = useState(false);
  
  // Object Storage
  const [objectStorage, setObjectstorage] = useState<ObjectStorageResponseDto[]>([]);
  const [objectStorageId, setObjectstorageId] = useState<string>("");
  const [isLoadingObjectStorage, setIsLoadingObjectStorage] = useState(false);

  // Fetch projects
  const fetchProjects = useCallback(async () => {
    if (!organizationId) {
      setProjects([]);
      setProjectId("");
      setIsLoadingProjects(false);
      return;
    }

    setIsLoadingProjects(true);

    try {
      const data = await getAllProjects(organizationId, true);
      setProjects(data);
      if (data.length === 1) {
        setProjectId(String(data[0].id));
      }
    } catch (error) {
      console.error("Error fetching projects:", error);
      setProjects([]);
    } finally {
      setIsLoadingProjects(false);
    }
  }, [organizationId]);

  // Fetch data sources and object storage when project changes
  useEffect(() => {
    if (!projectId) {
      setDataSources([]);
      setObjectstorage([]);
      setDataSourceId("");
      setObjectstorageId("");
      setIsLoadingDataSources(false);
      setIsLoadingObjectStorage(false);
      return;
    }

    setIsLoadingDataSources(true);
    setIsLoadingObjectStorage(true);
    setDataSourceId("");
    setObjectstorageId("");

    (async () => {
      // Fetch Data Sources
      try {
        const dataSource = await getAllDataSources(Number(projectId));
        setDataSources(dataSource);
        if (dataSource.length === 1) {
          setDataSourceId(String(dataSource[0].id));
        }
      } catch (error) {
        console.error("Error fetching data sources:", error);
        setDataSources([]);
      } finally {
        setIsLoadingDataSources(false);
      }

      // Fetch Object Storage
      if (organizationId) {
        try {
          const objectStorage = await getAllObjectStorages(
            organizationId,
            Number(projectId)
          );
          setObjectstorage(objectStorage);
          if (objectStorage.length === 1) {
            setObjectstorageId(String(objectStorage[0].id));
          }
        } catch (error) {
          console.error("Error fetching object storage:", error);
          setObjectstorage([]);
        } finally {
          setIsLoadingObjectStorage(false);
        }
      }
    })();
  }, [organizationId, projectId]);

  // Fetch projects on mount
  useEffect(() => {
    fetchProjects();
  }, [fetchProjects]);

  return {
    // Projects
    projects,
    projectId,
    isLoadingProjects,
    setProjectId,
    
    // Data Sources
    dataSources,
    dataSourceId,
    isLoadingDataSources,
    setDataSourceId,
    
    // Object Storage
    objectStorage,
    objectStorageId,
    isLoadingObjectStorage,
    setObjectstorageId,
  };
}