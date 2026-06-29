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

const ACTIONS_COLUMN_CLASS =
  "sticky right-0 z-20 w-20 min-w-20 border-l border-base-300/50 bg-base-100 shadow-[-8px_0_12px_-12px_rgba(15,23,42,0.45)]";
const ACTION_BUTTON_CLASS = "btn btn-ghost btn-sm px-1";

const ProjectUsersListTable: React.FC<ProjectUsersListTableProps> = ({
  tableData,
  loading,
  onEditRole,
  onViewGroupMembers,
  onOpenRemoveModal,
}) => {
  return (
    <div className="overflow-x-auto">
      <table className="table min-w-full">
        <thead>
          <tr>
            <th>Member</th>
            <th>Type</th>
            <th>Email</th>
            <th className={`${ACTIONS_COLUMN_CLASS} z-30 text-left`}>
              Actions
            </th>
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
                <td className={ACTIONS_COLUMN_CLASS}>
                  <div className="flex items-center justify-start gap-0.5 whitespace-nowrap">
                    {row.memberType === "group" ? (
                        <button
                          className={ACTION_BUTTON_CLASS}
                          disabled={loading}    
                          onClick={() => onViewGroupMembers(row)}
                          title={"View group members"}
                        >
                          <UserGroupIcon className="size-5" />
                        </button>
                    ) : null}
                    <button
                      className={ACTION_BUTTON_CLASS}
                      disabled={loading}
                      onClick={() => onEditRole(row)}
                      title="Edit role"
                    >
                      <PencilIcon className="size-5" />
                    </button>
                    <button
                      className={`${ACTION_BUTTON_CLASS} text-error`}
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
                      <TrashIcon className="size-5 text-error" />
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
