"use client";

import React from "react";
import { UserGroupIcon, PencilIcon, TrashIcon } from "@heroicons/react/24/outline";
import {
  ProjectMemberTableRow,
  MemberType,
} from "../../types/projectUsersTypes";

/* -------------------------------------------------------------------------- */
/*                          Users & Groups Data Table                         */
/* -------------------------------------------------------------------------- */

interface ProjectUsersListTableProps {
  tableData: ProjectMemberTableRow[];
  loading: boolean;
  onEditRole: (row: ProjectMemberTableRow) => void;
  onViewGroupMembers: (row: ProjectMemberTableRow) => void;
  onOpenRemoveModal: (payload: {
    memberId: number;
    memberName: string;
    memberType: MemberType;
  }) => void;
}

const ProjectUsersListTable: React.FC<ProjectUsersListTableProps> = ({
  tableData,
  loading,
  onEditRole,
  onViewGroupMembers,
  onOpenRemoveModal,
}) => {
  return (
    <div className="overflow-x-auto">
      <table className="table">
        <thead>
          <tr>
            <th>Member</th>
            <th>Type</th>
            <th>Email</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {tableData.length === 0 ? (
            <tr>
              <td colSpan={5} className="text-center py-8 text-base-content/70">
                No members in this project yet. Use &quot;Add User&quot; or
                &quot;Add Group&quot; to get started.
              </td>
            </tr>
          ) : (
            tableData.map((row) => (
              <tr key={`${row.memberType}-${row.memberId}`} className="hover">
                <td className="flex gap-2">
                  <div>{row.name || "—"}</div>
                  {(row.isProjectAdmin || row.role) && (
                    <div
                      className={[
                        "badge badge-sm",
                        row.isProjectAdmin
                          ? "badge-warning"
                          : "badge-info",
                      ].join(" ")}
                    >
                      {row.isProjectAdmin ? "Admin" : row.role}
                    </div>
                  )}
                </td>

                <td className="capitalize">{row.memberType}</td>
                <td className="text-base-content/70">{row.email || "—"}</td>
                <td>
                  <div className="flex gap-2 items-center">
                    {row.memberType === "group" ? (
                        <button
                          className="btn btn-ghost btn-xs"
                          disabled={loading}    
                          onClick={() => onViewGroupMembers(row)}
                          title={"View group members"}
                        >
                          <UserGroupIcon className="size-6" />
                        </button>
                    ) : (
                        <div className="btn btn-ghost btn-xs invisible">
                          <UserGroupIcon className="size-6" />
                        </div>
                    )}
                    <button
                      className="btn btn-ghost btn-xs"
                      disabled={loading}
                      onClick={() => onEditRole(row)}
                      title="Edit role"
                    >
                      <PencilIcon className="size-6" />
                    </button>
                    <button
                      className="btn btn-ghost btn-xs text-error"
                      disabled={loading}
                      onClick={() =>
                        onOpenRemoveModal({
                          memberId: row.memberId,
                          memberName: row.name,
                          memberType: row.memberType,
                        })
                      }
                      title="Remove from project"
                    >
                      <TrashIcon className="size-6 text-error" />
                    </button>
                  </div>
                </td>
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>
  );
};

export default ProjectUsersListTable;
