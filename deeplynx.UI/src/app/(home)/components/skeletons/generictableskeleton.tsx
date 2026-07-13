import { 
  ChevronLeftIcon, 
  ChevronRightIcon,
  AdjustmentsHorizontalIcon,
  MagnifyingGlassIcon
} from "@heroicons/react/24/outline";
import Skeleton from "react-loading-skeleton";

const times = (n: number) => Array.from({ length: n }, (_, i) => i);

type GenericTableSkeletonProps = {
  totalColumns?: number;
  totalRows?: number;
  title?: boolean;
  searchBar?: boolean;
  filters?: boolean;
  pagination?: boolean;
  actionButtons?: boolean;
  bordered?: boolean;
};

export default function GenericTableSkeleton({
  totalColumns = 7,
  totalRows = 10,
  title = false,
  searchBar = true,
  filters = false,
  pagination = true,
  actionButtons = false,
  bordered = false
}: GenericTableSkeletonProps) {
  
  return (
    <div className={`overflow-x-auto min-h-[80vh] ${bordered ? "rounded-box border border-base-200" : ""} p-4`}>
      {title && (
        <div className="mb-4">
          <Skeleton width={200} height={24} baseColor="var(--color-base-200)" highlightColor="var(--color-base-100)" />
        </div>
      )}
      
      <div className="my-2 flex justify-between items-center">
        {searchBar && (
          <div className="flex gap-2">
            <div className="relative">
              <div className="input input-bordered flex items-center gap-2 w-64">
                <MagnifyingGlassIcon className="size-5 opacity-30" />
                <Skeleton width={150} baseColor="var(--color-base-200)" highlightColor="var(--color-base-100)" />
              </div>
            </div>

            {filters && (
              <button className="btn btn-outline btn-sm gap-2" disabled>
                <AdjustmentsHorizontalIcon className="size-5 opacity-30" />
                <span className="opacity-30">Filters</span>
              </button>
            )}
          </div>
        )}

        {pagination && (
          <div className="flex justify-end items-center p-2">
            <p className="text-sm mr-2 opacity-30">Rows:</p>
            <div className="flex gap-1">
              <Skeleton width={40} height={32} baseColor="var(--color-base-200)" highlightColor="var(--color-base-100)" />
              <Skeleton width={40} height={32} baseColor="var(--color-base-200)" highlightColor="var(--color-base-100)" />
              <Skeleton width={40} height={32} baseColor="var(--color-base-200)" highlightColor="var(--color-base-100)" />
            </div>
          </div>
        )}

        {actionButtons && (
          <div className="p-2 flex gap-2">
            <Skeleton circle width={24} height={24} baseColor="var(--color-base-200)" highlightColor="var(--color-base-100)" />
            <Skeleton circle width={24} height={24} baseColor="var(--color-base-200)" highlightColor="var(--color-base-100)" />
            <Skeleton circle width={24} height={24} baseColor="var(--color-base-200)" highlightColor="var(--color-base-100)" />
          </div>
        )}
      </div>

      <table className={`table table-pin-cols ${bordered ? "table-bordered" : ""}`}>
        <thead>
          <tr>
            {times(totalColumns).map((i) => (
              <th key={i} className="opacity-0">
                <Skeleton width="80%" baseColor="var(--color-base-200)" highlightColor="var(--color-base-100)" />
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {times(totalRows).map((rowIndex) => (
            <tr key={rowIndex} className="hover:bg-base-200">
              {times(totalColumns).map((colIndex) => (
                <td key={colIndex} className="text-base-content">
                  <Skeleton width="70%" baseColor="var(--color-base-200)" highlightColor="var(--color-base-100)" />
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}