// src/app/(home)/organization_management/users/InviteUserModal.tsx

import React from "react";
import { EnvelopeIcon, XMarkIcon } from "@heroicons/react/24/outline";
import { useLanguage } from "@/app/contexts/Language";

/* -------------------------------------------------------------------------- */
/*                     Invite User to Organization Dialog                     */
/* -------------------------------------------------------------------------- */

interface InviteUserModalProps {
  isOpen: boolean;
  inviteEmail: string;
  modalLoading: boolean;

  onClose: () => void;
  onInvite: () => void;
  onChangeEmail: (value: string) => void;
}

const InviteUserModal: React.FC<InviteUserModalProps> = ({
  isOpen,
  inviteEmail,
  modalLoading,
  onClose,
  onInvite,
  onChangeEmail,
}) => {
  const { t } = useLanguage();
  if (!isOpen) return null;

  const inviteDisabled = !inviteEmail || modalLoading;

  return (
    <div className="modal modal-open">
      <div className="modal-box max-w-xl">
        {/* Header */}
        <div className="flex justify-between items-center mb-6">
          <h3 className="font-bold text-2xl">
            {t.translations.INVITE_USER_TO_ORG}
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
                    if (e.key === "Enter" && inviteEmail && !inviteDisabled) {
                      onInvite();
                    }
                  }}
                  autoFocus
                />
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

export default InviteUserModal;
