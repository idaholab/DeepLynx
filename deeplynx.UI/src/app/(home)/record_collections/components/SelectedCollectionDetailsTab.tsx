"use client";

import React from "react";
import CollectionEntitySelector from "./CollectionEntitySelector";
import CollectionDetailsReadonlyView from "./CollectionDetailsReadonlyView";
import CollectionRecordSearchControls from "./CollectionRecordSearchControls";
import CollectionRecordSearchResultsTable from "./CollectionRecordSearchResultsTable";
import SectionCard from "./SectionCard";
import { SelectedCollectionDetailsController } from "./componentTypes";

type Props = {
  controller: SelectedCollectionDetailsController;
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
                  <CollectionEntitySelector
                    title="Labels"
                    selectedItems={editableSelectedCollection.labels ?? []}
                    searchTerm={selectedCollectionLabelSearchTerm}
                    setSearchTerm={setSelectedCollectionLabelSearchTerm}
                    searchPlaceholder="Search or add label"
                    options={filteredSelectedCollectionLabelOptions}
                    loading={labelsLoading}
                    loadingText="Loading labels"
                    emptyOptionsText="No labels found."
                    addDisabled={
                      saving ||
                      selectedCollectionLabelCreating ||
                      !canAddTypedSelectedCollectionLabel
                    }
                    addButtonLoading={selectedCollectionLabelCreating}
                    selectedItemClassName={(item) =>
                      `badge badge-sm gap-1 ${getSensitivityClass(item.name)}`
                    }
                    addTypedItem={onAddSelectedCollectionLabelFromSearch}
                    selectOption={(item) =>
                      onAddSelectedCollectionLabel({
                        id: Number(item.id),
                        name: item.name,
                      })
                    }
                    removeItem={(item) => onRemoveLabel(Number(item.id))}
                  />
                  <CollectionEntitySelector
                    title="Tags"
                    selectedItems={editableSelectedCollection.tags ?? []}
                    searchTerm={selectedCollectionTagSearchTerm}
                    setSearchTerm={setSelectedCollectionTagSearchTerm}
                    searchPlaceholder="Search or add tag"
                    options={filteredSelectedCollectionTagOptions}
                    loading={tagsLoading}
                    loadingText="Loading tags"
                    emptyOptionsText="No tags found."
                    addDisabled={
                      saving ||
                      selectedCollectionTagCreating ||
                      !canAddTypedSelectedCollectionTag
                    }
                    addButtonLoading={selectedCollectionTagCreating}
                    selectedItemClassName={() =>
                      "badge badge-secondary badge-outline badge-sm gap-1"
                    }
                    addTypedItem={onAddSelectedCollectionTagFromSearch}
                    selectOption={(item) =>
                      onAddSelectedCollectionTag({
                        id: Number(item.id),
                        name: item.name,
                      })
                    }
                    removeItem={(item) => onRemoveTag(Number(item.id))}
                  />
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
              <CollectionRecordSearchControls
                searchTerm={recordSearchTerm}
                setSearchTerm={setRecordSearchTerm}
                placeholder="Search all records"
                searchLoading={recordSearchLoading}
                onSearch={onSearchRecords}
              />

              <CollectionRecordSearchResultsTable
                rows={editRecordResults.map((record) => {
                  const recordId = typeof record.id === "number" ? record.id : null;
                  const isAssigned =
                    typeof recordId === "number" && collectionRecordIds.has(recordId);
                  const isAdding =
                    typeof recordId === "number" && addingRecordIds.includes(recordId);
                  const isRemoving =
                    typeof recordId === "number" && removingRecordIds.includes(recordId);

                  return {
                    key: record.id ?? record.name ?? "record",
                    name: record.name,
                    className:
                      ("className" in record ? record.className : record.classId) ??
                      "Unclassified",
                    sourceName:
                      ("dataSourceName" in record
                        ? record.dataSourceName
                        : record.projectId) ??
                      projectId ??
                      "Unknown",
                    updatedAt: record.lastUpdatedAt,
                    actionCell: isAssigned ? (
                      <button
                        type="button"
                        className="btn btn-error btn-outline btn-xs"
                        disabled={isRemoving || recordId === null}
                        onClick={() =>
                          recordId !== null && void onRemoveCollectionRecord(recordId)
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
                          recordId !== null && void onAddCollectionRecord(recordId)
                        }
                      >
                        {isAdding ? (
                          <span className="loading loading-spinner loading-xs" />
                        ) : (
                          "Add"
                        )}
                      </button>
                    ),
                  };
                })}
                emptyMessage={
                  recordSearchTerm.trim()
                    ? "No records match this search."
                    : "No records are currently assigned."
                }
              />

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
