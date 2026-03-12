import React, { useState, useEffect } from "react";
import toast from "react-hot-toast";
import { useLanguage } from "@/app/contexts/Language";
import { updateUser } from "@/app/lib/client_service/user_services.client";
import { setOrganizationAdminStatus } from "@/app/lib/client_service/organization_services.client";
interface EditSysUserProps {
  isOpen: boolean;
  onClose: () => void;
  userId: number;
  userName: string;
  onUserUpdated: () => void;
  scope: "org" | "site";
  currentOrgAdminStatus: boolean;
  organizationId: number;
}

const EditSysUser = ({
  isOpen,
  onClose,
  userId,
  userName,
  onUserUpdated,
  scope,
  currentOrgAdminStatus,
  organizationId,
}: EditSysUserProps) => {
  const { t } = useLanguage();
  const [name, setName] = useState(userName);
  const [isOrgAdmin, setIsOrgAdmin] = useState(currentOrgAdminStatus);
  const [isSaving, setIsSaving] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  useEffect(() => {
    if (isOpen) {
      setName(userName);
      setIsOrgAdmin(currentOrgAdminStatus);
      setErrorMsg(null);
    }
  }, [isOpen, userName, currentOrgAdminStatus]);

  const handleUpdate = async (e: React.FormEvent) => {
    e.preventDefault();
    const trimmedName = name.trim();
    const nameChanged = trimmedName !== userName;
    const orgAdminChanged =
      scope === "org" && isOrgAdmin !== currentOrgAdminStatus;

    if (!trimmedName) {
      setErrorMsg("Name is required.");
      return;
    }

    if (!nameChanged && !orgAdminChanged) {
      onClose();
      return;
    }

    try {
      setIsSaving(true);
      setErrorMsg(null);

      if (nameChanged) {
        await updateUser(userId, { name: trimmedName });
      }

      if (orgAdminChanged) {
        await setOrganizationAdminStatus(organizationId, userId, isOrgAdmin);
      }

      if (nameChanged && orgAdminChanged) {
        toast.success("User and organization admin access updated.");
      } else if (nameChanged) {
        toast.success("User updated successfully.");
      } else if (orgAdminChanged) {
        toast.success("Organization admin access updated.");
      }

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

              {scope === "org" && (
                <div className="flex flex-col gap-2">
                  <label className="font-semibold text-sm text-neutral">
                    {t.translations.ADMIN}
                  </label>
                  <select
                    className="select select-primary w-full"
                    value={isOrgAdmin ? "true" : "false"}
                    onChange={(e) => setIsOrgAdmin(e.target.value === "true")}
                    disabled={isSaving}
                  >
                    <option value="false">No</option>
                    <option value="true">Yes</option>
                  </select>
                </div>
              )}
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
                    ? (t.translations.SAVING ?? "Saving...")
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
