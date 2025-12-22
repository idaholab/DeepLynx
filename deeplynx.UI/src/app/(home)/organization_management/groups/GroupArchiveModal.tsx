// src/app/(home)/organization_management/groups/GroupArchiveModal.tsx"use client";

import React from "react";
import { ExclamationTriangleIcon } from "@heroicons/react/24/outline";
import { useLanguage } from "@/app/contexts/Language";

interface GroupArchiveModalProps {
  isOpen: boolean;
  groupNames: string[];
  totalMembers?: number;
  onClose: () => void;
  onConfirm: () => void;
  loading?: boolean;
}

const GroupArchiveModal: React.FC<GroupArchiveModalProps> = ({
  isOpen,
  groupNames,
  totalMembers,
  onClose,
  onConfirm,
  loading = false,
}) => {
  const { t } = useLanguage();
  if (!isOpen) return null;

  const isSingle = groupNames.length === 1;

  return (
    <div className="modal modal-open">
      <div className="modal-box max-w-lg">
        {/* Header */}
        <div className="flex items-start gap-3 mb-4">
          <div className="mt-1">
            <ExclamationTriangleIcon className="w-8 h-8 text-warning" />
          </div>
          <div>
            <h3 className="font-bold text-xl mb-1">
              {isSingle
                ? t.translations.ARCHIVE_THIS_GROUP
                : t.translations.ARCHIVE_SELECTED_GROUPS}
            </h3>
            <p className="text-sm text-base-content/70">
              {isSingle
                ? t.translations.GROUP_WILL_BE_ARCHIVED_MEMBERS_WILL_BE_REMOVED
                : t.translations
                    .SELECTED_GROUP_WILL_BE_ARCHIVED_MEMBERS_WILL_BE_REMOVED}
            </p>
          </div>
        </div>

        {/* Group summary */}
        <div className="bg-base-200 rounded-lg p-3 mb-4 text-sm">
          {isSingle ? (
            <>
              <div className="font-semibold mb-1">{groupNames[0]}</div>
              {typeof totalMembers === "number" && (
                <div className="text-base-content/70">
                  {totalMembers} {t.translations.MEMBER}
                  {totalMembers === 1 ? "" : "s"} {t.translations.IN_THIS_GROUP}
                </div>
              )}
            </>
          ) : (
            <>
              <div className="font-semibold mb-1">
                {groupNames.length} {t.translations.GROUPS_SELECTED}
              </div>
              <ul className="list-disc list-inside text-base-content/70 space-y-0.5 max-h-24 overflow-y-auto">
                {groupNames.slice(0, 4).map((name) => (
                  <li key={name}>{name}</li>
                ))}
                {groupNames.length > 4 && (
                  <li className="italic">
                    +{groupNames.length - 4} {t.translations.MORE_}
                  </li>
                )}
              </ul>
            </>
          )}
        </div>

        {/* Warning blurb */}
        <div className="alert alert-warning mb-4">
          <ExclamationTriangleIcon className="w-5 h-5" />
          <div>
            <h4 className="font-semibold text-sm">
              {t.translations.WHAT_HAPPENS_NEXT}
            </h4>
            <p className="text-xs">{t.translations.GROUP_NO_LONGER_APPEAR}</p>
          </div>
        </div>

        {/* Actions */}
        <div className="modal-action">
          <button
            className="btn btn-ghost"
            onClick={onClose}
            disabled={loading}
          >
            {t.translations.CANCEL}
          </button>
          <button
            className={`btn btn-error gap-2 ${loading ? "btn-disabled" : ""}`}
            onClick={onConfirm}
            disabled={loading}
          >
            {loading ? (
              <span className="loading loading-spinner loading-sm" />
            ) : null}
            {t.translations.ARCHIVE}{" "}
            {isSingle ? t.translations.GROUP : t.translations.GROUPS}
          </button>
        </div>
      </div>

      {/* Backdrop */}
      <div className="modal-backdrop" onClick={onClose} />
    </div>
  );
};

export default GroupArchiveModal;
