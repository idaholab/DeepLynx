"use client";

import { Dispatch, SetStateAction, useEffect, useMemo } from "react";
import {
  SensitivityLabelsDto,
  TagResponseDto,
} from "../../types/responseDTOs";
import {
  countFacet,
} from "../components/utils";
import {
  deriveSelectedRecordMetadata,
  getSelectedRecordLabelNames,
  getSelectedRecordTagNames,
} from "../components/recordCollections.utils";
import { NewCollectionSelectedRecord } from "../components/recordCollections.types";

type Params = {
  availableLabels: SensitivityLabelsDto[];
  availableTags: TagResponseDto[];
  newCollectionSelectedLabelIds: number[];
  newCollectionSelectedTagNames: string[];
  newCollectionLabelSearchTerm: string;
  newCollectionTagSearchTerm: string;
  newCollectionRecordSearchResults: NewCollectionSelectedRecord[];
  newCollectionRecordPage: number;
  newCollectionSelectedRecordIds: number[];
  newCollectionSelectedRecords: NewCollectionSelectedRecord[];
  newCollectionReviewSearchTerm: string;
  newCollectionReviewPage: number;
  recordsPerPage: number;
  setNewCollectionReviewPage: Dispatch<SetStateAction<number>>;
};

export function useNewCollectionDerived({
  availableLabels,
  availableTags,
  newCollectionSelectedLabelIds,
  newCollectionSelectedTagNames,
  newCollectionLabelSearchTerm,
  newCollectionTagSearchTerm,
  newCollectionRecordSearchResults,
  newCollectionRecordPage,
  newCollectionSelectedRecordIds,
  newCollectionSelectedRecords,
  newCollectionReviewSearchTerm,
  newCollectionReviewPage,
  recordsPerPage,
  setNewCollectionReviewPage,
}: Params) {
  const newCollectionSelectedLabelTally = useMemo(
    () =>
      countFacet(
        newCollectionSelectedRecords.flatMap((record) =>
          getSelectedRecordLabelNames(record),
        ),
      ),
    [newCollectionSelectedRecords],
  );

  const newCollectionSelectedTagTally = useMemo(
    () =>
      countFacet(
        newCollectionSelectedRecords.flatMap((record) =>
          getSelectedRecordTagNames(record),
        ),
      ),
    [newCollectionSelectedRecords],
  );

  const selectedNewCollectionLabels = useMemo(
    () =>
      availableLabels.filter((label) =>
        newCollectionSelectedLabelIds.includes(label.id),
      ),
    [availableLabels, newCollectionSelectedLabelIds],
  );

  const filteredNewCollectionLabelOptions = useMemo(() => {
    const query = newCollectionLabelSearchTerm.trim().toLowerCase();
    return availableLabels
      .filter((label) => !newCollectionSelectedLabelIds.includes(label.id))
      .filter((label) => label.name.toLowerCase().includes(query));
  }, [
    availableLabels,
    newCollectionLabelSearchTerm,
    newCollectionSelectedLabelIds,
  ]);

  const filteredNewCollectionTagOptions = useMemo(() => {
    const query = newCollectionTagSearchTerm.trim().toLowerCase();
    return availableTags
      .filter(
        (tag) =>
          !newCollectionSelectedTagNames.some(
            (name) => name.toLowerCase() === tag.name.toLowerCase(),
          ),
      )
      .filter((tag) => tag.name.toLowerCase().includes(query));
  }, [availableTags, newCollectionSelectedTagNames, newCollectionTagSearchTerm]);

  const canAddTypedNewCollectionTag =
    newCollectionTagSearchTerm.trim().length > 0 &&
    !newCollectionSelectedTagNames.some(
      (name) => name.toLowerCase() === newCollectionTagSearchTerm.trim().toLowerCase(),
    );

  const canAddTypedNewCollectionLabel =
    newCollectionLabelSearchTerm.trim().length > 0 &&
    !selectedNewCollectionLabels.some(
      (label) =>
        label.name.toLowerCase() === newCollectionLabelSearchTerm.trim().toLowerCase(),
    );

  const newCollectionRecordPageCount = Math.max(
    1,
    Math.ceil(newCollectionRecordSearchResults.length / recordsPerPage),
  );

  const visibleNewCollectionRecords = useMemo(() => {
    const startIndex = (newCollectionRecordPage - 1) * recordsPerPage;
    return newCollectionRecordSearchResults.slice(startIndex, startIndex + recordsPerPage);
  }, [newCollectionRecordPage, newCollectionRecordSearchResults, recordsPerPage]);

  const visibleNewCollectionRecordIds = useMemo(
    () => visibleNewCollectionRecords.map((record) => record.id),
    [visibleNewCollectionRecords],
  );

  const allVisibleNewCollectionRecordsSelected =
    visibleNewCollectionRecordIds.length > 0 &&
    visibleNewCollectionRecordIds.every((id) =>
      newCollectionSelectedRecordIds.includes(id),
    );

  const allRetrievedNewCollectionRecordsSelected =
    newCollectionRecordSearchResults.length > 0 &&
    newCollectionRecordSearchResults.every((record) =>
      newCollectionSelectedRecordIds.includes(record.id),
    );

  const someVisibleNewCollectionRecordsSelected = visibleNewCollectionRecordIds.some(
    (id) => newCollectionSelectedRecordIds.includes(id),
  );

  const filteredNewCollectionSelectedRecords = useMemo(() => {
    const query = newCollectionReviewSearchTerm.trim().toLowerCase();
    if (!query) return newCollectionSelectedRecords;

    return newCollectionSelectedRecords.filter((record) => {
      const haystack = [
        record.name,
        record.description,
        record.className,
        record.dataSourceName,
        record.projectName,
        getSelectedRecordLabelNames(record).join(" "),
        getSelectedRecordTagNames(record).join(" "),
      ]
        .join(" ")
        .toLowerCase();

      return haystack.includes(query);
    });
  }, [newCollectionReviewSearchTerm, newCollectionSelectedRecords]);

  const newCollectionReviewPageCount = Math.max(
    1,
    Math.ceil(filteredNewCollectionSelectedRecords.length / recordsPerPage),
  );

  const visibleNewCollectionReviewRecords = useMemo(() => {
    const startIndex = (newCollectionReviewPage - 1) * recordsPerPage;
    return filteredNewCollectionSelectedRecords.slice(startIndex, startIndex + recordsPerPage);
  }, [filteredNewCollectionSelectedRecords, newCollectionReviewPage, recordsPerPage]);

  useEffect(() => {
    setNewCollectionReviewPage(1);
  }, [
    newCollectionReviewSearchTerm,
    newCollectionSelectedRecords.length,
    setNewCollectionReviewPage,
  ]);

  const selectedRecordMetadata = useMemo(
    () =>
      deriveSelectedRecordMetadata({
        availableLabels,
        selectedRecords: newCollectionSelectedRecords,
        selectedRecordLabelTally: newCollectionSelectedLabelTally,
        selectedRecordTagTally: newCollectionSelectedTagTally,
      }),
    [
      availableLabels,
      newCollectionSelectedLabelTally,
      newCollectionSelectedRecords,
      newCollectionSelectedTagTally,
    ],
  );

  return {
    newCollectionSelectedLabelTally,
    newCollectionSelectedTagTally,
    selectedNewCollectionLabels,
    filteredNewCollectionLabelOptions,
    filteredNewCollectionTagOptions,
    canAddTypedNewCollectionTag,
    canAddTypedNewCollectionLabel,
    newCollectionRecordPageCount,
    visibleNewCollectionRecords,
    visibleNewCollectionRecordIds,
    allVisibleNewCollectionRecordsSelected,
    allRetrievedNewCollectionRecordsSelected,
    someVisibleNewCollectionRecordsSelected,
    filteredNewCollectionSelectedRecords,
    newCollectionReviewPageCount,
    visibleNewCollectionReviewRecords,
    selectedRecordMetadata,
  };
}
