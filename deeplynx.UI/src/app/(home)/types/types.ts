// src/app/(home)/types/types.ts

import React, { ReactNode } from 'react';
import { CustomQueryRequestDto } from './requestDTOs';
import { HistoricalRecordResponseDto, RecordResponseDto } from './responseDTOs';

export type RecordTableRow = HistoricalRecordResponseDto & {
  fileType: string;
  timeseries?: boolean;
  fileSize?: number;
  select?: boolean;
  associatedRecords?: string[];
  archivedAt?: string | null;
};

export type Column<T extends object> = {
  accessor?: string;
  header?: string | ReactNode;
  data?: keyof T;
  sortable?: boolean;
  cell?: (row: T, index: number) => React.ReactNode
};

export type PopularTable = {
  id: number;
  name: string;
  image: string;
  nickname: string;
  visibility: string;
};

export type MySearchsTable = {
  id: number;
  name: string;
  filters: string[];
  createdAt: string;
  sortable?: boolean;
}

export type TeamMember = {
  id: number;
  name: string;
  image: string;
  nickname: string;
  visibility: string;
  role: string;
  lastLogin: string;
};

//DatePicker
export type DatePickerQuery = {
  id: string;
  connector?: string;
  filter?: string;
  operator?: string;
  value?: string; // you can store the combined timestamp here if you want
};

//NewFileUploadCard
export type FileMetadata = {
  name: string;
  description: string;
  isTimeSeries: boolean;
  updateAction?: "merge" | "overwrite";
};

//Widgets
export type WidgetType =
  | "DataOverview"
  | "Links"
  | "Graph"
  | "RecentActivity"
  | "ProjectOverview"
  | "TeamMembers";

export type QueryBuilderQuery = {
  id: string;
  query: CustomQueryRequestDto;
};

//Uploads
export type UploadType = "new" | "version" | "properties" | "";

export type ExistingFile = {
  id: string;
  name: string;
  alias?: string;
  description?: string;
  lastUpdate?: string;
  updatedBy?: string;
  tags?: string[];
  dataSource?: string;
  propertiesSources?: string;
  timeSeries?: boolean;
  image?: string;
};

export type RecentUploadIcon = "arrows" | "link" | "stack";

export type RecentUpload = {
  id: string;
  name: string;
  avatar: string;
  file: string;
  icon: RecentUploadIcon;
};

//file_upload_services
export type UploadFileArgs = {
  organizationId: number | string;
  projectId: number | string;
  dataSourceId: number | string;
  objectStorageId: number | string;
  file: File;

  // optional metadata
  name?: string;
  description?: string;
  properties?: unknown;
  tags?: string[];
  originalId?: string;
  classId?: number | string;
};

//notification_services
/** ---- Types ---- */
export type SendEmailResponse = {
  message: string;
};

//record_services
export type CreateRecordPayload = {
  name: string;
  original_id: string;
  description: string;

  properties: Record<string, unknown>;

  class_id?: number | null;
  object_storage_id?: number | null;
  uri?: string | null;
  class_name?: string | null;
  tags?: string[] | null;
  sensitivity_labels?: string[] | null;
};

export type GraphNode = {
  id: number;
  label: string;
  type: string;
};

export type GraphLink = {
  source: number;
  target: number;
  relationshipId?: number;
  relationshipName?: string;
  edgeId: number;
};

export type GraphResponse = {
  nodes?: GraphNode[];
  links?: GraphLink[];
};

// Users table row
export type UsersTableRow = {
  id: number;
  name: string;
  email: string;
  username: string | null;
  isActive: boolean;
  isArchived: boolean;
  isSysAdmin: boolean;
  isPending?: boolean;
  invitedAt?: string;
  projectName?: string;
  roleName?: string;
  projects?: Array<{ id: number; name: string; role: string }>;
};

export type Project = {
  id: string,
  name: string
}