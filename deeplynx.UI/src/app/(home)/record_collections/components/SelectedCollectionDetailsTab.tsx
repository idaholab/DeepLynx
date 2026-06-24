"use client";

import { useLanguage } from "@/app/contexts/Language";
import { formatLocalDateTime } from "@/app/lib/date_time";
import React from "react";
import CollectionEntitySelector from "./CollectionEntitySelector";
import CollectionDetailsReadonlyView from "./CollectionDetailsReadonlyView";
import CollectionRecordSearchControls from "./CollectionRecordSearchControls";
import CollectionRecordSearchResultsTable from "./CollectionRecordSearchResultsTable";
import SectionCard from "./SectionCard";
import { interpolateTemplate } from "@/app/lib/record_helpers";
import type { CollectionDetailsController } from "../[collectionId]/hooks/useCollectionDetails";

type Props = {
  controller: CollectionDetailsController["detailsController"];
};

export default function SelectedCollectionDetailsTab({
  controller: {
    readonlyView: {
      selectedCollection,
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
      recordMutationStatusById,
      onRemoveCollectionRecord,
      onAddCollectionRecord,
      onCancelSelectedCollectionEdit,
      onSaveSelectedDetails,
    },
  },
}: Props) {
  const { t } = useLanguage();
  const [confirmRemoveRecordId, setConfirmRemoveRecordId] = React.useState<
    number | null
  >(null);

  const handleConfirmRemoveRecord = React.useCallback(
    async (recordId: number) => {
      await onRemoveCollectionRecord(recordId);
      setConfirmRemoveRecordId((current) =>
        current === recordId ? null : current,
      );
    },
    [onRemoveCollectionRecord],
  );

  return (
    <div className="mt-4 space-y-4">
      <div className="grid gap-4 rounded-2xl border border-base-300 bg-base-200/30 p-4 text-sm sm:grid-cols-2 lg:grid-cols-4">
        <div>
          <p className="text-base-content/60">
            {t.translations.RECORD_COLLECTIONS_COLLECTION_ID}
          </p>
          <p className="font-semibold text-base-content">
            {selectedCollection.id}
          </p>
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
              {t.translations.RECORD_COLLECTIONS_EDIT_COLLECTION}
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
              {t.translations.RECORD_COLLECTIONS_VIEW_ALL}
            </button>
          }
          recordsPerPage={recordsPerPage}
          recordPage={collectionDetailRecordPage}
          setRecordPage={setCollectionDetailRecordPage}
          recordPageCount={collectionDetailRecordPageCount}
        />
      ) : (
        <SectionCard
          title={t.translations.RECORD_COLLECTIONS_MANAGE}
          subtitle={
            t.translations.RECORD_COLLECTIONS_MANAGE_IDENTITY_LABELS_METADATA
          }
          action={
            <div className="flex flex-wrap justify-end gap-2">
              <button
                type="button"
                className="btn btn-outline btn-sm"
                disabled={saving}
                onClick={() => void onCancelSelectedCollectionEdit()}
              >
                {t.translations.CANCEL}
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
                  t.translations.RECORD_COLLECTIONS_SAVE_MODIFICATIONS
                )}
              </button>
            </div>
          }
        >
          <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_380px]">
            <div className="space-y-4">
              <label className="form-control w-full">
                <div className="label">
                  <span className="label-text font-medium">
                    {t.translations.NAME}
                  </span>
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
                  {t.translations.DESCRIPTION}
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
                    {t.translations.RECORD_COLLECTIONS_ADDITIONAL_PROPERTIES}
                  </h3>
                  <button
                    type="button"
                    className="btn btn-outline btn-sm"
                    disabled={saving}
                    onClick={() =>
                      setSelectedCollectionPropertiesEditorOpen(true)
                    }
                  >
                    {t.translations.EDIT}
                  </button>
                </div>
                <div className="mt-4 max-h-[17.5rem] overflow-auto pr-1">
                  <table className="table table-pin-rows">
                    <thead className="bg-base-100">
                      <tr>
                        <th>{t.translations.RECORD_COLLECTIONS_FIELD}</th>
                        <th>{t.translations.RECORD_COLLECTIONS_VALUE}</th>
                      </tr>
                    </thead>
                    <tbody>
                      {getMetadataRows(editableSelectedCollection.properties)
                        .length ? (
                        getMetadataRows(
                          editableSelectedCollection.properties,
                        ).map((row) => (
                          <tr key={row.label} className="h-10">
                            <td className="font-medium">{row.label}</td>
                            <td>{row.value}</td>
                          </tr>
                        ))
                      ) : (
                        <tr>
                          <td colSpan={2}>
                            {
                              t.translations
                                .RECORD_COLLECTIONS_NO_ADDITIONAL_PROPERTIES_SET
                            }
                          </td>
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
                    title={t.translations.RECORD_COLLECTIONS_LABELS}
                    selectedItems={editableSelectedCollection.labels ?? []}
                    searchTerm={selectedCollectionLabelSearchTerm}
                    setSearchTerm={setSelectedCollectionLabelSearchTerm}
                    searchPlaceholder={
                      t.translations.RECORD_COLLECTIONS_SEARCH_OR_ADD_LABEL
                    }
                    options={filteredSelectedCollectionLabelOptions}
                    loading={labelsLoading}
                    loadingText={
                      t.translations.RECORD_COLLECTIONS_LOADING_LABELS
                    }
                    emptyOptionsText={
                      t.translations.RECORD_COLLECTIONS_NO_LABELS_FOUND
                    }
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
                    title={t.translations.RECORD_COLLECTIONS_TAGS}
                    selectedItems={editableSelectedCollection.tags ?? []}
                    searchTerm={selectedCollectionTagSearchTerm}
                    setSearchTerm={setSelectedCollectionTagSearchTerm}
                    searchPlaceholder={
                      t.translations.RECORD_COLLECTIONS_SEARCH_OR_ADD_TAG
                    }
                    options={filteredSelectedCollectionTagOptions}
                    loading={tagsLoading}
                    loadingText={t.translations.RECORD_COLLECTIONS_LOADING_TAGS}
                    emptyOptionsText={
                      t.translations.RECORD_COLLECTIONS_NO_TAGS_FOUND
                    }
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
                  {t.translations.RECORD_COLLECTIONS_MANAGE_RECORDS}
                </h3>
                <p className="text-sm text-base-content/60">
                  {t.translations.RECORD_COLLECTIONS_MANAGE_RECORDS_DESCRIPTION}
                </p>
              </div>
              <span className="text-sm font-semibold text-base-content/70">
                {interpolateTemplate(
                  t.translations.RECORD_COLLECTIONS_ASSIGNED_COUNT,
                  { count: collectionRecords.length },
                )}
              </span>
            </div>

            <div className="mt-4 rounded-2xl border border-base-300 bg-base-200/30 p-4">
              <CollectionRecordSearchControls
                searchTerm={recordSearchTerm}
                setSearchTerm={setRecordSearchTerm}
                placeholder={
                  t.translations.RECORD_COLLECTIONS_SEARCH_ALL_RECORDS
                }
                searchLoading={recordSearchLoading}
                onSearch={onSearchRecords}
              />

              <CollectionRecordSearchResultsTable
                rows={editRecordResults.map((record) => {
                  const recordId =
                    typeof record.id === "number" ? record.id : null;
                  const isAssigned =
                    typeof recordId === "number" &&
                    collectionRecordIds.has(recordId);
                  const mutationStatus =
                    typeof recordId === "number"
                      ? recordMutationStatusById[recordId]
                      : undefined;
                  const isAdding = mutationStatus === "adding";
                  const isRemoving = mutationStatus === "removing";
                  const showRemoveConfirmation =
                    typeof recordId === "number" &&
                    confirmRemoveRecordId === recordId;

                  return {
                    key: record.id ?? record.name ?? "record",
                    name: record.name,
                    className:
                      ("className" in record
                        ? record.className
                        : record.classId) ??
                      t.translations.RECORD_COLLECTIONS_UNCLASSIFIED,
                    sourceName:
                      ("dataSourceName" in record
                        ? record.dataSourceName
                        : record.dataSourceId) ??
                      t.translations.UNKNOWN,
                    updatedAt: record.lastUpdatedAt,
                    actionCell: (
                      <div className="flex min-w-[16rem] justify-end">
                        {isAssigned ? (
                          showRemoveConfirmation ? (
                            <div className="flex flex-nowrap items-center justify-end gap-2 text-xs">
                              <span className="whitespace-nowrap text-base-content/70">
                                {t.translations.RECORD_COLLECTIONS_ARE_YOU_SURE}
                              </span>
                              <button
                                type="button"
                                className="btn btn-error btn-xs"
                                disabled={isRemoving || recordId === null}
                                onClick={() =>
                                  recordId !== null &&
                                  void handleConfirmRemoveRecord(recordId)
                                }
                              >
                                {isRemoving ? (
                                  <span className="loading loading-spinner loading-xs" />
                                ) : (
                                  t.translations.YES
                                )}
                              </button>
                              <button
                                type="button"
                                className="btn btn-ghost btn-xs"
                                disabled={isRemoving}
                                onClick={() => setConfirmRemoveRecordId(null)}
                              >
                                {t.translations.NO}
                              </button>
                            </div>
                          ) : (
                            <button
                              type="button"
                              className="btn btn-error btn-outline btn-xs"
                              disabled={isRemoving || recordId === null}
                              onClick={() =>
                                recordId !== null &&
                                setConfirmRemoveRecordId(recordId)
                              }
                            >
                              {isRemoving ? (
                                <span className="loading loading-spinner loading-xs" />
                              ) : (
                                t.translations.REMOVE
                              )}
                            </button>
                          )
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
                              t.translations.ADD
                            )}
                          </button>
                        )}
                      </div>
                    ),
                  };
                })}
                emptyMessage={
                  recordSearchTerm.trim()
                    ? t.translations.RECORD_COLLECTIONS_NO_RECORDS_MATCH_SEARCH
                    : t.translations
                        .RECORD_COLLECTIONS_NO_RECORDS_ARE_CURRENTLY_ASSIGNED
                }
              />

              {recordSearchLoading ? (
                <div className="mt-3 flex items-center gap-2 text-sm text-base-content/70">
                  <span className="loading loading-spinner loading-sm" />
                  {t.translations.RECORD_COLLECTIONS_SEARCHING_RECORDS}
                </div>
              ) : null}
            </div>
          </div>
        </SectionCard>
      )}
    </div>
  );
}
