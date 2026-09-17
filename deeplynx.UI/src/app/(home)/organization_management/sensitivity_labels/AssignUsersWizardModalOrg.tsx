"use client";

import React, { useEffect, useMemo, useState } from "react";
import toast from "react-hot-toast";
import {
  MagnifyingGlassIcon,
  CheckIcon,
  UserGroupIcon,
} from "@heroicons/react/24/outline";
import type { SensitivityLabelPermissionActionResponseDto, UserResponseDto } from "@/app/(home)/types/responseDTOs";
import { getAvailablePermissionActionsForOrg, grantSensitivityLabelAccessOrg } from "@/app/lib/client_service/sensitivity_labels_services.client";
import AvatarCell from "@/app/(home)/components/Avatar";
import { useLanguage } from "@/app/contexts/Language";

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
  assignedUserIds: Set<number>;
  groupAccessByUser: Map<number, string[]>;
  onAssigned: () => void;
}

const AssignUsersWizardModalOrg: React.FC<Props> = ({
  isOpen,
  onClose,
  organizationId,
  labelId,
  labelName,
  members,
  assignedUserIds,
  groupAccessByUser,
  onAssigned,
}) => {
  const { t } = useLanguage();
  const [search, setSearch] = useState("");
  const [selectedUserIds, setSelectedUserIds] = useState<Set<number>>(new Set());
  const [permissionActions, setPermissionActions] = useState<SensitivityLabelPermissionActionResponseDto[]>([]);
  const [permissionsByUserId, setPermissionsByUserId] = useState<Map<number, Set<number>>>(new Map());
  const [loadingPermissions, setLoadingPermissions] = useState(false);
  const [assigning, setAssigning] = useState(false);

  const individualUsers: NormalizedUser[] = useMemo(
    () => members.map((m) => ({ id: m.id, name: m.name, email: m.email })),
    [members],
  );

  const permissionGroups = useMemo(
    () => [
      {
        name: t.translations.RECORD,
        permissions: permissionActions.filter((permission) =>
          permission.name.toLowerCase().includes("record"),
        ),
      },
      {
        name: t.translations.FILE,
        permissions: permissionActions.filter((permission) =>
          permission.name.toLowerCase().includes("file"),
        ),
      },
    ].filter((group) => group.permissions.length > 0),
    [permissionActions, t],
  );

  const resetState = () => {
    setSearch("");
    setSelectedUserIds(new Set());
    setPermissionsByUserId(new Map());
  };

  useEffect(() => {
    if (!isOpen) return;
    setLoadingPermissions(true);
    getAvailablePermissionActionsForOrg(organizationId)
      .then(setPermissionActions)
      .catch((error) => {
        console.error("Failed to load organization label permission actions:", error);
        toast.error(t.translations.NO_PERMISSIONS_AVAILABLE);
      })
      .finally(() => setLoadingPermissions(false));
  }, [isOpen, organizationId, t]);

  const handleClose = () => {
    resetState();
    onClose();
  };

  const toggleUser = (userId: number) => {
    if (assignedUserIds.has(userId)) return;

    setSelectedUserIds((current) => {
      const next = new Set(current);
      if (next.has(userId)) {
        next.delete(userId);
        setPermissionsByUserId((permissions) => {
          const updated = new Map(permissions);
          updated.delete(userId);
          return updated;
        });
      } else {
        next.add(userId);
        setPermissionsByUserId((permissions) => {
          const updated = new Map(permissions);
          updated.set(userId, new Set(permissionActions.map((action) => action.id)));
          return updated;
        });
      }
      return next;
    });
  };

  const togglePermission = (userId: number, permissionId: number) => {
    setPermissionsByUserId((current) => {
      const next = new Map(current);
      const selected = new Set(next.get(userId));
      if (selected.has(permissionId)) selected.delete(permissionId);
      else selected.add(permissionId);
      next.set(userId, selected);
      return next;
    });
  };

  const newAssignmentCount = Array.from(selectedUserIds).filter(
    (userId) => !assignedUserIds.has(userId),
  ).length;

  const handleConfirm = async () => {
    const idsToGrant = Array.from(selectedUserIds).filter(
      (userId) => !assignedUserIds.has(userId),
    );

    if (idsToGrant.length === 0 || idsToGrant.some((id) => !permissionsByUserId.get(id)?.size)) return;

    setAssigning(true);
    try {
      const results = await Promise.allSettled(
        idsToGrant.map((userId) => grantSensitivityLabelAccessOrg(
          organizationId, labelId, userId, Array.from(permissionsByUserId.get(userId)!),
        )),
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
      <div className="modal-box flex h-[min(620px,calc(100vh-4rem))] w-[min(90vw,64rem)] max-w-none flex-col overflow-hidden border border-base-300 p-0">
        <header className="flex shrink-0 items-start justify-between gap-4 border-b border-base-200 px-6 py-5">
          <div>
            <p className="text-xs font-bold uppercase tracking-wide text-base-content/55">
              {labelName}
            </p>
            <h2 className="text-xl font-bold">{t.translations.ASSIGN_LABEL_TO_USERS}</h2>
          </div>
        </header>

        <div className="grid min-h-0 flex-1 grid-cols-1 overflow-hidden lg:grid-cols-[1.35fr_.65fr]">
            <section className="min-h-0 overflow-y-auto border-b border-base-200 p-6 lg:border-b-0 lg:border-r">
              <label className="input input-bordered flex w-full items-center gap-2 bg-base-100">
                <MagnifyingGlassIcon className="h-4 w-4 text-base-content/50" />
                <input
                  type="search"
                  className="grow"
                  placeholder={t.translations.SEARCH_USERS}
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                />
              </label>

              <div className="mt-4 max-h-[340px] overflow-auto rounded-box border border-base-200">
                {filteredUsers.map((user) => {
                  const alreadyAssigned = assignedUserIds.has(user.id);
                  const groupNames = groupAccessByUser.get(user.id);
                  const hasGroupAccess = !!groupNames?.length;
                  return (
                    <label
                      key={user.id}
                      className={`flex items-center gap-3 border-b border-base-200 px-4 py-4 last:border-b-0 ${
                        alreadyAssigned
                          ? "cursor-not-allowed bg-success/10"
                          : hasGroupAccess
                          ? "cursor-pointer bg-base-200/25 hover:bg-base-200/50"
                          : "cursor-pointer hover:bg-base-200/50"
                      }`}
                    >
                      <input
                        type="checkbox"
                        className="checkbox checkbox-primary checkbox-sm"
                        checked={alreadyAssigned || selectedUserIds.has(user.id)}
                        disabled={alreadyAssigned || loadingPermissions || permissionActions.length === 0}
                        onChange={() => toggleUser(user.id)}
                      />
                      <AvatarCell name={user.name} size={9} containerClassName="space-x-0" />
                      <span className="min-w-0 flex-1">
                        <strong className="block">{user.name}</strong>
                        <span className="block text-sm text-base-content/60">{user.email}</span>
                      </span>
                      {alreadyAssigned && (
                        <span className="badge badge-success gap-1">
                          <CheckIcon className="h-3.5 w-3.5" />
                          {t.translations.ALREADY_ASSIGNED}
                        </span>
                      )}
                      {hasGroupAccess && (
                        <span
                          className="badge badge-secondary gap-1"
                          title={t.translations.ALSO_GRANTED_VIA_GROUPS.replace(
                            "{groups}",
                            groupNames.join(", "),
                          )}
                        >
                          <UserGroupIcon className="h-3.5 w-3.5" />
                          {t.translations.VIA_GROUP_BADGE}
                        </span>
                      )}
                    </label>
                  );
                })}
              </div>
            </section>

            <aside className="min-h-0 overflow-y-auto bg-base-100 p-6">
              <h3 className="font-bold">{t.translations.CURRENT_SELECTION}</h3>
              <div className="mt-5 space-y-5">
                <div>
                  <p className="mb-2 text-xs font-bold uppercase tracking-wide text-base-content/55">
                    {t.translations.INDIVIDUAL_USERS}
                  </p>
                  <div className="flex flex-col">
                    {selectedUserIds.size === 0 && (
                      <span className="text-sm text-base-content/50">{t.translations.NONE_SELECTED}</span>
                    )}
                    {individualUsers
                      .filter((u) => selectedUserIds.has(u.id))
                      .map((u) => (
                        <div key={u.id} className="w-full border-b border-base-200 py-3 last:border-b-0">
                          <strong className="text-sm">{u.name}</strong>
                          <p className="mt-2 text-xs font-bold uppercase tracking-wide text-base-content/55">{t.translations.PERMISSIONS}</p>
                          <div className="mt-1 space-y-3">
                            {permissionGroups.map((group) => (
                              <div key={group.name}>
                                <p className="text-xs font-semibold text-base-content/60">{group.name}</p>
                                <div className="mt-1 space-y-1">
                                  {group.permissions.map((permission) => (
                                    <label key={permission.id} className="flex cursor-pointer items-center gap-1.5 px-1.5 py-0.5 text-sm">
                                      <input type="checkbox" className="checkbox checkbox-primary checkbox-sm"
                                        checked={permissionsByUserId.get(u.id)?.has(permission.id) ?? false}
                                        onChange={() => togglePermission(u.id, permission.id)} />
                                      <span>{permission.name}</span>
                                    </label>
                                  ))}
                                </div>
                              </div>
                            ))}
                          </div>
                        </div>
                      ))}
                  </div>
                </div>
              </div>
            </aside>
        </div>

        <footer className="flex shrink-0 justify-end gap-3 border-t border-base-200 px-6 py-4">
          <div className="flex gap-3">
            <button type="button" className="btn btn-ghost" onClick={handleClose}>
              {t.translations.CANCEL}
            </button>
            <button
              type="button"
              className="btn btn-primary"
              onClick={handleConfirm}
              disabled={newAssignmentCount === 0 || assigning || loadingPermissions || permissionActions.length === 0 || Array.from(selectedUserIds).some((id) => !permissionsByUserId.get(id)?.size)}
            >
              {assigning
                ? t.translations.SAVING
                : t.translations.ASSIGN_LABEL_TO_N_USERS.replace(
                    "{count}",
                    String(newAssignmentCount),
                  )}
            </button>
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
