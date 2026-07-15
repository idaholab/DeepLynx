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
import { useLanguage } from "../../../../contexts/Language";

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
  isOrganizationRole: (role: RoleResponseDto) => boolean;
  isProjectRole: (role: RoleResponseDto) => boolean;
}
const getTranslatedPermissionName = (
  name: string,
  t: ReturnType<typeof useLanguage>["t"],
) => {
  switch (name) {
    case "Read Project":
      return t.translations.READ_PROJECT;
    case "Write Project":
      return t.translations.WRITE_PROJECT;
    case "Update Projects":
      return t.translations.UPDATE_PROJECTS;

    case "Read Object Storage":
      return t.translations.READ_OBJECT_STORAGE;
    case "Write Object Storage":
      return t.translations.WRITE_OBJECT_STORAGE;
    case "Update Object Storages":
      return t.translations.UPDATE_OBJECT_STORAGES;

    case "Read Data Source":
      return t.translations.READ_DATA_SOURCE;
    case "Write Data Source":
      return t.translations.WRITE_DATA_SOURCE;
    case "Update Data Sources":
      return t.translations.UPDATE_DATA_SOURCES;

    case "Read Record":
      return t.translations.READ_RECORD;
    case "Write Record":
      return t.translations.WRITE_RECORD;
    case "Update Records":
      return t.translations.UPDATE_RECORDS;

    case "Read Edge":
      return t.translations.READ_EDGE;
    case "Write Edge":
      return t.translations.WRITE_EDGE;
    case "Update Edges":
      return t.translations.UPDATE_EDGES;

    case "Read File":
      return t.translations.READ_FILE;
    case "Write File":
      return t.translations.WRITE_FILE;
    case "Update Files":
      return t.translations.UPDATE_FILES;

    case "Read Tag":
      return t.translations.READ_TAG;
    case "Write Tag":
      return t.translations.WRITE_TAG;
    case "Update Tags":
      return t.translations.UPDATE_TAGS;

    case "Read Class":
      return t.translations.READ_CLASS;
    case "Write Class":
      return t.translations.WRITE_CLASS;
    case "Update Classes":
      return t.translations.UPDATE_CLASSES;

    case "Read Relationship":
      return t.translations.READ_RELATIONSHIP;
    case "Write Relationship":
      return t.translations.WRITE_RELATIONSHIP;
    case "Update Relationships":
      return t.translations.UPDATE_RELATIONSHIPS;

    case "Read User":
      return t.translations.READ_USER;
    case "Write User":
      return t.translations.WRITE_USER;
    case "Update Users":
      return t.translations.UPDATE_USERS;

    case "Read Group":
      return t.translations.READ_GROUP;
    case "Write Group":
      return t.translations.WRITE_GROUP;
    case "Update Groups":
      return t.translations.UPDATE_GROUPS;

    case "Read Organization":
      return t.translations.READ_ORGANIZATION;
    case "Write Organization":
      return t.translations.WRITE_ORGANIZATION;
    case "Update Organizations":
      return t.translations.UPDATE_ORGANIZATIONS;

    case "Read Role":
      return t.translations.READ_ROLE;
    case "Write Role":
      return t.translations.WRITE_ROLE;
    case "Update Roles":
      return t.translations.UPDATE_ROLES;

    case "Read Permission":
      return t.translations.READ_PERMISSION;
    case "Write Permission":
      return t.translations.WRITE_PERMISSION;
    case "Update Permissions":
      return t.translations.UPDATE_PERMISSIONS;

    case "Read Sensitivity Label":
      return t.translations.READ_SENSITIVITY_LABEL;
    case "Write Sensitivity Label":
      return t.translations.WRITE_SENSITIVITY_LABEL;
    case "Update Sensitivity Labels":
      return t.translations.UPDATE_SENSITIVITY_LABELS;

    case "Read Insight":
      return t.translations.READ_INSIGHT;
    case "Write Insight":
      return t.translations.WRITE_INSIGHT;

    case "Read Record Collection":
      return t.translations.READ_RECORD_COLLECTION;
    case "Write Record Collection":
      return t.translations.WRITE_RECORD_COLLECTION;
    case "Update Record Collection":
      return t.translations.UPDATE_RECORD_COLLECTION;

    default:
      return name;
  }
};

const getTranslatedCategoryName = (
  label: string,
  t: ReturnType<typeof useLanguage>["t"],
) => {
  switch (label) {
    case "class":
      return t.translations.CLASS;
    case "data_source":
      return t.translations.DATA_SOURCE;
    case "edge":
      return t.translations.EDGE;
    case "file":
      return t.translations.FILE;
    case "group":
      return t.translations.GROUP;
    case "insight":
      return t.translations.INSIGHT;
    case "object_storage":
      return t.translations.OBJECT_STORAGE;
    case "organization":
      return t.translations.ORGANIZATION;
    case "permission":
      return t.translations.PERMISSION;
    case "project":
      return t.translations.PROJECT;
    case "record":
      return t.translations.RECORD;
    case "record_collection":
      return t.translations.RECORD_COLLECTION;
    case "relationship":
      return t.translations.RELATIONSHIP;
    case "role":
      return t.translations.ROLE;
    case "sensitivity_label":
      return t.translations.SENSITIVITY_LABEL;
    case "tag":
      return t.translations.TAG;
    case "user":
      return t.translations.USER;
    default:
      return label;
  }
};

const getTranslatedAction = (
  action: string,
  t: ReturnType<typeof useLanguage>["t"],
) => {
  switch (action) {
    case "read":
      return t.translations.READ;

    case "write":
      return t.translations.WRITE;

    case "update":
      return t.translations.UPDATE;

    default:
      return action;
  }
};
const getTranslatedPermissionDescription = (
  description: string,
  t: ReturnType<typeof useLanguage>["t"],
) => {
  switch (description) {
    case "Permission to embed files in Insight":
      return t.translations.PERMISSION_TO_EMBED_FILES_IN_INSIGHT;

    case "Permission to modify classes":
      return t.translations.PERMISSION_TO_MODIFY_CLASSES;

    case "Permission to modify data sources":
      return t.translations.PERMISSION_TO_MODIFY_DATA_SOURCES;

    case "Permission to modify files":
      return t.translations.PERMISSION_TO_MODIFY_FILES;

    case "Permission to modify groups":
      return t.translations.PERMISSION_TO_MODIFY_GROUPS;

    case "Permission to modify object storage":
      return t.translations.PERMISSION_TO_MODIFY_OBJECT_STORAGE;

    case "Permission to modify organization information":
      return t.translations.PERMISSION_TO_MODIFY_ORGANIZATION_INFORMATION;

    case "Permission to modify permissions":
      return t.translations.PERMISSION_TO_MODIFY_PERMISSIONS;

    case "Permission to modify project information":
      return t.translations.PERMISSION_TO_MODIFY_PROJECT_INFORMATION;

    case "Permission to modify records":
      return t.translations.PERMISSION_TO_MODIFY_RECORDS;

    case "Permission to modify relationships":
      return t.translations.PERMISSION_TO_MODIFY_RELATIONSHIPS;

    case "Permission to modify roles":
      return t.translations.PERMISSION_TO_MODIFY_ROLES;

    case "Permission to modify tags":
      return t.translations.PERMISSION_TO_MODIFY_TAGS;

    case "Permission to modify user information":
      return t.translations.PERMISSION_TO_MODIFY_USER_INFORMATION;

    case "Permission to read a record collection":
      return t.translations.PERMISSION_TO_READ_A_RECORD_COLLECTION;

    case "Permission to read classes":
      return t.translations.PERMISSION_TO_READ_CLASSES;

    case "Permission to read data sources":
      return t.translations.PERMISSION_TO_READ_DATA_SOURCES;

    case "Permission to read files":
      return t.translations.PERMISSION_TO_READ_FILES;

    case "Permission to read groups":
      return t.translations.PERMISSION_TO_READ_GROUPS;

    case "Permission to read object storage":
      return t.translations.PERMISSION_TO_READ_OBJECT_STORAGE;

    case "Permission to read organization information":
      return t.translations.PERMISSION_TO_READ_ORGANIZATION_INFORMATION;

    case "Permission to read permissions":
      return t.translations.PERMISSION_TO_READ_PERMISSIONS;

    case "Permission to read project information":
      return t.translations.PERMISSION_TO_READ_PROJECT_INFORMATION;

    case "Permission to read records":
      return t.translations.PERMISSION_TO_READ_RECORDS;

    case "Permission to read relationships":
      return t.translations.PERMISSION_TO_READ_RELATIONSHIPS;

    case "Permission to read roles":
      return t.translations.PERMISSION_TO_READ_ROLES;

    case "Permission to read tags":
      return t.translations.PERMISSION_TO_READ_TAGS;

    case "Permission to read user information":
      return t.translations.PERMISSION_TO_READ_USER_INFORMATION;

    case "Permission to update a record collection":
      return t.translations.PERMISSION_TO_UPDATE_A_RECORD_COLLECTION;

    case "Permission to update project data":
      return t.translations.PERMISSION_TO_UPDATE_PROJECT_DATA;

    case "Permission to modify sensitivity labels":
      return t.translations.PERMISSION_TO_MODIFY_SENSITIVITY_LABELS;

    case "Permission to read sensitivity labels":
      return t.translations.PERMISSION_TO_READ_SENSITIVITY_LABELS;

    case "Permission to update data sources":
      return t.translations.PERMISSION_TO_UPDATE_DATA_SOURCES;

    case "Permission to update edges":
      return t.translations.PERMISSION_TO_UPDATE_EDGES;

    case "Permission to update groups":
      return t.translations.PERMISSION_TO_UPDATE_GROUPS;

    case "Permission to update roles":
      return t.translations.PERMISSION_TO_UPDATE_ROLES;

    case "Permission to update sensitivity labels":
      return t.translations.PERMISSION_TO_UPDATE_SENSITIVITY_LABELS;

    case "Permission to update tags":
      return t.translations.PERMISSION_TO_UPDATE_TAGS;

    case "Permission to read results from Insight":
      return t.translations.PERMISSION_TO_READ_RESULTS_FROM_INSIGHT;

    case "Permission to update classes":
      return t.translations.PERMISSION_TO_UPDATE_CLASSES;

    case "Permission to update files":
      return t.translations.PERMISSION_TO_UPDATE_FILES;

    case "Permission to update organizations":
      return t.translations.PERMISSION_TO_UPDATE_ORGANIZATIONS;

    case "Permission to update records":
      return t.translations.PERMISSION_TO_UPDATE_RECORDS;

    case "Permission to write a record collection":
      return t.translations.PERMISSION_TO_WRITE_A_RECORD_COLLECTION;

    case "Permission to read edges":
      return t.translations.PERMISSION_TO_READ_EDGES;

    case "Permission to modify edges":
      return t.translations.PERMISSION_TO_MODIFY_EDGES;

    case "Permission to update object storage":
      return t.translations.PERMISSION_TO_UPDATE_OBJECT_STORAGE;

    case "Permission to update permissions":
      return t.translations.PERMISSION_TO_UPDATE_PERMISSIONS;

    case "Permission to update relationships":
      return t.translations.PERMISSION_TO_UPDATE_RELATIONSHIPS;

    case "Permission to update users":
      return t.translations.PERMISSION_TO_UPDATE_USERS;

    default:
      return description;
  }
};

const getTranslatedRoleSource = (
  source: string,
  t: ReturnType<typeof useLanguage>["t"],
) => {
  switch (source) {
    case "System":
      return "Sistema";
    case "Organization":
      return t.translations.ORGANIZATION;
    case "Project":
      return t.translations.PROJECT;
    default:
      return source;
  }
};

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
  isOrganizationRole,
  isProjectRole,
}) => {
  const { t } = useLanguage();
  const matrixPermissionCategories = React.useMemo(() => {
    // Matrix view intentionally excludes sensitivity-label permissions.
    return permissionCategories
      .map((category) => ({
        ...category,
        permissions: category.permissions.filter((perm) => perm.labelId == null),
      }))
      .filter((category) => category.permissions.length > 0);
  }, [permissionCategories]);

  // Determine if there are editable (project-only) roles
  const hasEditableRoles = roles.some(
    (role) => isProjectRole(role)
  );

  // Determine if a role can be edited
  const canEditRole = (role: RoleResponseDto) => {
    return isProjectRole(role);
  };

  const editMatrixDisabledReason = !hasEditableRoles
    ? t.translations.MATRIX_EDIT_REQUIRES_CUSTOM_PROJECT_ROLES
    : rolesLocked
      ? t.translations.ROLES_ARE_LOCKED
      : isLoadingPermissions
        ? t.translations.PERMISSIONS_STILL_LOADING
        : "";

  return (
    <div style={{ height: "calc(100vh - 28rem)" }}>
      <div className="card h-full flex flex-col overflow-hidden border border-base-300/50 bg-base-100 shadow-sm">
        {/* Matrix Header / Controls */}
        <div className="px-6 py-3 border-b border-base-300/50 flex items-center justify-between">
          <h3 className="text-sm font-semibold">
            {t.translations.PERMISSION_MATRIX}
          </h3>

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
                t.translations.EDIT_PERMISSIONS_FOR_PROJECT_ROLES_MATRIX_VIEW
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
                {t.translations.SAVE_CHANGES}
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
                    const isOrg = isOrganizationRole(role);
                    const isPrj = isProjectRole(role);
                    const editable = canEditRole(role);

                    const editDisabled = !editable;
                    const editTitle = isStd
                      ? t.translations.STANDARD_ROLES_CANNOT_BE_EDITED
                      : isOrg
                        ? t.translations.ORGANIZATION_ROLES_CANNOT_BE_EDITED_AT_PROJECT_LEVEL
                        : t.translations.EDIT_ROLE;

                    return (
                      <th key={role.id} className="text-center">
                        <div className="flex flex-col items-center gap-1">
                          <div className="flex items-center gap-2">
                            <ShieldCheckIcon className="w-4 h-4 text-primary" />
                            <span className="font-medium">{role.name}</span>
                            {isStd && (
                              <div className="badge badge-info badge-xs">
                                {t.translations.STD}
                              </div>
                            )}
                            {isOrg && (
                              <div className="badge badge-secondary badge-xs flex gap-1">
                                <BuildingOfficeIcon className="w-3 h-3" />
                                {t.translations.ORG}
                              </div>
                            )}
                            {isPrj && (
                              <div className="badge badge-primary badge-xs">
                                {t.translations.PRJ}
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
                              {role.description === "Administrator role with full permissions"
                                ? t.translations.ADMINISTRATOR_ROLE_WITH_FULL_PERMISSIONS
                                : role.description === "User role with limited permissions"
                                  ? t.translations.USER_ROLE_WITH_LIMITED_PERMISSIONS
                                  : role.description}
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
                {matrixPermissionCategories.map((category) => (
                  <React.Fragment key={category.id}>
                    {/* Category Row */}
                    <tr className="bg-base-200">
                      <td
                        colSpan={roles.length + 1}
                        className="font-semibold text-sm sticky left-0"
                      >
                        {getTranslatedCategoryName(category.label, t)}
                      </td>
                    </tr>

                    {/* Permission Rows */}
                    {category.permissions.map((perm: PermissionResponseDto) => (
                      <tr key={perm.id} className="hover">
                        <td className="sticky left-0 z-10 bg-base-100">
                          <div className="flex flex-col">
                            <span className="font-medium text-sm">
                              {getTranslatedPermissionName(perm.name, t)}
                            </span>
                            {perm.description && (
                              <span className="text-xs text-base-content/60">
                                {getTranslatedPermissionDescription(perm.description, t)}
                              </span>
                            )}
                            <span className="text-xs text-base-content/50 mt-1">
                              {t.translations.ACTION} {getTranslatedAction(perm.action, t)}
                            </span>
                          </div>
                        </td>

                        {roles.map((role) => {
                          const hasPermission = roleHasPermission(
                            role.id,
                            Number(perm.id)
                          );
                          const editable = canEditRole(role);
                          const isInherited = isOrganizationRole(role);

                          return (
                            <td key={role.id} className="text-center">
                              <div
                                onClick={() => {
                                  if (isEditingMatrix && editable) {
                                    onToggleMatrixPermission(
                                      role.id,
                                      Number(perm.id)
                                    );
                                  }
                                }}
                                className={`inline-block ${isEditingMatrix && editable
                                  ? "cursor-pointer hover:scale-110 transition-transform"
                                  : "cursor-default"
                                  } ${isInherited && isEditingMatrix
                                    ? "opacity-60 ring-2 ring-warning rounded-lg p-1"
                                    : ""
                                  }`}
                                title={
                                  isStandardRole(role) && isEditingMatrix
                                    ? t.translations.STANDARD_ROLE_PERMISSIONS_CANNOT_BE_MODIFIED
                                    : isOrganizationRole(role) && isEditingMatrix
                                      ? t.translations.ORGANIZATION_ROLE_PERMISSIONS_CANNOT_BE_MODIFIED_AT_PROJECT_LEVEL
                                      : isEditingMatrix
                                        ? t.translations.CLICK_TO_TOGGLE
                                        : hasPermission
                                          ? t.translations.HAS_PERMISSION
                                          : t.translations.NO_PERMISSION
                                }
                              >
                                {hasPermission ? (
                                  <CheckIcon
                                    className={`size-8 mx-auto ${isEditingMatrix && editable
                                      ? "text-success hover:text-success/70"
                                      : isInherited && isEditingMatrix
                                        ? "text-warning"
                                        : "text-success"
                                      }`}
                                  />
                                ) : (
                                  <XMarkIcon
                                    className={`size-8 mx-auto ${isEditingMatrix && editable
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
