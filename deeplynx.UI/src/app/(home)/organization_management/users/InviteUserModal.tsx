import React, { useEffect, useMemo, useState } from "react";
import { XMarkIcon, EnvelopeIcon, ExclamationCircleIcon } from "@heroicons/react/24/outline";

/* -------------------------------------------------------------------------- */
/*                     Invite Users to Organization Modal                     */
/*                (Mirrors "AddUsersToProjectModal" UI pattern)               */
/* -------------------------------------------------------------------------- */

interface InviteUserModalProps {
  isOpen: boolean;
  modalLoading: boolean;
  onClose: () => void;
  onInvite: (
    emails: string[]
  ) => Promise<{ successful: string[]; failed: { email: string; error: string }[] }>;
}

interface EmailError {
  email: string;
  error: string;
}

const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

const InviteUserModal: React.FC<InviteUserModalProps> = ({
  isOpen,
  modalLoading,
  onClose,
  onInvite,
}) => {
  const [emailInput, setEmailInput] = useState<string>("");
  const [emails, setEmails] = useState<string[]>([]);
  const [isProcessing, setIsProcessing] = useState(false);
  const [emailErrors, setEmailErrors] = useState<EmailError[]>([]);

  // Reset state when modal opens
  useEffect(() => {
    if (isOpen) {
      setEmailInput("");
      setEmails([]);
      setIsProcessing(false);
      setEmailErrors([]);
    }
  }, [isOpen]);

  const hasErrors = emailErrors.length > 0;

  const errorMap = useMemo(() => {
    const map = new Map<string, string>();
    for (const e of emailErrors) map.set(e.email.toLowerCase(), e.error);
    return map;
  }, [emailErrors]);

  const normalize = (value: string) => value.trim();
  const isValidEmail = (value: string) => EMAIL_REGEX.test(normalize(value));

  const addEmail = (value: string) => {
    const trimmed = normalize(value);
    if (!trimmed) return;

    // If invalid, keep it in the input (don’t add a pill)
    if (!isValidEmail(trimmed)) return;

    // Avoid dupes (case-insensitive)
    const exists = emails.some((e) => e.toLowerCase() === trimmed.toLowerCase());
    if (exists) {
      setEmailInput("");
      return;
    }

    setEmails((prev) => [...prev, trimmed]);
    setEmailInput("");

    // If previously failed, clear error for this email (case-insensitive)
    if (errorMap.size > 0) {
      setEmailErrors((prev) => prev.filter((e) => e.email.toLowerCase() !== trimmed.toLowerCase()));
    }
  };

  const removeEmail = (emailToRemove: string) => {
    setEmails((prev) => prev.filter((e) => e !== emailToRemove));
    setEmailErrors((prev) => prev.filter((e) => e.email.toLowerCase() !== emailToRemove.toLowerCase()));
  };

  const handleEmailInputKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === "Enter" || e.key === "," || e.key === " ") {
      e.preventDefault();
      addEmail(emailInput);
    } else if (e.key === "Backspace" && emailInput === "" && emails.length > 0) {
      // Match project modal behavior: remove last pill
      setEmails((prev) => prev.slice(0, -1));
    }
  };

  const handlePaste = (e: React.ClipboardEvent<HTMLInputElement>) => {
    e.preventDefault();
    const pasted = e.clipboardData.getData("text");
    const parts = pasted.split(/[\s,;]+/).filter(Boolean);

    const next: string[] = [];
    for (const p of parts) {
      const trimmed = normalize(p);
      if (!trimmed || !isValidEmail(trimmed)) continue;
      const already =
        emails.some((x) => x.toLowerCase() === trimmed.toLowerCase()) ||
        next.some((x) => x.toLowerCase() === trimmed.toLowerCase());
      if (!already) next.push(trimmed);
    }

    if (next.length > 0) {
      setEmails((prev) => [...prev, ...next]);
      setEmailInput("");
    }
  };

  const handleEmailInputBlur = () => {
    // Mirrors project modal: commit on blur
    addEmail(emailInput);
  };

  const handleClose = () => {
    if (!isProcessing && !modalLoading) {
      onClose();
    }
  };

  const handleSubmit = async () => {
    if (modalLoading || isProcessing) return;

    // Commit whatever is in the input first (if valid)
    const trimmed = normalize(emailInput);
    const shouldAdd =
      trimmed &&
      isValidEmail(trimmed) &&
      !emails.some((e) => e.toLowerCase() === trimmed.toLowerCase());

    const finalEmails = shouldAdd ? [...emails, trimmed] : [...emails];

    if (finalEmails.length === 0) return;

    setIsProcessing(true);
    setEmailErrors([]);

    try {
      const results = await onInvite(finalEmails);

      if (results.failed.length > 0) {
        // Keep only failed emails and show errors (mirrors project modal behavior)
        const failedEmails = results.failed.map((f) => f.email);
        setEmails(failedEmails);
        setEmailErrors(results.failed.map((f) => ({ email: f.email, error: f.error })));
        setEmailInput("");
      } else {
        // All succeeded
        onClose();
      }
    } finally {
      setIsProcessing(false);
    }
  };

  if (!isOpen) return null;

  const totalCount = emails.length;
  const canSubmit = totalCount > 0 || (normalize(emailInput) !== "" && isValidEmail(emailInput));
  const disabled = !canSubmit || isProcessing || modalLoading;

  return (
    <div className="modal modal-open">
      <div className="modal-box max-w-2xl overflow-visible">
        {/* Header */}
        <div className="flex justify-between items-center mb-6">
          <h3 className="font-bold text-2xl">Invite people</h3>
          <button
            className="btn btn-sm btn-circle btn-ghost"
            onClick={handleClose}
            disabled={isProcessing || modalLoading}
            aria-label="Close"
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
              {/* Emails Input (Project modal look & feel) */}
              <div className="form-control">
                <label className="label">
                  <span className="label-text font-semibold text-base">
                    Email addresses <span className="text-error">*</span>
                  </span>
                </label>

                <div
                  className={[
                    "border border-base-300 rounded-lg p-2 min-h-[120px] max-h-[300px] overflow-y-auto bg-base-100",
                    "focus-within:border-primary focus-within:outline-none focus-within:ring-2 focus-within:ring-primary focus-within:ring-opacity-50",
                    hasErrors ? "border-error" : "",
                  ].join(" ")}
                >
                  <div className="flex flex-wrap gap-2">
                    {/* Email pills */}
                    {emails.map((email) => {
                      const err = errorMap.get(email.toLowerCase());
                      const hasErr = Boolean(err);

                      return (
                        <div
                          key={email}
                          className={[
                            "badge badge-lg gap-2 px-3 py-3 border",
                            hasErr
                              ? "bg-error/10 border-error"
                              : "bg-base-200 border-base-300",
                          ].join(" ")}
                          title={hasErr ? err : undefined}
                        >
                          {hasErr && <ExclamationCircleIcon className="w-4 h-4 text-error" />}
                          <span className={["text-sm font-medium", hasErr ? "text-error" : ""].join(" ")}>
                            {email}
                          </span>
                          <button
                            className="btn btn-ghost btn-xs btn-circle hover:bg-base-300"
                            onClick={() => removeEmail(email)}
                            disabled={isProcessing}
                            aria-label={`Remove ${email}`}
                            type="button"
                          >
                            <XMarkIcon className="w-3 h-3" />
                          </button>
                        </div>
                      );
                    })}

                    {/* Input */}
                    <input
                      type="text"
                      className="flex-1 min-w-[220px] outline-none bg-transparent p-2"
                      placeholder={emails.length === 0 ? "Enter email addresses..." : ""}
                      value={emailInput}
                      onChange={(e) => setEmailInput(e.target.value)}
                      onKeyDown={handleEmailInputKeyDown}
                      onPaste={handlePaste}
                      onBlur={handleEmailInputBlur}
                      disabled={isProcessing}
                      autoFocus
                    />
                  </div>
                </div>

                <label className="label">
                  <span className="label-text-alt text-base-content/60">
                    Type email addresses and press Enter, comma, or space to add
                  </span>
                </label>

                {/* Optional inline hint if input is non-empty and invalid */}
                {normalize(emailInput) !== "" && !isValidEmail(emailInput) && (
                  <div className="mt-2 text-sm text-error flex items-center gap-2">
                    <ExclamationCircleIcon className="w-5 h-5" />
                    <span>That doesn’t look like a valid email address.</span>
                  </div>
                )}
              </div>

              {/* Error Messages (Project modal style) */}
              {hasErrors && (
                <div className="alert alert-error">
                  <div className="w-full">
                    <h4 className="font-semibold mb-2">
                      {emailErrors.length} invitation(s) failed:
                    </h4>
                    <ul className="text-sm space-y-1">
                      {emailErrors.map((err, idx) => (
                        <li key={`${err.email}-${idx}`}>
                          <strong>{err.email}:</strong> {err.error}
                        </li>
                      ))}
                    </ul>
                  </div>
                </div>
              )}

              {/* Info (keep it lightweight and aligned with project modal) */}
              {!hasErrors && (
                <div className="alert alert-info">
                  <EnvelopeIcon className="w-6 h-6" />
                  <div>
                    <h4 className="font-semibold">Email invitations</h4>
                    <p className="text-sm">Invitations will be sent to each address you add.</p>
                  </div>
                </div>
              )}
            </div>

            {/* Modal Actions */}
            <div className="modal-action">
              <button
                className="btn btn-ghost"
                onClick={handleClose}
                disabled={isProcessing || modalLoading}
              >
                Cancel
              </button>

              <button
                className={`btn btn-primary gap-2 ${disabled ? "btn-disabled" : ""}`}
                disabled={disabled}
                onClick={handleSubmit}
              >
                {isProcessing ? (
                  <>
                    <span className="loading loading-spinner loading-sm" />
                    Processing...
                  </>
                ) : (
                  <>
                    <EnvelopeIcon className="w-5 h-5" />
                    {hasErrors ? "Retry failed invitations" : "Send invitations"}
                    {emails.length > 0 && ` (${emails.length})`}
                  </>
                )}
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