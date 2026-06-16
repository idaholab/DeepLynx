"use client";

import PaginationControls, {
  DEFAULT_PAGE_SIZE_OPTIONS,
} from "@/app/(home)/components/PaginationControls";
import SearchInput from "@/app/(home)/components/SearchInput";
import { useLanguage } from "@/app/contexts/Language";
import Link from "next/link";
import React from "react";
import { PaginatedRecordCollectionsResponseDto } from "../types/responseDTOs";
import CollectionDashboardCard from "./components/CollectionDashboardCard";
import CollectionSortControl from "./components/CollectionSortControl";
import FilterSidebar from "./components/FilterSidebar";
import SectionCard from "./components/SectionCard";
import { COLLECTION_BADGE_DISPLAY_LIMIT } from "./components/recordCollections.constants";
import { getSensitivityClass } from "./components/recordCollections.utils";
import { renderCollectionSortLabel } from "./components/recordCollections.view-utils";
import { useCollectionsDashboard } from "./hooks/useCollectionsDashboard";

type Props = {
  organizationId: number;
  projectId: number;
  initialRecordCollectionsPage: PaginatedRecordCollectionsResponseDto;
};

export default function RecordCollectionsClient({
  organizationId,
  projectId,
  initialRecordCollectionsPage,
}: Props) {
  const { t } = useLanguage();

  const {
    summary,
    searchInput,
    sortControl,
    filterSidebar,
    collectionCards,
    pagination,
  } = useCollectionsDashboard({
    organizationId,
    projectId,
    initialPage: initialRecordCollectionsPage,
  });

  return (
    <div className="min-h-screen bg-base-200/30 px-4 py-6 lg:px-8">
      <div className="mx-auto max-w-7xl space-y-5">
        <div className="flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
          <div>
            <p className="text-xs font-semibold uppercase tracking-wide text-base-content/60">
              {t.translations.RECORD_COLLECTIONS}
            </p>
            <h1 className="mt-2 text-3xl font-bold text-base-content">
              {t.translations.RECORD_COLLECTIONS_COLLECTION_DASHBOARD}
            </h1>
            <p className="mt-2 max-w-3xl text-sm text-base-content/70">
              {t.translations.RECORD_COLLECTIONS_BROWSE_CREATE_MODIFY_EXISTING}
            </p>
          </div>
        </div>

        <div className="mt-4 space-y-6">
          <div className="grid gap-4 lg:grid-cols-[280px_minmax(0,1fr)] lg:items-start">
            <div className="lg:sticky lg:top-4">
              <FilterSidebar {...filterSidebar} />
            </div>

            <SectionCard
              title={t.translations.RECORD_COLLECTIONS_ALL}
              subtitle={t.translations.RECORD_COLLECTIONS_BROWSE_SEARCH_OPEN_PROJECT}
              action={
                <div className="flex flex-wrap items-center justify-end gap-3">
                  <div className="rounded-lg border border-base-300 bg-base-200/50 px-3 py-2 text-sm">
                    <span className="text-base-content/70">
                      {t.translations.RECORD_COLLECTIONS_TOTAL_COLLECTIONS}{" "}
                    </span>
                    <span className="font-semibold text-base-content">
                      {summary.filteredCount}
                    </span>
                  </div>
                  <Link
                    href="/record_collections/new_collection"
                    className="btn btn-primary px-2 text-base-content"
                  >
                    {t.translations.RECORD_COLLECTIONS_NEW}
                  </Link>
                </div>
              }
            >
              <div className="grid gap-3 lg:grid-cols-[minmax(0,1fr)_18rem]">
                <SearchInput
                  className="self-end"
                  placeholder={t.translations.RECORD_COLLECTIONS_FILTER_BY_TITLE_OR_DESCRIPTION}
                  {...searchInput}
                />
                <CollectionSortControl
                  {...sortControl}
                  renderLabel={(option) => renderCollectionSortLabel(option, t)}
                />
              </div>

              <div
                className={`grid gap-4 transition-opacity ${
                  summary.isLoading ? "opacity-70" : "opacity-100"
                }`}
              >
                {collectionCards.items.map((collection) => {
                  const labelsExpanded = collectionCards.isLabelsExpanded(collection.id);
                  const tagsExpanded = collectionCards.isTagsExpanded(collection.id);
                  return (
                    <CollectionDashboardCard
                      key={collection.id}
                      collection={collection}
                      labelsExpanded={labelsExpanded}
                      tagsExpanded={tagsExpanded}
                      badgeDisplayLimit={COLLECTION_BADGE_DISPLAY_LIMIT}
                      getSensitivityClass={getSensitivityClass}
                      onToggleLabels={collectionCards.onToggleLabels}
                      onToggleTags={collectionCards.onToggleTags}
                      detailsHref={`/record_collections/${collection.id}`}
                    />
                  );
                })}

                {!collectionCards.items.length ? (
                  <div className="rounded-xl border border-dashed border-base-300 bg-base-100/60 px-4 py-8 text-center text-sm text-base-content/70">
                    {summary.isLoading
                      ? t.translations.LOADING
                      : t.translations.RECORD_COLLECTIONS_NO_RECORDS_MATCH_SEARCH}
                  </div>
                ) : null}
              </div>

              {pagination.totalItems > pagination.pageSize ? (
                <div className="flex flex-col gap-3 border-t border-base-300 pt-4">
                  <span className="text-sm text-base-content/70">
                    {`${t.translations.SHOWING} ${pagination.startIndex + 1}-${Math.min(
                      pagination.startIndex + pagination.pageSize,
                      pagination.totalItems,
                    )} ${t.translations.OF} ${pagination.totalItems}`}
                  </span>
                  <PaginationControls
                    currentPage={pagination.currentPage}
                    pageSize={pagination.pageSize}
                    totalPages={pagination.totalPages}
                    pageSizeOptions={DEFAULT_PAGE_SIZE_OPTIONS}
                    onPageChange={pagination.onPageChange}
                    onPageSizeChange={pagination.onPageSizeChange}
                  />
                </div>
              ) : null}
            </SectionCard>
          </div>
        </div>
      </div>
    </div>
  );
}
