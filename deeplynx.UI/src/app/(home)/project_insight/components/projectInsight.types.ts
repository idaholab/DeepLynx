"use client";

export type NamedInsightOption = {
  id: number;
  name: string;
};

export type ProjectInsightRecord = {
  id: number;
  name: string;
  description: string;
  uri: string | null;
  fileType: string | null;
  fileSize: number | null;
  classId: number | null;
  className: string;
  dataSourceId: number | null;
  dataSourceName: string;
  tags: NamedInsightOption[];
  labels: NamedInsightOption[];
  lastUpdatedAt: string | null;
  isArchived: boolean;
  isInsightSupported: boolean;
};

export type ProjectInsightRecordState =
  | "checking"
  | "embedded"
  | "not_embedded"
  | "queued"
  | "processing"
  | "unsupported"
  | "error";

export type ProjectInsightStatus = {
  state: ProjectInsightRecordState;
  chunkCount?: number;
  pageCount?: number;
  error?: string;
};

export type ProjectInsightFiltersState = {
  classIds: number[];
  tagIds: number[];
};
