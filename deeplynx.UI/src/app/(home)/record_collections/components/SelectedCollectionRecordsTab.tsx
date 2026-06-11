"use client";

import Link from "next/link";
import React from "react";
import { formatLocalDateTime } from "@/app/lib/date_time";
import CollectionRecordSearchControls from "./CollectionRecordSearchControls";
import CollectionRecordSearchResultsTable from "./CollectionRecordSearchResultsTable";
import SectionCard from "./SectionCard";
import { SelectedCollectionRecordsController } from "./componentTypes";

type Props = {
  controller: SelectedCollectionRecordsController;
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
          <CollectionRecordSearchControls
            searchTerm={recordSearchTerm}
            setSearchTerm={setRecordSearchTerm}
            placeholder="Search records to add"
            searchLoading={recordSearchLoading}
            onSearch={onSearchRecords}
            action={
              <button
                type="button"
                className="btn btn-primary"
                disabled={saving || selectedRecordIds.length === 0}
                onClick={onAddSelectedRecords}
              >
                Add selected
              </button>
            }
          />

          {recordSearchResults.length ? (
            <CollectionRecordSearchResultsTable
              rows={addableRecordResults.map((record) => ({
                key: record.id ?? record.name,
                leadingCell: (
                  <input
                    type="checkbox"
                    className="checkbox checkbox-sm"
                    checked={
                      typeof record.id === "number" &&
                      selectedRecordIds.includes(record.id)
                    }
                    disabled={typeof record.id !== "number"}
                    onChange={() => {
                      if (typeof record.id === "number") {
                        onToggleSelectedRecord(record.id);
                      }
                    }}
                  />
                ),
                name: record.name,
                className: record.className ?? "Unclassified",
                sourceName: record.dataSourceName ?? "Unknown",
                updatedAt: record.lastUpdatedAt,
              }))}
              emptyMessage="All matching records are already in this collection."
              maxHeightClassName="max-h-fit"
              pinnedHeader={false}
            />
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
