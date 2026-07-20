"use client";

import React, { useMemo, useState } from "react";
import {
  BuildingOfficeIcon,
  CheckIcon,
  ShieldCheckIcon,
  TrashIcon,
  PencilIcon,
} from "@heroicons/react/24/outline";

import {
  PermissionResponseDto,
  RoleResponseDto,
} from "@/app/(home)/types/responseDTOs";
import Tabs from "@/app/(home)/components/Tabs";
import { PermissionCategory } from "./ProjectRolesAndPermissions";
import { useLanguage } from "../../../../contexts/Language";

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
  isOrganizationRole: (role: RoleResponseDto) => boolean;
  isProjectRole: (role: RoleResponseDto) => boolean;
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
  roleHasPermission,
  isOrganizationRole,
  isProjectRole,
  onStartEditingPermissions,
  onCancelEditingPermissions,
  onSavePermissions,
  onTogglePermission,
}) => {
  // Determine if current role can be edited
  const canEditRole =
    currentRole &&
    !isOrganizationRole(currentRole);
  const canEditPermissions = canEditRole && !rolesLocked;
  const { t } = useLanguage();
  const [activePermissionTab, setActivePermissionTab] = useState(
    t.translations.RESOURCE_PERMISSIONS,
  );
  const splitPermissionCategories = useMemo(() => {
    const withoutLabelId: PermissionCategory[] = [];
    const withLabelId: PermissionCategory[] = [];

    permissionCategories.forEach((category) => {
      const categoryWithoutLabel = category.permissions.filter(
        (perm) => perm.labelId == null,
      );
      const categoryWithLabel = category.permissions.filter(
        (perm) => perm.labelId != null,
      );

      if (categoryWithoutLabel.length > 0) {
        withoutLabelId.push({
          ...category,
          permissions: categoryWithoutLabel,
        });
      }

      if (categoryWithLabel.length > 0) {
        withLabelId.push({
          ...category,
          permissions: categoryWithLabel,
        });
      }
    });

    return { withoutLabelId, withLabelId };
  }, [permissionCategories]);
  const labelPermissionCategoriesByName = useMemo(() => {
    const labelPermissions = splitPermissionCategories.withLabelId.flatMap(
      (category) => category.permissions,
    );

    const groupedByName = labelPermissions.reduce(
      (acc, perm) => {
        const key = (perm.name || "Unnamed Label Permission").trim();
        if (!acc[key]) acc[key] = [];
        acc[key].push(perm);
        return acc;
      },
      {} as Record<string, PermissionResponseDto[]>,
    );

    return Object.entries(groupedByName).map(([name, perms]) => ({
      id: `label-${name.toLowerCase().replace(/\s+/g, "-")}`,
      label: name,
      permissions: perms.sort((a, b) =>
        String(a.action || "").localeCompare(String(b.action || "")),
      ),
    }));
  }, [splitPermissionCategories.withLabelId]);

  const renderPermissionsContent = (
    categories: PermissionCategory[],
    displayMode: "permission-name" | "permission-action" = "permission-name",
  ): React.ReactNode => {
    if (!currentRole) return null;

    return (
      <div className="pt-4">
        <div className="flex items-center justify-between mb-4">
          <h3 className="text-sm font-semibold">
            {t.translations.PERMISSIONS}
          </h3>
          {!isEditingPermissions ? (
            <button
              disabled={!canEditPermissions || isLoadingPermissions}
              onClick={onStartEditingPermissions}
              className="btn btn-primary btn-sm gap-2"
              title={
                isOrganizationRole(currentRole)
                    ? t.translations
                        .ORGANIZATION_ROLE_PERMISSIONS_CANNOT_BE_MODIFIED_AT_PROJECT_LEVEL
                    : rolesLocked
                      ? "Roles are locked"
                      : "Edit Permissions"
              }
            >
              <PencilIcon className="w-4 h-4" />
              {t.translations.EDIT_PERMISSIONS}
            </button>
          ) : (
            <div className="flex gap-2">
              <button
                onClick={onCancelEditingPermissions}
                className="btn btn-ghost btn-sm"
              >
                {t.translations.CANCEL}
              </button>
              <button
                onClick={onSavePermissions}
                className="btn btn-primary btn-sm gap-2"
              >
                <CheckIcon className="w-4 h-4" />
                {t.translations.SAVE_CHANGES}
              </button>
            </div>
          )}
        </div>

        {isOrganizationRole(currentRole) && (
          <div className="alert alert-warning mb-4">
            <span className="text-sm">
              {t.translations.THIS_ROLE_IS_INHERITED}
            </span>
          </div>
        )}

        {/* Permission Content */}
        {isLoadingPermissions ? (
          <div className="flex items-center justify-center py-12">
            <span className="loading loading-spinner loading-lg text-primary"></span>
          </div>
        ) : categories.length === 0 ? (
          <div className="alert">
            <span>{t.translations.NO_PERMISSIONS_AVAILABLE}</span>
          </div>
        ) : (
          <div className="space-y-4">
            {categories.map((category) => (
              <div
                key={category.id}
                className="card border border-base-300/50 bg-base-100 shadow-sm"
              >
                <div className="card-body p-4">
                  <h4 className="card-title text-sm mb-3">{category.label}</h4>
                  <div className="grid grid-cols-2 gap-3">
                    {category.permissions.map((perm: PermissionResponseDto) => {
                      const hasPermission = isEditingPermissions
                        ? tempPermissions.has(Number(perm.id))
                        : roleHasPermission(currentRole.id, Number(perm.id));

                      return (
                        <label
                          key={perm.id}
                          className={`label justify-start gap-2 ${
                            isEditingPermissions
                              ? "cursor-pointer"
                              : "cursor-default"
                          }`}
                          title={
                            displayMode === "permission-action"
                              ? perm.description || perm.action || perm.name
                              : perm.description || perm.name
                          }
                        >
                          <input
                            type="checkbox"
                            checked={hasPermission}
                            onChange={() => onTogglePermission(Number(perm.id))}
                            disabled={!isEditingPermissions}
                            className="checkbox checkbox-primary checkbox-sm"
                          />
                          <span className="label-text">
                            {displayMode === "permission-action"
                              ? perm.action
                              : perm.name}
                          </span>
                        </label>
                      );
                    })}
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    );
  };

  const permissionTabs = useMemo(
    () => [
      {
        label: t.translations.RESOURCE_PERMISSIONS,
        content: renderPermissionsContent(
          splitPermissionCategories.withoutLabelId,
        ),
      },
      {
        label: t.translations.SENSITIVITY_LABELS,
        content: renderPermissionsContent(
          labelPermissionCategoriesByName,
          "permission-action",
        ),
      },
    ],
    [
      splitPermissionCategories,
      isEditingPermissions,
      isLoadingPermissions,
      tempPermissions,
      currentRole,
      rolesLocked,
      canEditPermissions,
      labelPermissionCategoriesByName,
      t,
    ],
  );

  return (
    <div className="flex gap-6" style={{ height: "calc(100vh - 28rem)" }}>
      {/* Left Sidebar - Role List */}
      <div className="w-80 flex-shrink-0">
        <div className="card h-full flex flex-col border border-base-300/50 bg-base-100 shadow-sm">
          <div className="card-body p-0 flex flex-col h-full">
            <div className="px-4 py-3 border-base-300/50 flex-shrink-0">
              <div className="flex items-start justify-between">
                <div>
                  <h2 className="card-title text-base">
                    {t.translations.ROLES}
                  </h2>
                  <p className="text-xs text-base-content/60 mt-1">
                    {roles.length} {t.translations.TOTAL}
                  </p>
                </div>
                <button
                  onClick={onCreateRole}
                  disabled={rolesLocked}
                  className="btn btn-primary btn-sm"
                  title={rolesLocked ? "Roles are locked" : "Create Role"}
                >
                  {t.translations.CREATE_ROLE}
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
                  className={`w-full px-4 py-3 text-left border-b border-base-300/50 transition-colors ${
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
                    {isOrganizationRole(role) && (
                      <div className="badge badge-secondary badge-sm flex gap-1">
                        <BuildingOfficeIcon className="w-3 h-3" />
                        {t.translations.ORG}
                      </div>
                    )}
                    {isProjectRole(role) && (
                      <div className="badge badge-primary badge-sm">
                        {t.translations.PRJ}
                      </div>
                    )}
                  </div>
                  {role.description && (
                    <p className="text-xs text-base-content/60 mt-1 ml-6 truncate">
                      {role.description}
                    </p>
                  )}
                </button>
              ))}
            </div>
          </div>
        </div>
      </div>

      {/* Right Panel - Role Details & Permissions */}
      <div className="card flex-1 flex flex-col overflow-hidden border border-base-300/50 bg-base-100 shadow-sm">
        {currentRole ? (
          <>
            {/* Role Header */}
            <div className="px-6 py-4 border-base-300/50 flex-shrink-0">
              <div className="flex items-start justify-between">
                <div>
                  <div className="flex items-center gap-3">
                    <h2 className="card-title">{currentRole.name}</h2>
                    {isOrganizationRole(currentRole) && (
                      <div className="badge badge-secondary gap-1">
                        <BuildingOfficeIcon className="w-4 h-4" />
                        {t.translations.ORGANIZATION_ROLE}
                      </div>
                    )}
                    {isProjectRole(currentRole) && (
                      <div className="badge badge-primary">
                        {t.translations.PROJECT_ROLE}
                      </div>
                    )}
                  </div>
                  {currentRole.description && (
                    <p className="text-sm text-base-content/70 mt-1">
                      {currentRole.description}
                    </p>
                  )}
                  <p className="text-xs text-base-content/60 mt-2">
                    {t.translations.LAST_UPDATED}{" "}
                    {new Date(currentRole.lastUpdatedAt).toLocaleDateString()}
                  </p>
                </div>
                <div className="flex gap-2">
                  <button
                    disabled={!canEditRole}
                    onClick={() => onEditClick(currentRole)}
                    className="btn btn-ghost btn-sm btn-circle"
                    title={
                      isOrganizationRole(currentRole)
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

            {/* Permissions Section */}
            <div className="flex-1 overflow-y-auto p-6">
              <Tabs
                tabs={permissionTabs}
                activeTab={activePermissionTab}
                onTabChange={setActivePermissionTab}
              />
            </div>
          </>
        ) : (
          <div className="flex items-center justify-center h-full">
            <p className="text-base-content/60">
              {t.translations.SELECT_A_ROLE_TO_VIEW_DETAILS}
            </p>
          </div>
        )}
      </div>
    </div>
  );
};

export default SplitViewLayout;
