"use client";

import React from "react";
import {
  MagnifyingGlassIcon,
  XCircleIcon,
} from "@heroicons/react/24/outline";
import CollectionDetailsReadonlyView from "./CollectionDetailsReadonlyView";
import SectionCard from "./SectionCard";
import {
  HistoricalRecordResponseDto,
  RecordCollectionLabelDto,
  RecordCollectionResponseDto,
  RecordCollectionTagDto,
  RecordResponseDto,
  TagResponseDto,
  SensitivityLabelsDto,
} from "../../types/responseDTOs";
import { formatLocalDateTime } from "@/app/lib/date_time";
type MetadataRow = {
  label: string;
  value: string;
};

type EditableRecordResult = HistoricalRecordResponseDto | RecordResponseDto;

type Props = {
  controller: {
    readonlyView: {
      selectedCollection: RecordCollectionResponseDto;
      collectionSummaryPanel: React.ReactNode;
      selectedDescriptionRef: React.RefObject<HTMLParagraphElement | null>;
      selectedDescriptionExpanded: boolean;
      selectedDescriptionExpandable: boolean;
      setSelectedDescriptionExpanded: React.Dispatch<React.SetStateAction<boolean>>;
      selectedCollectionLabels: RecordCollectionLabelDto[];
      visibleSelectedCollectionLabels: RecordCollectionLabelDto[];
      selectedLabelsExpanded: boolean;
      setSelectedLabelsExpanded: React.Dispatch<React.SetStateAction<boolean>>;
      selectedCollectionTags: RecordCollectionTagDto[];
      visibleSelectedCollectionTags: RecordCollectionTagDto[];
      selectedTagsExpanded: boolean;
      setSelectedTagsExpanded: React.Dispatch<React.SetStateAction<boolean>>;
      badgeDisplayLimit: number;
      getMetadataRows: (properties?: string | null) => MetadataRow[];
      getSensitivityClass: (label: string) => string;
      collectionRecords: RecordResponseDto[];
      filteredCollectionDetailRecords: RecordResponseDto[];
      collectionDetailRecordSearchTerm: string;
      setCollectionDetailRecordSearchTerm: React.Dispatch<
        React.SetStateAction<string>
      >;
      visibleCollectionDetailRecords: RecordResponseDto[];
      recordsLoading: boolean;
      onViewAllCollectionRecords: () => void;
      recordsPerPage: number;
      collectionDetailRecordPage: number;
      setCollectionDetailRecordPage: React.Dispatch<React.SetStateAction<number>>;
      collectionDetailRecordPageCount: number;
      projectId: number;
      onOpenSelectedCollectionEdit: () => void;
    };
    editView: {
      editableSelectedCollection: RecordCollectionResponseDto;
      isEditingSelectedCollection: boolean;
      saving: boolean;
      setSelectedCollectionDraft: React.Dispatch<
        React.SetStateAction<RecordCollectionResponseDto | null>
      >;
      setSelectedCollectionPropertiesEditorOpen: React.Dispatch<
        React.SetStateAction<boolean>
      >;
      selectedCollectionLabelSearchTerm: string;
      setSelectedCollectionLabelSearchTerm: React.Dispatch<React.SetStateAction<string>>;
      selectedCollectionTagSearchTerm: string;
      setSelectedCollectionTagSearchTerm: React.Dispatch<React.SetStateAction<string>>;
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
      setRecordSearchTerm: React.Dispatch<React.SetStateAction<string>>;
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
};

export default function SelectedCollectionDetailsTab({
  controller: {
    readonlyView: {
      selectedCollection,
      collectionSummaryPanel,
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
      badgeDisplayLimit,
      getMetadataRows,
      getSensitivityClass,
      collectionRecords,
      filteredCollectionDetailRecords,
      collectionDetailRecordSearchTerm,
      setCollectionDetailRecordSearchTerm,
      visibleCollectionDetailRecords,
      recordsLoading,
      onViewAllCollectionRecords,
      recordsPerPage,
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
  },
}: Props) {
  return (
    <div className="mt-4 space-y-4">
      {collectionSummaryPanel}
      {!isEditingSelectedCollection ? (
        <CollectionDetailsReadonlyView
          summaryPanel={null}
          collection={{
            id: selectedCollection.id,
            name: selectedCollection.name,
            description: selectedCollection.description,
            labels: selectedCollectionLabels,
            tags: selectedCollectionTags,
            properties: selectedCollection.properties,
          }}
          primaryAction={
            <button
              type="button"
              className="btn btn-primary btn-sm"
              onClick={onOpenSelectedCollectionEdit}
            >
              Edit Collection
            </button>
          }
          descriptionRef={selectedDescriptionRef}
          descriptionExpanded={selectedDescriptionExpanded}
          descriptionExpandable={selectedDescriptionExpandable}
          setDescriptionExpanded={setSelectedDescriptionExpanded}
          visibleLabels={visibleSelectedCollectionLabels}
          labelsExpanded={selectedLabelsExpanded}
          setLabelsExpanded={setSelectedLabelsExpanded}
          visibleTags={visibleSelectedCollectionTags}
          tagsExpanded={selectedTagsExpanded}
          setTagsExpanded={setSelectedTagsExpanded}
          badgeDisplayLimit={badgeDisplayLimit}
          getMetadataRows={getMetadataRows}
          getSensitivityClass={getSensitivityClass}
          records={collectionRecords}
          filteredRecords={filteredCollectionDetailRecords}
          visibleRecords={visibleCollectionDetailRecords}
          recordsLoading={recordsLoading}
          recordSearchTerm={collectionDetailRecordSearchTerm}
          setRecordSearchTerm={setCollectionDetailRecordSearchTerm}
          projectId={projectId}
          recordsSectionAction={
            <button
              type="button"
              className="btn btn-primary btn-sm"
              onClick={onViewAllCollectionRecords}
            >
              View all
            </button>
          }
          recordsPerPage={recordsPerPage}
          recordPage={collectionDetailRecordPage}
          setRecordPage={setCollectionDetailRecordPage}
          recordPageCount={collectionDetailRecordPageCount}
        />
      ) : (
        <SectionCard
          title="Manage Collection"
          subtitle="Edit collection identity, labels, and metadata."
          action={
            <div className="flex flex-wrap justify-end gap-2">
              <button
                type="button"
                className="btn btn-outline btn-sm"
                disabled={saving}
                onClick={() => void onCancelSelectedCollectionEdit()}
              >
                Cancel
              </button>
              <button
                type="button"
                className="btn btn-primary btn-sm"
                disabled={saving}
                onClick={onSaveSelectedDetails}
              >
                {saving ? (
                  <span className="loading loading-spinner loading-xs" />
                ) : (
                  "Save Modifications"
                )}
              </button>
            </div>
          }
        >
          <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_380px]">
            <div className="space-y-4">
              <label className="form-control w-full">
                <div className="label">
                  <span className="label-text font-medium">Name</span>
                </div>
                <input
                  type="text"
                  className="input input-bordered w-full"
                  value={editableSelectedCollection.name}
                  onChange={(event) =>
                    setSelectedCollectionDraft({
                      ...editableSelectedCollection,
                      name: event.target.value,
                    })
                  }
                />
              </label>

              <div className="form-control w-full">
                <span className="mb-2 text-sm font-medium text-base-content">
                  Description
                </span>
                <textarea
                  className="textarea textarea-bordered min-h-40 w-full resize-y"
                  value={editableSelectedCollection.description}
                  onChange={(event) =>
                    setSelectedCollectionDraft({
                      ...editableSelectedCollection,
                      description: event.target.value,
                    })
                  }
                />
              </div>

              <div className="rounded-2xl border border-base-300 bg-base-100 p-5">
                <div className="flex items-start justify-between gap-3">
                  <h3 className="font-semibold text-base-content">
                    Additional Properties
                  </h3>
                  <button
                    type="button"
                    className="btn btn-outline btn-sm"
                    disabled={saving}
                    onClick={() => setSelectedCollectionPropertiesEditorOpen(true)}
                  >
                    Edit
                  </button>
                </div>
                <div className="mt-4 max-h-[17.5rem] overflow-auto pr-1">
                  <table className="table table-pin-rows">
                    <thead className="bg-base-100">
                      <tr>
                        <th>Field</th>
                        <th>Value</th>
                      </tr>
                    </thead>
                    <tbody>
                      {getMetadataRows(editableSelectedCollection.properties).length ? (
                        getMetadataRows(editableSelectedCollection.properties).map((row) => (
                          <tr key={row.label} className="h-10">
                            <td className="font-medium">{row.label}</td>
                            <td>{row.value}</td>
                          </tr>
                        ))
                      ) : (
                        <tr>
                          <td colSpan={2}>No additional properties set.</td>
                        </tr>
                      )}
                    </tbody>
                  </table>
                </div>
              </div>
            </div>

            <div className="space-y-4">
              <div className="rounded-2xl border border-base-300 bg-base-200/30 p-5">
                <div className="mt-1 space-y-3 text-sm text-base-content/80">
                  <div>
                    <p className="text-base-content/60">Labels</p>
                    <div className="mt-2 flex flex-wrap gap-2">
                      {editableSelectedCollection.labels?.length ? (
                        editableSelectedCollection.labels.map((label) => (
                          <button
                            type="button"
                            key={label.id}
                            className={`group badge badge-sm gap-1 transition-colors hover:brightness-95 ${getSensitivityClass(label.name)}`}
                            disabled={saving}
                            onClick={() => onRemoveLabel(label.id)}
                            title="Remove label"
                          >
                            {label.name}
                            <XCircleIcon
                              className="size-4 transition-colors group-hover:text-error group-focus-visible:text-error"
                              aria-hidden="true"
                            />
                          </button>
                        ))
                      ) : null}
                    </div>
                    <div className="mt-3 flex flex-col gap-2 sm:flex-row">
                      <label className="input input-bordered input-sm flex min-w-0 flex-1 items-center gap-2">
                        <MagnifyingGlassIcon className="size-4 text-base-content/60" />
                        <input
                          type="text"
                          className="grow"
                          placeholder="Search or add label"
                          value={selectedCollectionLabelSearchTerm}
                          onChange={(event) =>
                            setSelectedCollectionLabelSearchTerm(
                              event.target.value,
                            )
                          }
                          onKeyDown={(event) => {
                            if (event.key === "Enter") {
                              event.preventDefault();
                              void onAddSelectedCollectionLabelFromSearch();
                            }
                          }}
                        />
                      </label>
                      <button
                        type="button"
                        className="btn btn-primary btn-sm"
                        disabled={
                          saving ||
                          selectedCollectionLabelCreating ||
                          !canAddTypedSelectedCollectionLabel
                        }
                        onClick={() => void onAddSelectedCollectionLabelFromSearch()}
                      >
                        {selectedCollectionLabelCreating ? (
                          <span className="loading loading-spinner loading-xs" />
                        ) : (
                          "Add"
                        )}
                      </button>
                    </div>
                    <div className="mt-3 max-h-40 space-y-2 overflow-auto rounded-xl border border-base-300 bg-base-100 p-3">
                      {labelsLoading ? (
                        <div className="flex items-center gap-2 text-sm text-base-content/70">
                          <span className="loading loading-spinner loading-sm" />
                          Loading labels
                        </div>
                      ) : filteredSelectedCollectionLabelOptions.length ? (
                        filteredSelectedCollectionLabelOptions.map((label) => (
                          <button
                            type="button"
                            key={label.id}
                            className="flex w-full items-center justify-between rounded-lg px-2 py-1 text-left text-sm hover:bg-base-200"
                            onClick={() =>
                              onAddSelectedCollectionLabel({
                                id: label.id,
                                name: label.name,
                              })
                            }
                          >
                            <span className="truncate">{label.name}</span>
                            <span className="btn btn-primary btn-xs">Add</span>
                          </button>
                        ))
                      ) : (
                        <p className="text-sm text-base-content/60">
                          No labels found.
                        </p>
                      )}
                    </div>
                  </div>
                  <div>
                    <p className="text-base-content/60">Tags</p>
                    <div className="mt-2 flex flex-wrap gap-2">
                      {editableSelectedCollection.tags?.length ? (
                        editableSelectedCollection.tags.map((tag) => (
                          <button
                            type="button"
                            key={tag.id}
                            className="group badge badge-secondary badge-outline badge-sm gap-1 transition-colors hover:brightness-95"
                            disabled={saving}
                            onClick={() => onRemoveTag(tag.id)}
                            title="Remove tag"
                          >
                            {tag.name}
                            <XCircleIcon
                              className="size-4 transition-colors group-hover:text-error group-focus-visible:text-error"
                              aria-hidden="true"
                            />
                          </button>
                        ))
                      ) : null}
                    </div>
                    <div className="mt-3 flex flex-col gap-2 sm:flex-row">
                      <label className="input input-bordered input-sm flex min-w-0 flex-1 items-center gap-2">
                        <MagnifyingGlassIcon className="size-4 text-base-content/60" />
                        <input
                          type="text"
                          className="grow"
                          placeholder="Search or add tag"
                          value={selectedCollectionTagSearchTerm}
                          onChange={(event) =>
                            setSelectedCollectionTagSearchTerm(event.target.value)
                          }
                          onKeyDown={(event) => {
                            if (event.key === "Enter") {
                              event.preventDefault();
                              void onAddSelectedCollectionTagFromSearch();
                            }
                          }}
                        />
                      </label>
                      <button
                        type="button"
                        className="btn btn-primary btn-sm"
                        disabled={
                          saving ||
                          selectedCollectionTagCreating ||
                          !canAddTypedSelectedCollectionTag
                        }
                        onClick={() => void onAddSelectedCollectionTagFromSearch()}
                      >
                        {selectedCollectionTagCreating ? (
                          <span className="loading loading-spinner loading-xs" />
                        ) : (
                          "Add"
                        )}
                      </button>
                    </div>
                    <div className="mt-3 max-h-40 space-y-2 overflow-auto rounded-xl border border-base-300 bg-base-100 p-3">
                      {tagsLoading ? (
                        <div className="flex items-center gap-2 text-sm text-base-content/70">
                          <span className="loading loading-spinner loading-sm" />
                          Loading tags
                        </div>
                      ) : filteredSelectedCollectionTagOptions.length ? (
                        filteredSelectedCollectionTagOptions.map((tag) => (
                          <button
                            type="button"
                            key={tag.id}
                            className="flex w-full items-center justify-between rounded-lg px-2 py-1 text-left text-sm hover:bg-base-200"
                            onClick={() =>
                              onAddSelectedCollectionTag({
                                id: tag.id,
                                name: tag.name,
                              })
                            }
                          >
                            <span className="truncate">{tag.name}</span>
                            <span className="btn btn-primary btn-xs">Add</span>
                          </button>
                        ))
                      ) : (
                        <p className="text-sm text-base-content/60">
                          No tags found.
                        </p>
                      )}
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <div className="mt-6 rounded-2xl border border-base-300 bg-base-100 p-5">
            <div className="flex flex-col gap-1 sm:flex-row sm:items-start sm:justify-between">
              <div>
                <h3 className="font-semibold text-base-content">
                  Manage Records
                </h3>
                <p className="text-sm text-base-content/60">
                  Add records to this collection or remove records already assigned.
                </p>
              </div>
              <span className="text-sm font-semibold text-base-content/70">
                {collectionRecords.length} assigned
              </span>
            </div>

            <div className="mt-4 rounded-2xl border border-base-300 bg-base-200/30 p-4">
              <div className="flex flex-col gap-3 lg:flex-row">
                <label className="input input-bordered flex min-w-0 flex-1 items-center gap-2">
                  <MagnifyingGlassIcon className="size-5 text-base-content/60" />
                  <input
                    type="text"
                    className="grow"
                    placeholder="Search all records"
                    value={recordSearchTerm}
                    onChange={(event) => setRecordSearchTerm(event.target.value)}
                    onKeyDown={(event) => {
                      if (event.key === "Enter") {
                        event.preventDefault();
                        onSearchRecords();
                      }
                    }}
                  />
                </label>
                <button
                  type="button"
                  className="btn btn-outline"
                  disabled={recordSearchLoading}
                  onClick={onSearchRecords}
                >
                  Search
                </button>
              </div>

              <div className="mt-4 max-h-72 overflow-auto rounded-xl border border-base-300 bg-base-100">
                <table className="table table-sm table-pin-rows">
                  <thead className="bg-base-100">
                    <tr>
                      <th>Record</th>
                      <th>Class</th>
                      <th>Source</th>
                      <th>Updated</th>
                      <th></th>
                    </tr>
                  </thead>
                  <tbody>
                    {editRecordResults.length ? (
                      editRecordResults.map((record) => {
                        const recordId =
                          typeof record.id === "number" ? record.id : null;
                        const isAssigned =
                          typeof recordId === "number" &&
                          collectionRecordIds.has(recordId);
                        const isAdding =
                          typeof recordId === "number" &&
                          addingRecordIds.includes(recordId);
                        const isRemoving =
                          typeof recordId === "number" &&
                          removingRecordIds.includes(recordId);
                        const className =
                          "className" in record ? record.className : record.classId;
                        const sourceName =
                          "dataSourceName" in record
                            ? record.dataSourceName
                            : record.projectId ?? projectId;

                        return (
                          <tr key={record.id ?? record.name}>
                            <td className="font-medium">{record.name}</td>
                            <td>{className ?? "Unclassified"}</td>
                            <td>{sourceName ?? "Unknown"}</td>
                            <td>
                              {record.lastUpdatedAt
                                ? formatLocalDateTime(record.lastUpdatedAt)
                                : "Not Updated"}
                            </td>
                            <td className="text-right">
                              {isAssigned ? (
                                <button
                                  type="button"
                                  className="btn btn-error btn-outline btn-xs"
                                  disabled={isRemoving || recordId === null}
                                  onClick={() =>
                                    recordId !== null &&
                                    void onRemoveCollectionRecord(recordId)
                                  }
                                >
                                  {isRemoving ? (
                                    <span className="loading loading-spinner loading-xs" />
                                  ) : (
                                    "Remove"
                                  )}
                                </button>
                              ) : (
                                <button
                                  type="button"
                                  className="btn btn-primary btn-xs"
                                  disabled={isAdding || recordId === null}
                                  onClick={() =>
                                    recordId !== null &&
                                    void onAddCollectionRecord(recordId)
                                  }
                                >
                                  {isAdding ? (
                                    <span className="loading loading-spinner loading-xs" />
                                  ) : (
                                    "Add"
                                  )}
                                </button>
                              )}
                            </td>
                          </tr>
                        );
                      })
                    ) : (
                      <tr>
                        <td colSpan={5}>
                          {recordSearchTerm.trim()
                            ? "No records match this search."
                            : "No records are currently assigned."}
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>

              {recordSearchLoading ? (
                <div className="mt-3 flex items-center gap-2 text-sm text-base-content/70">
                  <span className="loading loading-spinner loading-sm" />
                  Searching records
                </div>
              ) : null}
            </div>
          </div>
        </SectionCard>
      )}

    </div>
  );
}
