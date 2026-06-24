"use client";

import AdditionalPropertiesEditor from "@/app/(home)/record/components/AdditionalPropertiesEditor";
import { useLanguage } from "@/app/contexts/Language";
import { ArrowLeftIcon } from "@heroicons/react/24/outline";
import { useRouter, useSearchParams } from "next/navigation";
import React from "react";
import {
  RecordCollectionResponseDto,
  RecordResponseDto,
} from "../../types/responseDTOs";
import SelectedCollectionDetailsTab from "../components/SelectedCollectionDetailsTab";
import SelectedCollectionRecordsTab from "../components/SelectedCollectionRecordsTab";
import { useCollectionDetails } from "./hooks/useCollectionDetails";

type Props = {
  organizationId: number;
  projectId: number;
  initialCollection: RecordCollectionResponseDto;
  initialCollectionRecords: RecordResponseDto[];
};

export default function CollectionDetailsClient({
  organizationId,
  projectId,
  initialCollection,
  initialCollectionRecords,
}: Props) {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { t } = useLanguage();
  const returnTo = searchParams.get("returnTo");
  const backHref =
    returnTo && returnTo.startsWith("/") && !returnTo.startsWith("//")
      ? returnTo
      : "/record_collections";
  const { workspace, detailsController, recordsController, propertiesEditor } =
    useCollectionDetails({
      organizationId,
      projectId,
      initialCollection,
      initialCollectionRecords,
    });

  return (
    <main className="min-h-screen bg-base-200/30">
      <section className="border-b border-base-300 bg-base-100">
        <div className="mx-auto flex w-full max-w-7xl flex-col gap-5 px-3 py-5 sm:px-6 lg:px-8">
          <div className="flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
            <div>
              <p className="text-xs font-semibold uppercase tracking-wide text-base-content/60">
                {t.translations.RECORD_COLLECTIONS}
              </p>
              <h1 className="text-2xl font-bold text-base-content sm:text-3xl">
                {t.translations.RECORD_COLLECTIONS_COLLECTION_DETAILS}
              </h1>
              <p className="mt-3 max-w-3xl text-base-content/70">
                {t.translations.RECORD_COLLECTIONS_REVIEW_AND_MODIFY_DETAILS}
              </p>
            </div>
            <button
              type="button"
              className="btn btn-outline btn-sm"
              onClick={() => router.push(backHref)}
            >
              <ArrowLeftIcon className="size-4" />
              {t.translations.RECORD_COLLECTIONS_BACK_TO_COLLECTIONS}
            </button>
          </div>
        </div>
      </section>

      <section className="mx-auto w-full max-w-7xl px-3 py-5 sm:px-6 lg:px-8">
        <div className="space-y-4">
          {workspace.tab === "Details" ? (
            <SelectedCollectionDetailsTab controller={detailsController} />
          ) : (
            <SelectedCollectionRecordsTab controller={recordsController} />
          )}
        </div>
      </section>

      <AdditionalPropertiesEditor {...propertiesEditor} />
    </main>
  );
}
