"use client";

import React, { useEffect, useMemo, useState } from "react";
import toast from "react-hot-toast";
import { AxiosError } from "axios";
import {
  MagnifyingGlassIcon,
  PlusIcon,
  ShieldCheckIcon,
  TrashIcon,
  CheckCircleIcon,
  XCircleIcon,
} from "@heroicons/react/24/outline";
import type {
  SensitivityLabelsDto,
  UserSensitivityLabelResponseDto,
  SensitivityLabelPermissionResponseDto,
  UserResponseDto,
  GroupResponseDto,
} from "@/app/(home)/types/responseDTOs";
import {
  archiveSensitivityLabelOrg,
  createSensitivityLabelsOrg,
  updateSensitivityLabelOrg,
  getAllSensitivityLabelsOrg,
  getUsersWithAccessToLabelOrg,
  revokeSensitivityLabelAccessOrg,
  getPermissionsForLabelOrg,
} from "@/app/lib/client_service/sensitivity_labels_services.client";
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
  groups: GroupResponseDto[];
}

function LabelPermissionsPanel({
  label,
  organizationId,
  refreshKey,
}: {
  label: SensitivityLabelsDto;
  organizationId: number;
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
      const perms = await getPermissionsForLabelOrg(organizationId, label.id);
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

  return (
    <div className="max-w-3xl py-6">
      <div className="grid grid-cols-2 gap-6">
        <div>
          <p className="mb-2 text-xs font-bold uppercase tracking-wide text-base-content/55">
            {t.translations.RECORD_PERMISSIONS}
          </p>
          <ul className="space-y-2">
            {RECORD_ACTIONS.map((action) => (
              <li key={action} className="flex items-center gap-2 text-sm">
                {grantedActions.has(action) ? (
                  <CheckCircleIcon className="h-4 w-4 shrink-0 text-success" />
                ) : (
                  <XCircleIcon className="h-4 w-4 shrink-0 text-base-content/30" />
                )}
                {actionLabel(action)}
              </li>
            ))}
          </ul>
        </div>
        <div>
          <p className="mb-2 text-xs font-bold uppercase tracking-wide text-base-content/55">
            {t.translations.FILE_PERMISSIONS}
          </p>
          <ul className="space-y-2">
            {FILE_ACTIONS.map((action) => (
              <li key={action} className="flex items-center gap-2 text-sm">
                {grantedActions.has(action) ? (
                  <CheckCircleIcon className="h-4 w-4 shrink-0 text-success" />
                ) : (
                  <XCircleIcon className="h-4 w-4 shrink-0 text-base-content/30" />
                )}
                {actionLabel(action)}
              </li>
            ))}
          </ul>
        </div>
      </div>
    </div>
  );
}

function AssignedUsersPanel({
  label,
  organizationId,
  members,
  groups,
}: {
  label: SensitivityLabelsDto;
  organizationId: number;
  members: UserResponseDto[];
  groups: GroupResponseDto[];
}) {
  const { t } = useLanguage();
  const [assignedUsers, setAssignedUsers] = useState<UserSensitivityLabelResponseDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [search, setSearch] = useState("");
  const [revokingUserId, setRevokingUserId] = useState<number | null>(null);
  const [wizardOpen, setWizardOpen] = useState(false);

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

  useEffect(() => {
    loadAssignedUsers();
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

  const normalizedSearch = search.trim().toLowerCase();
  const filteredUsers = normalizedSearch
    ? assignedUsers.filter(
        (u) =>
          u.userName.toLowerCase().includes(normalizedSearch) ||
          u.userEmail.toLowerCase().includes(normalizedSearch),
      )
    : assignedUsers;

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
          {filteredUsers.map((u) => (
            <div
              key={u.id}
              className="flex items-center gap-3 border-b border-base-200 px-4 py-4 last:border-b-0"
            >
              <AvatarCell name={u.userName} size={9} containerClassName="space-x-0" />
              <span className="min-w-0 flex-1">
                <strong className="block">{u.userName}</strong>
                <span className="block text-sm text-base-content/60">
                  {u.userEmail}
                  {u.grantedByName && (
                    <>
                      {" · "}
                      {t.translations.GRANTED_BY_ON.replace("{name}", u.grantedByName).replace(
                        "{date}",
                        new Date(u.grantedAt).toLocaleDateString(),
                      )}
                    </>
                  )}
                </span>
              </span>
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
            </div>
          ))}
        </div>
      )}

      <AssignUsersWizardModalOrg
        isOpen={wizardOpen}
        onClose={() => setWizardOpen(false)}
        organizationId={organizationId}
        labelId={label.id}
        labelName={label.name}
        members={members}
        groups={groups}
        assignedUserIds={new Set(assignedUsers.map((u) => u.userId))}
        onAssigned={loadAssignedUsers}
      />
    </div>
  );
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
    getPermissionsForLabelOrg(orgId, label.id)
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
    <div className="grid grid-cols-1 gap-5 p-4 lg:grid-cols-[minmax(280px,.78fr)_minmax(0,1.72fr)]">
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
            <button
              type="button"
              role="tab"
              onClick={() => setDetailTab("permissions")}
              className={`tab ${detailTab === "permissions" ? "tab-active text-primary" : ""}`}
            >
              {t.translations.PERMISSIONS}
            </button>
          </div>

          {orgId && detailTab === "permissions" ? (
            <LabelPermissionsPanel
              label={selectedLabel}
              organizationId={orgId}
              refreshKey={permissionsRefreshKey}
            />
          ) : orgId ? (
            <AssignedUsersPanel
              label={selectedLabel}
              organizationId={orgId}
              members={members}
              groups={groups}
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
