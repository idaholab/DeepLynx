import { useLanguage } from "@/app/contexts/Language";
import { ChevronLeftIcon, ChevronRightIcon, PencilIcon, TrashIcon } from "@heroicons/react/24/outline";
import Skeleton from "react-loading-skeleton";
import { useEffect, useState } from "react";

const times = (n: number) => Array.from({ length: n }, (_, i) => i);

function ProjectSettingsMemberSkeleton() {
  const { t } = useLanguage();
  const rows = 4;
  const totalPages = 2;

  return (
    <div className="w-full">
      <div>
        <div className="flex p-4">
          <div className="w-1/12 pl-4 pt-10">                  
            <input className="checkbox" type="checkbox"></input>
          </div>
          <div className="w-1/4 pl-4 pt-10 font-bold text-sm">Name</div>
          <div className="w-1/4 pl-4 pt-10 font-bold text-sm">Email</div>
          <div className="w-1/4 pl-4 pt-10 font-bold text-sm">Role</div>
        </div>
        {times(rows).map((i) => (
          <div key={i} className="flex p-4 items-center">
            <div className="w-1/12 pl-4 h-4 rounded">                  
              <input className="checkbox" type="checkbox"></input>
            </div>
            <div className="w-1/4 pl-4 h-4">
              <Skeleton width="60%" baseColor="var(--color-base-200)" highlightColor="var(--color-base-100)" />
            </div>
            <div className="w-1/4 pl-4 h-4">
              <Skeleton width="60%" baseColor="var(--color-base-200)" highlightColor="var(--color-base-100)" />
            </div>
            <div className="w-1/4 pl-4 h-4">
              <Skeleton width="40%" baseColor="var(--color-base-200)" highlightColor="var(--color-base-100)" />
            </div>
            <div className="w-1/12 pl-4">
              <button className="btn">
                {t.translations.ROLE}
              </button>
            </div>
            <div className="w-1/12 pl-4">
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

export default ProjectSettingsMemberSkeleton;