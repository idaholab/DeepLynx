"use client";

import React, { useMemo, useState } from "react";
import toast from "react-hot-toast";
import {
  UserGroupIcon,
  UserIcon,
  MagnifyingGlassIcon,
  ArrowLeftIcon,
  InformationCircleIcon,
  CheckIcon,
} from "@heroicons/react/24/outline";
import type {
  UserResponseDto,
  GroupResponseDto,
} from "@/app/(home)/types/responseDTOs";
import { getGroupMembers } from "@/app/lib/client_service/group_services.client";
import { grantSensitivityLabelAccessOrg } from "@/app/lib/client_service/sensitivity_labels_services.client";
import AvatarCell from "@/app/(home)/components/Avatar";
import { useLanguage } from "@/app/contexts/Language";

type WizardStep = "select" | "preview";
type PickerTab = "groups" | "users";

interface NormalizedUser {
  id: number;
  name: string;
  email: string;
}

interface Props {
  isOpen: boolean;
  onClose: () => void;
  organizationId: number;
  labelId: number;
  labelName: string;
  members: UserResponseDto[];
  groups: GroupResponseDto[];
  assignedUserIds: Set<number>;
  onAssigned: () => void;
}

const AssignUsersWizardModalOrg: React.FC<Props> = ({
  isOpen,
  onClose,
  organizationId,
  labelId,
  labelName,
  members,
  groups,
  assignedUserIds,
  onAssigned,
}) => {
  const { t } = useLanguage();
  const [step, setStep] = useState<WizardStep>("select");
  const [pickerTab, setPickerTab] = useState<PickerTab>("groups");
  const [search, setSearch] = useState("");
  const [selectedGroupIds, setSelectedGroupIds] = useState<Set<number>>(new Set());
  const [selectedUserIds, setSelectedUserIds] = useState<Set<number>>(new Set());
  const [excludedPreviewUserIds, setExcludedPreviewUserIds] = useState<Set<number>>(new Set());
  const [groupMembersCache, setGroupMembersCache] = useState<Record<number, NormalizedUser[]>>({});
  const [loadingGroupIds, setLoadingGroupIds] = useState<Set<number>>(new Set());
  const [assigning, setAssigning] = useState(false);

  const individualUsers: NormalizedUser[] = useMemo(
    () => members.map((m) => ({ id: m.id, name: m.name, email: m.email })),
    [members],
  );

  const resetState = () => {
    setStep("select");
    setPickerTab("groups");
    setSearch("");
    setSelectedGroupIds(new Set());
    setSelectedUserIds(new Set());
    setExcludedPreviewUserIds(new Set());
  };

  const handleClose = () => {
    resetState();
    onClose();
  };

  const toggleGroup = async (groupId: number) => {
    setSelectedGroupIds((current) => {
      const next = new Set(current);
      if (next.has(groupId)) next.delete(groupId);
      else next.add(groupId);
      return next;
    });

    if (!groupMembersCache[groupId]) {
      setLoadingGroupIds((current) => new Set(current).add(groupId));
      try {
        const res = await getGroupMembers(organizationId, groupId);
        const groupMembers: NormalizedUser[] = res.items.map((u) => ({
          id: u.id,
          name: u.name,
          email: u.email,
        }));
        setGroupMembersCache((current) => ({ ...current, [groupId]: groupMembers }));
      } catch (error) {
        console.error(`Failed to load members for group ${groupId}:`, error);
      } finally {
        setLoadingGroupIds((current) => {
          const next = new Set(current);
          next.delete(groupId);
          return next;
        });
      }
    }
  };

  const toggleUser = (userId: number) => {
    setSelectedUserIds((current) => {
      const next = new Set(current);
      if (next.has(userId)) next.delete(userId);
      else next.add(userId);
      return next;
    });
  };

  const togglePreviewUser = (userId: number) => {
    setExcludedPreviewUserIds((current) => {
      const next = new Set(current);
      if (next.has(userId)) next.delete(userId);
      else next.add(userId);
      return next;
    });
  };

  const previewUsers = useMemo(() => {
    const map = new Map<number, { user: NormalizedUser; viaGroupName?: string }>();
    individualUsers
      .filter((u) => selectedUserIds.has(u.id))
      .forEach((u) => map.set(u.id, { user: u }));
    groups
      .filter((g) => selectedGroupIds.has(g.id as number))
      .forEach((g) => {
        (groupMembersCache[g.id as number] ?? []).forEach((u) => {
          if (!map.has(u.id)) map.set(u.id, { user: u, viaGroupName: g.name });
        });
      });
    return Array.from(map.values());
  }, [individualUsers, selectedUserIds, groups, selectedGroupIds, groupMembersCache]);

  const newAssignmentCount = previewUsers.filter(
    ({ user }) => !assignedUserIds.has(user.id) && !excludedPreviewUserIds.has(user.id),
  ).length;
  const existingCount = previewUsers.filter(({ user }) => assignedUserIds.has(user.id)).length;

  const anySelectedGroupLoading = Array.from(selectedGroupIds).some((id) => loadingGroupIds.has(id));

  const handleConfirm = async () => {
    const idsToGrant = previewUsers
      .filter(({ user }) => !assignedUserIds.has(user.id) && !excludedPreviewUserIds.has(user.id))
      .map(({ user }) => user.id);

    if (idsToGrant.length === 0) return;

    setAssigning(true);
    try {
      const results = await Promise.allSettled(
        idsToGrant.map((userId) => grantSensitivityLabelAccessOrg(organizationId, labelId, userId)),
      );
      const succeeded = results.filter((r) => r.status === "fulfilled").length;
      const failed = results.length - succeeded;

      if (succeeded > 0) {
        toast.success(t.translations.GRANTED_ACCESS_TO_USERS.replace("{count}", String(succeeded)));
      }
      if (failed > 0) {
        toast.error(t.translations.FAILED_TO_GRANT_ACCESS);
      }

      onAssigned();
      handleClose();
    } finally {
      setAssigning(false);
    }
  };

  const normalizedSearch = search.trim().toLowerCase();
  const filteredGroups = normalizedSearch
    ? groups.filter((g) => g.name.toLowerCase().includes(normalizedSearch))
    : groups;
  const filteredUsers = normalizedSearch
    ? individualUsers.filter(
        (u) =>
          u.name.toLowerCase().includes(normalizedSearch) ||
          u.email.toLowerCase().includes(normalizedSearch),
      )
    : individualUsers;

  if (!isOpen) return null;

  return (
    <div className="modal modal-open">
      <div className="modal-box max-w-5xl border border-base-300 p-0">
        <header className="flex items-start justify-between gap-4 border-b border-base-200 px-6 py-5">
          <div>
            <p className="text-xs font-bold uppercase tracking-wide text-base-content/55">
              {labelName}
            </p>
            <h2 className="text-xl font-bold">{t.translations.ASSIGN_LABEL_TO_USERS}</h2>
            <p className="mt-1 text-sm text-base-content/65">
              {t.translations.GROUP_MEMBERSHIP_EXPANSION_NOTE}
            </p>
          </div>
        </header>

        <div className="border-b border-base-200 px-6 py-4">
          <ul className="steps w-full">
            <li className="step step-primary">{t.translations.SELECT_GROUPS_AND_USERS}</li>
            <li className={`step ${step === "preview" ? "step-primary" : ""}`}>
              {t.translations.PREVIEW_USERS}
            </li>
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
                  <UserGroupIcon className="h-4 w-4" />
                  {t.translations.GROUPS}
                </button>
                <button
                  type="button"
                  role="tab"
                  onClick={() => setPickerTab("users")}
                  className={`tab gap-2 ${pickerTab === "users" ? "tab-active text-primary" : ""}`}
                >
                  <UserIcon className="h-4 w-4" />
                  {t.translations.INDIVIDUAL_USERS}
                </button>
              </div>

              <label className="input input-bordered flex w-full items-center gap-2 bg-base-100">
                <MagnifyingGlassIcon className="h-4 w-4 text-base-content/50" />
                <input
                  type="search"
                  className="grow"
                  placeholder={
                    pickerTab === "groups" ? t.translations.SEARCH_GROUPS : t.translations.SEARCH_USERS
                  }
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                />
              </label>

              <div className="mt-4 max-h-[300px] overflow-auto rounded-box border border-base-200">
                {pickerTab === "groups"
                  ? filteredGroups.map((group) => (
                      <label
                        key={group.id}
                        className="flex cursor-pointer items-center gap-3 border-b border-base-200 px-4 py-4 last:border-b-0 hover:bg-base-200/50"
                      >
                        <input
                          type="checkbox"
                          className="checkbox checkbox-primary checkbox-sm"
                          checked={selectedGroupIds.has(group.id as number)}
                          onChange={() => toggleGroup(group.id as number)}
                        />
                        <span className="grid h-9 w-9 place-items-center rounded-full bg-primary/10 text-primary">
                          <UserGroupIcon className="h-5 w-5" />
                        </span>
                        <span className="min-w-0 flex-1">
                          <strong className="block">{group.name}</strong>
                          {group.description && (
                            <span className="block text-sm text-base-content/60">
                              {group.description}
                            </span>
                          )}
                        </span>
                        {loadingGroupIds.has(group.id as number) && (
                          <span className="loading loading-spinner loading-xs" />
                        )}
                        {typeof group.memberCount === "number" && (
                          <span className="badge badge-outline">{group.memberCount}</span>
                        )}
                      </label>
                    ))
                  : filteredUsers.map((user) => (
                      <label
                        key={user.id}
                        className="flex cursor-pointer items-center gap-3 border-b border-base-200 px-4 py-4 last:border-b-0 hover:bg-base-200/50"
                      >
                        <input
                          type="checkbox"
                          className="checkbox checkbox-primary checkbox-sm"
                          checked={selectedUserIds.has(user.id)}
                          onChange={() => toggleUser(user.id)}
                        />
                        <AvatarCell name={user.name} size={9} containerClassName="space-x-0" />
                        <span className="min-w-0 flex-1">
                          <strong className="block">{user.name}</strong>
                          <span className="block text-sm text-base-content/60">{user.email}</span>
                        </span>
                      </label>
                    ))}
              </div>
            </section>

            <aside className="bg-base-200/35 p-6">
              <h3 className="font-bold">{t.translations.CURRENT_SELECTION}</h3>
              <div className="mt-5 space-y-5">
                <div>
                  <p className="mb-2 text-xs font-bold uppercase tracking-wide text-base-content/55">
                    {t.translations.GROUPS}
                  </p>
                  <div className="flex flex-wrap gap-2">
                    {selectedGroupIds.size === 0 && (
                      <span className="text-sm text-base-content/50">{t.translations.NONE_SELECTED}</span>
                    )}
                    {groups
                      .filter((g) => selectedGroupIds.has(g.id as number))
                      .map((g) => (
                        <span key={g.id} className="badge badge-primary gap-1">
                          {g.name}
                        </span>
                      ))}
                  </div>
                </div>
                <div>
                  <p className="mb-2 text-xs font-bold uppercase tracking-wide text-base-content/55">
                    {t.translations.INDIVIDUAL_USERS}
                  </p>
                  <div className="flex flex-wrap gap-2">
                    {selectedUserIds.size === 0 && (
                      <span className="text-sm text-base-content/50">{t.translations.NONE_SELECTED}</span>
                    )}
                    {individualUsers
                      .filter((u) => selectedUserIds.has(u.id))
                      .map((u) => (
                        <span key={u.id} className="badge badge-outline badge-primary">
                          {u.name}
                        </span>
                      ))}
                  </div>
                </div>
              </div>
              <div className="alert alert-info mt-6 items-start">
                <InformationCircleIcon className="h-5 w-5 shrink-0" />
                <span className="text-sm">{t.translations.GROUP_MEMBERSHIP_EXPANSION_NOTE}</span>
              </div>
            </aside>
          </div>
        ) : (
          <section className="p-6">
            <div className="mb-4 flex flex-wrap items-end justify-between gap-4">
              <div className="text-right text-sm">
                <strong className="block">
                  {t.translations.NEW_USERS_WILL_RECEIVE_LABEL.replace("{count}", String(newAssignmentCount))}
                </strong>
                <span className="text-base-content/60">
                  {t.translations.ALREADY_ASSIGNED_COUNT.replace("{count}", String(existingCount))} ·{" "}
                  {t.translations.DESELECTED_COUNT.replace(
                    "{count}",
                    String(excludedPreviewUserIds.size),
                  )}
                </span>
              </div>
            </div>

            <div className="max-h-[390px] overflow-auto rounded-box border border-base-200">
              {previewUsers.map(({ user, viaGroupName }) => {
                const alreadyAssigned = assignedUserIds.has(user.id);
                const excluded = excludedPreviewUserIds.has(user.id);
                return (
                  <label
                    key={user.id}
                    className={`flex items-center gap-3 border-b border-base-200 px-4 py-4 last:border-b-0 ${
                      excluded ? "opacity-55" : ""
                    }`}
                  >
                    <input
                      type="checkbox"
                      className="checkbox checkbox-primary checkbox-sm"
                      checked={alreadyAssigned || !excluded}
                      disabled={alreadyAssigned}
                      onChange={() => togglePreviewUser(user.id)}
                    />
                    <AvatarCell name={user.name} size={9} containerClassName="space-x-0" />
                    <span className="min-w-0 flex-1">
                      <strong className="block">{user.name}</strong>
                      <span className="block text-sm text-base-content/60">{user.email}</span>
                    </span>
                    <span className="text-sm text-base-content/60">
                      {viaGroupName
                        ? t.translations.VIA_GROUP.replace("{group}", viaGroupName)
                        : t.translations.SELECTED_INDIVIDUALLY}
                    </span>
                    {alreadyAssigned && (
                      <span className="badge badge-success gap-1">
                        <CheckIcon className="h-3.5 w-3.5" />
                        {t.translations.ALREADY_ASSIGNED}
                      </span>
                    )}
                    {!alreadyAssigned && excluded && (
                      <span className="badge badge-ghost">{t.translations.DESELECTED}</span>
                    )}
                  </label>
                );
              })}
            </div>
          </section>
        )}

        <footer className="flex flex-wrap items-center justify-between gap-3 border-t border-base-200 px-6 py-4">
          {step === "preview" ? (
            <button type="button" className="btn btn-ghost" onClick={() => setStep("select")}>
              <ArrowLeftIcon className="h-4 w-4" />
              {t.translations.BACK}
            </button>
          ) : (
            <span />
          )}
          <div className="flex gap-3">
            <button type="button" className="btn btn-ghost" onClick={handleClose}>
              {t.translations.CANCEL}
            </button>
            {step === "select" ? (
              <button
                type="button"
                className="btn btn-primary"
                onClick={() => setStep("preview")}
                disabled={previewUsers.length === 0 || anySelectedGroupLoading}
              >
                {t.translations.PREVIEW_USERS_COUNT.replace("{count}", String(previewUsers.length))}
              </button>
            ) : (
              <button
                type="button"
                className="btn btn-primary"
                onClick={handleConfirm}
                disabled={newAssignmentCount === 0 || assigning}
              >
                {assigning
                  ? t.translations.SAVING
                  : t.translations.ASSIGN_LABEL_TO_N_USERS.replace(
                      "{count}",
                      String(newAssignmentCount),
                    )}
              </button>
            )}
          </div>
        </footer>
      </div>
      <button
        type="button"
        className="modal-backdrop"
        onClick={handleClose}
        aria-label={t.translations.CANCEL}
      />
    </div>
  );
};

export default AssignUsersWizardModalOrg;
