// app/(home)/(routes)/data_catalog/loading.tsx
"use client";

import { useLanguage } from "@/app/contexts/Language";
import {
  ArrowUturnLeftIcon,
  ChevronLeftIcon,
  ChevronRightIcon,
  EyeIcon,
  PlusIcon,
  QueueListIcon,
  TableCellsIcon,
} from "@heroicons/react/24/outline";
import { useSearchParams } from "next/navigation";
import Skeleton from "react-loading-skeleton";

// Small helpers so maps always have keys
const times = (n: number) => Array.from({ length: n }, (_, i) => i);

export default function DataCatalogLoading() {
  const params = useSearchParams();

  // If coming from Projects "Visit" button, you already push ?fromProject=...
  // You can also treat ?search=... as "list" mode
  const cameFromProject = !!params.get("fromProject");
  const hasSearch = !!params.get("search");
  const showListSkeleton = cameFromProject || hasSearch;

  return (
    <div>
      <HeaderSkeleton />

      <ToolbarSkeleton showListButtons={showListSkeleton} />

      {showListSkeleton ? <ListViewSkeleton /> : <CatalogViewSkeleton />}
    </div>
  );
}

function HeaderSkeleton() {
  return (
    <div className="flex justify-between items-center bg-base-200/40 pl-12 py-2">
      <div>
        <h1 className="text-2xl font-bold text-info-content">Data Catalog</h1>
        <Skeleton width={220} height={18} />
      </div>
    </div>
  );
}

function ToolbarSkeleton({ showListButtons }: { showListButtons: boolean }) {
  return (
    <div className="flex justify-between gap-4 mb-4 pt-20 pl-8 w-full box-border">
      {/* Left: Search */}
      <div className="flex flex-col md:w-1/2">
        {/* Keep this lightweight in loading UIs; a plain Skeleton box is cheaper than importing LargeSearchBar */}
        <div className="w-full">
          <Skeleton height={40} />
        </div>
      </div>

      {/* Right: actions */}
      <div className="flex gap-4 pr-4">
        {showListButtons ? (
          <button className="btn btn-outline btn-primary">
            <ArrowUturnLeftIcon className="h-6 w-6" />
            <Skeleton width={150} />
          </button>
        ) : (
          <button className="btn btn-outline btn-primary">
            <EyeIcon className="h-6 w-6" />
            <Skeleton width={150} />
          </button>
        )}

        <button className="btn btn-primary text-white">
          <PlusIcon className="h-6 w-6" />
          <Skeleton width={70} />
        </button>

        {showListButtons && (
          <div className="flex gap-1">
            <button className="btn btn-sm btn-primary">
              <QueueListIcon className="h-7 w-7" />
            </button>
            <button className="btn btn-sm btn-ghost">
              <TableCellsIcon className="h-7 w-7" />
            </button>
          </div>
        )}
      </div>
    </div>
  );
}

function ListViewSkeleton() {
  const { t } = useLanguage();
  const tagsCount = 3;
  const rows = 5;

  return (
    <div className="bg-base-100 rounded-xl shadow p-4 w-full mx-auto">
      <ul className="list">
        {times(rows).map((i) => (
          <li
            key={i}
            className="py-4 border-b border-base-200 hover:bg-base-200/30 p-3"
          >
            <div className="font-bold mb-1">
              <Skeleton width="60%" />
            </div>
            <span className="text-sm">
              <Skeleton count={2} />
            </span>

            <div className="flex pt-2 items-center gap-4">
              <span className="font-bold">
                <Skeleton width={120} />
              </span>
              <div>
                <span className="font-bold mr-2">
                  {t.translations.LAST_EDIT}
                </span>
                <Skeleton width={100} />
              </div>
            </div>

            <div className="pt-2 flex items-center gap-2">
              <span>{t.translations.TAGS}</span>
              {times(tagsCount).map((t) => (
                <Skeleton key={t} width={40} height={20} />
              ))}
            </div>
          </li>
        ))}
      </ul>
    </div>
  );
}

function CatalogViewSkeleton() {
  const { t } = useLanguage();
  const rows = 6;
  const totalPages = 2;

  return (
    <div className="flex w-full gap-8 pl-8">
      <div className="w-2/3">
        <div className="bg-base-100 rounded-xl p-4">
          <h2 className="text-lg text-black mb-2">
            <Skeleton width={220} />
          </h2>
          <div className="divider m-0 mt-2"></div>

          <ul className="list mt-0">
            {times(rows).map((i) => (
              <li
                key={i}
                className="border-b border-base-200 hover:bg-base-200/30 p-2 pl-0 rounded-sm"
              >
                <div className="text-accent-content mb-1">
                  <Skeleton width="55%" />
                </div>
                <div className="text-sm text-base-300 space-x-2 flex flex-wrap items-center">
                  <span>
                    {t.translations.CLASS}{" "}
                    <span className="badge badge-info badge-sm text-xs">
                      <Skeleton width={60} />
                    </span>
                  </span>
                  <span className="ml-4">
                    {t.translations.LAST_EDIT} <Skeleton width={80} />
                  </span>
                  <span className="ml-4">
                    {t.translations.PROJECT} <Skeleton width={120} />
                  </span>
                  <span className="ml-4">
                    {t.translations.DATA_SOURCE} <Skeleton width={100} />
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
                <Skeleton width={60} />
              </span>
              <button className="btn btn-sm btn-ghost">
                <ChevronRightIcon className="size-6" />
              </button>
            </div>
          )}
        </div>
      </div>

      <div className="w-1/3">
        <div className="bg-base-100 text-accent-content rounded-xl p-4 shadow-md card card-border mt-6">
          <h3 className="text-lg font-semibold mb-2">
            <Skeleton width={160} />
          </h3>
          <Skeleton width={160} height={80} />
          {times(8).map((i) => (
            <div key={i} className="py-2 border-b border-base-content/10">
              <Skeleton />
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
