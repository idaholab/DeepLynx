"use client";

import React, { useCallback, useEffect, useRef, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import {
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
  if (status === "promoted") return "badge-primary";
  if (status === "failed" || status === "rejected") return "badge-error";
  if (status === "running") return "badge-info";
  return "badge-warning";
}

function statusLabel(status: string) {
  if (status === "promoted") return "approved";
  return status;
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
          <PropertyTable title="Properties" rows={parseNestedRows(attrs)} />
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
          Existing Class ID: {cls.ontology_class_id}
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
        <div className="mt-3 flex items-center gap-2 text-xs">
          <div>
            <p className="text-base-content/40 uppercase tracking-wide" style={{ fontSize: "0.625rem" }}>Origin</p>
            <p className="mt-0.5 font-medium text-base-content/70">{rel.origin_class_name ?? "?"}</p>
          </div>
          <span className="mt-3 text-base-content/30">→</span>
          <div>
            <p className="text-base-content/40 uppercase tracking-wide" style={{ fontSize: "0.625rem" }}>Destination</p>
            <p className="mt-0.5 font-medium text-base-content/70">{rel.destination_class_name ?? "?"}</p>
          </div>
        </div>
      )}
    </div>
  );
}

function ExtractionDetailPanel({
  extractionId,
  organizationId,
  projectId,
  onStatusChange,
}: {
  extractionId: number;
  organizationId: number;
  projectId: number;
  onStatusChange?: () => void;
}) {
  const [staging, setStaging] = useState<ExtractionStagingResponseDTO | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isPromoting, setIsPromoting] = useState(false);
  const [activeTab, setActiveTab] = useState<DetailTab>("records");
  const pollingRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const fetchStaging = useCallback(async () => {
    try {
      const data = await getExtractionStaging(organizationId, projectId, extractionId);
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
  }, [organizationId, projectId, extractionId]);

  useEffect(() => {
    setIsLoading(true);
    setStaging(null);
    setError(null);
    setActiveTab("records");
    void fetchStaging();
    return () => {
      if (pollingRef.current) clearTimeout(pollingRef.current);
    };
  }, [fetchStaging]);

  const handlePromote = async (approve: boolean) => {
    if (!staging) return;
    try {
      setIsPromoting(true);
      await promoteExtraction(organizationId, projectId, extractionId, approve);
      toast.success(approve ? "Extraction approved." : "Extraction rejected.");
      await fetchStaging();
      onStatusChange?.();
    } catch {
      toast.error("Failed to process extraction.");
    } finally {
      setIsPromoting(false);
    }
  };

  if (isLoading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <span className="loading loading-spinner loading-lg" />
      </div>
    );
  }

  if (error || !staging) {
    return (
      <div className="p-4">
        <p className="text-error">{error ?? "No extraction data found."}</p>
      </div>
    );
  }

  const isRunning = !TERMINAL_STATUSES.includes(staging.status);
  const canDecide = staging.status === "complete";
  const tabs: DetailTab[] = ["records", "classes", "edges", "relationships"];

  return (
    <div className="flex flex-col gap-4">
      {/* Header: title + status + approve/reject */}
      <div className="flex flex-col gap-2">
        <h2 className="text-lg font-bold">Extraction #{staging.id}</h2>

        {/* Row 1: status + mode on left, buttons on right */}
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div className="flex flex-wrap items-center gap-2">
            <span className={`badge ${extractionStatusBadgeClass(staging.status)}`}>
              {isRunning ? (
                <>
                  <span className="loading loading-spinner loading-xs mr-1" />
                  {statusLabel(staging.status)}
                </>
              ) : (
                statusLabel(staging.status)
              )}
            </span>
            {staging.mode && (
              <span className="text-sm font-medium text-base-content/50 capitalize">
                {staging.mode}
              </span>
            )}
          </div>
          {canDecide && (
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
          )}
        </div>

        {/* Row 2: description on left, note on right */}
        <div className="flex flex-wrap items-start justify-between gap-2">
          <p className="text-sm text-base-content/70">
            {isRunning
              ? "Extraction is in progress. This page will update automatically."
              : canDecide
                ? "Review the staged items below, then approve or reject the entire extraction."
                : staging.status === "failed"
                  ? "This extraction failed to complete."
                  : staging.status === "rejected"
                    ? "This extraction was rejected."
                    : `This extraction has been ${statusLabel(staging.status)}.`}
          </p>
          {canDecide && (
            <p className="text-xs text-base-content/50 text-right max-w-[220px]">
              All-or-nothing. All items are promoted on approval, including{" "}
              <span className="font-medium text-error/70">invalid schema</span> items.
            </p>
          )}
        </div>
      </div>

      {/* Summary card */}
      <div className="rounded-2xl border border-base-300 bg-base-100 p-4 shadow-sm">
        <h3 className="mb-3 text-sm font-semibold text-base-content/70">Summary</h3>
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
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
              className="rounded-xl border border-base-300 bg-base-200/50 px-4 py-3 text-center"
            >
              <p className="text-xs font-medium text-base-content/60">{label}</p>
              <p className="text-2xl font-bold">{count}</p>
            </div>
          ))}
        </div>
      </div>

      {/* Items card with tabs */}
      <div className="rounded-2xl border border-base-300 bg-base-100 p-4 shadow-sm">
        <div className="flex flex-wrap gap-2">
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

        <div className="mt-4 space-y-3">
          {activeTab === "records" &&
            (staging.records.length === 0 ? (
              <EmptyState message="No records were staged." />
            ) : (
              staging.records.map((record) => <RecordCard key={record.id} record={record} />)
            ))}

          {activeTab === "classes" &&
            (staging.classes.length === 0 ? (
              <EmptyState message="No classes were staged." />
            ) : (
              staging.classes.map((cls) => <ClassCard key={cls.id} cls={cls} />)
            ))}

          {activeTab === "edges" &&
            (staging.edges.length === 0 ? (
              <EmptyState message="No edges were staged." />
            ) : (
              staging.edges.map((edge) => <EdgeCard key={edge.id} edge={edge} />)
            ))}

          {activeTab === "relationships" &&
            (staging.relationships.length === 0 ? (
              <EmptyState message="No relationships were staged." />
            ) : (
              staging.relationships.map((rel) => <RelationshipCard key={rel.id} rel={rel} />)
            ))}
        </div>
      </div>
    </div>
  );
}

function storageKey(projId: number) {
  return `lattice_selected_extraction_${projId}`;
}

export default function LatticeDecisionsPage() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { organization } = useOrganizationSession();
  const { project } = useProjectSession();

  const [items, setItems] = useState<ExtractionListItemDTO[]>([]);
  const [isListLoading, setIsListLoading] = useState(true);
  const [listError, setListError] = useState<string | null>(null);

  const selectedId = searchParams.get("extractionId")
    ? Number(searchParams.get("extractionId"))
    : null;

  const orgId = organization?.organizationId as number | undefined;
  const projId = project?.projectId as number | undefined;

  const refreshList = useCallback(() => {
    if (!orgId || !projId) return;
    listExtractions(orgId, projId)
      .then(setItems)
      .catch(() => setListError("Failed to load extractions."));
  }, [orgId, projId]);

  useEffect(() => {
    if (!orgId || !projId) return;
    setIsListLoading(true);
    listExtractions(orgId, projId)
      .then(setItems)
      .catch(() => setListError("Failed to load extractions."))
      .finally(() => setIsListLoading(false));
  }, [orgId, projId]);

  // Restore last selected extraction when arriving without a query param
  useEffect(() => {
    if (!projId || selectedId) return;
    const saved = localStorage.getItem(storageKey(projId));
    if (saved) router.replace(`/lattice/decisions?extractionId=${saved}`);
  }, [projId, selectedId, router]);

  const handleSelect = (id: number) => {
    if (projId) localStorage.setItem(storageKey(projId), String(id));
    router.replace(`/lattice/decisions?extractionId=${id}`);
  };

  return (
    <div className="mx-3 sm:mx-4 lg:mr-0 lg:ml-0">
      {/* Page header */}
      <div className="bg-base-200/40 px-3 sm:px-6 lg:px-12 p-4">
        <h1 className="text-xl sm:text-2xl font-bold text-base-content">Lattice</h1>
        <p className="mt-1 text-sm text-base-content/70">
          Extractions you have triggered for this project. Each staged item is scored and validated
          against the project ontology before human review. <span className="font-medium">Valid</span>{" "}
          items matched an existing ontology class or relationship.{" "}
          <span className="font-medium">Novel discovery</span> items involve known classes and relationships
          but in an unrecognized pattern.{" "}
          <span className="font-medium">Invalid schema</span> items could not be reconciled with the schema.
          Approving an extraction promotes <span className="font-medium">all</span> staged items into the
          knowledge graph and data schema, regardless of validation status.
        </p>
        <div className="mt-2">
          <span className="badge badge-warning badge-sm">Coming Soon</span>
          <span className="ml-2 text-sm text-base-content/70">Individual item approval and bulk approval for different statuses</span>
        </div>
      </div>

      <div className="px-3 sm:px-6 lg:px-12 py-6">
        <div className="grid gap-6 lg:grid-cols-[320px_1fr]">
          {/* Left: extraction list */}
          <aside className="rounded-2xl border border-base-300 bg-base-100 shadow-sm overflow-hidden self-start">
            <div className="border-b border-base-300 px-4 py-3">
              <h2 className="text-sm font-semibold text-base-content/70">Extractions</h2>
            </div>

            {isListLoading ? (
              <div className="flex h-32 items-center justify-center">
                <span className="loading loading-spinner loading-md" />
              </div>
            ) : listError ? (
              <p className="p-4 text-sm text-error">{listError}</p>
            ) : items.length === 0 ? (
              <p className="p-4 text-sm text-base-content/60">
                No extractions yet. Trigger one from a record page.
              </p>
            ) : (
              <ul className="divide-y divide-base-200 max-h-[60vh] overflow-y-auto">
                {items.map((item) => (
                  <li key={item.id}>
                    <button
                      type="button"
                      className={`flex w-full items-center justify-between gap-3 px-4 py-3 text-left transition hover:bg-base-200/60 ${selectedId === item.id ? "bg-base-200/80 font-semibold" : ""
                        }`}
                      onClick={() => handleSelect(item.id)}
                    >
                      <div className="min-w-0">
                        <p className="truncate text-sm">Extraction #{item.id}</p>
                        <div className="mt-2 grid grid-cols-2 gap-x-2">
                          <div>
                            <p className="text-base-content/40 uppercase tracking-wide" style={{ fontSize: "0.625rem" }}>Status</p>
                            <div className="mt-0.5 flex h-4 items-center">
                              <span className={`badge badge-xs ${extractionStatusBadgeClass(item.status)}`}>
                                {statusLabel(item.status)}
                              </span>
                            </div>
                          </div>
                          {item.mode && (
                            <div>
                              <p className="text-base-content/40 uppercase tracking-wide" style={{ fontSize: "0.625rem" }}>Mode</p>
                              <div className="mt-0.5 flex h-4 items-center">
                                <p className="text-xs font-medium text-base-content/70 capitalize">{item.mode}</p>
                              </div>
                            </div>
                          )}
                        </div>
                      </div>
                      <ArrowRightIcon
                        className={`size-4 flex-shrink-0 transition ${selectedId === item.id
                          ? "text-primary"
                          : "text-base-content/30"
                          }`}
                      />
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </aside>

          {/* Right: detail panel */}
          <div>
            {selectedId && orgId && projId ? (
              <ExtractionDetailPanel
                key={selectedId}
                extractionId={selectedId}
                organizationId={orgId}
                projectId={projId}
                onStatusChange={refreshList}
              />
            ) : (
              <div className="flex h-64 items-center justify-center rounded-2xl border border-dashed border-base-300 bg-base-100 text-sm text-base-content/50">
                Select an extraction from the list to review it.
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
