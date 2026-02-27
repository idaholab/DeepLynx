import { useLanguage } from "@/app/contexts/Language";
import { ChevronLeftIcon, ChevronRightIcon, PencilIcon, TrashIcon } from "@heroicons/react/24/outline";
import Skeleton from "react-loading-skeleton";
import { useEffect, useState } from "react";

const times = (n: number) => Array.from({ length: n }, (_, i) => i);

function UserSettingsSkeleton() {
  const rows = 4;
  const totalPages = 1;

  return (
    <div className="w-full bg-base-200 rounded-box p-6">
      <div>
        {times(rows).map((i) => (
          <div key={i} className="flex items-center font-mono text-sm pb-3">
            <div className="w-7/8 h-2">
              <Skeleton width="90%" baseColor="var(--skeleton-base)" highlightColor="var(--skeleton-highlight)" />
            </div>
            <div className="ml-auto">
              <span>
                <TrashIcon className="size-5 text-red-400" />
              </span>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

export default UserSettingsSkeleton;