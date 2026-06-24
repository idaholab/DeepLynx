"use client";

import React from "react";

type Props = {
  title: string;
  subtitle?: string;
  action?: React.ReactNode;
  children: React.ReactNode;
};

export default function SectionCard({
  title,
  subtitle,
  action,
  children,
}: Props) {
  return (
    <section className="card border border-base-300 bg-base-100 shadow-sm">
      <div className="card-body gap-4">
        <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
          <div>
            <h2 className="text-lg font-semibold text-base-content">{title}</h2>
            {subtitle ? (
              <p className="text-sm text-base-content/70">{subtitle}</p>
            ) : null}
          </div>
          {action}
        </div>
        {children}
      </div>
    </section>
  );
}
