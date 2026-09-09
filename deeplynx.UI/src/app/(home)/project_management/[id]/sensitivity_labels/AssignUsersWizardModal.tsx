"use client";

import React, { useMemo, useState } from "react";
import toast from "react-hot-toast";
import {
  MagnifyingGlassIcon,
  CheckIcon,
} from "@heroicons/react/24/outline";
import type { ProjectMemberResponseDto } from "@/app/(home)/types/responseDTOs";
import { grantSensitivityLabelAccessProject } from "@/app/lib/client_service/sensitivity_labels_services.client";
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
  projectId: number;
  labelId: number;
  labelName: string;
  projectMembers: ProjectMemberResponseDto[];
  assignedUserIds: Set<number>;
  onAssigned: () => void;
}

const AssignUsersWizardModal: React.FC<Props> = ({
  isOpen,
  onClose,
  projectId,
  labelId,
  labelName,
  projectMembers,
  assignedUserIds,
  onAssigned,
}) => {
  const { t } = useLanguage();
  const [search, setSearch] = useState("");
  const [selectedUserIds, setSelectedUserIds] = useState<Set<number>>(new Set());
  const [assigning, setAssigning] = useState(false);

  const individualUsers: NormalizedUser[] = useMemo(() => {
    const seen = new Set<number>();
    const result: NormalizedUser[] = [];
    for (const member of projectMembers) {
      if (!member.memberId || seen.has(member.memberId)) continue;
      seen.add(member.memberId);
      result.push({ id: member.memberId, name: member.name, email: member.email });
    }
    return result;
  }, [projectMembers]);

  const resetState = () => {
    setSearch("");
    setSelectedUserIds(new Set());
  };

  const handleClose = () => {
    resetState();
    onClose();
  };

  const toggleUser = (userId: number) => {
    if (assignedUserIds.has(userId)) return;

    setSelectedUserIds((current) => {
      const next = new Set(current);
      if (next.has(userId)) next.delete(userId);
      else next.add(userId);
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

    if (idsToGrant.length === 0) return;

    setAssigning(true);
    try {
      const results = await Promise.allSettled(
        idsToGrant.map((userId) => grantSensitivityLabelAccessProject(projectId, labelId, userId)),
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
      <div className="modal-box max-w-5xl border border-base-300 p-0">
        <header className="flex items-start justify-between gap-4 border-b border-base-200 px-6 py-5">
          <div>
            <p className="text-xs font-bold uppercase tracking-wide text-base-content/55">
              {labelName}
            </p>
            <h2 className="text-xl font-bold">{t.translations.ASSIGN_LABEL_TO_USERS}</h2>
          </div>
        </header>

        <div className="grid min-h-[470px] grid-cols-1 lg:grid-cols-[1.35fr_.65fr]">
            <section className="border-b border-base-200 p-6 lg:border-b-0 lg:border-r">
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
                  return (
                    <label
                      key={user.id}
                      className={`flex items-center gap-3 border-b border-base-200 px-4 py-4 last:border-b-0 ${
                        alreadyAssigned
                          ? "cursor-not-allowed bg-success/10"
                          : "cursor-pointer hover:bg-base-200/50"
                      }`}
                    >
                      <input
                        type="checkbox"
                        className="checkbox checkbox-primary checkbox-sm"
                        checked={alreadyAssigned || selectedUserIds.has(user.id)}
                        disabled={alreadyAssigned}
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
                    </label>
                  );
                })}
              </div>
            </section>

            <aside className="bg-base-200/35 p-6">
              <h3 className="font-bold">{t.translations.CURRENT_SELECTION}</h3>
              <div className="mt-5 space-y-5">
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
            </aside>
        </div>

        <footer className="flex justify-end gap-3 border-t border-base-200 px-6 py-4">
          <div className="flex gap-3">
            <button type="button" className="btn btn-ghost" onClick={handleClose}>
              {t.translations.CANCEL}
            </button>
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

export default AssignUsersWizardModal;
