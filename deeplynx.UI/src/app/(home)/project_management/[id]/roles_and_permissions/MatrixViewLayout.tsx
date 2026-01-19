"use client";

import React from "react";
import {
  BuildingOfficeIcon,
  CheckIcon,
  PencilIcon,
  ShieldCheckIcon,
  XMarkIcon,
} from "@heroicons/react/24/outline";

import {
  PermissionResponseDto,
  RoleResponseDto,
} from "@/app/(home)/types/responseDTOs";
import { PermissionCategory } from "./ProjectRolesAndPermissions";

interface MatrixViewLayoutProps {
  roles: RoleResponseDto[];
  rolesLocked: boolean;
  isLoadingPermissions: boolean;
  initialLoadComplete: boolean;
  permissionCategories: PermissionCategory[];
  tableContainerRef: React.RefObject<HTMLDivElement | null>;
  isEditingMatrix: boolean;

  onStartEditingMatrix: () => void;
  onCancelEditingMatrix: () => void;
  onSaveMatrixPermissions: () => void;
  onToggleMatrixPermission: (roleId: number, permissionId: number) => void;
  onEditClick: (role: RoleResponseDto) => void;

  roleHasPermission: (roleId: number, permissionId: number) => boolean;
  isStandardRole: (role: RoleResponseDto) => boolean;
  isOrganizationRole: (role: RoleResponseDto) => boolean;
  isProjectRole: (role: RoleResponseDto) => boolean;
  getRoleSource: (role: RoleResponseDto) => string;
}

const MatrixViewLayout: React.FC<MatrixViewLayoutProps> = ({
  roles,
  rolesLocked,
  permissionCategories,
  isLoadingPermissions,
  initialLoadComplete,
  tableContainerRef,
  isEditingMatrix,
  onStartEditingMatrix,
  onCancelEditingMatrix,
  onSaveMatrixPermissions,
  onToggleMatrixPermission,
  onEditClick,
  roleHasPermission,
  isStandardRole,
  isOrganizationRole,
  isProjectRole,
  getRoleSource,
}) => {
  // Determine if there are editable (project-only) roles
  const hasEditableRoles = roles.some(
    (role) => isProjectRole(role) && !isStandardRole(role)
  );

  // Determine if a role can be edited
  const canEditRole = (role: RoleResponseDto) => {
    return isProjectRole(role) && !isStandardRole(role);
  };

  const editMatrixDisabledReason = !hasEditableRoles
    ? "Matrix editing is only available when custom project roles exist."
    : rolesLocked
    ? "Roles are locked."
    : isLoadingPermissions
    ? "Permissions are still loading."
    : "";

  return (
    <div style={{ height: "calc(100vh - 28rem)" }}>
      <div className="card bg-base-100 shadow-xl h-full flex flex-col overflow-hidden border-2 border-primary">
        {/* Matrix Header / Controls */}
        <div className="px-6 py-3 border-b border-base-300 flex items-center justify-between">
          <h3 className="text-sm font-semibold">Permission Matrix</h3>

          {/* Toggle editable matrix mode */}
          {!isEditingMatrix ? (
            <button
              disabled={
                rolesLocked || isLoadingPermissions || !hasEditableRoles
              }
              onClick={onStartEditingMatrix}
              className="btn btn-primary btn-sm gap-2"
              title={
                editMatrixDisabledReason ||
                "Edit permissions for project roles in matrix view."
              }
            >
              <PencilIcon className="w-4 h-4" />
              Edit Matrix
            </button>
          ) : (
            <div className="flex gap-2">
              <button
                onClick={onCancelEditingMatrix}
                className="btn btn-ghost btn-sm"
              >
                Cancel
              </button>
              <button
                onClick={onSaveMatrixPermissions}
                className="btn btn-primary btn-sm gap-2"
              >
                <CheckIcon className="w-4 h-4" />
                Save All Changes
              </button>
            </div>
          )}
        </div>

        {/* Matrix Content */}
        {isLoadingPermissions && !initialLoadComplete ? (
          <div className="flex-1 flex items-center justify-center">
            <div className="text-center">
              <span className="loading loading-spinner loading-lg text-primary"></span>
              <p className="mt-4 text-base-content/60">
                Loading permissions...
              </p>
            </div>
          </div>
        ) : (
          <div ref={tableContainerRef} className="flex-1 overflow-auto">
            <table className="table">
              <thead className="sticky top-0 z-20 bg-base-100">
                <tr>
                  <th className="sticky left-0 bg-base-200 z-30">Permission</th>
                  {roles.map((role) => {
                    const isStd = isStandardRole(role);
                    const isOrg = isOrganizationRole(role);
                    const isPrj = isProjectRole(role);
                    const editable = canEditRole(role);

                    const editDisabled = !editable;
                    const editTitle = isStd
                      ? "Standard roles cannot be edited"
                      : isOrg
                      ? "Organization roles cannot be edited at project level"
                      : "Edit Role";

                    return (
                      <th key={role.id} className="text-center">
                        <div className="flex flex-col items-center gap-1">
                          <div className="flex items-center gap-2">
                            <ShieldCheckIcon className="w-4 h-4 text-primary" />
                            <span className="font-medium">{role.name}</span>
                            {isStd && (
                              <div className="badge badge-info badge-xs">STD</div>
                            )}
                            {isOrg && (
                              <div className="badge badge-secondary badge-xs flex gap-1">
                                <BuildingOfficeIcon className="w-3 h-3" />
                                ORG
                              </div>
                            )}
                            {isPrj && (
                              <div className="badge badge-primary badge-xs">PRJ</div>
                            )}
                            {!isEditingMatrix && (
                              <button
                                disabled={editDisabled}
                                onClick={() => onEditClick(role)}
                                className="btn btn-ghost btn-xs btn-circle"
                                title={editTitle}
                              >
                                <PencilIcon className="size-4" />
                              </button>
                            )}
                          </div>
                          {role.description && (
                            <span className="text-xs text-base-content/60 font-normal">
                              {role.description}
                            </span>
                          )}
                          <span className="text-xs text-base-content/50 font-normal">
                            {getRoleSource(role)}
                          </span>
                        </div>
                      </th>
                    );
                  })}
                </tr>
              </thead>
              <tbody>
                {permissionCategories.map((category) => (
                  <React.Fragment key={category.id}>
                    {/* Category Row */}
                    <tr className="bg-base-200">
                      <td
                        colSpan={roles.length + 2}
                        className="font-semibold text-sm sticky left-0"
                      >
                        {category.label}
                      </td>
                    </tr>

                    {/* Permission Rows */}
                    {category.permissions.map((perm: PermissionResponseDto) => (
                      <tr key={perm.id} className="hover">
                        <td className="sticky left-0 z-10 bg-base-100">
                          <div className="flex flex-col">
                            <span className="font-medium text-sm">
                              {perm.name}
                            </span>
                            {perm.description && (
                              <span className="text-xs text-base-content/60">
                                {perm.description}
                              </span>
                            )}
                            <span className="text-xs text-base-content/50 mt-1">
                              Action: {perm.action}
                            </span>
                          </div>
                        </td>

                        {roles.map((role) => {
                          const hasPermission = roleHasPermission(
                            role.id,
                            Number(perm.id)
                          );
                          const editable = canEditRole(role);
                          const isInherited = isStandardRole(role) || isOrganizationRole(role);

                          return (
                            <td key={role.id} className="text-center">
                              <div
                                onClick={(e) => {
                                  e.preventDefault();
                                  e.stopPropagation();
                                  if (isEditingMatrix && editable) {
                                    onToggleMatrixPermission(
                                      role.id,
                                      Number(perm.id)
                                    );
                                  }
                                }}
                                className={`inline-block ${
                                  isEditingMatrix && editable
                                    ? "cursor-pointer hover:scale-110 transition-transform"
                                    : "cursor-default"
                                } ${
                                  isInherited && isEditingMatrix
                                    ? "opacity-60 ring-2 ring-warning rounded-lg p-1"
                                    : ""
                                }`}
                                title={
                                  isStandardRole(role) && isEditingMatrix
                                    ? "Standard role permissions cannot be modified"
                                    : isOrganizationRole(role) && isEditingMatrix
                                    ? "Organization role permissions cannot be modified at project level"
                                    : isEditingMatrix
                                    ? "Click to toggle"
                                    : hasPermission
                                    ? "Has permission"
                                    : "No permission"
                                }
                              >
                                {hasPermission ? (
                                  <CheckIcon
                                    className={`size-8 mx-auto ${
                                      isEditingMatrix && editable
                                        ? "text-success hover:text-success/70"
                                        : isInherited && isEditingMatrix
                                        ? "text-warning"
                                        : "text-success"
                                    }`}
                                  />
                                ) : (
                                  <XMarkIcon
                                    className={`size-8 mx-auto ${
                                      isEditingMatrix && editable
                                        ? "text-base-300 hover:text-success/50"
                                        : isInherited && isEditingMatrix
                                        ? "text-warning/50"
                                        : "text-base-300"
                                    }`}
                                  />
                                )}
                              </div>
                            </td>
                          );
                        })}
                      </tr>
                    ))}
                  </React.Fragment>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
};

export default MatrixViewLayout;
