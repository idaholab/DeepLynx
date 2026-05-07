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
import { useLanguage } from "@/app/contexts/Language";

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

function statusLabel(status: string, translations: { LATTICE_APPROVED_STATUS: string }) {
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
      value: typeof value === "object" ? JSON.stringify(value) : String(value ?? ""),
    };
  });
}

function RecordCard({ record }: { record: StagedRecordDTO }) {
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
              {t.translations.LATTICE_SCORE}: {(record.ensemble_score * 100).toFixed(0)}%
            </span>
            <span className="rounded-full bg-base-200 px-3 py-1">
              {t.translations.LATTICE_FREQUENCY}: {record.frequency}
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
          <PropertyTable title={t.translations.LATTICE_PROPERTIES_TITLE} rows={parseNestedRows(attrs)} />
        </div>
      ) : null}
    </section>
  );
}

function ClassCard({ cls }: { cls: StagedClassDTO }) {
  const { t } = useLanguage();
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
          {t.translations.LATTICE_EXISTING_CLASS_ID} {cls.ontology_class_id}
        </p>
      )}
    </div>
  );
}

function EdgeCard({ edge }: { edge: StagedEdgeDTO }) {
  const { t } = useLanguage();
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
          {t.translations.LATTICE_SCORE}: {(edge.ensemble_score * 100).toFixed(0)}%
        </span>
        <span className="rounded-full bg-base-200 px-3 py-1">
          {t.translations.LATTICE_FREQUENCY}: {edge.frequency}
        </span>
      </div>
    </div>
  );
}

function RelationshipCard({ rel }: { rel: StagedRelationshipDTO }) {
  const { t } = useLanguage();
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
            <p className="text-base-content/40 uppercase tracking-wide" style={{ fontSize: "0.625rem" }}>{t.translations.LATTICE_ORIGIN}</p>
            <p className="mt-0.5 font-medium text-base-content/70">{rel.origin_class_name ?? "?"}</p>
          </div>
          <span className="mt-3 text-base-content/30">→</span>
          <div>
            <p className="text-base-content/40 uppercase tracking-wide" style={{ fontSize: "0.625rem" }}>{t.translations.LATTICE_DESTINATION}</p>
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
  const { t } = useLanguage();

  const fetchStaging = useCallback(async () => {
    try {
      const data = await getExtractionStaging(organizationId, projectId, extractionId);
      setStaging(data);
      setError(null);

      if (!TERMINAL_STATUSES.includes(data.status)) {
        pollingRef.current = setTimeout(fetchStaging, POLLING_INTERVAL_MS);
      }
    } catch {
      setError(t.translations.LATTICE_FAILED_LOAD_EXTRACTION);
    } finally {
      setIsLoading(false);
    }
  }, [organizationId, projectId, extractionId, t.translations.LATTICE_FAILED_LOAD_EXTRACTION]);

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
      toast.success(approve ? t.translations.LATTICE_EXTRACTION_APPROVED_TOAST : t.translations.LATTICE_EXTRACTION_REJECTED_TOAST);
      await fetchStaging();
      onStatusChange?.();
    } catch {
      toast.error(t.translations.LATTICE_PROCESS_FAILED);
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
        <p className="text-error">{error ?? t.translations.LATTICE_NO_EXTRACTION_DATA}</p>
      </div>
    );
  }

  const isRunning = !TERMINAL_STATUSES.includes(staging.status);
  const canDecide = staging.status === "complete";
  const tabs: DetailTab[] = ["records", "classes", "edges", "relationships"];

  const tabLabels: Record<DetailTab, string> = {
    records: t.translations.RECORDS,
    classes: t.translations.CLASSES,
    edges: t.translations.LATTICE_EDGES,
    relationships: t.translations.RELATIONSHIPS,
  };

  return (
    <div className="flex flex-col gap-4">
      {/* Header: title + status + approve/reject */}
      <div className="flex flex-col gap-2">
        <h2 className="text-lg font-bold">{t.translations.LATTICE_EXTRACTION_NUMBER}{staging.id}</h2>

        {/* Row 1: status + mode on left, buttons on right */}
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div className="flex flex-wrap items-center gap-2">
            <span className={`badge ${extractionStatusBadgeClass(staging.status)}`}>
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
                {t.translations.LATTICE_APPROVE_ALL}
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
                {t.translations.LATTICE_REJECT_ALL}
              </button>
            </div>
          )}
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
            <p className="text-xs text-base-content/50 text-right max-w-[220px]">
              {t.translations.LATTICE_APPROVE_NOTE_PREFIX}{" "}
              <span className="font-medium text-error/70">{t.translations.LATTICE_INVALID_SCHEMA_ITEMS}</span>{" "}
              {t.translations.LATTICE_ITEMS_SUFFIX}
            </p>
          )}
        </div>
      </div>

      {/* Summary card */}
      <div className="rounded-2xl border border-base-300 bg-base-100 p-4 shadow-sm">
        <h3 className="mb-3 text-sm font-semibold text-base-content/70">{t.translations.LATTICE_SUMMARY}</h3>
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
          {(
            [
              { label: t.translations.RECORDS, count: staging.records.length },
              { label: t.translations.CLASSES, count: staging.classes.length },
              { label: t.translations.RELATIONSHIPS, count: staging.relationships.length },
              { label: t.translations.LATTICE_EDGES, count: staging.edges.length },
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
              {tabLabels[tab]}
            </button>
          ))}
        </div>

        <div className="mt-4 space-y-3">
          {activeTab === "records" &&
            (staging.records.length === 0 ? (
              <EmptyState message={t.translations.LATTICE_NO_RECORDS_STAGED} />
            ) : (
              staging.records.map((record) => <RecordCard key={record.id} record={record} />)
            ))}

          {activeTab === "classes" &&
            (staging.classes.length === 0 ? (
              <EmptyState message={t.translations.LATTICE_NO_CLASSES_STAGED} />
            ) : (
              staging.classes.map((cls) => <ClassCard key={cls.id} cls={cls} />)
            ))}

          {activeTab === "edges" &&
            (staging.edges.length === 0 ? (
              <EmptyState message={t.translations.LATTICE_NO_EDGES_STAGED} />
            ) : (
              staging.edges.map((edge) => <EdgeCard key={edge.id} edge={edge} />)
            ))}

          {activeTab === "relationships" &&
            (staging.relationships.length === 0 ? (
              <EmptyState message={t.translations.LATTICE_NO_RELATIONSHIPS_STAGED} />
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

  const refreshList = useCallback(() => {
    if (!orgId || !projId) return;
    listExtractions(orgId, projId)
      .then(setItems)
      .catch(() => setListError(t.translations.LATTICE_FAILED_LOAD_EXTRACTIONS));
  }, [orgId, projId, t.translations.LATTICE_FAILED_LOAD_EXTRACTIONS]);

  useEffect(() => {
    if (!orgId || !projId) return;
    setIsListLoading(true);
    listExtractions(orgId, projId)
      .then(setItems)
      .catch(() => setListError(t.translations.LATTICE_FAILED_LOAD_EXTRACTIONS))
      .finally(() => setIsListLoading(false));
  }, [orgId, projId, t.translations.LATTICE_FAILED_LOAD_EXTRACTIONS]);

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
        <h1 className="text-xl sm:text-2xl font-bold text-base-content">{t.translations.LATTICE_PAGE_TITLE}</h1>
        <p className="mt-1 text-sm text-base-content/70">
          {t.translations.LATTICE_PAGE_DESCRIPTION_INTRO}{" "}
          <span className="font-medium">{t.translations.LATTICE_VALID_LABEL}</span>{" "}
          {t.translations.LATTICE_VALID_DESCRIPTION}{" "}
          <span className="font-medium">{t.translations.LATTICE_NOVEL_DISCOVERY_LABEL}</span>{" "}
          {t.translations.LATTICE_NOVEL_DISCOVERY_DESCRIPTION}{" "}
          <span className="font-medium">{t.translations.LATTICE_INVALID_SCHEMA_LABEL}</span>{" "}
          {t.translations.LATTICE_INVALID_SCHEMA_DESCRIPTION}{" "}
          {t.translations.LATTICE_APPROVE_PROMOTES_ALL}
        </p>
        <div className="mt-2">
          <span className="badge badge-warning badge-sm">{t.translations.LATTICE_COMING_SOON}</span>
          <span className="ml-2 text-sm text-base-content/70">{t.translations.LATTICE_COMING_SOON_TEXT}</span>
        </div>
      </div>

      <div className="px-3 sm:px-6 lg:px-12 py-6">
        <div className="grid gap-6 lg:grid-cols-[320px_1fr]">
          {/* Left: extraction list */}
          <aside className="rounded-2xl border border-base-300 bg-base-100 shadow-sm overflow-hidden self-start">
            <div className="border-b border-base-300 px-4 py-3">
              <h2 className="text-sm font-semibold text-base-content/70">{t.translations.LATTICE_EXTRACTIONS_PANEL_TITLE}</h2>
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
                      className={`flex w-full items-center justify-between gap-3 px-4 py-3 text-left transition hover:bg-base-200/60 ${selectedId === item.id ? "bg-base-200/80 font-semibold" : ""
                        }`}
                      onClick={() => handleSelect(item.id)}
                    >
                      <div className="min-w-0">
                        <p className="truncate text-sm">{t.translations.LATTICE_EXTRACTION_NUMBER}{item.id}</p>
                        <div className="mt-2 grid grid-cols-2 gap-x-2">
                          <div>
                            <p className="text-base-content/40 uppercase tracking-wide" style={{ fontSize: "0.625rem" }}>{t.translations.LATTICE_STATUS_HEADER}</p>
                            <div className="mt-0.5 flex h-4 items-center">
                              <span className={`badge badge-xs ${extractionStatusBadgeClass(item.status)}`}>
                                {statusLabel(item.status, t.translations)}
                              </span>
                            </div>
                          </div>
                          {item.mode && (
                            <div>
                              <p className="text-base-content/40 uppercase tracking-wide" style={{ fontSize: "0.625rem" }}>{t.translations.LATTICE_MODE_HEADER}</p>
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
                {t.translations.LATTICE_SELECT_EXTRACTION_PROMPT}
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
