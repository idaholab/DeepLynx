"use client";

import React from "react";
import {
  BuildingOfficeIcon,
  ExclamationCircleIcon,
  ShieldCheckIcon,
} from "@heroicons/react/24/outline";

import {
  PermissionResponseDto,
  RoleResponseDto,
} from "@/app/(home)/types/responseDTOs";
import { PermissionCategory } from "./ProjectRolesAndPermissions";

interface SplitViewLayoutProps {
  roles: RoleResponseDto[];
  selectedRoleId: number | null;
  permissionCategories: PermissionCategory[];
  currentRole: RoleResponseDto | null;
  isLoadingPermissions: boolean;

  onSelectRole: (roleId: number) => void;
  onCreateRole: () => void;
  roleHasPermission: (roleId: number, permissionId: number) => boolean;
  isStandardRole: (role: RoleResponseDto) => boolean;
  isOrganizationRole: (role: RoleResponseDto) => boolean;
  isProjectRole: (role: RoleResponseDto) => boolean;
  getRoleSource: (role: RoleResponseDto) => string;
}

const SplitViewLayout: React.FC<SplitViewLayoutProps> = ({
  roles,
  selectedRoleId,
  isLoadingPermissions,
  permissionCategories,
  currentRole,
  onSelectRole,
  onCreateRole,
  roleHasPermission,
  isStandardRole,
  isOrganizationRole,
  isProjectRole,
  getRoleSource,
}) => {
  return (
    <div className="flex gap-6" style={{ height: "calc(100vh - 28rem)" }}>
      {/* Roles List */}
      <div className="w-80 flex-shrink-0">
        <div className="card bg-base-100 shadow-xl h-full flex flex-col border-2 border-primary">
          <div className="card-body p-0">
            <div className="px-4 py-3 border-base-300">
              <div className="flex items-start justify-between">
                <div>
                  <h2 className="card-title text-base">Roles</h2>
                  <p className="text-xs text-base-content/60 mt-1">
                    {roles.length} total
                  </p>
                </div>
                <button
                  onClick={onCreateRole}
                  className="btn btn-primary btn-sm"
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
                  onClick={() => onSelectRole(role.id)}
                  className={`w-full px-4 py-3 text-left border-b border-base-300 transition-colors ${
                    selectedRoleId === role.id
                      ? "bg-primary/10 border-l-4 border-l-primary"
                      : ""
                  } hover:bg-base-200 cursor-pointer`}
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

      {/* Role details & permissions */}
      <div className="flex-1 card bg-base-100 shadow-xl flex flex-col overflow-hidden border-2 border-primary">
        {currentRole ? (
          <>
            {/* Role header */}
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
                {/* No edit/delete actions in this release */}
              </div>
            </div>

            {/* Info about non-editable perms */}
            <div className="p-3">
              <div className="alert alert-info py-2">
                <ExclamationCircleIcon className="w-5 h-5 flex-shrink-0" />
                <span className="text-sm">
                  Role permissions are read-only in this release. Standard roles
                  are defined by the system and cannot be modified here.
                </span>
              </div>
            </div>

            <div className="divider px-3"></div>

            {/* Permissions list for selected role */}
            <div className="flex-1 overflow-y-auto p-6">
              <div className="flex items-center justify-between mb-4">
                <h3 className="text-sm font-semibold">Permissions</h3>
              </div>

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
                              const hasPermission = roleHasPermission(
                                currentRole.id,
                                Number(perm.id)
                              );

                              return (
                                <label
                                  key={perm.id}
                                  className="label justify-start gap-2 cursor-default"
                                  title={perm.description || perm.name}
                                >
                                  <input
                                    type="checkbox"
                                    checked={hasPermission}
                                    readOnly
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
