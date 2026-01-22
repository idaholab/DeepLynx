// src/app/(home)/organization_management/users/UsersListTable.tsx

import React from "react";
import {
  ArrowPathIcon,
  EnvelopeIcon,
  FolderIcon,
  PencilIcon,
  TrashIcon,
  XMarkIcon,
} from "@heroicons/react/24/outline";
import { UsersTableRow } from "../../types/types";

/* -------------------------------------------------------------------------- */
/*                          Users & Invites Data Table                        */
/* -------------------------------------------------------------------------- */

interface UsersListTableProps {
  tableData: UsersTableRow[];
  loading: boolean;
  onResendInvite: (email: string) => void;
  onEditUser: (userId: number, userName: string) => void;
  onOpenConfirm: (payload: {
    isOpen: boolean;
    itemId: number | null;
    itemName: string;
    isPending: boolean;
  }) => void;
}

const UsersListTable: React.FC<UsersListTableProps> = ({
  tableData,
  loading,
  onResendInvite,
  onEditUser,
  onOpenConfirm,
}) => {
  return (
    <div className="overflow-x-auto">
      <table className="table">
        <thead>
          <tr>
            <th>User</th>
            <th>Email</th>
            <th>Username</th>
            <th>Status</th>
            <th>Project Assignment</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {tableData.length === 0 ? (
            <tr>
              <td colSpan={6} className="text-center py-8 text-base-content/70">
                No users or pending invites. Click &quot;Invite User&quot; to
                get started.
              </td>
            </tr>
          ) : (
            tableData
              .filter((row) => !row.isPending)
              .map((row) => (
                <tr
                  key={`${row.isPending ? "pending" : "user"}-${row.id}`}
                  className={row.isPending ? "bg-warning/10" : "hover"}
                >
                  {/* User Column */}
                  <td>
                    <div className="flex items-center gap-2">
                      {row.isPending ? (
                        <>
                          <div className="avatar placeholder">
                            <div className="bg-warning text-warning-content rounded-full w-8">
                              <EnvelopeIcon className="w-4 h-4" />
                            </div>
                          </div>
                          <div className="font-medium text-base-content/70">
                            Pending Invite
                          </div>
                        </>
                      ) : (
                        <>
                          <div className="font-medium">{row.name}</div>
                          {row.isSysAdmin && (
                            <div className="badge badge-warning badge-sm">
                              Admin
                            </div>
                          )}
                        </>
                      )}
                    </div>
                  </td>

                  {/* Email Column */}
                  <td className="text-base-content/70">{row.email}</td>

                  {/* Username Column */}
                  <td className="text-base-content/70">
                    {row.isPending ? "—" : row.username || "—"}
                  </td>

                  {/* Status Column */}
                  <td>
                    {row.isPending ? (
                      <div className="badge badge-warning gap-1">
                        <EnvelopeIcon className="w-3 h-3" />
                        Pending
                      </div>
                    ) : row.isArchived ? (
                      <div className="badge badge-error">Archived</div>
                    ) : row.isActive ? (
                      <div className="badge badge-success">Active</div>
                    ) : (
                      <div className="badge badge-warning">Inactive</div>
                    )}
                  </td>

                  {/* Project Assignment Column */}
                  <td>
                    {row.isPending ? (
                      row.projectName ? (
                        <div className="flex items-center gap-2 text-sm">
                          <FolderIcon className="w-4 h-4 text-base-content/50" />
                          <span>{row.projectName}</span>
                          {row.roleName && (
                            <span className="badge badge-sm badge-outline">
                              {row.roleName}
                            </span>
                          )}
                        </div>
                      ) : (
                        <span className="text-base-content/50 text-sm">—</span>
                      )
                    ) : row.projects && row.projects.length > 0 ? (
                      <div className="flex flex-wrap gap-1">
                        {row.projects.slice(0, 2).map((project) => (
                          <div
                            key={project.id}
                            className="badge badge-sm badge-primary gap-1"
                            title={`${project.name} (${project.role})`}
                          >
                            <FolderIcon className="w-3 h-3" />
                            {project.name}
                          </div>
                        ))}
                        {row.projects.length > 2 && (
                          <div className="badge badge-sm badge-ghost">
                            +{row.projects.length - 2} more
                          </div>
                        )}
                      </div>
                    ) : (
                      <span className="text-base-content/50 text-sm">
                        No projects
                      </span>
                    )}
                  </td>

                  {/* Actions Column */}
                  <td>
                    <div className="flex gap-2">
                      {row.isPending ? (
                        <>
                          <button
                            className="btn btn-ghost btn-sm gap-1"
                            onClick={() => onResendInvite(row.email)}
                            disabled={loading}
                            title="Resend invitation"
                          >
                            <ArrowPathIcon className="w-4 h-4" />
                          </button>
                          <button
                            className="btn btn-ghost btn-sm text-error"
                            onClick={() =>
                              onOpenConfirm({
                                isOpen: true,
                                itemId: row.id,
                                itemName: row.email,
                                isPending: true,
                              })
                            }
                            disabled={loading}
                            title="Cancel invitation"
                          >
                            <XMarkIcon className="w-4 h-4" />
                          </button>
                        </>
                      ) : (
                        <>
                          <button
                            className="btn btn-ghost btn-sm"
                            title="Edit user"
                            onClick={() => onEditUser(row.id, row.name)}
                            disabled={loading}
                          >
                            <PencilIcon className="w-4 h-4" />
                          </button>
                          <button
                            className="btn btn-ghost btn-sm text-error"
                            title="Remove from organization"
                            onClick={() =>
                              onOpenConfirm({
                                isOpen: true,
                                itemId: row.id,
                                itemName: row.name,
                                isPending: false,
                              })
                            }
                            disabled={loading || row.isSysAdmin}
                          >
                            <TrashIcon className="w-4 h-4" />
                          </button>
                        </>
                      )}
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

export default UsersListTable;
