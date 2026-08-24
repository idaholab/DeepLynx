"use client";

import {
  ArrowLeftIcon,
  CheckIcon,
  InformationCircleIcon,
  MagnifyingGlassIcon,
  PlusIcon,
  TrashIcon,
  UserGroupIcon,
  UserIcon,
  XMarkIcon,
} from "@heroicons/react/24/outline";
import { type Dispatch, type SetStateAction, useEffect, useMemo, useState } from "react";

type DetailTab = "details" | "assigned-users";
type WizardStep = "select" | "preview";
type PickerTab = "groups" | "users";

type LabelDefinition = {
  id: string;
  name: string;
  description: string;
  origin: "Organization" | "Project";
  color: string;
};

type ProjectUser = {
  id: string;
  name: string;
  email: string;
  role: string;
  groupIds: string[];
};

type ProjectGroup = {
  id: string;
  name: string;
  description: string;
  memberIds: string[];
};

const labels: LabelDefinition[] = [
  {
    id: "public",
    name: "Public",
    description: "Information approved for general project access.",
    origin: "Organization",
    color: "bg-success",
  },
  {
    id: "internal",
    name: "Project Internal",
    description: "Information restricted to approved project members.",
    origin: "Project",
    color: "bg-info",
  },
  {
    id: "export",
    name: "Export Controlled",
    description: "Export-controlled information requiring explicit user access.",
    origin: "Organization",
    color: "bg-warning",
  },
  {
    id: "cui",
    name: "CUI",
    description: "Controlled Unclassified Information.",
    origin: "Organization",
    color: "bg-secondary",
  },
];

const users: ProjectUser[] = [
  { id: "jordan", name: "Jordan Davis", email: "jordan.davis@inl.gov", role: "Project Member", groupIds: ["engineering"] },
  { id: "alex", name: "Alex Morgan", email: "alex.morgan@inl.gov", role: "Read Only", groupIds: ["analysis"] },
  { id: "taylor", name: "Taylor Kim", email: "taylor.kim@inl.gov", role: "Project Member", groupIds: ["engineering", "analysis"] },
  { id: "sam", name: "Sam Rivera", email: "sam.rivera@inl.gov", role: "Project Member", groupIds: ["engineering"] },
  { id: "priya", name: "Priya Shah", email: "priya.shah@inl.gov", role: "Project Member", groupIds: ["engineering"] },
  { id: "chris", name: "Chris Bennett", email: "chris.bennett@inl.gov", role: "Project Member", groupIds: ["engineering"] },
  { id: "drew", name: "Drew Adams", email: "drew.adams@inl.gov", role: "Project Member", groupIds: ["engineering"] },
  { id: "casey", name: "Casey Wilson", email: "casey.wilson@inl.gov", role: "Project Member", groupIds: ["engineering"] },
  { id: "riley", name: "Riley Chen", email: "riley.chen@inl.gov", role: "Project Member", groupIds: ["engineering"] },
  { id: "jamie", name: "Jamie Ortiz", email: "jamie.ortiz@inl.gov", role: "Project Member", groupIds: ["engineering"] },
  { id: "lee", name: "Lee Martin", email: "lee.martin@inl.gov", role: "Read Only", groupIds: ["analysis"] },
  { id: "noah", name: "Noah Clark", email: "noah.clark@inl.gov", role: "Project Member", groupIds: ["analysis"] },
  { id: "mia", name: "Mia Thompson", email: "mia.thompson@inl.gov", role: "Project Member", groupIds: [] },
];

const groups: ProjectGroup[] = [
  {
    id: "engineering",
    name: "Engineering Team",
    description: "Core project engineering contributors",
    memberIds: ["jordan", "taylor", "sam", "priya", "chris", "drew", "casey", "riley", "jamie"],
  },
  {
    id: "analysis",
    name: "Analysis Group",
    description: "Modeling and analysis contributors",
    memberIds: ["alex", "taylor", "lee", "noah"],
  },
  {
    id: "external",
    name: "External Partners",
    description: "Approved partner organization users",
    memberIds: ["alex", "mia"],
  },
];

const initiallyAssignedUserIds = ["jordan", "alex", "taylor"];

function userInitials(name: string) {
  return name
    .split(" ")
    .map((part) => part[0])
    .join("")
    .slice(0, 2);
}

function SearchField({ placeholder }: { placeholder: string }) {
  return (
    <label className="input input-bordered flex w-full items-center gap-2 bg-base-100">
      <MagnifyingGlassIcon className="h-4 w-4 text-base-content/50" />
      <input type="search" className="grow" placeholder={placeholder} aria-label={placeholder} />
    </label>
  );
}

function UserAvatar({ user }: { user: ProjectUser }) {
  return (
    <span className="grid h-9 w-9 shrink-0 place-items-center rounded-full bg-primary/10 text-sm font-bold text-primary">
      {userInitials(user.name)}
    </span>
  );
}

function AssignmentWizard({
  labelName,
  open,
  step,
  setStep,
  pickerTab,
  setPickerTab,
  selectedGroupIds,
  selectedUserIds,
  excludedPreviewUserIds,
  assignedUserIds,
  onToggleGroup,
  onToggleUser,
  onTogglePreviewUser,
  onClose,
  onAssign,
}: {
  labelName: string;
  open: boolean;
  step: WizardStep;
  setStep: (step: WizardStep) => void;
  pickerTab: PickerTab;
  setPickerTab: (tab: PickerTab) => void;
  selectedGroupIds: Set<string>;
  selectedUserIds: Set<string>;
  excludedPreviewUserIds: Set<string>;
  assignedUserIds: Set<string>;
  onToggleGroup: (id: string) => void;
  onToggleUser: (id: string) => void;
  onTogglePreviewUser: (id: string) => void;
  onClose: () => void;
  onAssign: () => void;
}) {
  const previewUsers = useMemo(() => {
    const ids = new Set(selectedUserIds);
    groups
      .filter((group) => selectedGroupIds.has(group.id))
      .forEach((group) => group.memberIds.forEach((id) => ids.add(id)));
    return users.filter((user) => ids.has(user.id));
  }, [selectedGroupIds, selectedUserIds]);

  const newAssignmentCount = previewUsers.filter(
    (user) => !assignedUserIds.has(user.id) && !excludedPreviewUserIds.has(user.id),
  ).length;
  const existingCount = previewUsers.filter((user) => assignedUserIds.has(user.id)).length;

  if (!open) return null;

  return (
    <dialog open className="modal modal-open">
      <div className="modal-box max-w-5xl border border-base-300 p-0">
        <header className="flex items-start justify-between gap-4 border-b border-base-200 px-6 py-5">
          <div>
            <p className="text-xs font-bold uppercase tracking-wide text-base-content/55">
              {labelName}
            </p>
            <h2 className="text-xl font-bold">Assign label to users</h2>
            <p className="mt-1 text-sm text-base-content/65">
              Groups help select users in bulk. The saved assignments belong to each user individually.
            </p>
          </div>
          <button type="button" className="btn btn-ghost btn-sm" onClick={onClose} aria-label="Close assignment dialog">
            <XMarkIcon className="h-5 w-5" />
          </button>
        </header>

        <div className="border-b border-base-200 px-6 py-4">
          <ul className="steps w-full">
            <li className="step step-primary">Select groups and users</li>
            <li className={`step ${step === "preview" ? "step-primary" : ""}`}>Preview users</li>
          </ul>
        </div>

        {step === "select" ? (
          <div className="grid min-h-[470px] grid-cols-1 lg:grid-cols-[1.35fr_.65fr]">
            <section className="border-b border-base-200 p-6 lg:border-b-0 lg:border-r">
              <div role="tablist" className="tabs tabs-border mb-5 border-b border-base-200">
                <button
                  type="button"
                  role="tab"
                  onClick={() => setPickerTab("groups")}
                  className={`tab gap-2 ${pickerTab === "groups" ? "tab-active text-primary" : ""}`}
                >
                  <UserGroupIcon className="h-4 w-4" />Groups
                </button>
                <button
                  type="button"
                  role="tab"
                  onClick={() => setPickerTab("users")}
                  className={`tab gap-2 ${pickerTab === "users" ? "tab-active text-primary" : ""}`}
                >
                  <UserIcon className="h-4 w-4" />Individual Users
                </button>
              </div>

              <SearchField placeholder={pickerTab === "groups" ? "Search groups" : "Search users"} />

              <div className="mt-4 overflow-hidden rounded-box border border-base-200">
                {pickerTab === "groups"
                  ? groups.map((group) => (
                      <label key={group.id} className="flex cursor-pointer items-center gap-3 border-b border-base-200 px-4 py-4 last:border-b-0 hover:bg-base-200/50">
                        <input
                          type="checkbox"
                          className="checkbox checkbox-primary checkbox-sm"
                          checked={selectedGroupIds.has(group.id)}
                          onChange={() => onToggleGroup(group.id)}
                        />
                        <span className="grid h-9 w-9 place-items-center rounded-full bg-primary/10 text-primary">
                          <UserGroupIcon className="h-5 w-5" />
                        </span>
                        <span className="min-w-0 flex-1">
                          <strong className="block">{group.name}</strong>
                          <span className="block text-sm text-base-content/60">{group.description}</span>
                        </span>
                        <span className="badge badge-outline">{group.memberIds.length} members</span>
                      </label>
                    ))
                  : users.map((user) => {
                      const includedGroup = groups.find(
                        (group) => selectedGroupIds.has(group.id) && group.memberIds.includes(user.id),
                      );
                      const includedThroughGroup = Boolean(includedGroup);
                      return (
                        <label key={user.id} className="flex cursor-pointer items-center gap-3 border-b border-base-200 px-4 py-4 last:border-b-0 hover:bg-base-200/50">
                          <input
                            type="checkbox"
                            className="checkbox checkbox-primary checkbox-sm"
                            checked={selectedUserIds.has(user.id) || includedThroughGroup}
                            disabled={includedThroughGroup}
                            onChange={() => onToggleUser(user.id)}
                          />
                          <UserAvatar user={user} />
                          <span className="min-w-0 flex-1">
                            <strong className="block">{user.name}</strong>
                            <span className="block text-sm text-base-content/60">{user.role} · {user.email}</span>
                          </span>
                          {includedGroup && <span className="badge badge-ghost">Via {includedGroup.name}</span>}
                        </label>
                      );
                    })}
              </div>
            </section>

            <aside className="bg-base-200/35 p-6">
              <h3 className="font-bold">Current selection</h3>
              <p className="mt-1 text-sm text-base-content/65">
                Select any combination of groups and individual users.
              </p>
              <div className="mt-5 space-y-5">
                <div>
                  <p className="mb-2 text-xs font-bold uppercase tracking-wide text-base-content/55">Groups</p>
                  <div className="flex flex-wrap gap-2">
                    {selectedGroupIds.size === 0 && <span className="text-sm text-base-content/50">None selected</span>}
                    {groups.filter((group) => selectedGroupIds.has(group.id)).map((group) => (
                      <span key={group.id} className="badge badge-primary gap-1">{group.name}</span>
                    ))}
                  </div>
                </div>
                <div>
                  <p className="mb-2 text-xs font-bold uppercase tracking-wide text-base-content/55">Individual users</p>
                  <div className="flex flex-wrap gap-2">
                    {selectedUserIds.size === 0 && <span className="text-sm text-base-content/50">None selected</span>}
                    {users.filter((user) => selectedUserIds.has(user.id)).map((user) => (
                      <span key={user.id} className="badge badge-outline badge-primary">{user.name}</span>
                    ))}
                  </div>
                </div>
              </div>
              <div className="alert alert-info mt-6 items-start">
                <InformationCircleIcon className="h-5 w-5 shrink-0" />
                <span className="text-sm">Group membership is expanded only for this assignment. Future group changes will not change label access.</span>
              </div>
            </aside>
          </div>
        ) : (
          <section className="p-6">
            <div className="mb-4 flex flex-wrap items-end justify-between gap-4">
              <div>
                <h3 className="text-lg font-bold">Preview individual assignments</h3>
                <p className="mt-1 text-sm text-base-content/65">
                  Users from selected groups have been expanded and duplicates removed.
                </p>
              </div>
              <div className="text-right text-sm">
                <strong className="block">{newAssignmentCount} new users will receive the label</strong>
                <span className="text-base-content/60">{existingCount} already assigned · {excludedPreviewUserIds.size} deselected</span>
              </div>
            </div>

            <div className="alert alert-info mb-4 items-start">
              <InformationCircleIcon className="h-5 w-5 shrink-0" />
              <span className="text-sm">Already assigned users are protected in this preview. Remove their access later from the Assigned Users list if needed.</span>
            </div>

            <div className="max-h-[390px] overflow-auto rounded-box border border-base-200">
              {previewUsers.map((user) => {
                const alreadyAssigned = assignedUserIds.has(user.id);
                const excluded = excludedPreviewUserIds.has(user.id);
                const selectedGroup = groups.find(
                  (group) => selectedGroupIds.has(group.id) && group.memberIds.includes(user.id),
                );
                return (
                  <label key={user.id} className={`flex items-center gap-3 border-b border-base-200 px-4 py-4 last:border-b-0 ${excluded ? "opacity-55" : ""}`}>
                    <input
                      type="checkbox"
                      className="checkbox checkbox-primary checkbox-sm"
                      checked={alreadyAssigned || !excluded}
                      disabled={alreadyAssigned}
                      onChange={() => onTogglePreviewUser(user.id)}
                    />
                    <UserAvatar user={user} />
                    <span className="min-w-0 flex-1">
                      <strong className="block">{user.name}</strong>
                      <span className="block text-sm text-base-content/60">{user.role} · {user.email}</span>
                    </span>
                    <span className="text-sm text-base-content/60">
                      {selectedGroup ? selectedGroup.name : "Selected individually"}
                    </span>
                    {alreadyAssigned && <span className="badge badge-success gap-1"><CheckIcon className="h-3.5 w-3.5" />Already assigned</span>}
                    {!alreadyAssigned && excluded && <span className="badge badge-ghost">Deselected</span>}
                  </label>
                );
              })}
            </div>
          </section>
        )}

        <footer className="flex flex-wrap items-center justify-between gap-3 border-t border-base-200 px-6 py-4">
          {step === "preview" ? (
            <button type="button" className="btn btn-ghost" onClick={() => setStep("select")}>
              <ArrowLeftIcon className="h-4 w-4" />Back
            </button>
          ) : <span />}
          <div className="flex gap-3">
            <button type="button" className="btn btn-ghost" onClick={onClose}>Cancel</button>
            {step === "select" ? (
              <button
                type="button"
                className="btn btn-primary"
                onClick={() => setStep("preview")}
                disabled={previewUsers.length === 0}
              >
                Preview {previewUsers.length} users
              </button>
            ) : (
              <button type="button" className="btn btn-primary" onClick={onAssign} disabled={newAssignmentCount === 0}>
                Assign label to {newAssignmentCount} users
              </button>
            )}
          </div>
        </footer>
      </div>
      <button type="button" className="modal-backdrop" onClick={onClose} aria-label="Close assignment dialog" />
    </dialog>
  );
}

export default function SensitivityLabelConcepts() {
  const [selectedLabelId, setSelectedLabelId] = useState("export");
  const [detailTab, setDetailTab] = useState<DetailTab>("assigned-users");
  const [assignedByLabel, setAssignedByLabel] = useState<Record<string, Set<string>>>({
    public: new Set(users.map((user) => user.id)),
    internal: new Set(["jordan", "taylor", "sam", "priya", "chris"]),
    export: new Set(initiallyAssignedUserIds),
    cui: new Set(["alex"]),
  });
  const [wizardOpen, setWizardOpen] = useState(false);
  const [wizardStep, setWizardStep] = useState<WizardStep>("select");
  const [pickerTab, setPickerTab] = useState<PickerTab>("groups");
  const [selectedGroupIds, setSelectedGroupIds] = useState(new Set<string>());
  const [selectedUserIds, setSelectedUserIds] = useState(new Set<string>());
  const [excludedPreviewUserIds, setExcludedPreviewUserIds] = useState(new Set<string>());

  const selectedLabel = labels.find((label) => label.id === selectedLabelId) ?? labels[2];
  const assignedUserIds = assignedByLabel[selectedLabel.id];
  const assignedUsers = users.filter((user) => assignedUserIds.has(user.id));

  const previewUsers = useMemo(() => {
    const ids = new Set(selectedUserIds);
    groups
      .filter((group) => selectedGroupIds.has(group.id))
      .forEach((group) => group.memberIds.forEach((id) => ids.add(id)));
    return users.filter((user) => ids.has(user.id));
  }, [selectedGroupIds, selectedUserIds]);

  useEffect(() => {
    const params = new URLSearchParams(window.location.search);
    const assignmentState = params.get("assign");
    if (assignmentState === "select" || assignmentState === "preview") {
      setSelectedGroupIds(new Set(["engineering"]));
      setSelectedUserIds(new Set(["mia"]));
      setWizardStep(assignmentState);
      setWizardOpen(true);
    }
  }, []);

  const toggleSetValue = (
    value: string,
    setter: Dispatch<SetStateAction<Set<string>>>,
  ) => {
    setter((current) => {
      const next = new Set(current);
      if (next.has(value)) next.delete(value);
      else next.add(value);
      return next;
    });
  };

  const openWizard = () => {
    setSelectedGroupIds(new Set());
    setSelectedUserIds(new Set());
    setExcludedPreviewUserIds(new Set());
    setWizardStep("select");
    setPickerTab("groups");
    setWizardOpen(true);
  };

  const assignPreviewUsers = () => {
    setAssignedByLabel((current) => {
      const next = new Set(current[selectedLabel.id]);
      previewUsers.forEach((user) => {
        if (!excludedPreviewUserIds.has(user.id)) next.add(user.id);
      });
      return { ...current, [selectedLabel.id]: next };
    });
    setWizardOpen(false);
  };

  return (
    <main data-theme="default" className="min-h-screen bg-base-200/30 text-base-content">
      <section className="border-b border-base-300 bg-base-100">
        <div className="mx-auto flex w-full max-w-7xl flex-col gap-5 px-3 py-5 sm:px-6 lg:px-8">
          <div>
            <p className="text-xs font-semibold uppercase tracking-wide text-base-content/60">Project</p>
            <h1 className="text-2xl font-bold sm:text-3xl">Project Management</h1>
            <p className="mt-2 text-base-content/70">Managing settings for project: Load Test Project 20</p>
          </div>
        </div>
      </section>

      <section className="mx-auto w-full max-w-7xl px-3 py-5 sm:px-6 lg:px-8">
        <div role="tablist" className="tabs tabs-border border-b border-base-300">
          {["Users", "Roles & Permissions", "Groups", "Data Sources", "Tags", "Labels", "Settings"].map((tab) => (
            <button
              key={tab}
              type="button"
              role="tab"
              className={`tab ${tab === "Labels" ? "tab-active text-primary" : ""}`}
            >
              {tab}
            </button>
          ))}
        </div>

        <header className="border-b border-base-300 px-4 py-6">
          <h2 className="text-2xl font-bold">Labels</h2>
          <p className="mt-2 text-base-content/70">
            Create project labels, review inherited organization labels, and manage user access in one place.
          </p>
        </header>

        <div id="figma-capture-target" className="grid min-h-[680px] grid-cols-1 gap-5 px-4 py-5 lg:grid-cols-[minmax(300px,.78fr)_minmax(0,1.72fr)]">
          <aside className="card overflow-hidden border border-base-300 bg-base-100 shadow-sm">
            <div className="flex items-start justify-between gap-3 border-b border-base-200 p-4">
              <div>
                <h3 className="font-bold">Labels</h3>
                <p className="text-sm text-base-content/60">{labels.length} Total</p>
              </div>
              <button type="button" className="btn btn-primary btn-sm">
                <PlusIcon className="h-4 w-4" />Create Label
              </button>
            </div>
            <div className="p-4"><SearchField placeholder="Search labels" /></div>
            {labels.map((label) => (
              <button
                key={label.id}
                type="button"
                onClick={() => setSelectedLabelId(label.id)}
                className={`flex w-full items-center gap-3 border-b border-base-200 px-4 py-4 text-left ${
                  selectedLabel.id === label.id
                    ? "border-l-4 border-l-primary bg-base-200"
                    : "border-l-4 border-l-transparent hover:bg-base-200/60"
                }`}
              >
                <span className={`h-2.5 w-2.5 rounded-full ${label.color}`} />
                <span className="min-w-0 flex-1">
                  <strong className="block">{label.name}</strong>
                  <span className="block text-sm text-base-content/60">
                    {label.origin === "Organization" ? "Inherited from organization" : "Created in project"}
                  </span>
                </span>
                <span className="badge badge-sm badge-outline">
                  {label.origin === "Organization" ? "ORG" : "PROJECT"}
                </span>
              </button>
            ))}
          </aside>

          <section className="card border border-base-300 bg-base-100 p-6 shadow-sm">
            <div className="flex flex-wrap items-start justify-between gap-4">
              <div>
                <div className="flex flex-wrap items-center gap-2">
                  <h3 className="text-xl font-bold">{selectedLabel.name}</h3>
                  <span className="badge badge-primary">{selectedLabel.origin} Label</span>
                </div>
                <p className="mt-1 text-sm text-base-content/60">
                  {selectedLabel.origin === "Organization" ? "Inherited from the organization" : "Created in this project"}
                </p>
              </div>
              {selectedLabel.origin === "Project" && <button type="button" className="btn btn-outline btn-primary btn-sm">Edit Label</button>}
            </div>

            <div role="tablist" className="tabs tabs-border mt-6 border-b border-base-200">
              <button
                type="button"
                role="tab"
                onClick={() => setDetailTab("details")}
                className={`tab ${detailTab === "details" ? "tab-active text-primary" : ""}`}
              >
                Details
              </button>
              <button
                type="button"
                role="tab"
                onClick={() => setDetailTab("assigned-users")}
                className={`tab gap-2 ${detailTab === "assigned-users" ? "tab-active text-primary" : ""}`}
              >
                Assigned Users <span className="badge badge-sm">{assignedUsers.length}</span>
              </button>
            </div>

            {detailTab === "details" ? (
              <div className="max-w-3xl py-6">
                <dl className="grid gap-6 sm:grid-cols-2">
                  <div><dt className="text-sm font-bold text-base-content/60">Label name</dt><dd className="mt-1">{selectedLabel.name}</dd></div>
                  <div><dt className="text-sm font-bold text-base-content/60">Origin</dt><dd className="mt-1">{selectedLabel.origin}</dd></div>
                  <div className="sm:col-span-2"><dt className="text-sm font-bold text-base-content/60">Description</dt><dd className="mt-1">{selectedLabel.description}</dd></div>
                  <div className="sm:col-span-2"><dt className="text-sm font-bold text-base-content/60">Access behavior</dt><dd className="mt-1">Only users explicitly assigned this label can access records carrying it.</dd></div>
                </dl>
              </div>
            ) : (
              <div className="py-6">
                <div className="mb-5 flex flex-wrap items-end justify-between gap-4">
                  <div>
                    <h4 className="text-lg font-bold">Assigned users</h4>
                    <p className="mt-1 text-sm text-base-content/65">
                      Assignments are stored against individual users, even when a group was used for bulk selection.
                    </p>
                  </div>
                  <button type="button" className="btn btn-primary" onClick={openWizard}>
                    <PlusIcon className="h-5 w-5" />Assign Users
                  </button>
                </div>
                <SearchField placeholder="Search assigned users" />
                <div className="mt-4 overflow-hidden rounded-box border border-base-200">
                  {assignedUsers.map((user) => (
                    <div key={user.id} className="flex items-center gap-3 border-b border-base-200 px-4 py-4 last:border-b-0">
                      <UserAvatar user={user} />
                      <span className="min-w-0 flex-1">
                        <strong className="block">{user.name}</strong>
                        <span className="block text-sm text-base-content/60">{user.role} · {user.email}</span>
                      </span>
                      <span className="badge badge-outline badge-primary">Assigned</span>
                      <button
                        type="button"
                        className="btn btn-ghost btn-sm text-error"
                        onClick={() => setAssignedByLabel((current) => {
                          const next = new Set(current[selectedLabel.id]);
                          next.delete(user.id);
                          return { ...current, [selectedLabel.id]: next };
                        })}
                        aria-label={`Remove ${selectedLabel.name} from ${user.name}`}
                      >
                        <TrashIcon className="h-5 w-5" />
                      </button>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </section>
        </div>
      </section>

      <AssignmentWizard
        labelName={selectedLabel.name}
        open={wizardOpen}
        step={wizardStep}
        setStep={setWizardStep}
        pickerTab={pickerTab}
        setPickerTab={setPickerTab}
        selectedGroupIds={selectedGroupIds}
        selectedUserIds={selectedUserIds}
        excludedPreviewUserIds={excludedPreviewUserIds}
        assignedUserIds={assignedUserIds}
        onToggleGroup={(id) => toggleSetValue(id, setSelectedGroupIds)}
        onToggleUser={(id) => toggleSetValue(id, setSelectedUserIds)}
        onTogglePreviewUser={(id) => toggleSetValue(id, setExcludedPreviewUserIds)}
        onClose={() => setWizardOpen(false)}
        onAssign={assignPreviewUsers}
      />
    </main>
  );
}
