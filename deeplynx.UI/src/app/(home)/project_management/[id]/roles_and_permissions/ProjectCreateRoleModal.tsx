import { useState } from "react";
import { useLanguage } from "../../../../contexts/Language";

interface CreateRoleModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSubmit: (data: {
    name: string;
    description: string | null;
  }) => Promise<void>;
  organizationId: number | string;
}

const ProjectCreateRoleModal = ({
  isOpen,
  onClose,
  onSubmit,
  organizationId,
}: CreateRoleModalProps) => {
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const { t } = useLanguage();

  const handleSubmit = async () => {
    setError(null);
    setIsSubmitting(true);

    try {
      await onSubmit({
        name: name.trim(),
        description: description.trim() || null,
      });
      setName("");
      setDescription("");
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to create role");
    } finally {
      setIsSubmitting(false);
    }
  };

  if (!isOpen) return null;

  return (
    <dialog className="modal modal-open">
      <div className="modal-box">
        <h3 className="font-bold text-lg mb-4">
          {t.translations.CREATE_NEW_ROLE}
        </h3>
        <div>
          <div className="form-control mb-4">
            <label className="label">
              <span className="label-text">
                {t.translations.ROLE_NAME}
                <span className="text-error">*</span>
              </span>
            </label>
            <input
              type="text"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="Enter role name"
              className="input input-bordered w-full"
              disabled={isSubmitting}
            />
          </div>

          <div className="form-control mb-4">
            <label className="label">
              <span className="label-text">
                {t.translations.DESCRIPTION}
              </span>
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
              {t.translations.CANCEL}
            </button>
            <button
              type="button"
              onClick={handleSubmit}
              className="btn btn-primary"
              disabled={isSubmitting || !name.trim()}
            >
              {isSubmitting ? (
                <>
                  <span className="loading loading-spinner loading-sm"></span>
                  {t.translations.CREATING}
                </>
              ) : (
                "Create Role"
              )}
            </button>
          </div>
        </div>
      </div>
      <form method="dialog" className="modal-backdrop" onClick={onClose}>
        <button>{t.translations.CLOSE}</button>
      </form>
    </dialog>
  );
};

export default ProjectCreateRoleModal;