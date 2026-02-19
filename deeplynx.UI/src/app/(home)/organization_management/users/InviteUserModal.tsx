import React, { useState, KeyboardEvent } from "react";
import { EnvelopeIcon, XMarkIcon } from "@heroicons/react/24/outline";

/* -------------------------------------------------------------------------- */
/*                     Invite User to Organization Dialog                     */
/* -------------------------------------------------------------------------- */

interface InviteUserModalProps {
  isOpen: boolean;
  modalLoading: boolean;
  onClose: () => void;
  onInvite: (emails: string[]) => void;
}

const InviteUserModal: React.FC<InviteUserModalProps> = ({
  isOpen,
  modalLoading,
  onClose,
  onInvite,
}) => {
  const [emails, setEmails] = useState<string[]>([]);
  const [inputValue, setInputValue] = useState("");

  if (!isOpen) return null;

  const isValidEmail = (email: string): boolean => {
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    return emailRegex.test(email.trim());
  };

  const addEmail = (email: string) => {
    const trimmedEmail = email.trim();
    if (trimmedEmail && isValidEmail(trimmedEmail) && !emails.includes(trimmedEmail)) {
      setEmails([...emails, trimmedEmail]);
      setInputValue("");
    }
  };

  const removeEmail = (emailToRemove: string) => {
    setEmails(emails.filter(email => email !== emailToRemove));
  };

  const handleKeyDown = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === "Enter" || e.key === "," || e.key === " ") {
      e.preventDefault();
      addEmail(inputValue);
    } else if (e.key === "Backspace" && !inputValue && emails.length > 0) {
      removeEmail(emails[emails.length - 1]);
    }
  };

  const handlePaste = (e: React.ClipboardEvent<HTMLInputElement>) => {
    e.preventDefault();
    const pastedText = e.clipboardData.getData("text");
    const emailList = pastedText.split(/[\s,;]+/).filter(Boolean);
    
    const validEmails = emailList.filter(email => 
      isValidEmail(email) && !emails.includes(email.trim())
    );
    
    setEmails([...emails, ...validEmails.map(e => e.trim())]);
  };

  const handleInvite = () => {
    if (inputValue.trim()) {
      addEmail(inputValue);
    }
    
    const finalEmails = inputValue.trim() && isValidEmail(inputValue.trim()) && !emails.includes(inputValue.trim())
      ? [...emails, inputValue.trim()]
      : emails;
    
    if (finalEmails.length > 0) {
      onInvite(finalEmails);
      handleClose();
    }
  };

  const handleClose = () => {
    setEmails([]);
    setInputValue("");
    onClose();
  };

  const inviteDisabled = emails.length === 0 && !inputValue.trim();

  return (
    <div className="modal modal-open">
      <div className="modal-box max-w-xl">
        {/* Header */}
        <div className="flex justify-between items-center mb-6">
          <h3 className="font-bold text-2xl">
            Invite Users to Organization
          </h3>
          <button
            className="btn btn-sm btn-circle btn-ghost"
            onClick={handleClose}
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
                    Email addresses <span className="text-error">*</span>
                  </span>
                </label>
                <div className="input input-bordered input-lg min-h-[3rem] h-auto flex flex-wrap gap-2 items-center p-2">
                  {emails.map((email) => (
                    <div
                      key={email}
                      className="badge badge-lg gap-2 bg-base-200 px-3 py-3"
                    >
                      <span className="text-sm">{email}</span>
                      <button
                        type="button"
                        onClick={() => removeEmail(email)}
                        className="btn btn-ghost btn-xs btn-circle"
                      >
                        <XMarkIcon className="w-4 h-4" />
                      </button>
                    </div>
                  ))}
                  <input
                    type="text"
                    placeholder={emails.length === 0 ? "user@example.com" : "add more..."}
                    className="flex-1 min-w-[200px] outline-none bg-transparent"
                    value={inputValue}
                    onChange={(e) => setInputValue(e.target.value)}
                    onKeyDown={handleKeyDown}
                    onPaste={handlePaste}
                    onBlur={() => {
                      if (inputValue.trim()) {
                        addEmail(inputValue);
                      }
                    }}
                    autoFocus
                  />
                </div>
                <label className="label">
                  <span className="mt-1 label-text-alt text-base-content/60">
                    Press Enter, comma, or space to add multiple emails
                  </span>
                </label>
              </div>

              {/* Info Alert */}
              <div className="alert alert-info">
                <EnvelopeIcon className="w-6 h-6" />
                <div>
                  <h4 className="font-semibold">Email Notifications</h4>
                  <p className="text-sm">
                    Invitations will be sent to all added email addresses.
                  </p>
                </div>
              </div>
            </div>

            {/* Modal Actions */}
            <div className="modal-action">
              <button
                className="btn btn-ghost"
                onClick={handleClose}
                disabled={modalLoading}
              >
                Cancel
              </button>
              <button
                className={`btn btn-primary gap-2 ${
                  inviteDisabled ? "btn-disabled" : ""
                }`}
                disabled={inviteDisabled || modalLoading}
                onClick={handleInvite}
              >
                {modalLoading ? (
                  <span className="loading loading-spinner loading-sm" />
                ) : (
                  <EnvelopeIcon className="w-5 h-5" />
                )}
                Send Invitation
                {emails.length > 0 && ` (${emails.length})`}
              </button>
            </div>
          </>
        )}
      </div>

      <div className="modal-backdrop" onClick={handleClose} />
    </div>
  );
};

export default InviteUserModal;