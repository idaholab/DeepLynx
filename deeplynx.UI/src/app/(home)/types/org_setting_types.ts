// src/app/types/org_setting_types.ts

export interface OrganizationLogo {
  logoUrl: string | null;
  logoFileName: string | null;
}

export interface UploadLogoRequest {
  organizationId: number;
  file: File;
}

export interface UploadLogoResponse {
  success: boolean;
  logoUrl: string;
  message: string;
}

export interface RemoveLogoRequest {
  organizationId: number;
}

export interface RemoveLogoResponse {
  success: boolean;
  message: string;
}

