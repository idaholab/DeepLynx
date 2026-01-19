import { useState, useEffect } from "react";
import { RoleResponseDto } from "../../../types/responseDTOs";

// Add this to your file (after CreateRoleModal or in a separate file)
interface EditRoleModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSubmit: (
    roleId: number,
    name?: string | null,
    description?: string | null
  ) => Promise<void>;
  role: RoleResponseDto | null;
}

const ProjectEditRoleModal = ({
  isOpen,
  onClose,
  onSubmit,
  role,
}: EditRoleModalProps) => {
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Update form when role changes
  useEffect(() => {
    if (role) {
      setName(role.name);
      setDescription(role.description || "");
    }
  }, [role]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!role) return;

    setError(null);
    setIsSubmitting(true);

    try {
      await onSubmit(role.id,
        name.trim() || null,
        description.trim() || null,
      );
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to update role");
    } finally {
      setIsSubmitting(false);
    }
  };

  if (!isOpen || !role) return null;

  return (
    <dialog className="modal modal-open">
      <div className="modal-box">
        <h3 className="font-bold text-lg mb-4">Edit Role</h3>

        <form onSubmit={handleSubmit}>
          <div className="form-control mb-4">
            <label className="label">
              <span className="label-text">
                Role Name <span className="text-error">*</span>
              </span>
            </label>
            <input
              type="text"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="Enter role name"
              className="input input-bordered w-full"
              required
              disabled={isSubmitting}
            />
          </div>

          <div className="form-control mb-4">
            <label className="label">
              <span className="label-text">Description</span>
            </label>
            <textarea
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="Enter role description (optional)"
              className="textarea textarea-bordered w-full"
              rows={3}
              disabled={isSubmitting}
            />
          </div>

          {error && (
            <div className="alert alert-error mb-4">
              <span className="text-sm">{error}</span>
            </div>
          )}

          <div className="modal-action">
            <button
              type="button"
              onClick={onClose}
              className="btn btn-ghost"
              disabled={isSubmitting}
            >
              Cancel
            </button>
            <button
              type="submit"
              className="btn btn-primary"
              disabled={isSubmitting || !name.trim()}
            >
              {isSubmitting ? (
                <>
                  <span className="loading loading-spinner loading-sm"></span>
                  Updating...
                </>
              ) : (
                "Update Role"
              )}
            </button>
          </div>
        </form>
      </div>
      <form method="dialog" className="modal-backdrop" onClick={onClose}>
        <button>close</button>
      </form>
    </dialog>
  );
};

export default ProjectEditRoleModal;
