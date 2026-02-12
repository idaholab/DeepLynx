export type ClassResponseDto = {
  id: number;
  name: string;
  description: string | null;
  uuid: string | null;
  projectid: number;
  lastUpdatedAt: string | null;
  lastUpdatedBy: string | null;
  isArchived: boolean;
  archivedat: string | null;
  createdby: string | null;
  createdat: string;
};

export type TokenResponseDto = {
  apiKey: string;
  apiSecret: string;
}

export type DataSourceResponseDto = {
  id: number;
  name: string;
  description: string | null;
  default: boolean;
  abbreviation: string | null;
  type: string | null;
  baseuri: string | null;
  config: Record<string, unknown> | null; // object | null
  projectid: number;
  lastUpdatedAt: string | null; // RFC 3339 or null
  lastUpdatedBy: string | null;
  isArchived: boolean;
  createdby: string | null;
  createdat: string;          // RFC 3339 date-time
  archivedat: string | null;  // RFC 3339 or null
};

export type RelatedRecordsResponseDto = {
  relatedRecordName: string;
  relatedRecordId: number;
  relatedRecordProjectId: number;
  relationshipName: string | null;
}

export type GroupResponseDto = {
  id: number | string;
  name: string;
  description?: string | null;
  lastUpdatedAt?: Date;
  lastUpdatedBy?: string | null;
  isArchived: boolean;
  organizationId: number | string;
  memberCount?: number;
}

export interface HistoricalRecordResponseDto {
  id: number;
  uri?: string | null;
  properties?: string | null;
  originalId?: string | null;
  name?: string | null;
  description?: string | null;
  classId?: number;
  className?: string | null;
  dataSourceId?: number;
  dataSourceName: string;
  projectId?: number;
  projectName: string;
  tags?: string | null;
  labels?: string | null;
  lastUpdatedAt: string;
  lastUpdatedBy?: string | null;
  isArchived: boolean;
  objectStorageId?: number;
  objectStorageName?: string | null;
}

export type RecordResponseDto = {
  id: number | null;
  name: string;
  description?: string | null;
  uri?: string | null;
  properties?: unknown;
  objectStorageId?: number | null;
  originalId?: string | null;
  classId?: number | null;
  dataSourceId?: number | null;
  projectId?: number | null;
  lastUpdatedAt?: string;
  lastUpdatedBy?: string | null;
  isArchived?: boolean;
  fileType?: string | null;
  tags?: { id: number | null; name: string }[];
};

export type ObjectStorageResponseDto = {
  id: number | string;
  name: string;
  type: string;
  projectId: number | string;
  default: boolean;
  lastUpdatedAt: string;
  lastUpdatedBy: string;
  isArchived: boolean;
}

export type OrganizationResponseDto = {
  id: number | string;
  name: string;
  description?: string | null;
  lastUpdatedAt?: Date;
  lastUpdatedBy?: string | null;
  isArchived: boolean;
  defaultOrg?: boolean;
  banner?: string;
}

export type PermissionResponseDto = {
  id: number | string;
  name: string;
  description?: string | null;
  action: string;
  resource?: string | null;
  isDefault: boolean;
  labelId?: number | string;
  lastUpdatedAt?: Date;
  lastUpdatedBy?: string | null;
  isArchived: boolean;
  projectId?: number | string;
  organizationId?: number | string;
}

export type ProjectMembersDto = {
  name: string;
  memberId?: number | null;
  email: string;
  role: string;
  roleId?: number | null;
  groupId?: number | null;
  projectId: number;
}

export type ProjectResponseDto = {
  id: number | string;
  name: string;
  description?: string | null;
  abbreviation?: string | null;
  lastUpdatedAt?: Date;
  lastUpdatedBy?: string | null;
  isArchived: boolean;
  organizationId: number | string;
  banner?: string;
};

export type ProjectStatResponseDto = {
  classes: number;
  records: number;
  datasources: number;
}

export type RoleResponseDto = {
  id: number;
  name: string;
  description: string | null;
  lastUpdatedAt: string;
  lastUpdatedBy: number;
  isArchived: boolean;
  projectId: number;
  organizationId: number;
}

export type TagResponseDto = {
  id: number;
  name: string;
  projectId: number;
  lastUpdatedAt?: string | null;
  lastUpdatedBy?: string | null;
  isArchived: boolean;
  archivedAt?: string | null;
};

export type SensitivityLabelsDto = {
  id: number;
  name: string;
  description: string | null;
  lastUpdatedAt: string;
  lastUpdatedBy: number | null;
  isArchived: boolean;
  projectId: number | null;
  organizationId: number | null;
}

export type UserResponseDto =
  {
    id: number;
    name: string;
    email: string;
    username: string;
    isSysAdmin: boolean;
    isArchived: boolean;
    isActive: boolean;
    role?: string;
  }

export type UserAdminInfoDto = {
  id: number;
  name: string;
  email: string;
  username: string | null;
  isSysAdmin: boolean;
  isArchived: boolean;
  isActive: boolean;
  isOrgAdmin: boolean | null;
  isProjectAdmin: boolean | null;
};

export type PendingInviteDto = {
  id: number;
  email: string;
  invitedAt: string;
  expiresAt: string;
  projectId?: number;
  projectName?: string;
  roleId?: number;
  roleName?: string;
  status: 'pending' | 'expired';
}

export type GraphResponseDto = {
  nodes: Array<{
    id: number;
    label: string;
    type: string;
  }>;
  links: Array<{
    source: number;
    target: number;
    relationshipId: number;
    relationshipName: string | null;
    edgeId: number;
  }>;
}

export type OauthApplicationResponseDto = {
  id: number;
  clientId: string;
  name: string;
  description?: string;
  callbackUrl: string;
  baseUrl?: string;
  appOwnerEmail?: string;
  isArchived: boolean;
  lastUpdatedAt: string;
  lastUpdatedBy?: number;
}

export type OauthApplicationSecureResponseDto = {
  name: string;
  clientId: string;
  clientSecretRaw: string;
}

export type PaginatedEventsResponseDto =
  {
    items: EventResponseDto[] | [];
    pageNumber: number;
    pageSize: number;
    maxPageSize: number;
    totalCount: number;
  };

export type EventResponseDto = {
  id: number;
  operation: string;
  entityType: string;
  entityId?: number | null;
  projectId: number;
  organizationId?: number | null;
  organizationName: string;
  dataSourceId?: number | null;
  properties?: JSON | string | null;
  projectName?: string | null;
  entityName?: string | null;
  dataSourceName?: string | null;
  lastUpdatedAt?: string | null;
  lastUpdatedBy?: number | null;
  lastUpdatedByUserName?: string | null;
};

export type EdgeResponseDto = {
  id: number;
  originId: number;
  destinationId: number;
  relationshipId?: number;
  dataSourceId: number;
  projectId: number;
  lastUpdatedAt: string;
  lastUpdatedBy?: number;
  isArchived: boolean;
};

export type RelationshipResponseDto = {
  id: number;
  name: string;
  description?: string;
  uuid?: string;
  projectId: number;
  lastUpdatedAt: string;
  lastUpdatedBy?: number;
  isArchived: boolean;
  originId?: number;
  destinationId?: number;
};

export type ProjectMemberResponseDto = {
  name: string;
  memberId?: number;
  email: string;
  role?: string;
  roleId?: number;
};
