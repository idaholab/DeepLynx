// app/(home)/(routes)/data_catalog/all_records/loading.tsx
import Skeleton from "react-loading-skeleton";

const times = (n: number) => Array.from({ length: n }, (_, i) => i);

export default function AllRecordsLoading() {
  return (
    <main className="min-h-screen bg-base-200/30">
      <section className="border-b border-base-300/50 bg-base-100">
        <div className="mx-auto flex w-full max-w-7xl flex-col gap-5 px-3 py-5 sm:px-6 lg:px-8">
          <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
            <div className="space-y-3">
              <div>
                <Skeleton height={14} width={116} />
                <div className="mt-2">
                  <Skeleton height={36} width={188} />
                </div>
              </div>
              <Skeleton height={48} width={288} />
            </div>

            <div className="w-full lg:max-w-xl">
              <Skeleton height={42} borderRadius={999} />
              <div className="mt-1 flex justify-end">
                <Skeleton height={16} width={112} />
              </div>
            </div>
          </div>
        </div>
      </section>

      <section className="mx-auto flex w-full max-w-7xl flex-col gap-4 px-3 py-5 sm:px-6 lg:px-8">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <Skeleton height={20} width={148} />
          <div className="flex flex-wrap items-center gap-2">
            <Skeleton height={20} width={96} />
            <Skeleton height={32} width={112} />
          </div>
        </div>

        <div className="grid grid-cols-1 gap-5 lg:grid-cols-[18rem_minmax(0,1fr)]">
          <FilterSidebarSkeleton />

          <div className="min-w-0">
            <RecordListSkeleton />
          </div>
        </div>

        <div className="join justify-end">
          <Skeleton height={32} width={40} />
          <Skeleton height={32} width={118} />
          <Skeleton height={32} width={40} />
        </div>
      </section>
    </main>
  );
}

function FilterSidebarSkeleton() {
  return (
    <aside className="space-y-4 lg:sticky lg:top-4 lg:self-start">
      <div className="rounded-box border border-base-300/50 bg-base-100 shadow-sm">
        <div className="flex items-center justify-between border-b border-base-200 px-4 py-3">
          <Skeleton height={20} width={112} />
          <Skeleton height={24} width={58} />
        </div>

        <div className="divide-y divide-base-200">
          {[
            { title: 68, rows: 3 },
            { title: 56, rows: 5 },
            { title: 48, rows: 5 },
          ].map((section, index) => (
            <div key={index} className="px-4 py-4">
              <Skeleton height={18} width={section.title} />
              {index > 0 && (
                <div className="mt-3">
                  <Skeleton height={32} />
                </div>
              )}
              <div className="mt-3 space-y-2">
                {times(section.rows).map((row) => (
                  <div
                    key={row}
                    className="flex items-center justify-between gap-3"
                  >
                    <div className="flex min-w-0 items-center gap-2">
                      <Skeleton width={14} height={14} />
                      <Skeleton width={row % 2 === 0 ? 108 : 82} height={16} />
                    </div>
                  </div>
                ))}
              </div>
            </div>
          ))}
        </div>
      </div>
    </aside>
  );
}

function RecordListSkeleton() {
  return (
    <div className="divide-y divide-base-200 overflow-hidden rounded-box border border-base-300/50 bg-base-100 shadow-sm">
      {times(6).map((i) => (
        <article
          key={i}
          className="grid grid-cols-1 gap-3 p-4 md:grid-cols-[minmax(0,1fr)_auto]"
        >
          <div className="min-w-0">
            <div className="mb-2 flex flex-wrap items-center gap-2">
              <Skeleton width={54} height={24} />
              <Skeleton width={128} height={24} />
              {i % 4 === 0 && <Skeleton width={92} height={24} />}
            </div>
            <Skeleton height={20} width={i % 2 === 0 ? "48%" : "62%"} />
            <div className="mt-1">
              <Skeleton height={16} width="76%" />
            </div>
            <div className="mt-3 flex flex-wrap items-center gap-x-4 gap-y-2">
              <Skeleton height={14} width={132} />
              <Skeleton height={14} width={156} />
              <Skeleton height={14} width={118} />
            </div>
          </div>

          <div className="flex items-center justify-end">
            <Skeleton width={40} height={32} />
          </div>
        </article>
      ))}
    </div>
  );
}
