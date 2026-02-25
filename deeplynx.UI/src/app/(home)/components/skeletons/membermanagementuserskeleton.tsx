import { useLanguage } from "@/app/contexts/Language";
import { ChevronLeftIcon, ChevronRightIcon, PencilIcon, TrashIcon } from "@heroicons/react/24/outline";
import Skeleton from "react-loading-skeleton";
import { useEffect, useState } from "react";

const times = (n: number) => Array.from({ length: n }, (_, i) => i);

function MemberManagementUserSkeleton() {
  const rows = 6;
  const totalPages = 2;

  return (
    <div className="w-full">
      <div>
        <div className="flex p-4">
          <div className="w-1/8 pl-4 pt-10">
            <input className="checkbox" type="checkbox"></input>
          </div>
          <div className="w-1/4 pl-4 pt-10 font-bold">Name</div>
          <div className="w-1/2 pl-4 pt-10 font-bold">Email</div>
        </div>
        {times(rows).map((i) => (
          <div key={i} className="flex p-4">
            <div className="w-1/8 pl-4 h-4 rounded">
              <input className="checkbox" type="checkbox"></input>
            </div>
            <div className="w-1/4 pl-4 h-4">
              <Skeleton width="35%" baseColor="var(--skeleton-base)" highlightColor="var(--skeleton-highlight)" />
            </div>
            <div className="w-1/2 pl-4 h-4">
              <Skeleton width="35%" baseColor="var(--skeleton-base)" highlightColor="var(--skeleton-highlight)" />
            </div>
            <div className="w-1/8 pl-4">
              <span>
                <PencilIcon className="size-6 text-secondary" />
              </span>
            </div>
            <div className="w-1/8 pl-4">
              <span>
                <TrashIcon className="size-6 text-red-500" />
              </span>
            </div>
          </div>
        ))}
        {totalPages > 1 && (
          <div className="flex justify-end gap-2 mt-4">
            <button className="btn btn-sm btn-ghost">
              <ChevronLeftIcon className="size-6" />
            </button>
            <span className="px-2 text-sm">
              <Skeleton width={60} baseColor="var(--skeleton-base)" highlightColor="var(--skeleton-highlight)" />
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

export default MemberManagementUserSkeleton;