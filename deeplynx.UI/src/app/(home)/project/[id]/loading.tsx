"use client";

import LargeSearchBar from "@/app/(home)/components/SearchBar";
import { useLanguage } from "@/app/contexts/Language";
import {
  ArrowsRightLeftIcon,
  ChevronLeftIcon,
  ChevronRightIcon,
  CircleStackIcon,
  Cog6ToothIcon,
  PlusIcon,
  RectangleGroupIcon,
  Squares2X2Icon,
} from "@heroicons/react/24/outline";
import Link from "next/link";
import Skeleton from "react-loading-skeleton";

const LoadingProjectDetail = () => {
  const { t } = useLanguage();

  const project = [1];
  const paginatedRecords = [1, 2, 3, 4, 5];
  const totalPages = 2;
  return (
    <div>
      <main>
        <div className="text-base-content bg-base-200/40 py-3 p-12">
          <h1 className="text-2xl font-bold">
            <Skeleton
              baseColor="var(--color-base-200)"
              highlightColor="var(--color-base-300)"
              width={350}
            />
          </h1>
          <p className="mt-2 text-base-content/70">
            <Skeleton
              baseColor="var(--color-base-200)"
              highlightColor="var(--color-base-300)"
              width={700}
            />
          </p>
          <p className="text-base-content/70">
            <Skeleton
              baseColor="var(--color-base-200)"
              highlightColor="var(--color-base-300)"
              width={50}
            />
          </p>
        </div>

        <div className="flex w-full mt-6">
          {/* left column */}
          <div className="w-full md:w-3/5 px-4">
            <div className="flex flex-col">
              <LargeSearchBar className="mb-4 px-4" />
            </div>

            <div className="card bg-base-100 border border-base-300/50">
              <div className="card-body">
                <div className="flex justify-between px-4">
                  <h1 className="text-xl font-semibold text-base-content">
                    {t.translations.DATA_CATALOG_OVERVIEW}
                  </h1>
                  <Link
                    className="btn btn-secondary text-secondary-content"
                    href={""}
                  >
                    {t.translations.VISIT}
                  </Link>
                </div>
                {/* Recently Added Records Card */}
                <div className="bg-base-100 rounded-xl p-4 border border-base-300/50">
                  <h2 className="text-lg text-base-content mb-4 font-semibold">
                    {t.translations.RECENTLY_ADDED_RECORDS}
                  </h2>
                  <div className="divider m-0 mt-2"></div>
                  <ul className="list mt-0">
                    {paginatedRecords.map((record, index) => (
                      <li
                        key={index}
                        className="border-b border-base-200 cursor-pointer hover:bg-base-200/30 p-2 pl-0 rounded-sm transition-colors"
                      >
                        <div className="text-base-content mb-1">
                          <Skeleton
                            baseColor="var(--color-base-200)"
                            highlightColor="var(--color-base-300)"
                          />
                        </div>
                        <div className="text-sm text-base-content/60 space-x-2 flex flex-wrap">
                          <span>
                            {t.translations.CLASS}{" "}
                            <span className="badge badge-info badge-sm text-xs text-info-content">
                              <Skeleton
                                width={40}
                                baseColor="var(--color-info)"
                                highlightColor="var(--color-info)"
                              />
                            </span>
                          </span>
                          <span className="ml-4">
                            {t.translations.LAST_EDIT}{" "}
                            <Skeleton
                              width={60}
                              baseColor="var(--color-base-200)"
                              highlightColor="var(--color-base-300)"
                            />
                          </span>
                          <span className="ml-4">
                            {t.translations.PROJECT}{" "}
                            <Skeleton
                              width={80}
                              baseColor="var(--color-base-200)"
                              highlightColor="var(--color-base-300)"
                            />
                          </span>
                          <span className="ml-4">
                            {t.translations.DATA_SOURCE}{" "}
                            <Skeleton
                              width={70}
                              baseColor="var(--color-base-200)"
                              highlightColor="var(--color-base-300)"
                            />
                          </span>
                        </div>
                      </li>
                    ))}
                  </ul>

                  {/* Pagination Controls */}
                  {totalPages > 1 && (
                    <div className="flex justify-end gap-2 mt-4">
                      <button className="btn btn-sm btn-ghost text-base-content">
                        <ChevronLeftIcon className="size-6" />
                      </button>
                      <span className="px-2 text-sm text-base-content">
                        <Skeleton
                          width={60}
                          baseColor="var(--color-base-200)"
                          highlightColor="var(--color-base-300)"
                        />
                      </span>
                      <button className="btn btn-sm btn-ghost text-base-content">
                        <ChevronRightIcon className="size-6" />
                      </button>
                    </div>
                  )}
                </div>
              </div>
            </div>
          </div>

          {/* right column */}
          <div className="w-full md:w-2/5 px-4">
            <div className="flex justify-between items-center mb-4">
              {/* <button className="btn btn-outline btn-secondary flex items-center mr-2">
                <Cog6ToothIcon className="h-6 w-6" />
                {t.translations.CUSTOMIZE}
              </button> */}
              <button className="btn btn-secondary text-secondary-content flex items-center">
                <PlusIcon className="h-6 w-6" />
                {t.translations.WIDGET}
              </button>
            </div>
            {/* Widget card */}
            <div className="card bg-base-100 border border-base-300/50 mt-4">
              <div className="card-body">
                <h2 className="card-title text-base-content">
                  {t.translations.PROJECT_OVERVIEW}
                </h2>
                <div className="stats bg-base-100 shadow border border-base-300/50">
                  <div className="stat">
                    <div className="stat-title text-base-content/70">
                      {t.translations.PROJECTS}
                    </div>
                    <div className="stat-value text-primary flex items-center">
                      <Squares2X2Icon className="size-8 mr-2" />
                      <Skeleton
                        width={40}
                        baseColor="var(--color-base-200)"
                        highlightColor="var(--color-base-300)"
                      />
                    </div>
                  </div>
                  <div className="stat">
                    <div className="stat-title text-base-content/70">
                      {t.translations.DATA_RECORD}
                    </div>
                    <div className="stat-value text-primary flex items-center">
                      <CircleStackIcon className="size-8 mr-2" />
                      <Skeleton
                        width={40}
                        baseColor="var(--color-base-200)"
                        highlightColor="var(--color-base-300)"
                      />
                    </div>
                  </div>
                </div>
                <div className="stats bg-base-100 shadow border border-base-300/50">
                  <div className="stat">
                    <div className="stat-title text-base-content/70">
                      {t.translations.CLASSES}
                    </div>
                    <div className="stat-value text-primary flex items-center">
                      <RectangleGroupIcon className="size-8 mr-2" />
                      <Skeleton
                        width={40}
                        baseColor="var(--color-base-200)"
                        highlightColor="var(--color-base-300)"
                      />
                    </div>
                  </div>
                  <div className="stat">
                    <div className="stat-title text-base-content/70">
                      {t.translations.CONNECTIONS}
                    </div>
                    <div className="stat-value text-primary flex items-center">
                      <ArrowsRightLeftIcon className="size-8 mr-2" />
                      <Skeleton
                        width={40}
                        baseColor="var(--color-base-200)"
                        highlightColor="var(--color-base-300)"
                      />
                    </div>
                  </div>
                </div>
              </div>
            </div>
            {/* Team Members */}
            <div className="card bg-base-100 border border-base-300/50 mt-4">
              <div className="card-body">
                <div className="">
                  <h2 className="card-title flex items-center text-base-content">
                    {t.translations.TEAM_MEMBERS}
                  </h2>
                  <Skeleton
                    width={700}
                    height={50}
                    baseColor="var(--color-base-200)"
                    highlightColor="var(--color-base-300)"
                  />
                </div>
              </div>
            </div>
          </div>
        </div>
      </main>
    </div>
  );
};

export default LoadingProjectDetail;
