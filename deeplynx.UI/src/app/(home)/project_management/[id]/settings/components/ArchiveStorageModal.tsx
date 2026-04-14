// src/app/(home)/project_management/[id]/settings/components/ArchiveStorageModal.tsx
"use client";

import { useLanguage } from "@/app/contexts/Language";

interface ArchiveStorageModalProps {
  isOpen: boolean;
  onToggle: (value: boolean) => void;
  archiveAction: boolean;
  onArchive: () => void;
}

const ArchiveStorageModal = ({
  isOpen,
  onToggle,
  archiveAction,
  onArchive,
}: ArchiveStorageModalProps) => {
  const { t } = useLanguage();

  return (
    <>
      <input
        type="checkbox"
        id="archive_storage_modal"
        className="modal-toggle"
        checked={isOpen}
        onChange={() => onToggle(!isOpen)}
      />
      <div className="modal" role="dialog">
        <div className="modal-box">
          <h3 className="text-lg font-bold">
            {archiveAction ? t.translations.ARCHIVE : t.translations.UNARCHIVE}{" "}
            {t.translations.STORAGE}
          </h3>
          <p className="py-4">
            {t.translations.ARE_YOU_SURE_YOU_WANT_TO_}{" "}
            {archiveAction ? t.translations.ARCHIVE : t.translations.UNARCHIVE}{" "}
            {t.translations._THIS_STORAGE}
          </p>
          <div className="modal-action">
            <button className="btn" onClick={() => onToggle(false)}>
              {t.translations.CANCEL}
            </button>
            <button className="btn btn-warning" onClick={onArchive}>
              {archiveAction
                ? t.translations.ARCHIVE
                : t.translations.UNARCHIVE}
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

export default ArchiveStorageModal;
