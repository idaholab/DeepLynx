import {
  HistoricalRecordResponseDto,
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
  label: string;
  count: number;
};

export type NewCollectionSelectedRecord = HistoricalRecordResponseDto & {
  fullRecord?: RecordResponseDto;
};

export type PendingRecordChanges = {
  added: number[];
  removed: number[];
};
