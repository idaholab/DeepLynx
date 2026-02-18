"use client";

import React from "react";
import { ExclamationTriangleIcon } from "@heroicons/react/24/outline";
import { useLanguage } from "@/app/contexts/Language";

type Props = {
  isOpen: boolean;
  labelName: string;
  onClose: () => void;
  onConfirm: () => void;
  loading?: boolean;
};

const ConfirmArchiveLabelModal: React.FC<Props> = ({
  isOpen,
  labelName,
  onClose,
  onConfirm,
  loading = false,
}) => {
  const { t } = useLanguage();

  return (
    <dialog className={`modal ${isOpen ? "modal-open" : ""}`}>
      <div className="modal-box max-w-sm">
        <div className="flex items-start gap-3">
          <ExclamationTriangleIcon className="w-8 h-8 text-warning" />
          <div>
            <h3 className="font-bold text-lg">{t.translations.ARCHIVE_LABEL}</h3>
            <p className="text-sm text-base-content/70 mt-1">
              {t.translations.ARE_YOU_SURE_YOU_WANT_TO_ARCHIVE}{" "}
              <span className="font-semibold">{labelName}</span>?<br />
              {t.translations.ARCHIVED_LABEL_RESTORED_LATER}
            </p>
          </div>
        </div>

        <div className="modal-action mt-6">
          <button
            className="btn btn-ghost"
            disabled={loading}
            onClick={onClose}
          >
            {t.translations.CANCEL}
          </button>

          <button
            className="btn btn-warning"
            disabled={loading}
            onClick={onConfirm}
          >
            {loading ? (
              <span className="loading loading-spinner loading-sm"></span>
            ) : (
              t.translations.ARCHIVE
            )}
          </button>
        </div>
      </div>

      <div className="modal-backdrop" onClick={onClose} />
    </dialog>
  );
};

export default ConfirmArchiveLabelModal;
