import React, { useState, useEffect } from "react";
import toast from "react-hot-toast";
import { useLanguage } from "@/app/contexts/Language";
import {
  updateUser,
  setSysAdmin,
} from "@/app/lib/client_service/user_services.client";
import { setOrganizationAdminStatus } from "@/app/lib/client_service/organization_services.client";
interface EditSysUserProps {
  isOpen: boolean;
  onClose: () => void;
  userId: number;
  userName: string;
  onUserUpdated: () => void;
  scope: "org" | "site";
  currentOrgAdminStatus: boolean;
  currentSysAdminStatus: boolean;
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
  currentSysAdminStatus,
  organizationId,
}: EditSysUserProps) => {
  const { t } = useLanguage();
  const [name, setName] = useState(userName);
  const [isOrgAdmin, setIsOrgAdmin] = useState(currentOrgAdminStatus);
  const [isSysAdmin, setIsSysAdmin] = useState(currentSysAdminStatus);
  const [isSaving, setIsSaving] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  useEffect(() => {
    if (isOpen) {
      setName(userName);
      setIsOrgAdmin(currentOrgAdminStatus);
      setIsSysAdmin(currentSysAdminStatus);
      setErrorMsg(null);
    }
  }, [isOpen, userName, currentOrgAdminStatus, currentSysAdminStatus]);

  const handleUpdate = async (e: React.FormEvent) => {
    e.preventDefault();
    const trimmedName = name.trim();
    const nameChanged = trimmedName !== userName;
    const orgAdminChanged =
      scope === "org" && isOrgAdmin !== currentOrgAdminStatus;
    const sysAdminChanged =
      scope === "site" && isSysAdmin !== currentSysAdminStatus;

    if (!trimmedName) {
      setErrorMsg(t.translations.NAME_REQUIRED);
      return;
    }

    if (!nameChanged && !orgAdminChanged && !sysAdminChanged) {
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

      if (sysAdminChanged) {
        await setSysAdmin(userId, isSysAdmin);
      }

      let successMessage: string | null = null;

      if (nameChanged && orgAdminChanged) {
        successMessage =
          t.translations.USER_AND_ORG_ADMIN_ACCESS_UPDATED;
      } else if (nameChanged && sysAdminChanged) {
        successMessage =
          t.translations.USER_AND_SYSTEM_ADMIN_ACCESS_UPDATED;
      } else if (nameChanged) {
        successMessage = t.translations.USER_UPDATED_SUCCESSFULLY;
      } else if (orgAdminChanged) {
        successMessage = t.translations.ORGANIZATION_ADMIN_ACCESS_UPDATED;
      } else if (sysAdminChanged) {
        successMessage = t.translations.SYSTEM_ADMIN_ACCESS_UPDATED;
      }

      if (successMessage) {
        toast.success(successMessage);
      }

      onUserUpdated();
      onClose();
    } catch (error) {
      console.error("Error updating user:", error);
      setErrorMsg(t.translations.ERROR_UPDATING_USER);
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
                  placeholder={t.translations.NAME}
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
                    {t.translations.ORG_ADMIN}
                  </label>
                  <select
                    className="select select-primary w-full"
                    value={isOrgAdmin ? "true" : "false"}
                    onChange={(e) => setIsOrgAdmin(e.target.value === "true")}
                    disabled={isSaving}
                  >
                    <option value="false">{t.translations.NO}</option>
                    <option value="true">{t.translations.YES}</option>
                  </select>
                </div>
              )}
              {scope === "site" && (
                <div className="flex flex-col gap-2">
                  <label className="font-semibold text-sm text-neutral">
                    {t.translations.SYSTEM_ADMIN}
                  </label>
                  <select
                    className="select select-primary w-full"
                    value={isSysAdmin ? "true" : "false"}
                    onChange={(e) => setIsSysAdmin(e.target.value === "true")}
                    disabled={isSaving}
                  >
                    <option value="false">{t.translations.NO}</option>
                    <option value="true">{t.translations.YES}</option>
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
