"use client";

import React, { useState } from "react";
import {
  GroupResponseDto,
  RoleResponseDto,
  UserResponseDto,
} from "@/app/(home)/types/responseDTOs";
import { useLanguage } from "@/app/contexts/Language";
import { UserIcon, MagnifyingGlassIcon } from "@heroicons/react/24/outline";

/* -------------------------------------------------------------------------- */
/*                        Add Group To Project Modal                          */
/* -------------------------------------------------------------------------- */

// Temporary dummy data for group members
const DUMMY_GROUP_MEMBERS: UserResponseDto[] = [
  { id: 1, name: "Alice Johnson", email: "alice.johnson@example.com" },
  { id: 2, name: "Bob Smith", email: "bob.smith@example.com" },
  { id: 3, name: "Carol Williams", email: "carol.williams@example.com" },
  { id: 4, name: "David Brown", email: "david.brown@example.com" },
  { id: 5, name: "Emma Davis", email: "emma.davis@example.com" },
  { id: 6, name: "Frank Miller", email: "frank.miller@example.com" },
  { id: 7, name: "Grace Wilson", email: "grace.wilson@example.com" },
  { id: 8, name: "Henry Moore", email: "henry.moore@example.com" },
  { id: 9, name: "Iris Taylor", email: "iris.taylor@example.com" },
  { id: 10, name: "Jack Anderson", email: "jack.anderson@example.com" },
] as UserResponseDto[];

interface AddGroupToProjectModalProps {
  isOpen: boolean;
  roles: RoleResponseDto[];
  availableGroups: GroupResponseDto[];
  selectedGroupId: string;
  selectedRoleId: string;
  modalLoading: boolean;
  onClose: () => void;
  onChangeGroup: (value: string) => void;
  onChangeRole: (value: string) => void;
  onConfirm: () => void;
}

const AddGroupToProjectModal: React.FC<AddGroupToProjectModalProps> = ({
  isOpen,
  roles,
  availableGroups,
  selectedGroupId,
  selectedRoleId,
  modalLoading,
  onClose,
  onChangeGroup,
  onChangeRole,
  onConfirm,
}) => {
  const { t } = useLanguage();
  const [memberSearchQuery, setMemberSearchQuery] = useState<string>("");
  
  if (!isOpen) return null;

  // TODO: Replace with actual API call to get group members
  const groupMembers = selectedGroupId ? DUMMY_GROUP_MEMBERS : [];

  // Filter group members based on search query
  const filteredGroupMembers = groupMembers.filter((member) => {
    const searchLower = memberSearchQuery.toLowerCase();
    return (
      member.name.toLowerCase().includes(searchLower) ||
      member.email?.toLowerCase().includes(searchLower)
    );
  });

  // Get selected group display text
  const getSelectedGroupDisplay = () => {
    if (!selectedGroupId) {
      return t.translations.SELECT_A_GROUP;
    }
    const group = availableGroups.find((g) => g.id === Number(selectedGroupId));
    return group ? group.name : t.translations.SELECT_A_GROUP;
  };

  // Get selected role display text
  const getSelectedRoleDisplay = () => {
    if (!selectedRoleId) {
      return t.translations.SELECT_A_ROLE;
    }
    return (
      roles.find((r) => r.id === Number(selectedRoleId))?.name ||
      t.translations.SELECT_A_ROLE
    );
  };

  return (
    <dialog className="modal modal-open">
      <div className="modal-box max-w-4xl overflow-visible">
        <h3 className="font-bold text-lg">
          {t.translations.ADD_GROUP_TO_PROJECT}
        </h3>
        <p className="py-2 text-sm text-base-content/70">
          {t.translations.ADD_GROUP_TO_PROJECT_DESCRIPTION}
        </p>

        <div className="grid grid-cols-2 gap-6 mt-4">
          {/* Left Side - Group Selection */}
          <div className="space-y-4">
            {/* Group select - Custom Dropdown */}
            <div className="form-control">
              <label className="label">
                <span className="label-text">{t.translations.GROUP}</span>
              </label>
              <div className="dropdown dropdown-bottom w-full">
                <div
                  tabIndex={0}
                  role="button"
                  className={`select select-bordered w-full flex items-center justify-between ${modalLoading ? "select-disabled" : ""}`}
                >
                  <span
                    className={selectedGroupId ? "" : "text-base-content/50"}
                  >
                    {getSelectedGroupDisplay()}
                  </span>
                </div>
                <ul
                  tabIndex={0}
                  className="dropdown-content menu bg-base-100 rounded-box z-[100] w-full p-2 shadow-lg border border-base-300 max-h-60 overflow-y-auto mt-1"
                >
                  {availableGroups.map((g) => (
                    <li
                      key={g.id}
                      onClick={() => onChangeGroup(g.id.toString())}
                    >
                      <a>
                        <span>{g.name}</span>
                      </a>
                    </li>
                  ))}
                </ul>
              </div>
            </div>

            {/* Role select - Custom Dropdown */}
            <div className="form-control">
              <label className="label">
                <span className="label-text">{t.translations.ROLE}</span>
              </label>
              <div className="dropdown dropdown-bottom w-full">
                <div
                  tabIndex={0}
                  role="button"
                  className={`select select-bordered w-full flex items-center justify-between ${modalLoading ? "select-disabled" : ""}`}
                >
                  <span
                    className={selectedRoleId ? "" : "text-base-content/50"}
                  >
                    {getSelectedRoleDisplay()}
                  </span>
                </div>
                <ul
                  tabIndex={0}
                  className="dropdown-content menu bg-base-100 rounded-box z-[100] w-full p-2 shadow-lg border border-base-300 max-h-60 overflow-y-auto mt-1"
                >
                  {roles.map((r) => (
                    <li
                      key={r.id}
                      onClick={() => onChangeRole(r.id.toString())}
                    >
                      <a>
                        <span>{r.name}</span>
                        {!r.projectId && (
                          <span className="badge badge-primary badge-sm">
                            {t.translations.ORG}
                          </span>
                        )}
                      </a>
                    </li>
                  ))}
                </ul>
              </div>
            </div>
          </div>

          {/* Right Side - Group Members Preview */}
          <div className="form-control">
            <label className="label">
              <span className="label-text">Group Members</span>
              <span className="label-text-alt text-base-content/60">
                {groupMembers.length}{" "}
                {groupMembers.length === 1
                  ? t.translations.MEMBER
                  : t.translations.MEMBERS}
              </span>
            </label>

            {/* Search Input for Group Members */}
            {selectedGroupId && groupMembers.length > 0 && (
              <div className="relative mb-2">
                <MagnifyingGlassIcon className="w-5 h-5 absolute left-3 top-1/2 -translate-y-1/2 text-base-content/60" />
                <input
                  type="text"
                  placeholder="Search members..."
                  className="input input-bordered input-sm w-full pl-10"
                  value={memberSearchQuery}
                  onChange={(e) => setMemberSearchQuery(e.target.value)}
                  disabled={modalLoading}
                />
              </div>
            )}

            <div className="border border-base-300 rounded-lg p-3 max-h-96 overflow-y-auto bg-base-200">
              {!selectedGroupId ? (
                <div className="flex items-center justify-center h-full min-h-[100px] text-base-content/50 text-sm">
                  Select a group to view members
                </div>
              ) : groupMembers.length === 0 ? (
                <div className="flex items-center justify-center h-full min-h-[100px] text-base-content/50 text-sm">
                  No Members
                </div>
              ) : filteredGroupMembers.length === 0 ? (
                <div className="flex items-center justify-center h-full min-h-[100px] text-base-content/50 text-sm">
                  No members match your search
                </div>
              ) : (
                <ul className="space-y-2">
                  {filteredGroupMembers.map((member) => (
                    <li
                      key={member.id}
                      className="flex items-center gap-2 p-2 rounded bg-base-100"
                    >
                      <UserIcon className="w-4 h-4 text-base-content/60" />
                      <div className="flex-1 min-w-0">
                        <div className="text-sm font-medium truncate">
                          {member.name}
                        </div>
                        {member.email && (
                          <div className="text-xs text-base-content/60 truncate">
                            {member.email}
                          </div>
                        )}
                      </div>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          </div>
        </div>

        <div className="modal-action">
          <button
            className="btn btn-ghost"
            onClick={onClose}
            disabled={modalLoading}
          >
            {t.translations.CANCEL}
          </button>
          <button
            className="btn btn-primary"
            onClick={onConfirm}
            disabled={modalLoading || !selectedGroupId || !selectedRoleId}
          >
            {modalLoading
              ? t.translations.ADDING
              : t.translations.ADD_TO_PROJECT}
          </button>
        </div>
      </div>
    </dialog>
  );
};

export default AddGroupToProjectModal;