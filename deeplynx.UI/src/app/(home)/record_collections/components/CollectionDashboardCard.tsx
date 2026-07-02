"use client";

import { useLanguage } from "@/app/contexts/Language";
import { formatLocalDateTime } from "@/app/lib/date_time";
import { RecordCollectionResponseDto } from "../../types/responseDTOs";
import Link from "next/link";
import React from "react";

type Props = {
  collection: RecordCollectionResponseDto;
  labelsExpanded?: boolean;
  tagsExpanded?: boolean;
  badgeDisplayLimit?: number;
  getSensitivityClass?: (label: string) => string;
  onToggleLabels?: (collectionId: number) => void;
  onToggleTags?: (collectionId: number) => void;
  detailsHref: string;
  showBadges?: boolean;
};

export default function CollectionDashboardCard({
  collection,
  labelsExpanded = false,
  tagsExpanded = false,
  badgeDisplayLimit = 10,
  getSensitivityClass = () => "badge-neutral",
  onToggleLabels,
  onToggleTags,
  detailsHref,
  showBadges = true,
}: Props) {
  const { t } = useLanguage();
  const collectionLabels = collection.labels ?? [];
  const collectionTags = collection.tags ?? [];
  const visibleLabels = labelsExpanded
    ? collectionLabels
    : collectionLabels.slice(0, badgeDisplayLimit);
  const visibleTags = tagsExpanded
    ? collectionTags
    : collectionTags.slice(0, badgeDisplayLimit);

  return (
    <div className="card border border-base-300/50 bg-base-100 text-left shadow-sm">
      <div className="card-body gap-4 p-5">
        <div className="space-y-2">
          <h3 className="card-title text-lg">{collection.name}</h3>
          <p className="line-clamp-4 text-sm text-base-content/70">
            {collection.description}
          </p>
        </div>

        {showBadges ? (
          <div className="space-y-2">
            <div className="flex flex-wrap items-center gap-2">
              <span className="text-xs font-semibold uppercase text-base-content/60">
                {t.translations.SENSITIVITY_LABELS}
              </span>
              {visibleLabels.map((label) => (
                <span
                  key={`${collection.id}-label-${label.id}`}
                  className={`badge badge-sm ${getSensitivityClass(label.name)}`}
                >
                  {label.name}
                </span>
              ))}
              {collectionLabels.length > badgeDisplayLimit && onToggleLabels ? (
                <button
                  type="button"
                  className="btn btn-ghost btn-xs px-1"
                  onClick={() => onToggleLabels(collection.id)}
                >
                  {labelsExpanded
                    ? t.translations.SHOW_LESS
                    : t.translations.RECORD_COLLECTIONS_SHOW_MORE}
                </button>
              ) : null}
            </div>
            <div className="flex flex-wrap items-center gap-2">
              <span className="text-xs font-semibold uppercase text-base-content/60">
                {t.translations.TAGS}
              </span>
              {visibleTags.map((tag) => (
                <span
                  key={`${collection.id}-${tag.id}`}
                  className="badge badge-sm badge-outline badge-secondary"
                >
                  {tag.name}
                </span>
              ))}
              {collectionTags.length > badgeDisplayLimit && onToggleTags ? (
                <button
                  type="button"
                  className="btn btn-ghost btn-xs px-1"
                  onClick={() => onToggleTags(collection.id)}
                >
                  {tagsExpanded
                    ? t.translations.SHOW_LESS
                    : t.translations.RECORD_COLLECTIONS_SHOW_MORE}
                </button>
              ) : null}
            </div>
          </div>
        ) : null}

        <div className="grid gap-3 text-sm sm:grid-cols-[repeat(3,minmax(0,1fr))_auto] sm:items-start">
          <div>
            <p className="text-base-content/60">
              {t.translations.RECORD_COLLECTIONS_COLLECTION_ID}
            </p>
            <p className="font-semibold text-base-content">{collection.id}</p>
          </div>
          <div>
            <p className="text-base-content/60">
              {t.translations.RECORD_COLLECTIONS_TOTAL_RECORDS}
            </p>
            <p className="font-semibold text-base-content">
              {collection.recordCount}
            </p>
          </div>
          <div>
            <p className="text-base-content/60">
              {t.translations.RECORD_COLLECTIONS_UPDATED}
            </p>
            <p className="font-semibold text-base-content">
              {formatLocalDateTime(collection.lastUpdatedAt)}
            </p>
          </div>
          <div className="card-actions justify-start sm:justify-end sm:self-end">
            <Link href={detailsHref} className="btn btn-primary btn-sm">
              {t.translations.RECORD_COLLECTIONS_OPEN_DETAILS}
            </Link>
          </div>
        </div>
      </div>
    </div>
  );
}
