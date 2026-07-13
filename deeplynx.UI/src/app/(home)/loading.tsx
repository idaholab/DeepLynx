"use client";
import {
  ChevronLeftIcon,
  ChevronRightIcon,
  MagnifyingGlassIcon,
  PlusIcon,
} from "@heroicons/react/24/outline";
import React from "react";
import Skeleton from "react-loading-skeleton";
import WithT from "./components/WithT";
// NOTE: CSS import moved to layout or globals

export default function Loadingtranslations() {
  const totalPages = 2;
  const expandedIndex = 1;
  const columns = [1];
  const paginatedRecords = [1, 2, 3, 4, 5];
  return (
    <WithT>
      {(t) => (
        <div className="min-h-screen bg-base-100">
          {/* Header */}
          <div className="flex justify-between items-center bg-base-200/40 pl-12 pt-3 pb-2">
            <h1 className="text-2xl font-bold text-base-content">
              <span>{t.translations.WELECOME}</span>
            </h1>
            <label
              className={`input input-bordered flex items-center relative mr-4`}
            >
              {/* Input field */}
              <input type="text" className="grow" />
              {/* Search icon */}
              <MagnifyingGlassIcon className="absolute left-3 top-2.5 w-5 h-5 text-base-content/50 size-6" />
            </label>
          </div>

          {/* Main Content */}
          <div className="p-6">
            <div
              className="w-4/5 mx-auto"
            >
              <div className="card border border-base-300/50 bg-base-100 p-4 shadow-sm">
                <div className="flex justify-between items-center mb-4">
                  <h3 className="text-info-content text-lg font-semibold">
                    {t.translations.YOUR_PROJECTS}
                  </h3>

                  <div className="flex gap-2">
                    <button className="btn btn-outline btn-secondary flex items-center gap-1">
                      <PlusIcon className="size-6" />
                      <span>{t.translations.RECORD}</span>
                    </button>
                    <button className="btn btn-secondary text-secondary-content flex items-center gap-1">
                      <PlusIcon className="size-6" />
                      <span>{t.translations.PROJECT}</span>
                    </button>
                  </div>
                </div>

                <div>
                  <table className="table w-full border-separate p-2 border-spacing-y-2">
                    {expandedIndex === null && (
                      <thead>
                        <tr>
                          {columns.map((col, i) => (
                            <th key={i} className="text-base-content/70">
                              <Skeleton />
                            </th>
                          ))}
                          <th></th>
                        </tr>
                      </thead>
                    )}

                    <tbody>
                      {paginatedRecords.map((row, index) => {
                        const globalIndex = index; // because paginatedRecords index is local
                        return (
                          <React.Fragment key={globalIndex}>
                            <tr className="bg-base-100 hover:bg-base-200/40 rounded-lg overflow-hidden shadow-sm">
                              {columns.map((col, colIndex) => (
                                <React.Fragment key={`col-${colIndex}`}>
                                  <td className="text-base-content">
                                    <Skeleton
                                      width={100}
                                      baseColor="var(--color-base-200)"
                                      highlightColor="var(--color-base-300)"
                                    />
                                  </td>
                                  <td className="text-base-content">
                                    <Skeleton
                                      width={350}
                                      baseColor="var(--color-base-200)"
                                      highlightColor="var(--color-base-300)"
                                    />
                                  </td>
                                  <td className="text-base-content">
                                    <Skeleton
                                      width={75}
                                      baseColor="var(--color-base-200)"
                                      highlightColor="var(--color-base-300)"
                                    />
                                  </td>
                                  <td className="text-base-content">
                                    <Skeleton
                                      width={50}
                                      baseColor="var(--color-base-200)"
                                      highlightColor="var(--color-base-300)"
                                    />
                                  </td>
                                </React.Fragment>
                              ))}
                            </tr>
                          </React.Fragment>
                        );
                      })}
                    </tbody>
                  </table>

                  {/* Pagination Controls */}
                  {totalPages > 1 && (
                    <div className="flex justify-end gap-2 mt-4">
                      <button className="btn btn-sm btn-ghost text-base-content">
                        <ChevronLeftIcon className="size-6" />
                      </button>
                      <span className="px-2 text-sm text-base-content">
                        <Skeleton
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
        </div>
      )}
    </WithT>
  );
}
