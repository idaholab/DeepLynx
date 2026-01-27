// src/app/(home)/organization_management/roles_and_permissions/DeleteRoleModal.tsx

import {
  ExclamationCircleIcon,
  ShieldCheckIcon,
  TrashIcon,
  UsersIcon,
} from "@heroicons/react/24/outline";
import { useState } from "react";
import { RoleResponseDto } from "../../../types/responseDTOs";
import { useLanguage } from "../../../../contexts/Language";

interface DeleteRoleModalProps {
  isOpen: boolean;
  onClose: () => void;
  onConfirm: () => Promise<void>;
  role: RoleResponseDto | null;
}

const ProjectDeleteRoleModal = ({
  isOpen,
  onClose,
  onConfirm,
  role,
}: DeleteRoleModalProps) => {
  const [isDeleting, setIsDeleting] = useState(false);
  const { t } = useLanguage();

  const handleConfirm = async () => {
    setIsDeleting(true);
    try {
      await onConfirm();
      onClose();
    } catch (error) {
      console.error("Error deleting role:", error);
    } finally {
      setIsDeleting(false);
    }
  };

  if (!isOpen || !role) return null;

  return (
    <dialog className="modal modal-open">
      <div className="modal-box">
        <h3 className="font-bold text-lg flex items-center gap-2">
          <ExclamationCircleIcon className="w-6 h-6 text-error" />
          {t.translations.DELETE_ROLE}
        </h3>

        <div className="py-4 space-y-4">
          <div className="alert alert-warning">
            <ExclamationCircleIcon className="w-5 h-5 flex-shrink-0" />
            <div className="text-sm">
              <p className="font-semibold">
                {t.translations.ROLE_ARCHIVE}
              </p>
              <p>
                {t.translations.ROLE_RESTORE}
              </p>
            </div>
          </div>
          <div className="space-y-2">
            <div className="flex items-center gap-2">
              <ShieldCheckIcon className="w-5 h-5 text-primary" />
              <div>
                <p className="font-semibold">{role.name}</p>
                {role.description && (
                  <p className="text-sm text-base-content/60">
                    {role.description}
                  </p>
                )}
              </div>
            </div>
          </div>
          <p className="text-sm text-base-content/70">
            {t.translations.ROLE_DELETED}
          </p>
          <ul className="list-disc list-inside text-sm text-base-content/70 space-y-1">
            <li>
                {t.translations.ROLE_DELETED}
            </li>
            <li>
                {t.translations.HIDE_ROLE}
            </li>
            <li>
              {t.translations.PRESERVE_PERMISSIONS}
            </li>
          </ul>
        </div>

        <div className="modal-action">
          <button
            onClick={onClose}
            className="btn btn-ghost"
            disabled={isDeleting}
          >
            {t.translations.CANCEL}
          </button>
          <button
            onClick={handleConfirm}
            className="btn btn-error gap-2"
            disabled={isDeleting}
          >
            {isDeleting ? (
              <>
                <span className="loading loading-spinner loading-sm"></span>
                {t.translations.DELETING}
              </>
            ) : (
              <>
                <TrashIcon className="w-4 h-4" />
                {t.translations.DELETE}
              </>
            )}
          </button>
        </div>
      </div>
      <form method="dialog" className="modal-backdrop" onClick={onClose}>
        <button>
          {t.translations.CLOSE}
        </button>
      </form>
    </dialog>
  );
};

export default ProjectDeleteRoleModal;
