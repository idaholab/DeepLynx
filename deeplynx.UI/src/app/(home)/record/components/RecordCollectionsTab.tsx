"use client";

import React, { useEffect, useState } from "react";
import toast from "react-hot-toast";
import {
  PaginatedRecordCollectionsResponseDto,
  RecordCollectionResponseDto,
} from "@/app/(home)/types/responseDTOs";
import { useLanguage } from "@/app/contexts/Language";
import { getRecordCollectionsForRecord } from "@/app/lib/client_service/record_collection_services.client";
import PaginationControls from "../../components/PaginationControls";
import CollectionDashboardCard from "../../record_collections/components/CollectionDashboardCard";

interface Props {
  organizationId: number;
  projectId: number;
  recordId: number;
}

export default function RecordCollectionsTab({
  organizationId,
  projectId,
  recordId,
}: Props) {
  const { t } = useLanguage();

  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(10);

  const [pageData, setPageData] =
    useState<PaginatedRecordCollectionsResponseDto | null>(null);
  const collections = pageData?.items ?? [];
  const [isLoadingCollections, setIsLoadingCollections] = useState(true);
  const [collectionError, setCollectionError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    const fetchCollections = async () => {
      setIsLoadingCollections(true);
      setCollectionError(null);

      try {
        const result = await getRecordCollectionsForRecord(
          organizationId,
          projectId,
          recordId,
          { pageNumber, pageSize },
        );
        if (cancelled) return;
        setPageData(result);
      } catch (error) {
        if (cancelled) return;
        console.error("Error fetching record collections:", error);
        setPageData(null);
        setCollectionError(t.translations.FAILED_TO_LOAD_RECORD_COLLECTIONS);
        toast.error(t.translations.FAILED_TO_LOAD_RECORD_COLLECTIONS);
      } finally {
        if (!cancelled) setIsLoadingCollections(false);
      }
    };

    fetchCollections();

    return () => {
      cancelled = true;
    };
  }, [
    organizationId,
    projectId,
    recordId,
    pageNumber,
    pageSize,
    t.translations.FAILED_TO_LOAD_RECORD_COLLECTIONS,
  ]);

  useEffect(() => {
    setPageNumber(1);
  }, [organizationId, projectId, recordId]);

  if (isLoadingCollections) {
    return (
      <div className="mt-4 card bg-base-100 shadow-lg">
        <div className="card-body">
          <div className="flex items-center gap-3">
            <span className="loading loading-spinner loading-md" />
            <p>{t.translations.LOADING_RECORD_COLLECTIONS}</p>
          </div>
        </div>
      </div>
    );
  }

  if (collectionError) {
    return (
      <div className="mt-4 alert alert-error">
        <span>{collectionError}</span>
      </div>
    );
  }

  if (collections.length === 0) {
    return (
      <div className="mt-4 card bg-base-100 shadow-lg">
        <div className="card-body">
          <h3 className="card-title">{t.translations.RECORD_COLLECTIONS}</h3>
          <p className="opacity-80">
            {t.translations.RECORD_NOT_IN_ANY_COLLECTIONS}
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="mt-4 space-y-4 p-2">
      <div className="grid gap-4">
        {collections.map((collection) => (
          <CollectionDashboardCard
            key={collection.id}
            collection={collection}
            detailsHref={`/record_collections/${collection.id}?returnTo=${encodeURIComponent(`/record?recordId=${recordId}&projectId=${projectId}`)}`}
            showBadges={false}
          />
        ))}
      </div>

      {pageData && pageData.totalPages > 1 ? (
        <PaginationControls
          currentPage={pageData.pageNumber}
          pageSize={pageData.pageSize}
          totalPages={pageData.totalPages}
          onPageChange={setPageNumber}
          onPageSizeChange={(nextPageSize) => {
            setPageSize(nextPageSize);
            setPageNumber(1);
          }}
        />
      ) : null}
    </div>
  );
}
