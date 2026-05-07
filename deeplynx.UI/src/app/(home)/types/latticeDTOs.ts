export interface StagedClassDTO {
  id: number;
  name: string;
  validation_status: string | null;
  ontology_class_id: number | null;
  promoted_id: number | null;
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
}

export interface StagedRelationshipDTO {
  id: number;
  name: string;
  origin_class_name: string | null;
  destination_class_name: string | null;
  validation_status: string | null;
  ontology_relationship_id: number | null;
  promoted_id: number | null;
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
}

export interface ExtractionListItemDTO {
  id: number;
  status: string;
  mode: string | null;
  created_by: number | null;
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
