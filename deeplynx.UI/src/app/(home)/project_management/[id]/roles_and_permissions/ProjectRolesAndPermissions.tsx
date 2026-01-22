// src/app/(home)/project_management/[id]/roles_and_permissions/ProjectRolesAndPermissions.tsx

"use client";

import React, { useEffect, useState } from "react";
import toast from "react-hot-toast";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import {
  PermissionResponseDto,
  RoleResponseDto,
} from "@/app/(home)/types/responseDTOs";
import { LockClosedIcon } from "@heroicons/react/24/outline";
import { getPermissionsForRole } from "@/app/lib/client_service/permission_services.client";
import MatrixViewLayout from "./MatrixViewLayout";
import SplitViewLayout from "./SplitViewLayout";
import { useLanguage } from "@/app/contexts/Language";

/* -------------------------------------------------------------------------- */
/*                                   Types                                    */
/* -------------------------------------------------------------------------- */

interface ProjectRolesAndPermissionsProps {
  initialRoles: RoleResponseDto[];
  initialPermissions: PermissionResponseDto[];
  projectId: number;
  rolesLocked?: boolean;
}

export interface PermissionCategory {
  id: string;
  label: string;
  permissions: PermissionResponseDto[];
}

/* -------------------------------------------------------------------------- */
/*                           ProjectRolesAndPermissions                       */
/* -------------------------------------------------------------------------- */

const ProjectRolesAndPermissions = ({
  initialRoles,
  initialPermissions,
  projectId,
  rolesLocked = false,
}: ProjectRolesAndPermissionsProps) => {
  /* ------------------------------------------------------------------------ */
  /*                               Core State                                */
  /* ------------------------------------------------------------------------ */

  // For this release: only standard roles (Admin, User, Viewer)
  const standardInitialRoles = initialRoles.filter((role) =>
    ["Admin", "User", "Viewer"].includes(role.name),
  );

  const [activeLayout, setActiveLayout] = useState<"split-view" | "matrix">(
    "split-view",
  );
  const [selectedRoleId, setSelectedRoleId] = useState<number | null>(
    standardInitialRoles[0]?.id || null,
  );
  const [roles] = useState<RoleResponseDto[]>(standardInitialRoles);
  const [permissions] = useState<PermissionResponseDto[]>(initialPermissions);

  const [rolePermissions, setRolePermissions] = useState<
    Record<number, PermissionResponseDto[]>
  >({});
  const [isLoadingPermissions, setIsLoadingPermissions] = useState(false);
  const [initialLoadComplete, setInitialLoadComplete] = useState(false);

  const { organization } = useOrganizationSession();

  const { t } = useLanguage();

  /* ------------------------------------------------------------------------ */
  /*                 Permission grouping & derived helpers                    */
  /* ------------------------------------------------------------------------ */

  const groupPermissionsByResource = (): PermissionCategory[] => {
    const grouped = permissions.reduce(
      (acc, perm) => {
        const resource = perm.resource || "General";
        if (!acc[resource]) acc[resource] = [];
        acc[resource].push(perm);
        return acc;
      },
      {} as Record<string, PermissionResponseDto[]>,
    );

    return Object.entries(grouped).map(([resource, perms]) => ({
      id: resource.toLowerCase().replace(/\s+/g, "-"),
      label: resource,
      permissions: perms,
    }));
  };

  const permissionCategories = groupPermissionsByResource();
  const currentRole = roles.find((r) => r.id === selectedRoleId) || null;

  const roleHasPermission = (roleId: number, permissionId: number): boolean =>
    rolePermissions[roleId]?.some((p) => p.id === permissionId) || false;

  const isStandardRole = (role: RoleResponseDto): boolean =>
    role.name === "Admin" || role.name === "User" || role.name === "Viewer";

  const isOrganizationRole = (role: RoleResponseDto): boolean =>
    role.organizationId != null && role.projectId == null;

  const isProjectRole = (role: RoleResponseDto): boolean =>
    role.projectId != null && role.projectId === projectId;

  const getRoleSource = (role: RoleResponseDto): string => {
    if (isStandardRole(role)) return "System";
    if (isOrganizationRole(role)) return "Organization";
    if (isProjectRole(role)) return "Project";
    return "Unknown";
  };

  /* ------------------------------------------------------------------------ */
  /*                       Permissions Fetching (per role)                    */
  /* ------------------------------------------------------------------------ */

  const fetchRolePermissions = async (roleId: number) => {
    if (rolePermissions[roleId]) return;
    if (!organization?.organizationId) return;

    setIsLoadingPermissions(true);
    try {
      const perms = await getPermissionsForRole(
        Number(organization.organizationId),
        projectId,
        roleId,
      );
      setRolePermissions((prev) => ({
        ...prev,
        [roleId]: perms,
      }));
    } catch (error) {
      console.error(`Error fetching permissions for role ${roleId}:`, error);
      toast.error("Failed to load role permissions");
    } finally {
      setIsLoadingPermissions(false);
    }
  };

  const fetchAllRolePermissions = async () => {
    if (!organization?.organizationId) return;

    setIsLoadingPermissions(true);
    try {
      const orgId = Number(organization.organizationId);

      const promises = roles.map((role) =>
        getPermissionsForRole(orgId, projectId, role.id)
          .then((perms) => ({ roleId: role.id, perms }))
          .catch((error) => {
            console.error(
              `Error fetching permissions for role ${role.id}:`,
              error,
            );
            return { roleId: role.id, perms: [] as PermissionResponseDto[] };
          }),
      );

      const results = await Promise.all(promises);

      const newRolePermissions: Record<number, PermissionResponseDto[]> = {};
      results.forEach(({ roleId, perms }) => {
        newRolePermissions[roleId] = perms;
      });

      setRolePermissions(newRolePermissions);
      setInitialLoadComplete(true);
    } catch (error) {
      console.error("Error fetching all role permissions:", error);
      toast.error("Failed to load permissions");
    } finally {
      setIsLoadingPermissions(false);
    }
  };

  useEffect(() => {
    if (!initialLoadComplete && roles.length > 0) {
      fetchAllRolePermissions();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [roles, initialLoadComplete]);

  useEffect(() => {
    if (selectedRoleId && initialLoadComplete) {
      fetchRolePermissions(selectedRoleId);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedRoleId, initialLoadComplete]);

  const handleRoleSelection = (roleId: number) => {
    setSelectedRoleId(roleId);
  };

  /* ------------------------------------------------------------------------ */
  /*                                   Render                                 */
  /* ------------------------------------------------------------------------ */

  return (
    <div className="p-6 mx-auto">
      {/* Header */}
      <div className="mb-6 border-b border-base-300 pb-4">
        <div className="flex items-center justify-between mb-2">
          <h1 className="text-2xl font-bold">
            {t.translations.PROJECT_ROLES_AND_PERMISSIONS}
          </h1>
        </div>
        <p className="text-base-content/70">
          {t.translations.VIEW_PROJECT_LEVEL_ROLES_AND_PERMISSIONS}
        </p>
      </div>

      {/* Global lock notice (still useful context) */}
      {rolesLocked && (
        <div className="alert alert-warning mb-6">
          <LockClosedIcon className="w-6 h-6" />
          <div>
            <h3 className="font-bold">{t.translations.ROLES_LOCKED_BY_ORG}</h3>
            <div className="text-sm">
              {
                t.translations
                  .PROJECT_LEVEL_ROLE_CREATION_AND_PERMISSION_MODIFICATION_DESCRIPTION
              }
            </div>
          </div>
        </div>
      )}

      {/* Layout toggle */}
      <div className="mb-6">
        <label className="label">
          <span className="label-text font-medium">
            {t.translations.VIEW_LAYOUT}:
          </span>
        </label>
        <div className="btn-group">
          <button
            onClick={() => setActiveLayout("split-view")}
            className={`btn border-2 border-primary mr-3 ${
              activeLayout === "split-view" ? "btn-primary" : "btn-ghost"
            }`}
          >
            {t.translations.SPLIT_VIEW}
          </button>
          <button
            onClick={() => setActiveLayout("matrix")}
            className={`btn border-2 border-primary ${
              activeLayout === "matrix" ? "btn-primary" : "btn-ghost"
            }`}
          >
            {t.translations.MATRIX_VIEW}
          </button>
        </div>
      </div>

      {/* Layouts */}
      {activeLayout === "split-view" && (
        <SplitViewLayout
          roles={roles}
          selectedRoleId={selectedRoleId}
          onSelectRole={handleRoleSelection}
          permissionCategories={permissionCategories}
          currentRole={currentRole}
          roleHasPermission={roleHasPermission}
          isStandardRole={isStandardRole}
          isOrganizationRole={isOrganizationRole}
          isProjectRole={isProjectRole}
          getRoleSource={getRoleSource}
          isLoadingPermissions={isLoadingPermissions}
        />
      )}

      {activeLayout === "matrix" && (
        <MatrixViewLayout
          roles={roles}
          permissionCategories={permissionCategories}
          roleHasPermission={roleHasPermission}
          isStandardRole={isStandardRole}
          isOrganizationRole={isOrganizationRole}
          isProjectRole={isProjectRole}
          getRoleSource={getRoleSource}
          isLoadingPermissions={isLoadingPermissions}
          initialLoadComplete={initialLoadComplete}
        />
      )}
    </div>
  );
};

export default ProjectRolesAndPermissions;
