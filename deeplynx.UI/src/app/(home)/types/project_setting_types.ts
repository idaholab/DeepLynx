// src/app/types/projectSettings.types.ts

export interface ProjectLogo {
  logoUrl: string | null;
  logoFileName: string | null;
}

export interface UploadProjectLogoRequest {
  organizationId: number;
  projectId: number;
  file: File;
}

export interface UploadProjectLogoResponse {
  success: boolean;
  logoUrl: string;
  message: string;
}

export interface RemoveProjectLogoRequest {
  organizationId: number;
  projectId: number;
}

export interface RemoveProjectLogoResponse {
  success: boolean;
  message: string;
}

export interface ProjectBannerSettings {
  isInheritedFromOrg: boolean;
  orgBannerText?: string;
  projectBannerText: string;
  canEdit: boolean; // false if inherited from org
}

export interface SaveProjectBannerRequest {
  organizationId: number;
  projectId: number;
  bannerText: string;
}

export interface StorageLocation {
  id: string;
  name: string;
  type: "s3" | "azure" | "local" | "custom";
  region?: string;
  endpoint?: string;
  isOrgDefault?: boolean;
}

export interface ProjectStorageSettings {
  orgDefaultStorage: StorageLocation;
  additionalStorageLocations: StorageLocation[];
}

export interface AddStorageLocationRequest {
  organizationId: number;
  projectId: number;
  storageLocation: Omit<StorageLocation, "id">;
}

export interface RemoveStorageLocationRequest {
  organizationId: number;
  projectId: number;
  storageLocationId: string;
}