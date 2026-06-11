import React from "react";
import {
  HistoricalRecordResponseDto,
  RecordCollectionLabelDto,
  RecordCollectionResponseDto,
  RecordCollectionTagDto,
  RecordResponseDto,
  SensitivityLabelsDto,
  TagResponseDto,
} from "../../types/responseDTOs";
import {
  FacetOption,
  MetadataRow,
  NewCollectionSelectedRecord,
} from "./recordCollections.types";

type StateSetter<T> = React.Dispatch<React.SetStateAction<T>>;

export type NewCollectionStep = "Records" | "Metadata" | "Modify" | "Review";
export type EditableRecordResult = HistoricalRecordResponseDto | RecordResponseDto;

export type NewCollectionTabController = {
  workflow: {
    projectId: number;
    newCollectionStep: NewCollectionStep;
    setNewCollectionStep: StateSetter<NewCollectionStep>;
    recordsPerPage: number;
    setRecordsPerPage: (pageSize: number) => void;
    recordPageSizeOptions: number[];
    saving: boolean;
    getSensitivityClass: (label: string) => string;
  };
  metadata: {
    newCollectionName: string;
    setNewCollectionName: StateSetter<string>;
    newCollectionDescription: string;
    setNewCollectionDescription: StateSetter<string>;
  };
  recordSearch: {
    newCollectionRecordSearchTerm: string;
    setNewCollectionRecordSearchTerm: StateSetter<string>;
    newCollectionRecordSearchResults: HistoricalRecordResponseDto[];
    newCollectionRecordSearchLoading: boolean;
    visibleNewCollectionRecords: HistoricalRecordResponseDto[];
    allVisibleNewCollectionRecordsSelected: boolean;
    allRetrievedNewCollectionRecordsSelected: boolean;
    someVisibleNewCollectionRecordsSelected: boolean;
    newCollectionRecordPage: number;
    setNewCollectionRecordPage: StateSetter<number>;
    newCollectionRecordPageCount: number;
    onSearchRecords: (overrideTerm?: string) => Promise<void>;
    onClearRecordSearch: () => void;
    onToggleSelectAllVisibleRecords: () => Promise<void>;
    onToggleNewCollectionRecord: (record: HistoricalRecordResponseDto) => Promise<void>;
    onSelectAllSearchedRecords: () => Promise<void>;
  };
  selection: {
    newCollectionSelectedRecordIds: number[];
    newCollectionSelectedRecords: NewCollectionSelectedRecord[];
    confirmClearNewCollectionRecords: boolean;
    setConfirmClearNewCollectionRecords: StateSetter<boolean>;
    newCollectionSelectedLabelTally: FacetOption[];
    newCollectionSelectedTagTally: FacetOption[];
    onDeselectRecordsByLabel: (labelName: string) => void;
    onDeselectRecordsByTag: (tagName: string) => void;
    onClearSelectedRecords: () => void;
  };
  review: {
    newCollectionReviewSearchTerm: string;
    setNewCollectionReviewSearchTerm: StateSetter<string>;
    filteredNewCollectionSelectedRecords: NewCollectionSelectedRecord[];
    visibleNewCollectionReviewRecords: NewCollectionSelectedRecord[];
    newCollectionReviewPage: number;
    setNewCollectionReviewPage: StateSetter<number>;
    newCollectionReviewPageCount: number;
  };
  labelsAndTags: {
    selectedNewCollectionLabels: SensitivityLabelsDto[];
    newCollectionSelectedTagNames: string[];
    newCollectionLabelSearchTerm: string;
    setNewCollectionLabelSearchTerm: StateSetter<string>;
    newCollectionTagSearchTerm: string;
    setNewCollectionTagSearchTerm: StateSetter<string>;
    filteredNewCollectionLabelOptions: SensitivityLabelsDto[];
    filteredNewCollectionTagOptions: TagResponseDto[];
    labelsLoading: boolean;
    tagsLoading: boolean;
    newCollectionLabelCreating: boolean;
    canAddTypedNewCollectionLabel: boolean;
    canAddTypedNewCollectionTag: boolean;
    onAddNewCollectionLabelFromSearch: () => Promise<void>;
    onAddNewCollectionLabel: (labelId: number) => void;
    onRemoveNewCollectionLabel: (labelId: number) => void;
    onAddNewCollectionTag: (tagName: string) => void;
    onRemoveNewCollectionTag: (tagName: string) => void;
  };
  actions: {
    onCancelToAllCollections: () => void;
    onGoToModifyStep: () => void;
    onGoToReviewStep: () => void;
    onCreateCollection: () => Promise<void>;
  };
};

export type SelectedCollectionDetailsController = {
  readonlyView: {
    selectedCollection: RecordCollectionResponseDto;
    collectionSummaryPanel: React.ReactNode;
    selectedDescriptionRef: React.RefObject<HTMLParagraphElement | null>;
    selectedDescriptionExpanded: boolean;
    selectedDescriptionExpandable: boolean;
    setSelectedDescriptionExpanded: StateSetter<boolean>;
    selectedCollectionLabels: RecordCollectionLabelDto[];
    visibleSelectedCollectionLabels: RecordCollectionLabelDto[];
    selectedLabelsExpanded: boolean;
    setSelectedLabelsExpanded: StateSetter<boolean>;
    selectedCollectionTags: RecordCollectionTagDto[];
    visibleSelectedCollectionTags: RecordCollectionTagDto[];
    selectedTagsExpanded: boolean;
    setSelectedTagsExpanded: StateSetter<boolean>;
    badgeDisplayLimit: number;
    getMetadataRows: (properties?: string | null) => MetadataRow[];
    getSensitivityClass: (label: string) => string;
    collectionRecords: RecordResponseDto[];
    filteredCollectionDetailRecords: RecordResponseDto[];
    collectionDetailRecordSearchTerm: string;
    setCollectionDetailRecordSearchTerm: StateSetter<string>;
    visibleCollectionDetailRecords: RecordResponseDto[];
    recordsLoading: boolean;
    onViewAllCollectionRecords: () => void;
    recordsPerPage: number;
    collectionDetailRecordPage: number;
    setCollectionDetailRecordPage: StateSetter<number>;
    collectionDetailRecordPageCount: number;
    projectId: number;
    onOpenSelectedCollectionEdit: () => void;
  };
  editView: {
    editableSelectedCollection: RecordCollectionResponseDto;
    isEditingSelectedCollection: boolean;
    saving: boolean;
    setSelectedCollectionDraft: StateSetter<RecordCollectionResponseDto | null>;
    setSelectedCollectionPropertiesEditorOpen: StateSetter<boolean>;
    selectedCollectionLabelSearchTerm: string;
    setSelectedCollectionLabelSearchTerm: StateSetter<string>;
    selectedCollectionTagSearchTerm: string;
    setSelectedCollectionTagSearchTerm: StateSetter<string>;
    selectedCollectionLabelCreating: boolean;
    selectedCollectionTagCreating: boolean;
    canAddTypedSelectedCollectionLabel: boolean;
    canAddTypedSelectedCollectionTag: boolean;
    filteredSelectedCollectionLabelOptions: SensitivityLabelsDto[];
    filteredSelectedCollectionTagOptions: TagResponseDto[];
    labelsLoading: boolean;
    tagsLoading: boolean;
    onAddSelectedCollectionLabelFromSearch: () => Promise<void>;
    onAddSelectedCollectionTagFromSearch: () => Promise<void>;
    onAddSelectedCollectionLabel: (label: { id: number; name: string }) => void;
    onAddSelectedCollectionTag: (tag: { id: number; name: string }) => void;
    onRemoveLabel: (labelId: number) => void;
    onRemoveTag: (tagId: number) => void;
    recordSearchTerm: string;
    setRecordSearchTerm: StateSetter<string>;
    recordSearchLoading: boolean;
    onSearchRecords: () => void;
    editRecordResults: EditableRecordResult[];
    collectionRecordIds: Set<number>;
    addingRecordIds: number[];
    removingRecordIds: number[];
    onRemoveCollectionRecord: (recordId: number) => Promise<void>;
    onAddCollectionRecord: (recordId: number) => Promise<void>;
    onCancelSelectedCollectionEdit: () => Promise<void>;
    onSaveSelectedDetails: () => void;
  };
};

export type SelectedCollectionRecordsController = {
  overview: {
    selectedCollection: RecordCollectionResponseDto;
    projectId: number;
    collectionRecords: RecordResponseDto[];
    recordsLoading: boolean;
  };
  search: {
    recordSearchTerm: string;
    setRecordSearchTerm: StateSetter<string>;
    recordSearchLoading: boolean;
    recordSearchResults: HistoricalRecordResponseDto[];
    addableRecordResults: HistoricalRecordResponseDto[];
    onSearchRecords: () => void;
  };
  selection: {
    saving: boolean;
    selectedRecordIds: number[];
    onToggleSelectedRecord: (recordId: number) => void;
    onAddSelectedRecords: () => void;
  };
  actions: {
    onBackToDetails: () => void;
  };
};
