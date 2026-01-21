// src/app/(home)/project_management/[id]/settings/components/DeleteStorageModal.tsx
"use client";

interface DeleteStorageModalProps {
  isOpen: boolean;
  onToggle: (value: boolean) => void;
  onDelete: () => void;
}

const DeleteStorageModal = ({
  isOpen,
  onToggle,
  onDelete,
}: DeleteStorageModalProps) => (
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
        <h3 className="text-lg font-bold text-error">Delete Storage</h3>
        <p className="py-4">
          Are you sure you want to delete this storage? This action cannot be
          undone.
        </p>
        <div className="modal-action">
          <button className="btn" onClick={() => onToggle(false)}>
            Cancel
          </button>
          <button className="btn btn-error" onClick={onDelete}>
            Delete
          </button>
        </div>
      </div>
      <label className="modal-backdrop" onClick={() => onToggle(false)}>
        Close
      </label>
    </div>
  </>
);

export default DeleteStorageModal;
