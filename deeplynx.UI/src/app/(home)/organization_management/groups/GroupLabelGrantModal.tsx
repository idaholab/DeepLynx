// src/app/(home)/organization_management/groups/GroupLabelGrantModal.tsx
"use client";

import React from "react";
import { ExclamationTriangleIcon, ShieldCheckIcon } from "@heroicons/react/24/outline";
import { useLanguage } from "@/app/contexts/Language";

interface GroupLabelGrantModalProps {
  isOpen: boolean;
  labelName: string;
  groupName: string;
  memberCount?: number;
  loading?: boolean;
  onClose: () => void;
  onConfirm: () => void;
}

const GroupLabelGrantModal: React.FC<GroupLabelGrantModalProps> = ({
  isOpen,
  labelName,
  groupName,
  memberCount,
  loading = false,
  onClose,
  onConfirm,
}) => {
  const { t } = useLanguage();
  if (!isOpen) return null;

  return (
    <div className="modal modal-open">
      <div className="modal-box max-w-lg">
        {/* Header */}
        <div className="mb-4 flex items-start gap-3">
          <div className="mt-1">
            <ShieldCheckIcon className="h-8 w-8 text-secondary" />
          </div>
          <div>
            <h3 className="mb-1 text-xl font-bold">
              {t.translations.ASSIGN_LABEL_TO_GROUP}
            </h3>
            <p className="text-sm text-base-content/70">
              {t.translations.GROUP_LABELS_GRANT_NOTE}
            </p>
          </div>
        </div>

        {/* Summary */}
        <div className="mb-4 rounded-lg bg-base-200 p-3 text-sm">
          <div className="mb-1 font-semibold">
            {labelName} → {groupName}
          </div>
          {typeof memberCount === "number" && (
            <div className="text-base-content/70">
              {memberCount} {t.translations.MEMBER}
              {memberCount === 1 ? "" : "s"} {t.translations.IN_THIS_GROUP}
            </div>
          )}
        </div>

        {/* Warning blurb */}
        <div className="alert alert-warning mb-4">
          <ExclamationTriangleIcon className="h-5 w-5" />
          <div>
            <h4 className="text-sm font-semibold">
              {t.translations.WHAT_HAPPENS_NEXT}
            </h4>
            <p className="text-xs">
              {typeof memberCount === "number"
                ? t.translations.GROUP_LABEL_GRANT_WARNING.replace(
                    "{count}",
                    String(memberCount),
                  )
                : t.translations.GROUP_LABEL_GRANT_WARNING_GENERIC}
            </p>
          </div>
        </div>

        {/* Actions */}
        <div className="modal-action">
          <button className="btn btn-ghost" onClick={onClose} disabled={loading}>
            {t.translations.CANCEL}
          </button>
          <button
            className={`btn btn-primary gap-2 ${loading ? "btn-disabled" : ""}`}
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

export default GroupLabelGrantModal;
