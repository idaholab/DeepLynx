// src/app/(home)/project_management/[id]/settings/components/DeleteStorageModal.tsx
"use client";

import { useLanguage } from "@/app/contexts/Language";

interface DeleteStorageModalProps {
  isOpen: boolean;
  onToggle: (value: boolean) => void;
  onDelete: () => void;
}

const DeleteStorageModal = ({
  isOpen,
  onToggle,
  onDelete,
}: DeleteStorageModalProps) => {
  const { t } = useLanguage();
  return (
    <>
      <input
        type="checkbox"
        id="delete_storage_modal"
        className="modal-toggle"
        checked={isOpen}
        onChange={() => onToggle(!isOpen)}
      />
      <div className="modal" role="dialog">
        <div className="modal-box">
          <h3 className="text-lg font-bold text-error">
            {t.translations.DEFAULT_STORAGE}
          </h3>
          <p className="py-4">
            {t.translations.ARE_YOU_SURE_YOU_WANT_TO_DELETE_THIS_STORAGE}
          </p>
          <div className="modal-action">
            <button className="btn" onClick={() => onToggle(false)}>
              {t.translations.CANCEL}
            </button>
            <button className="btn btn-error" onClick={onDelete}>
              {t.translations.DELETE}
            </button>
          </div>
        </div>
        <label className="modal-backdrop" onClick={() => onToggle(false)}>
          {t.translations.CLOSE}
        </label>
      </div>
    </>
  );
};

export default DeleteStorageModal;
