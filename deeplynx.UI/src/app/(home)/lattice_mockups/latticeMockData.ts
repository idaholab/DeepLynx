export type LatticeDecision = "pending" | "approved" | "denied";

export interface LatticeRecordContext {
  projectId: number;
  recordId: number;
  recordName: string;
  recordUri: string;
  recordClass: string;
}

export interface LatticeClassSuggestion {
  id: string;
  name: string;
  confidence: number;
  rationale: string;
  evidence: string;
  decision: LatticeDecision;
}

export interface LatticeEdgeSuggestion {
  id: string;
  from: string;
  to: string;
  label: string;
  confidence: number;
  rationale: string;
  evidence: string;
  decision: LatticeDecision;
}

export interface LatticeRelationshipSuggestion {
  id: string;
  subject: string;
  predicate: string;
  object: string;
  confidence: number;
  rationale: string;
  evidence: string;
  decision: LatticeDecision;
}

export interface LatticeNeighborRecord {
  id: number;
  name: string;
  className: string;
  relationship: string;
  confidence: number;
  status: "existing" | "suggested";
}

export interface LatticeRecordGroup {
  recordId: number;
  projectId: number;
  recordName: string;
  recordUri: string;
  recordClass: string;
  extractedAt: string;
  llmModel: string;
  summary: string;
  reviewDecision: LatticeDecision;
  suggestedRecords: LatticeSuggestedRecordDraft[];
  suggestedClasses: LatticeClassSuggestion[];
  suggestedEdges: LatticeEdgeSuggestion[];
  suggestedRelationships: LatticeRelationshipSuggestion[];
  connectedRecords: LatticeNeighborRecord[];
}

export interface LatticeSuggestedRecordDraft {
  name: string;
  description: string;
  uri: string;
  originalId: string;
  dataSourceName: string;
  proposedClass: string;
  sourceRecordName: string;
  sourceRecordUri: string;
  additionalProperties: Record<string, unknown>;
}

export interface FlattenedSuggestion {
  id: string;
  type: "record" | "class" | "edge" | "relationship";
  title: string;
  confidence: number;
  rationale: string;
  evidence: string;
  decision: LatticeDecision;
  recordDraft?: LatticeSuggestedRecordDraft;
}

type SearchParamReader = {
  get: (key: string) => string | null;
};

const DEFAULT_CONTEXT: LatticeRecordContext = {
  projectId: 14,
  recordId: 1842,
  recordName: "Pump Skid P-204 General Arrangement.pdf",
  recordUri: "s3://nexus/project-14/pump-skid-p204-ga.pdf",
  recordClass: "Engineering Drawing",
};

function toNumber(value: string | null, fallback: number) {
  if (!value) return fallback;

  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : fallback;
}

function toText(value: string | null, fallback: string) {
  return value && value.trim().length > 0 ? value : fallback;
}

export function getLatticeRecordContext(
  searchParams: SearchParamReader,
): LatticeRecordContext {
  return {
    projectId: toNumber(searchParams.get("projectId"), DEFAULT_CONTEXT.projectId),
    recordId: toNumber(searchParams.get("recordId"), DEFAULT_CONTEXT.recordId),
    recordName: toText(searchParams.get("recordName"), DEFAULT_CONTEXT.recordName),
    recordUri: toText(searchParams.get("recordUri"), DEFAULT_CONTEXT.recordUri),
    recordClass: toText(
      searchParams.get("recordClass"),
      DEFAULT_CONTEXT.recordClass,
    ),
  };
}

function buildPrimaryRecord(context: LatticeRecordContext): LatticeRecordGroup {
  const assetName = context.recordName.replace(/\.[^.]+$/, "");

  return {
    recordId: context.recordId,
    projectId: context.projectId,
    recordName: context.recordName,
    recordUri: context.recordUri,
    recordClass: context.recordClass,
    extractedAt: "2026-04-14 09:18",
    llmModel: "gpt-5.4",
    reviewDecision: "pending",
    summary:
      "The extraction run found one dominant asset, two neighboring systems, and enough evidence to create relationship suggestions without auto-committing them.",
    suggestedRecords: [
      {
        name: "Pump Skid P-204",
        description:
          "Generated metadata record for the pump skid extracted from the general arrangement drawing.",
        uri: "https://example.com/assets/pump-skid-p-204",
        originalId: "asset-p204",
        dataSourceName: "Default Data Source",
        proposedClass: "Pump Assembly",
        sourceRecordName: context.recordName,
        sourceRecordUri: context.recordUri,
        additionalProperties: {
          asset_tag: "P-204",
          assembly_type: "Pump Skid",
          service: "Cooling Water Circulation",
          design_conditions: {
            suction_pressure_psig: 42,
            discharge_pressure_psig: 88,
            design_flow_gpm: 950,
          },
          equipment_details: {
            motor_voltage: 480,
            frame_material: "Carbon Steel",
            mounting: "Skid Mounted",
          },
          connected_systems: {
            primary_loop: "Cooling Loop CL-7",
            electrical_source: "MCC-2",
          },
        },
      },
    ],
    suggestedClasses: [
      {
        id: `class-${context.recordId}-1`,
        name: "Pump Assembly",
        confidence: 0.94,
        rationale:
          "The document title, callouts, and BOM language all center on a pump skid assembly.",
        evidence: `${assetName} references suction piping, discharge piping, skid framing, and motor mounting details.`,
        decision: "pending",
      },
      {
        id: `class-${context.recordId}-2`,
        name: "Reference Document",
        confidence: 0.76,
        rationale:
          "The record also behaves like a design reference consumed by later inspection and maintenance records.",
        evidence:
          "Revision blocks and cross-sheet references suggest downstream records depend on this source.",
        decision: "pending",
      },
    ],
    suggestedEdges: [
      {
        id: `edge-${context.recordId}-1`,
        from: context.recordName,
        to: "Cooling Loop CL-7",
        label: "feeds",
        confidence: 0.88,
        rationale:
          "The extracted line annotations point from the skid discharge to CL-7.",
        evidence:
          "Discharge nozzle tags align with the cooling loop identifier on the drawing.",
        decision: "pending",
      },
      {
        id: `edge-${context.recordId}-2`,
        from: context.recordName,
        to: "Motor Control Center MCC-2",
        label: "powered_by",
        confidence: 0.81,
        rationale:
          "The control note block references the MCC panel as the electrical source.",
        evidence:
          "Starter schedule and equipment notes both mention MCC-2.",
        decision: "pending",
      },
    ],
    suggestedRelationships: [
      {
        id: `relationship-${context.recordId}-1`,
        subject: "Pump Skid P-204",
        predicate: "documents",
        object: context.recordName,
        confidence: 0.96,
        rationale:
          "This record appears to be the primary design artifact for the physical pump skid asset.",
        evidence:
          "The title block, revision metadata, and component callouts all point to P-204.",
        decision: "pending",
      },
      {
        id: `relationship-${context.recordId}-2`,
        subject: "Inspection Report IR-77",
        predicate: "references",
        object: context.recordName,
        confidence: 0.72,
        rationale:
          "The extraction inferred that the inspection report cites this drawing revision.",
        evidence:
          "The notes section includes a revision marker matching the source record.",
        decision: "pending",
      },
    ],
    connectedRecords: [
      {
        id: 2104,
        name: "Cooling Loop CL-7 P&ID Rev C.pdf",
        className: "P&ID",
        relationship: "feeds",
        confidence: 0.88,
        status: "suggested",
      },
      {
        id: 2118,
        name: "Motor Control Center MCC-2 Wiring Schedule.xlsx",
        className: "Electrical Schedule",
        relationship: "powered_by",
        confidence: 0.81,
        status: "suggested",
      },
      {
        id: 2087,
        name: "Inspection Report IR-77.pdf",
        className: "Inspection Record",
        relationship: "references",
        confidence: 0.72,
        status: "existing",
      },
    ],
  };
}

export function getLatticeMockRecordGroups(
  context: LatticeRecordContext,
): LatticeRecordGroup[] {
  return [
    buildPrimaryRecord(context),
    {
      recordId: 2087,
      projectId: context.projectId,
      recordName: "Inspection Report IR-77.pdf",
      recordUri: "s3://nexus/project-14/inspection-report-ir-77.pdf",
      recordClass: "Inspection Record",
      extractedAt: "2026-04-14 09:11",
      llmModel: "gpt-5.4",
      reviewDecision: "pending",
      summary:
        "This extraction only produced one relationship suggestion. No new record draft, class, or edge looked strong enough to propose.",
      suggestedRecords: [],
      suggestedClasses: [],
      suggestedEdges: [],
      suggestedRelationships: [
        {
          id: "relationship-2087-1",
          subject: "Inspection Report IR-77",
          predicate: "verifies_condition_of",
          object: "Pump Skid P-204",
          confidence: 0.84,
          rationale:
            "Observed findings map cleanly to the pump skid asset and not just the source document.",
          evidence:
            "The findings describe seal leakage and vibration values tied to the P-204 tag.",
          decision: "pending",
        },
      ],
      connectedRecords: [],
    },
    {
      recordId: 2104,
      projectId: context.projectId,
      recordName: "Cooling Loop CL-7 P&ID Rev C.pdf",
      recordUri: "s3://nexus/project-14/cooling-loop-cl7-pid-rev-c.pdf",
      recordClass: "P&ID",
      extractedAt: "2026-04-14 09:04",
      llmModel: "gpt-5.4",
      reviewDecision: "pending",
      summary:
        "This extraction is more metadata-oriented. It suggests a new record draft and one class, but no edges or relationships.",
      suggestedRecords: [
        {
          name: "Cooling Loop CL-7",
          description:
            "Generated system metadata record extracted from the P&ID source.",
          uri: "https://example.com/systems/cl-7",
          originalId: "system-cl-7",
          dataSourceName: "Default Data Source",
          proposedClass: "Process Diagram",
          sourceRecordName: "Cooling Loop CL-7 P&ID Rev C.pdf",
          sourceRecordUri:
            "s3://nexus/project-14/cooling-loop-cl7-pid-rev-c.pdf",
          additionalProperties: {
            loop_identifier: "CL-7",
            system_type: "Cooling Loop",
            operating_window: {
              normal_temperature_f: 112,
              max_temperature_f: 135,
            },
            loop_components: {
              pump_skid: "P-204",
              control_center: "MCC-2",
            },
          },
        },
      ],
      suggestedClasses: [
        {
          id: "class-2104-1",
          name: "Process Diagram",
          confidence: 0.93,
          rationale:
            "Symbols, line identifiers, and instrumentation references match a process diagram.",
          evidence:
            "The sheet is dominated by tagged process lines and instrument loops.",
          decision: "pending",
        },
      ],
      suggestedEdges: [],
      suggestedRelationships: [],
      connectedRecords: [],
    },
    {
      recordId: 2118,
      projectId: context.projectId,
      recordName: "Motor Control Center MCC-2 Wiring Schedule.xlsx",
      recordUri: "s3://nexus/project-14/mcc-2-wiring-schedule.xlsx",
      recordClass: "Electrical Schedule",
      extractedAt: "2026-04-14 08:59",
      llmModel: "gpt-5.4",
      reviewDecision: "pending",
      summary:
        "This extraction only found one edge. It did not suggest a class, relationship, or new record draft.",
      suggestedRecords: [],
      suggestedClasses: [],
      suggestedEdges: [
        {
          id: "edge-2118-1",
          from: "Motor Control Center MCC-2",
          to: "Pump Skid P-204",
          label: "powers",
          confidence: 0.79,
          rationale:
            "The schedule strongly links MCC-2 to the pump skid motor starter.",
          evidence:
            "Starter references and equipment IDs align to the P-204 equipment tag.",
          decision: "pending",
        },
      ],
      suggestedRelationships: [],
      connectedRecords: [],
    },
    {
      recordId: 2156,
      projectId: context.projectId,
      recordName: "Vendor Data Sheet VDS-204.pdf",
      recordUri: "s3://nexus/project-14/vendor-data-sheet-vds-204.pdf",
      recordClass: "Vendor Document",
      extractedAt: "2026-04-14 08:51",
      llmModel: "gpt-5.4",
      reviewDecision: "pending",
      summary:
        "This extraction produced multiple new record drafts. No class, edge, or relationship cleared the threshold.",
      suggestedRecords: [
        {
          name: "Pump Motor Specification P-204",
          description:
            "Generated equipment metadata record based on the vendor data sheet.",
          uri: "https://example.com/vendor/p204-motor-spec",
          originalId: "vds-p204-motor",
          dataSourceName: "Default Data Source",
          proposedClass: "Equipment Specification",
          sourceRecordName: "Vendor Data Sheet VDS-204.pdf",
          sourceRecordUri:
            "s3://nexus/project-14/vendor-data-sheet-vds-204.pdf",
          additionalProperties: {
            manufacturer: "North Ridge Pumps",
            model_number: "NRP-480-204",
            nameplate: {
              horsepower: 125,
              voltage: 480,
              enclosure: "TEFC",
            },
            maintenance_window: {
              inspection_interval_days: 180,
              lubrication_type: "EP2 Grease",
            },
          },
        },
        {
          name: "Pump Seal Kit P-204",
          description:
            "Generated spare-part metadata record derived from the same vendor package.",
          uri: "https://example.com/vendor/p204-seal-kit",
          originalId: "vds-p204-seal-kit",
          dataSourceName: "Default Data Source",
          proposedClass: "Spare Part",
          sourceRecordName: "Vendor Data Sheet VDS-204.pdf",
          sourceRecordUri:
            "s3://nexus/project-14/vendor-data-sheet-vds-204.pdf",
          additionalProperties: {
            manufacturer: "North Ridge Pumps",
            part_number: "SK-204-A",
            compatibility: {
              asset_tag: "P-204",
              service: "Cooling Water Circulation",
            },
            replacement_cycle: {
              recommended_months: 24,
              criticality: "High",
            },
          },
        },
      ],
      suggestedClasses: [],
      suggestedEdges: [],
      suggestedRelationships: [],
      connectedRecords: [],
    },
    {
      recordId: 2192,
      projectId: context.projectId,
      recordName: "Loop Narrative CL-7.docx",
      recordUri: "s3://nexus/project-14/loop-narrative-cl7.docx",
      recordClass: "Narrative",
      extractedAt: "2026-04-14 08:43",
      llmModel: "gpt-5.4",
      reviewDecision: "pending",
      summary:
        "This extraction produced a class and a relationship suggestion, but no new record draft or edges.",
      suggestedRecords: [],
      suggestedClasses: [
        {
          id: "class-2192-1",
          name: "System Narrative",
          confidence: 0.82,
          rationale:
            "The source reads like explanatory system documentation instead of a drawing or asset record.",
          evidence:
            "Most content is prose describing system behavior, operating states, and alarms.",
          decision: "pending",
        },
      ],
      suggestedEdges: [],
      suggestedRelationships: [
        {
          id: "relationship-2192-1",
          subject: "Loop Narrative CL-7",
          predicate: "describes",
          object: "Cooling Loop CL-7",
          confidence: 0.77,
          rationale:
            "The narrative is about the loop as an operating system rather than a single component.",
          evidence:
            "The sections describe startup, shutdown, and normal operating behavior for CL-7.",
          decision: "pending",
        },
      ],
      connectedRecords: [],
    },
  ];
}

export function countPendingSuggestions(groups: LatticeRecordGroup[]) {
  return groups.reduce(
    (totals, group) => ({
      classes:
        totals.classes +
        group.suggestedClasses.filter((item) => item.decision === "pending")
          .length,
      edges:
        totals.edges +
        group.suggestedEdges.filter((item) => item.decision === "pending").length,
      relationships:
        totals.relationships +
        group.suggestedRelationships.filter(
          (item) => item.decision === "pending",
        ).length,
    }),
    { classes: 0, edges: 0, relationships: 0 },
  );
}

export function getRecordSuggestionCount(group: LatticeRecordGroup) {
  return (
    group.suggestedRecords.length +
    group.suggestedClasses.length +
    group.suggestedEdges.length +
    group.suggestedRelationships.length
  );
}

export function getRecordReviewStatus(group: LatticeRecordGroup) {
  if (group.reviewDecision === "approved") return "approved";
  if (group.reviewDecision === "denied") return "denied";
  return "needs_review";
}

export function flattenSuggestions(
  group: LatticeRecordGroup,
): FlattenedSuggestion[] {
  return [
    ...group.suggestedRecords.map((item, index) => ({
      id: `record-${group.recordId}-${index}`,
      type: "record" as const,
      title: item.name,
      confidence: 0.91,
      rationale:
        "The extraction found enough structured metadata to justify creating a new record draft.",
      evidence: item.description,
      decision: group.reviewDecision,
      recordDraft: item,
    })),
    ...group.suggestedClasses.map((item) => ({
      id: item.id,
      type: "class" as const,
      title: item.name,
      confidence: item.confidence,
      rationale: item.rationale,
      evidence: item.evidence,
      decision: item.decision,
    })),
    ...group.suggestedEdges.map((item) => ({
      id: item.id,
      type: "edge" as const,
      title: `${item.from} -> ${item.label} -> ${item.to}`,
      confidence: item.confidence,
      rationale: item.rationale,
      evidence: item.evidence,
      decision: item.decision,
    })),
    ...group.suggestedRelationships.map((item) => ({
      id: item.id,
      type: "relationship" as const,
      title: `${item.subject} ${item.predicate} ${item.object}`,
      confidence: item.confidence,
      rationale: item.rationale,
      evidence: item.evidence,
      decision: item.decision,
    })),
  ];
}
