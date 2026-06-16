import {
  QueryRecordViewResponseDto,
  RecordResponseDto,
} from "../../types/responseDTOs";

export type CollectionSortOption =
  | "updatedDesc"
  | "updatedAsc"
  | "alphabeticalAsc"
  | "alphabeticalDesc"
  | "recordCountDesc"
  | "recordCountAsc";

export type MetadataRow = {
  label: string;
  value: string;
};

export type FacetOption = {
  id?: number;
  label: string;
  count: number;
};

export type NewCollectionSelectedRecord = QueryRecordViewResponseDto & {
  fullRecord?: RecordResponseDto;
};

export type PendingRecordChanges = {
  added: number[];
  removed: number[];
};
