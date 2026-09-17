"use client";

import React, { useEffect, useMemo, useState } from "react";
import toast from "react-hot-toast";
import { AxiosError } from "axios";
import {
  MagnifyingGlassIcon,
  PlusIcon,
  ShieldCheckIcon,
  TrashIcon,
  InformationCircleIcon,
  UserGroupIcon,
} from "@heroicons/react/24/outline";
import type {
  SensitivityLabelsDto,
  SensitivityLabelGrantResponseDto,
  SensitivityLabelPermissionResponseDto,
  SensitivityLabelMemberAccessDto,
  GroupSensitivityLabelResponseDto,
  ProjectMemberResponseDto,
  SensitivityLabelUserAccessDto
} from "@/app/(home)/types/responseDTOs";
import {
  archiveSensitivityLabelProject,
  createSensitivityLabelProject,
  updateSensitivityLabelProject,
  getUsersWithAccessToLabelProject,
  revokeSensitivityLabelAccessProject,
  getUsersWithAccessToLabelOrg,
  revokeSensitivityLabelAccessOrg,
  getGroupsWithAccessToLabelOrg,
  getGroupsWithAccessToLabelProject,
  grantSensitivityLabelAccessToGroupProject,
  revokeSensitivityLabelAccessFromGroupProject,
  getGroupPermissionsForLabelProject,
  getUserPermissionsMatrixForLabelOrg,
  getUserPermissionsMatrixForLabelProject,
  grantSensitivityLabelAccessOrg,
  getUserPermissionsForLabelProject,
  getAvailablePermissionActionsForProject,
  grantSensitivityLabelAccessProject,
} from "@/app/lib/client_service/sensitivity_labels_services.client";
import { getGroupMembers } from "@/app/lib/client_service/group_services.client";
import LabelEditModal, {
  FILE_ACTIONS,
  RECORD_ACTIONS,
} from "@/app/(home)/organization_management/tag_management/LabelEditModal";
import ConfirmArchiveLabelModal from "@/app/(home)/organization_management/tag_management/ConfirmArchiveLabelModal";
import AvatarCell from "@/app/(home)/components/Avatar";
import AssignUsersWizardModal from "./AssignUsersWizardModal";
import { useLanguage } from "@/app/contexts/Language";

type DetailTab = "permissions" | "assigned-users" | "assigned-groups";

interface Props {
  labels: SensitivityLabelsDto[];
  projectId: number;
  organizationId: number;
  orgLabelsLocked: boolean;
  refreshLabels: () => Promise<void>;
  projectMembers: ProjectMemberResponseDto[];
}

function AssignedUsersPanel({
  label,
  projectId,
  organizationId,
  projectMembers,
}: {
  label: SensitivityLabelsDto;
  projectId: number;
  organizationId: number;
  projectMembers: ProjectMemberResponseDto[];
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
  const [editedPermissionIds, setEditedPermissionIds] = useState<Set<number>>(new Set());
  const [savingPermissions, setSavingPermissions] = useState(false);
  const [permissionsRefreshKey, setPermissionsRefreshKey] = useState(0);
  const [groupAccessByUser, setGroupAccessByUser] = useState<Map<number, string[]>>(new Map());
  const isOrgLabel = !label.projectId;

  const loadAssignedUsers = async () => {
    try {
      setLoading(true);
      const users = isOrgLabel
        ? await getUsersWithAccessToLabelOrg(organizationId, label.id)
        : await getUsersWithAccessToLabelProject(projectId, label.id);
      setAssignedUsers(users);
    } catch (error) {
      console.error("Failed to load assigned users:", error);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadAssignedUsers();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [label.id, label.projectId]);

  useEffect(() => {
    if (!selectedUserId) {
      setSelectedUserPermissions(null);
      setPermissionsEditing(false);
      return;
    }

    setPermissionsLoading(true);
    setPermissionsEditing(false);
    const fetchMatrix = isOrgLabel
      ? getUserPermissionsMatrixForLabelOrg(organizationId, label.id, selectedUserId)
      : getUserPermissionsMatrixForLabelProject(projectId, label.id, selectedUserId);
    fetchMatrix
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
  }, [isOrgLabel, label.id, organizationId, projectId, permissionsRefreshKey, selectedUserId]);

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
        if (isOrgLabel) {
          await revokeSensitivityLabelAccessOrg(organizationId, label.id, selectedUserId);
        } else {
          await revokeSensitivityLabelAccessProject(projectId, label.id, selectedUserId);
        }
      } else if (isOrgLabel) {
        await grantSensitivityLabelAccessOrg(
          organizationId, label.id, selectedUserId, Array.from(editedPermissionIds),
        );
      } else {
        await grantSensitivityLabelAccessProject(
          projectId, label.id, selectedUserId, Array.from(editedPermissionIds),
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

  useEffect(() => {
    const loadGroupAccess = async () => {
      try {
        const groups = isOrgLabel
          ? await getGroupsWithAccessToLabelOrg(organizationId, label.id)
          : await getGroupsWithAccessToLabelProject(projectId, label.id);
        const memberLists = await Promise.all(
          groups.map(async (group) => {
            try {
              const members = await getGroupMembers(organizationId, group.groupId);
              return { name: group.groupName, members: members.items };
            } catch (error) {
              console.error(`Failed to load members for group ${group.groupId}:`, error);
              return { name: group.groupName, members: [] };
            }
          }),
        );
        const accessByUser = new Map<number, string[]>();
        memberLists.forEach(({ name, members }) => {
          members.forEach((member) => {
            const existing = accessByUser.get(member.id);
            if (existing) existing.push(name);
            else accessByUser.set(member.id, [name]);
          });
        });
        setGroupAccessByUser(accessByUser);
      } catch (error) {
        console.error("Failed to load group-derived label access:", error);
        setGroupAccessByUser(new Map());
      }
    };

    void loadGroupAccess();
  }, [isOrgLabel, label.id, organizationId, projectId]);

  const handleRevoke = async (userId: number) => {
    try {
      setRevokingUserId(userId);
      if (isOrgLabel) {
        await revokeSensitivityLabelAccessOrg(organizationId, label.id, userId);
      } else {
        await revokeSensitivityLabelAccessProject(projectId, label.id, userId);
      }
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

  const allUsersView = useMemo(() => {
    const seen = new Set<number>();
    const result: {
      userId: number;
      userName: string;
      userEmail: string;
      assigned: SensitivityLabelGrantResponseDto | null;
    }[] = [];
    for (const member of projectMembers) {
      if (member.type === "group") continue;
      if (!member.memberId || !member.email || seen.has(member.memberId)) continue;
      if (!assignedByUserId.has(member.memberId) && !groupAccessByUser.has(member.memberId)) {
        continue;
      }
      seen.add(member.memberId);
      result.push({
        userId: member.memberId,
        userName: member.name,
        userEmail: member.email,
        assigned: assignedByUserId.get(member.memberId) ?? null,
      });
    }
    return result.sort((a, b) => {
      const aHasAccess = !!a.assigned || groupAccessByUser.has(a.userId);
      const bHasAccess = !!b.assigned || groupAccessByUser.has(b.userId);
      if (aHasAccess === bHasAccess) return a.userName.localeCompare(b.userName);
      return aHasAccess ? -1 : 1;
    });
  }, [projectMembers, assignedByUserId, groupAccessByUser]);

  const normalizedSearch = search.trim().toLowerCase();
  const filteredUsers = normalizedSearch
    ? allUsersView.filter(
        (u) =>
          u.userName.toLowerCase().includes(normalizedSearch) ||
          u.userEmail.toLowerCase().includes(normalizedSearch),
      )
    : allUsersView;

  const permissionGroups = useMemo(() => {
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
  }, [selectedUserPermissions, t]);

  const selectedGroupPermissions = selectedUserPermissions?.groupPermissions ?? [];
  const permissionGridColumns = `14rem 7rem 7rem${selectedGroupPermissions.map(() => " 7rem").join("")}`;
  const permissionGridMinWidth = `${30.5 + selectedGroupPermissions.length * 7.5}rem`;

  return (
    <div className="flex min-h-0 flex-1 flex-col pb-0 pt-5">
      {isOrgLabel && (
        <div className="alert alert-info mb-5 items-start">
          <InformationCircleIcon className="h-5 w-5 shrink-0" />
          <span className="text-sm">{t.translations.LABEL_ACCESS_MANAGED_AT_ORG_LEVEL}</span>
        </div>
      )}
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
        {!isOrgLabel && (
          <button type="button" className="btn btn-primary btn-sm" onClick={() => setWizardOpen(true)}>
            <PlusIcon className="h-4 w-4" />
            {t.translations.ASSIGN_USERS}
          </button>
        )}
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
          <div className={`overflow-y-auto rounded-box border border-base-200 ${selectedUserId === null ? "h-full" : "h-[280px]"}`}>
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
                  {hasDirectAssignment && !isOrgLabel && (
                    <button
                      type="button"
                      className="btn btn-ghost btn-sm text-error"
                      onClick={() => handleRevoke(u.userId)}
                      disabled={revokingUserId === u.userId}
                      aria-label={hasGroupAccess ? t.translations.REMOVE_DIRECT_ACCESS : t.translations.REMOVE_ACCESS}
                      title={hasGroupAccess ? t.translations.REMOVE_DIRECT_ACCESS : t.translations.REMOVE_ACCESS}
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
                <p className="text-sm text-base-content/60">{t.translations.SELECT_A_USER}</p>
              ) : permissionsLoading ? (
                <div className="flex min-h-48 items-center justify-center">
                  <span className="loading loading-spinner loading-md" />
                </div>
              ) : selectedUserPermissions && permissionGroups.length ? (
                <div>
                  <div className="flex items-center justify-between gap-3">
                    <h4 className="font-semibold">{t.translations.PERMISSIONS}</h4>
                    {!isOrgLabel && (
                      permissionsEditing ? (
                        <div className="flex gap-2">
                          <button type="button" className="btn btn-ghost btn-sm" onClick={() => setPermissionsEditing(false)} disabled={savingPermissions}>
                            {t.translations.CANCEL}
                          </button>
                          <button type="button" className="btn btn-primary btn-sm" onClick={saveUserPermissions} disabled={savingPermissions}>
                            {savingPermissions && <span className="loading loading-spinner loading-xs" />}
                            {t.translations.SAVE}
                          </button>
                        </div>
                      ) : (
                        <button type="button" className="btn btn-outline btn-sm" onClick={() => setPermissionsEditing(true)}>
                          {t.translations.EDIT}
                        </button>
                      )
                    )}
                  </div>
                  <div className="mt-3 overflow-x-auto rounded-box border border-base-200 bg-base-100">
                    <div className="grid gap-2 border-b border-base-200 bg-base-200 px-3 py-2 text-xs font-semibold text-base-content/65" style={{ gridTemplateColumns: permissionGridColumns, minWidth: permissionGridMinWidth }}>
                      <span className="sticky left-0 z-20 bg-base-200">{t.translations.PERMISSIONS}</span>
                      <span className="sticky left-56 z-20 bg-base-200 text-center">{t.translations.TOTAL}</span>
                      <span className="text-center">{t.translations.DIRECT_ACCESS}</span>
                      {selectedGroupPermissions.map((group) => (
                        <span key={group.groupId} className="truncate text-center" title={group.groupName}>{group.groupName}</span>
                      ))}
                    </div>
                    {permissionGroups.map((group) => (
                      <div key={group.name}>
                        <div className="border-b border-base-200 bg-base-200/25 px-3 py-1.5 text-xs font-semibold text-base-content/60" style={{ minWidth: permissionGridMinWidth }}>
                          {group.name}
                        </div>
                        {group.permissions.map((permission) => (
                          <div key={permission.permissionId} className={`grid items-center gap-2 border-b border-base-200 bg-base-100 px-3 py-1.5 last:border-b-0 ${permissionsEditing ? "" : "text-base-content/60"}`} style={{ gridTemplateColumns: permissionGridColumns, minWidth: permissionGridMinWidth }}>
                            <span className="sticky left-0 z-10 bg-base-100 text-sm">{permission.permissionName}</span>
                            <span className="sticky left-56 z-10 bg-base-100 text-center">
                              <input type="checkbox" className="checkbox checkbox-secondary checkbox-sm"
                                checked={selectedUserPermissions.totalPermissions.some((tp) => tp.permissionId === permission.permissionId && tp.hasPermission)}
                                disabled />
                            </span>
                            <span className="flex justify-center bg-base-100">
                              <input type="checkbox" className="checkbox checkbox-primary checkbox-sm"
                                checked={editedPermissionIds.has(permission.permissionId)}
                                disabled={!permissionsEditing}
                                onChange={() => toggleEditedPermission(permission.permissionId)} />
                            </span>
                            {selectedGroupPermissions.map((group) => (
                              <span key={group.groupId} className="flex justify-center bg-base-100">
                                <input type="checkbox" className="checkbox checkbox-secondary checkbox-sm"
                                  checked={group.permissions.some((gp) => gp.permissionId === permission.permissionId && gp.hasPermission)}
                                  disabled />
                              </span>
                            ))}
                          </div>
                        ))}
                      </div>
                    ))}
                  </div>
                </div>
              ) : (
                <p className="text-sm text-base-content/60">{t.translations.NO_PERMISSIONS_AVAILABLE}</p>
              )}
            </div>
          </div>
        </div>
      )}

      <AssignUsersWizardModal
        isOpen={wizardOpen}
        onClose={() => setWizardOpen(false)}
        projectId={projectId}
        labelId={label.id}
        labelName={label.name}
        projectMembers={projectMembers}
        assignedUserIds={new Set(assignedUsers.map((u) => u.userId))}
        onAssigned={loadAssignedUsers}
      />
    </div>
  );
}

function AssignedGroupsPanel({
  label,
  projectId,
  organizationId,
  projectMembers,
}: {
  label: SensitivityLabelsDto;
  projectId: number;
  organizationId: number;
  projectMembers: ProjectMemberResponseDto[];
}) {
  const { t } = useLanguage();

  // Groups scoped to this project: rows in projectMembers with type
  // "group" — memberId is the groupId, name is the group name.
  const projectGroups = useMemo(
    () =>
      projectMembers
        .filter(
          (m): m is ProjectMemberResponseDto & { memberId: number } =>
            m.type === "group" && m.memberId !== undefined,
        )
        .map((m) => ({ id: m.memberId, name: m.name })),
    [projectMembers],
  );

  const [assigned, setAssigned] = useState<GroupSensitivityLabelResponseDto[]>([]);
  const [actions, setActions] = useState<{ id: number; name: string }[]>([]);
  const [selectedGroupId, setSelectedGroupId] = useState<number | null>(null);
  const [permissionIds, setPermissionIds] = useState<Set<number>>(new Set());
  const [addOpen, setAddOpen] = useState(false);
  const [groupsToAdd, setGroupsToAdd] = useState<Set<number>>(new Set());
  const [newGroupPermissionIds, setNewGroupPermissionIds] = useState<Set<number>>(new Set());
  const [loading, setLoading] = useState(false);

  const actionGroups = useMemo(
    () => [
      { name: t.translations.RECORD, actions: actions.filter((a) => a.name.toLowerCase().includes("record")) },
      { name: t.translations.FILE, actions: actions.filter((a) => a.name.toLowerCase().includes("file")) },
    ].filter((g) => g.actions.length > 0),
    [actions, t],
  );

  const load = async () => {
    try {
      setLoading(true);
      const [groupGrantsResult, actionsResult] = await Promise.allSettled([
        getGroupsWithAccessToLabelProject(projectId, label.id),
        getAvailablePermissionActionsForProject(projectId),
      ]);
      const groupGrants =
        groupGrantsResult.status === "fulfilled" ? groupGrantsResult.value : [];
      const permissionActions =
        actionsResult.status === "fulfilled" ? actionsResult.value : [];
      setAssigned(groupGrants);
      setActions(permissionActions);
      setSelectedGroupId((current) =>
        groupGrants.some((g) => g.groupId === current)
          ? current
          : groupGrants[0]?.groupId ?? null,
      );
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [label.id, projectId]);

  useEffect(() => {
    if (!selectedGroupId) {
      setPermissionIds(new Set());
      return;
    }
    getGroupPermissionsForLabelProject(projectId, label.id, selectedGroupId)
      .then((grant) =>
        setPermissionIds(
          new Set(
            grant?.permissions
              .map((p) => p.labelPermissionId)
              .filter((id): id is number => id !== null) ?? [],
          ),
        ),
      )
      .catch(() => setPermissionIds(new Set()));
  }, [label.id, projectId, selectedGroupId]);

  const save = async () => {
    if (!selectedGroupId || permissionIds.size === 0) return;
    try {
      await grantSensitivityLabelAccessToGroupProject(
        projectId,
        label.id,
        selectedGroupId,
        Array.from(permissionIds),
      );
      await load();
      toast.success(t.translations.SUCCESSFULLY);
    } catch (error) {
      console.error("Failed to update group label permissions:", error);
      toast.error(t.translations.FAILED_TO_UPDATE_SENSITIVITY_LABELS);
    }
  };

  const addGroups = async () => {
    if (groupsToAdd.size === 0 || newGroupPermissionIds.size === 0) return;
    try {
      await Promise.all(
        Array.from(groupsToAdd).map((groupId) =>
          grantSensitivityLabelAccessToGroupProject(
            projectId,
            label.id,
            groupId,
            Array.from(newGroupPermissionIds),
          ),
        ),
      );
      setAddOpen(false);
      setGroupsToAdd(new Set());
      await load();
    } catch (error) {
      console.error("Failed to add groups to label:", error);
      toast.error(t.translations.FAILED_TO_UPDATE_SENSITIVITY_LABELS);
    }
  };

  const removeGroup = async (groupId: number) => {
    try {
      await revokeSensitivityLabelAccessFromGroupProject(projectId, label.id, groupId);
      if (selectedGroupId === groupId) setSelectedGroupId(null);
      await load();
    } catch (error) {
      console.error(`Failed to revoke access for group ${groupId}:`, error);
      toast.error(t.translations.FAILED_TO_REVOKE_ACCESS);
    }
  };

  const unassignedGroups = projectGroups.filter(
    (g) => !assigned.some((grant) => grant.groupId === g.id),
  );

  return (
    <div className="py-6">
      <div className="mb-5 flex justify-end">
        <button
          type="button"
          className="btn btn-primary btn-sm"
          onClick={() => {
            setNewGroupPermissionIds(new Set(actions.map((a) => a.id)));
            setAddOpen(true);
          }}
          disabled={projectGroups.length === 0}
        >
          <PlusIcon className="h-4 w-4" />
          {t.translations.ADD_GROUPS}
        </button>
      </div>

      {loading ? (
        <div className="flex justify-center py-10">
          <span className="loading loading-spinner loading-md" />
        </div>
      ) : (
        <div className="grid h-[480px] gap-4 lg:grid-cols-[minmax(0,1.15fr)_minmax(0,1.1fr)]">
          <div className="overflow-y-auto rounded-box border border-base-200">
            {assigned.length === 0 ? (
              <p className="py-8 text-center text-sm text-base-content/60">
                {t.translations.NO_ASSIGNED_USERS}
              </p>
            ) : (
              assigned.map((group) => (
                <div
                  key={group.groupId}
                  className={`flex items-center gap-3 border-b border-base-200 px-4 py-4 last:border-b-0 ${
                    selectedGroupId === group.groupId
                      ? "border-l-4 border-l-primary bg-primary/10 pl-3"
                      : ""
                  }`}
                >
                  <button
                    type="button"
                    onClick={() => setSelectedGroupId(group.groupId)}
                    className="flex min-w-0 flex-1 items-center gap-3 text-left"
                  >
                    <UserGroupIcon className="h-5 w-5 text-secondary" />
                    <span className="min-w-0 flex-1">
                      <strong className="block truncate">{group.groupName}</strong>
                    </span>
                  </button>
                  <button
                    type="button"
                    className="btn btn-ghost btn-sm shrink-0 text-error"
                    onClick={() => removeGroup(group.groupId)}
                    aria-label={t.translations.REMOVE_ACCESS}
                    title={t.translations.REMOVE_ACCESS}
                  >
                    <TrashIcon className="h-5 w-5" />
                  </button>
                </div>
              ))
            )}
          </div>
          <div className="overflow-y-auto rounded-box border border-base-200 p-5">
            {!selectedGroupId ? (
              <p className="text-sm text-base-content/60">{t.translations.SELECT_A_GROUP}</p>
            ) : (
              <>
                <div className="flex items-center justify-between">
                  <h4 className="font-semibold">{t.translations.PERMISSIONS}</h4>
                </div>
                <div className="mt-4 space-y-4 opacity-60">
                  {actionGroups.map((group) => (
                    <div key={group.name}>
                      <p className="text-xs font-semibold text-base-content/60">{group.name}</p>
                      <div className="mt-1 space-y-1">
                        {group.actions.map((action) => (
                          <label key={action.id} className="flex items-center gap-2">
                            <input
                              type="checkbox"
                              className="checkbox checkbox-primary checkbox-sm"
                              checked={permissionIds.has(action.id)}
                              disabled
                            />
                            <span className="text-sm">{action.name}</span>
                          </label>
                        ))}
                      </div>
                    </div>
                  ))}
                </div>
              </>
            )}
          </div>
        </div>
      )}

      {addOpen && (
        <div className="modal modal-open">
          <div className="modal-box flex h-[min(620px,calc(100vh-4rem))] max-w-3xl flex-col overflow-hidden p-0">
            <header className="border-b border-base-200 px-6 py-5">
              <h3 className="text-xl font-bold">{t.translations.ADD_GROUPS}</h3>
            </header>
            <div className="grid min-h-0 flex-1 grid-cols-1 lg:grid-cols-2">
              <section className="min-h-0 overflow-y-auto border-r border-base-200 p-5">
                {projectGroups.length === 0 ? (
                  <p className="py-8 text-center text-sm text-base-content/60">
                    {t.translations.NO_GROUPS_DEFINED}
                  </p>
                ) : (
                  projectGroups.map((group) => {
                    const alreadyAssigned = assigned.some((grant) => grant.groupId === group.id);
                    return (
                      <label
                        key={group.id}
                        className={`flex items-center gap-3 border-b border-base-200 py-3 ${
                          alreadyAssigned ? "cursor-not-allowed opacity-50" : "cursor-pointer"
                        }`}
                      >
                        <input
                          type="checkbox"
                          className="checkbox checkbox-primary checkbox-sm"
                          disabled={alreadyAssigned}
                          checked={alreadyAssigned || groupsToAdd.has(group.id)}
                          onChange={() =>
                            setGroupsToAdd((current) => {
                              const next = new Set(current);
                              if (next.has(group.id)) next.delete(group.id);
                              else next.add(group.id);
                              return next;
                            })
                          }
                        />
                        <UserGroupIcon className="h-5 w-5 text-secondary" />
                        <span>{group.name}</span>
                        {alreadyAssigned && (
                          <span className="badge badge-ghost badge-sm">
                            {t.translations.ALREADY_ASSIGNED}
                          </span>
                        )}
                      </label>
                    );
                  })
                )}
              </section>
              <section className="min-h-0 overflow-y-auto p-5">
                <h4 className="font-semibold">{t.translations.PERMISSIONS}</h4>
                <div className="mt-3 space-y-4">
                  {actionGroups.map((group) => (
                    <div key={group.name}>
                      <p className="text-xs font-semibold text-base-content/60">{group.name}</p>
                      <div className="mt-1 space-y-1">
                        {group.actions.map((action) => (
                          <label key={action.id} className="flex items-center gap-2">
                            <input
                              type="checkbox"
                              className="checkbox checkbox-primary checkbox-sm"
                              checked={newGroupPermissionIds.has(action.id)}
                              onChange={() =>
                                setNewGroupPermissionIds((current) => {
                                  const next = new Set(current);
                                  if (next.has(action.id)) next.delete(action.id);
                                  else next.add(action.id);
                                  return next;
                                })
                              }
                            />
                            <span className="text-sm">{action.name}</span>
                          </label>
                        ))}
                      </div>
                    </div>
                  ))}
                </div>
              </section>
            </div>
            <footer className="flex justify-end gap-2 border-t border-base-200 px-6 py-4">
              <button className="btn btn-ghost" onClick={() => setAddOpen(false)}>
                {t.translations.CANCEL}
              </button>
              <button
                className="btn btn-primary"
                onClick={() => void addGroups()}
                disabled={groupsToAdd.size === 0 || newGroupPermissionIds.size === 0}
              >
                {t.translations.ADD_GROUPS}
              </button>
            </footer>
          </div>
          <div className="modal-backdrop" onClick={() => setAddOpen(false)} />
        </div>
      )}
    </div>
  );
}

const ProjectSensitivityLabelsClient: React.FC<Props> = ({
  labels,
  projectId,
  organizationId,
  orgLabelsLocked,
  refreshLabels,
  projectMembers,
}) => {
  const { t } = useLanguage();
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

  const normalizedSearch = labelSearch.trim().toLowerCase();
  const filteredLabels = useMemo(
    () =>
      normalizedSearch
        ? labels.filter(
            (l) =>
              l.name.toLowerCase().includes(normalizedSearch) ||
              l.description?.toLowerCase().includes(normalizedSearch),
          )
        : labels,
    [labels, normalizedSearch],
  );

  const selectedLabel =
    labels.find((l) => l.id === selectedLabelId) ?? labels[0] ?? null;
  const isSelectedLabelOrgLabel = !selectedLabel?.projectId;

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
    setPermissionsLoading(true);
  };

  const closeLabelModal = () => {
    setIsLabelModalOpen(false);
    resetLabelModalState();
  };

  const handleSaveLabel = async () => {
    if (!labelNameInput.trim() || !projectId) return;

    try {
      setSavingLabel(true);

      if (editingLabel) {
        await updateSensitivityLabelProject(projectId, editingLabel.id, {
          name: labelNameInput.trim(),
          description: labelDescriptionInput.trim() || null,
          permissionActions: Array.from(selectedActions),
        });
        toast.success(t.translations.PROJECT_LABEL_UPDATED);
      } else {
        const created = await createSensitivityLabelProject(projectId, {
          name: labelNameInput.trim(),
          description: labelDescriptionInput.trim() || null,
          permissionActions: Array.from(selectedActions),
        });
        setSelectedLabelId(created.id);
        toast.success(t.translations.PROJECT_LABEL_CREATED);
      }

      await refreshLabels();
      setPermissionsRefreshKey((prev) => prev + 1);
      closeLabelModal();
    } catch (error) {
      console.error("Failed to save project label:", error);
      toast.error(t.translations.FAILED_TO_SAVE_PROJECT_LABEL);
    } finally {
      setSavingLabel(false);
    }
  };

  const openArchiveModal = (label: SensitivityLabelsDto) => {
    setLabelToArchive(label);
    setShowArchiveModal(true);
  };

  const confirmArchiveLabel = async () => {
    if (!labelToArchive || !projectId) return;

    try {
      setArchivingLabelId(labelToArchive.id);
      await archiveSensitivityLabelProject(projectId, labelToArchive.id, true);
      await refreshLabels();
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
              {labels.length} {t.translations.TOTAL}
            </p>
          </div>
          <button
            type="button"
            className="btn btn-primary btn-sm"
            onClick={openCreateLabelModal}
            disabled={orgLabelsLocked || !projectId}
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
              <span className="block text-sm text-base-content/60">
                {label.projectId
                  ? t.translations.PROJECT
                  : t.translations.ORGANIZATION_LABEL}
              </span>
            </span>
            <span
              className={`badge badge-sm ${
                label.projectId ? "badge-primary" : "badge-secondary"
              }`}
            >
              {label.projectId ? "PROJECT" : "ORG"}
            </span>
          </button>
        ))}
      </aside>

      {selectedLabel ? (
        <section className="card border border-base-300/50 bg-base-100 p-6 shadow-sm">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div className="flex flex-wrap items-center gap-2">
              <ShieldCheckIcon className="h-5 w-5 text-secondary" />
              <h3 className="text-xl font-bold">{selectedLabel.name}</h3>
            </div>
            {selectedLabel.projectId ? (
              <div className="flex gap-2">
                <button
                  type="button"
                  className="btn btn-outline btn-primary btn-sm"
                  onClick={() => openEditLabelModal(selectedLabel)}
                  disabled={orgLabelsLocked}
                >
                  {t.translations.EDIT}
                </button>
                <button
                  type="button"
                  className="btn btn-outline btn-error btn-sm"
                  onClick={() => openArchiveModal(selectedLabel)}
                  disabled={
                    orgLabelsLocked || archivingLabelId === selectedLabel.id
                  }
                >
                  {archivingLabelId === selectedLabel.id
                    ? t.translations.ARCHIVING
                    : t.translations.DELETE}
                </button>
              </div>
            ) : null}
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
            {!isSelectedLabelOrgLabel && (
              <button
                type="button"
                role="tab"
                onClick={() => setDetailTab("assigned-groups")}
                className={`tab ${detailTab === "assigned-groups" ? "tab-active text-primary" : ""}`}
              >
                {t.translations.GROUPS}
              </button>
            )}
          </div>

          {detailTab === "assigned-groups" && !isSelectedLabelOrgLabel ? (
            <AssignedGroupsPanel
              label={selectedLabel}
              projectId={projectId}
              organizationId={organizationId}
              projectMembers={projectMembers}
            />
          ) : (
            <AssignedUsersPanel
              label={selectedLabel}
              projectId={projectId}
              organizationId={organizationId}
              projectMembers={projectMembers}
            />
          )}
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

export default ProjectSensitivityLabelsClient;