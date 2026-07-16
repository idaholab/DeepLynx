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
    <main className="min-h-screen bg-base-200/30">
      <section className="border-b border-base-300/50 bg-base-100">
        <div className="mx-auto flex w-full max-w-7xl flex-col gap-5 px-3 py-5 sm:px-6 lg:px-8">
          <div className="flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
            <div>
              <p className="text-xs font-semibold uppercase tracking-wide text-base-content/60">
                {t.translations.RECORD_COLLECTIONS}
              </p>
              <h1 className="text-2xl font-bold text-base-content sm:text-3xl">
                {t.translations.RECORD_COLLECTIONS_NEW}
              </h1>
              <p className="mt-3 max-w-3xl text-base-content/70">
                {t.translations.RECORD_COLLECTIONS_CREATE_IN_ACTIVE_PROJECT}
              </p>
            </div>
          </div>
        </div>
      </section>

      <section className="mx-auto w-full max-w-7xl px-3 py-5 sm:px-6 lg:px-8">
        <NewCollectionTabContent controller={controller} />
      </section>
    </main>
  );
}
