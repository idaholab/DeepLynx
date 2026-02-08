"use client";

import React from "react";
import {
  UserResponseDto,
  GroupResponseDto,
  RoleResponseDto,
} from "@/app/(home)/types/responseDTOs";
import { AddMemberModalState, MemberType } from "../../types/projectUsersTypes";
import { useLanguage } from "@/app/contexts/Language";

/* -------------------------------------------------------------------------- */
/*                           Add Member Modal Component                       */
/* -------------------------------------------------------------------------- */

interface AddProjectMemberModalProps {
  addModal: AddMemberModalState;
  roles: RoleResponseDto[];
  availableUsers: UserResponseDto[];
  availableGroups: GroupResponseDto[];
  selectedMemberId: string;
  selectedRoleId: string;
  modalLoading: boolean;
  onClose: () => void;
  onChangeMember: (value: string) => void;
  onChangeRole: (value: string) => void;
  onConfirm: () => void;
}

const AddProjectMemberModal: React.FC<AddProjectMemberModalProps> = ({
  addModal,
  roles,
  availableUsers,
  availableGroups,
  selectedMemberId,
  selectedRoleId,
  modalLoading,
  onClose,
  onChangeMember,
  onChangeRole,
  onConfirm,
}) => {
  const { t } = useLanguage();
  if (!addModal.isOpen) return null;

  const labelForType: Record<MemberType, string> = {
    user: t.translations.USER,
    group: t.translations.GROUP,
  };

  // Get selected member display text
  const getSelectedMemberDisplay = () => {
    if (!selectedMemberId) {
      return addModal.memberType === "user"
        ? t.translations.SELECT_A_USER
        : t.translations.SELECT_A_GROUP;
    }

    if (addModal.memberType === "user") {
      const user = availableUsers.find(
        (u) => u.id === Number(selectedMemberId),
      );
      return user
        ? `${user.name}${user.email ? ` (${user.email})` : ""}`
        : t.translations.SELECT_A_USER;
    } else {
      const group = availableGroups.find(
        (g) => g.id === Number(selectedMemberId),
      );
      return group ? group.name : t.translations.SELECT_A_GROUP;
    }
  };

  return (
    <dialog className="modal modal-open">
      <div className="modal-box overflow-visible">
        <h3 className="font-bold text-lg">
          {addModal.memberType === "user"
            ? t.translations.ADD_USER_TO_PROJECT
            : t.translations.ADD_GROUP_TO_PROJECT}
        </h3>
        <p className="py-2 text-sm text-base-content/70">
          {addModal.memberType === "user"
            ? t.translations.ADD_USER_TO_PROJECT_DESCRIPTION
            : t.translations.ADD_GROUP_TO_PROJECT_DESCRIPTION}
        </p>

        {/* Member select - Custom Dropdown */}
        <div className="form-control mt-4">
          <label className="label">
            <span className="label-text capitalize">
              {labelForType[addModal.memberType]}
            </span>
          </label>
          <div className="dropdown dropdown-bottom w-full">
            <div
              tabIndex={0}
              role="button"
              className={`select select-bordered w-full flex items-center justify-between ${modalLoading ? "select-disabled" : ""}`}
            >
              <span className={selectedMemberId ? "" : "text-base-content/50"}>
                {getSelectedMemberDisplay()}
              </span>
            </div>
            <ul
              tabIndex={0}
              className="dropdown-content menu bg-base-100 rounded-box z-[100] w-full p-2 shadow-lg border border-base-300 max-h-60 overflow-y-auto mt-1"
            >
              {addModal.memberType === "user"
                ? availableUsers.map((u) => (
                    <li
                      key={u.id}
                      onClick={() => onChangeMember(u.id.toString())}
                    >
                      <a>
                        <span>
                          {u.name} {u.email ? `(${u.email})` : ""}
                        </span>
                      </a>
                    </li>
                  ))
                : availableGroups.map((g) => (
                    <li
                      key={g.id}
                      onClick={() => onChangeMember(g.id.toString())}
                      className=""
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
        <div className="form-control mt-4">
          <label className="label">
            <span className="label-text">{t.translations.ROLE}</span>
          </label>
          <div className="dropdown dropdown-bottom w-full">
            <div
              tabIndex={0}
              role="button"
              className={`select select-bordered w-full flex items-center justify-between ${modalLoading ? "select-disabled" : ""}`}
            >
              <span className={selectedRoleId ? "" : "text-base-content/50"}>
                {selectedRoleId
                  ? roles.find((r) => r.id === Number(selectedRoleId))?.name ||
                    t.translations.SELECT_A_ROLE
                  : t.translations.SELECT_A_ROLE}
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
                  className=""
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
            disabled={modalLoading}
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

export default AddProjectMemberModal;
