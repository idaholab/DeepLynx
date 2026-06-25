"use client";

import PaginationControls from "@/app/(home)/components/PaginationControls";
import SearchInput from "@/app/(home)/components/SearchInput";
import { useLanguage } from "@/app/contexts/Language";
import Link from "next/link";
import React from "react";
import { formatLocalDateTime } from "@/app/lib/date_time";
import { MetadataRow } from "./recordCollections.types";
import SectionCard from "./SectionCard";
import { interpolateTemplate } from "@/app/lib/record_helpers";

type NamedItem = {
  id: number | string;
  name: string;
};

type CollectionRecordLike = {
  id?: number | null;
  name?: string | null;
  classId?: string | number | null;
  className?: string | null;
  dataSourceId?: string | number | null;
  dataSourceName?: string | null;
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
  showProperties?: boolean;
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
  recordPageSizeOptions?: number[];
  onRecordPageSizeChange?: (pageSize: number) => void;
  recordsSectionBordered?: boolean;
  recordsSectionElevated?: boolean;
  recordsSectionClassName?: string;
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
  showProperties = true,
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
  recordPageSizeOptions,
  onRecordPageSizeChange,
  recordsSectionBordered,
  recordsSectionElevated,
  recordsSectionClassName,
}: Props) {
  const { t } = useLanguage();
  const collectionLabels = collection.labels ?? [];
  const collectionTags = collection.tags ?? [];

  return (
    <div className="space-y-4">
      {summaryPanel}
      <SectionCard
        title={collection.name}
        action={primaryAction}
        bodyClassName="gap-2"
      >
        <div className="space-y-4">
          <div className="max-w-5xl">
            <p
              ref={descriptionRef}
              className={`whitespace-pre-wrap text-sm leading-5 text-base-content/75 ${descriptionExpanded ? "" : "line-clamp-8"}`}
            >
              {collection.description ||
                t.translations.RECORD_COLLECTIONS_NO_DESCRIPTION_PROVIDED}
            </p>
            {collection.description &&
            (descriptionExpanded || descriptionExpandable) ? (
              <button
                type="button"
                className="btn btn-ghost btn-xs mt-2 px-0"
                onClick={() => setDescriptionExpanded((expanded) => !expanded)}
              >
                {descriptionExpanded
                  ? t.translations.SHOW_LESS
                  : t.translations.RECORD_COLLECTIONS_SHOW_MORE}
              </button>
            ) : null}
          </div>

          <div className="space-y-3 text-sm">
            <div className="flex flex-col gap-2 sm:flex-row sm:items-start">
              <span className="min-w-36 font-semibold text-base-content">
                {t.translations.SENSITIVITY_LABELS}
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
                        {labelsExpanded
                          ? t.translations.SHOW_LESS
                          : t.translations.RECORD_COLLECTIONS_SHOW_MORE}
                      </button>
                    ) : null}
                  </>
                ) : null}
              </div>
            </div>
            <div className="flex flex-col gap-2 sm:flex-row sm:items-start">
              <span className="min-w-36 font-semibold text-base-content">
                {t.translations.TAGS}
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
                        {tagsExpanded
                          ? t.translations.SHOW_LESS
                          : t.translations.RECORD_COLLECTIONS_SHOW_MORE}
                      </button>
                    ) : null}
                  </>
                ) : null}
              </div>
            </div>
          </div>

          {showProperties ? (
            <div className="rounded-2xl border border-base-300/50 bg-base-100 p-5">
              <h3 className="font-semibold text-base-content">
                {t.translations.RECORD_COLLECTIONS_ADDITIONAL_PROPERTIES}
              </h3>
              <div className="mt-4 max-h-[17.5rem] overflow-auto pr-1">
                <table className="table table-pin-rows">
                  <thead className="bg-base-100">
                    <tr>
                      <th>{t.translations.RECORD_COLLECTIONS_FIELD}</th>
                      <th>{t.translations.RECORD_COLLECTIONS_VALUE}</th>
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
                        <td colSpan={2}>
                          {t.translations.RECORD_COLLECTIONS_NO_ADDITIONAL_PROPERTIES_SET}
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>
            </div>
          ) : null}
        </div>
      </SectionCard>

      <SectionCard
        title={t.translations.RECORDS}
        subtitle={interpolateTemplate(
          t.translations.RECORD_COLLECTIONS_ASSIGNED_RECORDS_SHOWN,
          { shown: filteredRecords.length, total: records.length },
        )}
        action={recordsSectionAction}
        bordered={recordsSectionBordered}
        elevated={recordsSectionElevated}
        className={recordsSectionClassName}
      >
        <SearchInput
          placeholder={t.translations.RECORD_COLLECTIONS_SEARCH_IN_THIS_COLLECTION}
          value={recordSearchTerm}
          onChange={(event) => setRecordSearchTerm(event.target.value)}
        />

        <div className="overflow-x-auto rounded-2xl border border-base-300/50">
          <table className="table">
            <thead>
              <tr>
                <th>{t.translations.RECORD}</th>
                <th>{t.translations.RECORD_COLLECTIONS_CLASS}</th>
                <th>{t.translations.PROJECT}</th>
                <th>{t.translations.RECORD_COLLECTIONS_UPDATED}</th>
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
                          className="link text-base-content hover:text-base-content/80"
                        >
                          {record.name ??
                            t.translations.RECORD_COLLECTIONS_UNNAMED_RECORD}
                        </Link>
                      ) : (
                        record.name ?? t.translations.RECORD_COLLECTIONS_UNNAMED_RECORD
                      )}
                    </td>
                    <td>
                      {record.className ??
                        record.classId ??
                        t.translations.RECORD_COLLECTIONS_UNCLASSIFIED}
                    </td>
                    <td>
                      {record.dataSourceName ??
                        record.dataSourceId ??
                        t.translations.UNKNOWN}
                    </td>
                    <td>
                      {record.lastUpdatedAt
                        ? formatLocalDateTime(record.lastUpdatedAt)
                        : t.translations.RECORD_COLLECTIONS_NOT_UPDATED}
                    </td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td colSpan={4}>
                    {records.length
                      ? t.translations.RECORD_COLLECTIONS_NO_RECORDS_MATCH_SEARCH
                      : t.translations.RECORD_COLLECTIONS_NO_RECORDS_ARE_CURRENTLY_ASSIGNED}
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>

        {filteredRecords.length > recordsPerPage ? (
          <div className="flex flex-col gap-3 text-sm sm:flex-row sm:items-center sm:justify-between">
            <span className="text-base-content/70">
              {`${t.translations.SHOWING} ${(recordPage - 1) * recordsPerPage + 1}-${Math.min(
                recordPage * recordsPerPage,
                filteredRecords.length,
              )} ${t.translations.OF} ${filteredRecords.length}`}
            </span>
            <PaginationControls
              currentPage={recordPage}
              pageSize={recordsPerPage}
              totalPages={recordPageCount}
              pageSizeOptions={recordPageSizeOptions ?? [recordsPerPage]}
              onPageChange={setRecordPage}
              onPageSizeChange={onRecordPageSizeChange ?? (() => undefined)}
            />
          </div>
        ) : null}
      </SectionCard>
    </div>
  );
}
