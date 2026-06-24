"use client";

import React from "react";

type Props = {
  title: string;
  subtitle?: string;
  action?: React.ReactNode;
  children: React.ReactNode;
  bordered?: boolean;
  elevated?: boolean;
  className?: string;
  bodyClassName?: string;
};

export default function SectionCard({
  title,
  subtitle,
  action,
  children,
  bordered = true,
  elevated = true,
  className = "",
  bodyClassName = "gap-4",
}: Props) {
  return (
    <section
      className={`card bg-base-100 ${elevated ? "shadow-sm" : ""} ${
        bordered ? "border border-base-300/50" : ""
      } ${className}`}
    >
      <div className={`card-body ${bodyClassName}`}>
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
