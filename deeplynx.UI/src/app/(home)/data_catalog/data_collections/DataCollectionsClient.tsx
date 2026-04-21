"use client";

import Tabs from "@/app/(home)/components/Tabs";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import { useProjectSession } from "@/app/contexts/ProjectSessionProvider";
import {
  ArrowsRightLeftIcon,
  ArrowLeftIcon,
  ChatBubbleLeftRightIcon,
  FolderPlusIcon,
  MagnifyingGlassIcon,
  PencilSquareIcon,
  ShieldCheckIcon,
  UserGroupIcon,
} from "@heroicons/react/24/outline";
import Link from "next/link";
import React, { useMemo, useState } from "react";

type MockCollection = {
  id: number;
  name: string;
  description: string;
  sensitivity: "Low" | "Moderate" | "High";
  tags: string[];
  recordCount: number;
  owner: string;
  updatedAt: string;
  metadata: Array<{ label: string; value: string }>;
};

type TopLevelTabId = "All Collections" | "New Collection";
type CollectionWorkspaceTabId = "Details" | "Records";

const MOCK_COLLECTIONS: MockCollection[] = [
  {
    id: 1,
    name: "Critical Asset Review",
    description:
      "Focused set of high-value records prepared for engineering review and audit follow-up.",
    sensitivity: "High",
    tags: ["audit", "handoff", "priority"],
    recordCount: 84,
    owner: "Systems Integrity Team",
    updatedAt: "Apr 20, 2026",
    metadata: [
      { label: "Retention", value: "FY26 review cycle" },
      { label: "Owning group", value: "Systems Integrity" },
      { label: "Review cadence", value: "Weekly" },
    ],
  },
  {
    id: 2,
    name: "Field Validation Set",
    description:
      "Records grouped for validation, exception handling, and field-ready signoff.",
    sensitivity: "Moderate",
    tags: ["validation", "field", "qa"],
    recordCount: 231,
    owner: "Deployment Operations",
    updatedAt: "Apr 18, 2026",
    metadata: [
      { label: "Region", value: "North Testbed" },
      { label: "Lifecycle", value: "In validation" },
      { label: "External share", value: "Restricted" },
    ],
  },
  {
    id: 3,
    name: "Model Training Candidates",
    description:
      "Curated records tagged for enrichment and downstream analytics preparation.",
    sensitivity: "Low",
    tags: ["analytics", "training", "metadata"],
    recordCount: 512,
    owner: "Data Science",
    updatedAt: "Apr 16, 2026",
    metadata: [
      { label: "Pipeline", value: "Classification prep" },
      { label: "Coverage", value: "Cross-project" },
      { label: "Refresh", value: "Nightly" },
    ],
  },
  {
    id: 4,
    name: "Supplier Package Intake",
    description:
      "Temporary intake collection for imported records awaiting classification and tagging.",
    sensitivity: "Moderate",
    tags: ["intake", "supplier", "staging"],
    recordCount: 148,
    owner: "Project Controls",
    updatedAt: "Apr 14, 2026",
    metadata: [
      { label: "Queue", value: "Intake backlog" },
      { label: "Target SLA", value: "48 hours" },
      { label: "Disposition", value: "Needs review" },
    ],
  },
];

const MOCK_RECORD_ROWS = [
  {
    name: "Pump Station Survey Package",
    className: "Document",
    source: "Field Upload",
    state: "Included",
  },
  {
    name: "Equipment Registry Snapshot",
    className: "Dataset",
    source: "System Sync",
    state: "Pending",
  },
  {
    name: "Inspection Finding 1844",
    className: "Record",
    source: "Manual Entry",
    state: "Included",
  },
  {
    name: "Valve Assembly Diagram",
    className: "CAD File",
    source: "Supplier Import",
    state: "Excluded",
  },
];

const COLLECTION_RECORDS: Record<number, typeof MOCK_RECORD_ROWS> = {
  1: [
    {
      name: "Pump Station Survey Package",
      className: "Document",
      source: "Field Upload",
      state: "Included",
    },
    {
      name: "Inspection Finding 1844",
      className: "Record",
      source: "Manual Entry",
      state: "Included",
    },
    {
      name: "Control Room Asset Register",
      className: "Dataset",
      source: "System Sync",
      state: "Included",
    },
  ],
  2: [
    {
      name: "Field Validation Checklist",
      className: "Document",
      source: "Field Upload",
      state: "Included",
    },
    {
      name: "Equipment Registry Snapshot",
      className: "Dataset",
      source: "System Sync",
      state: "Included",
    },
    {
      name: "Valve Assembly Diagram",
      className: "CAD File",
      source: "Supplier Import",
      state: "Included",
    },
  ],
  3: [
    {
      name: "Metadata Enrichment Batch 12",
      className: "Dataset",
      source: "System Sync",
      state: "Included",
    },
    {
      name: "Ontology Candidate Review",
      className: "Record",
      source: "Manual Entry",
      state: "Included",
    },
  ],
  4: [
    {
      name: "Supplier Package Intake",
      className: "Document",
      source: "Supplier Import",
      state: "Included",
    },
    {
      name: "Receiving Log Extract",
      className: "Dataset",
      source: "System Sync",
      state: "Included",
    },
  ],
};

function getSensitivityClass(sensitivity: MockCollection["sensitivity"]) {
  switch (sensitivity) {
    case "High":
      return "badge-error";
    case "Moderate":
      return "badge-warning";
    default:
      return "badge-success";
  }
}

function SectionCard({
  title,
  subtitle,
  action,
  children,
}: {
  title: string;
  subtitle?: string;
  action?: React.ReactNode;
  children: React.ReactNode;
}) {
  return (
    <section className="card border border-base-300 bg-base-100 shadow-sm">
      <div className="card-body gap-4">
        <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
          <div>
            <h2 className="text-lg font-semibold text-base-content">{title}</h2>
            {subtitle ? (
              <p className="text-sm text-base-content/70">{subtitle}</p>
            ) : null}
          </div>
          {action}
        </div>
        {children}
      </div>
    </section>
  );
}

export default function DataCollectionsClient() {
  const { project } = useProjectSession();
  const { organization } = useOrganizationSession();
  const [activeTab, setActiveTab] = useState<TopLevelTabId>("All Collections");
  const [searchTerm, setSearchTerm] = useState("");
  const [selectedCollection, setSelectedCollection] =
    useState<MockCollection | null>(null);
  const [collectionWorkspaceTab, setCollectionWorkspaceTab] =
    useState<CollectionWorkspaceTabId>("Details");

  const filteredCollections = useMemo(() => {
    const query = searchTerm.trim().toLowerCase();
    if (!query) return MOCK_COLLECTIONS;

    return MOCK_COLLECTIONS.filter((collection) => {
      const haystack = [
        collection.name,
        collection.description,
        collection.sensitivity,
        collection.tags.join(" "),
      ]
        .join(" ")
        .toLowerCase();

      return haystack.includes(query);
    });
  }, [searchTerm]);

  const selectedCollectionRecords = useMemo(
    () =>
      selectedCollection
        ? COLLECTION_RECORDS[selectedCollection.id] ?? MOCK_RECORD_ROWS
        : [],
    [selectedCollection],
  );

  const explorerTab = (
    <div className="mt-4 space-y-6">
      <div className="grid gap-4 md:grid-cols-3">
        <div className="rounded-2xl border border-base-300 bg-base-100 p-5 shadow-sm">
          <p className="text-sm uppercase tracking-wide text-base-content/60">
            Visible Collections
          </p>
          <p className="mt-2 text-3xl font-semibold text-base-content">
            {MOCK_COLLECTIONS.length}
          </p>
          <p className="mt-1 text-sm text-base-content/70">
            Default landing state shows every collection the user can access.
          </p>
        </div>
        <div className="rounded-2xl border border-base-300 bg-base-100 p-5 shadow-sm">
          <p className="text-sm uppercase tracking-wide text-base-content/60">
            Search Coverage
          </p>
          <p className="mt-2 text-3xl font-semibold text-base-content">
            Name, Tags, Sensitivity
          </p>
          <p className="mt-1 text-sm text-base-content/70">
            Matches your stated search fields without needing a new filter model.
          </p>
        </div>
        <div className="rounded-2xl border border-base-300 bg-base-100 p-5 shadow-sm">
          <p className="text-sm uppercase tracking-wide text-base-content/60">
            Active Project
          </p>
          <p className="mt-2 text-3xl font-semibold text-base-content">
            {project?.projectName || "No project selected"}
          </p>
          <p className="mt-1 text-sm text-base-content/70">
            Organization: {organization?.organizationName || "No organization"}
          </p>
        </div>
      </div>

      <SectionCard
        title="All Collections"
        subtitle="Wireframe for the default collections landing page with search and quick governance context."
        action={
          <button
            type="button"
            className="btn btn-primary btn-sm"
            onClick={() => setActiveTab("New Collection")}
          >
            <FolderPlusIcon className="size-4" />
            New Collection
          </button>
        }
      >
        <div className="grid gap-3 lg:grid-cols-[minmax(0,1fr)_auto_auto]">
          <label className="input input-bordered flex items-center gap-2 w-full">
            <MagnifyingGlassIcon className="size-5 text-base-content/60" />
            <input
              type="text"
              className="grow"
              placeholder="Search by name, tags, or sensitivity"
              value={searchTerm}
              onChange={(event) => setSearchTerm(event.target.value)}
            />
          </label>
          <div className="flex gap-2 flex-wrap">
            {["All", "High", "Moderate", "Low"].map((chip) => (
              <button
                key={chip}
                type="button"
                className="btn btn-sm btn-ghost border border-base-300"
              >
                {chip}
              </button>
            ))}
          </div>
          <button type="button" className="btn btn-sm btn-outline">
            Saved Views
          </button>
        </div>

        <div className="grid gap-4 xl:grid-cols-2">
          {filteredCollections.map((collection) => (
            <button
              key={collection.id}
              type="button"
              className="rounded-2xl border border-base-300 bg-base-200/30 p-5 text-left transition hover:border-base-content/30 hover:bg-base-200/50"
              onClick={() => {
                setSelectedCollection(collection);
                setCollectionWorkspaceTab("Details");
              }}
            >
              <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
                <div className="space-y-2">
                  <div className="flex flex-wrap items-center gap-2">
                    <h3 className="text-lg font-semibold text-base-content">
                      {collection.name}
                    </h3>
                    <span
                      className={`badge badge-sm ${getSensitivityClass(collection.sensitivity)}`}
                    >
                      {collection.sensitivity}
                    </span>
                  </div>
                  <p className="text-sm text-base-content/70">
                    {collection.description}
                  </p>
                </div>
                <span className="text-sm font-medium text-info-content">
                  Open details
                </span>
              </div>

              <div className="mt-4 flex flex-wrap gap-2">
                {collection.tags.map((tag) => (
                  <span
                    key={`${collection.id}-${tag}`}
                    className="badge badge-sm badge-outline badge-secondary"
                  >
                    {tag}
                  </span>
                ))}
              </div>

              <div className="mt-4 grid gap-3 sm:grid-cols-3 text-sm">
                <div>
                  <p className="text-base-content/60">Records</p>
                  <p className="font-semibold text-base-content">
                    {collection.recordCount}
                  </p>
                </div>
                <div>
                  <p className="text-base-content/60">Owner</p>
                  <p className="font-semibold text-base-content">
                    {collection.owner}
                  </p>
                </div>
                <div>
                  <p className="text-base-content/60">Updated</p>
                  <p className="font-semibold text-base-content">
                    {collection.updatedAt}
                  </p>
                </div>
              </div>
            </button>
          ))}
        </div>
      </SectionCard>
    </div>
  );

  const newCollectionTab = (
    <div className="mt-4">
      <SectionCard
        title="New Collection"
        subtitle="Wireframe for the create flow, showing the minimum metadata and governance fields needed at creation time."
      >
        <div className="grid gap-6 xl:grid-cols-[minmax(0,1.3fr)_minmax(280px,0.7fr)]">
          <div className="space-y-4">
            <label className="form-control w-full">
              <div className="label">
                <span className="label-text font-medium">Collection name</span>
              </div>
              <input
                type="text"
                className="input input-bordered w-full"
                value="Safety Review Bundle"
                readOnly
              />
            </label>

            <label className="form-control w-full">
              <div className="label">
                <span className="label-text font-medium">Description</span>
              </div>
              <textarea
                className="textarea textarea-bordered h-28"
                value="Collection for staged record review, audit prep, and controlled collaboration with project stakeholders."
                readOnly
              />
            </label>

            <div className="grid gap-4 md:grid-cols-2">
              <label className="form-control">
                <div className="label">
                  <span className="label-text font-medium">Sensitivity</span>
                </div>
                <select className="select select-bordered" value="Moderate" disabled>
                  <option>Low</option>
                  <option>Moderate</option>
                  <option>High</option>
                </select>
              </label>

              <label className="form-control">
                <div className="label">
                  <span className="label-text font-medium">Default visibility</span>
                </div>
                <select
                  className="select select-bordered"
                  value="Project members"
                  disabled
                >
                  <option>Project members</option>
                  <option>Owners only</option>
                  <option>Custom access</option>
                </select>
              </label>
            </div>

            <div className="grid gap-4 md:grid-cols-2">
              <label className="form-control">
                <div className="label">
                  <span className="label-text font-medium">Tags</span>
                </div>
                <div className="rounded-2xl border border-base-300 bg-base-200/30 p-4">
                  <div className="flex flex-wrap gap-2">
                    {["review", "handoff", "critical"].map((tag) => (
                      <span
                        key={tag}
                        className="badge badge-secondary badge-outline"
                      >
                        {tag}
                      </span>
                    ))}
                    <button type="button" className="btn btn-ghost btn-xs">
                      + add tag
                    </button>
                  </div>
                </div>
              </label>

              <label className="form-control">
                <div className="label">
                  <span className="label-text font-medium">
                    Metadata template
                  </span>
                </div>
                <div className="rounded-2xl border border-dashed border-base-300 bg-base-100 p-4">
                  <div className="space-y-2 text-sm text-base-content/70">
                    <p>Owner group: Systems Integrity</p>
                    <p>Retention rule: FY26 lifecycle</p>
                    <p>Required reviewer: Project QA Lead</p>
                  </div>
                </div>
              </label>
            </div>
          </div>

          <div className="space-y-4">
            <div className="rounded-2xl border border-base-300 bg-base-200/30 p-5">
              <div className="flex items-center gap-2">
                <ShieldCheckIcon className="size-5 text-info-content" />
                <h3 className="font-semibold text-base-content">
                  Create-Time Guardrails
                </h3>
              </div>
              <ul className="mt-4 space-y-3 text-sm text-base-content/80">
                <li>Require sensitivity before saving.</li>
                <li>Allow optional metadata template injection.</li>
                <li>Start with an empty collection or prefill from a saved search.</li>
              </ul>
            </div>

            <div className="rounded-2xl border border-base-300 bg-base-100 p-5">
              <h3 className="font-semibold text-base-content">
                Footer Actions
              </h3>
              <div className="mt-4 flex flex-wrap gap-2">
                <button type="button" className="btn btn-primary btn-sm">
                  Save Collection
                </button>
                <button
                  type="button"
                  className="btn btn-outline btn-sm"
                  onClick={() => {
                    setSelectedCollection(MOCK_COLLECTIONS[0]);
                    setCollectionWorkspaceTab("Records");
                  }}
                >
                  Save and add records
                </button>
                <button type="button" className="btn btn-ghost btn-sm">
                  Cancel
                </button>
              </div>
            </div>
          </div>
        </div>
      </SectionCard>
    </div>
  );

  const selectedCollectionRecordsTab = (
    <div className="mt-4">
      <SectionCard
        title="Records"
        subtitle={`Records currently assigned to ${selectedCollection?.name ?? "this collection"}, plus add/remove controls for that collection only.`}
      >
        <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_360px]">
          <div className="space-y-4">
            <label className="input input-bordered flex items-center gap-2 w-full">
              <MagnifyingGlassIcon className="size-5 text-base-content/60" />
              <input
                type="text"
                className="grow"
                placeholder="Search records to add or remove"
                value="inspection"
                readOnly
              />
            </label>

            <div className="overflow-x-auto rounded-2xl border border-base-300">
              <table className="table">
                <thead>
                  <tr>
                    <th></th>
                    <th>Record</th>
                    <th>Class</th>
                    <th>Source</th>
                    <th>Status</th>
                  </tr>
                </thead>
                <tbody>
                  {selectedCollectionRecords.map((row) => (
                    <tr key={row.name}>
                      <td>
                        <input
                          type="checkbox"
                          className="checkbox checkbox-sm"
                          defaultChecked={row.state !== "Excluded"}
                        />
                      </td>
                      <td className="font-medium">{row.name}</td>
                      <td>{row.className}</td>
                      <td>{row.source}</td>
                      <td>
                        <span className="badge badge-sm badge-outline">
                          {row.state}
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>

          <div className="space-y-4">
            <div className="rounded-2xl border border-base-300 bg-base-200/30 p-5">
              <div className="flex items-center gap-2">
                <ArrowsRightLeftIcon className="size-5 text-info-content" />
                <h3 className="font-semibold text-base-content">
                  Collection Actions
                </h3>
              </div>
              <div className="mt-4 space-y-3">
                <div className="rounded-xl border border-base-300 bg-base-100 p-4">
                  <p className="text-sm text-base-content/60">Selected collection</p>
                  <p className="mt-1 font-medium text-base-content">
                    {selectedCollection?.name ?? "No collection selected"}
                  </p>
                </div>
                <div className="rounded-xl border border-base-300 bg-base-100 p-4">
                  <p className="text-sm text-base-content/60">Scope</p>
                  <p className="mt-1 text-sm text-base-content/80">
                    Add records to this collection, remove records from this
                    collection, or bulk-apply governance checks before saving.
                  </p>
                </div>
                <div className="rounded-xl border border-base-300 bg-base-100 p-4">
                  <p className="text-sm text-base-content/60">Quick stats</p>
                  <p className="mt-1 text-sm text-base-content/80">
                    {selectedCollectionRecords.length} visible records in this
                    collection mockup.
                  </p>
                </div>
              </div>
            </div>

            <div className="rounded-2xl border border-base-300 bg-base-100 p-5">
              <h3 className="font-semibold text-base-content">Change summary</h3>
              <ul className="mt-4 space-y-2 text-sm text-base-content/80">
                <li>2 records will be added to {selectedCollection?.name ?? "this collection"}.</li>
                <li>1 record will be removed from {selectedCollection?.name ?? "this collection"}.</li>
                <li>Governance checks run before commit.</li>
              </ul>

              <div className="mt-5 flex flex-wrap gap-2">
                <button type="button" className="btn btn-primary btn-sm">
                  Apply changes
                </button>
                <button type="button" className="btn btn-outline btn-sm">
                  Preview impact
                </button>
              </div>
            </div>
          </div>
        </div>
      </SectionCard>
    </div>
  );

  const selectedCollectionDetailsTab = (
    <div className="mt-4">
      <SectionCard
        title="Collection Details"
        subtitle="Wireframe for editing collection sensitivity, metadata, and tags after creation."
      >
        <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_380px]">
          <div className="space-y-4">
            <div className="rounded-2xl border border-base-300 bg-base-200/30 p-5">
              <div className="flex items-center justify-between gap-3">
                <div>
                  <h3 className="text-lg font-semibold text-base-content">
                    {selectedCollection?.name}
                  </h3>
                  <p className="text-sm text-base-content/70">
                    {selectedCollection?.description}
                  </p>
                </div>
                <button type="button" className="btn btn-ghost btn-sm">
                  <PencilSquareIcon className="size-4" />
                  Rename
                </button>
              </div>

              <div className="mt-5 grid gap-4 md:grid-cols-2">
                <div className="rounded-xl border border-base-300 bg-base-100 p-4">
                  <p className="text-sm text-base-content/60">Sensitivity</p>
                  <div className="mt-2 flex items-center gap-2">
                    <span
                      className={`badge badge-sm ${getSensitivityClass(selectedCollection?.sensitivity ?? "Low")}`}
                    >
                      {selectedCollection?.sensitivity}
                    </span>
                    <span className="text-sm text-base-content/70">
                      Override requires review
                    </span>
                  </div>
                </div>
                <div className="rounded-xl border border-base-300 bg-base-100 p-4">
                  <p className="text-sm text-base-content/60">Tags</p>
                  <div className="mt-2 flex flex-wrap gap-2">
                    {selectedCollection?.tags.map((tag) => (
                      <span
                        key={`governance-${tag}`}
                        className="badge badge-secondary badge-outline"
                      >
                        {tag}
                      </span>
                    ))}
                  </div>
                </div>
              </div>
            </div>

            <div className="rounded-2xl border border-base-300 bg-base-100 p-5">
              <h3 className="font-semibold text-base-content">Metadata</h3>
              <div className="mt-4 overflow-x-auto">
                <table className="table">
                  <thead>
                    <tr>
                      <th>Field</th>
                      <th>Value</th>
                      <th></th>
                    </tr>
                  </thead>
                  <tbody>
                    {selectedCollection?.metadata.map((row) => (
                      <tr key={row.label}>
                        <td className="font-medium">{row.label}</td>
                        <td>{row.value}</td>
                        <td className="text-right">
                          <button type="button" className="btn btn-ghost btn-xs">
                            Edit
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          </div>

          <div className="space-y-4">
            <div className="rounded-2xl border border-base-300 bg-base-200/30 p-5">
              <h3 className="font-semibold text-base-content">
                Implementation Notes
              </h3>
              <ul className="mt-4 space-y-3 text-sm text-base-content/80">
                <li>Sensitivity should be first-class so it can drive both filtering and access checks.</li>
                <li>Metadata wants a schema-backed key/value editor rather than free text only.</li>
                <li>Tagging can reuse existing badge and attachment patterns from record management.</li>
              </ul>
            </div>

            <div className="rounded-2xl border border-base-300 bg-base-100 p-5">
              <h3 className="font-semibold text-base-content">Next step</h3>
              <p className="mt-3 text-sm text-base-content/70">
                This wireframe page is linked from the sidebar and pairs with the
                record detail panel so the feature exists both as a destination
                page and as embedded context.
              </p>
              <div className="mt-4">
                <Link
                  href="/data_catalog/all_records"
                  className="link link-hover text-info-content"
                >
                  Return to All Records
                </Link>
              </div>
            </div>
          </div>
        </div>
      </SectionCard>
    </div>
  );

  const topLevelTabs = [
    { label: "All Collections", content: explorerTab },
    { label: "New Collection", content: newCollectionTab },
  ];

  return (
    <div>
      <div className="bg-base-200/40 px-3 sm:px-6 lg:px-12 py-2 pb-4">
        <div className="flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
          <div>
            <div className="badge badge-outline mb-3">Wireframe</div>
            <h1 className="text-xl sm:text-2xl font-bold text-info-content">
              Data Collections
            </h1>
            <p className="mt-2 max-w-3xl text-sm text-base-content/70">
              Dedicated mockup page for browsing collections, creating new
              collections, managing record membership, and editing sensitivity,
              metadata, and tags while preserving the current UI language.
            </p>
          </div>
          <div className="flex gap-2 flex-wrap">
            <button
              type="button"
              className="btn btn-primary btn-sm"
              onClick={() => setActiveTab("New Collection")}
            >
              New collection
            </button>
          </div>
        </div>
      </div>

      <div className="px-3 pb-8 pt-6 sm:px-6 lg:px-8">
        {selectedCollection ? (
          <div className="grid gap-6 xl:grid-cols-[300px_minmax(0,1fr)]">
            <aside className="space-y-4">
              <div className="rounded-2xl border border-base-300 bg-base-100 p-5 shadow-sm">
                <button
                  type="button"
                  className="btn btn-ghost btn-sm justify-start px-0"
                  onClick={() => {
                    setSelectedCollection(null);
                    setActiveTab("All Collections");
                  }}
                >
                  <ArrowLeftIcon className="size-4" />
                  All Collections
                </button>

                <div className="mt-4 rounded-2xl bg-base-200/40 p-4">
                  <p className="text-xs uppercase tracking-wide text-base-content/60">
                    Collection Workspace
                  </p>
                  <h2 className="mt-2 text-xl font-semibold text-base-content">
                    {selectedCollection.name}
                  </h2>
                  <p className="mt-2 text-sm text-base-content/70">
                    {selectedCollection.description}
                  </p>
                  <div className="mt-4 flex flex-wrap gap-2">
                    <span
                      className={`badge badge-sm ${getSensitivityClass(selectedCollection.sensitivity)}`}
                    >
                      {selectedCollection.sensitivity}
                    </span>
                    <span className="badge badge-outline badge-sm">
                      {selectedCollectionRecords.length} records
                    </span>
                    <span className="badge badge-outline badge-sm">
                      {selectedCollection.owner}
                    </span>
                  </div>
                </div>

                <div className="mt-4 space-y-2">
                  <button
                    type="button"
                    className={`w-full rounded-2xl border px-4 py-3 text-left transition ${
                      collectionWorkspaceTab === "Details"
                        ? "border-info bg-info/10"
                        : "border-base-300 bg-base-100 hover:bg-base-200/40"
                    }`}
                    onClick={() => setCollectionWorkspaceTab("Details")}
                  >
                    <p className="font-medium text-base-content">Details</p>
                    <p className="mt-1 text-sm text-base-content/65">
                      Governance, metadata, tags, and collection context.
                    </p>
                  </button>
                  <button
                    type="button"
                    className={`w-full rounded-2xl border px-4 py-3 text-left transition ${
                      collectionWorkspaceTab === "Records"
                        ? "border-info bg-info/10"
                        : "border-base-300 bg-base-100 hover:bg-base-200/40"
                    }`}
                    onClick={() => setCollectionWorkspaceTab("Records")}
                  >
                    <p className="font-medium text-base-content">Records</p>
                    <p className="mt-1 text-sm text-base-content/65">
                      Browse and manage records that belong to this collection.
                    </p>
                  </button>
                </div>
              </div>

              <div className="rounded-2xl border border-base-300 bg-base-100 p-5 shadow-sm">
                <div className="flex items-center gap-2">
                  <UserGroupIcon className="size-5 text-info-content" />
                  <h3 className="font-semibold text-base-content">
                    Collaboration
                  </h3>
                </div>
                <div className="mt-4 flex items-center gap-2">
                  {["SI", "QA", "DO"].map((member) => (
                    <span
                      key={member}
                      className="inline-flex size-9 items-center justify-center rounded-full bg-info/15 text-xs font-semibold text-info-content"
                    >
                      {member}
                    </span>
                  ))}
                </div>
                <p className="mt-3 text-sm text-base-content/70">
                  Shared workspace for review, handoff, and collection-level
                  decisions tied to {selectedCollection.name}.
                </p>
              </div>

              <div className="rounded-2xl border border-base-300 bg-base-100 p-5 shadow-sm">
                <div className="flex items-center gap-2">
                  <ChatBubbleLeftRightIcon className="size-5 text-info-content" />
                  <h3 className="font-semibold text-base-content">
                    Recent Activity
                  </h3>
                </div>
                <ul className="mt-4 space-y-3 text-sm text-base-content/75">
                  <li>{selectedCollection.owner} updated collection metadata.</li>
                  <li>Three records were reviewed for inclusion.</li>
                  <li>Sensitivity and tags were last checked on {selectedCollection.updatedAt}.</li>
                </ul>
              </div>
            </aside>

            <div className="space-y-4">
              <div className="rounded-2xl border border-base-300 bg-base-100 p-5 shadow-sm">
                <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
                  <div>
                    <p className="text-xs uppercase tracking-wide text-base-content/60">
                      Inside {selectedCollection.name}
                    </p>
                    <h3 className="mt-2 text-2xl font-semibold text-base-content">
                      {collectionWorkspaceTab}
                    </h3>
                    <p className="mt-2 max-w-3xl text-sm text-base-content/70">
                      This view is scoped to {selectedCollection.name}. Switch
                      between collection details and collection records without
                      leaving the selected collection workspace.
                    </p>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    <span className="badge badge-outline">
                      Updated {selectedCollection.updatedAt}
                    </span>
                    <span className="badge badge-outline">
                      Owner: {selectedCollection.owner}
                    </span>
                  </div>
                </div>
              </div>

              {collectionWorkspaceTab === "Details"
                ? selectedCollectionDetailsTab
                : selectedCollectionRecordsTab}
            </div>
          </div>
        ) : (
          <Tabs
            tabs={topLevelTabs}
            className="w-full"
            activeTab={activeTab}
            onTabChange={(label) => setActiveTab(label as TopLevelTabId)}
          />
        )}
      </div>
    </div>
  );
}
