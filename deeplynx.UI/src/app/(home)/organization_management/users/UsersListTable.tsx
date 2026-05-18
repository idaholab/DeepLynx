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
import { useLanguage } from "@/app/contexts/Language";
import { UsersTableRow } from "../../types/types";

/* -------------------------------------------------------------------------- */
/*                          Users & Invites Data Table                        */
/* -------------------------------------------------------------------------- */

interface UsersListTableProps {
  tableData: UsersTableRow[];
  scope: "org" | "site";
  loading: boolean;
  onResendInvite: (email: string) => void;
  onEditUser: (
    userId: number,
    userName: string,
    isOrgAdmin: boolean,
    isSysAdmin: boolean,
  ) => void;
  onOpenConfirm: (payload: {
    isOpen: boolean;
    itemId: number | null;
    itemName: string;
    isPending: boolean;
  }) => void;
}

const UsersListTable: React.FC<UsersListTableProps> = ({
  tableData,
  scope,
  loading,
  onResendInvite,
  onEditUser,
  onOpenConfirm,
}) => {
  const { t } = useLanguage();

  const formatLastLogin = (lastLogin?: string | null) => {
    if (!lastLogin) return "—";

    const normalized = /Z$|[+-]\d{2}:\d{2}$/.test(lastLogin)
      ? lastLogin
      : `${lastLogin}Z`;
    const date = new Date(normalized);

    if (Number.isNaN(date.getTime())) return "—";

    return new Intl.DateTimeFormat(undefined, {
      dateStyle: "medium",
      timeStyle: "short",
    }).format(date);
  };

  return (
    <div className="overflow-x-auto">
      <table className="table">
        <thead>
          <tr>
            <th>{t.translations.USER}</th>
            <th>{t.translations.EMAIL}</th>
            <th>{t.translations.USERNAME}</th>
            <th>{t.translations.STATUS}</th>
            <th>{t.translations.LAST_LOGIN}</th>
            <th>{t.translations.PROJECT_ASSIGNMENT}</th>
            <th>{t.translations.ACTIONS}</th>
          </tr>
        </thead>
        <tbody>
          {tableData.length === 0 ? (
            <tr>
              <td colSpan={7} className="text-center py-8 text-base-content/70">
                {t.translations.NO_USERS_OR_PENDING_INVITES_GET_STARTED}
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
                            {t.translations.PENDING_INVITE}
                          </div>
                        </>
                      ) : (
                        <>
                          <div className="font-medium">{row.name}</div>
                          {row.isSysAdmin && (
                            <div className="badge badge-warning badge-sm h-auto min-h-6 px-3 py-1 text-center leading-tight">
                              {t.translations.SYSTEM_ADMIN}
                            </div>
                          )}
                          {scope === "org" && row.isOrgAdmin && (
                            <div className="badge badge-info badge-sm h-auto min-h-6 px-3 py-1 text-center leading-tight">
                              {t.translations.ORG_ADMIN}
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
                        {t.translations.PENDING}
                      </div>
                    ) : row.isArchived ? (
                      <div className="badge badge-error">
                        {t.translations.ARCHIVED_BADGE}
                      </div>
                    ) : row.isActive ? (
                      <div className="badge badge-success">
                        {t.translations.ACTIVE}
                      </div>
                    ) : (
                      <div className="badge badge-warning">
                        {t.translations.INACTIVE}
                      </div>
                    )}
                  </td>

                  <td className="text-base-content/70">
                    {formatLastLogin(row.lastLogin)}
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
                            +{row.projects.length - 2} {t.translations.MORE}
                          </div>
                        )}
                      </div>
                    ) : (
                      <span className="text-base-content/50 text-sm">
                        {t.translations.NO_PROJECTS}
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
                            title={t.translations.RESEND_INVITATION}
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
                            title={t.translations.CANCEL_INVITATION}
                          >
                            <XMarkIcon className="w-4 h-4" />
                          </button>
                        </>
                      ) : (
                        <>
                          <button
                            className="btn btn-ghost btn-sm"
                            title={t.translations.EDIT_USER}
                            onClick={() =>
                              onEditUser(
                                row.id,
                                row.name,
                                !!row.isOrgAdmin,
                                !!row.isSysAdmin,
                              )
                            }
                            disabled={loading}
                          >
                            <PencilIcon className="size-6" />
                          </button>

                          {scope === "org" && (
                            <button
                              className="btn btn-ghost btn-sm text-error"
                              title={t.translations.REMOVE_FROM_ORGANIZATION}
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
                              <TrashIcon className="size-6" />
                            </button>
                          )}

                          {scope === "site" && (
                            <button
                              className="btn btn-ghost btn-sm text-error"
                              title={t.translations.ARCHIVE_USER_ACTION}
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
                              <TrashIcon className="size-6" />
                            </button>
                          )}
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
