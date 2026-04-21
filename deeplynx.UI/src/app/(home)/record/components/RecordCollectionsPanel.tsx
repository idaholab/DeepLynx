"use client";

import Link from "next/link";
import React, { useMemo } from "react";

type RecordCollectionsPanelProps = {
  recordId: number;
  recordName: string;
};

type RecordCollectionMembership = {
  id: string;
  name: string;
  sensitivity: "High" | "Moderate" | "Low";
  tags: string[];
  accessLabel: string;
};

function getBadgeClass(sensitivity: RecordCollectionMembership["sensitivity"]) {
  switch (sensitivity) {
    case "High":
      return "badge-error";
    case "Moderate":
      return "badge-warning";
    default:
      return "badge-success";
  }
}

export default function RecordCollectionsPanel({
  recordId,
  recordName,
}: RecordCollectionsPanelProps) {
  const accessibleCollections = useMemo<RecordCollectionMembership[]>(() => {
    const baseCollections: RecordCollectionMembership[] = [
      {
        id: "review-bundle",
        name: "Safety Review Bundle",
        sensitivity: "High",
        tags: ["review", "qa"],
        accessLabel: "Visible to you",
      },
      {
        id: "handoff-set",
        name: "Project Handoff Set",
        sensitivity: "Moderate",
        tags: ["handoff", "delivery"],
        accessLabel: "Editable",
      },
      {
        id: "metadata-candidates",
        name: "Metadata Cleanup Queue",
        sensitivity: "Low",
        tags: ["metadata", "cleanup"],
        accessLabel: "Visible to you",
      },
    ];

    return recordId % 2 === 0 ? baseCollections : baseCollections.slice(0, 2);
  }, [recordId]);

  return (
    <section className="card border border-base-300 bg-base-100 shadow-sm">
      <div className="card-body gap-4">
        <div className="flex items-start justify-between gap-3">
          <div>
            <h2 className="text-lg font-semibold text-base-content">
              Collection Membership
            </h2>
            <p className="text-sm text-base-content/70">
              Collections containing {recordName} that the current user can
              access.
            </p>
          </div>
          <span className="badge badge-outline">
            {accessibleCollections.length} visible
          </span>
        </div>

        <div className="space-y-3">
          {accessibleCollections.map((collection) => (
            <div
              key={collection.id}
              className="rounded-2xl border border-base-300 bg-base-200/30 p-4"
            >
              <div className="flex flex-wrap items-center gap-2">
                <h3 className="font-medium text-base-content">
                  {collection.name}
                </h3>
                <span
                  className={`badge badge-sm ${getBadgeClass(collection.sensitivity)}`}
                >
                  {collection.sensitivity}
                </span>
                <span className="badge badge-sm badge-outline">
                  {collection.accessLabel}
                </span>
              </div>

              <div className="mt-3 flex flex-wrap gap-2">
                {collection.tags.map((tag) => (
                  <span
                    key={`${collection.id}-${tag}`}
                    className="badge badge-secondary badge-outline badge-sm"
                  >
                    {tag}
                  </span>
                ))}
              </div>
            </div>
          ))}
        </div>

        <div className="flex items-center justify-between gap-3">
          <p className="text-sm text-base-content/60">
            Wireframe only. This is the intended right-side record context panel.
          </p>
          <Link
            href="/data_catalog/data_collections"
            className="btn btn-ghost btn-sm"
          >
            Open Data Collections
          </Link>
        </div>
      </div>
    </section>
  );
}
