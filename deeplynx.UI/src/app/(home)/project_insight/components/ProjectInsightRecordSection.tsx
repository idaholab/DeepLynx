"use client";

import React from "react";

interface ProjectInsightRecordSectionProps {
  title: string;
  description: string;
  count: number;
  emptyMessage: string;
  actions?: React.ReactNode;
  children: React.ReactNode;
}

export default function ProjectInsightRecordSection({
  title,
  description,
  count,
  emptyMessage,
  actions,
  children,
}: ProjectInsightRecordSectionProps) {
  return (
    <>
    <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
      {actions}
    </div>
    <div className="min-h-0 flex-1 overflow-y-auto pr-1">
      <section className="bg-base-100">
        <div className="flex flex-col gap-5 p-4 sm:p-5">
          {count === 0 ? (
            <div className="rounded-xl border border-dashed border-base-300 px-5 py-8 text-center text-sm text-base-content/60">
              {emptyMessage}
            </div>
          ) : (
            children
          )}
        </div>
      </section>
    </div>
    </>
  );
}
