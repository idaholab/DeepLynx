"use client";

import { useLanguage } from "@/app/contexts/Language";
import React from "react";
import { formatLocalDateTime } from "@/app/lib/date_time";
import {
  RecordCollectionResponseDto,
  RecordResponseDto,
} from "../../types/responseDTOs";
import { SelectedCollectionDetailsController } from "../components/componentTypes";
import {
  COLLECTION_BADGE_DISPLAY_LIMIT,
  NEW_COLLECTION_RECORDS_PER_PAGE,
} from "../components/recordCollections.constants";
import { MetadataRow } from "../components/recordCollections.types";
import { SelectedCollectionDetailsViewState } from "./useSelectedCollectionDetailsView";
import { SelectedCollectionEditDerivedState } from "./useSelectedCollectionEditDerived";

type Params = {
  projectId: number;
  selectedCollection: RecordCollectionResponseDto | null;
  collectionRecords: RecordResponseDto[];
  collectionDetailsView: SelectedCollectionDetailsViewState;
  selectedCollectionEditDerived: SelectedCollectionEditDerivedState;
  recordsLoading: boolean;
  saving: boolean;
  isEditingSelectedCollection: boolean;
  setSelectedCollectionDraft: React.Dispatch<
    React.SetStateAction<RecordCollectionResponseDto | null>
  >;
  setSelectedCollectionPropertiesEditorOpen: React.Dispatch<
    React.SetStateAction<boolean>
  >;
  selectedCollectionLabelSearchTerm: string;
  setSelectedCollectionLabelSearchTerm: React.Dispatch<
    React.SetStateAction<string>
  >;
  selectedCollectionTagSearchTerm: string;
  setSelectedCollectionTagSearchTerm: React.Dispatch<React.SetStateAction<string>>;
  selectedCollectionLabelCreating: boolean;
  selectedCollectionTagCreating: boolean;
  labelsLoading: boolean;
  tagsLoading: boolean;
  onAddSelectedCollectionLabelFromSearch: () => Promise<void>;
  onAddSelectedCollectionTagFromSearch: () => Promise<void>;
  onAddSelectedCollectionLabel: (label: { id: number; name: string }) => void;
  onAddSelectedCollectionTag: (tag: { id: number; name: string }) => void;
  onRemoveLabel: (labelId: number) => void;
  onRemoveTag: (tagId: number) => void;
  recordSearchTerm: string;
  setRecordSearchTerm: React.Dispatch<React.SetStateAction<string>>;
  recordSearchLoading: boolean;
  onSearchRecords: () => void;
  addingRecordIds: number[];
  removingRecordIds: number[];
  onRemoveCollectionRecord: (recordId: number) => Promise<void>;
  onAddCollectionRecord: (recordId: number) => Promise<void>;
  onCancelSelectedCollectionEdit: () => Promise<void>;
  onSaveSelectedDetails: () => void;
  onViewAllCollectionRecords: () => void;
  onOpenSelectedCollectionEdit: () => void;
  getMetadataRows: (properties?: string | null) => MetadataRow[];
  getSensitivityClass: (label: string) => string;
};

export function useSelectedCollectionDetailsController({
  projectId,
  selectedCollection,
  collectionRecords,
  collectionDetailsView,
  selectedCollectionEditDerived,
  recordsLoading,
  saving,
  isEditingSelectedCollection,
  setSelectedCollectionDraft,
  setSelectedCollectionPropertiesEditorOpen,
  selectedCollectionLabelSearchTerm,
  setSelectedCollectionLabelSearchTerm,
  selectedCollectionTagSearchTerm,
  setSelectedCollectionTagSearchTerm,
  selectedCollectionLabelCreating,
  selectedCollectionTagCreating,
  labelsLoading,
  tagsLoading,
  onAddSelectedCollectionLabelFromSearch,
  onAddSelectedCollectionTagFromSearch,
  onAddSelectedCollectionLabel,
  onAddSelectedCollectionTag,
  onRemoveLabel,
  onRemoveTag,
  recordSearchTerm,
  setRecordSearchTerm,
  recordSearchLoading,
  onSearchRecords,
  addingRecordIds,
  removingRecordIds,
  onRemoveCollectionRecord,
  onAddCollectionRecord,
  onCancelSelectedCollectionEdit,
  onSaveSelectedDetails,
  onViewAllCollectionRecords,
  onOpenSelectedCollectionEdit,
  getMetadataRows,
  getSensitivityClass,
}: Params): SelectedCollectionDetailsController | null {
  const {
    selectedDescriptionRef,
    selectedDescriptionExpandable,
    selectedDescriptionExpanded,
    setSelectedDescriptionExpanded,
    selectedLabelsExpanded,
    setSelectedLabelsExpanded,
    selectedTagsExpanded,
    setSelectedTagsExpanded,
    collectionDetailRecordSearchTerm,
    setCollectionDetailRecordSearchTerm,
    collectionDetailRecordPage,
    setCollectionDetailRecordPage,
    collectionDetailRecordPageCount,
    filteredCollectionDetailRecords,
    visibleCollectionDetailRecords,
    editableSelectedCollection,
    selectedCollectionLabels,
    selectedCollectionTags,
    visibleSelectedCollectionLabels,
    visibleSelectedCollectionTags,
  } = collectionDetailsView;

  const {
    filteredSelectedCollectionLabelOptions,
    filteredSelectedCollectionTagOptions,
    canAddTypedSelectedCollectionLabel,
    canAddTypedSelectedCollectionTag,
    collectionRecordIds,
    editRecordResults,
  } = selectedCollectionEditDerived;
  const { t } = useLanguage();

  if (!selectedCollection || !editableSelectedCollection) {
    return null;
  }

  return {
    readonlyView: {
      selectedCollection,
      collectionSummaryPanel: (
        <div className="grid gap-4 rounded-2xl border border-base-300 bg-base-200/30 p-4 text-sm sm:grid-cols-2 lg:grid-cols-4">
          <div>
            <p className="text-base-content/60">
              {t.translations.RECORD_COLLECTIONS_COLLECTION_ID}
            </p>
            <p className="font-semibold text-base-content">{selectedCollection.id}</p>
          </div>
          <div>
            <p className="text-base-content/60">
              {t.translations.RECORD_COLLECTIONS_TOTAL_RECORDS}
            </p>
            <p className="font-semibold text-base-content">
              {selectedCollection.recordCount}
            </p>
          </div>
          <div>
            <p className="text-base-content/60">
              {t.translations.RECORD_COLLECTIONS_UPDATED}
            </p>
            <p className="font-semibold text-base-content">
              {formatLocalDateTime(selectedCollection.lastUpdatedAt)}
            </p>
          </div>
          <div>
            <p className="text-base-content/60">
              {t.translations.RECORD_COLLECTIONS_LAST_UPDATED_BY}
            </p>
            <p className="font-semibold text-base-content">
              {selectedCollection.lastUpdatedBy ?? t.translations.UNKNOWN}
            </p>
          </div>
        </div>
      ),
      selectedDescriptionRef,
      selectedDescriptionExpanded,
      selectedDescriptionExpandable,
      setSelectedDescriptionExpanded,
      selectedCollectionLabels,
      visibleSelectedCollectionLabels,
      selectedLabelsExpanded,
      setSelectedLabelsExpanded,
      selectedCollectionTags,
      visibleSelectedCollectionTags,
      selectedTagsExpanded,
      setSelectedTagsExpanded,
      badgeDisplayLimit: COLLECTION_BADGE_DISPLAY_LIMIT,
      getMetadataRows,
      getSensitivityClass,
      collectionRecords,
      filteredCollectionDetailRecords,
      collectionDetailRecordSearchTerm,
      setCollectionDetailRecordSearchTerm,
      visibleCollectionDetailRecords,
      recordsLoading,
      onViewAllCollectionRecords,
      recordsPerPage: NEW_COLLECTION_RECORDS_PER_PAGE,
      collectionDetailRecordPage,
      setCollectionDetailRecordPage,
      collectionDetailRecordPageCount,
      projectId,
      onOpenSelectedCollectionEdit,
    },
    editView: {
      editableSelectedCollection,
      isEditingSelectedCollection,
      saving,
      setSelectedCollectionDraft,
      setSelectedCollectionPropertiesEditorOpen,
      selectedCollectionLabelSearchTerm,
      setSelectedCollectionLabelSearchTerm,
      selectedCollectionTagSearchTerm,
      setSelectedCollectionTagSearchTerm,
      selectedCollectionLabelCreating,
      selectedCollectionTagCreating,
      canAddTypedSelectedCollectionLabel,
      canAddTypedSelectedCollectionTag,
      filteredSelectedCollectionLabelOptions,
      filteredSelectedCollectionTagOptions,
      labelsLoading,
      tagsLoading,
      onAddSelectedCollectionLabelFromSearch,
      onAddSelectedCollectionTagFromSearch,
      onAddSelectedCollectionLabel,
      onAddSelectedCollectionTag,
      onRemoveLabel,
      onRemoveTag,
      recordSearchTerm,
      setRecordSearchTerm,
      recordSearchLoading,
      onSearchRecords,
      editRecordResults,
      collectionRecordIds,
      addingRecordIds,
      removingRecordIds,
      onRemoveCollectionRecord,
      onAddCollectionRecord,
      onCancelSelectedCollectionEdit,
      onSaveSelectedDetails,
    },
  };
}
