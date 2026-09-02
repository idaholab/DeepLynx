"use client";

import React, { useCallback, useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import {
  ArrowRightIcon,
  ArrowTopRightOnSquareIcon,
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
  rejectExtraction,
  triggerLatticeExtraction
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
import { useLocalPagination } from "@/app/hooks/useLocalPagination";
import PaginationControls from "../../components/PaginationControls";
import { getRecord } from "@/app/lib/client_service/record_services.client";
import { TriggerLatticeExtractionMode } from "../../types/requestDTOs";

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
  return Object.entries(obj)
    .filter(([key]) => key.toLowerCase() !== "tags")
    .map(([key, value]) => {
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
    record: StagedRecordDTO;
    isApproved: boolean;
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

              <span className="badge badge-outline">{t.translations.ID_LABEL}{record.id}</span>

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
            {record.promoted_id ? (
              <span className="badge badge-success badge-outline">
                {t.translations.LATTICE_APPROVED}
              </span>
            ) : record.rejected ? (
              <span className="badge badge-error badge-outline">
                {t.translations.LATTICE_REJECTED}
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
          {attrs.tags !== undefined && Array.isArray(attrs.tags) && (
            <div className="border-t border-base-300 px-4 py-5 mt-4">
              <h3 className="text-lg font-bold">{t.translations.TAGS}</h3>
              <div className="flex flex-wrap gap-2 mt-2">
                {attrs.tags.map((tag, index) => (
                  <span
                    key={index}
                    className="inline-block bg-blue-100 text-blue-800 text-xs font-medium px-2.5 py-0.5 rounded-full"
                  >
                    {tag}
                  </span>
                ))}
              </div>
            </div>
          )}
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

            <span className="badge badge-outline">{t.translations.ID_LABEL}{cls.id}</span>

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
              {t.translations.LATTICE_ALREADY_IN_PROJECT}
            </span>
          ) : cls.promoted_id ? (
            <span className="badge badge-success badge-outline">
              {t.translations.LATTICE_APPROVED}
            </span>
          ) : cls.rejected ? (
            <span className="badge badge-error badge-outline">
              {t.translations.LATTICE_REJECTED}
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


function EdgeCard({ edge, isApproved, isRejected, onToggle, locked }: {
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

            <span className="badge badge-outline">{t.translations.ID_LABEL}{edge.id}</span>

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
          {edge.promoted_id ? (
            <span className="badge badge-success badge-outline">
              {t.translations.LATTICE_APPROVED}
            </span>
          ) : edge.rejected ? (
            <span className="badge badge-error badge-outline">
              {t.translations.LATTICE_REJECTED}
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

function RelationshipCard({ rel, isApproved, isRejected, onToggle, locked }: {
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

            <span className="badge badge-outline">{t.translations.ID_LABEL}{rel.id}</span>

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
              {t.translations.LATTICE_ALREADY_IN_PROJECT}
            </span>
          ) : rel.promoted_id ? (
            <span className="badge badge-success badge-outline">
              {t.translations.LATTICE_APPROVED}
            </span>
          ) : rel.rejected ? (
            <span className="badge badge-error badge-outline">
              {t.translations.LATTICE_REJECTED}
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
  extractionName,
  organizationId,
  projectId,
  onStatusChange,
}: {
  extractionId: number;
  extractionName: string;
  organizationId: number;
  projectId: number;
  onStatusChange?: () => void;
}) {
  const [staging, setStaging] = useState<ExtractionStagingResponseDTO | null>(
    null,
  );
  const router = useRouter();
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

  const toggleApproveByStatus = (status: string) => {
    const statuses = status === "valid_novel_invalid" ? ["valid", "novel_discovery", "invalid_schema"] : [status];

    const recordIds = visibleRecords
      .filter((r) => statuses.includes(r.validation_status as string))
      .map((r) => r.id);
    const classIds = visibleClasses
      .filter((c) => !c.ontology_class_id && statuses.includes(c.validation_status as string))
      .map((c) => c.id);
    const edgeIds = visibleEdges
      .filter((e) => statuses.includes(e.validation_status as string))
      .map((e) => e.id);
    const relationshipIds = visibleRelationships
      .filter((rel) => !rel.ontology_relationship_id && statuses.includes(rel.validation_status as string))
      .map((rel) => rel.id);

    const allApproved =
      recordIds.every((id) => approved.records.has(id)) &&
      classIds.every((id) => approved.classes.has(id)) &&
      edgeIds.every((id) => approved.edges.has(id)) &&
      relationshipIds.every((id) => approved.relationships.has(id));

    if (allApproved) {
      setApproved((prev) => ({
        records: new Set([...prev.records].filter((id) => !recordIds.includes(id))),
        classes: new Set([...prev.classes].filter((id) => !classIds.includes(id))),
        edges: new Set([...prev.edges].filter((id) => !edgeIds.includes(id))),
        relationships: new Set([...prev.relationships].filter((id) => !relationshipIds.includes(id))),
      }));

    } else {
      setApproved((prev) => ({
        records: new Set([...prev.records, ...recordIds]),
        classes: new Set([...prev.classes, ...classIds]),
        edges: new Set([...prev.edges, ...edgeIds]),
        relationships: new Set([...prev.relationships, ...relationshipIds]),
      }));

      setRejected((prev) => ({
        records: new Set([...prev.records].filter((id) => !recordIds.includes(id))),
        classes: new Set([...prev.classes].filter((id) => !classIds.includes(id))),
        edges: new Set([...prev.edges].filter((id) => !edgeIds.includes(id))),
        relationships: new Set([...prev.relationships].filter((id) => !relationshipIds.includes(id))),
      }));
    }

  };

  const isAllApproved = (status: string): boolean => {
    const statuses = status === "valid_novel_invalid" ? ["valid", "novel_discovery", "invalid_schema"] : [status];

    const recordIds = visibleRecords
      .filter(r => statuses.includes(r.validation_status as string))
      .map(r => r.id);
    const classIds = visibleClasses
      .filter(c => !c.ontology_class_id && statuses.includes(c.validation_status as string))
      .map(c => c.id);
    const edgeIds = visibleEdges
      .filter(e => statuses.includes(e.validation_status as string))
      .map(e => e.id);
    const relationshipIds = visibleRelationships
      .filter(rel => !rel.ontology_relationship_id && statuses.includes(rel.validation_status as string))
      .map(rel => rel.id);


    const nonEmptyCategories = [recordIds, classIds, edgeIds, relationshipIds].filter(arr => arr.length > 0).length;

    if (nonEmptyCategories === 0) {
      return false;
    }

    const allRecordsApproved = recordIds.length === 0 || recordIds.every(id => {
      const has = approved.records.has(id);
      return has;
    });

    const allClassesApproved = classIds.length === 0 || classIds.every(id => {
      const has = approved.classes.has(id);
      return has;
    });

    const allEdgesApproved = edgeIds.length === 0 || edgeIds.every(id => {
      const has = approved.edges.has(id);
      return has;
    });

    const allRelationshipsApproved = relationshipIds.length === 0 || relationshipIds.every(id => {
      const has = approved.relationships.has(id);
      return has;
    });

    const result = allRecordsApproved && allClassesApproved && allEdgesApproved && allRelationshipsApproved;

    return result;
  };
  const requestIdRef = useRef(0);

  const fetchStaging = useCallback(async () => {
    const myRequestId = ++requestIdRef.current;

    try {
      const data = await getExtractionStaging(
        organizationId,
        projectId,
        extractionId,
      );

      if (requestIdRef.current !== myRequestId) return;

      setStaging(data);

      if (NOT_RUNNING_STATUSES.includes(data.status)) {
        onStatusChange?.();
      }
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
    onStatusChange,
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
    if (!staging || NOT_RUNNING_STATUSES.includes(staging.status)) {
      return;
    }

    let cancelled = false;

    const poll = async () => {
      await fetchStaging();

      if (!cancelled) {
        timeoutId = window.setTimeout(poll, 3000);
      }
    };

    let timeoutId = window.setTimeout(poll, 3000);

    return () => {
      cancelled = true;
      window.clearTimeout(timeoutId);
    };
  }, [staging?.status, fetchStaging]);

  const handleTriggerLatticeExtraction = useCallback(async (staging: ExtractionStagingResponseDTO) => {
    try {

      const extraction = await getExtractionStaging(organizationId, projectId, staging.id);

      const record = await getRecord(organizationId, projectId, extraction.record_id as number, true);

      const newExtractionId = await triggerLatticeExtraction(
        organizationId as number,
        projectId,
        extraction.record_id as number,
        {
          data_source_id: record.dataSourceId as number,
          mode: staging!.mode as TriggerLatticeExtractionMode,
        },
      );

      if (newExtractionId) {
        router.replace(`/lattice/decisions?extractionId=${newExtractionId}`);
      } else {
        router.replace(`/lattice/decisions?extractionId=${staging.id}`);
      }

      onStatusChange?.();
    } catch (error: any) {
      if (error?.response?.status === 400) {
        toast(t.translations.LATTICE_EMBEDDINGS_GENERATING, { icon: "⏳" });
      } else {
        console.error("Error triggering Lattice extraction:", error);
        toast.error(t.translations.LATTICE_FAILED_TO_START_ANALYSIS);
      }
    }
  }, [
    organizationId,
    projectId,
    onStatusChange,
    t.translations,
  ]);

  useEffect(() => {
    if (staging?.status === "failed") {
      const timeoutId = window.setTimeout(() => {
        fetchStaging();
      }, 3000);

      return () => {
        window.clearTimeout(timeoutId);
      };
    }
  }, [staging?.status, fetchStaging]);

  const handleSave = async () => {
    if (!staging) return;
    try {
      setIsPromoting(true);

      const filterApproved = <T extends { id: number; rejected?: boolean; promoted_id: number | null }>(
        items: T[],
        approvedSet: Set<number>
      ) => {
        return [...approvedSet].filter(id => {
          const item = items.find(i => i.id === id);
          return item && !item.rejected && !item.promoted_id;
        });
      };

      const filterRejected = <T extends { id: number; rejected?: boolean; promoted_id: number | null }>(
        items: T[],
        rejectedSet: Set<number>
      ) => {
        return [...rejectedSet].filter(id => {
          const item = items.find(i => i.id === id);
          return item && !item.promoted_id && !item.rejected;
        });
      };

      const filteredApproved = {
        record_ids: filterApproved(staging.records, approved.records),
        class_ids: filterApproved(staging.classes, approved.classes),
        edge_ids: filterApproved(staging.edges, approved.edges),
        relationship_ids: filterApproved(staging.relationships, approved.relationships),
      };

      const filteredRejected = {
        record_ids: filterRejected(staging.records, rejected.records),
        class_ids: filterRejected(staging.classes, rejected.classes),
        edge_ids: filterRejected(staging.edges, rejected.edges),
        relationship_ids: filterRejected(staging.relationships, rejected.relationships),
      };

      const hasApprovals = Object.values(filteredApproved).some(arr => arr.length > 0);
      const hasRejections = Object.values(filteredRejected).some(arr => arr.length > 0);

      if (hasApprovals) {
        await promoteExtraction(organizationId, projectId, extractionId, filteredApproved);
        await fetchStaging();
      }
      if (hasRejections) {
        await rejectExtraction(organizationId, projectId, extractionId, {
          ...filteredRejected,
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

  const visibleRecords = staging.records;
  const visibleClasses = staging.classes;
  const visibleEdges = staging.edges;
  const visibleRelationships = staging.relationships;

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
  const invalidSchemaCount = countByStatus("invalid_schema");

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
          {extractionName ? ` -  ${extractionName}` : ""}
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
            {isRunning ? (
              <span>{t.translations.LATTICE_EXTRACTION_RUNNING}</span>
            ) : canDecide ? (
              <span>{t.translations.LATTICE_EXTRACTION_REVIEW}</span>
            ) : staging.status === "failed" ? (
              <>
                <p className="text-sm text-red-600 font-bold">
                  {t.translations.LATTICE_EXTRACTION_FAILED_MSG}
                </p>
                <div className="mt-4 p-3 bg-red-100 text-red-600 rounded-lg">
                  <p className="font-bold">{t.translations.ERROR_DETAILS}</p>
                  <p>{staging.failure_message}</p>
                </div>
                {/* Retry Extraction Button */}
                {staging.record_id && (
                  <button
                    className="btn btn-error btn-sm mt-4"
                    onClick={() => handleTriggerLatticeExtraction(staging)}
                  >
                    {t.translations.RETRY} {t.translations.LATTICE_EXTRACTION_NUMBER}{staging.id}
                  </button>
                )}
              </>
            ) : staging.status === "rejected" ? (
              <span>{t.translations.LATTICE_EXTRACTION_REJECTED_MSG}</span>
            ) : (
              <>
                <span>{t.translations.LATTICE_EXTRACTION_BEEN}</span>{" "}
                <span>{statusLabel(staging.status, t.translations)}.</span>
                <ArrowTopRightOnSquareIcon className="size-4" />
              </>
            )}

          </p>
          {canDecide && (
            <div className="flex flex-wrap gap-2">
              <button
                type="button"
                className={`btn btn-outline btn-success btn-sm ${isAllApproved("valid") ? "bg-green-500 text-white border-green-600" : ""}`}
                onClick={() => toggleApproveByStatus("valid")}
                disabled={isPromoting || validCount === 0}
              >
                <CheckCircleIcon className="size-4" />
                {t.translations.OAUTH_DEVICE_APPROVE} {t.translations.VALID_LABEL} ({validCount})
              </button>

              <button
                type="button"
                className={`btn btn-outline btn-warning btn-sm ${isAllApproved("novel_discovery") ? "bg-yellow-400 text-white border-yellow-500" : ""}`}
                onClick={() => toggleApproveByStatus("novel_discovery")}
                disabled={isPromoting || novelDiscoveryCount === 0}
              >
                <CheckCircleIcon className="size-4" />
                {t.translations.OAUTH_DEVICE_APPROVE} {t.translations.LATTICE_NOVEL_DISCOVERY_LABEL} ({novelDiscoveryCount})
              </button>


              <button
                type="button"
                className={`btn btn-outline btn-error btn-sm ${isAllApproved("invalid_schema") ? "bg-red-400 text-white border-red-400" : ""}`}
                onClick={() => toggleApproveByStatus("invalid_schema")}
                disabled={isPromoting || invalidSchemaCount === 0}
              >
                <CheckCircleIcon className="size-4" />
                {t.translations.OAUTH_DEVICE_APPROVE} {t.translations.LATTICE_INVALID_SCHEMA_ITEMS} ({invalidSchemaCount})
              </button>

              <button
                type="button"
                className={`btn btn-outline btn-primary btn-sm ${isAllApproved("valid_novel_invalid") ? "bg-blue-600 text-white border-blue-700" : ""}`}
                onClick={() => toggleApproveByStatus("valid_novel_invalid")}
                disabled={isPromoting || (validCount + novelDiscoveryCount + invalidSchemaCount === 0)}
              >
                <CheckCircleIcon className="size-4" />
                {t.translations.LATTICE_APPROVE_ALL} ({validCount + novelDiscoveryCount + invalidSchemaCount})
              </button>

              <button
                type="button"
                className="btn btn-primary btn-sm"
                onClick={handleSave}
                disabled={isPromoting || !hasPendingDecisions}
              >
                {isPromoting ? <span className="loading loading-spinner loading-xs" /> : null}
                {t.translations.SAVE}
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
                <RecordCard
                  key={record.id}
                  record={record}
                  isApproved={approved.records.has(record.id)}
                  isRejected={rejected.records.has(record.id)}
                  locked={!!record.promoted_id || record.rejected}
                  onToggle={(action) => toggleItem("records", record.id, action)}
                />
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
  const { t } = useLanguage();
  const { organization } = useOrganizationSession();
  const { project } = useProjectSession();

  const [selectedId, setSelectedId] = useState<number | null>(null);

  const [items, setItems] = useState<ExtractionListItemDTO[]>([]);
  const [isListLoading, setIsListLoading] = useState(true);
  const [listError, setListError] = useState<string | null>(null);
  const [names, setNames] = useState<Record<string, string>>({});

  const orgId = organization?.organizationId as number | undefined;
  const projId = project?.projectId as number | undefined;
  const insightHidden = isInsightHidden();

  const refreshList = useCallback(() => {
    if (!orgId || !projId) return;
    listExtractions(orgId, projId)
      .then((response) => {
        if (response?.items) {
          setItems(response.items);


          const latestExtraction = response.items.reduce((prev, current) =>
            current.id > prev.id ? current : prev,
            response.items[0]);

          if (latestExtraction && !selectedId) {
            router.replace(`/lattice/decisions?extractionId=${latestExtraction.id}`, { scroll: false });
          }
        } else {
          setItems([]);
        }
      })
      .catch(() =>
        setListError(t.translations.LATTICE_FAILED_LOAD_EXTRACTIONS),
      );
  }, [orgId, projId, selectedId, t.translations.LATTICE_FAILED_LOAD_EXTRACTIONS, router]);

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
      .then((response) => {
        if (response?.items) {
          setItems(response.items);
        } else {
          setItems([]);
        }
      })
      .catch(() => {
        setListError(t.translations.LATTICE_FAILED_LOAD_EXTRACTIONS);
      })
      .finally(() => {
        setIsListLoading(false);
      });
  }, [
    insightHidden,
    orgId,
    projId,
    t.translations.LATTICE_FAILED_LOAD_EXTRACTIONS,
  ]);

  // Restore last selected extraction when arriving without a query param
  useEffect(() => {
    if (!selectedId && projId) {
      const saved = localStorage.getItem(storageKey(projId));
      if (saved) setSelectedId(Number(saved));
    }
  }, [projId, selectedId]);

  const handleSelect = (id: number) => {
    setSelectedId(id);
    if (projId) localStorage.setItem(storageKey(projId), String(id));
  };

  if (insightHidden) {
    return null;
  }

  const {
    currentPage: extractionPage,
    pageSize: extractionPageSize,
    paginatedItems: paginatedExtractions,
    resetPagination: resetExtractionPagination,
    setCurrentPage: setExtractionPage,
    setPageSize: setExtractionPageSize,
    totalPages: extractionTotalPages,
  } = useLocalPagination({
    items: items,
    initialPageSize: 5,
  });

  useEffect(() => {
    resetExtractionPagination();
  }, [resetExtractionPagination]);

  useEffect(() => {
    let cancelled = false;
    async function loadNames() {
      const entries = await Promise.all(
        paginatedExtractions.map(async (item) => {
          let res = null;
          if (item.source_record_id == null) return null;
          res = await getRecord(Number(orgId), Number(projId), item.source_record_id);
          return [item.id, res.name] as const;
        })
      );
      const validEntries = entries.filter(
        (entry): entry is readonly [number, string] => entry !== null
      );
      if (!cancelled) {
        setNames(Object.fromEntries(validEntries));
      }
    }
    loadNames();
    return () => { cancelled = true };
  }, [paginatedExtractions, orgId, projId])

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
        <div className="grid gap-6 lg:grid-cols-[410px_1fr]">
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
                {paginatedExtractions.map((item) => (
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
                        <p className="truncate text-sm">{names[item.id] ? names[item.id] : ""}</p>
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

                <PaginationControls
                  currentPage={extractionPage}
                  pageSize={extractionPageSize}
                  totalPages={extractionTotalPages}
                  onPageChange={setExtractionPage}
                  onPageSizeChange={setExtractionPageSize}
                />
              </ul>
            )}
          </aside>

          {/* Right: detail panel */}
          <div>
            {selectedId && orgId && projId ? (
              <ExtractionDetailPanel
                key={selectedId}
                extractionId={selectedId}
                extractionName={names[selectedId]}
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
