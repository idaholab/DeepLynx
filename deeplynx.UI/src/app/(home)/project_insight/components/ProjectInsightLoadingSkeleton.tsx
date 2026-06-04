"use client";

import React from "react";
import Skeleton from "react-loading-skeleton";
import { AdjustmentsHorizontalIcon } from "@heroicons/react/24/outline";

const skeletonColors = {
  baseColor: "var(--color-base-200)",
  highlightColor: "var(--color-base-300)",
};

const recordRows = Array.from({ length: 4 }, (_, index) => index);
const chatRows = Array.from({ length: 3 }, (_, index) => index);
const pillRows = Array.from({ length: 3 }, (_, index) => index);

function InsightRecordCardSkeleton() {
  return (
    <article className="rounded-xl border border-base-300/60 bg-base-100 px-3 py-3">
      <div className="flex items-start gap-2">
        <Skeleton
          {...skeletonColors}
          circle
          width={24}
          height={24}
          containerClassName="shrink-0"
        />

        <div className="min-w-0 flex-1">
          <div className="flex items-start gap-3">
            <div className="min-w-0 flex-1">
              <Skeleton {...skeletonColors} height={20} width="58%" />

              <div className="mt-2 flex flex-wrap items-center gap-2">
                <Skeleton {...skeletonColors} height={18} width={62} />
                <Skeleton {...skeletonColors} height={18} width={88} />
              </div>
            </div>

            <Skeleton {...skeletonColors} height={24} width={52} />
          </div>

          <div className="mt-3 flex flex-wrap gap-x-4 gap-y-2">
            <Skeleton {...skeletonColors} height={14} width={128} />
            <Skeleton {...skeletonColors} height={14} width={132} />
            <Skeleton {...skeletonColors} height={14} width={96} />
          </div>

          <div className="mt-2 flex flex-wrap gap-1.5">
            <Skeleton {...skeletonColors} height={18} width={54} />
            <Skeleton {...skeletonColors} height={18} width={72} />
          </div>

          <div className="mt-2">
            <Skeleton {...skeletonColors} height={14} width="42%" />
          </div>
        </div>
      </div>
    </article>
  );
}

export default function ProjectInsightLoadingSkeleton() {
  return (
    <div className="flex h-[calc(100dvh-7rem)] min-h-0 flex-col overflow-hidden bg-base-100">
      <div className="border-b border-base-300/40 bg-base-200/40 px-6 py-6 lg:px-10">
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div className="max-w-4xl w-full">
            <Skeleton {...skeletonColors} height={32} width={240} />
            <div className="mt-2">
              <Skeleton {...skeletonColors} height={16} width="72%" />
            </div>
          </div>
        </div>
      </div>

      <div className="flex-1 min-h-0 overflow-hidden p-6 lg:p-8">
        <div className="grid h-full min-h-0 grid-cols-1 gap-6 overflow-y-auto pr-1 xl:grid-cols-[minmax(0,1.7fr)_minmax(340px,1fr)]">
          <section className="flex min-h-0 flex-col">
            <section className="card border border-base-300/60 bg-base-100 shadow-lg h-full min-h-0">
              <div className="card-body flex h-full min-h-0 flex-col gap-4 p-5 lg:p-6">
                <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
                  <Skeleton {...skeletonColors} height={14} width={180} />
                  <Skeleton {...skeletonColors} height={22} width={96} />
                </div>

                <div className="flex-1 min-h-0 rounded-box border border-base-300 bg-base-200/70">
                  <div className="h-full px-4 py-4">
                    <div className="space-y-4">
                      {chatRows.map((index) => (
                        <div
                          key={index}
                          className={`flex ${
                            index === 1 ? "justify-end" : "justify-start"
                          }`}
                        >
                          <div className="max-w-[85%] space-y-2">
                            <Skeleton
                              {...skeletonColors}
                              height={12}
                              width={120}
                            />
                            <Skeleton
                              {...skeletonColors}
                              height={index === 1 ? 52 : 68}
                              width={index === 1 ? 260 : 360}
                            />
                          </div>
                        </div>
                      ))}
                    </div>
                  </div>
                </div>

                <div className="shrink-0 flex flex-col gap-3">
                  <Skeleton {...skeletonColors} height={96} />
                  <div className="flex items-center justify-between gap-3">
                    <Skeleton {...skeletonColors} height={14} width={120} />
                    <Skeleton {...skeletonColors} height={36} width={120} />
                  </div>
                </div>
              </div>
            </section>
          </section>

          <aside className="card card-border bg-base-100 shadow-md shadow-base-content/10 xl:h-full xl:min-h-0">
            <div className="card-body h-full min-h-0 gap-4 p-4 sm:p-5">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div className="min-w-0 w-full">
                  <div className="flex gap-3 items-center">
                    <Skeleton {...skeletonColors} height={26} width={176} />
                    <Skeleton {...skeletonColors} height={24} width={36} />
                  </div>
                  <div className="mt-2 min-h-[2.5rem]">
                    <Skeleton {...skeletonColors} height={16} width="88%" />
                    <Skeleton {...skeletonColors} height={16} width="64%" />
                  </div>
                </div>
              </div>

              <div className="inline-flex w-fit rounded-full border border-base-300/60 bg-base-200/60 p-1 gap-1">
                <Skeleton
                  {...skeletonColors}
                  height={34}
                  width={112}
                  borderRadius={9999}
                />
                <Skeleton
                  {...skeletonColors}
                  height={34}
                  width={112}
                  borderRadius={9999}
                />
              </div>

              <div className="card bg-base-100">
                <div className="card-body gap-4 p-4">
                  <div className="flex flex-wrap items-start justify-between gap-3">
                    <button
                      type="button"
                      className="btn btn-outline btn-sm gap-2 pointer-events-none"
                    >
                      <AdjustmentsHorizontalIcon className="size-4" />
                      <Skeleton {...skeletonColors} height={14} width={88} />
                    </button>
                  </div>

                  <Skeleton
                    {...skeletonColors}
                    height={38}
                    borderRadius={9999}
                  />

                  <div className="flex flex-wrap items-center gap-2">
                    {pillRows.map((index) => (
                      <Skeleton
                        key={index}
                        {...skeletonColors}
                        height={24}
                        width={index === 1 ? 86 : 72}
                      />
                    ))}
                  </div>
                </div>
              </div>

              <div className="min-h-0 flex-1 overflow-y-auto pr-1">
                <section className="bg-base-100">
                  <div className="flex flex-col gap-5 p-4 sm:p-5">
                    <div className="space-y-3">
                      {recordRows.map((index) => (
                        <InsightRecordCardSkeleton key={index} />
                      ))}
                    </div>
                  </div>
                </section>
              </div>
            </div>
          </aside>
        </div>
      </div>
    </div>
  );
}
