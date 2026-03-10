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
    <section className="card border border-base-300/60 bg-base-100 shadow-lg">
      <div className="card-body gap-5 p-5 lg:p-6">
        <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
          <div>
            <div className="flex items-center gap-2">
              <h2 className="text-xl font-semibold text-base-content">{title}</h2>
              <span className="badge badge-ghost">{count}</span>
            </div>
            <p className="mt-1 text-sm text-base-content/70">{description}</p>
          </div>
          {actions}
        </div>

        {count === 0 ? (
          <div className="rounded-box border border-dashed border-base-300 px-5 py-8 text-center text-sm text-base-content/60">
            {emptyMessage}
          </div>
        ) : (
          children
        )}
      </div>
    </section>
  );
}
