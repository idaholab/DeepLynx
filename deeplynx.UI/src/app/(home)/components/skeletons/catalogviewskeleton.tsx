import { useLanguage } from "@/app/contexts/Language";
import { ChevronLeftIcon, ChevronRightIcon } from "@heroicons/react/24/outline";
import Skeleton from "react-loading-skeleton";
import { useEffect, useState } from "react";

const times = (n: number) => Array.from({ length: n }, (_, i) => i);

function CatalogViewSkeleton() {
  const { t } = useLanguage();
  const rows = 6;
  const totalPages = 2;

  return (
    <div className="shadow-md shadow-base-content/10 rounded-xl">
      <div className="bg-base-100 rounded-xl p-4">
        <h2 className="text-lg text-base-content mb-2 font-bold">
          Recently Added Records
        </h2>
        <div className="divider m-0 mt-2"></div>

        <ul className="space-y-1 p-2">
          {times(rows).map((i) => (
            <li
              key={i}
              className="border-b border-base-content/20 hover:bg-base-200/30 p-2 pl-0 rounded-sm"
            >
              <div className="text-accent-content mb-1 w-160">
                <Skeleton width="35%" baseColor="var(--color-base-200)" highlightColor="var(--color-base-100)" />
              </div>
              <div className="text-sm text-base-content space-x-2 flex flex-wrap items-center">
                <span>
                  {t.translations.CLASS}{" "}
                  <span className="badge badge-sm text-xs text-base-content">
                    <Skeleton width={100} baseColor="var(--color-base-200)" highlightColor="var(--color-base-100)" />
                  </span>
                </span>
                <span className="ml-4">
                  {t.translations.LAST_EDIT} <Skeleton width={250} baseColor="var(--color-base-200)" highlightColor="var(--color-base-100)" />
                </span>
                <span className="ml-4">
                  {t.translations.PROJECT} <Skeleton width={150} baseColor="var(--color-base-200)" highlightColor="var(--color-base-100)" />
                </span>
                <span className="basis-full text-left">
                  {t.translations.DATA_SOURCE} <Skeleton width={200} baseColor="var(--color-base-200)" highlightColor="var(--color-base-100)" />
                </span>
              </div>
            </li>
          ))}
        </ul>

        {totalPages > 1 && (
          <div className="flex justify-end gap-2 mt-4">
            <button className="btn btn-sm btn-ghost">
              <ChevronLeftIcon className="size-6" />
            </button>
            <span className="px-2 text-sm">
              <Skeleton width={60} baseColor="var(--color-base-200)" highlightColor="var(--color-base-100)" />
            </span>
            <button className="btn btn-sm btn-ghost">
              <ChevronRightIcon className="size-6" />
            </button>
          </div>
        )}
      </div>
    </div>
  );
}

export default CatalogViewSkeleton;