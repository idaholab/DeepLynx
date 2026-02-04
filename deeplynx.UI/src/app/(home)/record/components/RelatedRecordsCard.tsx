// src/app(home)/record/components/RelatedRecordsCard.tsx

"use client";

import { PlusIcon } from "@heroicons/react/24/outline";
import React, { useEffect, useRef } from "react";

export interface CardColumn<T extends object> {
  key: keyof T;
  label: string;
  render?: (row: T) => React.ReactNode;
}

interface RelatedRecordsCardProps<T extends object> {
  title?: string;
  columns: CardColumn<T>[];
  rows: T[];
  showIndex?: boolean;
  onLoadMore?: () => void;
  isLoading?: boolean;
  hasMore?: boolean;
  relationship: string;
  relationshipDirection?: "outgoing" | "incoming";
  onAddRelationship?: () => void;
}

function RelatedRecordsCard<T extends object>({
  title = "Related Records:",
  columns,
  rows,
  showIndex = true,
  onLoadMore,
  isLoading = false,
  hasMore = false,
  relationship,
  relationshipDirection = "outgoing",
  onAddRelationship,
}: RelatedRecordsCardProps<T>) {
  const scrollContainerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const scrollContainer = scrollContainerRef.current;
    if (!scrollContainer || !onLoadMore || !hasMore) return;

    const handleScroll = () => {
      if (isLoading) return;

      const { scrollTop, scrollHeight, clientHeight } = scrollContainer;
      // Trigger when user scrolls to within 100px of the bottom
      if (scrollHeight - scrollTop <= clientHeight + 100) {
        onLoadMore();
      }
    };

    scrollContainer.addEventListener("scroll", handleScroll);
    return () => scrollContainer.removeEventListener("scroll", handleScroll);
  }, [onLoadMore, isLoading, hasMore]);

  return (
    <div className="card bg-base-100 shadow-md mt-4 p-2">
      <div className="flex justify-between px-4">
        <h2 className="text-xl font-bold md-4 text-base-content">{title}</h2>
        {onAddRelationship && (
          <button
            className="flex items-center justify-center w-8 h-7 rounded-full bg-primary text-white cursor-pointer"
            onClick={onAddRelationship}
          >
            <PlusIcon className="size-6" />
          </button>
        )}
      </div>
      <div className="card-body p-4">
        <div
          ref={scrollContainerRef}
          className="overflow-auto rounded-box border border-base-300 bg-base-100"
          style={{ maxHeight: "320px" }}
        >
          <table className="table">
            <thead className="sticky top-0 bg-base-200 z-10">
              <tr className="bg-base-200 text-base-content">
                {columns.map((col) => (
                  <th key={String(col.key)}>{col.label}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {rows.map((row, i) => (
                <tr key={i}>
                  {columns.map((col) => {
                    const raw = row[col.key];
                    const content = col.render
                      ? col.render(row)
                      : (raw as React.ReactNode);
                    return <td key={String(col.key)}>{content}</td>;
                  })}
                </tr>
              ))}
              {/* Loading indicator row */}
              {isLoading && (
                <tr>
                  <td colSpan={columns.length} className="text-center py-4">
                    <span className="loading loading-spinner loading-sm"></span>
                    <span className="ml-2">Loading more...</span>
                  </td>
                </tr>
              )}
              {/* No more data indicator */}
              {!hasMore && rows.length > 0 && (
                <tr>
                  <td
                    colSpan={columns.length}
                    className="text-center py-2 text-base-content/50"
                  >
                    No more records
                  </td>
                </tr>
              )}
            </tbody>
          </table>
          {/* Empty state */}
          {rows.length === 0 && !isLoading && (
            <div className="text-center text-base-content/60 py-8">
              No relations found
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

export default RelatedRecordsCard;
