// app/(home)/(routes)/data_catalog/record/loading.tsx
"use client";

import Skeleton from "react-loading-skeleton";
import { useSearchParams } from "next/navigation";

// Small helpers so maps always have keys
const times = (n: number) => Array.from({ length: n }, (_, i) => i);

export default function RecordLoading() {
  // Optional: grab recordId/projectId to tweak the headline while loading
  const params = useSearchParams();
  const rid = params.get("recordId");

  return (
    <div className="">
      {/* Header */}
      <div className="text-base-content bg-base-200/40 py-3 p-12">
        <h1 className="text-2xl font-bold">
          <Skeleton
            baseColor="var(--color-base-200)"
            highlightColor="var(--color-base-300)"
            width={350}
            className="animate-pulse"
          />
        </h1>
      </div>

      <div className="divider" />

      {/* Tabs header skeleton */}
      <div className="tabs tabs-bordered mb-4 px-6">
        <a className="tab tab-active mr-4">
          <Skeleton width={140} height={20} className="animate-pulse" />
        </a>
        <a className="tab mr-4">
          <Skeleton width={80} height={20} className="animate-pulse" />
        </a>
        <a className="tab mr-4">
          <Skeleton width={120} height={20} className="animate-pulse" />
        </a>
        <a className="tab">
          <Skeleton width={100} height={20} className="animate-pulse" />
        </a>
      </div>

      {/* Active tab content skeleton = “Record Information” */}
      <div className="flex w-full gap-4">
        {/* Left column: Property tables */}
        <div className="w-full md:w-1/2 p-3 px-4">
          <PropertyTableSkeleton titleWidth={180} rows={8} />
          <PropertyTableSkeleton className="mt-4" titleWidth={200} rows={6} />
        </div>

        {/* Right column: Tags card */}
        <div className="flex-grow">
          <div className="card border border-base-300/50 bg-base-100 p-4 shadow-sm">
            <div className="card-title mb-2">
              <Skeleton width={120} />
            </div>
            <div className="flex gap-2 flex-wrap">
              {times(6).map((i) => (
                <Skeleton key={i} width={60} height={24} />
              ))}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

/** Light skeleton for a property table (don’t import real PropertyTable here) */
function PropertyTableSkeleton({
  titleWidth = 160,
  rows = 6,
  className = "",
}: {
  titleWidth?: number;
  rows?: number;
  className?: string;
}) {
  return (
    <div
      className={`card border border-base-300/50 bg-base-100 p-3 shadow-sm ${className}`}
    >
      <div className="mb-3">
        <Skeleton width={titleWidth} height={20} />
      </div>
      <div className="divide-y divide-base-200">
        {times(rows).map((i) => (
          <div key={i} className="py-3 grid grid-cols-3 gap-4 items-center">
            <Skeleton height={18} />
            <div className="col-span-2">
              <Skeleton height={18} />
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
