"use client";

import React, { useEffect, useMemo, useState } from "react";
import toast from "react-hot-toast";
import { AxiosError } from "axios";
import {
  MagnifyingGlassIcon,
  PlusIcon,
  ShieldCheckIcon,
  TrashIcon,
  UserGroupIcon,
} from "@heroicons/react/24/outline";
import type {
  SensitivityLabelsDto,
  SensitivityLabelGrantResponseDto,
  SensitivityLabelUserAccessDto,
  GroupResponseDto,
  GroupSensitivityLabelResponseDto,
  UserResponseDto,
} from "@/app/(home)/types/responseDTOs";
import {
  archiveSensitivityLabelOrg,
  createSensitivityLabelsOrg,
  updateSensitivityLabelOrg,
  getAllSensitivityLabelsOrg,
  getUsersWithAccessToLabelOrg,
  revokeSensitivityLabelAccessOrg,
  getGroupsWithAccessToLabelOrg,
  getAvailablePermissionActionsForOrg,
  getGroupPermissionsForLabelOrg,
  getUserPermissionsMatrixForLabelOrg,
  grantSensitivityLabelAccessOrg,
  grantSensitivityLabelAccessToGroupOrg,
  revokeSensitivityLabelAccessFromGroupOrg,
} from "@/app/lib/client_service/sensitivity_labels_services.client";
import { getAllGroups, getGroupMembers } from "@/app/lib/client_service/group_services.client";
import LabelEditModal, {
  FILE_ACTIONS,
  RECORD_ACTIONS,
} from "@/app/(home)/organization_management/tag_management/LabelEditModal";
import ConfirmArchiveLabelModal from "@/app/(home)/organization_management/tag_management/ConfirmArchiveLabelModal";
import AvatarCell from "@/app/(home)/components/Avatar";
import AssignUsersWizardModalOrg from "./AssignUsersWizardModalOrg";
import { useLanguage } from "@/app/contexts/Language";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";

type DetailTab = "permissions" | "assigned-users" | "assigned-groups";

interface Props {
  labels: SensitivityLabelsDto[];
  members: UserResponseDto[];
  groups: GroupResponseDto[];
}

function AssignedUsersPanel({
  label,
  organizationId,
  members,
}: {
  label: SensitivityLabelsDto;
  organizationId: number;
  members: UserResponseDto[];
}) {
  const { t } = useLanguage();
  const [assignedUsers, setAssignedUsers] = useState<SensitivityLabelGrantResponseDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [search, setSearch] = useState("");
  const [revokingUserId, setRevokingUserId] = useState<number | null>(null);
  const [wizardOpen, setWizardOpen] = useState(false);
  const [selectedUserId, setSelectedUserId] = useState<number | null>(null);
  const [selectedUserPermissions, setSelectedUserPermissions] =
    useState<SensitivityLabelUserAccessDto | null>(null);
  const [permissionsLoading, setPermissionsLoading] = useState(false);
  const [permissionsEditing, setPermissionsEditing] = useState(false);
  const [editedPermissionIds, setEditedPermissionIds] = useState<Set<number>>(
    new Set(),
  );
  const [savingPermissions, setSavingPermissions] = useState(false);
  const [permissionsRefreshKey, setPermissionsRefreshKey] = useState(0);
  // userId -> names of groups that also grant this label to the user
  const [groupAccessByUser, setGroupAccessByUser] = useState<
    Map<number, string[]>
  >(new Map());

  const loadAssignedUsers = async () => {
    try {
      setLoading(true);
      const users = await getUsersWithAccessToLabelOrg(organizationId, label.id);
      setAssignedUsers(users);
    } catch (error) {
      console.error("Failed to load assigned users:", error);
    } finally {
      setLoading(false);
    }
  };

  const loadGroupAccess = async () => {
    try {
      const groups = await getGroupsWithAccessToLabelOrg(organizationId, label.id);
      const memberLists = await Promise.all(
        groups.map(async (g) => {
          try {
            const res = await getGroupMembers(organizationId, g.groupId);
            return { id: g.groupId, name: g.groupName, members: res.items };
          } catch (error) {
            console.error(`Failed to load members for group ${g.groupId}:`, error);
            return { id: g.groupId, name: g.groupName, members: [] };
          }
        }),
      );
      const map = new Map<number, string[]>();
      memberLists.forEach(({ name, members: groupMembers }) => {
        groupMembers.forEach((u) => {
          const existing = map.get(u.id);
          if (existing) existing.push(name);
          else map.set(u.id, [name]);
        });
      });
      setGroupAccessByUser(map);
    } catch (error) {
      console.error("Failed to load group-derived access:", error);
      setGroupAccessByUser(new Map());
    }
  };

  useEffect(() => {
    loadAssignedUsers();
    loadGroupAccess();
    setSelectedUserId(null);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [label.id]);

  useEffect(() => {
    if (!selectedUserId) {
      setSelectedUserPermissions(null);
      setPermissionsEditing(false);
      return;
    }

    setPermissionsLoading(true);
    setPermissionsEditing(false);
    getUserPermissionsMatrixForLabelOrg(organizationId, label.id, selectedUserId)
      .then((matrix) => {
        setSelectedUserPermissions(matrix);
        setEditedPermissionIds(
          new Set(
            matrix.userPermissions
              .filter((permission) => permission.hasPermission)
              .map((permission) => permission.permissionId),
          ),
        );
      })
      .catch((error) => {
        console.error("Failed to load user label permissions:", error);
        setSelectedUserPermissions(null);
      })
      .finally(() => setPermissionsLoading(false));
  }, [
    label.id,
    organizationId,
    permissionsRefreshKey,
    selectedUserId,
  ]);

  const toggleEditedPermission = (permissionId: number) => {
    setEditedPermissionIds((current) => {
      const next = new Set(current);
      if (next.has(permissionId)) next.delete(permissionId);
      else next.add(permissionId);
      return next;
    });
  };

  const selectUser = (userId: number) => {
    if (userId === selectedUserId) return;
    setSelectedUserPermissions(null);
    setPermissionsLoading(true);
    setSelectedUserId(userId);
  };

  const saveUserPermissions = async () => {
    if (!selectedUserId) return;
    try {
      setSavingPermissions(true);
      if (editedPermissionIds.size === 0) {
        await revokeSensitivityLabelAccessOrg(
          organizationId,
          label.id,
          selectedUserId,
        );
      } else {
        await grantSensitivityLabelAccessOrg(
          organizationId,
          label.id,
          selectedUserId,
          Array.from(editedPermissionIds),
        );
      }
      await loadAssignedUsers();
      setPermissionsEditing(false);
      setPermissionsRefreshKey((key) => key + 1);
      toast.success(t.translations.SUCCESSFULLY);
    } catch (error) {
      console.error("Failed to update user label permissions:", error);
      toast.error(t.translations.FAILED_TO_UPDATE_SENSITIVITY_LABELS);
    } finally {
      setSavingPermissions(false);
    }
  };

  const handleRevoke = async (userId: number) => {
    try {
      setRevokingUserId(userId);
      await revokeSensitivityLabelAccessOrg(organizationId, label.id, userId);
      setAssignedUsers((current) => current.filter((u) => u.userId !== userId));
      if (selectedUserId === userId) setSelectedUserId(null);
    } catch (error) {
      console.error(`Failed to revoke access for user ${userId}:`, error);
      toast.error(t.translations.FAILED_TO_REVOKE_ACCESS);
    } finally {
      setRevokingUserId(null);
    }
  };

  const assignedByUserId = useMemo(
    () => new Map(assignedUsers.map((u) => [u.userId, u])),
    [assignedUsers],
  );

  const allUsersView = useMemo(
    () =>
      members
        .filter((member) => assignedByUserId.has(member.id))
        .map((m) => ({
          userId: m.id,
          userName: m.name,
          userEmail: m.email,
          assigned: assignedByUserId.get(m.id) ?? null,
        }))
        .sort((a, b) => {
          if (!!a.assigned === !!b.assigned) return a.userName.localeCompare(b.userName);
          return a.assigned ? -1 : 1;
        }),
    [members, assignedByUserId, groupAccessByUser],
  );

  const normalizedSearch = search.trim().toLowerCase();
  const filteredUsers = normalizedSearch
    ? allUsersView.filter(
        (u) =>
          u.userName.toLowerCase().includes(normalizedSearch) ||
          u.userEmail.toLowerCase().includes(normalizedSearch),
      )
    : allUsersView;

  const permissionGroups = useMemo(
    () => {
      const permissionRows = selectedUserPermissions?.totalPermissions.length
        ? selectedUserPermissions.totalPermissions
        : selectedUserPermissions?.userPermissions ?? [];

      return [
      {
        name: t.translations.RECORD,
        permissions: permissionRows.filter((permission) =>
          permission.permissionName.toLowerCase().includes("record"),
        ),
      },
      {
        name: t.translations.FILE,
        permissions: permissionRows.filter((permission) =>
          permission.permissionName.toLowerCase().includes("file"),
        ),
      },
      ].filter((group) => group.permissions.length > 0);
    },
    [selectedUserPermissions, t],
  );
  const selectedGroupPermissions = selectedUserPermissions?.groupPermissions ?? [];
  const permissionGridColumns = `14rem 7rem 7rem${selectedGroupPermissions
    .map(() => " 7rem")
    .join("")}`;
  // Include columns, `gap-2` spaces, and the horizontal `px-3` cell padding.
  const permissionGridMinWidth = `${30.5 + selectedGroupPermissions.length * 7.5}rem`;

  return (
    <div className="flex min-h-0 flex-1 flex-col pb-0 pt-5">
      <div className="mb-5 flex flex-wrap items-end justify-between gap-4">
        <label className="input input-bordered input-sm flex w-full max-w-xs items-center gap-2 bg-base-100">
          <MagnifyingGlassIcon className="h-4 w-4 text-base-content/50" />
          <input
            type="search"
            className="grow"
            placeholder={t.translations.SEARCH_ASSIGNED_USERS}
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </label>
        <button type="button" className="btn btn-primary btn-sm" onClick={() => setWizardOpen(true)}>
          <PlusIcon className="h-4 w-4" />
          {t.translations.ASSIGN_USERS}
        </button>
      </div>

      {loading ? (
        <div className="flex justify-center py-10">
          <span className="loading loading-spinner loading-md" />
        </div>
      ) : filteredUsers.length === 0 ? (
        <p className="py-8 text-center text-sm text-base-content/60">
          {t.translations.NO_ASSIGNED_USERS}
        </p>
      ) : (
        <div className={`min-h-0 flex-1 ${selectedUserId === null ? "space-y-0" : "space-y-3 pr-1"}`}>
          <div
            className={`overflow-y-auto rounded-box border border-base-200 ${
              selectedUserId === null ? "h-full" : "h-[280px]"
            }`}
          >
          {filteredUsers.map((u) => {
            const hasGroupAccess = groupAccessByUser.has(u.userId);
            const hasDirectAssignment = !!u.assigned;
            const isAssigned = hasDirectAssignment || hasGroupAccess;
            return (
              <div
                key={u.userId}
                className={`flex cursor-pointer items-center gap-3 border-b border-base-200 px-4 py-4 ${
                  selectedUserId === u.userId
                    ? "border-l-4 border-l-primary bg-primary/10 pl-3"
                    : isAssigned ? "hover:bg-base-200/60" : "opacity-50"
                }`}
              >
                <button
                  type="button"
                  className={`flex min-w-0 flex-1 cursor-pointer items-center gap-3 overflow-hidden text-left ${
                    selectedUserId === u.userId ? "font-semibold" : ""
                  }`}
                  onClick={() => selectUser(u.userId)}
                >
                  <AvatarCell name={u.userName} size={9} containerClassName="space-x-0" />
                  <div className="min-w-0 flex-1">
                    <div className="flex min-w-0 items-center gap-1.5">
                      <strong className="truncate">{u.userName}</strong>
                      {hasGroupAccess && (
                        <span className="flex shrink-0 items-center gap-1">
                          {hasDirectAssignment && (
                            <span className="badge badge-primary badge-sm">
                              {t.translations.DIRECT_ACCESS}
                            </span>
                          )}
                          <span
                            className="badge badge-secondary badge-sm gap-1"
                            title={t.translations.ALSO_GRANTED_VIA_GROUPS.replace(
                              "{groups}",
                              groupAccessByUser.get(u.userId)!.join(", "),
                            )}
                          >
                            <UserGroupIcon className="h-3.5 w-3.5" />
                            {t.translations.VIA_GROUP_BADGE}
                          </span>
                        </span>
                      )}
                    </div>
                    <span className="block truncate text-sm text-base-content/60">
                      {u.userEmail}
                    </span>
                  </div>
                </button>
                {hasDirectAssignment && (
                  <button
                    type="button"
                    className="btn btn-ghost btn-sm text-error"
                    onClick={() => handleRevoke(u.userId)}
                    disabled={revokingUserId === u.userId}
                    aria-label={
                      hasGroupAccess
                        ? t.translations.REMOVE_DIRECT_ACCESS
                        : t.translations.REMOVE_ACCESS
                    }
                    title={
                      hasGroupAccess
                        ? t.translations.REMOVE_DIRECT_ACCESS
                        : t.translations.REMOVE_ACCESS
                    }
                  >
                    <TrashIcon className="h-5 w-5" />
                  </button>
                )}
              </div>
            );
          })}
          </div>
          <div className={selectedUserId === null ? "hidden" : ""}>
          <div className="rounded-box border border-base-200 p-4">
            {selectedUserId === null ? (
              <p className="text-sm text-base-content/60">
                {t.translations.SELECT_A_USER}
              </p>
            ) : permissionsLoading ? (
              <div className="flex min-h-48 items-center justify-center">
                <span className="loading loading-spinner loading-md" />
              </div>
            ) : selectedUserPermissions && permissionGroups.length ? (
              <div>
                <div className="flex items-center justify-between gap-3">
                  <h4 className="font-semibold">{t.translations.PERMISSIONS}</h4>
                  {permissionsEditing ? (
                    <div className="flex gap-2">
                      <button
                        type="button"
                        className="btn btn-ghost btn-sm"
                        onClick={() => setPermissionsEditing(false)}
                        disabled={savingPermissions}
                      >
                        {t.translations.CANCEL}
                      </button>
                      <button
                        type="button"
                        className="btn btn-primary btn-sm"
                        onClick={saveUserPermissions}
                        disabled={savingPermissions}
                      >
                        {savingPermissions && <span className="loading loading-spinner loading-xs" />}
                        {t.translations.SAVE}
                      </button>
                    </div>
                  ) : (
                    <button
                      type="button"
                      className="btn btn-outline btn-sm"
                      onClick={() => setPermissionsEditing(true)}
                    >
                      {t.translations.EDIT}
                    </button>
                  )}
                </div>
                <div className="mt-3 overflow-x-auto rounded-box border border-base-200 bg-base-100">
                  <div className="grid gap-2 border-b border-base-200 bg-base-200 px-3 py-2 text-xs font-semibold text-base-content/65" style={{ gridTemplateColumns: permissionGridColumns, minWidth: permissionGridMinWidth }}>
                    <span className="sticky left-0 z-20 bg-base-200">{t.translations.PERMISSIONS}</span>
                    <span className="sticky left-56 z-20 bg-base-200 text-center">{t.translations.TOTAL}</span>
                    <span className="text-center">{t.translations.DIRECT_ACCESS}</span>
                    {selectedGroupPermissions.map((group) => <span key={group.groupId} className="truncate text-center" title={group.groupName}>{group.groupName}</span>)}
                  </div>
                  {permissionGroups.map((group) => (
                    <div key={group.name}>
                      <div className="border-b border-base-200 bg-base-200/25 px-3 py-1.5 text-xs font-semibold text-base-content/60" style={{ minWidth: permissionGridMinWidth }}>
                        {group.name}
                      </div>
                      {group.permissions.map((permission) => (
                        <div key={permission.permissionId} className={`grid items-center gap-2 border-b border-base-200 bg-base-100 px-3 py-1.5 last:border-b-0 ${permissionsEditing ? "" : "text-base-content/60"}`} style={{ gridTemplateColumns: permissionGridColumns, minWidth: permissionGridMinWidth }}>
                          <span className="sticky left-0 z-10 bg-base-100 text-sm">{permission.permissionName}</span>
                          <span className="sticky left-56 z-10 bg-base-100 text-center"><input type="checkbox" className="checkbox checkbox-secondary checkbox-sm" checked={selectedUserPermissions.totalPermissions.some((totalPermission) => totalPermission.permissionId === permission.permissionId && totalPermission.hasPermission)} disabled /></span>
                          <span className="flex justify-center bg-base-100"><input type="checkbox" className="checkbox checkbox-primary checkbox-sm" checked={editedPermissionIds.has(permission.permissionId)} disabled={!permissionsEditing} onChange={() => toggleEditedPermission(permission.permissionId)} /></span>
                          {selectedGroupPermissions.map((group) => <span key={group.groupId} className="flex justify-center bg-base-100"><input type="checkbox" className="checkbox checkbox-secondary checkbox-sm" checked={group.permissions.some((groupPermission) => groupPermission.permissionId === permission.permissionId && groupPermission.hasPermission)} disabled /></span>)}
                        </div>
                      ))}
                    </div>
                  ))}
                </div>
              </div>
            ) : (
              <p className="text-sm text-base-content/60">
                {t.translations.NO_PERMISSIONS_AVAILABLE}
              </p>
            )}
          </div>
        </div>
        </div>
      )}

      <AssignUsersWizardModalOrg
        isOpen={wizardOpen}
        onClose={() => setWizardOpen(false)}
        organizationId={organizationId}
        labelId={label.id}
        labelName={label.name}
        members={members}
        assignedUserIds={new Set(assignedUsers.map((u) => u.userId))}
        groupAccessByUser={groupAccessByUser}
        onAssigned={loadAssignedUsers}
      />
    </div>
  );
}

function AssignedGroupsPanel({ label, organizationId, availableGroups }: { label: SensitivityLabelsDto; organizationId: number; availableGroups: GroupResponseDto[] }) {
  const { t } = useLanguage();
  const [groups, setGroups] = useState<GroupResponseDto[]>(availableGroups);
  const [assigned, setAssigned] = useState<GroupSensitivityLabelResponseDto[]>([]);
  const [actions, setActions] = useState<{ id: number; name: string }[]>([]);
  const [selectedGroupId, setSelectedGroupId] = useState<number | null>(null);
  const [permissionIds, setPermissionIds] = useState<Set<number>>(new Set());
  const [editing, setEditing] = useState(false);
  const [addOpen, setAddOpen] = useState(false);
  const [groupsToAdd, setGroupsToAdd] = useState<Set<number>>(new Set());
  const [newGroupPermissionIds, setNewGroupPermissionIds] = useState<Set<number>>(new Set());
  const actionGroups = useMemo(
    () => [
      { name: t.translations.RECORD, actions: actions.filter((action) => action.name.toLowerCase().includes("record")) },
      { name: t.translations.FILE, actions: actions.filter((action) => action.name.toLowerCase().includes("file")) },
    ].filter((group) => group.actions.length > 0),
    [actions, t],
  );

  const load = async () => {
    const [allGroupsResult, groupGrantsResult, actionsResult] =
      await Promise.allSettled([
        getAllGroups(organizationId),
        getGroupsWithAccessToLabelOrg(organizationId, label.id),
        getAvailablePermissionActionsForOrg(organizationId),
      ]);
    const allGroups =
      allGroupsResult.status === "fulfilled"
        ? allGroupsResult.value.items
        : availableGroups;
    const groupGrants =
      groupGrantsResult.status === "fulfilled" ? groupGrantsResult.value : [];
    const permissionActions =
      actionsResult.status === "fulfilled" ? actionsResult.value : [];
    setGroups(allGroups);
    setAssigned(groupGrants);
    setActions(permissionActions);
    setSelectedGroupId((current) =>
      groupGrants.some((group) => group.groupId === current)
        ? current
        : groupGrants[0]?.groupId ?? null,
    );
  };

  useEffect(() => { void load(); }, [availableGroups, label.id, organizationId]);
  useEffect(() => {
    if (!addOpen) return;
    getAllGroups(organizationId)
      .then((result) => setGroups(result.items))
      .catch((error) => console.error("Failed to load organization groups:", error));
  }, [addOpen, organizationId]);
  useEffect(() => {
    if (!selectedGroupId) { setPermissionIds(new Set()); setEditing(false); return; }
    getGroupPermissionsForLabelOrg(organizationId, label.id, selectedGroupId)
      .then((grant) => setPermissionIds(new Set(grant?.permissions.map((p) => p.labelPermissionId).filter((id): id is number => id !== null) ?? [])))
      .catch(() => setPermissionIds(new Set()));
    setEditing(false);
  }, [label.id, organizationId, selectedGroupId]);

  const save = async () => {
    if (!selectedGroupId || permissionIds.size === 0) return;
    await grantSensitivityLabelAccessToGroupOrg(organizationId, label.id, selectedGroupId, Array.from(permissionIds));
    setEditing(false);
    await load();
    toast.success(t.translations.SUCCESSFULLY);
  };
  const addGroups = async () => {
    if (groupsToAdd.size === 0 || newGroupPermissionIds.size === 0) return;
    await Promise.all(Array.from(groupsToAdd).map((groupId) =>
      grantSensitivityLabelAccessToGroupOrg(organizationId, label.id, groupId, Array.from(newGroupPermissionIds)),
    ));
    setAddOpen(false);
    setGroupsToAdd(new Set());
    await load();
  };
  const remove = async () => {
    if (!selectedGroupId) return;
    await revokeSensitivityLabelAccessFromGroupOrg(organizationId, label.id, selectedGroupId);
    await load();
  };
  const unassigned = groups.filter((group) => !assigned.some((grant) => grant.groupId === group.id));

  return <div className="py-6">
    <div className="mb-5 flex justify-end"><button type="button" className="btn btn-primary btn-sm" onClick={() => { setNewGroupPermissionIds(new Set(actions.map((action) => action.id))); setAddOpen(true); }}><PlusIcon className="h-4 w-4" />{t.translations.ADD_GROUPS}</button></div>
    <div className="grid h-[480px] gap-4 lg:grid-cols-[minmax(0,1.15fr)_minmax(0,1.1fr)]">
      <div className="overflow-y-auto rounded-box border border-base-200">
        {assigned.map((group) => <div key={group.groupId} className={`flex items-center gap-3 border-b border-base-200 px-4 py-4 last:border-b-0 ${selectedGroupId === group.groupId ? "border-l-4 border-l-primary bg-primary/10 pl-3" : ""}`}>
          <button type="button" onClick={() => setSelectedGroupId(group.groupId)} className="flex min-w-0 flex-1 items-center gap-3 text-left"><UserGroupIcon className="h-5 w-5 text-secondary" /><span className="min-w-0 flex-1"><strong className="block truncate">{group.groupName}</strong></span></button>
          <button type="button" className="btn btn-ghost btn-sm shrink-0 text-error" onClick={async () => { await revokeSensitivityLabelAccessFromGroupOrg(organizationId, label.id, group.groupId); await load(); }} aria-label={t.translations.REMOVE_ACCESS} title={t.translations.REMOVE_ACCESS}><TrashIcon className="h-5 w-5" /></button>
        </div>)}
      </div>
      <div className="overflow-y-auto rounded-box border border-base-200 p-5">
        {!selectedGroupId ? <p className="text-sm text-base-content/60">{t.translations.SELECT_A_GROUP}</p> : <>
          <div className="flex items-center justify-between"><h4 className="font-semibold">{t.translations.PERMISSIONS}</h4>{editing ? <div className="flex gap-2"><button className="btn btn-ghost btn-sm" onClick={() => setEditing(false)}>{t.translations.CANCEL}</button><button className="btn btn-primary btn-sm" onClick={() => void save()}>{t.translations.SAVE}</button></div> : <button className="btn btn-outline btn-sm" onClick={() => setEditing(true)}>{t.translations.EDIT}</button>}</div>
          <div className={`mt-4 space-y-4 ${editing ? "" : "opacity-60"}`}>{actionGroups.map((group) => <div key={group.name}><p className="text-xs font-semibold text-base-content/60">{group.name}</p><div className="mt-1 space-y-1">{group.actions.map((action) => <label key={action.id} className="flex items-center gap-2"><input type="checkbox" className="checkbox checkbox-primary checkbox-sm" checked={permissionIds.has(action.id)} disabled={!editing} onChange={() => setPermissionIds((current) => { const next = new Set(current); next.has(action.id) ? next.delete(action.id) : next.add(action.id); return next; })} /><span className="text-sm">{action.name}</span></label>)}</div></div>)}</div>
        </>}</div>
    </div>
    {addOpen && <div className="modal modal-open"><div className="modal-box flex h-[min(620px,calc(100vh-4rem))] max-w-3xl flex-col overflow-hidden p-0"><header className="border-b border-base-200 px-6 py-5"><h3 className="text-xl font-bold">{t.translations.ADD_GROUPS}</h3></header><div className="grid min-h-0 flex-1 grid-cols-1 lg:grid-cols-2"><section className="min-h-0 overflow-y-auto border-r border-base-200 p-5">{groups.length === 0 ? <p className="py-8 text-center text-sm text-base-content/60">{t.translations.NO_GROUPS_DEFINED}</p> : groups.map((group) => { const alreadyAssigned = assigned.some((grant) => grant.groupId === group.id); return <label key={group.id} className={`flex items-center gap-3 border-b border-base-200 py-3 ${alreadyAssigned ? "cursor-not-allowed opacity-50" : "cursor-pointer"}`}><input type="checkbox" className="checkbox checkbox-primary checkbox-sm" disabled={alreadyAssigned} checked={alreadyAssigned || groupsToAdd.has(Number(group.id))} onChange={() => setGroupsToAdd((current) => { const next = new Set(current); const id = Number(group.id); next.has(id) ? next.delete(id) : next.add(id); return next; })} /><UserGroupIcon className="h-5 w-5 text-secondary" /><span>{group.name}</span>{alreadyAssigned && <span className="badge badge-ghost badge-sm">{t.translations.ALREADY_ASSIGNED}</span>}</label>; })}</section><section className="min-h-0 overflow-y-auto p-5"><h4 className="font-semibold">{t.translations.PERMISSIONS}</h4><div className="mt-3 space-y-4">{actionGroups.map((group) => <div key={group.name}><p className="text-xs font-semibold text-base-content/60">{group.name}</p><div className="mt-1 space-y-1">{group.actions.map((action) => <label key={action.id} className="flex items-center gap-2"><input type="checkbox" className="checkbox checkbox-primary checkbox-sm" checked={newGroupPermissionIds.has(action.id)} onChange={() => setNewGroupPermissionIds((current) => { const next = new Set(current); next.has(action.id) ? next.delete(action.id) : next.add(action.id); return next; })} /><span className="text-sm">{action.name}</span></label>)}</div></div>)}</div></section></div><footer className="flex justify-end gap-2 border-t border-base-200 px-6 py-4"><button className="btn btn-ghost" onClick={() => setAddOpen(false)}>{t.translations.CANCEL}</button><button className="btn btn-primary" onClick={() => void addGroups()} disabled={groupsToAdd.size === 0 || newGroupPermissionIds.size === 0}>{t.translations.ADD_GROUPS}</button></footer></div><div className="modal-backdrop" onClick={() => setAddOpen(false)} /></div>}
  </div>;
}

const OrganizationSensitivityLabelsClient: React.FC<Props> = ({
  labels,
  members,
  groups,
}) => {
  const { t } = useLanguage();
  const { organization } = useOrganizationSession();
  const orgId = organization?.organizationId as number | undefined;

  const [selectedLabelId, setSelectedLabelId] = useState<number | null>(
    labels[0]?.id ?? null,
  );
  const [detailTab, setDetailTab] = useState<DetailTab>("assigned-users");
  const [labelSearch, setLabelSearch] = useState("");
  const [archivingLabelId, setArchivingLabelId] = useState<number | null>(
    null,
  );

  const [isLabelModalOpen, setIsLabelModalOpen] = useState(false);
  const [editingLabel, setEditingLabel] = useState<SensitivityLabelsDto | null>(
    null,
  );
  const [labelNameInput, setLabelNameInput] = useState("");
  const [labelDescriptionInput, setLabelDescriptionInput] = useState("");
  const [savingLabel, setSavingLabel] = useState(false);
  const [selectedActions, setSelectedActions] = useState<Set<string>>(
    new Set([...RECORD_ACTIONS, ...FILE_ACTIONS]),
  );
  const [permissionsLoading, setPermissionsLoading] = useState(false);

  const [showArchiveModal, setShowArchiveModal] = useState(false);
  const [labelToArchive, setLabelToArchive] =
    useState<SensitivityLabelsDto | null>(null);

  const [permissionsRefreshKey, setPermissionsRefreshKey] = useState(0);

  const [localLabels, setLocalLabels] = useState<SensitivityLabelsDto[]>(labels);

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
    loadLabels();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [orgId]);

  const normalizedSearch = labelSearch.trim().toLowerCase();
  const filteredLabels = useMemo(
    () =>
      normalizedSearch
        ? localLabels.filter(
            (l) =>
              l.name.toLowerCase().includes(normalizedSearch) ||
              l.description?.toLowerCase().includes(normalizedSearch),
          )
        : localLabels,
    [localLabels, normalizedSearch],
  );

  const selectedLabel =
    localLabels.find((l) => l.id === selectedLabelId) ?? localLabels[0] ?? null;

  const resetLabelModalState = () => {
    setEditingLabel(null);
    setLabelNameInput("");
    setLabelDescriptionInput("");
    setSavingLabel(false);
    setSelectedActions(new Set([...RECORD_ACTIONS, ...FILE_ACTIONS]));
    setPermissionsLoading(false);
  };

  const toggleAction = (action: string) => {
    setSelectedActions((current) => {
      const next = new Set(current);
      if (next.has(action)) next.delete(action);
      else next.add(action);
      return next;
    });
  };

  const openCreateLabelModal = () => {
    resetLabelModalState();
    setIsLabelModalOpen(true);
  };

  const openEditLabelModal = (label: SensitivityLabelsDto) => {
    resetLabelModalState();
    setEditingLabel(label);
    setLabelNameInput(label.name);
    setLabelDescriptionInput(label.description ?? "");
    setIsLabelModalOpen(true);
    if (!orgId) return;
    setPermissionsLoading(true);
  };

  const closeLabelModal = () => {
    setIsLabelModalOpen(false);
    resetLabelModalState();
  };

  const handleSaveLabel = async () => {
    if (!labelNameInput.trim() || !orgId) return;

    try {
      setSavingLabel(true);

      if (editingLabel) {
        const updated = await updateSensitivityLabelOrg(orgId, editingLabel.id, {
          name: labelNameInput.trim(),
          description: labelDescriptionInput.trim() || null,
          permissionActions: Array.from(selectedActions),
        });
        setLocalLabels((prev) => prev.map((l) => (l.id === updated.id ? updated : l)));
        toast.success(t.translations.ORGANIZATION_LABEL_UPDATED);
      } else {
        const created = await createSensitivityLabelsOrg(orgId, {
          name: labelNameInput.trim(),
          description: labelDescriptionInput.trim() || null,
          permissionActions: Array.from(selectedActions),
        });
        setLocalLabels((prev) => [...prev, created]);
        setSelectedLabelId(created.id);
        toast.success(t.translations.ORGANIZATION_LABEL_CREATED);
      }

      setPermissionsRefreshKey((prev) => prev + 1);
      closeLabelModal();
    } catch (error) {
      console.error("Failed to save organization label:", error);
      toast.error(t.translations.FAILED_TO_SAVE_ORGANIZATION_LABEL);
    } finally {
      setSavingLabel(false);
    }
  };

  const openArchiveModal = (label: SensitivityLabelsDto) => {
    setLabelToArchive(label);
    setShowArchiveModal(true);
  };

  const confirmArchiveLabel = async () => {
    if (!labelToArchive || !orgId) return;

    try {
      setArchivingLabelId(labelToArchive.id);
      await archiveSensitivityLabelOrg(orgId, labelToArchive.id, true);
      setLocalLabels((prev) => prev.filter((l) => l.id !== labelToArchive.id));
      toast.success(
        `${t.translations.LABEL} "${labelToArchive.name}" ${t.translations.ARCHIVED}.`,
      );
    } catch (error) {
      console.error("Failed to archive label:", error);
      const errorMessage = (error as AxiosError).message ?? "";
      const recordCountMatch = errorMessage.match(/used on (\d+) records?/);
      if (recordCountMatch) {
        toast.error(
          t.translations.LABEL_IN_USE_ON_RECORDS.replace(
            "{name}",
            labelToArchive.name,
          ).replace("{count}", recordCountMatch[1]),
        );
      } else if (errorMessage.includes("Cannot archive")) {
        toast.error(t.translations.LABEL_IN_USE);
      } else {
        toast.error(t.translations.FAILED_TO_ARCHIVE_LABEL);
      }
    } finally {
      setArchivingLabelId(null);
      setShowArchiveModal(false);
      setLabelToArchive(null);
    }
  };

  return (
    <div className="grid grid-cols-1 gap-5 p-4 lg:min-h-[75vh] lg:grid-cols-[minmax(280px,.78fr)_minmax(0,1.72fr)]">
      <aside className="card overflow-hidden border border-base-300/50 bg-base-100 shadow-sm">
        <div className="flex items-start justify-between gap-3 border-b border-base-200 p-4">
          <div>
            <h3 className="font-bold">{t.translations.SENSITIVITY_LABELS}</h3>
            <p className="text-sm text-base-content/60">
              {localLabels.length} {t.translations.TOTAL}
            </p>
          </div>
          <button
            type="button"
            className="btn btn-primary btn-sm"
            onClick={openCreateLabelModal}
            disabled={!orgId}
          >
            <PlusIcon className="h-4 w-4" />
            {t.translations.NEW_LABEL}
          </button>
        </div>
        <div className="p-4">
          <label className="input input-bordered input-sm flex w-full items-center gap-2 bg-base-100">
            <MagnifyingGlassIcon className="h-4 w-4 text-base-content/50" />
            <input
              type="search"
              className="grow"
              placeholder={t.translations.SEARCH_LABELS}
              value={labelSearch}
              onChange={(e) => setLabelSearch(e.target.value)}
            />
          </label>
        </div>
        {filteredLabels.map((label) => (
          <button
            key={label.id}
            type="button"
            onClick={() => {
              setSelectedLabelId(label.id);
              setDetailTab("assigned-users");
            }}
            className={`flex w-full items-center gap-3 border-b border-base-200 px-4 py-4 text-left ${
              selectedLabel?.id === label.id
                ? "border-l-4 border-l-primary bg-base-200"
                : "border-l-4 border-l-transparent hover:bg-base-200/60"
            }`}
          >
            <span className="min-w-0 flex-1">
              <strong className="block">{label.name}</strong>
            </span>
          </button>
        ))}
      </aside>

      {selectedLabel ? (
        <section className="card flex min-h-0 flex-col border border-base-300/50 bg-base-100 p-6 shadow-sm">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div className="flex flex-wrap items-center gap-2">
              <ShieldCheckIcon className="h-5 w-5 text-secondary" />
              <h3 className="text-xl font-bold">{selectedLabel.name}</h3>
            </div>
            <div className="flex gap-2">
              <button
                type="button"
                className="btn btn-outline btn-primary btn-sm"
                onClick={() => openEditLabelModal(selectedLabel)}
              >
                {t.translations.EDIT}
              </button>
              <button
                type="button"
                className="btn btn-outline btn-error btn-sm"
                onClick={() => openArchiveModal(selectedLabel)}
                disabled={archivingLabelId === selectedLabel.id}
              >
                {archivingLabelId === selectedLabel.id
                  ? t.translations.ARCHIVING
                  : t.translations.DELETE}
              </button>
            </div>
          </div>

          <div role="tablist" className="tabs tabs-border mt-6 border-b border-base-200">
            <button
              type="button"
              role="tab"
              onClick={() => setDetailTab("assigned-users")}
              className={`tab ${detailTab === "assigned-users" ? "tab-active text-primary" : ""}`}
            >
              {t.translations.ASSIGNED_USERS}
            </button>
            <button
              type="button"
              role="tab"
              onClick={() => setDetailTab("assigned-groups")}
              className={`tab ${detailTab === "assigned-groups" ? "tab-active text-primary" : ""}`}
            >
              {t.translations.GROUPS}
            </button>
          </div>

          {orgId ? detailTab === "assigned-groups" ? (
            <AssignedGroupsPanel
              label={selectedLabel}
              organizationId={orgId}
              availableGroups={groups}
            />
          ) : (
            <AssignedUsersPanel
              label={selectedLabel}
              organizationId={orgId}
              members={members}
            />
          ) : null}
        </section>
      ) : (
        <section className="card border border-base-300/50 bg-base-100 p-6 shadow-sm">
          <p className="text-sm text-base-content/60">
            {t.translations.NO_PROJECT_LABELS_DEFINED_WHEN_UNLOCKED}
          </p>
        </section>
      )}

      <LabelEditModal
        isOpen={isLabelModalOpen}
        isSaving={savingLabel}
        editingLabel={!!editingLabel}
        nameInput={labelNameInput}
        descriptionInput={labelDescriptionInput}
        selectedActions={selectedActions}
        onNameChange={setLabelNameInput}
        onDescriptionChange={setLabelDescriptionInput}
        onToggleAction={toggleAction}
        onCancel={closeLabelModal}
        onSave={handleSaveLabel}
        permissionsLoading={permissionsLoading}
      />

      <ConfirmArchiveLabelModal
        isOpen={showArchiveModal}
        labelName={labelToArchive?.name ?? ""}
        onClose={() => {
          setShowArchiveModal(false);
          setLabelToArchive(null);
        }}
        onConfirm={confirmArchiveLabel}
        loading={archivingLabelId === labelToArchive?.id}
      />
    </div>
  );
};

export default OrganizationSensitivityLabelsClient;
