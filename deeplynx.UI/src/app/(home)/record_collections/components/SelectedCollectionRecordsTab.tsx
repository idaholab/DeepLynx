"use client";

import Link from "next/link";
import { MagnifyingGlassIcon } from "@heroicons/react/24/outline";
import React from "react";
import { formatLocalDateTime } from "@/app/lib/date_time";
import SectionCard from "./SectionCard";
import {
  HistoricalRecordResponseDto,
  RecordCollectionResponseDto,
  RecordResponseDto,
} from "../../types/responseDTOs";

type Props = {
  controller: {
    overview: {
      selectedCollection: RecordCollectionResponseDto;
      projectId: number;
      collectionRecords: RecordResponseDto[];
      recordsLoading: boolean;
    };
    search: {
      recordSearchTerm: string;
      setRecordSearchTerm: React.Dispatch<React.SetStateAction<string>>;
      recordSearchLoading: boolean;
      recordSearchResults: HistoricalRecordResponseDto[];
      addableRecordResults: HistoricalRecordResponseDto[];
      onSearchRecords: () => void;
    };
    selection: {
      saving: boolean;
      selectedRecordIds: number[];
      onToggleSelectedRecord: (recordId: number) => void;
      onAddSelectedRecords: () => void;
    };
    actions: {
      onBackToDetails: () => void;
    };
  };
};

export default function SelectedCollectionRecordsTab({
  controller: {
    overview: { selectedCollection, projectId, collectionRecords, recordsLoading },
    search: {
      recordSearchTerm,
      setRecordSearchTerm,
      recordSearchLoading,
      recordSearchResults,
      addableRecordResults,
      onSearchRecords,
    },
    selection: {
      saving,
      selectedRecordIds,
      onToggleSelectedRecord,
      onAddSelectedRecords,
    },
    actions: { onBackToDetails },
  },
}: Props) {
  return (
    <div className="mt-4">
      <SectionCard
        title="Records"
        subtitle={`Records currently assigned to ${selectedCollection.name}.`}
        action={
          <button
            type="button"
            className="btn btn-outline btn-sm"
            onClick={onBackToDetails}
          >
            Back to details
          </button>
        }
      >
        <div className="rounded-2xl border border-base-300 bg-base-200/30 p-4">
          <div className="flex flex-col gap-3 lg:flex-row">
            <label className="input input-bordered flex min-w-0 flex-1 items-center gap-2">
              <MagnifyingGlassIcon className="size-5 text-base-content/60" />
              <input
                type="text"
                className="grow"
                placeholder="Search records to add"
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
            <button
              type="button"
              className="btn btn-primary"
              disabled={saving || selectedRecordIds.length === 0}
              onClick={onAddSelectedRecords}
            >
              Add selected
            </button>
          </div>

          {recordSearchResults.length ? (
            <div className="mt-4 overflow-x-auto rounded-xl border border-base-300 bg-base-100">
              <table className="table table-sm">
                <thead>
                  <tr>
                    <th></th>
                    <th>Record</th>
                    <th>Class</th>
                    <th>Source</th>
                    <th>Updated</th>
                  </tr>
                </thead>
                <tbody>
                  {addableRecordResults.length ? (
                    addableRecordResults.map((record) => (
                      <tr key={record.id}>
                        <td>
                          <input
                            type="checkbox"
                            className="checkbox checkbox-sm"
                            checked={selectedRecordIds.includes(record.id)}
                            onChange={() => onToggleSelectedRecord(record.id)}
                          />
                        </td>
                        <td className="font-medium">{record.name}</td>
                        <td>{record.className ?? "Unclassified"}</td>
                        <td>{record.dataSourceName ?? "Unknown"}</td>
                        <td>{formatLocalDateTime(record.lastUpdatedAt)}</td>
                      </tr>
                    ))
                  ) : (
                    <tr>
                      <td colSpan={5}>
                        All matching records are already in this collection.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          ) : null}

          {recordSearchLoading ? (
            <div className="mt-3 flex items-center gap-2 text-sm text-base-content/70">
              <span className="loading loading-spinner loading-sm" />
              Searching records
            </div>
          ) : null}
        </div>

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
              ) : collectionRecords.length ? (
                collectionRecords.map((record) => (
                  <tr key={record.id ?? record.name}>
                    <td className="font-medium">
                      {record.id ? (
                        <Link
                          href={`/record?recordId=${record.id}&projectId=${record.projectId ?? projectId}`}
                          className="link link-primary"
                        >
                          {record.name}
                        </Link>
                      ) : (
                        record.name
                      )}
                    </td>
                    <td>{record.classId ?? "Unclassified"}</td>
                    <td>{record.projectId ?? projectId}</td>
                    <td>
                      {record.lastUpdatedAt
                        ? formatLocalDateTime(record.lastUpdatedAt)
                        : "Not updated"}
                    </td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td colSpan={4}>No records are currently assigned.</td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </SectionCard>
    </div>
  );
}
