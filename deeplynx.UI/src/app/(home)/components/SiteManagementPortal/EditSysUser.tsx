import React, { useState, useEffect } from "react";
import { useLanguage } from "@/app/contexts/Language";
import { updateUser } from "@/app/lib/client_service/user_services.client";

interface EditSysUserProps {
  isOpen: boolean;
  onClose: () => void;
  userId: number;
  userName: string;
  // currentAdminStatus: boolean; //need to add backend
  onUserUpdated: () => void;
}

const EditSysUser = ({
  isOpen,
  onClose,
  userId,
  userName,
  onUserUpdated,
}: EditSysUserProps) => {
  const { t } = useLanguage();
  const [name, setName] = useState(userName);
  const [isSaving, setIsSaving] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  useEffect(() => {
    if (isOpen) {
      setName(userName);
      setErrorMsg(null);
    }
  }, [isOpen, userName]);

  const handleUpdate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim()) return;

    try {
      setIsSaving(true);
      setErrorMsg(null);

      await updateUser(userId, { name: name.trim() });

      // If you later expose other fields, include them in this payload:
      // await updateUser(userId, { name: name.trim(), username, isArchived, projectId, isActive });

      onUserUpdated();
      onClose();
    } catch (error) {
      console.error("Error updating user:", error);
      setErrorMsg("An error occurred while updating the user.");
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <>
      {isOpen && (
        <dialog className="modal modal-open">
          <div className="modal-box max-w-lg">
            <h3 className="font-bold text-lg mb-4 text-neutral">
              {t.translations.EDIT_USER}
            </h3>

            <form onSubmit={handleUpdate} className="flex flex-col gap-4">
              <div className="flex flex-col gap-2">
                <label className="font-semibold text-sm text-neutral">
                  {t.translations.NAME}
                </label>
                <input
                  type="text"
                  placeholder="Name"
                  className="input input-primary w-full"
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  required
                  disabled={isSaving}
                />
              </div>

              {/* Future admin UI
              <div className="flex flex-col gap-2">
                <label className="font-semibold text-sm text-neutral">
                  {t.translations.ADMIN}
                </label>
                <select
                  className="select select-primary w-full"
                  value={isAdmin ? "true" : "false"}
                  onChange={(e) => setIsAdmin(e.target.value === "true")}
                  disabled={isSaving}
                >
                  <option value="false">No</option>
                  <option value="true">Yes</option>
                </select>
              </div>
              */}

              {errorMsg && (
                <p className="text-error text-sm" role="alert">
                  {errorMsg}
                </p>
              )}

              <div className="modal-action">
                <button
                  type="button"
                  className="btn"
                  onClick={onClose}
                  disabled={isSaving}
                >
                  {t.translations.CANCEL}
                </button>
                <button
                  type="submit"
                  className={`btn btn-primary ${isSaving ? "loading" : ""}`}
                  disabled={isSaving}
                >
                  {isSaving
                    ? t.translations.SAVING ?? "Saving..."
                    : t.translations.SAVE}
                </button>
              </div>
            </form>
          </div>
        </dialog>
      )}
    </>
  );
};

export default EditSysUser;
