"use client";

import React, { useMemo, useState } from "react";
import toast from "react-hot-toast";
import {
  MagnifyingGlassIcon,
  ArrowLeftIcon,
  CheckIcon,
} from "@heroicons/react/24/outline";
import type { UserResponseDto } from "@/app/(home)/types/responseDTOs";
import { grantSensitivityLabelAccessOrg } from "@/app/lib/client_service/sensitivity_labels_services.client";
import AvatarCell from "@/app/(home)/components/Avatar";
import { useLanguage } from "@/app/contexts/Language";

type WizardStep = "select" | "preview";

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
  onAssigned,
}) => {
  const { t } = useLanguage();
  const [step, setStep] = useState<WizardStep>("select");
  const [search, setSearch] = useState("");
  const [selectedUserIds, setSelectedUserIds] = useState<Set<number>>(new Set());
  const [excludedPreviewUserIds, setExcludedPreviewUserIds] = useState<Set<number>>(new Set());
  const [assigning, setAssigning] = useState(false);

  const individualUsers: NormalizedUser[] = useMemo(
    () => members.map((m) => ({ id: m.id, name: m.name, email: m.email })),
    [members],
  );

  const resetState = () => {
    setStep("select");
    setSearch("");
    setSelectedUserIds(new Set());
    setExcludedPreviewUserIds(new Set());
  };

  const handleClose = () => {
    resetState();
    onClose();
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
    const map = new Map<number, { user: NormalizedUser }>();
    individualUsers
      .filter((u) => selectedUserIds.has(u.id))
      .forEach((u) => map.set(u.id, { user: u }));
    return Array.from(map.values());
  }, [individualUsers, selectedUserIds]);

  const newAssignmentCount = previewUsers.filter(
    ({ user }) => !assignedUserIds.has(user.id) && !excludedPreviewUserIds.has(user.id),
  ).length;
  const existingCount = previewUsers.filter(({ user }) => assignedUserIds.has(user.id)).length;

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

        {step === "select" ? (
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
                {filteredUsers.map((user) => (
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
              {previewUsers.map(({ user }) => {
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
                disabled={previewUsers.length === 0}
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
