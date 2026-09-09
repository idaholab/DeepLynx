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
  UserSensitivityLabelResponseDto,
  SensitivityLabelPermissionResponseDto,
  ProjectMemberResponseDto,
} from "@/app/(home)/types/responseDTOs";
import {
  archiveSensitivityLabelProject,
  createSensitivityLabelProject,
  updateSensitivityLabelProject,
  getUsersWithAccessToLabelProject,
  revokeSensitivityLabelAccessProject,
  getPermissionsForLabelProject,
  getUsersWithAccessToLabelOrg,
  revokeSensitivityLabelAccessOrg,
  getGroupsWithAccessToLabelOrg,
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

type DetailTab = "permissions" | "assigned-users";

interface Props {
  labels: SensitivityLabelsDto[];
  projectId: number;
  organizationId: number;
  orgLabelsLocked: boolean;
  refreshLabels: () => Promise<void>;
  projectMembers: ProjectMemberResponseDto[];
}

function LabelPermissionsPanel({
  label,
  projectId,
  refreshKey,
}: {
  label: SensitivityLabelsDto;
  projectId: number;
  refreshKey: number;
}) {
  const { t } = useLanguage();
  const [permissions, setPermissions] = useState<
    SensitivityLabelPermissionResponseDto[]
  >([]);
  const [loading, setLoading] = useState(false);

  const actionLabel = (action: string): string => {
    const key = `PERMISSION_${action.toUpperCase().replace(" ", "_")}` as keyof typeof t.translations;
    return (t.translations[key] as string | undefined) ?? action;
  };

  const loadPermissions = async () => {
    try {
      setLoading(true);
      const perms = await getPermissionsForLabelProject(projectId, label.id);
      setPermissions(perms.filter((p) => !p.isArchived));
    } catch (error) {
      console.error(`Failed to load permissions for label ${label.id}:`, error);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadPermissions();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [label.id, refreshKey]);

  if (loading) {
    return (
      <div className="flex justify-center py-10">
        <span className="loading loading-spinner loading-md" />
      </div>
    );
  }

  const grantedActions = new Set(permissions.map((p) => p.action));

  const permissionCategories = [
    { id: "records", label: t.translations.RECORD_PERMISSIONS, actions: RECORD_ACTIONS },
    { id: "files", label: t.translations.FILE_PERMISSIONS, actions: FILE_ACTIONS },
  ];

  return (
    <div className="py-6">
      <div className="space-y-4">
        {permissionCategories.map((category) => (
          <div key={category.id} className="card bg-base-200/25">
            <div className="card-body p-4">
              <h3 className="card-title mb-3 text-sm">{category.label}</h3>
              <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                {category.actions.map((action) => (
                  <label
                    key={action}
                    className="label cursor-default justify-start gap-2"
                  >
                    <input
                      type="checkbox"
                      checked={grantedActions.has(action)}
                      disabled
                      className="checkbox checkbox-primary checkbox-sm"
                    />
                    <span className="label-text">{actionLabel(action)}</span>
                  </label>
                ))}
              </div>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
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
  const [assignedUsers, setAssignedUsers] = useState<UserSensitivityLabelResponseDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [search, setSearch] = useState("");
  const [revokingUserId, setRevokingUserId] = useState<number | null>(null);
  const [wizardOpen, setWizardOpen] = useState(false);
  const [groupAccessByUser, setGroupAccessByUser] = useState<
    Map<number, string[]>
  >(new Map());
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
    if (!isOrgLabel) {
      setGroupAccessByUser(new Map());
      return;
    }

    const loadGroupAccess = async () => {
      try {
        const groups = await getGroupsWithAccessToLabelOrg(organizationId, label.id);
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
  }, [isOrgLabel, label.id, organizationId]);

  const handleRevoke = async (userId: number) => {
    try {
      setRevokingUserId(userId);
      if (isOrgLabel) {
        await revokeSensitivityLabelAccessOrg(organizationId, label.id, userId);
      } else {
        await revokeSensitivityLabelAccessProject(projectId, label.id, userId);
      }
      setAssignedUsers((current) => current.filter((u) => u.userId !== userId));
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
      assigned: UserSensitivityLabelResponseDto | null;
    }[] = [];
    for (const member of projectMembers) {
      if (!member.memberId || !member.email || seen.has(member.memberId)) continue;
      if (
        !assignedByUserId.has(member.memberId) &&
        !groupAccessByUser.has(member.memberId)
      ) {
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

  const assignedOnlyView = useMemo(
    () =>
      allUsersView
        .filter((u) => !!u.assigned)
        .sort((a, b) => a.userName.localeCompare(b.userName)),
    [allUsersView],
  );

  const usersView = isOrgLabel ? allUsersView : assignedOnlyView;

  const normalizedSearch = search.trim().toLowerCase();
  const filteredUsers = normalizedSearch
    ? usersView.filter(
        (u) =>
          u.userName.toLowerCase().includes(normalizedSearch) ||
          u.userEmail.toLowerCase().includes(normalizedSearch),
      )
    : usersView;

  return (
    <div className="py-6">
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
        <div className="overflow-hidden rounded-box border border-base-200">
          {filteredUsers.map((u) => {
            const hasGroupAccess = groupAccessByUser.has(u.userId);
            const isAssigned = !!u.assigned || hasGroupAccess;
            return (
              <div
                key={u.userId}
                className={`flex items-center gap-3 border-b border-base-200 px-4 py-4 last:border-b-0 ${
                  isOrgLabel ? "opacity-50" : isAssigned ? "" : "opacity-50"
                }`}
              >
                <AvatarCell name={u.userName} size={9} containerClassName="space-x-0" />
                <span className="min-w-0 flex-1">
                  <strong className="block">{u.userName}</strong>
                  <span className="block text-sm text-base-content/60">
                    {u.userEmail}
                    {u.assigned?.grantedByName && (
                      <>
                        {" · "}
                        {t.translations.GRANTED_BY_ON.replace(
                          "{name}",
                          u.assigned.grantedByName,
                        ).replace(
                          "{date}",
                          new Date(u.assigned.grantedAt).toLocaleDateString(),
                        )}
                      </>
                    )}
                  </span>
                </span>
                {!isAssigned && !isOrgLabel && (
                  <span className="badge badge-ghost">{t.translations.UNASSIGNED}</span>
                )}
                {hasGroupAccess && (
                  <span
                    className="badge badge-secondary gap-1"
                    title={t.translations.ALSO_GRANTED_VIA_GROUPS.replace(
                      "{groups}",
                      groupAccessByUser.get(u.userId)!.join(", "),
                    )}
                  >
                    <UserGroupIcon className="h-3.5 w-3.5" />
                    {t.translations.VIA_GROUP_BADGE}
                  </span>
                )}
                {isAssigned && !isOrgLabel && (
                  <button
                    type="button"
                    className="btn btn-ghost btn-sm text-error"
                    onClick={() => handleRevoke(u.userId)}
                    disabled={revokingUserId === u.userId}
                    aria-label={t.translations.REMOVE_ACCESS}
                    title={t.translations.REMOVE_ACCESS}
                  >
                    <TrashIcon className="h-5 w-5" />
                  </button>
                )}
              </div>
            );
          })}
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
    getPermissionsForLabelProject(projectId, label.id)
      .then((perms) => {
        setSelectedActions(
          new Set(perms.filter((p) => !p.isArchived).map((p) => p.action)),
        );
      })
      .catch((error) => {
        console.error(`Failed to load permissions for label ${label.id}:`, error);
      })
      .finally(() => setPermissionsLoading(false));
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
            <button
              type="button"
              role="tab"
              onClick={() => setDetailTab("permissions")}
              className={`tab ${detailTab === "permissions" ? "tab-active text-primary" : ""}`}
            >
              {t.translations.PERMISSIONS}
            </button>
          </div>

          {detailTab === "permissions" ? (
            <LabelPermissionsPanel
              label={selectedLabel}
              projectId={projectId}
              refreshKey={permissionsRefreshKey}
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
