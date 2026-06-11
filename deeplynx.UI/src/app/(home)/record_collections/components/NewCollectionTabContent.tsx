"use client";

import PaginationControls from "@/app/(home)/components/PaginationControls";
import React from "react";
import CollectionDetailsReadonlyView from "./CollectionDetailsReadonlyView";
import CollectionEntitySelector from "./CollectionEntitySelector";
import CollectionFacetSummary from "./CollectionFacetSummary";
import CollectionRecordSearchControls from "./CollectionRecordSearchControls";
import CollectionRecordSearchResultsTable from "./CollectionRecordSearchResultsTable";
import NewCollectionStepIndicator from "./NewCollectionStepIndicator";
import SelectedRecordsPreviewPanel from "./SelectedRecordsPreviewPanel";
import SectionCard from "./SectionCard";
import { NewCollectionTabController } from "./componentTypes";

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
      allVisibleNewCollectionRecordsSelected,
      allRetrievedNewCollectionRecordsSelected,
      someVisibleNewCollectionRecordsSelected,
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
  const [reviewDescriptionExpanded, setReviewDescriptionExpanded] = React.useState(false);
  const [reviewDescriptionExpandable, setReviewDescriptionExpandable] = React.useState(false);
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

    const frameId = window.requestAnimationFrame(measureReviewDescriptionOverflow);
    window.addEventListener("resize", measureReviewDescriptionOverflow);

    return () => {
      window.cancelAnimationFrame(frameId);
      window.removeEventListener("resize", measureReviewDescriptionOverflow);
    };
  }, [newCollectionDescription, newCollectionStep]);

  const reviewTagItems = newCollectionSelectedTagNames.map((tagName, index) => ({
    id: `${tagName}-${index}`,
    name: tagName,
  }));
  const visibleReviewLabels = reviewLabelsExpanded
    ? selectedNewCollectionLabels
    : selectedNewCollectionLabels.slice(0, 10);
  const visibleReviewTags = reviewTagsExpanded
    ? reviewTagItems
    : reviewTagItems.slice(0, 10);
  const reviewSummaryPanel = (
    <div className="grid gap-4 rounded-2xl border border-base-300 bg-base-200/30 p-4 text-sm sm:grid-cols-2 lg:grid-cols-4">
      <div>
        <p className="text-base-content/60">Collection ID</p>
        <p className="font-semibold text-base-content">Pending</p>
      </div>
      <div>
        <p className="text-base-content/60">Total Records</p>
        <p className="font-semibold text-base-content">
          {newCollectionSelectedRecords.length}
        </p>
      </div>
      <div>
        <p className="text-base-content/60">Updated</p>
        <p className="font-semibold text-base-content">On create</p>
      </div>
      <div>
        <p className="text-base-content/60">Last Updated By</p>
        <p className="font-semibold text-base-content">Pending</p>
      </div>
    </div>
  );

  return (
    <div className="mt-4">
      <SectionCard
        title="New Collection"
        subtitle="Create a record collection in the active project."
      >
        <NewCollectionStepIndicator
          steps={[
            {
              label: "Step 1",
              detail: "Select Records",
              active: newCollectionStep === "Records",
            },
            {
              label: "Step 2",
              detail: "Add Metadata",
              active: newCollectionStep === "Metadata",
            },
            {
              label: "Step 3",
              detail: "Modify Labels and Tags",
              active: newCollectionStep === "Modify",
            },
            {
              label: "Step 4",
              detail: "Review",
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
                    placeholder="Search records to add"
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
                        Clear
                      </button>
                    }
                  />

                  <div className="mt-3 flex flex-wrap items-center gap-2 text-sm text-base-content/70">
                    <span>{newCollectionSelectedRecordIds.length} records selected</span>
                    <button
                      type="button"
                      className="btn btn-xs btn-outline"
                      disabled={
                        newCollectionRecordSearchLoading ||
                        newCollectionRecordSearchResults.length === 0 ||
                        allRetrievedNewCollectionRecordsSelected
                      }
                      onClick={() => void onSelectAllSearchedRecords()}
                    >
                      Select All
                    </button>
                    <button
                      type="button"
                      className="btn btn-xs btn-outline"
                      disabled={
                        newCollectionRecordSearchLoading ||
                        newCollectionRecordSearchResults.length === 0 ||
                        !allRetrievedNewCollectionRecordsSelected
                      }
                      onClick={onClearSelectedRecords}
                    >
                      Unselect All
                    </button>
                  </div>

                  {newCollectionRecordSearchResults.length ? (
                    <>
                      <CollectionRecordSearchResultsTable
                        rows={visibleNewCollectionRecords.map((record) => ({
                          key: record.id,
                          leadingCell: (
                            <input
                              type="checkbox"
                              className="checkbox checkbox-sm"
                              checked={newCollectionSelectedRecordIds.includes(record.id)}
                              onChange={() => void onToggleNewCollectionRecord(record)}
                            />
                          ),
                          name: record.name,
                          className: record.className ?? "Unclassified",
                          sourceName: record.dataSourceName ?? "Unknown",
                          updatedAt: record.lastUpdatedAt,
                        }))}
                        emptyMessage="No records found."
                        maxHeightClassName="max-h-fit"
                        pinnedHeader={false}
                        leadingHeaderCell={
                          <input
                            type="checkbox"
                            className="checkbox checkbox-sm"
                            checked={allVisibleNewCollectionRecordsSelected}
                            ref={(input) => {
                              if (input) {
                                input.indeterminate =
                                  !allVisibleNewCollectionRecordsSelected &&
                                  someVisibleNewCollectionRecordsSelected;
                              }
                            }}
                            onChange={() => void onToggleSelectAllVisibleRecords()}
                          />
                        }
                      />
                      {newCollectionRecordSearchResults.length > recordsPerPage ? (
                        <div className="flex flex-col gap-3 px-4 py-3 text-sm sm:flex-row sm:items-center sm:justify-between">
                          <span className="text-base-content/70">
                            Showing {(newCollectionRecordPage - 1) * recordsPerPage + 1}-
                            {Math.min(
                              newCollectionRecordPage * recordsPerPage,
                              newCollectionRecordSearchResults.length,
                            )}{" "}
                            of {newCollectionRecordSearchResults.length}
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
                      Searching records
                    </div>
                  ) : null}
                </div>
              </div>

              <div className="flex h-full flex-col justify-between gap-4">
                <div className="rounded-2xl border border-base-300 bg-base-100 p-5">
                  <h3 className="font-semibold text-base-content">
                    Selected Record Summary
                  </h3>
                  <div className="mt-2 flex flex-wrap items-center gap-2 text-sm text-base-content/70">
                    <span>{newCollectionSelectedRecords.length} Records Selected</span>
                    {newCollectionSelectedRecords.length > 0 ? (
                      confirmClearNewCollectionRecords ? (
                        <span className="flex flex-wrap items-center gap-2">
                          <span>Are you sure?</span>
                          <button
                            type="button"
                            className="btn btn-error btn-xs"
                            onClick={onClearSelectedRecords}
                          >
                            Yes
                          </button>
                          <button
                            type="button"
                            className="btn btn-ghost btn-xs"
                            onClick={() => setConfirmClearNewCollectionRecords(false)}
                          >
                            No
                          </button>
                        </span>
                      ) : (
                        <button
                          type="button"
                          className="btn btn-outline btn-xs"
                          onClick={() => setConfirmClearNewCollectionRecords(true)}
                        >
                          Clear
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
                  <button
                    type="button"
                    className="btn btn-ghost btn-sm"
                    onClick={onCancelToAllCollections}
                  >
                    Cancel
                  </button>
                  <button
                    type="button"
                    className="btn btn-primary btn-sm"
                    onClick={() => setNewCollectionStep("Metadata")}
                  >
                    Next
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
                          Collection Title
                        </span>
                      </div>
                      <input
                        type="text"
                        className="input input-bordered w-full"
                        value={newCollectionName}
                        onChange={(event) =>
                          setNewCollectionName(event.target.value)
                        }
                      />
                    </label>

                    <label className="form-control w-full">
                      <div className="label">
                        <span className="label-text font-medium">Description</span>
                      </div>
                      <textarea
                        className="textarea textarea-bordered min-h-32 w-full resize-y"
                        value={newCollectionDescription}
                        onChange={(event) =>
                          setNewCollectionDescription(event.target.value)
                        }
                      />
                    </label>

                    <SelectedRecordsPreviewPanel
                      title="Selected Records"
                      shownCount={filteredNewCollectionSelectedRecords.length}
                      totalCount={newCollectionSelectedRecords.length}
                      searchTerm={newCollectionReviewSearchTerm}
                      setSearchTerm={setNewCollectionReviewSearchTerm}
                      records={visibleNewCollectionReviewRecords}
                      emptyMessage="No selected records match this search."
                      currentPage={newCollectionReviewPage}
                      setCurrentPage={setNewCollectionReviewPage}
                      pageCount={newCollectionReviewPageCount}
                      pageSize={recordsPerPage}
                      pageSizeOptions={recordPageSizeOptions}
                      onPageSizeChange={setRecordsPerPage}
                    />
                  </div>
                </div>
              </div>

              <div className="flex h-full flex-col justify-between gap-4">
                <div className="rounded-2xl border border-base-300 bg-base-100 p-5">
                  <h3 className="font-semibold text-base-content">
                    Selected Labels and Tags
                  </h3>
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
                    Cancel
                  </button>
                  <button
                    type="button"
                    className="btn btn-outline btn-sm"
                    onClick={() => setNewCollectionStep("Records")}
                  >
                    Back
                  </button>
                  <button
                    type="button"
                    className="btn btn-primary btn-sm"
                    disabled={saving}
                    onClick={onGoToModifyStep}
                  >
                    Next
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
                          Collection Title
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
                        <span className="label-text font-medium">Description</span>
                      </div>
                      <textarea
                        className="textarea textarea-bordered min-h-32 w-full resize-y bg-base-200"
                        value={newCollectionDescription}
                        readOnly
                      />
                    </label>

                    <SelectedRecordsPreviewPanel
                      title="Selected Records"
                      shownCount={filteredNewCollectionSelectedRecords.length}
                      totalCount={newCollectionSelectedRecords.length}
                      searchTerm={newCollectionReviewSearchTerm}
                      setSearchTerm={setNewCollectionReviewSearchTerm}
                      records={visibleNewCollectionReviewRecords}
                      emptyMessage="No selected records match this search."
                      currentPage={newCollectionReviewPage}
                      setCurrentPage={setNewCollectionReviewPage}
                      pageCount={newCollectionReviewPageCount}
                      pageSize={recordsPerPage}
                      pageSizeOptions={recordPageSizeOptions}
                      onPageSizeChange={setRecordsPerPage}
                    />
                  </div>
                </div>
              </div>

              <div className="flex h-full flex-col justify-between gap-4">
                <div className="rounded-2xl border border-base-300 bg-base-100 p-5">
                  <h3 className="font-semibold text-base-content">
                    Modify Labels and Tags
                  </h3>
                  <div className="mt-5 space-y-6">
                    <CollectionEntitySelector
                      title="Sensitivity Labels"
                      selectedItems={selectedNewCollectionLabels}
                      searchTerm={newCollectionLabelSearchTerm}
                      setSearchTerm={setNewCollectionLabelSearchTerm}
                      searchPlaceholder="Search or add label"
                      options={filteredNewCollectionLabelOptions}
                      loading={labelsLoading}
                      loadingText="Loading labels"
                      emptyOptionsText="No labels found."
                      addDisabled={
                        !canAddTypedNewCollectionLabel || newCollectionLabelCreating
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
                      title="Tags"
                      selectedItems={newCollectionSelectedTagNames.map((tag) => ({
                        id: tag,
                        name: tag,
                      }))}
                      searchTerm={newCollectionTagSearchTerm}
                      setSearchTerm={setNewCollectionTagSearchTerm}
                      searchPlaceholder="Search or add tag"
                      options={filteredNewCollectionTagOptions}
                      loading={tagsLoading}
                      loadingText="Loading tags"
                      emptyOptionsText="No tags found."
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
                    Cancel
                  </button>
                  <button
                    type="button"
                    className="btn btn-outline btn-sm"
                    onClick={() => setNewCollectionStep("Metadata")}
                  >
                    Back
                  </button>
                  <button
                    type="button"
                    className="btn btn-primary btn-sm"
                    disabled={saving}
                    onClick={onGoToReviewStep}
                  >
                    Next
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
                  name: newCollectionName.trim() || "Untitled Collection",
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
                    Cancel
                  </button>
                  <button
                    type="button"
                    className="btn btn-outline btn-sm"
                    onClick={() => setNewCollectionStep("Modify")}
                  >
                    Back
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
                      "Save Collection"
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
