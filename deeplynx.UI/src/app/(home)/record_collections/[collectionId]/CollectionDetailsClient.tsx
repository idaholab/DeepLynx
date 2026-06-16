"use client";

import AdditionalPropertiesEditor from "@/app/(home)/record/components/AdditionalPropertiesEditor";
import { useLanguage } from "@/app/contexts/Language";
import { ArrowLeftIcon } from "@heroicons/react/24/outline";
import { useRouter } from "next/navigation";
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
  const { t } = useLanguage();
  const { workspace, detailsController, recordsController, propertiesEditor } =
    useCollectionDetails({
      organizationId,
      projectId,
      initialCollection,
      initialCollectionRecords,
    });

  return (
    <div className="min-h-screen bg-base-200/30 px-4 py-6 lg:px-8">
      <div className="mx-auto max-w-7xl space-y-5">
        <div className="flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
          <div>
            <p className="text-xs font-semibold uppercase tracking-wide text-base-content/60">
              {t.translations.RECORD_COLLECTIONS}
            </p>
            <h1 className="mt-2 text-3xl font-bold text-base-content">
              {t.translations.RECORD_COLLECTIONS_COLLECTION_DETAILS}
            </h1>
            <p className="mt-2 max-w-3xl text-sm text-base-content/70">
              {t.translations.RECORD_COLLECTIONS_REVIEW_AND_MODIFY_DETAILS}
            </p>
          </div>
          <button
            type="button"
            className="btn btn-outline btn-sm"
            onClick={() => router.push("/record_collections")}
          >
            <ArrowLeftIcon className="size-4" />
            {t.translations.RECORD_COLLECTIONS_BACK_TO_COLLECTIONS}
          </button>
        </div>

        <div className="space-y-4">
          {workspace.tab === "Details" ? (
            <SelectedCollectionDetailsTab controller={detailsController} />
          ) : (
            <SelectedCollectionRecordsTab controller={recordsController} />
          )}
        </div>
      </div>

      <AdditionalPropertiesEditor {...propertiesEditor} />
    </div>
  );
}
