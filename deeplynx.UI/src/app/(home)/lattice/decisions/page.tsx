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
  rejectExtraction
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
import { useLanguage } from "@/app/contexts/Language";
import { BetaBadge } from "@/app/(home)/components/BetaBadge";
import { isInsightHidden } from "@/app/lib/feature_flags";

type DetailTab = "records" | "classes" | "edges" | "relationships";

const NOT_RUNNING_STATUSES = ["complete", "failed", "promoted", "rejected", "partially_promoted"];

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

function statusLabel(
  status: string,
  translations: { LATTICE_APPROVED_STATUS: string },
) {
  if (status === "promoted") return translations.LATTICE_APPROVED_STATUS;
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
      value:
        typeof value === "object" ? JSON.stringify(value) : String(value ?? ""),
    };
  });
}

function DecisionButtons({
  isApproved,
  isRejected,
  onToggle,
}: {
  isApproved: boolean;
  isRejected: boolean;
  onToggle: (action: "approve" | "reject") => void;
}) {
  return (
    <div className="join flex-shrink-0" onClick={(e) => e.stopPropagation()}>
      <button
        type="button"
        className={`btn join-item btn-xs ${isApproved ? "btn-success" : "btn-outline btn-success"
          }`}
        onClick={() => onToggle("approve")}
      >
        <CheckCircleIcon className="size-4" />
        Approve
      </button>
      <button
        type="button"
        className={`btn join-item btn-xs ${isRejected ? "btn-error" : "btn-outline btn-error"
          }`}
        onClick={() => onToggle("reject")}
      >
        <XCircleIcon className="size-4" />
        Reject
      </button>
    </div>
  );
}

function RecordCard({ record, isApproved, isRejected, onToggle, locked }:
  {
    record: StagedRecordDTO; isApproved: boolean;
    isRejected: boolean;
    onToggle: (action: "approve" | "reject") => void;
    locked: boolean;
  }) {
  const { t } = useLanguage();
  const [expanded, setExpanded] = useState(false);
  const attrs = parseAttributes(record.attributes);

  return (
    <section className="rounded-2xl border border-base-300 bg-base-100 shadow-sm">
      <button
        type="button"
        className="flex w-full flex-wrap items-center justify-between gap-3 px-4 py-4 text-left"
        onClick={() => setExpanded((prev) => !prev)}
      >
        <div className="flex w-full flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
          <div className="min-w-0 flex-1">
            <div className="flex flex-wrap items-center gap-2">
              <p className="text-lg font-semibold break-words">{record.name}</p>

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
                {t.translations.LATTICE_SCORE}: {(record.ensemble_score * 100).toFixed(0)}%
              </span>

              <span className="rounded-full bg-base-200 px-3 py-1">
                {t.translations.LATTICE_FREQUENCY}: {record.frequency}
              </span>
            </div>
          </div>

          <div className="flex justify-end sm:ml-4 shrink-0">
            <DecisionButtons
              isApproved={isApproved}
              isRejected={isRejected}
              onToggle={onToggle}
            />
          </div>
        </div>

        {expanded ? (
          <ChevronUpIcon className="size-5 shrink-0" />
        ) : (
          <ChevronDownIcon className="size-5 shrink-0" />
        )}
      </button>

      {expanded && attrs ? (
        <div className="border-t border-base-300 px-4 py-5">
          <PropertyTable
            title={t.translations.LATTICE_PROPERTIES_TITLE}
            rows={parseNestedRows(attrs)}
          />
        </div>
      ) : null}
    </section>
  );
}

function ClassCard({ cls, isApproved, isRejected, onToggle, locked }:
  {
    cls: StagedClassDTO; isApproved: boolean;
    isRejected: boolean;
    onToggle: (action: "approve" | "reject") => void;
    locked: boolean;
  }) {
  const { t } = useLanguage();
  return (
    <div className="rounded-2xl border border-base-300 bg-base-200/50 p-4">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2">
            <p className="font-semibold break-words">{cls.name}</p>

            {cls.validation_status && (
              <span className={`badge ${validationBadgeClass(cls.validation_status)}`}>
                {cls.validation_status}
              </span>
            )}
          </div>

          {cls.ontology_class_id && (
            <p className="mt-2 text-xs text-base-content/60">
              {t.translations.LATTICE_EXISTING_CLASS_ID} {cls.ontology_class_id}
            </p>
          )}
        </div>

        <div className="flex justify-end sm:ml-4 shrink-0">
          {cls.ontology_class_id ? (
            <span className="badge badge-info badge-outline">
              Already in project
            </span>
          ) : (
            <DecisionButtons
              isApproved={isApproved}
              isRejected={isRejected}
              onToggle={onToggle}
            />
          )}
        </div>
      </div>
    </div>
  );
}

function EdgeCard({ edge, isApproved,
  isRejected,
  onToggle,
  locked, }: {
    edge: StagedEdgeDTO; isApproved: boolean;
    isRejected: boolean;
    onToggle: (action: "approve" | "reject") => void;
    locked: boolean;
  }) {
  const { t } = useLanguage();
  return (
    <div className="rounded-2xl border border-base-300 bg-base-200/50 p-4">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2">
            <p className="font-semibold break-words">
              {edge.origin_record_name ?? "?"} → {edge.relationship_name ?? "?"} →{" "}
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
              {t.translations.LATTICE_SCORE}: {(edge.ensemble_score * 100).toFixed(0)}%
            </span>

            <span className="rounded-full bg-base-200 px-3 py-1">
              {t.translations.LATTICE_FREQUENCY}: {edge.frequency}
            </span>
          </div>
        </div>

        <div className="flex justify-end sm:ml-4 shrink-0">
          <DecisionButtons
            isApproved={isApproved}
            isRejected={isRejected}
            onToggle={onToggle}
          />
        </div>
      </div>
    </div>
  );
}

function RelationshipCard({ rel, isApproved,
  isRejected,
  onToggle,
  locked, }: {
    rel: StagedRelationshipDTO; isApproved: boolean;
    isRejected: boolean;
    onToggle: (action: "approve" | "reject") => void;
    locked: boolean;
  }) {
  const { t } = useLanguage();
  return (
    <div className="rounded-2xl border border-base-300 bg-base-200/50 p-4">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2">
            <p className="font-semibold break-words">{rel.name}</p>

            {rel.validation_status && (
              <span className={`badge ${validationBadgeClass(rel.validation_status)}`}>
                {rel.validation_status}
              </span>
            )}
          </div>

          {rel.ontology_relationship_id && (
            <p className="mt-2 text-xs text-base-content/60">
              Existing relationship ID: {rel.ontology_relationship_id}
            </p>
          )}

          {(rel.origin_class_name || rel.destination_class_name) && (
            <div className="mt-3 flex items-center gap-2 text-xs">
              <div>
                <p className="text-base-content/40 uppercase tracking-wide" style={{ fontSize: "0.625rem" }}>
                  {t.translations.LATTICE_ORIGIN}
                </p>
                <p className="mt-0.5 font-medium text-base-content/70">
                  {rel.origin_class_name ?? "?"}
                </p>
              </div>

              <span className="mt-3 text-base-content/30">→</span>

              <div>
                <p className="text-base-content/40 uppercase tracking-wide" style={{ fontSize: "0.625rem" }}>
                  {t.translations.LATTICE_DESTINATION}
                </p>
                <p className="mt-0.5 font-medium text-base-content/70">
                  {rel.destination_class_name ?? "?"}
                </p>
              </div>
            </div>
          )}
        </div>

        <div className="flex justify-end sm:ml-4 shrink-0">
          {rel.ontology_relationship_id ? (
            <span className="badge badge-info badge-outline">
              Already in project
            </span>
          ) : (
            <DecisionButtons
              isApproved={isApproved}
              isRejected={isRejected}
              onToggle={onToggle}
            />
          )}
        </div>
      </div>
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
  const [staging, setStaging] = useState<ExtractionStagingResponseDTO | null>(
    null,
  );
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isPromoting, setIsPromoting] = useState(false);
  const [activeTab, setActiveTab] = useState<DetailTab>("records");
  const { t } = useLanguage();

  type ItemType = "records" | "classes" | "edges" | "relationships";

  const [approved, setApproved] = useState<Record<ItemType, Set<number>>>({
    records: new Set(), classes: new Set(), edges: new Set(), relationships: new Set(),
  });
  const [rejected, setRejected] = useState<Record<ItemType, Set<number>>>({
    records: new Set(), classes: new Set(), edges: new Set(), relationships: new Set(),
  });

  const approveByStatus = (status: string) => {
    setApproved((prev) => ({
      records: new Set([
        ...prev.records,
        ...visibleRecords
          .filter((record) => record.validation_status === status)
          .map((record) => record.id),
      ]),
      classes: new Set([
        ...prev.classes,
        ...visibleClasses
          .filter((cls) => !cls.ontology_class_id && cls.validation_status === status)
          .map((cls) => cls.id),
      ]),
      edges: new Set([
        ...prev.edges,
        ...visibleEdges
          .filter((edge) => edge.validation_status === status)
          .map((edge) => edge.id),
      ]),
      relationships: new Set([
        ...prev.relationships,
        ...visibleRelationships
          .filter(
            (rel) =>
              !rel.ontology_relationship_id &&
              rel.validation_status === status
          )
          .map((rel) => rel.id),
      ]),
    }));

    setRejected((prev) => ({
      records: new Set([...prev.records].filter(
        (id) => !visibleRecords.some((record) => record.id === id && record.validation_status === status),
      )),
      classes: new Set([...prev.classes].filter(
        (id) => !visibleClasses.some((cls) => cls.id === id && cls.validation_status === status),
      )),
      edges: new Set([...prev.edges].filter(
        (id) => !visibleEdges.some((edge) => edge.id === id && edge.validation_status === status),
      )),
      relationships: new Set([...prev.relationships].filter(
        (id) => !visibleRelationships.some((rel) => rel.id === id && rel.validation_status === status),
      )),
    }));
  };
  const requestIdRef = useRef(0);

  const fetchStaging = useCallback(async () => {
    const myRequestId = ++requestIdRef.current;
    try {
      const data = await getExtractionStaging(organizationId, projectId, extractionId);
      if (requestIdRef.current !== myRequestId) return; // stale — drop it
      setStaging(data);
      setApproved((prev) => ({
        records: new Set([...prev.records].filter((id) =>
          data.records.some((r) => r.id === id && !r.promoted_id && !r.rejected),
        )),
        classes: new Set([...prev.classes].filter((id) =>
          data.classes.some((c) => c.id === id && !c.promoted_id && !c.rejected && !c.ontology_class_id),
        )),
        edges: new Set([...prev.edges].filter((id) =>
          data.edges.some((e) => e.id === id && !e.promoted_id && !e.rejected),
        )),
        relationships: new Set([...prev.relationships].filter((id) =>
          data.relationships.some((r) => r.id === id && !r.promoted_id && !r.rejected && !r.ontology_relationship_id),
        )),
      }));

      setRejected((prev) => ({
        records: new Set([...prev.records].filter((id) =>
          data.records.some((r) => r.id === id && !r.promoted_id && !r.rejected),
        )),
        classes: new Set([...prev.classes].filter((id) =>
          data.classes.some((c) => c.id === id && !c.promoted_id && !c.rejected && !c.ontology_class_id),
        )),
        edges: new Set([...prev.edges].filter((id) =>
          data.edges.some((e) => e.id === id && !e.promoted_id && !e.rejected),
        )),
        relationships: new Set([...prev.relationships].filter((id) =>
          data.relationships.some((r) => r.id === id && !r.promoted_id && !r.rejected && !r.ontology_relationship_id),
        )),
      }));
      setError(null);
    } catch {
      if (requestIdRef.current !== myRequestId) return;
      setError(t.translations.LATTICE_FAILED_LOAD_EXTRACTION);
    } finally {
      if (requestIdRef.current === myRequestId) setIsLoading(false);
    }
  }, [
    organizationId,
    projectId,
    extractionId,
    t.translations.LATTICE_FAILED_LOAD_EXTRACTION,
  ]);

  const toggleItem = (type: ItemType, id: number, action: "approve" | "reject") => {
    const setter = action === "approve" ? setApproved : setRejected;
    const otherSetter = action === "approve" ? setRejected : setApproved;
    setter(prev => {
      const next = new Set(prev[type]);
      next.has(id) ? next.delete(id) : next.add(id);
      return { ...prev, [type]: next };
    });
    // Uncheck the opposite column if checking this one
    otherSetter(prev => {
      const next = new Set(prev[type]);
      next.delete(id);
      return { ...prev, [type]: next };
    });
  };

  useEffect(() => {
    setIsLoading(true);
    setStaging(null);
    setError(null);
    setActiveTab("records");
    setApproved({ records: new Set(), classes: new Set(), edges: new Set(), relationships: new Set() });
    setRejected({ records: new Set(), classes: new Set(), edges: new Set(), relationships: new Set() });
    void fetchStaging();
  }, [fetchStaging]);

  useEffect(() => {
    if (!staging || staging.status !== "running") return;

    const timeoutId = window.setTimeout(() => {
      void fetchStaging();
    }, 3000);

    return () => window.clearTimeout(timeoutId);
  }, [staging?.status, fetchStaging]);

  const handleSave = async () => {
    if (!staging) return;
    try {
      setIsPromoting(true);
      const hasApprovals = (["records", "classes", "edges", "relationships"] as ItemType[])
        .some(t => approved[t].size > 0);
      const hasRejections = (["records", "classes", "edges", "relationships"] as ItemType[])
        .some(t => rejected[t].size > 0);

      if (hasApprovals) {
        await promoteExtraction(organizationId, projectId, extractionId, {
          record_ids: [...approved.records],
          class_ids: [...approved.classes],
          edge_ids: [...approved.edges],
          relationship_ids: [...approved.relationships]
        });
        await fetchStaging();
      }
      if (hasRejections) {
        await rejectExtraction(organizationId, projectId, extractionId, {
          record_ids: [...rejected.records],
          class_ids: [...rejected.classes],
          edge_ids: [...rejected.edges],
          relationship_ids: [...rejected.relationships],
          reject_by_status: [],
          reject_all_remaining: false,
        });
        await fetchStaging();
      }
      toast.success(t.translations.LATTICE_EXTRACTION_APPROVED_TOAST);

      await fetchStaging();
      onStatusChange?.();
      setApproved({ records: new Set(), classes: new Set(), edges: new Set(), relationships: new Set() });
      setRejected({ records: new Set(), classes: new Set(), edges: new Set(), relationships: new Set() });
    } catch (error: any) {
      const data = error?.response?.data;

      const apiMessage =
        data?.message ??
        data?.detail ??
        data?.title ??
        (Array.isArray(data?.errors) ? data.errors.join("\n") : undefined) ??
        (typeof data === "string" ? data : undefined) ??
        error?.message ??
        t.translations.LATTICE_PROCESS_FAILED;

      toast.error(apiMessage);
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
        <p className="text-error">
          {error ?? t.translations.LATTICE_NO_EXTRACTION_DATA}
        </p>
      </div>
    );
  }

  const isRunning = !NOT_RUNNING_STATUSES.includes(staging.status);
  const canDecide =
    staging.status === "complete" ||
    staging.status === "partially_promoted";
  const tabs: DetailTab[] = ["classes", "relationships", "records", "edges"];

  const tabLabels: Record<DetailTab, string> = {
    records: t.translations.RECORDS,
    classes: t.translations.CLASSES,
    edges: t.translations.LATTICE_EDGES,
    relationships: t.translations.RELATIONSHIPS,
  };

  const visibleRecords = staging.records.filter(
    (record) => !record.promoted_id && !record.rejected,
  );

  const visibleClasses = staging.classes.filter(
    (cls) => !cls.promoted_id && !cls.rejected,
  );

  const visibleEdges = staging.edges.filter(
    (edge) => !edge.promoted_id && !edge.rejected,
  );

  const visibleRelationships = staging.relationships.filter(
    (rel) => !rel.promoted_id && !rel.rejected,
  );

  const countByStatus = (status: string) =>
    visibleRecords.filter((r) => r.validation_status === status).length +
    visibleClasses.filter(
      (c) => !c.ontology_class_id && c.validation_status === status,
    ).length +
    visibleEdges.filter((e) => e.validation_status === status).length +
    visibleRelationships.filter(
      (r) => !r.ontology_relationship_id && r.validation_status === status,
    ).length;

  const validCount = countByStatus("valid");
  const novelDiscoveryCount = countByStatus("novel_discovery");

  const hasPendingDecisions = (
    ["records", "classes", "edges", "relationships"] as ItemType[]
  ).some(
    (type) => approved[type].size > 0 || rejected[type].size > 0,
  );

  return (
    <div className="flex flex-col gap-4">
      {/* Header: title + status + approve/reject */}
      <div className="flex flex-col gap-2">
        <h2 className="text-lg font-bold">
          {t.translations.LATTICE_EXTRACTION_NUMBER}
          {staging.id}
        </h2>

        {/* Row 1: status + mode on left, buttons on right */}
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div className="flex flex-wrap items-center gap-2">
            <span
              className={`badge ${extractionStatusBadgeClass(staging.status)}`}
            >
              {isRunning ? (
                <>
                  <span className="loading loading-spinner loading-xs mr-1" />
                  {statusLabel(staging.status, t.translations)}
                </>
              ) : (
                statusLabel(staging.status, t.translations)
              )}
            </span>
            {staging.mode && (
              <span className="text-sm font-medium text-base-content/50 capitalize">
                {staging.mode}
              </span>
            )}
          </div>
        </div>

        {/* Row 2: description on left, note on right */}
        <div className="flex flex-wrap items-start justify-between gap-2">
          <p className="text-sm text-base-content/70">
            {isRunning
              ? t.translations.LATTICE_EXTRACTION_RUNNING
              : canDecide
                ? t.translations.LATTICE_EXTRACTION_REVIEW
                : staging.status === "failed"
                  ? t.translations.LATTICE_EXTRACTION_FAILED_MSG
                  : staging.status === "rejected"
                    ? t.translations.LATTICE_EXTRACTION_REJECTED_MSG
                    : `${t.translations.LATTICE_EXTRACTION_BEEN} ${statusLabel(staging.status, t.translations)}.`}
          </p>
          {canDecide && (
            <div className="flex flex-wrap gap-2">
              <button
                type="button"
                className="btn btn-outline btn-success btn-sm"
                onClick={() => approveByStatus("valid")}
                disabled={isPromoting || validCount === 0}
              >
                <CheckCircleIcon className="size-4" />
                Approve valid ({validCount})
              </button>

              <button
                type="button"
                className="btn btn-outline btn-warning btn-sm"
                onClick={() => approveByStatus("novel_discovery")}
                disabled={isPromoting || novelDiscoveryCount === 0}
              >
                <CheckCircleIcon className="size-4" />
                Approve novel discoveries ({novelDiscoveryCount})
              </button>

              <button
                type="button"
                className="btn btn-primary btn-sm"
                onClick={handleSave}
                disabled={isPromoting || !hasPendingDecisions}
              >
                {isPromoting ? <span className="loading loading-spinner loading-xs" /> : null}
                Save
              </button>
            </div>
          )}
        </div>
      </div>

      {/* Summary card */}
      <div className="rounded-2xl border border-base-300 bg-base-100 p-4 shadow-sm">
        <h3 className="mb-3 text-sm font-semibold text-base-content/70">
          {t.translations.LATTICE_SUMMARY}
        </h3>
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
          {(
            [
              { label: t.translations.RECORDS, count: staging.records.length },
              { label: t.translations.CLASSES, count: staging.classes.length },
              {
                label: t.translations.RELATIONSHIPS,
                count: staging.relationships.length,
              },
              {
                label: t.translations.LATTICE_EDGES,
                count: staging.edges.length,
              },
            ] as const
          ).map(({ label, count }) => (
            <div
              key={label}
              className="rounded-xl border border-base-300 bg-base-200/50 px-4 py-3 text-center"
            >
              <p className="text-xs font-medium text-base-content/60">
                {label}
              </p>
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
              {tabLabels[tab]}
            </button>
          ))}
        </div>

        <div className="mt-4 space-y-3">
          {activeTab === "records" &&
            (visibleRecords.length === 0 ? (
              <EmptyState message={t.translations.LATTICE_NO_RECORDS_STAGED} />
            ) : (
              visibleRecords.map((record) => (
                <RecordCard key={record.id} record={record} isApproved={approved.records.has(record.id)}
                  isRejected={rejected.records.has(record.id)}
                  locked={!!record.promoted_id || record.rejected}
                  onToggle={(action) => toggleItem("records", record.id, action)} />
              ))
            ))}

          {activeTab === "classes" &&
            (visibleClasses.length === 0 ? (
              <EmptyState message={t.translations.LATTICE_NO_CLASSES_STAGED} />
            ) : (
              visibleClasses.map((cls) => <ClassCard key={cls.id} cls={cls} isApproved={approved.classes.has(cls.id)}
                isRejected={rejected.classes.has(cls.id)}
                locked={!!cls.promoted_id || cls.rejected}
                onToggle={(action) => toggleItem("classes", cls.id, action)} />)
            ))}

          {activeTab === "edges" &&
            (visibleEdges.length === 0 ? (
              <EmptyState message={t.translations.LATTICE_NO_EDGES_STAGED} />
            ) : (
              visibleEdges.map((edge) => (
                <EdgeCard key={edge.id} edge={edge} isApproved={approved.edges.has(edge.id)}
                  isRejected={rejected.edges.has(edge.id)}
                  locked={!!edge.promoted_id || edge.rejected}
                  onToggle={(action) => toggleItem("edges", edge.id, action)} />
              ))
            ))}

          {activeTab === "relationships" &&
            (visibleRelationships.length === 0 ? (
              <EmptyState
                message={t.translations.LATTICE_NO_RELATIONSHIPS_STAGED}
              />
            ) : (
              visibleRelationships.map((rel) => (
                <RelationshipCard key={rel.id} rel={rel} isApproved={approved.relationships.has(rel.id)}
                  isRejected={rejected.relationships.has(rel.id)}
                  locked={!!rel.promoted_id || rel.rejected}
                  onToggle={(action) => toggleItem("relationships", rel.id, action)} />
              ))
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
  const { t } = useLanguage();
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
  const insightHidden = isInsightHidden();

  const refreshList = useCallback(() => {
    if (!orgId || !projId) return;
    listExtractions(orgId, projId)
      .then(setItems)
      .catch(() =>
        setListError(t.translations.LATTICE_FAILED_LOAD_EXTRACTIONS),
      );
  }, [orgId, projId, t.translations.LATTICE_FAILED_LOAD_EXTRACTIONS]);

  useEffect(() => {
    if (insightHidden) {
      router.replace("/");
    }
  }, [insightHidden, router]);

  useEffect(() => {
    if (insightHidden) return;
    if (!orgId || !projId) return;
    setIsListLoading(true);
    listExtractions(orgId, projId)
      .then(setItems)
      .catch(() => setListError(t.translations.LATTICE_FAILED_LOAD_EXTRACTIONS))
      .finally(() => setIsListLoading(false));
  }, [
    insightHidden,
    orgId,
    projId,
    t.translations.LATTICE_FAILED_LOAD_EXTRACTIONS,
  ]);

  // Restore last selected extraction when arriving without a query param
  useEffect(() => {
    if (insightHidden) return;
    if (!projId || selectedId) return;
    const saved = localStorage.getItem(storageKey(projId));
    if (saved) router.replace(`/lattice/decisions?extractionId=${saved}`);
  }, [insightHidden, projId, selectedId, router]);

  const [pendingId, setPendingId] = useState<number | null>(null);

  const handleSelect = (id: number) => {
    setPendingId(id);
    if (projId) localStorage.setItem(storageKey(projId), String(id));
    router.replace(`/lattice/decisions?extractionId=${id}`);
  };

  if (insightHidden) {
    return null;
  }

  return (
    <main className="min-h-screen bg-base-200/30">
      {/* Page header */}
      <section className="border-b border-base-300 bg-base-100">
        <div className="mx-auto flex w-full max-w-7xl flex-col gap-5 px-3 py-5 sm:px-6 lg:px-8">
          <div>
            <p className="text-xs font-semibold uppercase tracking-wide text-base-content/60">
              {t.translations.LATTICE_EXTRACTIONS_PANEL_TITLE}
            </p>
            <div className="flex flex-wrap items-center gap-3">
              <h1 className="text-2xl font-bold text-base-content sm:text-3xl">
                {t.translations.LATTICE_PAGE_TITLE}
              </h1>
              <BetaBadge size="sm" />
            </div>
            <p className="mt-3 max-w-4xl text-base-content/70">
              {t.translations.LATTICE_PAGE_DESCRIPTION_INTRO}{" "}
              <span className="font-medium">
                {t.translations.LATTICE_VALID_LABEL}
              </span>{" "}
              {t.translations.LATTICE_VALID_DESCRIPTION}{" "}
              <span className="font-medium">
                {t.translations.LATTICE_NOVEL_DISCOVERY_LABEL}
              </span>{" "}
              {t.translations.LATTICE_NOVEL_DISCOVERY_DESCRIPTION}{" "}
              <span className="font-medium">
                {t.translations.LATTICE_INVALID_SCHEMA_LABEL}
              </span>{" "}
              {t.translations.LATTICE_INVALID_SCHEMA_DESCRIPTION}{" "}
              {t.translations.LATTICE_APPROVE_PROMOTES_ALL}
            </p>
          </div>
        </div>
      </section>

      <section className="mx-auto w-full max-w-7xl px-3 py-5 sm:px-6 lg:px-8">
        <div className="grid gap-6 lg:grid-cols-[320px_1fr]">
          {/* Left: extraction list */}
          <aside className="rounded-2xl border border-base-300 bg-base-100 shadow-sm overflow-hidden self-start">
            <div className="border-b border-base-300 px-4 py-3">
              <h2 className="text-sm font-semibold text-base-content/70">
                {t.translations.LATTICE_EXTRACTIONS_PANEL_TITLE}
              </h2>
            </div>

            {isListLoading ? (
              <div className="flex h-32 items-center justify-center">
                <span className="loading loading-spinner loading-md" />
              </div>
            ) : listError ? (
              <p className="p-4 text-sm text-error">{listError}</p>
            ) : items.length === 0 ? (
              <p className="p-4 text-sm text-base-content/60">
                {t.translations.LATTICE_NO_EXTRACTIONS}
              </p>
            ) : (
              <ul className="divide-y divide-base-200 max-h-[60vh] overflow-y-auto">
                {items.map((item) => (
                  <li key={item.id}>
                    <button
                      type="button"
                      className={`flex w-full items-center justify-between gap-3 px-4 py-3 text-left transition hover:bg-base-200/60 ${selectedId === item.id
                        ? "bg-base-200/80 font-semibold"
                        : ""
                        }`}
                      onClick={() => handleSelect(item.id)}
                    >
                      <div className="min-w-0">
                        <p className="truncate text-sm">
                          {t.translations.LATTICE_EXTRACTION_NUMBER}
                          {item.id}
                        </p>
                        <div className="mt-2 grid grid-cols-2 gap-x-2">
                          <div>
                            <p
                              className="text-base-content/40 uppercase tracking-wide"
                              style={{ fontSize: "0.625rem" }}
                            >
                              {t.translations.LATTICE_STATUS_HEADER}
                            </p>
                            <div className="mt-0.5 flex h-4 items-center">
                              <span
                                className={`badge badge-xs ${extractionStatusBadgeClass(item.status)}`}
                              >
                                {statusLabel(item.status, t.translations)}
                              </span>
                            </div>
                          </div>
                          {item.mode && (
                            <div>
                              <p
                                className="text-base-content/40 uppercase tracking-wide"
                                style={{ fontSize: "0.625rem" }}
                              >
                                {t.translations.LATTICE_MODE_HEADER}
                              </p>
                              <div className="mt-0.5 flex h-4 items-center">
                                <p className="text-xs font-medium text-base-content/70 capitalize">
                                  {item.mode}
                                </p>
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
                {t.translations.LATTICE_SELECT_EXTRACTION_PROMPT}
              </div>
            )}
          </div>
        </div>
      </section>
    </main>
  );
}
