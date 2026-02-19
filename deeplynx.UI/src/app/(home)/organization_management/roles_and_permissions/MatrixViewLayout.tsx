// src/app/(home)/organization_management/roles_and_permissions/MatrixViewLayout.tsx

import React from "react";
import {
  CheckIcon,
  PencilIcon,
  ShieldCheckIcon,
  XMarkIcon,
} from "@heroicons/react/24/outline";
import {
  PermissionResponseDto,
  RoleResponseDto,
} from "../../types/responseDTOs";
import { PermissionCategory } from "./RolesAndPermissions";
import { useLanguage } from "../../../contexts/Language";

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
  onEditClick: (role: RoleResponseDto) => void;
  onToggleMatrixPermission: (roleId: number, permissionId: number) => void;

  matrixRoleHasPermission: (roleId: number, permissionId: number) => boolean;
  isStandardRole: (role: RoleResponseDto) => boolean;
}

const MatrixViewLayout: React.FC<MatrixViewLayoutProps> = ({
  roles,
  rolesLocked,
  isLoadingPermissions,
  initialLoadComplete,
  permissionCategories,
  tableContainerRef,
  isEditingMatrix,
  onStartEditingMatrix,
  onCancelEditingMatrix,
  onSaveMatrixPermissions,
  onEditClick,
  onToggleMatrixPermission,
  matrixRoleHasPermission,
  isStandardRole,
}) => {
  const { t } = useLanguage();
  const hasNonStandardRoles = roles.some((role) => !isStandardRole(role));
  const matrixPermissionCategories = React.useMemo(() => {
    // Matrix view intentionally excludes sensitivity-label permissions.
    return permissionCategories
      .map((category) => ({
        ...category,
        permissions: category.permissions.filter((perm) => perm.labelId == null),
      }))
      .filter((category) => category.permissions.length > 0);
  }, [permissionCategories]);

  const editMatrixDisabledReason = !hasNonStandardRoles
    ? t.translations.MATRIX_EDIT_REQUIRES_CUSTOM_ORG_ROLES
    : rolesLocked
    ? t.translations.ROLES_ARE_LOCKED
    : isLoadingPermissions
    ? t.translations.PERMISSIONS_STILL_LOADING
    : "";

  return (
    <div style={{ height: "calc(100vh - 28rem)" }}>
      <div className="card bg-base-100 shadow-xl h-full flex flex-col overflow-hidden border-2 border-primary">
        {/* Matrix Header / Controls */}
        <div className="px-6 py-3 border-b border-base-300 flex items-center justify-between">
          <h3 className="text-sm font-semibold">
            {t.translations.PERMISSION_MATRIX}
          </h3>

          {/* Toggle editable matrix mode */}
          {!isEditingMatrix ? (
            <button
              disabled={
                rolesLocked || isLoadingPermissions || !hasNonStandardRoles
              }
              onClick={onStartEditingMatrix}
              className="btn btn-primary btn-sm gap-2"
              title={
                editMatrixDisabledReason ||
                t.translations.EDIT_PERMISSIONS_ACROSS_ROLES_MATRIX_VIEW
              }
            >
              <PencilIcon className="w-4 h-4" />
              {t.translations.EDIT_MATRIX}
            </button>
          ) : (
            <div className="flex gap-2">
              <button
                onClick={onCancelEditingMatrix}
                className="btn btn-ghost btn-sm"
              >
                {t.translations.CANCEL}
              </button>
              <button
                onClick={onSaveMatrixPermissions}
                className="btn btn-primary btn-sm gap-2"
              >
                <CheckIcon className="w-4 h-4" />
                {t.translations.SAVE_ALL_CHANGES}
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
                {t.translations.LOADING_PERMISSIONS}
              </p>
            </div>
          </div>
        ) : (
          <div ref={tableContainerRef} className="flex-1 overflow-auto">
            <table className="table">
              <thead className="sticky top-0 z-20 bg-base-100">
                <tr>
                  <th className="sticky left-0 bg-base-200 z-30">
                    {t.translations.PERMISSION}
                  </th>
                  {roles.map((role) => {
                    const standard = isStandardRole(role);

                    const editDisabled = standard || !hasNonStandardRoles;

                    const editTitle = !hasNonStandardRoles
                      ? t.translations.ONLY_STANDARD_ROLES_NO_CUSTOM_TO_EDIT
                      : standard
                      ? t.translations.STANDARD_ROLES_CANNOT_BE_EDITED
                      : t.translations.EDIT_ROLE;

                    return (
                      <th key={role.id} className="text-center">
                        <div className="flex flex-col items-center gap-1">
                          <div className="flex items-center gap-2">
                            <ShieldCheckIcon className="w-4 h-4 text-primary" />
                            <span className="font-medium">{role.name}</span>
                            {standard && (
                              <div className="badge badge-info badge-xs">
                                {t.translations.STD}
                              </div>
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
                        </div>
                      </th>
                    );
                  })}
                </tr>
              </thead>
              <tbody>
                {matrixPermissionCategories.map((category) => (
                  <React.Fragment key={category.id}>
                    {/* Category Row */}
                    <tr className="bg-base-200">
                      <td
                        colSpan={roles.length + 1}
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
                              {t.translations.ACTION} {perm.action}
                            </span>
                          </div>
                        </td>

                        {roles.map((role) => {
                          const hasPermission = matrixRoleHasPermission(
                            role.id,
                            Number(perm.id)
                          );
                          const standard = isStandardRole(role);

                          return (
                            <td key={role.id} className="text-center">
                              <div
                                onClick={() => {
                                  if (isEditingMatrix && !standard) {
                                    onToggleMatrixPermission(
                                      role.id,
                                      Number(perm.id)
                                    );
                                  }
                                }}
                                className={`inline-block ${
                                  isEditingMatrix && !standard
                                    ? "cursor-pointer hover:scale-110 transition-transform"
                                    : "cursor-default"
                                } ${
                                  standard && isEditingMatrix
                                    ? "opacity-60 ring-2 ring-warning rounded-lg p-1"
                                    : ""
                                }`}
                                title={
                                  standard && isEditingMatrix
                                    ? t.translations.STANDARD_ROLE_PERMISSIONS_CANNOT_BE_MODIFIED
                                    : isEditingMatrix
                                    ? t.translations.CLICK_TO_TOGGLE
                                    : hasPermission
                                    ? t.translations.HAS_PERMISSION
                                    : t.translations.NO_PERMISSION
                                }
                              >
                                {hasPermission ? (
                                  <CheckIcon
                                    className={`size-8 mx-auto ${
                                      isEditingMatrix && !standard
                                        ? "text-success hover:text-success/70"
                                        : standard && isEditingMatrix
                                        ? "text-warning"
                                        : "text-success"
                                    }`}
                                  />
                                ) : (
                                  <XMarkIcon
                                    className={`size-8 mx-auto ${
                                      isEditingMatrix && !standard
                                        ? "text-base-300 hover:text-success/50"
                                        : standard && isEditingMatrix
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
