// API Payload Types

export interface BulkTag {
  id?: number;
  name: string;
}

export interface BulkClass {
  id?: number;
  name: string;
  description?: string | null;
  uuid?: string | null;
  project_id?: number | null;
  organization_id?: number;
  last_updated_at?: string;
  last_updated_by?: string | null;
  is_archived?: boolean;
}

export interface BulkRelationship {
  id?: number;
  name: string;
  description?: string | null;
  uuid?: string | null;
  project_id?: number | null;
  organization_id?: number;
  last_updated_at?: string;
  last_updated_by?: string | null;
  is_archived?: boolean;
  origin_id?: number | null;
  destination_id?: number | null;
}

export interface BulkRecord {
  id?: number;
  name: string; // required
  description: string; // required
  uri?: string | null;
  properties: Record<string, unknown>; // required - object/JSON
  object_storage_id?: number | null;
  original_id: string; // required
  class_id?: number | null;
  class_name?: string | null;
  data_source_id?: number;
  project_id?: number;
  organization_id?: number;
  last_updated_at?: string;
  last_updated_by?: string | null;
  is_archived?: boolean;
  file_type?: string | null;
  tags?: string[]; // Array of tag names
  sensitivity_labels?: string[];
}

export interface BulkEdge {
  id?: number;
  origin_id?: number | null;
  destination_id?: number | null;
  relationship_id?: number | null;
  relationship_name?: string | null;
  origin_oid?: string | null;
  destination_oid?: string | null;
  data_source_id?: number;
  project_id?: number;
  organization_id?: number;
  last_updated_at?: string;
  last_updated_by?: string | null;
  is_archived?: boolean;
}

export interface BulkMetadataPayload {
  classes: BulkClass[];
  relationships: BulkRelationship[];
  tags: BulkTag[];
  records: BulkRecord[];
  edges: BulkEdge[];
}

// Parsed CSV Row Type (before validation, with human-readable field names)
export interface ParsedCsvRow {
  name: string;
  description: string;
  original_id: string;
  properties: string; // JSON string from CSV
  uri?: string;
  object_storage_id?: string; // Will be converted to number
  class_id?: string; // Will be converted to number
  class_name?: string;
  file_type?: string;
  tags?: string; // Comma-separated string
  sensitivity_labels?: string; // Comma-separated string
}

// Validation Result Types
export interface ValidationError {
  row: number;
  recordName: string;
  errors: string[];
}

export interface ValidationResult {
  isValid: boolean;
  validRecords: BulkRecord[];
  errors: ValidationError[];
  totalRows: number;
  validCount: number;
  invalidCount: number;
}
