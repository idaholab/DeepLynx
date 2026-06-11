"use client";

import { useMemo } from "react";
import {
  HistoricalRecordResponseDto,
  RecordCollectionResponseDto,
  RecordResponseDto,
  SensitivityLabelsDto,
  TagResponseDto,
} from "../../types/responseDTOs";

type Params = {
  selectedCollectionDraft: RecordCollectionResponseDto | null;
  availableLabels: SensitivityLabelsDto[];
  availableTags: TagResponseDto[];
  selectedCollectionLabelSearchTerm: string;
  selectedCollectionTagSearchTerm: string;
  recordSearchTerm: string;
  recordSearchResults: HistoricalRecordResponseDto[];
  collectionRecords: RecordResponseDto[];
};

export function useSelectedCollectionEditDerived({
  selectedCollectionDraft,
  availableLabels,
  availableTags,
  selectedCollectionLabelSearchTerm,
  selectedCollectionTagSearchTerm,
  recordSearchTerm,
  recordSearchResults,
  collectionRecords,
}: Params) {
  const unattachedLabels = useMemo(() => {
    const attachedIds = new Set(
      selectedCollectionDraft?.labels?.map((label) => label.id) ?? [],
    );
    return availableLabels.filter((label) => !attachedIds.has(label.id));
  }, [availableLabels, selectedCollectionDraft?.labels]);

  const unattachedTags = useMemo(() => {
    const attachedIds = new Set(selectedCollectionDraft?.tags?.map((tag) => tag.id) ?? []);
    return availableTags.filter((tag) => !attachedIds.has(tag.id));
  }, [availableTags, selectedCollectionDraft?.tags]);

  const filteredSelectedCollectionLabelOptions = useMemo(() => {
    const query = selectedCollectionLabelSearchTerm.trim().toLowerCase();
    return unattachedLabels.filter((label) =>
      label.name.toLowerCase().includes(query),
    );
  }, [selectedCollectionLabelSearchTerm, unattachedLabels]);

  const filteredSelectedCollectionTagOptions = useMemo(() => {
    const query = selectedCollectionTagSearchTerm.trim().toLowerCase();
    return unattachedTags.filter((tag) => tag.name.toLowerCase().includes(query));
  }, [selectedCollectionTagSearchTerm, unattachedTags]);

  const canAddTypedSelectedCollectionLabel =
    selectedCollectionLabelSearchTerm.trim().length > 0 &&
    !(selectedCollectionDraft?.labels ?? []).some(
      (label) =>
        label.name.toLowerCase() ===
        selectedCollectionLabelSearchTerm.trim().toLowerCase(),
    );

  const canAddTypedSelectedCollectionTag =
    selectedCollectionTagSearchTerm.trim().length > 0 &&
    !(selectedCollectionDraft?.tags ?? []).some(
      (tag) =>
        tag.name.toLowerCase() === selectedCollectionTagSearchTerm.trim().toLowerCase(),
    );

  const addableRecordResults = useMemo(() => {
    const existingIds = new Set(
      collectionRecords
        .map((record) => record.id)
        .filter((id): id is number => typeof id === "number"),
    );
    return recordSearchResults.filter((record) => !existingIds.has(record.id));
  }, [collectionRecords, recordSearchResults]);

  const collectionRecordIds = useMemo(
    () =>
      new Set(
        collectionRecords
          .map((record) => record.id)
          .filter((id): id is number => typeof id === "number"),
      ),
    [collectionRecords],
  );

  const editRecordResults = useMemo(
    () => (recordSearchTerm.trim().length ? recordSearchResults : collectionRecords),
    [collectionRecords, recordSearchResults, recordSearchTerm],
  );

  return {
    filteredSelectedCollectionLabelOptions,
    filteredSelectedCollectionTagOptions,
    canAddTypedSelectedCollectionLabel,
    canAddTypedSelectedCollectionTag,
    addableRecordResults,
    collectionRecordIds,
    editRecordResults,
  };
}

export type SelectedCollectionEditDerivedState = ReturnType<
  typeof useSelectedCollectionEditDerived
>;
