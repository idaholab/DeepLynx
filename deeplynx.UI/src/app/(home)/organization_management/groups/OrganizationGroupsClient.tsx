// src/app/(home)/organization_management/groups/OrganizationGroupsClient.tsx
"use client";

import React, { useEffect, useMemo, useState } from "react";
import toast from "react-hot-toast";
import {
  MagnifyingGlassIcon,
  PlusIcon,
  UserGroupIcon,
  TrashIcon,
  ShieldCheckIcon,
} from "@heroicons/react/24/outline";
import type {
  GroupResponseDto,
  UserResponseDto,
  SensitivityLabelsDto,
} from "@/app/(home)/types/responseDTOs";
import type {
  CreateGroupRequestDto,
  UpdateGroupRequestDto,
} from "@/app/(home)/types/requestDTOs";
import {
  addUserToGroup,
  archiveGroup,
  createGroup,
  getAllGroups,
  getGroupMembers,
  removeUserFromGroup,
  updateGroup,
} from "@/app/lib/client_service/group_services.client";
import {
  getAllSensitivityLabelsOrg,
  getGroupsWithAccessToLabelOrg,
  grantSensitivityLabelAccessToGroupOrg,
  revokeSensitivityLabelAccessFromGroupOrg,
} from "@/app/lib/client_service/sensitivity_labels_services.client";
import AvatarCell from "@/app/(home)/components/Avatar";
import GroupEditModal from "./GroupEditModal";
import GroupArchiveModal from "./GroupArchiveModal";
import GroupLabelGrantModal from "./GroupLabelGrantModal";
import GroupMemberLabelWarningModal from "./GroupMemberLabelWarningModal";
import { useLanguage } from "@/app/contexts/Language";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";

type DetailTab = "members" | "labels";

interface Props {
  initialGroups: GroupResponseDto[];
  members: UserResponseDto[];
  labels: SensitivityLabelsDto[];
}

function GroupMembersPanel({
  group,
  organizationId,
  availableUsers,
  groupLabels,
  onMemberCountChange,
}: {
  group: GroupResponseDto;
  organizationId: number;
  availableUsers: UserResponseDto[];
  groupLabels: SensitivityLabelsDto[];
  onMemberCountChange: (groupId: number | string, count: number) => void;
}) {
  const { t } = useLanguage();
  const [members, setMembers] = useState<UserResponseDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [memberSearch, setMemberSearch] = useState("");
  const [search, setSearch] = useState("");
  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set());
  const [addingSelected, setAddingSelected] = useState(false);
  const [showLabelWarning, setShowLabelWarning] = useState(false);
  const [removeSelectedIds, setRemoveSelectedIds] = useState<Set<number>>(
    new Set(),
  );
  const [removingSelected, setRemovingSelected] = useState(false);
  const [showRemoveWarning, setShowRemoveWarning] = useState(false);

  const loadMembers = async () => {
    try {
      setLoading(true);
      const res = await getGroupMembers(organizationId, group.id as number);
      setMembers(res.items);
      onMemberCountChange(group.id, res.totalCount);
    } catch (error) {
      console.error("Failed to load group members:", error);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadMembers();
    setMemberSearch("");
    setSearch("");
    setSelectedIds(new Set());
    setShowLabelWarning(false);
    setRemoveSelectedIds(new Set());
    setShowRemoveWarning(false);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [group.id]);

  const toggleSelected = (userId: number) => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(userId)) next.delete(userId);
      else next.add(userId);
      return next;
    });
  };

  const handleAddSelectedClick = () => {
    if (selectedIds.size === 0) return;
    // If the group carries sensitivity labels, warn that new members inherit them.
    if (groupLabels.length > 0) {
      setShowLabelWarning(true);
      return;
    }
    void performAddSelected();
  };

  const performAddSelected = async () => {
    if (selectedIds.size === 0) return;
    const ids = Array.from(selectedIds);
    try {
      setAddingSelected(true);
      const results = await Promise.allSettled(
        ids.map((userId) =>
          addUserToGroup(organizationId, group.id as number, userId),
        ),
      );
      const failed = results.filter((r) => r.status === "rejected").length;
      await loadMembers();
      setSelectedIds(new Set());
      setShowLabelWarning(false);
      if (failed > 0) {
        toast.error(t.translations.FAILED_TO_ADD_MEMBER);
      } else {
        toast.success(t.translations.MEMBERS_ADDED);
      }
    } catch (error) {
      console.error("Failed to add selected members:", error);
      toast.error(t.translations.FAILED_TO_ADD_MEMBER);
    } finally {
      setAddingSelected(false);
    }
  };

  const toggleRemoveSelected = (userId: number) => {
    setRemoveSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(userId)) next.delete(userId);
      else next.add(userId);
      return next;
    });
  };

  const handleRemoveSelectedClick = () => {
    if (removeSelectedIds.size === 0) return;
    // If the group carries sensitivity labels, warn that removed members lose them.
    if (groupLabels.length > 0) {
      setShowRemoveWarning(true);
      return;
    }
    void performRemoveSelected();
  };

  const performRemoveSelected = async () => {
    if (removeSelectedIds.size === 0) return;
    const ids = Array.from(removeSelectedIds);
    try {
      setRemovingSelected(true);
      const results = await Promise.allSettled(
        ids.map((userId) =>
          removeUserFromGroup(organizationId, group.id as number, userId),
        ),
      );
      const failed = results.filter((r) => r.status === "rejected").length;
      await loadMembers();
      setRemoveSelectedIds(new Set());
      setShowRemoveWarning(false);
      if (failed > 0) {
        toast.error(t.translations.FAILED_TO_REMOVE_MEMBER);
      } else {
        toast.success(t.translations.MEMBERS_REMOVED);
      }
    } catch (error) {
      console.error("Failed to remove selected members:", error);
      toast.error(t.translations.FAILED_TO_REMOVE_MEMBER);
    } finally {
      setRemovingSelected(false);
    }
  };

  const memberIds = new Set(members.map((m) => m.id));
  const normalizedMemberSearch = memberSearch.trim().toLowerCase();
  const filteredMembers = normalizedMemberSearch
    ? members.filter(
        (member) =>
          member.name.toLowerCase().includes(normalizedMemberSearch) ||
          member.email.toLowerCase().includes(normalizedMemberSearch),
      )
    : members;
  const normalizedSearch = search.trim().toLowerCase();
  const candidates = availableUsers.filter(
    (u) => !memberIds.has(u.id) && !u.isArchived,
  );
  const filteredCandidates = normalizedSearch
    ? candidates.filter(
        (u) =>
          u.name?.toLowerCase().includes(normalizedSearch) ||
          u.email?.toLowerCase().includes(normalizedSearch),
      )
    : candidates;

  const allFilteredSelected =
    filteredCandidates.length > 0 &&
    filteredCandidates.every((u) => selectedIds.has(u.id));

  const toggleSelectAll = () => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (allFilteredSelected) {
        filteredCandidates.forEach((u) => next.delete(u.id));
      } else {
        filteredCandidates.forEach((u) => next.add(u.id));
      }
      return next;
    });
  };

  const allMembersSelected =
    filteredMembers.length > 0 &&
    filteredMembers.every((u) => removeSelectedIds.has(u.id));

  const toggleRemoveSelectAll = () => {
    setRemoveSelectedIds((prev) => {
      const next = new Set(prev);
      if (allMembersSelected) {
        filteredMembers.forEach((u) => next.delete(u.id));
      } else {
        filteredMembers.forEach((u) => next.add(u.id));
      }
      return next;
    });
  };

  return (
    <div className="grid grid-cols-1 gap-6 py-6 lg:grid-cols-2">
      <div>
        <div className="mb-3 flex items-center justify-between gap-2">
          <p className="text-xs font-bold uppercase tracking-wide text-base-content/55">
            {t.translations.CURRENT_MEMBERS} ({members.length})
          </p>
          <button
            type="button"
            className="btn btn-error btn-sm gap-2"
            onClick={handleRemoveSelectedClick}
            disabled={removeSelectedIds.size === 0 || removingSelected}
          >
            {removingSelected ? (
              <span className="loading loading-spinner loading-xs" />
            ) : (
              <TrashIcon className="h-4 w-4" />
            )}
            {t.translations.REMOVE_SELECTED}
            {removeSelectedIds.size > 0 ? ` (${removeSelectedIds.size})` : ""}
          </button>
        </div>
        <label className="input input-bordered input-sm mb-3 flex w-full items-center gap-2 bg-base-100">
          <MagnifyingGlassIcon className="h-4 w-4 text-base-content/50" />
          <input
            type="search"
            className="grow"
            placeholder={t.translations.SEARCH_MEMBERS}
            value={memberSearch}
            onChange={(e) => setMemberSearch(e.target.value)}
          />
        </label>
        {loading ? (
          <div className="flex justify-center py-10">
            <span className="loading loading-spinner loading-md" />
          </div>
        ) : filteredMembers.length === 0 ? (
          <p className="py-8 text-center text-sm text-base-content/60">
            {t.translations.NO_MEMBERS}
          </p>
        ) : (
          <div className="max-h-[520px] overflow-auto rounded-box border border-base-200">
            <label className="flex cursor-pointer items-center gap-3 border-b border-base-200 bg-base-200/40 px-4 py-2">
              <input
                type="checkbox"
                className="checkbox checkbox-primary checkbox-sm"
                checked={allMembersSelected}
                onChange={toggleRemoveSelectAll}
              />
              <span className="text-xs font-semibold uppercase tracking-wide text-base-content/55">
                {t.translations.SELECT_ALL}
              </span>
            </label>
            {filteredMembers.map((u) => (
              <label
                key={u.id}
                className="flex cursor-pointer items-center gap-3 border-b border-base-200 px-4 py-3 last:border-b-0 hover:bg-base-200/50"
              >
                <input
                  type="checkbox"
                  className="checkbox checkbox-primary checkbox-sm"
                  checked={removeSelectedIds.has(u.id)}
                  onChange={() => toggleRemoveSelected(u.id)}
                />
                <AvatarCell name={u.name} size={9} containerClassName="space-x-0" />
                <span className="min-w-0 flex-1">
                  <strong className="block">{u.name}</strong>
                  <span className="block text-sm text-base-content/60">{u.email}</span>
                </span>
              </label>
            ))}
          </div>
        )}
      </div>

      <div>
        <div className="mb-3 flex items-center justify-between gap-2">
          <p className="text-xs font-bold uppercase tracking-wide text-base-content/55">
            {t.translations.ADD_MEMBERS}
          </p>
          <button
            type="button"
            className="btn btn-primary btn-sm gap-2"
            onClick={handleAddSelectedClick}
            disabled={selectedIds.size === 0 || addingSelected}
          >
            {addingSelected ? (
              <span className="loading loading-spinner loading-xs" />
            ) : (
              <PlusIcon className="h-4 w-4" />
            )}
            {t.translations.ADD_SELECTED}
            {selectedIds.size > 0 ? ` (${selectedIds.size})` : ""}
          </button>
        </div>
        <label className="input input-bordered input-sm mb-3 flex w-full items-center gap-2 bg-base-100">
          <MagnifyingGlassIcon className="h-4 w-4 text-base-content/50" />
          <input
            type="search"
            className="grow"
            placeholder={t.translations.SEARCH_MEMBERS}
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </label>
        {filteredCandidates.length === 0 ? (
          <p className="py-8 text-center text-sm text-base-content/60">
            {t.translations.NO_AVAILABLE_USERS}
          </p>
        ) : (
          <div className="max-h-[520px] overflow-auto rounded-box border border-base-200">
            <label className="flex cursor-pointer items-center gap-3 border-b border-base-200 bg-base-200/40 px-4 py-2">
              <input
                type="checkbox"
                className="checkbox checkbox-primary checkbox-sm"
                checked={allFilteredSelected}
                onChange={toggleSelectAll}
              />
              <span className="text-xs font-semibold uppercase tracking-wide text-base-content/55">
                {t.translations.SELECT_ALL}
              </span>
            </label>
            {filteredCandidates.map((u) => (
              <label
                key={u.id}
                className="flex cursor-pointer items-center gap-3 border-b border-base-200 px-4 py-3 last:border-b-0 hover:bg-base-200/50"
              >
                <input
                  type="checkbox"
                  className="checkbox checkbox-primary checkbox-sm"
                  checked={selectedIds.has(u.id)}
                  onChange={() => toggleSelected(u.id)}
                />
                <AvatarCell name={u.name} size={9} containerClassName="space-x-0" />
                <span className="min-w-0 flex-1">
                  <strong className="block">{u.name}</strong>
                  <span className="block text-sm text-base-content/60">{u.email}</span>
                </span>
              </label>
            ))}
          </div>
        )}
      </div>

      <GroupMemberLabelWarningModal
        isOpen={showLabelWarning}
        mode="add"
        groupName={group.name}
        memberCount={selectedIds.size}
        labels={groupLabels}
        loading={addingSelected}
        onClose={() => setShowLabelWarning(false)}
        onConfirm={performAddSelected}
      />

      <GroupMemberLabelWarningModal
        isOpen={showRemoveWarning}
        mode="remove"
        groupName={group.name}
        memberCount={removeSelectedIds.size}
        labels={groupLabels}
        loading={removingSelected}
        onClose={() => setShowRemoveWarning(false)}
        onConfirm={performRemoveSelected}
      />
    </div>
  );
}

function GroupLabelsPanel({
  group,
  organizationId,
  labels,
  grantedLabelIds,
  grantsLoading,
  onGranted,
  onRevoked,
}: {
  group: GroupResponseDto;
  organizationId: number;
  labels: SensitivityLabelsDto[];
  grantedLabelIds: Set<number>;
  grantsLoading: boolean;
  onGranted: (labelId: number) => void;
  onRevoked: (labelId: number) => void;
}) {
  const { t } = useLanguage();
  const [busyLabelId, setBusyLabelId] = useState<number | null>(null);
  const [search, setSearch] = useState("");

  const [pendingGrantLabel, setPendingGrantLabel] =
    useState<SensitivityLabelsDto | null>(null);
  const [grantMemberCount, setGrantMemberCount] = useState<number | undefined>(
    undefined,
  );
  const [grantSaving, setGrantSaving] = useState(false);

  useEffect(() => {
    setSearch("");
  }, [group.id]);

  const handleToggle = (label: SensitivityLabelsDto, currentlyGranted: boolean) => {
    if (currentlyGranted) {
      handleRevoke(label.id);
    } else {
      openGrantModal(label);
    }
  };

  const handleRevoke = async (labelId: number) => {
    try {
      setBusyLabelId(labelId);
      await revokeSensitivityLabelAccessFromGroupOrg(
        organizationId,
        labelId,
        group.id as number,
      );
      onRevoked(labelId);
      toast.success(t.translations.GROUP_LABEL_REVOKED);
    } catch (error) {
      console.error(`Failed to revoke label ${labelId} for group:`, error);
      toast.error(t.translations.FAILED_TO_UPDATE_GROUP_LABEL);
    } finally {
      setBusyLabelId(null);
    }
  };

  const openGrantModal = async (label: SensitivityLabelsDto) => {
    setPendingGrantLabel(label);
    setGrantMemberCount(group.memberCount);
    try {
      const res = await getGroupMembers(organizationId, group.id as number, 1, 1);
      setGrantMemberCount(res.totalCount);
    } catch (error) {
      console.error("Failed to load group member count:", error);
    }
  };

  const confirmGrant = async () => {
    if (!pendingGrantLabel) return;
    const labelId = pendingGrantLabel.id;
    try {
      setGrantSaving(true);
      await grantSensitivityLabelAccessToGroupOrg(
        organizationId,
        labelId,
        group.id as number,
      );
      onGranted(labelId);
      toast.success(t.translations.GROUP_LABEL_GRANTED);
      setPendingGrantLabel(null);
    } catch (error) {
      console.error(`Failed to grant label ${labelId} for group:`, error);
      toast.error(t.translations.FAILED_TO_UPDATE_GROUP_LABEL);
    } finally {
      setGrantSaving(false);
    }
  };

  const normalizedSearch = search.trim().toLowerCase();
  const filteredLabels = normalizedSearch
    ? labels.filter(
        (l) =>
          l.name.toLowerCase().includes(normalizedSearch) ||
          l.description?.toLowerCase().includes(normalizedSearch),
      )
    : labels;

  return (
    <div className="py-6">
      <div className="alert alert-info mb-5 items-start">
        <ShieldCheckIcon className="h-5 w-5 shrink-0" />
        <span className="text-sm">{t.translations.GROUP_LABELS_GRANT_NOTE}</span>
      </div>

      <label className="input input-bordered input-sm mb-4 flex w-full max-w-xs items-center gap-2 bg-base-100">
        <MagnifyingGlassIcon className="h-4 w-4 text-base-content/50" />
        <input
          type="search"
          className="grow"
          placeholder={t.translations.SEARCH_LABELS}
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
      </label>

      {grantsLoading ? (
        <div className="flex justify-center py-10">
          <span className="loading loading-spinner loading-md" />
        </div>
      ) : filteredLabels.length === 0 ? (
        <p className="py-8 text-center text-sm text-base-content/60">
          {t.translations.NO_LABELS_AVAILABLE}
        </p>
      ) : (
        <div className="overflow-hidden rounded-box border border-base-200">
          {filteredLabels.map((label) => {
            const granted = grantedLabelIds.has(label.id);
            return (
              <label
                key={label.id}
                className="flex cursor-pointer items-center gap-3 border-b border-base-200 px-4 py-3 last:border-b-0 hover:bg-base-200/50"
              >
                <input
                  type="checkbox"
                  className="checkbox checkbox-primary checkbox-sm"
                  checked={granted}
                  disabled={busyLabelId === label.id}
                  onChange={() => handleToggle(label, granted)}
                />
                <ShieldCheckIcon className="h-5 w-5 shrink-0 text-secondary" />
                <span className="min-w-0 flex-1">
                  <strong className="block">{label.name}</strong>
                  {label.description && (
                    <span className="block text-sm text-base-content/60">
                      {label.description}
                    </span>
                  )}
                </span>
                {busyLabelId === label.id && (
                  <span className="loading loading-spinner loading-xs" />
                )}
              </label>
            );
          })}
        </div>
      )}

      <GroupLabelGrantModal
        isOpen={!!pendingGrantLabel}
        labelName={pendingGrantLabel?.name ?? ""}
        groupName={group.name}
        memberCount={grantMemberCount}
        loading={grantSaving}
        onClose={() => setPendingGrantLabel(null)}
        onConfirm={confirmGrant}
      />
    </div>
  );
}

const OrganizationGroupsClient: React.FC<Props> = ({
  initialGroups,
  members,
  labels,
}) => {
  const { t } = useLanguage();
  const { organization } = useOrganizationSession();
  const orgId = organization?.organizationId as number | undefined;

  const [localGroups, setLocalGroups] =
    useState<GroupResponseDto[]>(initialGroups);
  const [verifiedMemberCountGroupIds, setVerifiedMemberCountGroupIds] = useState<
    Set<number | string>
  >(new Set());
  const [localLabels, setLocalLabels] = useState<SensitivityLabelsDto[]>(labels);
  const [selectedGroupId, setSelectedGroupId] = useState<number | string | null>(
    initialGroups[0]?.id ?? null,
  );
  const [detailTab, setDetailTab] = useState<DetailTab>("members");
  const [groupSearch, setGroupSearch] = useState("");

  const [grantedLabelIds, setGrantedLabelIds] = useState<Set<number>>(new Set());
  const [grantsLoading, setGrantsLoading] = useState(false);

  const [isGroupModalOpen, setIsGroupModalOpen] = useState(false);
  const [editingGroup, setEditingGroup] = useState<GroupResponseDto | null>(null);
  const [groupNameInput, setGroupNameInput] = useState("");
  const [groupDescriptionInput, setGroupDescriptionInput] = useState("");
  const [savingGroup, setSavingGroup] = useState(false);

  const [showArchiveModal, setShowArchiveModal] = useState(false);
  const [groupToArchive, setGroupToArchive] = useState<GroupResponseDto | null>(
    null,
  );
  const [archivingGroupId, setArchivingGroupId] = useState<number | string | null>(
    null,
  );

  const loadGroups = async () => {
    if (!orgId) return;
    try {
      const fresh = await getAllGroups(orgId);
      const memberCounts = await Promise.all(
        fresh.items.map(async (group) => {
          try {
            const members = await getGroupMembers(orgId, group.id as number, 1, 1);
            return [group.id, members.totalCount] as const;
          } catch (error) {
            console.error(`Failed to load member count for group ${group.id}:`, error);
            return null;
          }
        }),
      );
      const resolvedCounts = new Map(
        memberCounts.filter(
          (count): count is readonly [number | string, number] => count !== null,
        ),
      );

      setLocalGroups(
        fresh.items.map((group) => ({
          ...group,
          memberCount: resolvedCounts.get(group.id) ?? group.memberCount,
        })),
      );
      setVerifiedMemberCountGroupIds(new Set(resolvedCounts.keys()));
    } catch (error) {
      console.error("Failed to load groups:", error);
    }
  };

  const loadLabels = async () => {
    if (!orgId) return;
    try {
      const fresh = await getAllSensitivityLabelsOrg(orgId);
      setLocalLabels(fresh);
    } catch (error) {
      console.error("Failed to load organization sensitivity labels:", error);
    }
  };

  useEffect(() => {
    loadGroups();
    loadLabels();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [orgId]);

  const normalizedSearch = groupSearch.trim().toLowerCase();
  const filteredGroups = useMemo(
    () =>
      normalizedSearch
        ? localGroups.filter(
            (g) =>
              g.name.toLowerCase().includes(normalizedSearch) ||
              g.description?.toLowerCase().includes(normalizedSearch),
          )
        : localGroups,
    [localGroups, normalizedSearch],
  );

  const selectedGroup =
    localGroups.find((g) => g.id === selectedGroupId) ?? localGroups[0] ?? null;

  const loadGrantedLabels = async (groupId: number | string) => {
    if (!orgId || localLabels.length === 0) {
      setGrantedLabelIds(new Set());
      return;
    }
    try {
      setGrantsLoading(true);
      const results = await Promise.all(
        localLabels.map((label) =>
          getGroupsWithAccessToLabelOrg(orgId, label.id)
            .then((grants) => ({ labelId: label.id, grants }))
            .catch(() => ({ labelId: label.id, grants: [] })),
        ),
      );
      const granted = new Set<number>();
      results.forEach(({ labelId, grants }) => {
        if (grants.some((g) => g.groupId === groupId)) granted.add(labelId);
      });
      setGrantedLabelIds(granted);
    } catch (error) {
      console.error("Failed to load group label grants:", error);
    } finally {
      setGrantsLoading(false);
    }
  };

  useEffect(() => {
    if (selectedGroup) {
      loadGrantedLabels(selectedGroup.id);
    } else {
      setGrantedLabelIds(new Set());
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedGroup?.id, localLabels]);

  const grantedLabels = useMemo(
    () => localLabels.filter((l) => grantedLabelIds.has(l.id)),
    [localLabels, grantedLabelIds],
  );

  const handleMemberCountChange = (
    groupId: number | string,
    count: number,
  ) => {
    setLocalGroups((prev) =>
      prev.map((g) => (g.id === groupId ? { ...g, memberCount: count } : g)),
    );
    setVerifiedMemberCountGroupIds((prev) => new Set(prev).add(groupId));
  };

  const openCreateGroupModal = () => {
    setEditingGroup(null);
    setGroupNameInput("");
    setGroupDescriptionInput("");
    setIsGroupModalOpen(true);
  };

  const openEditGroupModal = (group: GroupResponseDto) => {
    setEditingGroup(group);
    setGroupNameInput(group.name);
    setGroupDescriptionInput(group.description ?? "");
    setIsGroupModalOpen(true);
  };

  const closeGroupModal = () => {
    setIsGroupModalOpen(false);
    setEditingGroup(null);
    setGroupNameInput("");
    setGroupDescriptionInput("");
    setSavingGroup(false);
  };

  const handleSaveGroup = async () => {
    if (!groupNameInput.trim() || !orgId) return;

    try {
      setSavingGroup(true);
      if (editingGroup) {
        const dto: UpdateGroupRequestDto = {
          name: groupNameInput.trim(),
          description: groupDescriptionInput.trim(),
        };
        const updated = await updateGroup(orgId, editingGroup.id as number, dto);
        setLocalGroups((prev) =>
          prev.map((g) => (g.id === updated.id ? { ...g, ...updated } : g)),
        );
        toast.success(t.translations.GROUP_UPDATED);
      } else {
        const dto: CreateGroupRequestDto = {
          name: groupNameInput.trim(),
          description: groupDescriptionInput.trim(),
        };
        const created = await createGroup(orgId, dto);
        setLocalGroups((prev) => [...prev, created]);
        setSelectedGroupId(created.id);
        toast.success(t.translations.GROUP_CREATED);
      }
      closeGroupModal();
    } catch (error) {
      console.error("Failed to save group:", error);
      toast.error(t.translations.FAILED_TO_SAVE_GROUP);
    } finally {
      setSavingGroup(false);
    }
  };

  const openArchiveModal = (group: GroupResponseDto) => {
    setGroupToArchive(group);
    setShowArchiveModal(true);
  };

  const confirmArchiveGroup = async () => {
    if (!groupToArchive || !orgId) return;
    try {
      setArchivingGroupId(groupToArchive.id);
      await archiveGroup(orgId, groupToArchive.id as number, true);
      setLocalGroups((prev) => prev.filter((g) => g.id !== groupToArchive.id));
      toast.success(t.translations.GROUP_ARCHIVED);
    } catch (error) {
      console.error("Failed to archive group:", error);
      toast.error(t.translations.FAILED_TO_ARCHIVE_GROUPS);
    } finally {
      setArchivingGroupId(null);
      setShowArchiveModal(false);
      setGroupToArchive(null);
    }
  };

  return (
    <div className="grid grid-cols-1 gap-5 p-4 lg:min-h-[75vh] lg:grid-cols-[minmax(280px,.78fr)_minmax(0,1.72fr)]">
      <aside className="card overflow-hidden border border-base-300/50 bg-base-100 shadow-sm">
        <div className="flex items-start justify-between gap-3 border-b border-base-200 p-4">
          <div>
            <h3 className="font-bold">{t.translations.GROUPS}</h3>
            <p className="text-sm text-base-content/60">
              {localGroups.length} {t.translations.TOTAL}
            </p>
          </div>
          <button
            type="button"
            className="btn btn-primary btn-sm"
            onClick={openCreateGroupModal}
            disabled={!orgId}
          >
            <PlusIcon className="h-4 w-4" />
            {t.translations.NEW_GROUP}
          </button>
        </div>
        <div className="p-4">
          <label className="input input-bordered input-sm flex w-full items-center gap-2 bg-base-100">
            <MagnifyingGlassIcon className="h-4 w-4 text-base-content/50" />
            <input
              type="search"
              className="grow"
              placeholder={t.translations.SEARCH_GROUPS}
              value={groupSearch}
              onChange={(e) => setGroupSearch(e.target.value)}
            />
          </label>
        </div>
        {filteredGroups.map((group) => (
          <button
            key={group.id}
            type="button"
            onClick={() => {
              setSelectedGroupId(group.id);
              setDetailTab("members");
            }}
            className={`flex w-full items-center gap-3 border-b border-base-200 px-4 py-4 text-left ${
              selectedGroup?.id === group.id
                ? "border-l-4 border-l-primary bg-base-200"
                : "border-l-4 border-l-transparent hover:bg-base-200/60"
            }`}
          >
            <span className="min-w-0 flex-1">
              <strong className="block">{group.name}</strong>
              {group.description && (
                <span className="block truncate text-sm text-base-content/60">
                  {group.description}
                </span>
              )}
            </span>
            {verifiedMemberCountGroupIds.has(group.id) &&
              typeof group.memberCount === "number" && (
              <span className="badge badge-primary">{group.memberCount}</span>
            )}
          </button>
        ))}
      </aside>

      {selectedGroup ? (
        <section className="card border border-base-300/50 bg-base-100 p-6 shadow-sm">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div className="min-w-0">
              <div className="flex flex-wrap items-center gap-2">
                <UserGroupIcon className="h-5 w-5 text-secondary" />
                <h3 className="text-xl font-bold">{selectedGroup.name}</h3>
              </div>
              {grantedLabels.length > 0 && (
                <div className="mt-2 flex flex-wrap items-center gap-1.5">
                  {grantedLabels.map((label) => (
                    <span
                      key={label.id}
                      className="badge badge-secondary gap-1"
                      title={label.description ?? undefined}
                    >
                      <ShieldCheckIcon className="h-3 w-3" />
                      {label.name}
                    </span>
                  ))}
                </div>
              )}
              {selectedGroup.description && (
                <p className="mt-1 text-sm text-base-content/65">
                  {selectedGroup.description}
                </p>
              )}
            </div>
            <div className="flex gap-2">
              <button
                type="button"
                className="btn btn-outline btn-primary btn-sm"
                onClick={() => openEditGroupModal(selectedGroup)}
              >
                {t.translations.EDIT}
              </button>
              <button
                type="button"
                className="btn btn-outline btn-error btn-sm"
                onClick={() => openArchiveModal(selectedGroup)}
                disabled={archivingGroupId === selectedGroup.id}
              >
                {archivingGroupId === selectedGroup.id
                  ? t.translations.ARCHIVING
                  : t.translations.DELETE}
              </button>
            </div>
          </div>

          <div role="tablist" className="tabs tabs-border mt-6 border-b border-base-200">
            <button
              type="button"
              role="tab"
              onClick={() => setDetailTab("members")}
              className={`tab ${detailTab === "members" ? "tab-active text-primary" : ""}`}
            >
              {t.translations.CURRENT_MEMBERS}
            </button>
            <button
              type="button"
              role="tab"
              onClick={() => setDetailTab("labels")}
              className={`tab ${detailTab === "labels" ? "tab-active text-primary" : ""}`}
            >
              {t.translations.SENSITIVITY_LABELS}
            </button>
          </div>

          {orgId ? (
            detailTab === "labels" ? (
              <GroupLabelsPanel
                key={`${selectedGroup.id}-labels`}
                group={selectedGroup}
                organizationId={orgId}
                labels={localLabels}
                grantedLabelIds={grantedLabelIds}
                grantsLoading={grantsLoading}
                onGranted={(labelId) =>
                  setGrantedLabelIds((prev) => new Set(prev).add(labelId))
                }
                onRevoked={(labelId) =>
                  setGrantedLabelIds((prev) => {
                    const next = new Set(prev);
                    next.delete(labelId);
                    return next;
                  })
                }
              />
            ) : (
              <GroupMembersPanel
                key={selectedGroup.id}
                group={selectedGroup}
                organizationId={orgId}
                availableUsers={members}
                groupLabels={grantedLabels}
                onMemberCountChange={handleMemberCountChange}
              />
            )
          ) : null}
        </section>
      ) : (
        <section className="card border border-base-300/50 bg-base-100 p-6 shadow-sm">
          <p className="text-sm text-base-content/60">
            {t.translations.NO_GROUPS_DEFINED}
          </p>
        </section>
      )}

      <GroupEditModal
        isOpen={isGroupModalOpen}
        isSaving={savingGroup}
        editingGroup={!!editingGroup}
        nameInput={groupNameInput}
        descriptionInput={groupDescriptionInput}
        onNameChange={setGroupNameInput}
        onDescriptionChange={setGroupDescriptionInput}
        onCancel={closeGroupModal}
        onSave={handleSaveGroup}
      />

      <GroupArchiveModal
        isOpen={showArchiveModal}
        groupNames={groupToArchive ? [groupToArchive.name] : []}
        totalMembers={groupToArchive?.memberCount}
        onClose={() => {
          setShowArchiveModal(false);
          setGroupToArchive(null);
        }}
        onConfirm={confirmArchiveGroup}
        loading={archivingGroupId === groupToArchive?.id}
      />
    </div>
  );
};

export default OrganizationGroupsClient;
