// src/app/(home)/project_management/[id]/roles_and_permissions/ProjectRolesAndPermissions.tsx

"use client";

import React, { useEffect, useState } from "react";
import toast from "react-hot-toast";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import { useProjectSession } from "@/app/contexts/ProjectSessionProvider";
import {
  archiveRole,
  createRole,
  getOrgRolePermissions,
  setPermissionsForRole,
  updateRole,
} from "@/app/lib/client_service/role_services.client";

import {
  CreateRoleRequestDto,
  UpdateRoleRequestDto,
} from "../../../types/requestDTOs";
import {
  PermissionResponseDto,
  RoleResponseDto,
} from "@/app/(home)/types/responseDTOs";

import { LockClosedIcon } from "@heroicons/react/24/outline";
import { getPermissionsForRole } from "@/app/lib/client_service/permission_services.client";
import CreateRoleModal from "@/app/(home)/organization_management/roles_and_permissions/CreateRoleModal";
import MatrixViewLayout from "./MatrixViewLayout";
import SplitViewLayout from "./SplitViewLayout";

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
    ["Admin", "User", "Viewer"].includes(role.name)
  );

  const [activeLayout, setActiveLayout] = useState<"split-view" | "matrix">(
    "split-view"
  );
  const [selectedRoleId, setSelectedRoleId] = useState<number | null>(
    standardInitialRoles[0]?.id || null
  );
  const [roles, setRoles] = useState(initialRoles);
  const [permissions, setPermissions] = useState(initialPermissions);

  const [rolePermissions, setRolePermissions] = useState<
    Record<number, PermissionResponseDto[]>
  >({});
  const [isLoadingPermissions, setIsLoadingPermissions] = useState(false);
  const [initialLoadComplete, setInitialLoadComplete] = useState(false);

  const { organization } = useOrganizationSession();

  /* ------------------------------------------------------------------------ */
  /*                           Context: Project                         */
  /* ------------------------------------------------------------------------ */

    const { project } = useProjectSession();

  /* ------------------------------------------------------------------------ */
  /*                             Create Role Modal                            */
  /* ------------------------------------------------------------------------ */

  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);

  const handleCreateRole = async (data: {
    name: string;
    description: string | null;
  }) => {
    if (!organization?.organizationId) {
      throw new Error("No organization selected");
    }

    const dto: CreateRoleRequestDto = {
      name: data.name,
      description: data.description,
    };

    try {
      const newRole = await createRole(
        organization.organizationId as number,
        project?.projectId as number,
        dto
      );

      setRoles((prev) => [...prev, newRole]);
      setSelectedRoleId(newRole.id);
      toast.success("Created new role");
    } catch (error) {
      console.error("Error creating role:", error);
      toast.error("Failed to create new role");
      throw error;
    }
  };

  /* ------------------------------------------------------------------------ */
  /*                 Permission grouping & derived helpers                    */
  /* ------------------------------------------------------------------------ */

  const groupPermissionsByResource = (): PermissionCategory[] => {
    const grouped = permissions.reduce((acc, perm) => {
      const resource = perm.resource || "General";
      if (!acc[resource]) acc[resource] = [];
      acc[resource].push(perm);
      return acc;
    }, {} as Record<string, PermissionResponseDto[]>);

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
        roleId
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
              error
            );
            return { roleId: role.id, perms: [] as PermissionResponseDto[] };
          })
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
      <div className="mb-6">
        <div className="flex items-center justify-between mb-2">
          <h1 className="text-2xl font-bold">Project Roles & Permissions</h1>
        </div>
        <p className="text-base-content/70">
          View project-level roles and their permissions. Standard roles are
          defined by the system and are read-only in this release.
        </p>
      </div>

      {/* Global lock notice (still useful context) */}
      {rolesLocked && (
        <div className="alert alert-warning mb-6">
          <LockClosedIcon className="w-6 h-6" />
          <div>
            <h3 className="font-bold">Roles Locked by Organization</h3>
            <div className="text-sm">
              Project-level role creation and permission modification is
              disabled. Contact your organization administrator to unlock.
            </div>
          </div>
        </div>
      )}

      {/* Layout toggle */}
      <div className="mb-6">
        <label className="label">
          <span className="label-text font-medium">View Layout:</span>
        </label>
        <div className="btn-group">
          <button
            onClick={() => setActiveLayout("split-view")}
            className={`btn border-2 border-primary mr-3 ${
              activeLayout === "split-view" ? "btn-primary" : "btn-ghost"
            }`}
          >
            Split View
          </button>
          <button
            onClick={() => setActiveLayout("matrix")}
            className={`btn border-2 border-primary ${
              activeLayout === "matrix" ? "btn-primary" : "btn-ghost"
            }`}
          >
            Matrix View
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
          onCreateRole={() => setIsCreateModalOpen(true)}
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

      {/* Modals */}
      <CreateRoleModal
        isOpen={isCreateModalOpen}
        onClose={() => setIsCreateModalOpen(false)}
        onSubmit={handleCreateRole}
        organizationId={organization?.organizationId || 0}
      />
    </div>
  );
};

export default ProjectRolesAndPermissions;
