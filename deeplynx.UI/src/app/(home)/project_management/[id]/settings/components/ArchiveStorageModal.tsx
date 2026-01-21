// src/app/(home)/project_management/[id]/settings/components/ArchiveStorageModal.tsx
"use client";

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
}: ArchiveStorageModalProps) => (
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
          {archiveAction ? "Archive" : "Unarchive"} Storage
        </h3>
        <p className="py-4">
          Are you sure you want to {archiveAction ? "archive" : "unarchive"}{" "}
          this storage?
        </p>
        <div className="modal-action">
          <button className="btn" onClick={() => onToggle(false)}>
            Cancel
          </button>
          <button className="btn btn-warning" onClick={onArchive}>
            {archiveAction ? "Archive" : "Unarchive"}
          </button>
        </div>
      </div>
      <label className="modal-backdrop" onClick={() => onToggle(false)}>
        Close
      </label>
    </div>
  </>
);

export default ArchiveStorageModal;
