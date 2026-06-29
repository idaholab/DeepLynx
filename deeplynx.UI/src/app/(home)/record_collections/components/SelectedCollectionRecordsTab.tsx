"use client";

import { useLanguage } from "@/app/contexts/Language";
import Link from "next/link";
import React from "react";
import CollectionRecordSearchControls from "./CollectionRecordSearchControls";
import CollectionRecordSearchResultsTable from "./CollectionRecordSearchResultsTable";
import SectionCard from "./SectionCard";
import { interpolateTemplate } from "@/app/lib/record_helpers";
import type { CollectionDetailsController } from "../[collectionId]/hooks/useCollectionDetails";

type Props = {
  controller: CollectionDetailsController["recordsController"];
};

export default function SelectedCollectionRecordsTab({
  controller: {
    overview: {
      selectedCollection,
      projectId,
      collectionRecords,
      recordsLoading,
      classNameById,
      dataSourceNameById,
    },
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
  const { t } = useLanguage();

  return (
    <div className="mt-4">
      <SectionCard
        title={t.translations.RECORDS}
        subtitle={interpolateTemplate(
          t.translations.RECORD_COLLECTIONS_RECORDS_ASSIGNED_TO,
          { name: selectedCollection.name },
        )}
        action={
          <button
            type="button"
            className="btn btn-outline btn-sm"
            onClick={onBackToDetails}
          >
            {t.translations.RECORD_COLLECTIONS_BACK_TO_DETAILS}
          </button>
        }
      >
        <div className="rounded-2xl border border-base-300/50 bg-base-100 p-4">
          <CollectionRecordSearchControls
            searchTerm={recordSearchTerm}
            setSearchTerm={setRecordSearchTerm}
            placeholder={
              t.translations.RECORD_COLLECTIONS_SEARCH_RECORDS_TO_ADD
            }
            searchLoading={recordSearchLoading}
            onSearch={onSearchRecords}
            action={
              <button
                type="button"
                className="btn btn-primary"
                disabled={saving || selectedRecordIds.length === 0}
                onClick={onAddSelectedRecords}
              >
                {t.translations.RECORD_COLLECTIONS_ADD_SELECTED}
              </button>
            }
          />

          {recordSearchResults.length ? (
            <CollectionRecordSearchResultsTable
              rows={addableRecordResults.map((record) => {
                const classDisplayName =
                  record.className ??
                  (typeof record.classId === "number"
                    ? classNameById[record.classId]
                    : undefined) ??
                  record.classId ??
                  t.translations.RECORD_COLLECTIONS_UNCLASSIFIED;
                const sourceDisplayName =
                  record.dataSourceName ??
                  (typeof record.dataSourceId === "number"
                    ? dataSourceNameById[record.dataSourceId]
                    : undefined) ??
                  record.dataSourceId ??
                  t.translations.UNKNOWN;

                return {
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
                  className: classDisplayName,
                  sourceName: sourceDisplayName,
                  updatedAt: record.lastUpdatedAt,
                };
              })}
              emptyMessage={
                t.translations
                  .RECORD_COLLECTIONS_ALL_MATCHING_ALREADY_IN_THIS_COLLECTION
              }
              maxHeightClassName="max-h-fit"
              pinnedHeader={false}
            />
          ) : null}

          {recordSearchLoading ? (
            <div className="mt-3 flex items-center gap-2 text-sm text-base-content/70">
              <span className="loading loading-spinner loading-sm" />
              {t.translations.RECORD_COLLECTIONS_SEARCHING_RECORDS}
            </div>
          ) : null}
        </div>

        {recordsLoading ? (
          <div className="mt-4 flex items-center gap-2 rounded-xl border border-base-300/50 bg-base-100 p-4 text-sm text-base-content/70">
            <span className="loading loading-spinner loading-sm" />
            {t.translations.LOADING}
          </div>
        ) : (
          <CollectionRecordSearchResultsTable
            rows={collectionRecords.map((record) => {
              const classDisplayName =
                record.className ??
                (typeof record.classId === "number"
                  ? classNameById[record.classId]
                  : undefined) ??
                record.classId ??
                t.translations.RECORD_COLLECTIONS_UNCLASSIFIED;
              const sourceDisplayName =
                record.dataSourceName ??
                (typeof record.dataSourceId === "number"
                  ? dataSourceNameById[record.dataSourceId]
                  : undefined) ??
                record.dataSourceId ??
                t.translations.UNKNOWN;
              const recordName =
                record.name ?? t.translations.RECORD_COLLECTIONS_UNNAMED_RECORD;

              return {
                key: record.id ?? record.name,
                name: record.id ? (
                  <Link
                    href={`/record?recordId=${record.id}&projectId=${record.projectId ?? projectId}`}
                    className="link text-base-content hover:text-base-content/80"
                  >
                    {recordName}
                  </Link>
                ) : (
                  recordName
                ),
                className: classDisplayName,
                sourceName: sourceDisplayName,
                updatedAt: record.lastUpdatedAt,
              };
            })}
            emptyMessage={
              t.translations.RECORD_COLLECTIONS_NO_RECORDS_ARE_CURRENTLY_ASSIGNED
            }
            maxHeightClassName="max-h-fit"
            pinnedHeader={false}
          />
        )}
      </SectionCard>
    </div>
  );
}
