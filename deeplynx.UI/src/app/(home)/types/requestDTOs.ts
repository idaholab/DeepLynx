export type CustomQueryRequestDto = {
  connector?: string | null;
  filter: string;
  operator: string;
  value: string;
  json?: string;
  jsonKey?: string;
  jsonValue?: string;
};

export type RelatedRecordsRequestDto = {
  recordId?: number;
  isOrigin?: boolean;
  page?: number;
  pageSize?: number;
  hideArchived?: boolean;
}

export type CreateRoleRequestDto =
  {
    name: string;
    description?: string | null;
  }

export type PermissionRequestDto =
  {
    name: string;
    description?: string | null;
    projectId?: number;
    organizationId?: number;
  }

export type CreateOrganizationRequestDto = {
  name: string;
  description?: string;
}

export type UpdateOrganizationRequestDto = {
  name: string;
  description?: string;
}

export type CreateOauthApplicationRequestDto = {
  name: string;
  description?: string;
  callbackUrl: string;
  baseUrl?: string;
  appOwnerEmail?: string;
}

export type UpdateOauthApplicationRequestDto = {
  name?: string;
  description?: string;
  callbackUrl?: string;
  baseUrl?: string;
  appOwnerEmail?: string;
}

export type CreateObjectStorageRequestDto = {
  name: string;
  config: string;
}

export type UpdateObjectStorageRequestDto = {
  name: string;
}

export type CreateClassRequestDto = {
  name: string;
  description: string;
  uuid?: string;
}

export type UpdateClassRequestDto = {
  name: string;
  description: string;
  uuid?: string;
}

export type CreateDataSourceRequestDto = {
  name: string;
  description?: string | null;
  abbreviation?: string | null;
  type?: string | null;
  baseUri?: string | null;
  config: Record<string, unknown>;
  default: boolean;
};


export type UpdateDataSourceRequestDto = {
  name?: string;
  description?: string | null;
  abbreviation?: string | null;
  type?: string | null;
  baseUri?: string | null;
  config?: Record<string, unknown>;
};


export type CreateEdgeRequestDto = {
  origin_id?: number;
  destination_id?: number;
  relationship_id?: number;
  relationship_name?: string;
  origin_oid?: string;
  destination_oid?: string;
};

export type UpdateEdgeRequestDto = {
  origin_id?: number;
  destination_id?: number;
  name?: string;
  relationshipId?: number;
};

export type EventsQueryRequestDTO = {
  pageNumber?: number;
  pageSize?: number;
  projectId?: number;
  organizationId?: number;
  projectName?: string;
  lastUpdatedBy?: string;
  operation?: string;
  entityType?: string;
  entityName?: string;
  dataSourceName?: string;
  startDate?: string;
  endDate?: string;
};

export type CreateEventRequestDto = {
  operation: string;
  entityType: string;
  entityId?: number;
  entityName?: string;
  projectId?: number;
  organizationId?: number;
  dataSourceId?: number;
  properties: string;
  lastUpdatedBy?: string;
};

export type CreateGroupRequestDto = {
  name: string;
  description?: string;
}

export type UpdateGroupRequestDto = {
  name?: string;
  description?: string;
}

export type CreatePermissionRequestDto = {
  name: string;
  description?: string;
  action: string;
  labelId?: number;
  projectId?: number;
  organizationId?: number;
};

export type UpdatePermissionRequestDto = {
  name?: string;
  description?: string;
  action?: string;
  labelId?: number;
};

export type CreateProjectRequestDto = {
  name: string;
  description?: string;
  abbreviation?: string;
};

export type UpdateProjectRequestDto = {
  organizationId: number;
  name?: string;
  description?: string;
  abbreviation?: string;
};

export type CreateRecordRequestDto = {
  name: string;
  description: string;
  object_storage_id?: number;
  uri?: string;
  properties: Record<string, unknown>;
  original_id: string;
  class_id?: number;
  class_name?: string;
  file_type?: string;
  tags?: string[];
  sensitivity_labels?: string[];
};

export type UpdateRecordRequestDto = {
  uri?: string;
  properties?: string;
  original_id?: string;
  name?: string;
  class_id?: number;
  class_name?: string;
  description?: string;
  object_storage_id?: number;
  file_type?: string;
};

export type CreateRelationshipRequestDto = {
  name: string;
  description?: string;
  uuid?: string;
  origin_id?: number;
  destination_id?: number;
};

export type UpdateRelationshipRequestDto = {
  name?: string;
  description?: string;
  uuid?: string;
  origin_id?: number;
  destination_id?: number;
};

export type UpdateRoleRequestDto = {
  name?: string | null;
  description?: string | null;
};


export type CreateTokenDto = {
  apiKey: string;
  apiSecret: string;
  expirationMinutes?: number;
};

export type CreateTagRequestDto = {
  name: string;
}
export type UpdateTagRequestDto = {
  name?: string;
}

export interface InviteUserToOrganizationRequestDto {
    userEmail: string;
    userName?: string;
}

export interface InviteUserToProjectRequestDto {
    userEmail: string;
    userName?: string;
    roleId?: number | string;
}