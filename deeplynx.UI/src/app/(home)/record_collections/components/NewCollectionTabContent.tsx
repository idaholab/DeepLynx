"use client";

import { formatLocalDateTime } from "@/app/lib/date_time";
import {
  MagnifyingGlassIcon,
  XCircleIcon,
} from "@heroicons/react/24/outline";
import React from "react";
import {
  HistoricalRecordResponseDto,
  RecordResponseDto,
  SensitivityLabelsDto,
  TagResponseDto,
} from "../../types/responseDTOs";
import CollectionDetailsReadonlyView from "./CollectionDetailsReadonlyView";
import NewCollectionStepIndicator from "./NewCollectionStepIndicator";
import SectionCard from "./SectionCard";

type NewCollectionStep = "Records" | "Metadata" | "Modify" | "Review";

type FacetOption = {
  label: string;
  count: number;
};

type NewCollectionSelectedRecord = HistoricalRecordResponseDto & {
  fullRecord?: RecordResponseDto;
};

type Props = {
  controller: {
    workflow: {
      projectId: number;
      newCollectionStep: NewCollectionStep;
      setNewCollectionStep: React.Dispatch<React.SetStateAction<NewCollectionStep>>;
      recordsPerPage: number;
      saving: boolean;
      getSensitivityClass: (label: string) => string;
    };
    metadata: {
      newCollectionName: string;
      setNewCollectionName: React.Dispatch<React.SetStateAction<string>>;
      newCollectionDescription: string;
      setNewCollectionDescription: React.Dispatch<React.SetStateAction<string>>;
    };
    recordSearch: {
      newCollectionRecordSearchTerm: string;
      setNewCollectionRecordSearchTerm: React.Dispatch<React.SetStateAction<string>>;
      newCollectionRecordSearchResults: HistoricalRecordResponseDto[];
      newCollectionRecordSearchLoading: boolean;
      visibleNewCollectionRecords: HistoricalRecordResponseDto[];
      allVisibleNewCollectionRecordsSelected: boolean;
      allRetrievedNewCollectionRecordsSelected: boolean;
      someVisibleNewCollectionRecordsSelected: boolean;
      newCollectionRecordPage: number;
      setNewCollectionRecordPage: React.Dispatch<React.SetStateAction<number>>;
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
      setConfirmClearNewCollectionRecords: React.Dispatch<React.SetStateAction<boolean>>;
      newCollectionSelectedLabelTally: FacetOption[];
      newCollectionSelectedTagTally: FacetOption[];
      onDeselectRecordsByLabel: (labelName: string) => void;
      onDeselectRecordsByTag: (tagName: string) => void;
      onClearSelectedRecords: () => void;
    };
    review: {
      newCollectionReviewSearchTerm: string;
      setNewCollectionReviewSearchTerm: React.Dispatch<React.SetStateAction<string>>;
      filteredNewCollectionSelectedRecords: NewCollectionSelectedRecord[];
      visibleNewCollectionReviewRecords: NewCollectionSelectedRecord[];
      newCollectionReviewPage: number;
      setNewCollectionReviewPage: React.Dispatch<React.SetStateAction<number>>;
      newCollectionReviewPageCount: number;
    };
    labelsAndTags: {
      selectedNewCollectionLabels: SensitivityLabelsDto[];
      newCollectionSelectedTagNames: string[];
      newCollectionLabelSearchTerm: string;
      setNewCollectionLabelSearchTerm: React.Dispatch<React.SetStateAction<string>>;
      newCollectionTagSearchTerm: string;
      setNewCollectionTagSearchTerm: React.Dispatch<React.SetStateAction<string>>;
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
};

function SelectedRecordsTable({
  records,
  emptyMessage,
  showSource = true,
}: {
  records: NewCollectionSelectedRecord[];
  emptyMessage: string;
  showSource?: boolean;
}) {
  return (
    <div className="mt-4 max-h-48 overflow-auto rounded-xl border border-base-300">
      <table className="table table-sm">
        <thead>
          <tr>
            <th>Record</th>
            <th>Class</th>
            {showSource ? <th>Source</th> : null}
            <th>Updated</th>
          </tr>
        </thead>
        <tbody>
          {records.length ? (
            records.map((record) => (
              <tr key={record.id}>
                <td className="font-medium">{record.name ?? "Unnamed record"}</td>
                <td>{record.className ?? "Unclassified"}</td>
                {showSource ? <td>{record.dataSourceName ?? "Unknown"}</td> : null}
                <td>
                  {record.lastUpdatedAt
                    ? formatLocalDateTime(record.lastUpdatedAt)
                    : "Not updated"}
                </td>
              </tr>
            ))
          ) : (
            <tr>
              <td colSpan={showSource ? 4 : 3}>{emptyMessage}</td>
            </tr>
          )}
        </tbody>
      </table>
    </div>
  );
}

function SelectedRecordsPagination({
  count,
  currentPage,
  setCurrentPage,
  pageCount,
  pageSize,
}: {
  count: number;
  currentPage: number;
  setCurrentPage: React.Dispatch<React.SetStateAction<number>>;
  pageCount: number;
  pageSize: number;
}) {
  return count > pageSize ? (
    <div className="mt-3 flex flex-col gap-3 text-sm sm:flex-row sm:items-center sm:justify-between">
      <span className="text-base-content/70">
        Showing {(currentPage - 1) * pageSize + 1}-
        {Math.min(currentPage * pageSize, count)} of {count}
      </span>
      <div className="join">
        <button
          type="button"
          className="btn btn-sm join-item"
          disabled={currentPage === 1}
          onClick={() => setCurrentPage((page) => Math.max(1, page - 1))}
        >
          Previous
        </button>
        <button
          type="button"
          className="btn btn-sm join-item"
          disabled={currentPage >= pageCount}
          onClick={() =>
            setCurrentPage((page) => Math.min(pageCount, page + 1))
          }
        >
          Next
        </button>
      </div>
    </div>
  ) : null;
}

export default function NewCollectionTabContent({
  controller: {
    workflow: {
      projectId,
      newCollectionStep,
      setNewCollectionStep,
      recordsPerPage,
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
                  <div className="flex flex-col gap-3 lg:flex-row">
                    <label className="input input-bordered flex min-w-0 flex-1 items-center gap-2">
                      <MagnifyingGlassIcon className="size-5 text-base-content/60" />
                      <input
                        type="text"
                        className="grow"
                        placeholder="Search records to add"
                        value={newCollectionRecordSearchTerm}
                        onChange={(event) =>
                          setNewCollectionRecordSearchTerm(event.target.value)
                        }
                        onKeyDown={(event) => {
                          if (event.key === "Enter") {
                            event.preventDefault();
                            void onSearchRecords();
                          }
                        }}
                      />
                    </label>
                    <button
                      type="button"
                      className="btn btn-outline"
                      disabled={newCollectionRecordSearchLoading}
                      onClick={() => void onSearchRecords()}
                    >
                      Search
                    </button>
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
                  </div>

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
                    <div className="mt-4 overflow-x-auto rounded-xl border border-base-300 bg-base-100">
                      <table className="table table-sm">
                        <thead>
                          <tr>
                            <th>
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
                                onChange={() =>
                                  void onToggleSelectAllVisibleRecords()
                                }
                              />
                            </th>
                            <th>Record</th>
                            <th>Class</th>
                            <th>Source</th>
                            <th>Updated</th>
                          </tr>
                        </thead>
                        <tbody>
                          {visibleNewCollectionRecords.map((record) => (
                            <tr key={record.id}>
                              <td>
                                <input
                                  type="checkbox"
                                  className="checkbox checkbox-sm"
                                  checked={newCollectionSelectedRecordIds.includes(
                                    record.id,
                                  )}
                                  onChange={() =>
                                    void onToggleNewCollectionRecord(record)
                                  }
                                />
                              </td>
                              <td className="font-medium">{record.name}</td>
                              <td>{record.className ?? "Unclassified"}</td>
                              <td>{record.dataSourceName ?? "Unknown"}</td>
                              <td>
                                {record.lastUpdatedAt
                                  ? formatLocalDateTime(record.lastUpdatedAt)
                                  : "Not updated"}
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>

                      {newCollectionRecordSearchResults.length > recordsPerPage ? (
                        <div className="flex flex-col gap-3 border-t border-base-300 px-4 py-3 text-sm sm:flex-row sm:items-center sm:justify-between">
                          <span className="text-base-content/70">
                            Showing {(newCollectionRecordPage - 1) * recordsPerPage + 1}-
                            {Math.min(
                              newCollectionRecordPage * recordsPerPage,
                              newCollectionRecordSearchResults.length,
                            )}{" "}
                            of {newCollectionRecordSearchResults.length}
                          </span>
                          <div className="join">
                            <button
                              type="button"
                              className="btn btn-sm join-item"
                              disabled={newCollectionRecordPage === 1}
                              onClick={() =>
                                setNewCollectionRecordPage((page) =>
                                  Math.max(1, page - 1),
                                )
                              }
                            >
                              Previous
                            </button>
                            <button
                              type="button"
                              className="btn btn-sm join-item"
                              disabled={
                                newCollectionRecordPage >=
                                newCollectionRecordPageCount
                              }
                              onClick={() =>
                                setNewCollectionRecordPage((page) =>
                                  Math.min(newCollectionRecordPageCount, page + 1),
                                )
                              }
                            >
                              Next
                            </button>
                          </div>
                        </div>
                      ) : null}
                    </div>
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
                    <div>
                      <p className="text-sm font-medium text-base-content">
                        Sensitivity Labels
                      </p>
                      <div className="mt-2 flex flex-wrap gap-2">
                        {newCollectionSelectedLabelTally.length ? (
                          newCollectionSelectedLabelTally.map((label) => (
                            <span
                              key={label.label}
                              className={`badge badge-sm gap-1 ${getSensitivityClass(
                                label.label,
                              )}`}
                            >
                              {label.label} ({label.count})
                              <button
                                type="button"
                                className="group ml-1 rounded-full px-1 leading-none text-base-content/70 transition-colors hover:bg-base-100/70 hover:text-error focus-visible:bg-base-100/70 focus-visible:text-error"
                                onClick={() =>
                                  onDeselectRecordsByLabel(label.label)
                                }
                                title={`Deselect records with ${label.label}`}
                              >
                                <XCircleIcon
                                  className="size-4 transition-colors group-hover:text-error group-focus-visible:text-error"
                                  aria-hidden="true"
                                />
                              </button>
                            </span>
                          ))
                        ) : null}
                      </div>
                    </div>

                    <div>
                      <p className="text-sm font-medium text-base-content">
                        Tags
                      </p>
                      <div className="mt-2 flex flex-wrap gap-2">
                        {newCollectionSelectedTagTally.length ? (
                          newCollectionSelectedTagTally.map((tag) => (
                            <span
                              key={tag.label}
                              className="badge badge-sm badge-outline badge-secondary gap-1"
                            >
                              {tag.label} ({tag.count})
                              <button
                                type="button"
                                className="group ml-1 rounded-full px-1 leading-none text-base-content/70 transition-colors hover:bg-base-100/70 hover:text-error focus-visible:bg-base-100/70 focus-visible:text-error"
                                onClick={() =>
                                  onDeselectRecordsByTag(tag.label)
                                }
                                title={`Deselect records with ${tag.label}`}
                              >
                                <XCircleIcon
                                  className="size-4 transition-colors group-hover:text-error group-focus-visible:text-error"
                                  aria-hidden="true"
                                />
                              </button>
                            </span>
                          ))
                        ) : null}
                      </div>
                    </div>
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

                    <div className="mt-2 rounded-2xl border border-base-300 bg-base-100 p-4">
                      <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
                        <div>
                          <h3 className="font-semibold text-base-content">
                            Selected Records
                          </h3>
                          <p className="text-sm text-base-content/70">
                            {filteredNewCollectionSelectedRecords.length} of{" "}
                            {newCollectionSelectedRecords.length} records shown
                          </p>
                        </div>
                        <label className="input input-bordered input-sm flex min-w-0 items-center gap-2 lg:w-72">
                          <MagnifyingGlassIcon className="size-4 text-base-content/60" />
                          <input
                            type="text"
                            className="grow"
                            placeholder="Search selected records"
                            value={newCollectionReviewSearchTerm}
                            onChange={(event) =>
                              setNewCollectionReviewSearchTerm(event.target.value)
                            }
                          />
                        </label>
                      </div>

                      <SelectedRecordsTable
                        records={visibleNewCollectionReviewRecords}
                        emptyMessage="No selected records match this search."
                      />
                      <SelectedRecordsPagination
                        count={filteredNewCollectionSelectedRecords.length}
                        currentPage={newCollectionReviewPage}
                        setCurrentPage={setNewCollectionReviewPage}
                        pageCount={newCollectionReviewPageCount}
                        pageSize={recordsPerPage}
                      />
                    </div>
                  </div>
                </div>
              </div>

              <div className="flex h-full flex-col justify-between gap-4">
                <div className="rounded-2xl border border-base-300 bg-base-100 p-5">
                  <h3 className="font-semibold text-base-content">
                    Selected Labels and Tags
                  </h3>
                  <div className="mt-5 space-y-5">
                    <div>
                      <p className="text-sm font-medium text-base-content">
                        Sensitivity Labels
                      </p>
                      <div className="mt-2 flex flex-wrap gap-2">
                        {newCollectionSelectedLabelTally.length ? (
                          newCollectionSelectedLabelTally.map((label) => (
                            <span
                              key={label.label}
                              className={`badge badge-sm ${getSensitivityClass(
                                label.label,
                              )}`}
                            >
                              {label.label} ({label.count})
                            </span>
                          ))
                        ) : null}
                      </div>
                    </div>

                    <div>
                      <p className="text-sm font-medium text-base-content">
                        Tags
                      </p>
                      <div className="mt-2 flex flex-wrap gap-2">
                        {newCollectionSelectedTagTally.length ? (
                          newCollectionSelectedTagTally.map((tag) => (
                            <span
                              key={tag.label}
                              className="badge badge-sm badge-outline badge-secondary"
                            >
                              {tag.label} ({tag.count})
                            </span>
                          ))
                        ) : null}
                      </div>
                    </div>
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

                    <div className="mt-2 rounded-2xl border border-base-300 bg-base-100 p-4">
                      <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
                        <div>
                          <h3 className="font-semibold text-base-content">
                            Selected Records
                          </h3>
                          <p className="text-sm text-base-content/70">
                            {filteredNewCollectionSelectedRecords.length} of{" "}
                            {newCollectionSelectedRecords.length} records shown
                          </p>
                        </div>
                        <label className="input input-bordered input-sm flex min-w-0 items-center gap-2 lg:w-72">
                          <MagnifyingGlassIcon className="size-4 text-base-content/60" />
                          <input
                            type="text"
                            className="grow"
                            placeholder="Search selected records"
                            value={newCollectionReviewSearchTerm}
                            onChange={(event) =>
                              setNewCollectionReviewSearchTerm(event.target.value)
                            }
                          />
                        </label>
                      </div>

                      <SelectedRecordsTable
                        records={visibleNewCollectionReviewRecords}
                        emptyMessage="No selected records match this search."
                      />
                      <SelectedRecordsPagination
                        count={filteredNewCollectionSelectedRecords.length}
                        currentPage={newCollectionReviewPage}
                        setCurrentPage={setNewCollectionReviewPage}
                        pageCount={newCollectionReviewPageCount}
                        pageSize={recordsPerPage}
                      />
                    </div>
                  </div>
                </div>
              </div>

              <div className="flex h-full flex-col justify-between gap-4">
                <div className="rounded-2xl border border-base-300 bg-base-100 p-5">
                  <h3 className="font-semibold text-base-content">
                    Modify Labels and Tags
                  </h3>
                  <div className="mt-5 space-y-6">
                    <div>
                      <p className="text-sm font-medium text-base-content">
                        Sensitivity Labels
                      </p>
                      <div className="mt-2 flex flex-wrap gap-2">
                        {selectedNewCollectionLabels.length ? (
                          selectedNewCollectionLabels.map((label) => (
                            <span
                              key={label.id}
                              className={`badge badge-sm gap-1 ${getSensitivityClass(
                                label.name,
                              )}`}
                            >
                              {label.name}
                              <button
                                type="button"
                                className="group ml-1 rounded-full px-1 leading-none text-base-content/70 transition-colors hover:bg-base-100/70 hover:text-error focus-visible:bg-base-100/70 focus-visible:text-error"
                                onClick={() =>
                                  onRemoveNewCollectionLabel(label.id)
                                }
                                title={`Remove ${label.name}`}
                              >
                                <XCircleIcon
                                  className="size-4 transition-colors group-hover:text-error group-focus-visible:text-error"
                                  aria-hidden="true"
                                />
                              </button>
                            </span>
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
                            value={newCollectionLabelSearchTerm}
                            onChange={(event) =>
                              setNewCollectionLabelSearchTerm(event.target.value)
                            }
                            onKeyDown={(event) => {
                              if (event.key === "Enter") {
                                event.preventDefault();
                                void onAddNewCollectionLabelFromSearch();
                              }
                            }}
                          />
                        </label>
                        <button
                          type="button"
                          className="btn btn-primary btn-sm"
                          disabled={
                            !canAddTypedNewCollectionLabel ||
                            newCollectionLabelCreating
                          }
                          onClick={() => void onAddNewCollectionLabelFromSearch()}
                        >
                          {newCollectionLabelCreating ? (
                            <span className="loading loading-spinner loading-xs" />
                          ) : (
                            "Add"
                          )}
                        </button>
                      </div>
                      <div className="mt-3 max-h-48 space-y-2 overflow-auto rounded-xl border border-base-300 bg-base-200/30 p-3">
                        {labelsLoading ? (
                          <div className="flex items-center gap-2 text-sm text-base-content/70">
                            <span className="loading loading-spinner loading-sm" />
                            Loading labels
                          </div>
                        ) : filteredNewCollectionLabelOptions.length ? (
                          filteredNewCollectionLabelOptions.map((label) => (
                            <button
                              type="button"
                              key={label.id}
                              className="flex w-full items-center justify-between rounded-lg px-2 py-1 text-left text-sm hover:bg-base-200"
                              onClick={() => onAddNewCollectionLabel(label.id)}
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
                      <p className="text-sm font-medium text-base-content">
                        Tags
                      </p>
                      <div className="mt-2 flex flex-wrap gap-2">
                        {newCollectionSelectedTagNames.length ? (
                          newCollectionSelectedTagNames.map((tag) => (
                            <span
                              key={tag}
                              className="badge badge-sm badge-outline badge-secondary gap-1"
                            >
                              {tag}
                              <button
                                type="button"
                                className="group ml-1 rounded-full px-1 leading-none text-base-content/70 transition-colors hover:bg-base-100/70 hover:text-error focus-visible:bg-base-100/70 focus-visible:text-error"
                                onClick={() => onRemoveNewCollectionTag(tag)}
                                title={`Remove ${tag}`}
                              >
                                <XCircleIcon
                                  className="size-4 transition-colors group-hover:text-error group-focus-visible:text-error"
                                  aria-hidden="true"
                                />
                              </button>
                            </span>
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
                            value={newCollectionTagSearchTerm}
                            onChange={(event) =>
                              setNewCollectionTagSearchTerm(event.target.value)
                            }
                            onKeyDown={(event) => {
                              if (event.key === "Enter") {
                                event.preventDefault();
                                onAddNewCollectionTag(newCollectionTagSearchTerm);
                              }
                            }}
                          />
                        </label>
                        <button
                          type="button"
                          className="btn btn-primary btn-sm"
                          disabled={!canAddTypedNewCollectionTag}
                          onClick={() =>
                            onAddNewCollectionTag(newCollectionTagSearchTerm)
                          }
                        >
                          Add
                        </button>
                      </div>
                      <div className="mt-3 max-h-48 space-y-2 overflow-auto rounded-xl border border-base-300 bg-base-200/30 p-3">
                        {tagsLoading ? (
                          <div className="flex items-center gap-2 text-sm text-base-content/70">
                            <span className="loading loading-spinner loading-sm" />
                            Loading tags
                          </div>
                        ) : filteredNewCollectionTagOptions.length ? (
                          filteredNewCollectionTagOptions.map((tag) => (
                            <button
                              type="button"
                              key={tag.id}
                              className="flex w-full items-center justify-between rounded-lg px-2 py-1 text-left text-sm hover:bg-base-200"
                              onClick={() => onAddNewCollectionTag(tag.name)}
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
