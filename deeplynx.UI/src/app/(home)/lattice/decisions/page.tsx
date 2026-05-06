"use client";

import React, { useCallback, useEffect, useRef, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import {
  ArrowLeftIcon,
  ArrowRightIcon,
  CheckCircleIcon,
  ChevronDownIcon,
  ChevronUpIcon,
  XCircleIcon,
} from "@heroicons/react/24/outline";
import PropertyTable from "@/app/(home)/record/components/PropertyTable";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import { useProjectSession } from "@/app/contexts/ProjectSessionProvider";
import {
  getExtractionStaging,
  listExtractions,
  promoteExtraction,
} from "@/app/lib/client_service/lattice_services.client";
import {
  ExtractionListItemDTO,
  ExtractionStagingResponseDTO,
  StagedClassDTO,
  StagedEdgeDTO,
  StagedRecordDTO,
  StagedRelationshipDTO,
} from "@/app/(home)/types/latticeDTOs";
import toast from "react-hot-toast";

type DetailTab = "records" | "classes" | "edges" | "relationships";

const POLLING_INTERVAL_MS = 3000;
const TERMINAL_STATUSES = ["complete", "failed", "promoted", "rejected"];

function validationBadgeClass(status: string | null) {
  if (status === "valid") return "badge-success";
  if (status === "invalid_schema") return "badge-error";
  if (status === "novel_discovery") return "badge-warning";
  return "badge-outline";
}

function extractionStatusBadgeClass(status: string) {
  if (status === "complete") return "badge-success";
  if (status === "failed" || status === "rejected") return "badge-error";
  if (status === "promoted") return "badge-success";
  if (status === "running") return "badge-info";
  return "badge-warning";
}

function EmptyState({ message }: { message: string }) {
  return (
    <div className="rounded-2xl border border-dashed border-base-300 bg-base-100 p-5 text-sm text-base-content/65">
      {message}
    </div>
  );
}

function parseAttributes(raw: string | null): Record<string, unknown> | null {
  if (!raw) return null;
  try {
    return JSON.parse(raw);
  } catch {
    return null;
  }
}

function parseNestedRows(
  obj: Record<string, unknown>,
): { label: string; value: React.ReactNode }[] {
  return Object.entries(obj).map(([key, value]) => {
    const label = key
      .split("_")
      .map((w) => w.charAt(0).toUpperCase() + w.slice(1))
      .join(" ");
    return {
      label,
      value: typeof value === "object" ? JSON.stringify(value) : String(value ?? ""),
    };
  });
}

function RecordCard({ record }: { record: StagedRecordDTO }) {
  const [expanded, setExpanded] = useState(false);
  const attrs = parseAttributes(record.attributes);

  return (
    <section className="rounded-2xl border border-base-300 bg-base-100 shadow-sm">
      <button
        type="button"
        className="flex w-full flex-wrap items-center justify-between gap-3 px-4 py-4 text-left"
        onClick={() => setExpanded((prev) => !prev)}
      >
        <div>
          <div className="flex flex-wrap items-center gap-2">
            <p className="text-lg font-semibold">{record.name}</p>
            {record.class_name && (
              <span className="badge badge-outline">{record.class_name}</span>
            )}
            {record.validation_status && (
              <span className={`badge ${validationBadgeClass(record.validation_status)}`}>
                {record.validation_status}
              </span>
            )}
          </div>
          <div className="mt-2 flex flex-wrap gap-2 text-xs text-base-content/60">
            <span className="rounded-full bg-base-200 px-3 py-1">
              Score: {(record.ensemble_score * 100).toFixed(0)}%
            </span>
            <span className="rounded-full bg-base-200 px-3 py-1">
              Frequency: {record.frequency}
            </span>
          </div>
        </div>
        {expanded ? (
          <ChevronUpIcon className="size-5 flex-shrink-0" />
        ) : (
          <ChevronDownIcon className="size-5 flex-shrink-0" />
        )}
      </button>

      {expanded && attrs ? (
        <div className="border-t border-base-300 px-4 py-5">
          <PropertyTable title="Attributes" rows={parseNestedRows(attrs)} />
        </div>
      ) : null}
    </section>
  );
}

function ClassCard({ cls }: { cls: StagedClassDTO }) {
  return (
    <div className="rounded-2xl border border-base-300 bg-base-200/50 p-4">
      <div className="flex items-center justify-between gap-2">
        <p className="font-semibold">{cls.name}</p>
        {cls.validation_status && (
          <span className={`badge ${validationBadgeClass(cls.validation_status)}`}>
            {cls.validation_status}
          </span>
        )}
      </div>
      {cls.ontology_class_id && (
        <p className="mt-2 text-xs text-base-content/60">
          Ontology class ID: {cls.ontology_class_id}
        </p>
      )}
    </div>
  );
}

function EdgeCard({ edge }: { edge: StagedEdgeDTO }) {
  return (
    <div className="rounded-2xl border border-base-300 bg-base-200/50 p-4">
      <div className="flex items-center justify-between gap-2">
        <p className="font-semibold">
          {edge.origin_record_name ?? "?"} →{" "}
          {edge.relationship_name ?? "?"} →{" "}
          {edge.destination_record_name ?? "?"}
        </p>
        {edge.validation_status && (
          <span className={`badge ${validationBadgeClass(edge.validation_status)}`}>
            {edge.validation_status}
          </span>
        )}
      </div>
      <div className="mt-2 flex flex-wrap gap-2 text-xs text-base-content/60">
        <span className="rounded-full bg-base-200 px-3 py-1">
          Score: {(edge.ensemble_score * 100).toFixed(0)}%
        </span>
        <span className="rounded-full bg-base-200 px-3 py-1">
          Frequency: {edge.frequency}
        </span>
      </div>
    </div>
  );
}

function RelationshipCard({ rel }: { rel: StagedRelationshipDTO }) {
  return (
    <div className="rounded-2xl border border-base-300 bg-base-200/50 p-4">
      <div className="flex items-center justify-between gap-2">
        <p className="font-semibold">{rel.name}</p>
        {rel.validation_status && (
          <span className={`badge ${validationBadgeClass(rel.validation_status)}`}>
            {rel.validation_status}
          </span>
        )}
      </div>
      {(rel.origin_class_name || rel.destination_class_name) && (
        <p className="mt-2 text-xs text-base-content/60">
          {rel.origin_class_name ?? "?"} → {rel.destination_class_name ?? "?"}
        </p>
      )}
    </div>
  );
}

function ExtractionListView() {
  const router = useRouter();
  const { organization } = useOrganizationSession();
  const { project } = useProjectSession();
  const [items, setItems] = useState<ExtractionListItemDTO[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const orgId = organization?.organizationId as number | undefined;
    const projId = project?.projectId as number | undefined;
    if (!orgId || !projId) return;

    listExtractions(orgId, projId)
      .then(setItems)
      .catch(() => setError("Failed to load extractions."))
      .finally(() => setIsLoading(false));
  }, [organization?.organizationId, project?.projectId]);

  const handleOpen = (item: ExtractionListItemDTO) => {
    const orgId = organization?.organizationId as number;
    const projId = project?.projectId as number;
    const params = new URLSearchParams({
      extractionId: String(item.id),
      projectId: String(projId),
      organizationId: String(orgId),
    });
    router.push(`/lattice/decisions?${params.toString()}`);
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-64">
        <span className="loading loading-spinner loading-lg" />
      </div>
    );
  }

  if (error) {
    return <div className="p-6"><p className="text-error">{error}</p></div>;
  }

  return (
    <div className="mx-3 space-y-6 pb-8 sm:mx-4 lg:mx-0 p-6">
      <div>
        <h1 className="text-2xl font-bold">My Extractions</h1>
        <p className="mt-1 text-sm text-base-content/70">
          Lattice extractions you have triggered.
        </p>
      </div>

      {items.length === 0 ? (
        <div className="rounded-2xl border border-dashed border-base-300 bg-base-100 p-8 text-center text-sm text-base-content/65">
          No extractions yet. Trigger one from a record page.
        </div>
      ) : (
        <div className="space-y-3">
          {items.map((item) => (
            <button
              key={item.id}
              type="button"
              className="w-full rounded-2xl border border-base-300 bg-base-100 px-5 py-4 text-left shadow-sm hover:bg-base-200/60 transition"
              onClick={() => handleOpen(item)}
            >
              <div className="flex flex-wrap items-center justify-between gap-3">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="font-semibold">Extraction #{item.id}</span>
                  <span className={`badge ${extractionStatusBadgeClass(item.status)}`}>
                    {item.status}
                  </span>
                  {item.mode && (
                    <span className="rounded-full bg-base-200 px-3 py-1 text-xs text-base-content/60">
                      {item.mode}
                    </span>
                  )}
                </div>
                <ArrowRightIcon className="size-4 text-base-content/40" />
              </div>
            </button>
          ))}
        </div>
      )}
    </div>
  );
}

function ExtractionDetailView({
  extractionId,
  projectId,
}: {
  extractionId: number;
  projectId: number;
}) {
  const router = useRouter();
  const { organization } = useOrganizationSession();

  const [staging, setStaging] = useState<ExtractionStagingResponseDTO | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isPromoting, setIsPromoting] = useState(false);
  const [activeTab, setActiveTab] = useState<DetailTab>("records");
  const pollingRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const fetchStaging = useCallback(async () => {
    if (!organization?.organizationId || !extractionId || !projectId) return;

    try {
      const data = await getExtractionStaging(
        organization.organizationId as number,
        projectId,
        extractionId,
      );
      setStaging(data);
      setError(null);

      if (!TERMINAL_STATUSES.includes(data.status)) {
        pollingRef.current = setTimeout(fetchStaging, POLLING_INTERVAL_MS);
      }
    } catch {
      setError("Failed to load extraction data.");
    } finally {
      setIsLoading(false);
    }
  }, [organization?.organizationId, extractionId, projectId]);

  useEffect(() => {
    void fetchStaging();
    return () => {
      if (pollingRef.current) clearTimeout(pollingRef.current);
    };
  }, [fetchStaging]);

  const handlePromote = async (approve: boolean) => {
    if (!organization?.organizationId || !staging) return;

    try {
      setIsPromoting(true);
      const result = await promoteExtraction(
        organization.organizationId as number,
        projectId,
        extractionId,
        approve,
      );
      setStaging(result);
      toast.success(approve ? "Extraction approved." : "Extraction rejected.");
    } catch {
      toast.error("Failed to process extraction.");
    } finally {
      setIsPromoting(false);
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-64">
        <span className="loading loading-spinner loading-lg" />
      </div>
    );
  }

  if (error || !staging) {
    return (
      <div className="p-6">
        <p className="text-error">{error ?? "No extraction data found."}</p>
      </div>
    );
  }

  const isRunning = !TERMINAL_STATUSES.includes(staging.status);
  const canDecide = staging.status === "complete";
  const tabs: DetailTab[] = ["records", "classes", "edges", "relationships"];

  return (
    <div className="mx-3 space-y-6 pb-8 sm:mx-4 lg:mx-0 p-6">
      <button
        type="button"
        className="btn btn-ghost btn-sm"
        onClick={() => router.push("/lattice/decisions")}
      >
        <ArrowLeftIcon className="size-4" />
        All Extractions
      </button>
      <section className="grid gap-6 xl:grid-cols-[0.95fr_1.45fr]">
        <aside className="rounded-3xl border border-base-300 bg-base-100 p-4 shadow-sm">
          <div className="border-b border-base-300 pb-4">
            <div className="flex items-center justify-between gap-2">
              <h2 className="text-lg font-semibold">Extraction #{staging.id}</h2>
              <span className={`badge ${extractionStatusBadgeClass(staging.status)}`}>
                {isRunning ? (
                  <>
                    <span className="loading loading-spinner loading-xs mr-1" />
                    {staging.status}
                  </>
                ) : (
                  staging.status
                )}
              </span>
            </div>
            {staging.mode && (
              <p className="mt-1 text-sm text-base-content/60">Mode: {staging.mode}</p>
            )}
          </div>

          <div className="mt-4 space-y-3">
            {(
              [
                { label: "Records", count: staging.records.length },
                { label: "Classes", count: staging.classes.length },
                { label: "Relationships", count: staging.relationships.length },
                { label: "Edges", count: staging.edges.length },
              ] as const
            ).map(({ label, count }) => (
              <div
                key={label}
                className="rounded-2xl border border-base-300 bg-base-200/50 px-4 py-3"
              >
                <p className="text-sm font-medium">{label}</p>
                <p className="text-2xl font-bold">{count}</p>
              </div>
            ))}
          </div>
        </aside>

        <section className="rounded-3xl border border-base-300 bg-base-100 p-5 shadow-sm">
          <div className="flex flex-col gap-4 border-b border-base-300 pb-5">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div>
                <h2 className="text-2xl font-semibold">Review Extraction</h2>
                <p className="mt-1 text-sm text-base-content/70">
                  {isRunning
                    ? "Extraction is in progress. This page will update automatically."
                    : canDecide
                      ? "Review the staged items below, then approve or reject the entire extraction."
                      : `This extraction has been ${staging.status}.`}
                </p>
              </div>

              {canDecide ? (
                <div className="flex flex-wrap gap-2">
                  <button
                    type="button"
                    className="btn btn-success btn-sm"
                    onClick={() => handlePromote(true)}
                    disabled={isPromoting}
                  >
                    {isPromoting ? (
                      <span className="loading loading-spinner loading-xs" />
                    ) : (
                      <CheckCircleIcon className="size-4" />
                    )}
                    Approve All
                  </button>
                  <button
                    type="button"
                    className="btn btn-outline btn-error btn-sm"
                    onClick={() => handlePromote(false)}
                    disabled={isPromoting}
                  >
                    {isPromoting ? (
                      <span className="loading loading-spinner loading-xs" />
                    ) : (
                      <XCircleIcon className="size-4" />
                    )}
                    Reject All
                  </button>
                </div>
              ) : null}
            </div>
          </div>

          <div className="mt-5 flex flex-wrap gap-2">
            {tabs.map((tab) => (
              <button
                key={tab}
                type="button"
                className={`btn btn-sm ${activeTab === tab ? "btn-primary" : "btn-ghost"}`}
                onClick={() => setActiveTab(tab)}
              >
                {tab.charAt(0).toUpperCase() + tab.slice(1)}
              </button>
            ))}
          </div>

          {activeTab === "records" ? (
            <div className="mt-5 space-y-3">
              {staging.records.length === 0 ? (
                <EmptyState message="No records were staged." />
              ) : (
                staging.records.map((record) => (
                  <RecordCard key={record.id} record={record} />
                ))
              )}
            </div>
          ) : null}

          {activeTab === "classes" ? (
            <div className="mt-5 space-y-3">
              {staging.classes.length === 0 ? (
                <EmptyState message="No classes were staged." />
              ) : (
                staging.classes.map((cls) => (
                  <ClassCard key={cls.id} cls={cls} />
                ))
              )}
            </div>
          ) : null}

          {activeTab === "edges" ? (
            <div className="mt-5 space-y-3">
              {staging.edges.length === 0 ? (
                <EmptyState message="No edges were staged." />
              ) : (
                staging.edges.map((edge) => (
                  <EdgeCard key={edge.id} edge={edge} />
                ))
              )}
            </div>
          ) : null}

          {activeTab === "relationships" ? (
            <div className="mt-5 space-y-3">
              {staging.relationships.length === 0 ? (
                <EmptyState message="No relationships were staged." />
              ) : (
                staging.relationships.map((rel) => (
                  <RelationshipCard key={rel.id} rel={rel} />
                ))
              )}
            </div>
          ) : null}
        </section>
      </section>
    </div>
  );
}

export default function LatticeDecisionsPage() {
  const searchParams = useSearchParams();
  const extractionIdParam = searchParams.get("extractionId");

  if (!extractionIdParam) {
    return <ExtractionListView />;
  }

  return (
    <ExtractionDetailView
      extractionId={Number(extractionIdParam)}
      projectId={Number(searchParams.get("projectId"))}
    />
  );
}
