"use client";

import React from "react";
import {
  BuildingOfficeIcon,
  ExclamationCircleIcon,
  CheckIcon,
  ShieldCheckIcon,
  TrashIcon,
  PencilIcon,
} from "@heroicons/react/24/outline";

import {
  PermissionResponseDto,
  RoleResponseDto,
} from "@/app/(home)/types/responseDTOs";
import { PermissionCategory } from "./ProjectRolesAndPermissions";

interface SplitViewLayoutProps {
  roles: RoleResponseDto[];
  rolesLocked: boolean;
  selectedRoleId: number | null;
  currentRole: RoleResponseDto | null;
  isEditingPermissions: boolean;
  isLoadingPermissions: boolean;
  permissionCategories: PermissionCategory[];
  tempPermissions: Set<number>;

  onRoleSelection: (roleId: number) => void;
  onCreateRole: () => void;
  onEditClick: (role: RoleResponseDto) => void;
  onDeleteClick: (role: RoleResponseDto) => void;
  onStartEditingPermissions: () => void;
  onCancelEditingPermissions: () => void;
  onSavePermissions: () => void;
  onTogglePermission: (permissionId: number) => void;

  roleHasPermission: (roleId: number, permissionId: number) => boolean;
  isStandardRole: (role: RoleResponseDto) => boolean;
  isOrganizationRole: (role: RoleResponseDto) => boolean;
  isProjectRole: (role: RoleResponseDto) => boolean;
  getRoleSource: (role: RoleResponseDto) => string;
}

const SplitViewLayout: React.FC<SplitViewLayoutProps> = ({
  roles,
  rolesLocked,
  selectedRoleId,
  currentRole,
  isEditingPermissions,
  isLoadingPermissions,
  permissionCategories,
  tempPermissions,
  onRoleSelection,
  onCreateRole,
  onEditClick,
  onDeleteClick,
  getRoleSource,
  roleHasPermission,
  isStandardRole,
  isOrganizationRole,
  isProjectRole,
  onStartEditingPermissions,
  onCancelEditingPermissions,
  onSavePermissions,
  onTogglePermission,
}) => {
  // Determine if current role can be edited
  const canEditRole = currentRole && !isStandardRole(currentRole) && !isOrganizationRole(currentRole);
  const canEditPermissions = canEditRole && !rolesLocked;

  return (
    <div className="flex gap-6" style={{ height: "calc(100vh - 28rem)" }}>
      {/* Left Sidebar - Role List */}
      <div className="w-80 flex-shrink-0">
        <div className="card bg-base-100 shadow-xl h-full flex flex-col border-2 border-primary">
          <div className="card-body p-0 flex flex-col h-full">
            <div className="px-4 py-3 border-base-300 flex-shrink-0">
              <div className="flex items-start justify-between">
                <div>
                  <h2 className="card-title text-base">Roles</h2>
                  <p className="text-xs text-base-content/60 mt-1">
                    {roles.length} total
                  </p>
                </div>
                <button
                  onClick={onCreateRole}
                  disabled={rolesLocked}
                  className="btn btn-primary btn-sm"
                  title={rolesLocked ? "Roles are locked" : "Create Role"}
                >
                  Create Role
                </button>
              </div>
            </div>
            <div className="divider px-3"></div>
            <div className="flex-1 overflow-y-auto">
              {roles.map((role) => (
                <button
                  key={role.id}
                  onClick={() => onRoleSelection(role.id)}
                  disabled={isEditingPermissions}
                  className={`w-full px-4 py-3 text-left border-b border-base-300 transition-colors ${
                    selectedRoleId === role.id
                      ? "bg-primary/10 border-l-4 border-l-primary"
                      : ""
                  } ${
                    isEditingPermissions
                      ? "opacity-50 cursor-not-allowed"
                      : "hover:bg-base-200 cursor-pointer"
                  }`}
                >
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2">
                      <ShieldCheckIcon
                        className={`w-4 h-4 ${
                          selectedRoleId === role.id
                            ? "text-primary"
                            : "text-base-content/40"
                        }`}
                      />
                      <span className="font-medium text-sm">{role.name}</span>
                    </div>
                    {isStandardRole(role) && (
                      <div className="badge badge-info badge-sm">STD</div>
                    )}
                    {isOrganizationRole(role) && (
                      <div className="badge badge-secondary badge-sm flex gap-1">
                        <BuildingOfficeIcon className="w-3 h-3" />
                        ORG
                      </div>
                    )}
                    {isProjectRole(role) && (
                      <div className="badge badge-primary badge-sm">PRJ</div>
                    )}
                  </div>
                  {role.description && (
                    <p className="text-xs text-base-content/60 mt-1 ml-6 truncate">
                      {role.description}
                    </p>
                  )}
                  <p className="text-xs text-base-content/50 mt-1 ml-6">
                    Source: {getRoleSource(role)}
                  </p>
                </button>
              ))}
            </div>
          </div>
        </div>
      </div>

      {/* Right Panel - Role Details & Permissions */}
      <div className="flex-1 card bg-base-100 shadow-xl flex flex-col overflow-hidden border-2 border-primary">
        {currentRole ? (
          <>
            {/* Role Header */}
            <div className="px-6 py-4 border-base-300 flex-shrink-0">
              <div className="flex items-start justify-between">
                <div>
                  <div className="flex items-center gap-3">
                    <h2 className="card-title">{currentRole.name}</h2>
                    {isStandardRole(currentRole) && (
                      <div className="badge badge-info">Standard Role</div>
                    )}
                    {isOrganizationRole(currentRole) && (
                      <div className="badge badge-secondary gap-1">
                        <BuildingOfficeIcon className="w-4 h-4" />
                        Organization Role
                      </div>
                    )}
                    {isProjectRole(currentRole) && (
                      <div className="badge badge-primary">Project Role</div>
                    )}
                  </div>
                  {currentRole.description && (
                    <p className="text-sm text-base-content/70 mt-1">
                      {currentRole.description}
                    </p>
                  )}
                  <p className="text-xs text-base-content/60 mt-2">
                    Source: {getRoleSource(currentRole)} • Last updated:{" "}
                    {new Date(currentRole.lastUpdatedAt).toLocaleDateString()}
                  </p>
                </div>
                <div className="flex gap-2">
                  <button
                    disabled={!canEditRole}
                    onClick={() => onEditClick(currentRole)}
                    className="btn btn-ghost btn-sm btn-circle"
                    title={
                      isStandardRole(currentRole)
                        ? "Standard roles cannot be edited"
                        : isOrganizationRole(currentRole)
                        ? "Organization roles cannot be edited at project level"
                        : "Edit Role"
                    }
                  >
                    <PencilIcon className="size-6" />
                  </button>
                  {canEditRole && (
                    <button
                      disabled={rolesLocked}
                      onClick={() => onDeleteClick(currentRole)}
                      className="btn btn-ghost btn-sm btn-circle text-error"
                      title={rolesLocked ? "Roles are locked" : "Delete Role"}
                    >
                      <TrashIcon className="size-6" />
                    </button>
                  )}
                </div>
              </div>
            </div>

            <div className="divider px-3"></div>

            {/* Permissions Section */}
            <div className="flex-1 overflow-y-auto p-6">
              <div className="flex items-center justify-between mb-4">
                <h3 className="text-sm font-semibold">Permissions</h3>
                {!isEditingPermissions ? (
                  <button
                    disabled={!canEditPermissions || isLoadingPermissions}
                    onClick={onStartEditingPermissions}
                    className="btn btn-primary btn-sm gap-2"
                    title={
                      isStandardRole(currentRole)
                        ? "Standard role permissions cannot be modified"
                        : isOrganizationRole(currentRole)
                        ? "Organization role permissions cannot be modified at project level"
                        : rolesLocked
                        ? "Roles are locked"
                        : "Edit Permissions"
                    }
                  >
                    <PencilIcon className="w-4 h-4" />
                    Edit Permissions
                  </button>
                ) : (
                  <div className="flex gap-2">
                    <button
                      onClick={onCancelEditingPermissions}
                      className="btn btn-ghost btn-sm"
                    >
                      Cancel
                    </button>
                    <button
                      onClick={onSavePermissions}
                      className="btn btn-primary btn-sm gap-2"
                    >
                      <CheckIcon className="w-4 h-4" />
                      Save Changes
                    </button>
                  </div>
                )}
              </div>

              {/* Info alerts for read-only roles */}
              {isStandardRole(currentRole) && (
                <div className="alert alert-info mb-4">
                  <ExclamationCircleIcon className="w-5 h-5 flex-shrink-0" />
                  <span className="text-sm">
                    This is a standard role and cannot be modified. Create a
                    custom project role if you need different permissions.
                  </span>
                </div>
              )}

              {isOrganizationRole(currentRole) && (
                <div className="alert alert-warning mb-4">
                  <ExclamationCircleIcon className="w-5 h-5 flex-shrink-0" />
                  <span className="text-sm">
                    This role is inherited from the organization and cannot be
                    modified at the project level. You can view its permissions
                    or create a custom project role.
                  </span>
                </div>
              )}

              {/* Permission Content */}
              {isLoadingPermissions ? (
                <div className="flex items-center justify-center py-12">
                  <span className="loading loading-spinner loading-lg text-primary"></span>
                </div>
              ) : permissionCategories.length === 0 ? (
                <div className="alert">
                  <span>No permissions available.</span>
                </div>
              ) : (
                <div className="space-y-4">
                  {permissionCategories.map((category) => (
                    <div key={category.id} className="card bg-base-200/25">
                      <div className="card-body p-4">
                        <h4 className="card-title text-sm mb-3">
                          {category.label}
                        </h4>
                        <div className="grid grid-cols-2 gap-3">
                          {category.permissions.map(
                            (perm: PermissionResponseDto) => {
                              const hasPermission = isEditingPermissions
                                ? tempPermissions.has(Number(perm.id))
                                : roleHasPermission(
                                    currentRole.id,
                                    Number(perm.id)
                                  );

                              return (
                                <label
                                  key={perm.id}
                                  className={`label justify-start gap-2 ${
                                    isEditingPermissions
                                      ? "cursor-pointer"
                                      : "cursor-default"
                                  }`}
                                  title={perm.description || perm.name}
                                >
                                  <input
                                    type="checkbox"
                                    checked={hasPermission}
                                    onChange={() =>
                                      onTogglePermission(Number(perm.id))
                                    }
                                    disabled={!isEditingPermissions}
                                    className="checkbox checkbox-primary checkbox-sm"
                                  />
                                  <span className="label-text">
                                    {perm.name}
                                  </span>
                                </label>
                              );
                            }
                          )}
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </>
        ) : (
          <div className="flex items-center justify-center h-full">
            <p className="text-base-content/60">
              Select a role to view details
            </p>
          </div>
        )}
      </div>
    </div>
  );
};

export default SplitViewLayout;
