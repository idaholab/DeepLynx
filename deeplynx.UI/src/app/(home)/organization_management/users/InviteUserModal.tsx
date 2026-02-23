import React, { useState, KeyboardEvent } from "react";
import { EnvelopeIcon, XMarkIcon, ExclamationCircleIcon } from "@heroicons/react/24/outline";

/* -------------------------------------------------------------------------- */
/*                     Invite User to Organization Dialog                     */
/* -------------------------------------------------------------------------- */

interface InviteUserModalProps {
  isOpen: boolean;
  modalLoading: boolean;
  onClose: () => void;
  onInvite: (emails: string[]) => Promise<{ successful: string[]; failed: { email: string; error: string }[] }>;
}

const InviteUserModal: React.FC<InviteUserModalProps> = ({
  isOpen,
  modalLoading,
  onClose,
  onInvite,
}) => {
  const [emails, setEmails] = useState<string[]>([]);
  const [inputValue, setInputValue] = useState("");
  const [emailErrors, setEmailErrors] = useState<Map<string, string>>(new Map());

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
      // Clear any previous error for this email
      const newErrors = new Map(emailErrors);
      newErrors.delete(trimmedEmail);
      setEmailErrors(newErrors);
    }
  };

  const removeEmail = (emailToRemove: string) => {
    setEmails(emails.filter(email => email !== emailToRemove));
    const newErrors = new Map(emailErrors);
    newErrors.delete(emailToRemove);
    setEmailErrors(newErrors);
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

  const handleInvite = async () => {
    if (inputValue.trim()) {
      addEmail(inputValue);
    }
    
    const finalEmails = inputValue.trim() && isValidEmail(inputValue.trim()) && !emails.includes(inputValue.trim())
      ? [...emails, inputValue.trim()]
      : emails;
    
    if (finalEmails.length > 0) {
      const results = await onInvite(finalEmails);
      
      // Handle results
      if (results.failed.length > 0) {
        // Keep only failed emails in the list
        setEmails(results.failed.map(f => f.email));
        
        // Store error messages
        const newErrors = new Map<string, string>();
        results.failed.forEach(failure => {
          newErrors.set(failure.email, failure.error);
        });
        setEmailErrors(newErrors);
        
        // Don't close the modal if there are failures
      } else {
        // All succeeded, close the modal
        handleClose();
      }
    }
  };

  const handleClose = () => {
    setEmails([]);
    setInputValue("");
    setEmailErrors(new Map());
    onClose();
  };

  const inviteDisabled = emails.length === 0 && !inputValue.trim();
  const hasErrors = emailErrors.size > 0;

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
                <div className={`input input-bordered input-lg min-h-[3rem] h-auto flex flex-wrap gap-2 items-center p-2 ${
                  hasErrors ? 'border-error border-2' : ''
                }`}>
                  {emails.map((email) => {
                    const hasError = emailErrors.has(email);
                    return (
                      <div
                        key={email}
                        className={`badge badge-lg gap-2 px-3 py-3 ${
                          hasError ? 'bg-error/10 border-error border' : 'bg-base-200'
                        }`}
                      >
                        {hasError && <ExclamationCircleIcon className="w-4 h-4 text-error" />}
                        <span className={`text-sm ${hasError ? 'text-error' : ''}`}>{email}</span>
                        <button
                          type="button"
                          onClick={() => removeEmail(email)}
                          className="btn btn-ghost btn-xs btn-circle"
                        >
                          <XMarkIcon className="w-4 h-4" />
                        </button>
                      </div>
                    );
                  })}
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

              {/* Error Messages */}
              {hasErrors && (
                <div className="alert alert-error">
                  <ExclamationCircleIcon className="w-6 h-6" />
                  <div className="flex-1">
                    <h4 className="font-semibold">Invitation Errors</h4>
                    <div className="text-sm mt-1 space-y-1">
                      {Array.from(emailErrors.entries()).map(([email, error]) => (
                        <div key={email}>
                          <span className="font-semibold">{email}:</span> {error}
                        </div>
                      ))}
                    </div>
                  </div>
                </div>
              )}

              {/* Info Alert */}
              {!hasErrors && (
                <div className="alert alert-info">
                  <EnvelopeIcon className="w-6 h-6" />
                  <div>
                    <h4 className="font-semibold">Email Notifications</h4>
                    <p className="text-sm">
                      Invitations will be sent to all added email addresses.
                    </p>
                  </div>
                </div>
              )}
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
                {hasErrors ? 'Retry Failed Invitations' : 'Send Invitation'}
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