"use client";

import { ArrowRightIcon } from "@heroicons/react/24/outline";
import React from "react";

type Step = {
  label: string;
  detail: string;
  active: boolean;
};

type Props = {
  steps: Step[];
};

export default function NewCollectionStepIndicator({ steps }: Props) {
  return (
    <div className="flex flex-col gap-3 text-sm md:flex-row md:items-center">
      {steps.map((step, index) => (
        <React.Fragment key={step.label}>
          <div
            className={`flex-1 rounded-xl border px-4 py-3 ${
              step.active
                ? "border-primary bg-primary/10 text-base-content"
                : "border-base-300 bg-base-100 text-base-content/70"
            }`}
          >
            <p className="text-xs font-semibold uppercase">{step.label}</p>
            <p className="mt-1 font-medium">{step.detail}</p>
          </div>
          {index < steps.length - 1 ? (
            <div className="hidden text-base-content/40 md:flex md:flex-shrink-0">
              <ArrowRightIcon className="size-5" />
            </div>
          ) : null}
        </React.Fragment>
      ))}
    </div>
  );
}
