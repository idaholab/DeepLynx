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
  SensitivityLabelPermissionResponseDto,
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
} from "@/app/lib/client_service/sensitivity_labels_services.client";
import { getGroupMembers } from "@/app/lib/client_service/group_services.client";
import LabelEditModal, {
  FILE_ACTIONS,
  RECORD_ACTIONS,
} from "@/app/(home)/organization_management/tag_management/LabelEditModal";
import ConfirmArchiveLabelModal from "@/app/(home)/organization_management/tag_management/ConfirmArchiveLabelModal";
import AvatarCell from "@/app/(home)/components/Avatar";
import AssignUsersWizardModalOrg from "./AssignUsersWizardModalOrg";
import { useLanguage } from "@/app/contexts/Language";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";

type DetailTab = "permissions" | "assigned-users";

interface Props {
  labels: SensitivityLabelsDto[];
  members: UserResponseDto[];
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
            return { name: g.groupName, members: res.items };
          } catch (error) {
            console.error(`Failed to load members for group ${g.groupId}:`, error);
            return { name: g.groupName, members: [] };
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
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [label.id]);

  const handleRevoke = async (userId: number) => {
    try {
      setRevokingUserId(userId);
      await revokeSensitivityLabelAccessOrg(organizationId, label.id, userId);
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

  const allUsersView = useMemo(
    () =>
      members
        .filter(
          (member) =>
            assignedByUserId.has(member.id) || groupAccessByUser.has(member.id),
        )
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

  return (
    <div className="py-6">
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
        <div className="overflow-hidden rounded-box border border-base-200">
          {filteredUsers.map((u) => {
            const hasGroupAccess = groupAccessByUser.has(u.userId);
            const hasDirectAssignment = !!u.assigned;
            const isAssigned = hasDirectAssignment || hasGroupAccess;
            return (
              <div
                key={u.userId}
                className={`flex items-center gap-3 border-b border-base-200 px-4 py-4 last:border-b-0 ${
                  isAssigned ? "" : "opacity-50"
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
                {hasGroupAccess && (
                  <div className="flex items-center gap-1">
                    {hasDirectAssignment && hasGroupAccess && (
                      <span className="badge badge-primary">
                        {t.translations.DIRECT_ACCESS}
                      </span>
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
                  </div>
                )}
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

const OrganizationSensitivityLabelsClient: React.FC<Props> = ({
  labels,
  members,
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
        <section className="card border border-base-300/50 bg-base-100 p-6 shadow-sm">
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
          </div>

          {orgId ? (
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
