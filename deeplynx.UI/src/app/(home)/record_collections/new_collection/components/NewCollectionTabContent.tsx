"use client";

import PaginationControls from "@/app/(home)/components/PaginationControls";
import { useLanguage } from "@/app/contexts/Language";
import React from "react";
import CollectionDetailsReadonlyView from "../../components/CollectionDetailsReadonlyView";
import CollectionEntitySelector from "../../components/CollectionEntitySelector";
import CollectionFacetSummary from "../../components/CollectionFacetSummary";
import CollectionRecordSearchControls from "../../components/CollectionRecordSearchControls";
import CollectionRecordSearchResultsTable from "../../components/CollectionRecordSearchResultsTable";
import NewCollectionStepIndicator from "./NewCollectionStepIndicator";
import SelectedRecordsPreviewPanel from "../../components/SelectedRecordsPreviewPanel";
import SectionCard from "../../components/SectionCard";
import { interpolateTemplate } from "@/app/lib/record_helpers";
import type { NewCollectionTabController } from "../hooks/useNewCollectionWorkflow";

type Props = {
  controller: NewCollectionTabController;
};

export default function NewCollectionTabContent({
  controller: {
    workflow: {
      projectId,
      newCollectionStep,
      setNewCollectionStep,
      recordsPerPage,
      setRecordsPerPage,
      recordPageSizeOptions,
      saving,
      selectedRecordEnrichmentPending,
      hasSelectedRecordEnrichmentFailures,
      selectedRecordEnrichmentFailureCount,
      getSensitivityClass,
    },
    metadata: {
      newCollectionName,
      setNewCollectionName,
      newCollectionDescription,
      setNewCollectionDescription,
    },
    recordSearch: {
      newCollectionRecordSearchTerm,
      setNewCollectionRecordSearchTerm,
      newCollectionRecordSearchResults,
      newCollectionRecordSearchLoading,
      visibleNewCollectionRecords,
      visibleSelectionState,
      retrievedSelectionState,
      newCollectionRecordPage,
      setNewCollectionRecordPage,
      newCollectionRecordPageCount,
      onSearchRecords,
      onClearRecordSearch,
      onToggleSelectAllVisibleRecords,
      onToggleNewCollectionRecord,
      onSelectAllSearchedRecords,
    },
    selection: {
      newCollectionSelectedRecordIds,
      newCollectionSelectedRecords,
      confirmClearNewCollectionRecords,
      setConfirmClearNewCollectionRecords,
      newCollectionSelectedLabelTally,
      newCollectionSelectedTagTally,
      onDeselectRecordsByLabel,
      onDeselectRecordsByTag,
      onClearSelectedRecords,
    },
    review: {
      newCollectionReviewSearchTerm,
      setNewCollectionReviewSearchTerm,
      filteredNewCollectionSelectedRecords,
      visibleNewCollectionReviewRecords,
      newCollectionReviewPage,
      setNewCollectionReviewPage,
      newCollectionReviewPageCount,
    },
    labelsAndTags: {
      selectedNewCollectionLabels,
      newCollectionSelectedTagNames,
      newCollectionLabelSearchTerm,
      setNewCollectionLabelSearchTerm,
      newCollectionTagSearchTerm,
      setNewCollectionTagSearchTerm,
      filteredNewCollectionLabelOptions,
      filteredNewCollectionTagOptions,
      labelsLoading,
      tagsLoading,
      newCollectionLabelCreating,
      canAddTypedNewCollectionLabel,
      canAddTypedNewCollectionTag,
      onAddNewCollectionLabelFromSearch,
      onAddNewCollectionLabel,
      onRemoveNewCollectionLabel,
      onAddNewCollectionTag,
      onRemoveNewCollectionTag,
    },
    actions: {
      onCancelToAllCollections,
      onGoToModifyStep,
      onGoToReviewStep,
      onCreateCollection,
    },
  },
}: Props) {
  const { t } = useLanguage();
  const [reviewDescriptionExpanded, setReviewDescriptionExpanded] =
    React.useState(false);
  const [reviewDescriptionExpandable, setReviewDescriptionExpandable] =
    React.useState(false);
  const [reviewLabelsExpanded, setReviewLabelsExpanded] = React.useState(false);
  const [reviewTagsExpanded, setReviewTagsExpanded] = React.useState(false);
  const reviewDescriptionRef = React.useRef<HTMLParagraphElement | null>(null);

  React.useEffect(() => {
    const measureReviewDescriptionOverflow = () => {
      const descriptionElement = reviewDescriptionRef.current;
      const nextExpandable = Boolean(
        descriptionElement &&
        descriptionElement.scrollHeight - descriptionElement.clientHeight > 1,
      );

      setReviewDescriptionExpandable((current) =>
        current === nextExpandable ? current : nextExpandable,
      );
    };

    const frameId = window.requestAnimationFrame(
      measureReviewDescriptionOverflow,
    );
    window.addEventListener("resize", measureReviewDescriptionOverflow);

    return () => {
      window.cancelAnimationFrame(frameId);
      window.removeEventListener("resize", measureReviewDescriptionOverflow);
    };
  }, [newCollectionDescription, newCollectionStep]);

  const reviewTagItems = newCollectionSelectedTagNames.map(
    (tagName, index) => ({
      id: `${tagName}-${index}`,
      name: tagName,
    }),
  );
  const visibleReviewLabels = reviewLabelsExpanded
    ? selectedNewCollectionLabels
    : selectedNewCollectionLabels.slice(0, 10);
  const visibleReviewTags = reviewTagsExpanded
    ? reviewTagItems
    : reviewTagItems.slice(0, 10);
  const reviewSummaryPanel = (
    <div className="grid gap-4 rounded-2xl border border-base-300 bg-base-200/30 p-4 text-sm sm:grid-cols-2 lg:grid-cols-4">
      <div>
        <p className="text-base-content/60">
          {t.translations.RECORD_COLLECTIONS_COLLECTION_ID}
        </p>
        <p className="font-semibold text-base-content">
          {t.translations.PENDING}
        </p>
      </div>
      <div>
        <p className="text-base-content/60">
          {t.translations.RECORD_COLLECTIONS_TOTAL_RECORDS}
        </p>
        <p className="font-semibold text-base-content">
          {newCollectionSelectedRecords.length}
        </p>
      </div>
      <div>
        <p className="text-base-content/60">
          {t.translations.RECORD_COLLECTIONS_UPDATED}
        </p>
        <p className="font-semibold text-base-content">
          {t.translations.RECORD_COLLECTIONS_UPDATED_ON_CREATE}
        </p>
      </div>
      <div>
        <p className="text-base-content/60">
          {t.translations.RECORD_COLLECTIONS_LAST_UPDATED_BY}
        </p>
        <p className="font-semibold text-base-content">
          {t.translations.PENDING}
        </p>
      </div>
    </div>
  );
  const selectedRecordsPreview = (
    <SelectedRecordsPreviewPanel
      title={t.translations.SELECTED_RECORDS}
      shownCount={filteredNewCollectionSelectedRecords.length}
      totalCount={newCollectionSelectedRecords.length}
      searchTerm={newCollectionReviewSearchTerm}
      setSearchTerm={setNewCollectionReviewSearchTerm}
      records={visibleNewCollectionReviewRecords}
      emptyMessage={
        t.translations.RECORD_COLLECTIONS_NO_SELECTED_RECORDS_MATCH_SEARCH
      }
      currentPage={newCollectionReviewPage}
      setCurrentPage={setNewCollectionReviewPage}
      pageCount={newCollectionReviewPageCount}
      pageSize={recordsPerPage}
      pageSizeOptions={recordPageSizeOptions}
      onPageSizeChange={setRecordsPerPage}
    />
  );

  const hasSelectedRecords = newCollectionSelectedRecordIds.length > 0;
  const hasRequiredMetadata =
    newCollectionName.trim().length > 0 &&
    newCollectionDescription.trim().length > 0;

  return (
    <div className="mt-4">
      <SectionCard
        title={t.translations.RECORD_COLLECTIONS_NEW}
        subtitle={t.translations.RECORD_COLLECTIONS_CREATE_IN_ACTIVE_PROJECT}
      >
        <NewCollectionStepIndicator
          steps={[
            {
              label: t.translations.RECORD_COLLECTIONS_STEP_1,
              detail: t.translations.RECORD_COLLECTIONS_STEP_SELECT_RECORDS,
              active: newCollectionStep === "Records",
            },
            {
              label: t.translations.RECORD_COLLECTIONS_STEP_2,
              detail: t.translations.RECORD_COLLECTIONS_STEP_ADD_METADATA,
              active: newCollectionStep === "Metadata",
            },
            {
              label: t.translations.RECORD_COLLECTIONS_STEP_3,
              detail:
                t.translations.RECORD_COLLECTIONS_STEP_MODIFY_LABELS_AND_TAGS,
              active: newCollectionStep === "Modify",
            },
            {
              label: t.translations.RECORD_COLLECTIONS_STEP_4,
              detail: t.translations.RECORD_COLLECTIONS_STEP_REVIEW,
              active: newCollectionStep === "Review",
            },
          ]}
        />

        <div
          className={
            newCollectionStep === "Review"
              ? "space-y-6"
              : "grid gap-6 xl:grid-cols-[minmax(0,1.35fr)_minmax(320px,0.65fr)]"
          }
        >
          {newCollectionStep === "Records" ? (
            <>
              <div className="space-y-4">
                <div className="rounded-2xl border border-base-300 bg-base-200/30 p-4">
                  <CollectionRecordSearchControls
                    searchTerm={newCollectionRecordSearchTerm}
                    setSearchTerm={setNewCollectionRecordSearchTerm}
                    placeholder={
                      t.translations.RECORD_COLLECTIONS_SEARCH_RECORDS_TO_ADD
                    }
                    searchLoading={newCollectionRecordSearchLoading}
                    onSearch={() => void onSearchRecords()}
                    action={
                      <button
                        type="button"
                        className="btn btn-outline"
                        disabled={
                          newCollectionRecordSearchLoading ||
                          !newCollectionRecordSearchTerm.trim()
                        }
                        onClick={onClearRecordSearch}
                      >
                        {t.translations.CLEAR}
                      </button>
                    }
                  />

                  <div className="mt-3 flex flex-wrap items-center gap-2 text-sm text-base-content/70">
                    <span>
                      {interpolateTemplate(
                        t.translations
                          .RECORD_COLLECTIONS_SELECTED_RECORDS_COUNT,
                        { count: newCollectionSelectedRecordIds.length },
                      )}
                    </span>
                    <button
                      type="button"
                      className="btn btn-xs btn-outline"
                      disabled={
                        newCollectionRecordSearchLoading ||
                        newCollectionRecordSearchResults.length === 0 ||
                        retrievedSelectionState === "all"
                      }
                      onClick={() => void onSelectAllSearchedRecords()}
                    >
                      {t.translations.SELECT_ALL}
                    </button>
                    <button
                      type="button"
                      className="btn btn-xs btn-outline"
                      disabled={
                        newCollectionRecordSearchLoading ||
                        newCollectionRecordSearchResults.length === 0 ||
                        retrievedSelectionState !== "all"
                      }
                      onClick={onClearSelectedRecords}
                    >
                      {t.translations.RECORD_COLLECTIONS_UNSELECT_ALL}
                    </button>
                  </div>

                  {newCollectionRecordSearchResults.length ? (
                    <>
                      <CollectionRecordSearchResultsTable
                        rows={visibleNewCollectionRecords.map((record) => ({
                          key: record.id ?? record.name,
                          leadingCell: (
                            <input
                              type="checkbox"
                              className="checkbox checkbox-sm"
                              checked={
                                typeof record.id === "number" &&
                                newCollectionSelectedRecordIds.includes(
                                  record.id,
                                )
                              }
                              disabled={typeof record.id !== "number"}
                              onChange={() =>
                                void onToggleNewCollectionRecord(record)
                              }
                            />
                          ),
                          name: record.name,
                          className:
                            record.className ??
                            t.translations.RECORD_COLLECTIONS_UNCLASSIFIED,
                          sourceName:
                            record.dataSourceName ?? t.translations.UNKNOWN,
                          updatedAt: record.lastUpdatedAt,
                        }))}
                        emptyMessage={
                          t.translations.RECORD_COLLECTIONS_NO_RECORDS_FOUND
                        }
                        maxHeightClassName="max-h-fit"
                        pinnedHeader={false}
                        leadingHeaderCell={
                          <input
                            type="checkbox"
                            className="checkbox checkbox-sm"
                            checked={visibleSelectionState === "all"}
                            ref={(input) => {
                              if (input) {
                                input.indeterminate =
                                  visibleSelectionState === "some";
                              }
                            }}
                            onChange={() =>
                              void onToggleSelectAllVisibleRecords()
                            }
                          />
                        }
                      />
                      {newCollectionRecordSearchResults.length >
                      recordsPerPage ? (
                        <div className="flex flex-col gap-3 px-4 py-3 text-sm sm:flex-row sm:items-center sm:justify-between">
                          <span className="text-base-content/70">
                            {`${t.translations.SHOWING} ${(newCollectionRecordPage - 1) * recordsPerPage + 1}-${Math.min(
                              newCollectionRecordPage * recordsPerPage,
                              newCollectionRecordSearchResults.length,
                            )} ${t.translations.OF} ${newCollectionRecordSearchResults.length}`}
                          </span>
                          <PaginationControls
                            currentPage={newCollectionRecordPage}
                            pageSize={recordsPerPage}
                            totalPages={newCollectionRecordPageCount}
                            pageSizeOptions={recordPageSizeOptions}
                            onPageChange={setNewCollectionRecordPage}
                            onPageSizeChange={setRecordsPerPage}
                          />
                        </div>
                      ) : null}
                    </>
                  ) : null}

                  {newCollectionRecordSearchLoading ? (
                    <div className="mt-3 flex items-center gap-2 text-sm text-base-content/70">
                      <span className="loading loading-spinner loading-sm" />
                      {t.translations.RECORD_COLLECTIONS_SEARCHING_RECORDS}
                    </div>
                  ) : null}
                </div>
              </div>

              <div className="flex h-full flex-col justify-between gap-4">
                <div className="rounded-2xl border border-base-300 bg-base-100 p-5">
                  <h3 className="font-semibold text-base-content">
                    {t.translations.RECORD_COLLECTIONS_RECORD_SUMMARY}
                  </h3>
                  <p className="mt-2 text-sm text-base-content/70">
                    {t.translations.RECORD_COLLECTIONS_RECORD_SUMMARY_HELP}
                  </p>
                  <div className="mt-2 flex flex-wrap items-center gap-2 text-sm text-base-content/70">
                    <span>
                      {interpolateTemplate(
                        t.translations
                          .RECORD_COLLECTIONS_SELECTED_RECORDS_COUNT,
                        { count: newCollectionSelectedRecords.length },
                      )}
                    </span>
                    {newCollectionSelectedRecords.length > 0 ? (
                      confirmClearNewCollectionRecords ? (
                        <span className="flex flex-wrap items-center gap-2">
                          <span>
                            {t.translations.RECORD_COLLECTIONS_ARE_YOU_SURE}
                          </span>
                          <button
                            type="button"
                            className="btn btn-error btn-xs"
                            onClick={onClearSelectedRecords}
                          >
                            {t.translations.YES}
                          </button>
                          <button
                            type="button"
                            className="btn btn-ghost btn-xs"
                            onClick={() =>
                              setConfirmClearNewCollectionRecords(false)
                            }
                          >
                            {t.translations.NO}
                          </button>
                        </span>
                      ) : (
                        <button
                          type="button"
                          className="btn btn-outline btn-xs"
                          onClick={() =>
                            setConfirmClearNewCollectionRecords(true)
                          }
                        >
                          {t.translations.CLEAR}
                        </button>
                      )
                    ) : null}
                  </div>

                  <div className="mt-5 space-y-5">
                    <CollectionFacetSummary
                      labelFacets={newCollectionSelectedLabelTally}
                      tagFacets={newCollectionSelectedTagTally}
                      getSensitivityClass={getSensitivityClass}
                      onRemoveLabel={onDeselectRecordsByLabel}
                      onRemoveTag={onDeselectRecordsByTag}
                    />
                  </div>
                </div>

                <div className="flex flex-wrap justify-end gap-2 self-end">
                  {selectedRecordEnrichmentPending ? (
                    <p className="w-full text-right text-sm text-base-content/70">
                      {
                        t.translations
                          .RECORD_COLLECTIONS_WAIT_FOR_SELECTED_RECORD_METADATA
                      }
                    </p>
                  ) : null}
                  {!selectedRecordEnrichmentPending &&
                  hasSelectedRecordEnrichmentFailures ? (
                    <p className="w-full text-right text-sm text-error">
                      {t.translations.RECORD_COLLECTIONS_SELECTED_RECORD_METADATA_INCOMPLETE.replace(
                        "{count}",
                        String(selectedRecordEnrichmentFailureCount),
                      )}
                    </p>
                  ) : null}
                  <button
                    type="button"
                    className="btn btn-ghost btn-sm"
                    onClick={onCancelToAllCollections}
                  >
                    {t.translations.CANCEL}
                  </button>
                  <button
                    type="button"
                    className="btn btn-primary btn-sm"
                    onClick={() => setNewCollectionStep("Metadata")}
                    disabled={
                      !hasSelectedRecords || selectedRecordEnrichmentPending
                    }
                  >
                    {t.translations.NEXT}
                  </button>
                </div>
              </div>
            </>
          ) : null}

          {newCollectionStep === "Metadata" ? (
            <>
              <div className="space-y-4">
                <div className="rounded-2xl border border-base-300 bg-base-200/30 p-5">
                  <div className="space-y-4">
                    <label className="form-control w-full">
                      <div className="label">
                        <span className="label-text font-medium">
                          {t.translations.RECORD_COLLECTIONS_TITLE}
                        </span>
                      </div>
                      <input
                        type="text"
                        className="input input-bordered w-full"
                        value={newCollectionName}
                        required
                        onChange={(event) =>
                          setNewCollectionName(event.target.value)
                        }
                      />
                    </label>

                    <label className="form-control w-full">
                      <div className="label">
                        <span className="label-text font-medium">
                          {t.translations.DESCRIPTION}
                        </span>
                      </div>
                      <textarea
                        className="textarea textarea-bordered min-h-32 w-full resize-y"
                        value={newCollectionDescription}
                        required
                        onChange={(event) =>
                          setNewCollectionDescription(event.target.value)
                        }
                      />
                    </label>

                    {selectedRecordsPreview}
                  </div>
                </div>
              </div>

              <div className="flex h-full flex-col justify-between gap-4">
                <div className="rounded-2xl border border-base-300 bg-base-100 p-5">
                  <h3 className="font-semibold text-base-content">
                    {t.translations.RECORD_COLLECTIONS_SELECTED_LABELS_AND_TAGS}
                  </h3>
                  <p className="mt-2 text-sm text-base-content/70">
                    {
                      t.translations
                        .RECORD_COLLECTIONS_SELECTED_LABELS_AND_TAGS_HELP
                    }
                  </p>
                  <div className="mt-5">
                    <CollectionFacetSummary
                      labelFacets={newCollectionSelectedLabelTally}
                      tagFacets={newCollectionSelectedTagTally}
                      getSensitivityClass={getSensitivityClass}
                    />
                  </div>
                </div>

                <div className="flex flex-wrap justify-end gap-2 self-end">
                  <button
                    type="button"
                    className="btn btn-ghost btn-sm"
                    onClick={onCancelToAllCollections}
                  >
                    {t.translations.CANCEL}
                  </button>
                  <button
                    type="button"
                    className="btn btn-outline btn-sm"
                    onClick={() => setNewCollectionStep("Records")}
                  >
                    {t.translations.BACK}
                  </button>
                  <button
                    type="button"
                    className="btn btn-primary btn-sm"
                    disabled={
                      saving ||
                      selectedRecordEnrichmentPending ||
                      !hasRequiredMetadata
                    }
                    onClick={() => void onGoToModifyStep()}
                  >
                    {t.translations.NEXT}
                  </button>
                </div>
              </div>
            </>
          ) : null}

          {newCollectionStep === "Modify" ? (
            <>
              <div className="space-y-4">
                <div className="rounded-2xl border border-base-300 bg-base-200/30 p-5">
                  <div className="space-y-4">
                    <label className="form-control w-full">
                      <div className="label">
                        <span className="label-text font-medium">
                          {t.translations.RECORD_COLLECTIONS_TITLE}
                        </span>
                      </div>
                      <input
                        type="text"
                        className="input input-bordered w-full bg-base-200"
                        value={newCollectionName}
                        readOnly
                      />
                    </label>

                    <label className="form-control w-full">
                      <div className="label">
                        <span className="label-text font-medium">
                          {t.translations.DESCRIPTION}
                        </span>
                      </div>
                      <textarea
                        className="textarea textarea-bordered min-h-32 w-full resize-y bg-base-200"
                        value={newCollectionDescription}
                        readOnly
                      />
                    </label>

                    {selectedRecordsPreview}
                  </div>
                </div>
              </div>

              <div className="flex h-full flex-col justify-between gap-4">
                <div className="rounded-2xl border border-base-300 bg-base-100 p-5">
                  <h3 className="font-semibold text-base-content">
                    {t.translations.RECORD_COLLECTIONS_MODIFY_LABELS_AND_TAGS}
                  </h3>
                  <p className="mt-2 text-sm text-base-content/70">
                    {
                      t.translations
                        .RECORD_COLLECTIONS_MODIFY_LABELS_AND_TAGS_HELP
                    }
                  </p>
                  <div className="mt-5 space-y-6">
                    <CollectionEntitySelector
                      title={t.translations.SENSITIVITY_LABELS}
                      selectedItems={selectedNewCollectionLabels}
                      searchTerm={newCollectionLabelSearchTerm}
                      setSearchTerm={setNewCollectionLabelSearchTerm}
                      searchPlaceholder={
                        t.translations.RECORD_COLLECTIONS_SEARCH_OR_ADD_LABEL
                      }
                      options={filteredNewCollectionLabelOptions}
                      loading={labelsLoading}
                      loadingText={
                        t.translations.RECORD_COLLECTIONS_LOADING_LABELS
                      }
                      emptyOptionsText={
                        t.translations.RECORD_COLLECTIONS_NO_LABELS_FOUND
                      }
                      addDisabled={
                        !canAddTypedNewCollectionLabel ||
                        newCollectionLabelCreating
                      }
                      addButtonLoading={newCollectionLabelCreating}
                      selectedItemClassName={(item) =>
                        `badge badge-sm gap-1 ${getSensitivityClass(item.name)}`
                      }
                      addTypedItem={onAddNewCollectionLabelFromSearch}
                      selectOption={(item) =>
                        onAddNewCollectionLabel(Number(item.id))
                      }
                      removeItem={(item) =>
                        onRemoveNewCollectionLabel(Number(item.id))
                      }
                    />

                    <CollectionEntitySelector
                      title={t.translations.RECORD_COLLECTIONS_TAGS}
                      selectedItems={newCollectionSelectedTagNames.map(
                        (tag) => ({
                          id: tag,
                          name: tag,
                        }),
                      )}
                      searchTerm={newCollectionTagSearchTerm}
                      setSearchTerm={setNewCollectionTagSearchTerm}
                      searchPlaceholder={
                        t.translations.RECORD_COLLECTIONS_SEARCH_OR_ADD_TAG
                      }
                      options={filteredNewCollectionTagOptions}
                      loading={tagsLoading}
                      loadingText={
                        t.translations.RECORD_COLLECTIONS_LOADING_TAGS
                      }
                      emptyOptionsText={
                        t.translations.RECORD_COLLECTIONS_NO_TAGS_FOUND
                      }
                      addDisabled={!canAddTypedNewCollectionTag}
                      selectedItemClassName={() =>
                        "badge badge-sm badge-outline badge-secondary gap-1"
                      }
                      addTypedItem={() =>
                        onAddNewCollectionTag(newCollectionTagSearchTerm)
                      }
                      selectOption={(item) => onAddNewCollectionTag(item.name)}
                      removeItem={(item) => onRemoveNewCollectionTag(item.name)}
                    />
                  </div>
                </div>

                <div className="flex flex-wrap justify-end gap-2 self-end">
                  <button
                    type="button"
                    className="btn btn-ghost btn-sm"
                    onClick={onCancelToAllCollections}
                  >
                    {t.translations.CANCEL}
                  </button>
                  <button
                    type="button"
                    className="btn btn-outline btn-sm"
                    onClick={() => setNewCollectionStep("Metadata")}
                  >
                    {t.translations.BACK}
                  </button>
                  <button
                    type="button"
                    className="btn btn-primary btn-sm"
                    disabled={saving}
                    onClick={onGoToReviewStep}
                  >
                    {t.translations.NEXT}
                  </button>
                </div>
              </div>
            </>
          ) : null}

          {newCollectionStep === "Review" ? (
            <>
              <CollectionDetailsReadonlyView
                summaryPanel={reviewSummaryPanel}
                collection={{
                  id: "pending",
                  name:
                    newCollectionName.trim() ||
                    t.translations.RECORD_COLLECTIONS_UNTITLED_COLLECTION,
                  description: newCollectionDescription,
                  labels: selectedNewCollectionLabels,
                  tags: reviewTagItems,
                  properties: "{}",
                }}
                descriptionRef={reviewDescriptionRef}
                descriptionExpanded={reviewDescriptionExpanded}
                descriptionExpandable={reviewDescriptionExpandable}
                setDescriptionExpanded={setReviewDescriptionExpanded}
                visibleLabels={visibleReviewLabels}
                labelsExpanded={reviewLabelsExpanded}
                setLabelsExpanded={setReviewLabelsExpanded}
                visibleTags={visibleReviewTags}
                tagsExpanded={reviewTagsExpanded}
                setTagsExpanded={setReviewTagsExpanded}
                badgeDisplayLimit={10}
                getMetadataRows={() => []}
                getSensitivityClass={getSensitivityClass}
                showProperties={false}
                records={newCollectionSelectedRecords}
                filteredRecords={filteredNewCollectionSelectedRecords}
                visibleRecords={visibleNewCollectionReviewRecords}
                recordSearchTerm={newCollectionReviewSearchTerm}
                setRecordSearchTerm={setNewCollectionReviewSearchTerm}
                projectId={projectId}
                recordsPerPage={recordsPerPage}
                recordPage={newCollectionReviewPage}
                setRecordPage={setNewCollectionReviewPage}
                recordPageCount={newCollectionReviewPageCount}
                recordPageSizeOptions={recordPageSizeOptions}
                onRecordPageSizeChange={setRecordsPerPage}
              />

              <div className="flex justify-end rounded-2xl border border-base-300 bg-base-100 p-4">
                <div className="flex flex-wrap justify-end gap-2">
                  <button
                    type="button"
                    className="btn btn-ghost btn-sm"
                    onClick={onCancelToAllCollections}
                  >
                    {t.translations.CANCEL}
                  </button>
                  <button
                    type="button"
                    className="btn btn-outline btn-sm"
                    onClick={() => setNewCollectionStep("Modify")}
                  >
                    {t.translations.BACK}
                  </button>
                  <button
                    type="button"
                    className="btn btn-primary btn-sm"
                    disabled={saving}
                    onClick={() => void onCreateCollection()}
                  >
                    {saving ? (
                      <span className="loading loading-spinner loading-xs" />
                    ) : (
                      t.translations.RECORD_COLLECTIONS_SAVE_COLLECTION
                    )}
                  </button>
                </div>
              </div>
            </>
          ) : null}
        </div>
      </SectionCard>
    </div>
  );
}
