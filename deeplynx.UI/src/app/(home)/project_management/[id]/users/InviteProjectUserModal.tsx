// src/app/(home)/project_management/[id]/users/InviteProjectUserModal.tsx

import React from "react";
import { EnvelopeIcon, XMarkIcon } from "@heroicons/react/24/outline";
import { RoleResponseDto } from "@/app/(home)/types/responseDTOs";
import { useLanguage } from "@/app/contexts/Language";

/* -------------------------------------------------------------------------- */
/*                     Invite User to Project Dialog                          */
/* -------------------------------------------------------------------------- */

interface InviteProjectUserModalProps {
  isOpen: boolean;
  inviteEmail: string;
  selectedRoleId: string;
  roles: RoleResponseDto[];
  modalLoading: boolean;

  onClose: () => void;
  onInvite: () => void;
  onChangeEmail: (value: string) => void;
  onChangeRole: (value: string) => void;
}

const InviteProjectUserModal: React.FC<InviteProjectUserModalProps> = ({
  isOpen,
  inviteEmail,
  selectedRoleId,
  roles,
  modalLoading,
  onClose,
  onInvite,
  onChangeEmail,
  onChangeRole,
}) => {
  const { t } = useLanguage();
  if (!isOpen) return null;

  const inviteDisabled = !inviteEmail || !selectedRoleId || modalLoading;

  return (
    <div className="modal modal-open">
      <div className="modal-box max-w-xl">
        {/* Header */}
        <div className="flex justify-between items-center mb-6">
          <h3 className="font-bold text-2xl">
            {t.translations.INVITE_USER_TO_PROJECT}
          </h3>
          <button
            className="btn btn-sm btn-circle btn-ghost"
            onClick={onClose}
            disabled={modalLoading}
          >
            <XMarkIcon className="w-5 h-5" />
          </button>
        </div>

        {modalLoading ? (
          <div className="flex justify-center items-center py-12">
            <span className="loading loading-spinner loading-lg" />
          </div>
        ) : (
          <>
            <div className="space-y-4">
              {/* Email Input */}
              <div className="form-control">
                <label className="label">
                  <span className="label-text font-semibold">
                    {t.translations.EMAIL_ADDRESS}{" "}
                    <span className="text-error mr-2">*</span>
                  </span>
                </label>
                <input
                  type="email"
                  placeholder="user@example.com"
                  className="input input-bordered input-lg"
                  value={inviteEmail}
                  onChange={(e) => onChangeEmail(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === "Enter" && !inviteDisabled) {
                      onInvite();
                    }
                  }}
                  autoFocus
                />
              </div>

              {/* Role Selection */}
              <div className="form-control">
                <label className="label">
                  <span className="label-text font-semibold">
                    {t.translations.PROJECT_ROLES}{" "}
                    <span className="text-error mr-2">*</span>
                  </span>
                </label>
                <select
                  className="select select-bordered select-lg"
                  value={selectedRoleId}
                  onChange={(e) => onChangeRole(e.target.value)}
                >
                  <option value="">{t.translations.SELECT_A_ROLE_}</option>
                  {roles.map((role) => (
                    <option key={role.id} value={role.id}>
                      {role.name}
                    </option>
                  ))}
                </select>
              </div>

              {/* Info Alert */}
              <div className="alert alert-info">
                <EnvelopeIcon className="w-6 h-6" />
                <div>
                  <h4 className="font-semibold">
                    {t.translations.EMAIL_NOTIFICATIONS}
                  </h4>
                  <p className="text-sm">
                    {t.translations.EMAIL_INVITATION_DESCRIPTION}
                  </p>
                </div>
              </div>
            </div>

            {/* Modal Actions */}
            <div className="modal-action">
              <button
                className="btn btn-ghost"
                onClick={onClose}
                disabled={modalLoading}
              >
                {t.translations.CANCEL}
              </button>
              <button
                className={`btn btn-primary gap-2 ${
                  inviteDisabled ? "btn-disabled" : ""
                }`}
                disabled={inviteDisabled}
                onClick={onInvite}
              >
                {modalLoading ? (
                  <span className="loading loading-spinner loading-sm" />
                ) : (
                  <EnvelopeIcon className="w-5 h-5" />
                )}
                {t.translations.SEND_INVITATION}
              </button>
            </div>
          </>
        )}
      </div>

      <div className="modal-backdrop" onClick={onClose} />
    </div>
  );
};

export default InviteProjectUserModal;
