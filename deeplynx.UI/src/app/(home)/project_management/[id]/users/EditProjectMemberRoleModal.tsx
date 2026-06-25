"use client";

import React from "react";
import { EditRoleModalState } from "../../types/projectUsersTypes";
import { RoleResponseDto } from "@/app/(home)/types/responseDTOs";

/* -------------------------------------------------------------------------- */
/*                           Edit Member Role Modal                           */
/* -------------------------------------------------------------------------- */

interface EditProjectMemberRoleModalProps {
  editRoleModal: EditRoleModalState;
  roles: RoleResponseDto[];
  loading: boolean;
  selectedRoleId: string;
  isProjectAdmin: boolean;
  onChangeRole: (value: string) => void;
  onChangeIsProjectAdmin: (value: boolean) => void;
  onCancel: () => void;
  onSave: () => void;
}

const EditProjectMemberRoleModal: React.FC<EditProjectMemberRoleModalProps> = ({
  editRoleModal,
  roles,
  loading,
  selectedRoleId,
  isProjectAdmin,
  onChangeRole,
  onChangeIsProjectAdmin,
  onCancel,
  onSave,
}) => {
  if (!editRoleModal.isOpen) return null;

  return (
    <dialog className="modal modal-open">
      <div className="modal-box">
        <h3 className="font-bold text-lg">Edit Member Role</h3>
        <p className="py-2 text-sm text-base-content/70">
          Change the role for{" "}
          <span className="font-semibold">{editRoleModal.memberName}</span> in
          this project.
        </p>

        <div className="form-control mt-4">
          <label className="label">
            <span className="label-text">Role</span>
          </label>
          <select
            className="select select-bordered w-full"
            value={selectedRoleId}
            onChange={(e) => onChangeRole(e.target.value)}
            disabled={loading}
          >
            <option value="">Select a role</option>
            {roles.map((r) => (
              <option key={r.id} value={r.id}>
                {r.name}
              </option>
            ))}
          </select>
        </div>

        {/* Project Admin Checkbox */}
        <div className="form-control mt-4">
          <label className="label cursor-pointer justify-start gap-3">
            <input
              type="checkbox"
              className="checkbox checkbox-warning"
              checked={isProjectAdmin}
              onChange={(e) => onChangeIsProjectAdmin(e.target.checked)}
              disabled={loading}
            />
            <div className="flex flex-col">
              <span className="label-text font-medium">
                Project Admin
              </span>
              <span className="text-xs text-base-content/60">
                Grant project admin permissions
              </span>
            </div>
          </label>
        </div>

        <div className="modal-action">
          <button
            className="btn btn-ghost"
            onClick={onCancel}
            disabled={loading}
          >
            Cancel
          </button>
          <button
            className="btn btn-primary"
            onClick={onSave}
            disabled={loading}
          >
            {loading ? "Saving..." : "Save"}
          </button>
        </div>
      </div>
    </dialog>
  );
};

export default EditProjectMemberRoleModal;
