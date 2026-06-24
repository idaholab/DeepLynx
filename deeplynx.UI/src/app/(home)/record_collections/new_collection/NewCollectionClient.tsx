"use client";

import { useLanguage } from "@/app/contexts/Language";
import React from "react";
import NewCollectionTabContent from "./components/NewCollectionTabContent";
import { useNewCollectionWorkflow } from "./hooks/useNewCollectionWorkflow";

type Props = {
  organizationId: number;
  projectId: number;
};

export default function NewCollectionClient({
  organizationId,
  projectId,
}: Props) {
  const { t } = useLanguage();
  const { controller } = useNewCollectionWorkflow({
    organizationId,
    projectId,
  });

  return (
    <main className="min-h-screen bg-base-200/30 px-4 py-6 lg:px-8">
      <div className="mx-auto max-w-7xl space-y-5">
        <div className="flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
          <div>
            <p className="text-xs font-semibold uppercase tracking-wide text-base-content/60">
              {t.translations.RECORD_COLLECTIONS}
            </p>
            <h1 className="mt-2 text-3xl font-bold text-base-content">
              {t.translations.RECORD_COLLECTIONS_NEW}
            </h1>
            <p className="mt-2 max-w-3xl text-sm text-base-content/70">
              {t.translations.RECORD_COLLECTIONS_CREATE_IN_ACTIVE_PROJECT}
            </p>
          </div>
        </div>

        <NewCollectionTabContent controller={controller} />
      </div>
    </main>
  );
}
