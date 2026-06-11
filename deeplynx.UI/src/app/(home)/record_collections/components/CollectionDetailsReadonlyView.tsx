"use client";

import Link from "next/link";
import React from "react";
import { formatLocalDateTime } from "@/app/lib/date_time";
import { MagnifyingGlassIcon } from "@heroicons/react/24/outline";
import SectionCard from "./SectionCard";

type MetadataRow = {
  label: string;
  value: string;
};

type NamedItem = {
  id: number | string;
  name: string;
};

type CollectionRecordLike = {
  id?: number | null;
  name?: string | null;
  classId?: string | number | null;
  className?: string | null;
  projectId?: number | null;
  lastUpdatedAt?: string | null;
};

type Props = {
  summaryPanel?: React.ReactNode;
  collection: {
    id: number | string;
    name: string;
    description?: string | null;
    labels?: NamedItem[];
    tags?: NamedItem[];
    properties?: string | null;
  };
  primaryAction?: React.ReactNode;
  descriptionRef: React.RefObject<HTMLParagraphElement | null>;
  descriptionExpanded: boolean;
  descriptionExpandable: boolean;
  setDescriptionExpanded: React.Dispatch<React.SetStateAction<boolean>>;
  visibleLabels: NamedItem[];
  labelsExpanded: boolean;
  setLabelsExpanded: React.Dispatch<React.SetStateAction<boolean>>;
  visibleTags: NamedItem[];
  tagsExpanded: boolean;
  setTagsExpanded: React.Dispatch<React.SetStateAction<boolean>>;
  badgeDisplayLimit: number;
  getMetadataRows: (properties?: string | null) => MetadataRow[];
  getSensitivityClass: (label: string) => string;
  records: CollectionRecordLike[];
  filteredRecords: CollectionRecordLike[];
  visibleRecords: CollectionRecordLike[];
  recordsLoading?: boolean;
  recordSearchTerm: string;
  setRecordSearchTerm: React.Dispatch<React.SetStateAction<string>>;
  projectId: number;
  recordsSectionAction?: React.ReactNode;
  recordsPerPage: number;
  recordPage: number;
  setRecordPage: React.Dispatch<React.SetStateAction<number>>;
  recordPageCount: number;
};

export default function CollectionDetailsReadonlyView({
  summaryPanel,
  collection,
  primaryAction,
  descriptionRef,
  descriptionExpanded,
  descriptionExpandable,
  setDescriptionExpanded,
  visibleLabels,
  labelsExpanded,
  setLabelsExpanded,
  visibleTags,
  tagsExpanded,
  setTagsExpanded,
  badgeDisplayLimit,
  getMetadataRows,
  getSensitivityClass,
  records,
  filteredRecords,
  visibleRecords,
  recordsLoading = false,
  recordSearchTerm,
  setRecordSearchTerm,
  projectId,
  recordsSectionAction,
  recordsPerPage,
  recordPage,
  setRecordPage,
  recordPageCount,
}: Props) {
  const collectionLabels = collection.labels ?? [];
  const collectionTags = collection.tags ?? [];

  return (
    <div className="space-y-4">
      {summaryPanel}
      <SectionCard title={collection.name} action={primaryAction}>
        <div className="space-y-5">
          <div className="max-w-5xl">
            <p
              ref={descriptionRef}
              className={`whitespace-pre-wrap text-sm leading-6 text-base-content/75 ${descriptionExpanded ? "" : "line-clamp-10"}`}
            >
              {collection.description || "No description provided."}
            </p>
            {collection.description &&
            (descriptionExpanded || descriptionExpandable) ? (
              <button
                type="button"
                className="btn btn-ghost btn-xs mt-2 px-0"
                onClick={() => setDescriptionExpanded((expanded) => !expanded)}
              >
                {descriptionExpanded ? "Show less" : "Show more"}
              </button>
            ) : null}
          </div>

          <div className="space-y-3 text-sm">
            <div className="flex flex-col gap-2 sm:flex-row sm:items-start">
              <span className="min-w-36 font-semibold text-base-content">
                Sensitivity Labels:
              </span>
              <div className="flex flex-wrap gap-2">
                {collectionLabels.length ? (
                  <>
                    {visibleLabels.map((label) => (
                      <span
                        key={`${collection.id}-${label.id}`}
                        className={`badge badge-sm ${getSensitivityClass(label.name)}`}
                      >
                        {label.name}
                      </span>
                    ))}
                    {collectionLabels.length > badgeDisplayLimit ? (
                      <button
                        type="button"
                        className="btn btn-ghost btn-xs px-1"
                        onClick={() => setLabelsExpanded((expanded) => !expanded)}
                      >
                        {labelsExpanded ? "Show less" : "Show more"}
                      </button>
                    ) : null}
                  </>
                ) : null}
              </div>
            </div>
            <div className="flex flex-col gap-2 sm:flex-row sm:items-start">
              <span className="min-w-36 font-semibold text-base-content">
                Tags:
              </span>
              <div className="flex flex-wrap gap-2">
                {collectionTags.length ? (
                  <>
                    {visibleTags.map((tag) => (
                      <span
                        key={`${collection.id}-${tag.id}`}
                        className="badge badge-sm badge-outline badge-secondary"
                      >
                        {tag.name}
                      </span>
                    ))}
                    {collectionTags.length > badgeDisplayLimit ? (
                      <button
                        type="button"
                        className="btn btn-ghost btn-xs px-1"
                        onClick={() => setTagsExpanded((expanded) => !expanded)}
                      >
                        {tagsExpanded ? "Show less" : "Show more"}
                      </button>
                    ) : null}
                  </>
                ) : null}
              </div>
            </div>
          </div>

          <div className="rounded-2xl border border-base-300 bg-base-100 p-5">
            <h3 className="font-semibold text-base-content">
              Additional Properties
            </h3>
            <div className="mt-4 max-h-[17.5rem] overflow-auto pr-1">
              <table className="table table-pin-rows">
                <thead className="bg-base-100">
                  <tr>
                    <th>Field</th>
                    <th>Value</th>
                  </tr>
                </thead>
                <tbody>
                  {getMetadataRows(collection.properties).length ? (
                    getMetadataRows(collection.properties).map((row) => (
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
      </SectionCard>

      <SectionCard
        title="Records"
        subtitle={`${filteredRecords.length} of ${records.length} assigned records shown.`}
        action={recordsSectionAction}
      >
        <label className="input input-bordered flex w-full items-center gap-2">
          <MagnifyingGlassIcon className="size-5 text-base-content/60" />
          <input
            type="text"
            className="grow"
            placeholder="Search records in this collection"
            value={recordSearchTerm}
            onChange={(event) => setRecordSearchTerm(event.target.value)}
          />
        </label>

        <div className="overflow-x-auto rounded-2xl border border-base-300">
          <table className="table">
            <thead>
              <tr>
                <th>Record</th>
                <th>Class</th>
                <th>Project</th>
                <th>Updated</th>
              </tr>
            </thead>
            <tbody>
              {recordsLoading ? (
                <tr>
                  <td colSpan={4}>
                    <span className="loading loading-spinner loading-sm" />
                  </td>
                </tr>
              ) : visibleRecords.length ? (
                visibleRecords.map((record) => (
                  <tr key={record.id ?? record.name}>
                    <td className="font-medium">
                      {record.id ? (
                        <Link
                          href={`/record?recordId=${record.id}&projectId=${record.projectId ?? projectId}`}
                          className="link link-primary"
                        >
                          {record.name ?? "Unnamed record"}
                        </Link>
                      ) : (
                        record.name ?? "Unnamed record"
                      )}
                    </td>
                    <td>{record.classId ?? record.className ?? "Unclassified"}</td>
                    <td>{record.projectId ?? projectId}</td>
                    <td>
                      {record.lastUpdatedAt
                        ? formatLocalDateTime(record.lastUpdatedAt)
                        : "Not Updated"}
                    </td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td colSpan={4}>
                    {records.length
                      ? "No records match this search."
                      : "No records are currently assigned."}
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>

        {filteredRecords.length > recordsPerPage ? (
          <div className="flex flex-col gap-3 text-sm sm:flex-row sm:items-center sm:justify-between">
            <span className="text-base-content/70">
              Showing {(recordPage - 1) * recordsPerPage + 1}-
              {Math.min(recordPage * recordsPerPage, filteredRecords.length)} of{" "}
              {filteredRecords.length}
            </span>
            <div className="join">
              <button
                type="button"
                className="btn btn-sm join-item"
                disabled={recordPage === 1}
                onClick={() => setRecordPage((page) => Math.max(1, page - 1))}
              >
                Previous
              </button>
              <button
                type="button"
                className="btn btn-sm join-item"
                disabled={recordPage >= recordPageCount}
                onClick={() =>
                  setRecordPage((page) => Math.min(recordPageCount, page + 1))
                }
              >
                Next
              </button>
            </div>
          </div>
        ) : null}
      </SectionCard>
    </div>
  );
}
