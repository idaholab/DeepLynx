"use client";

import React, { useEffect, useMemo, useState } from "react";
import { useSearchParams } from "next/navigation";
import {
  CheckCircleIcon,
  ChevronDownIcon,
  ChevronUpIcon,
  ClipboardDocumentCheckIcon,
  XCircleIcon,
} from "@heroicons/react/24/outline";
import PropertyTable from "@/app/(home)/record/components/PropertyTable";
import {
  getLatticeMockRecordGroups,
  getLatticeRecordContext,
  getRecordReviewStatus,
  getRecordSuggestionCount,
  type LatticeDecision,
  type LatticeRecordGroup,
  type LatticeSuggestedRecordDraft,
} from "../latticeMockData";

type DetailTab = "record" | "classes" | "edges" | "relationships";

interface PropertyRow {
  label: string;
  value: React.ReactNode;
  isNested?: boolean;
  nestedRows?: PropertyRow[];
}

function parseNestedProperties(
  input: Record<string, unknown> | null | undefined,
): PropertyRow[] {
  if (!input) return [];

  return Object.entries(input).map(([key, value]) => {
    const label = key
      .split("_")
      .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
      .join(" ");

    const isNestedObject =
      value !== null && typeof value === "object" && !Array.isArray(value);

    if (isNestedObject) {
      return {
        label,
        value: "",
        isNested: true,
        nestedRows: parseNestedProperties(value as Record<string, unknown>),
      };
    }

    return {
      label,
      value: Array.isArray(value) ? JSON.stringify(value) : String(value),
    };
  });
}

function applyDecision(group: LatticeRecordGroup, decision: LatticeDecision) {
  return {
    ...group,
    reviewDecision: decision,
    suggestedClasses: group.suggestedClasses.map((item) => ({
      ...item,
      decision,
    })),
    suggestedEdges: group.suggestedEdges.map((item) => ({ ...item, decision })),
    suggestedRelationships: group.suggestedRelationships.map((item) => ({
      ...item,
      decision,
    })),
  };
}

function statusBadgeClass(group: LatticeRecordGroup) {
  const status = getRecordReviewStatus(group);

  if (status === "approved") return "badge-success";
  if (status === "denied") return "badge-error";
  return "badge-warning";
}

function statusLabel(group: LatticeRecordGroup) {
  const status = getRecordReviewStatus(group);

  if (status === "approved") return "Approved";
  if (status === "denied") return "Denied";
  return "Needs Review";
}

function statusSortRank(group: LatticeRecordGroup) {
  const status = getRecordReviewStatus(group);

  if (status === "needs_review") return 0;
  if (status === "denied") return 1;
  return 2;
}

export default function LatticeWorkspaceMockPage() {
  const searchParams = useSearchParams();
  const selectedRecord = useMemo(
    () => getLatticeRecordContext(searchParams),
    [searchParams],
  );
  const [groups, setGroups] = useState<LatticeRecordGroup[]>(() =>
    getLatticeMockRecordGroups(selectedRecord),
  );
  const [selectedGroupId, setSelectedGroupId] = useState<number>(
    selectedRecord.recordId,
  );
  const [activeTab, setActiveTab] = useState<DetailTab>("record");
  const [expandedSuggestedRecords, setExpandedSuggestedRecords] = useState<
    number[]
  >([]);

  useEffect(() => {
    const nextGroups = getLatticeMockRecordGroups(selectedRecord);
    setGroups(nextGroups);
    const sortedGroups = [...nextGroups].sort(
      (left, right) => statusSortRank(left) - statusSortRank(right),
    );
    setSelectedGroupId(sortedGroups[0]?.recordId ?? selectedRecord.recordId);
    setActiveTab("record");
    setExpandedSuggestedRecords([]);
  }, [selectedRecord]);

  useEffect(() => {
    setExpandedSuggestedRecords([]);
  }, [selectedGroupId]);

  const sortedGroups = useMemo(
    () =>
      [...groups].sort(
        (left, right) => statusSortRank(left) - statusSortRank(right),
      ),
    [groups],
  );

  const selectedGroup =
    groups.find((group) => group.recordId === selectedGroupId) ?? groups[0];
  const suggestedRecords = selectedGroup.suggestedRecords;

  const handleDecision = (decision: LatticeDecision) => {
    setGroups((previous) =>
      previous.map((group) =>
        group.recordId === selectedGroup.recordId
          ? applyDecision(group, decision)
          : group,
      ),
    );
  };

  const toggleSuggestedRecord = (index: number) => {
    setExpandedSuggestedRecords((previous) =>
      previous.includes(index)
        ? previous.filter((value) => value !== index)
        : [...previous, index],
    );
  };

  const buildSystemRows = (
    suggestedRecord: LatticeSuggestedRecordDraft,
  ): PropertyRow[] => [
    {
      label: "Record Name",
      value: suggestedRecord.name,
    },
    {
      label: "Record Description",
      value: suggestedRecord.description,
    },
    {
      label: "Uri",
      value: suggestedRecord.uri,
    },
    {
      label: "Original ID",
      value: suggestedRecord.originalId,
    },
    {
      label: "Data Source",
      value: suggestedRecord.dataSourceName,
    },
    {
      label: "Proposed Class",
      value: suggestedRecord.proposedClass,
    },
    {
      label: "Source Record",
      value: suggestedRecord.sourceRecordName,
    },
  ];

  return (
    <div className="mx-3 space-y-6 pb-8 sm:mx-4 lg:mx-0 p-6">
      <section className="rounded-3xl border border-info/20 bg-gradient-to-br from-base-100 via-base-100 to-warning/10 p-6 shadow-sm">
        <div className="max-w-3xl space-y-3">
          <span className="badge badge-warning badge-outline gap-2 px-3 py-3">
            <ClipboardDocumentCheckIcon className="size-4" />
            Lattice Mockup 3
          </span>
          <div>
            <h1 className="text-3xl font-semibold">
              Split-Pane Review Workspace
            </h1>
            <p className="mt-2 text-sm text-base-content/70">
              Queue on the left, selected record on the right. This version uses
              compact metadata, a single record-level summary, and tabbed detail
              sections including collapsible suggested record drafts.
            </p>
          </div>
        </div>
      </section>

      <section className="grid gap-6 xl:grid-cols-[0.95fr_1.45fr]">
        <aside className="rounded-3xl border border-base-300 bg-base-100 p-4 shadow-sm">
          <div className="border-b border-base-300 pb-4">
            <h2 className="text-lg font-semibold">Extraction Queue</h2>
            <p className="text-sm text-base-content/70">
              Select a record that needs review.
            </p>
          </div>

          <div className="mt-4 space-y-3">
            {sortedGroups.map((group) => (
              <button
                key={group.recordId}
                type="button"
                className={`w-full rounded-2xl border px-4 py-4 text-left transition ${
                  selectedGroup.recordId === group.recordId
                    ? "border-info bg-info/10"
                    : "border-base-300 bg-base-100 hover:bg-base-200/70"
                }`}
                onClick={() => setSelectedGroupId(group.recordId)}
              >
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <p className="font-semibold">{group.recordName}</p>
                  <span className={`badge ${statusBadgeClass(group)}`}>
                    {statusLabel(group)}
                  </span>
                </div>
                <div className="mt-2 flex flex-wrap gap-2 text-xs text-base-content/60">
                  <span className="rounded-full bg-base-200 px-3 py-1">
                    {group.recordClass}
                  </span>
                  <span className="rounded-full bg-base-200 px-3 py-1">
                    {getRecordSuggestionCount(group)} suggestions
                  </span>
                </div>
              </button>
            ))}
          </div>
        </aside>

        <section className="rounded-3xl border border-base-300 bg-base-100 p-5 shadow-sm">
          <div className="flex flex-col gap-4 border-b border-base-300 pb-5">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div>
                <div className="flex flex-wrap items-center gap-2">
                  <h2 className="text-2xl font-semibold">
                    {selectedGroup.recordName}
                  </h2>
                  <span className={`badge ${statusBadgeClass(selectedGroup)}`}>
                    {statusLabel(selectedGroup)}
                  </span>
                </div>
                <div className="mt-2 flex flex-wrap gap-2 text-xs text-base-content/60">
                  <span className="rounded-full bg-base-200 px-3 py-1">
                    {selectedGroup.recordClass}
                  </span>
                  <span className="rounded-full bg-base-200 px-3 py-1">
                    {selectedGroup.recordUri}
                  </span>
                  <span className="rounded-full bg-base-200 px-3 py-1">
                    Extracted {selectedGroup.extractedAt}
                  </span>
                </div>
              </div>

              <div className="flex flex-wrap gap-2">
                <button
                  type="button"
                  className="btn btn-success btn-sm"
                  onClick={() => handleDecision("approved")}
                >
                  <CheckCircleIcon className="size-4" />
                  Approve Record
                </button>
                <button
                  type="button"
                  className="btn btn-outline btn-error btn-sm"
                  onClick={() => handleDecision("denied")}
                >
                  <XCircleIcon className="size-4" />
                  Deny Record
                </button>
              </div>
            </div>

            <div className="rounded-2xl bg-base-200/60 p-4 text-sm text-base-content/75">
              {selectedGroup.summary}
            </div>
          </div>

          <div className="mt-5 flex flex-wrap gap-2">
            <button
              type="button"
              className={`btn btn-sm ${
                activeTab === "record" ? "btn-primary" : "btn-ghost"
              }`}
              onClick={() => setActiveTab("record")}
            >
              Record
            </button>
            <button
              type="button"
              className={`btn btn-sm ${
                activeTab === "classes" ? "btn-primary" : "btn-ghost"
              }`}
              onClick={() => setActiveTab("classes")}
            >
              Classes
            </button>
            <button
              type="button"
              className={`btn btn-sm ${
                activeTab === "edges" ? "btn-primary" : "btn-ghost"
              }`}
              onClick={() => setActiveTab("edges")}
            >
              Edges
            </button>
            <button
              type="button"
              className={`btn btn-sm ${
                activeTab === "relationships" ? "btn-primary" : "btn-ghost"
              }`}
              onClick={() => setActiveTab("relationships")}
            >
              Relationships
            </button>
          </div>

          {activeTab === "record" ? (
            <div className="mt-5 space-y-5">
              <div className="rounded-2xl border border-base-300 bg-info/5 p-4">
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <div>
                    <h3 className="text-lg font-semibold">
                      Suggested Records
                    </h3>
                    <p className="text-sm text-base-content/70">
                      Read-only draft records generated from the selected
                      extraction. Approving this extraction will create new nodes with these
                      properties and graph suggestions.
                    </p>
                  </div>
                  {suggestedRecords.length > 0 ? (
                    <span className="badge badge-outline">
                      {suggestedRecords.length} suggested{" "}
                      {suggestedRecords.length === 1 ? "record" : "records"}
                    </span>
                  ) : null}
                </div>
              </div>

              {suggestedRecords.length > 0 ? (
                <>
                  <div className="space-y-4">
                    {suggestedRecords.map((suggestedRecord, index) => {
                      const isExpanded = expandedSuggestedRecords.includes(index);
                      const suggestedRecordSystemRows = buildSystemRows(
                        suggestedRecord,
                      );
                      const suggestedRecordAdditionalRows = parseNestedProperties(
                        suggestedRecord.additionalProperties,
                      );

                      return (
                        <section
                          key={`${selectedGroup.recordId}-${suggestedRecord.originalId}-${index}`}
                          className="rounded-2xl border border-base-300 bg-base-100 shadow-sm"
                        >
                          <button
                            type="button"
                            className="flex w-full flex-wrap items-center justify-between gap-3 px-4 py-4 text-left"
                            onClick={() => toggleSuggestedRecord(index)}
                          >
                            <div>
                              <div className="flex flex-wrap items-center gap-2">
                                <p className="text-lg font-semibold">
                                  {suggestedRecord.name}
                                </p>
                                <span className="badge badge-outline">
                                  {suggestedRecord.proposedClass}
                                </span>
                              </div>
                              <div className="mt-2 flex flex-wrap gap-2 text-xs text-base-content/60">
                                <span className="rounded-full bg-base-200 px-3 py-1">
                                  Original ID {suggestedRecord.originalId}
                                </span>
                                <span className="rounded-full bg-base-200 px-3 py-1">
                                  {Object.keys(suggestedRecord.additionalProperties)
                                    .length}{" "}
                                  property groups
                                </span>
                              </div>
                            </div>

                            {isExpanded ? (
                              <ChevronUpIcon className="size-5 flex-shrink-0" />
                            ) : (
                              <ChevronDownIcon className="size-5 flex-shrink-0" />
                            )}
                          </button>

                          {isExpanded ? (
                            <div className="border-t border-base-300 px-4 py-5">
                              <PropertyTable
                                title="System Properties"
                                rows={suggestedRecordSystemRows}
                              />

                              <PropertyTable
                                title="Additional Properties"
                                rows={suggestedRecordAdditionalRows}
                              />
                            </div>
                          ) : null}
                        </section>
                      );
                    })}
                  </div>
                </>
              ) : (
                <div className="rounded-2xl border border-dashed border-base-300 bg-base-100 p-5 text-sm text-base-content/65">
                  This extraction did not suggest a new record. Review the
                  `Classes`, `Edges`, or `Relationships` tabs instead.
                </div>
              )}
            </div>
          ) : null}

          {activeTab === "classes" ? (
            <div className="mt-5 space-y-3">
              {selectedGroup.suggestedClasses.map((item) => (
                <div
                  key={item.id}
                  className="rounded-2xl border border-base-300 bg-base-200/50 p-4"
                >
                  <div className="flex items-center justify-between gap-2">
                    <p className="font-semibold">{item.name}</p>
                    <span className="badge badge-outline">
                      {Math.round(item.confidence * 100)}%
                    </span>
                  </div>
                  <p className="mt-2 text-sm text-base-content/70">
                    Evidence: {item.evidence}
                  </p>
                </div>
              ))}
              {selectedGroup.suggestedClasses.length === 0 ? (
                <div className="rounded-2xl border border-dashed border-base-300 bg-base-100 p-5 text-sm text-base-content/65">
                  No class suggestions for this extraction.
                </div>
              ) : null}
            </div>
          ) : null}

          {activeTab === "edges" ? (
            <div className="mt-5 space-y-3">
              {selectedGroup.suggestedEdges.map((item) => (
                <div
                  key={item.id}
                  className="rounded-2xl border border-base-300 bg-base-200/50 p-4"
                >
                  <div className="flex items-center justify-between gap-2">
                    <p className="font-semibold">
                      {item.from} - {item.label} - {item.to}
                    </p>
                    <span className="badge badge-outline">
                      {Math.round(item.confidence * 100)}%
                    </span>
                  </div>
                  <p className="mt-2 text-sm text-base-content/70">
                    Evidence: {item.evidence}
                  </p>
                </div>
              ))}
              {selectedGroup.suggestedEdges.length === 0 ? (
                <div className="rounded-2xl border border-dashed border-base-300 bg-base-100 p-5 text-sm text-base-content/65">
                  No edge suggestions for this extraction.
                </div>
              ) : null}
            </div>
          ) : null}

          {activeTab === "relationships" ? (
            <div className="mt-5 space-y-3">
              {selectedGroup.suggestedRelationships.map((item) => (
                <div
                  key={item.id}
                  className="rounded-2xl border border-base-300 bg-base-200/50 p-4"
                >
                  <div className="flex items-center justify-between gap-2">
                    <p className="font-semibold">
                      {item.subject} {item.predicate} {item.object}
                    </p>
                    <span className="badge badge-outline">
                      {Math.round(item.confidence * 100)}%
                    </span>
                  </div>
                  <p className="mt-2 text-sm text-base-content/70">
                    Evidence: {item.evidence}
                  </p>
                </div>
              ))}
              {selectedGroup.suggestedRelationships.length === 0 ? (
                <div className="rounded-2xl border border-dashed border-base-300 bg-base-100 p-5 text-sm text-base-content/65">
                  No relationship suggestions for this extraction.
                </div>
              ) : null}
            </div>
          ) : null}
        </section>
      </section>
    </div>
  );
}
