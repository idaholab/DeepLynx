import { List } from "echarts";

export interface StagedClassDTO {
  id: number;
  name: string;
  validation_status: string | null;
  ontology_class_id: number | null;
  promoted_id: number | null;
  rejected: boolean;
}

export interface StagedRecordDTO {
  id: number;
  name: string;
  class_name: string | null;
  attributes: string | null;
  validation_status: string | null;
  ensemble_score: number;
  frequency: number;
  deeplynx_record_id: number | null;
  promoted_id: number | null;
  rejected: boolean;

}

export interface StagedRelationshipDTO {
  id: number;
  name: string;
  origin_class_name: string | null;
  destination_class_name: string | null;
  validation_status: string | null;
  ontology_relationship_id: number | null;
  promoted_id: number | null;
  rejected: boolean;

}

export interface StagedEdgeDTO {
  id: number;
  origin_record_name: string | null;
  destination_record_name: string | null;
  relationship_name: string | null;
  validation_status: string | null;
  ensemble_score: number;
  frequency: number;
  promoted_id: number | null;
  rejected: boolean;

}

export interface ExtractionListItemDTO {
  id: number;
  status: string;
  mode: string | null;
  created_by: number | null;
  project_id: number | null;
}

export interface ExtractionStagingResponseDTO {
  id: number;
  status: string;
  mode: string | null;
  created_by: number | null;
  classes: StagedClassDTO[];
  records: StagedRecordDTO[];
  relationships: StagedRelationshipDTO[];
  edges: StagedEdgeDTO[];
}

export interface EmbeddingStatusResponseDTO {
  ontology_ready: boolean;
  class_count: number;
  embedded_class_count: number;
  relationship_count: number;
  embedded_relationship_count: number;
}

export interface PromoteExtractionRequestDto {
  class_ids: number[];
  record_ids: number[];
  relationship_ids: number[];
  edge_ids: number[];
  approve_by_status?: string[];

}

export interface ExtractionResponseDto {
  id: number;
  properties: string | null;
  created_by: number | null;
  class_count: number;
  relationship_count: number;
  record_count: number;
  edge_count: number;
}

export interface RejectExtractionRequestDto {
  class_ids: number[];
  record_ids: number[];
  relationship_ids: number[];
  edge_ids: number[];
  reject_by_status: string[] | null;
  reject_all_remaining: boolean | null;
}
