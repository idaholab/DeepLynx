import React, { useState, useEffect, useMemo } from "react";
import { XMarkIcon, MagnifyingGlassIcon } from "@heroicons/react/24/outline";
import {
  UserResponseDto,
  RoleResponseDto,
  ProjectMemberResponseDto,
} from "@/app/(home)/types/responseDTOs";
import { useLanguage } from "@/app/contexts/Language";

/* -------------------------------------------------------------------------- */
/*                     Bulk Invite/Add Users to Project Modal                */
/* -------------------------------------------------------------------------- */

interface AddUsersToProjectModalProps {
  isOpen: boolean;
  roles: RoleResponseDto[];
  availableOrgUsers: UserResponseDto[];
  projectMembers: ProjectMemberResponseDto[];
  modalLoading?: boolean;
  onClose: () => void;
  onAddInviteUser: (emailOrUserId: string | number, roleId?: number) => Promise<void>;
}

interface EmailError {
  email: string;
  error: string;
}

const AddUsersToProjectModal: React.FC<AddUsersToProjectModalProps> = ({
  isOpen,
  roles,
  availableOrgUsers,
  projectMembers,
  modalLoading = false,
  onClose,
  onAddInviteUser,
}) => {
  const { t } = useLanguage();
  
  const [externalEmailInput, setExternalEmailInput] = useState<string>("");
  const [externalEmails, setExternalEmails] = useState<string[]>([]);
  const [selectedOrgUserIds, setSelectedOrgUserIds] = useState<number[]>([]);
  const [selectedRoleId, setSelectedRoleId] = useState<string>("");
  const [isProcessing, setIsProcessing] = useState(false);
  const [emailErrors, setEmailErrors] = useState<EmailError[]>([]);
  const [searchQuery, setSearchQuery] = useState<string>("");

  // Filter out users who are already in the project
  const usersNotInProject = useMemo(() => {
    // Get IDs and names of users already in the project (those with emails)
    const existingUserIds = new Set(
      projectMembers
        .filter(member => member.email !== "" && member.memberId !== undefined) // Users have emails
        .map(member => member.memberId!)
    );
    
    const existingUserNames = new Set(
      projectMembers
        .filter(member => member.email !== "")
        .map(member => member.name.toLowerCase())
    );

    return availableOrgUsers.filter(user => 
      !existingUserIds.has(user.id) && 
      !existingUserNames.has(user.name.toLowerCase())
    );
  }, [availableOrgUsers, projectMembers]);

  // Reset state when modal opens
  useEffect(() => {
    if (isOpen) {
      setExternalEmailInput("");
      setExternalEmails([]);
      setSelectedOrgUserIds([]);
      setSelectedRoleId("");
      setEmailErrors([]);
      setSearchQuery("");
    }
  }, [isOpen]);

  if (!isOpen) return null;

  const handleClose = () => {
    if (!isProcessing) {
      onClose();
    }
  };

  const toggleOrgUser = (userId: number) => {
    setSelectedOrgUserIds((prev) =>
      prev.includes(userId)
        ? prev.filter((id) => id !== userId)
        : [...prev, userId]
    );
  };

  const removeSelectedUser = (userId: number) => {
    setSelectedOrgUserIds((prev) => prev.filter((id) => id !== userId));
  };

  const removeExternalEmail = (email: string) => {
    setExternalEmails((prev) => prev.filter((e) => e !== email));
  };

  const handleEmailInputKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === "Enter" || e.key === "," || e.key === " ") {
      e.preventDefault();
      const email = externalEmailInput.trim();
      if (email && !externalEmails.includes(email)) {
        setExternalEmails((prev) => [...prev, email]);
        setExternalEmailInput("");
      }
    } else if (e.key === "Backspace" && externalEmailInput === "" && externalEmails.length > 0) {
      setExternalEmails((prev) => prev.slice(0, -1));
    }
  };

  const handleEmailInputBlur = () => {
    const email = externalEmailInput.trim();
    if (email && !externalEmails.includes(email)) {
      setExternalEmails((prev) => [...prev, email]);
      setExternalEmailInput("");
    }
  };

  const handleSubmit = async () => {
    if (!selectedRoleId) return;

    setIsProcessing(true);
    setEmailErrors([]);
    const errors: EmailError[] = [];

    const roleId = Number(selectedRoleId);

    // Process org users first (pass userId and roleId)
    for (const userId of selectedOrgUserIds) {
      try {
        await onAddInviteUser(userId, roleId);
      } catch (error: any) {
        const user = usersNotInProject.find((u) => u.id === userId);
        errors.push({
          email: user?.email || user?.name || `User ${userId}`,
          error: error?.message || "Failed to add user",
        });
      }
    }

    // Process external emails (pass email string only, no roleId for organization invites)
    const failedEmails: string[] = [];

    for (const email of externalEmails) {
      try {
        await onAddInviteUser(email);
      } catch (error: any) {
        failedEmails.push(email);
        errors.push({
          email,
          error: error?.message || "Failed to send invitation",
        });
      }
    }

    setEmailErrors(errors);
    setIsProcessing(false);

    // Keep only failed emails
    setExternalEmails(failedEmails);

    // Clear successfully added org users
    const failedOrgUserEmails = errors
      .map((e) => e.email)
      .filter((email) =>
        usersNotInProject.some((u) => u.email === email || u.name === email)
      );
    
    setSelectedOrgUserIds((prev) =>
      prev.filter((id) => {
        const user = usersNotInProject.find((u) => u.id === id);
        return failedOrgUserEmails.includes(user?.email || user?.name || "");
      })
    );

    // If all succeeded, close modal
    if (errors.length === 0) {
      handleClose();
    }
  };

  const totalSelectedCount = selectedOrgUserIds.length + externalEmails.length;

  const canSubmit =
    selectedRoleId &&
    totalSelectedCount > 0 &&
    !isProcessing &&
    !modalLoading;

  const hasErrors = emailErrors.length > 0;

  // Filter users based on search query
  const filteredOrgUsers = usersNotInProject.filter((user) => {
    const searchLower = searchQuery.toLowerCase();
    return (
      user.name.toLowerCase().includes(searchLower) ||
      user.email?.toLowerCase().includes(searchLower)
    );
  });

  // Get selected users for display
  const selectedUsers = usersNotInProject.filter((user) =>
    selectedOrgUserIds.includes(user.id)
  );

  return (
    <div className="modal modal-open">
      <div className="modal-box max-w-5xl overflow-visible">
        {/* Header */}
        <div className="flex justify-between items-center mb-6">
          <h3 className="font-bold text-2xl">
            {t.translations.ADD_USER || "Add people"}
          </h3>
          <button
            className="btn btn-sm btn-circle btn-ghost"
            onClick={handleClose}
            disabled={isProcessing || modalLoading}
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
            <div className="grid grid-cols-2 gap-6">
              {/* Left Side - Names/Emails Input and Role Selection */}
              <div className="space-y-4">
                {/* External Emails Input */}
                <div className="form-control">
                  <label className="label">
                    <span className="label-text font-semibold text-base">
                      {t.translations.INVITE_EXTERNAL_USERS_VIA_EMAIL}
                    </span>
                  </label>
                  <div className="border border-base-300 rounded-lg p-2 min-h-[120px] max-h-[300px] overflow-y-auto bg-base-100 focus-within:border-primary focus-within:outline-none focus-within:ring-2 focus-within:ring-primary focus-within:ring-opacity-50">
                    <div className="flex flex-wrap gap-2">
                      {/* External Emails as Pills */}
                      {externalEmails.map((email) => (
                        <div
                          key={`email-${email}`}
                          className="badge badge-lg gap-2 bg-base-200 border-base-300 px-3 py-3"
                        >
                          <span className="text-sm font-medium">{email}</span>
                          <button
                            className="btn btn-ghost btn-xs btn-circle hover:bg-base-300"
                            onClick={() => removeExternalEmail(email)}
                            disabled={isProcessing}
                          >
                            <XMarkIcon className="w-3 h-3" />
                          </button>
                        </div>
                      ))}
                      
                      {/* Input Field */}
                      <input
                        type="text"
                        className="flex-1 min-w-[200px] outline-none bg-transparent p-2"
                        placeholder={externalEmails.length === 0 ? `${t.translations.ENTER_EMAIL_ADDRESSES}` : ""}
                        value={externalEmailInput}
                        onChange={(e) => setExternalEmailInput(e.target.value)}
                        onKeyDown={handleEmailInputKeyDown}
                        onBlur={handleEmailInputBlur}
                        disabled={isProcessing}
                      />
                    </div>
                  </div>
                  <label className="label">
                    <span className="label-text-alt text-base-content/60">
                      {t.translations.ADD_EMAIL_ADDRESSES_HELPER_TEXT}
                    </span>
                  </label>
                </div>

                {/* Selected Organization Users Display */}
                {selectedUsers.length > 0 && (
                  <div className="form-control">
                    <label className="label">
                      <span className="label-text font-semibold text-base">
                        {t.translations.SELECTED_ORGANIZATION_USERS}
                      </span>
                      <span className="label-text-alt text-base-content/60">
                        ({selectedUsers.length} {t.translations.SELECTED})
                      </span>
                    </label>
                    <div className="border border-base-300 rounded-lg p-2 max-h-[200px] overflow-y-auto bg-base-100">
                      <div className="flex flex-wrap gap-2">
                        {selectedUsers.map((user) => (
                          <div
                            key={`org-${user.id}`}
                            className="badge badge-lg gap-2 bg-primary/10 border-primary/30 px-3 py-3"
                          >
                            <span className="text-sm font-medium">{user.name}</span>
                            <button
                              className="btn btn-ghost btn-xs btn-circle hover:bg-primary/20"
                              onClick={() => removeSelectedUser(user.id)}
                              disabled={isProcessing}
                            >
                              <XMarkIcon className="w-3 h-3" />
                            </button>
                          </div>
                        ))}
                      </div>
                    </div>
                  </div>
                )}

                {/* Role Selection */}
                <div className="form-control">
                  <label className="label">
                    <span className="label-text font-medium">
                      {t.translations.ROLE} <span className="text-error">*</span>
                    </span>
                  </label>
                  <div className="dropdown dropdown-bottom w-full">
                    <div
                      tabIndex={0}
                      role="button"
                      className={`select select-bordered w-full flex items-center justify-between ${
                        isProcessing ? "select-disabled" : ""
                      }`}
                    >
                      <span
                        className={selectedRoleId ? "" : "text-base-content/50"}
                      >
                        {selectedRoleId
                          ? roles.find((r) => r.id === Number(selectedRoleId))
                              ?.name || `${t.translations.SELECT_ROLE}`
                          : `${t.translations.SELECT_ROLE}`}
                      </span>
                    </div>
                    <ul
                      tabIndex={0}
                      className="dropdown-content menu bg-base-100 rounded-box z-[100] w-full p-2 shadow-lg border border-base-300 max-h-60 overflow-y-auto mt-1"
                    >
                      {roles.map((role) => (
                        <li
                          key={role.id}
                          onClick={() => setSelectedRoleId(role.id.toString())}
                        >
                          <a>
                            <span>{role.name}</span>
                            {!role.projectId && (
                              <span className="badge badge-primary badge-sm">
                                Org
                              </span>
                            )}
                          </a>
                        </li>
                      ))}
                    </ul>
                  </div>
                </div>

                {/* Error Messages */}
                {hasErrors && (
                  <div className="alert alert-error">
                    <div className="w-full">
                      <h4 className="font-semibold mb-2">
                        {emailErrors.length} {t.translations.USERS_FAILED_TO_ADD}:
                      </h4>
                      <ul className="text-sm space-y-1">
                        {emailErrors.map((err, idx) => (
                          <li key={idx}>
                            <strong>{err.email}:</strong> {err.error}
                          </li>
                        ))}
                      </ul>
                    </div>
                  </div>
                )}
              </div>

              {/* Right Side - Organization Users List */}
              <div className="form-control">
                <label className="label">
                  <span className="label-text font-semibold text-base">
                    {t.translations.SELECT_ORGANIZATION_USERS}
                  </span>
                  <span className="label-text-alt text-base-content/60">
                    ({usersNotInProject.length} {t.translations.AVAILABLE})
                  </span>
                </label>

                {/* Search Input */}
                <div className="relative mb-2">
                  <MagnifyingGlassIcon className="w-5 h-5 absolute left-3 top-1/2 -translate-y-1/2 text-base-content/60" />
                  <input
                    type="text"
                    placeholder={`${t.translations.SEARCH_BY_NAME_OR_EMAIL}...`}
                    className="input input-bordered w-full pl-10"
                    value={searchQuery}
                    onChange={(e) => setSearchQuery(e.target.value)}
                    disabled={isProcessing}
                  />
                </div>

                {/* Users List */}
                <div className="border border-base-300 rounded-lg p-3 h-[400px] overflow-y-auto bg-base-200">
                  {filteredOrgUsers.length === 0 ? (
                    <div className="flex items-center justify-center h-full text-base-content/50 text-sm">
                      {searchQuery
                        ? "No users match your search"
                        : usersNotInProject.length === 0
                        ? `${t.translations.ALL_ORG_USERS_ALREADY_IN_PROJECT}`
                        : `${t.translations.NO_AVAILABLE_USERS}`}
                    </div>
                  ) : (
                    <ul className="space-y-2">
                      {filteredOrgUsers.map((user) => {
                        const isSelected = selectedOrgUserIds.includes(user.id);
                        return (
                          <li key={user.id}>
                            <label
                              className={`flex items-center gap-3 p-2 rounded cursor-pointer hover:bg-base-100 transition-colors ${
                                isSelected ? "bg-primary/10" : ""
                              }`}
                            >
                              <input
                                type="checkbox"
                                className="checkbox checkbox-sm checkbox-primary"
                                checked={isSelected}
                                onChange={() => toggleOrgUser(user.id)}
                                disabled={isProcessing}
                              />
                              <div className="flex-1 min-w-0">
                                <div className="text-sm font-medium truncate">
                                  {user.name}
                                </div>
                                {user.email && (
                                  <div className="text-xs text-base-content/60 truncate">
                                    {user.email}
                                  </div>
                                )}
                              </div>
                            </label>
                          </li>
                        );
                      })}
                    </ul>
                  )}
                </div>
              </div>
            </div>

            {/* Modal Actions */}
            <div className="modal-action">
              <button
                className="btn btn-ghost"
                onClick={handleClose}
                disabled={isProcessing || modalLoading}
              >
                {t.translations.CANCEL || "Cancel"}
              </button>
              <button
                className={`btn btn-primary gap-2 ${
                  !canSubmit ? "btn-disabled" : ""
                }`}
                disabled={!canSubmit}
                onClick={handleSubmit}
              >
                {isProcessing ? (
                  <>
                    <span className="loading loading-spinner loading-sm" />
                    {t.translations.PROCESSING}
                  </>
                ) : (
                  <>
                    {totalSelectedCount > 0
                      ? `${t.translations.ADD} ${totalSelectedCount} ${totalSelectedCount === 1 ? `${t.translations.PERSON}` : `${t.translations.PEOPLE}`}`
                      : `${t.translations.ADD_PEOPLE}`}
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

export default AddUsersToProjectModal;