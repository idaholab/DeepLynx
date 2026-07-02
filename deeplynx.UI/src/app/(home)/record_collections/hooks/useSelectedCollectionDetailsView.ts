"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  RecordCollectionResponseDto,
  RecordResponseDto,
} from "../../types/responseDTOs";

type Params = {
  selectedCollection: RecordCollectionResponseDto | null;
  selectedCollectionDraft: RecordCollectionResponseDto | null;
  collectionRecords: RecordResponseDto[];
  badgeDisplayLimit: number;
  recordsPerPage: number;
};

export function useSelectedCollectionDetailsView({
  selectedCollection,
  selectedCollectionDraft,
  collectionRecords,
  badgeDisplayLimit,
  recordsPerPage,
}: Params) {
  const [selectedDescriptionExpandable, setSelectedDescriptionExpandable] =
    useState(false);
  const [selectedDescriptionExpanded, setSelectedDescriptionExpanded] =
    useState(false);
  const [selectedLabelsExpanded, setSelectedLabelsExpanded] = useState(false);
  const [selectedTagsExpanded, setSelectedTagsExpanded] = useState(false);
  const [collectionDetailRecordSearchTerm, setCollectionDetailRecordSearchTerm] =
    useState("");
  const [collectionDetailRecordPage, setCollectionDetailRecordPage] = useState(1);
  const selectedDescriptionRef = useRef<HTMLParagraphElement | null>(null);

  useEffect(() => {
    const measureDescriptionOverflow = () => {
      const selectedDescriptionElement = selectedDescriptionRef.current;
      const nextSelectedExpandable = Boolean(
        selectedDescriptionElement &&
        selectedDescriptionElement.scrollHeight - selectedDescriptionElement.clientHeight > 1,
      );
      setSelectedDescriptionExpandable((current) =>
        current === nextSelectedExpandable ? current : nextSelectedExpandable,
      );
    };

    const frameId = window.requestAnimationFrame(measureDescriptionOverflow);
    window.addEventListener("resize", measureDescriptionOverflow);

    return () => {
      window.cancelAnimationFrame(frameId);
      window.removeEventListener("resize", measureDescriptionOverflow);
    };
  }, [selectedCollection?.id, selectedCollection?.description]);

  const filteredCollectionDetailRecords = useMemo(() => {
    const query = collectionDetailRecordSearchTerm.trim().toLowerCase();
    if (!query) return collectionRecords;

    return collectionRecords.filter((record) => {
      const haystack = [
        record.name,
        record.description,
        record.uri,
        record.classId,
        record.dataSourceId,
        record.projectId,
        record.lastUpdatedBy,
      ]
        .join(" ")
        .toLowerCase();

      return haystack.includes(query);
    });
  }, [collectionDetailRecordSearchTerm, collectionRecords]);

  const collectionDetailRecordPageCount = Math.max(
    1,
    Math.ceil(filteredCollectionDetailRecords.length / recordsPerPage),
  );

  const visibleCollectionDetailRecords = useMemo(() => {
    const startIndex = (collectionDetailRecordPage - 1) * recordsPerPage;
    return filteredCollectionDetailRecords.slice(startIndex, startIndex + recordsPerPage);
  }, [collectionDetailRecordPage, filteredCollectionDetailRecords, recordsPerPage]);

  useEffect(() => {
    setCollectionDetailRecordPage(1);
  }, [collectionDetailRecordSearchTerm, collectionRecords.length]);

  const editableSelectedCollection = selectedCollectionDraft ?? selectedCollection;
  const selectedCollectionLabels = selectedCollection?.labels ?? [];
  const selectedCollectionTags = selectedCollection?.tags ?? [];
  const visibleSelectedCollectionLabels = selectedLabelsExpanded
    ? selectedCollectionLabels
    : selectedCollectionLabels.slice(0, badgeDisplayLimit);
  const visibleSelectedCollectionTags = selectedTagsExpanded
    ? selectedCollectionTags
    : selectedCollectionTags.slice(0, badgeDisplayLimit);

  const resetCollectionDetailsView = useCallback(() => {
    setSelectedDescriptionExpanded(false);
    setSelectedLabelsExpanded(false);
    setSelectedTagsExpanded(false);
    setCollectionDetailRecordSearchTerm("");
    setCollectionDetailRecordPage(1);
  }, []);

  return {
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
    resetCollectionDetailsView,
  };
}

export type SelectedCollectionDetailsViewState = ReturnType<
  typeof useSelectedCollectionDetailsView
>;
