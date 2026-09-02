// src/app/(home)/organization_management/groups/GroupMemberLabelWarningModal.tsx
"use client";

import React from "react";
import { ExclamationTriangleIcon, ShieldCheckIcon } from "@heroicons/react/24/outline";
import type { SensitivityLabelsDto } from "@/app/(home)/types/responseDTOs";
import { useLanguage } from "@/app/contexts/Language";

interface GroupMemberLabelWarningModalProps {
  isOpen: boolean;
  mode: "add" | "remove";
  groupName: string;
  memberCount: number;
  labels: SensitivityLabelsDto[];
  loading?: boolean;
  onClose: () => void;
  onConfirm: () => void;
}

const GroupMemberLabelWarningModal: React.FC<GroupMemberLabelWarningModalProps> = ({
  isOpen,
  mode,
  groupName,
  memberCount,
  labels,
  loading = false,
  onClose,
  onConfirm,
}) => {
  const { t } = useLanguage();
  if (!isOpen) return null;

  const isRemove = mode === "remove";
  const title = isRemove
    ? t.translations.REMOVE_MEMBERS_LOSE_LABELS
    : t.translations.ADD_MEMBERS_INHERIT_LABELS;
  const note = isRemove
    ? t.translations.REMOVE_MEMBERS_LOSE_LABELS_NOTE
    : t.translations.ADD_MEMBERS_INHERIT_LABELS_NOTE;
  const labelsHeading = isRemove
    ? t.translations.LABELS_THEY_WILL_LOSE
    : t.translations.LABELS_THEY_WILL_INHERIT;
  const warning = isRemove
    ? t.translations.REMOVE_MEMBERS_LOSE_LABELS_WARNING
    : t.translations.ADD_MEMBERS_INHERIT_LABELS_WARNING;
  const arrow = isRemove ? "✕" : "→";
  const confirmClass = isRemove ? "btn-error" : "btn-primary";

  return (
    <div className="modal modal-open">
      <div className="modal-box max-w-lg">
        {/* Header */}
        <div className="mb-4 flex items-start gap-3">
          <div className="mt-1">
            <ShieldCheckIcon
              className={`h-8 w-8 ${isRemove ? "text-error" : "text-secondary"}`}
            />
          </div>
          <div>
            <h3 className="mb-1 text-xl font-bold">{title}</h3>
            <p className="text-sm text-base-content/70">{note}</p>
          </div>
        </div>

        {/* Summary */}
        <div className="mb-4 rounded-lg bg-base-200 p-3 text-sm">
          <div className="text-base-content/70">
            {memberCount} {t.translations.MEMBER}
            {memberCount === 1 ? "" : "s"} {arrow} {groupName}
          </div>
        </div>

        {/* Affected labels */}
        <div className="mb-4">
          <p className="mb-2 text-xs font-bold uppercase tracking-wide text-base-content/55">
            {labelsHeading}
          </p>
          <div className="flex flex-wrap gap-1.5">
            {labels.map((label) => (
              <span
                key={label.id}
                className={`badge gap-1 ${isRemove ? "badge-error" : "badge-secondary"}`}
                title={label.description ?? undefined}
              >
                <ShieldCheckIcon className="h-3 w-3" />
                {label.name}
              </span>
            ))}
          </div>
        </div>

        {/* Warning blurb */}
        <div className="alert alert-warning mb-4">
          <ExclamationTriangleIcon className="h-5 w-5" />
          <div>
            <h4 className="text-sm font-semibold">
              {t.translations.WHAT_HAPPENS_NEXT}
            </h4>
            <p className="text-xs">{warning}</p>
          </div>
        </div>

        {/* Actions */}
        <div className="modal-action">
          <button className="btn btn-ghost" onClick={onClose} disabled={loading}>
            {t.translations.CANCEL}
          </button>
          <button
            className={`btn ${confirmClass} gap-2 ${loading ? "btn-disabled" : ""}`}
            onClick={onConfirm}
            disabled={loading}
          >
            {loading ? <span className="loading loading-spinner loading-sm" /> : null}
            {t.translations.CONTINUE}
          </button>
        </div>
      </div>

      {/* Backdrop */}
      <div className="modal-backdrop" onClick={onClose} />
    </div>
  );
};

export default GroupMemberLabelWarningModal;
