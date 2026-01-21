// src/app/(home)/project_management/[id]/roles_and_permissions/ProjectRolesAndPermissions.tsx

"use client";

import React, { useEffect, useRef, useState } from "react";
import toast from "react-hot-toast";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import { useProjectSession } from "@/app/contexts/ProjectSessionProvider";
import {
  archiveRole,
  createRole,
  setPermissionsForRole,
  updateRole,
  getAllRoles,
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
import ProjectCreateRoleModal from "./ProjectCreateRoleModal";
import ProjectDeleteRoleModal from "./ProjectDeleteRoleModal";
import ProjectEditRoleModal from "./ProjectEditRoleModal";
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
  const [roles, setRoles] = useState(initialRoles);
  const [permissions, setPermissions] = useState(initialPermissions);

  const [rolePermissions, setRolePermissions] = useState<
    Record<number, PermissionResponseDto[]>
  >({});
  const [isLoadingPermissions, setIsLoadingPermissions] = useState(false);
  const [initialLoadComplete, setInitialLoadComplete] = useState(false);

  const { organization } = useOrganizationSession();

  const { t } = useLanguage();

  /* ------------------------------------------------------------------------ */
  /*                           Context: Project                               */
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
  /*                              Edit Role Modal                             */
  /* ------------------------------------------------------------------------ */

  const [isEditModalOpen, setIsEditModalOpen] = useState(false);
  const [roleToEdit, setRoleToEdit] = useState<RoleResponseDto | null>(null);

  const handleUpdateRole = async (
    roleId: number,
    name?: string | null,
    description?: string | null
  ) => {
    try {
      const dto: UpdateRoleRequestDto = { name, description };
      const updatedRole = await updateRole(
        organization?.organizationId as number,
        project?.projectId as number,
        roleId,
        dto
      );

      setRoles((prev) =>
        prev.map((role) => (role.id === roleId ? updatedRole : role))
      );
      toast.success("Role updated successfully");
    } catch (error) {
      console.error("Error updating role:", error);
      toast.error("Failed to upload Role");
      throw error;
    }
  };

  const handleEditClick = (role: RoleResponseDto) => {
    setRoleToEdit(role);
    setIsEditModalOpen(true);
  };

  const handleCloseEditModal = () => {
    setIsEditModalOpen(false);
    setRoleToEdit(null);
  };

  /* ------------------------------------------------------------------------ */
  /*                             Delete Role Modal                            */
  /* ------------------------------------------------------------------------ */

  const [isDeleteModalOpen, setIsDeleteModalOpen] = useState(false);
  const [roleToDelete, setRoleToDelete] = useState<RoleResponseDto | null>(
    null
  );

  const handleDeleteRole = async () => {
    if (!roleToDelete) return;

    try {
      await archiveRole(
        organization?.organizationId as number,
        project?.projectId as number,
        roleToDelete.id
      );

      // Remove archived role from UI
      setRoles((prev) => prev.filter((role) => role.id !== roleToDelete.id));

      // Re-select a fallback role if needed
      if (selectedRoleId === roleToDelete.id) {
        const remainingRoles = roles.filter(
          (role) => role.id !== roleToDelete.id
        );
        setSelectedRoleId(remainingRoles[0]?.id || null);
      }

      toast.success("Role archived successfully");
    } catch (error) {
      console.error("Error archiving role:", error);
      toast.error("Failed to archive role");
      throw error;
    }
  };

  const handleDeleteClick = (role: RoleResponseDto) => {
    setRoleToDelete(role);
    setIsDeleteModalOpen(true);
  };

  const handleCloseDeleteModal = () => {
    setIsDeleteModalOpen(false);
    setRoleToDelete(null);
  };

  /* ------------------------------------------------------------------------ */
  /*                    Single-Role Permission Editing State                  */
  /* ------------------------------------------------------------------------ */

    const [isEditingPermissions, setIsEditingPermissions] = useState(false);
    const [tempPermissions, setTempPermissions] = useState<Set<number>>(
      new Set()
    );

    const handleStartEditingPermissions = () => {
      if (!currentRole) return;

      // Only allow editing for project-specific roles
      if (isStandardRole(currentRole) || isOrganizationRole(currentRole)) {
        toast.error("Cannot edit permissions for inherited roles");
        return;
      }

      const currentPermissionIds =
        rolePermissions[currentRole.id]?.map((p) => Number(p.id)) || [];

      setTempPermissions(new Set(currentPermissionIds));
      setIsEditingPermissions(true);
    };

    const handleTogglePermission = (permissionId: number) => {
      setTempPermissions((prev) => {
        const next = new Set(prev);
        if (next.has(permissionId)) {
          next.delete(permissionId);
        } else {
          next.add(permissionId);
        }
        return next;
      });
    };

    const handleSavePermissions = async () => {
      if (!currentRole) return;

      try {
        await setPermissionsForRole(
          organization?.organizationId as number,
          project?.projectId as number,
          currentRole.id,
          Array.from(tempPermissions)
        );

        const updatedPerms = permissions.filter((p) =>
          tempPermissions.has(Number(p.id))
        );

        setRolePermissions((prev) => ({
          ...prev,
          [currentRole.id]: updatedPerms,
        }));

        setIsEditingPermissions(false);
        toast.success("Permissions updated successfully");
      } catch (error) {
        console.error("Error updating permissions:", error);
        toast.error("Failed to update permissions");
        throw error;
      }
    };

    const handleCancelEditingPermissions = () => {
      setIsEditingPermissions(false);
      setTempPermissions(new Set());
    };

  /* ------------------------------------------------------------------------ */
  /*                             Role Selection                               */
  /* ------------------------------------------------------------------------ */

    const handleRoleSelection = (roleId: number) => {
      if (isEditingPermissions) {
        toast.error("Please save or cancel your changes before switching roles");
        return;
      }
      setSelectedRoleId(roleId);
    };

    /* ------------------------------------------------------------------------ */
    /*                      Matrix (All Roles/Perms) Editing                    */
    /* ------------------------------------------------------------------------ */

    const tableContainerRef = useRef<HTMLDivElement | null>(null);
    const [isEditingMatrix, setIsEditingMatrix] = useState(false);
    const [matrixTempPermissions, setMatrixTempPermissions] = useState<
      Record<number, Set<number>>
    >({});

    const handleStartEditingMatrix = () => {
      const initialMatrix: Record<number, Set<number>> = {};

      roles.forEach((role) => {
        const rolePerms =
          rolePermissions[role.id]?.map((p) => Number(p.id)) || [];
        initialMatrix[role.id] = new Set(rolePerms);
      });

      setMatrixTempPermissions(initialMatrix);
      setIsEditingMatrix(true);
    };

    const handleToggleMatrixPermission = (
      roleId: number,
      permissionId: number
    ) => {
      const scrollTop = tableContainerRef.current?.scrollTop || 0;
      const scrollLeft = tableContainerRef.current?.scrollLeft || 0;

      setMatrixTempPermissions((prev) => {
        const updated = { ...prev };
        if (!updated[roleId]) {
          updated[roleId] = new Set();
        }

        const roleSet = new Set(updated[roleId]);
        if (roleSet.has(permissionId)) {
          roleSet.delete(permissionId);
        } else {
          roleSet.add(permissionId);
        }

        updated[roleId] = roleSet;
        return updated;
      });

      // Preserve scroll after re-render
      requestAnimationFrame(() => {
        if (tableContainerRef.current) {
          tableContainerRef.current.scrollTop = scrollTop;
          tableContainerRef.current.scrollLeft = scrollLeft;
        }
      });
    };

    const handleSaveMatrixPermissions = async () => {
      try {
        // Only update project-specific roles
        const projectRolesToUpdate = roles.filter(
          (role) => isProjectRole(role) && !isStandardRole(role)
        );

        const updatePromises = projectRolesToUpdate.map((role) => {
          const newPermissions = Array.from(matrixTempPermissions[role.id] || []);
          return setPermissionsForRole(
            organization?.organizationId as number,
            project?.projectId as number,
            role.id,
            newPermissions
          );
        });

        await Promise.all(updatePromises);

        const updatedRolePermissions: Record<number, PermissionResponseDto[]> =
          {};
        projectRolesToUpdate.forEach((role) => {
          const permIds = matrixTempPermissions[role.id] || new Set();
          updatedRolePermissions[role.id] = permissions.filter((p) =>
            permIds.has(Number(p.id))
          );
        });

        setRolePermissions((prev) => ({
          ...prev,
          ...updatedRolePermissions,
        }));

        setIsEditingMatrix(false);
        setMatrixTempPermissions({});
        toast.success("All permissions updated successfully");
      } catch (error) {
        console.error("Error updating matrix permissions:", error);
        toast.error("Failed to update permissions");
        throw error;
      }
    };

    const handleCancelEditingMatrix = () => {
      setIsEditingMatrix(false);
      setMatrixTempPermissions({});
    };

    const matrixRoleHasPermission = (
      roleId: number,
      permissionId: number
    ): boolean => {
      if (isEditingMatrix) {
        return matrixTempPermissions[roleId]?.has(permissionId) || false;
      }
      return roleHasPermission(roleId, permissionId);
    };

  /* ------------------------------------------------------------------------ */
  /*                 Permission Grouping / Helpers                            */
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

  /* ------------------------------------------------------------------------ */
  /*                               useEffect Hooks                            */
  /* ------------------------------------------------------------------------ */

  useEffect(() => {
    if (!initialLoadComplete && roles.length > 0) {
      fetchAllRolePermissions();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [roles, initialLoadComplete, fetchAllRolePermissions]);

  useEffect(() => {
    if (selectedRoleId && initialLoadComplete) {
      fetchRolePermissions(selectedRoleId);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedRoleId, initialLoadComplete, fetchRolePermissions]);

    /* ------------------------------------------------------------------------ */
    /*                          Permission Check Helper                         */
    /* ------------------------------------------------------------------------ */

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
  /*                          Refetch Roles on Mount                          */
  /* ------------------------------------------------------------------------ */

  const refetchAllRoles = async () => {
    if (!organization?.organizationId || !project?.projectId) return;

    try {
      const updatedRoles = await getAllRoles(
        organization.organizationId as number,
        project.projectId as number,
        true
      );

      console.log("Refetched project roles:", updatedRoles.map(r => ({
        id: r.id,
        name: r.name,
        orgId: r.organizationId,
        projId: r.projectId,
        source: r.projectId === null ? "ORG" : r.projectId === project.projectId ? "PROJECT" : "OTHER"
      })));

      setRoles(updatedRoles);
    } catch (error) {
      console.error("Error refetching roles:", error);
      toast.error("Failed to reload roles");
    }
  };

  // useEffect to refetch on mount
  useEffect(() => {
    refetchAllRoles();
  }, [organization?.organizationId, project?.projectId]);

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
          rolesLocked={rolesLocked}
          selectedRoleId={selectedRoleId}
          permissionCategories={permissionCategories}
          currentRole={currentRole || null}
          isEditingPermissions={isEditingPermissions}
          isLoadingPermissions={isLoadingPermissions}
          tempPermissions={tempPermissions}
          onRoleSelection={handleRoleSelection}
          roleHasPermission={roleHasPermission}
          isStandardRole={isStandardRole}
          isOrganizationRole={isOrganizationRole}
          isProjectRole={isProjectRole}
          getRoleSource={getRoleSource}
          onEditClick={handleEditClick}
          onDeleteClick={handleDeleteClick}
          onCreateRole={() => setIsCreateModalOpen(true)}
          onStartEditingPermissions={handleStartEditingPermissions}
          onCancelEditingPermissions={handleCancelEditingPermissions}
          onSavePermissions={handleSavePermissions}
          onTogglePermission={handleTogglePermission}
        />
      )}

      {activeLayout === "matrix" && (
        <MatrixViewLayout
          roles={roles}
          rolesLocked={rolesLocked}
          permissionCategories={permissionCategories}
          isLoadingPermissions={isLoadingPermissions}
          initialLoadComplete={initialLoadComplete}
          tableContainerRef={tableContainerRef}
          isEditingMatrix={isEditingMatrix}
          onStartEditingMatrix={handleStartEditingMatrix}
          onCancelEditingMatrix={handleCancelEditingMatrix}
          onSaveMatrixPermissions={handleSaveMatrixPermissions}
          onToggleMatrixPermission={handleToggleMatrixPermission}
          roleHasPermission={roleHasPermission}
          isStandardRole={isStandardRole}
          isOrganizationRole={isOrganizationRole}
          isProjectRole={isProjectRole}
          getRoleSource={getRoleSource}
          onEditClick={handleEditClick}
        />
      )}

      {/* Modals */}
      <ProjectCreateRoleModal
        isOpen={isCreateModalOpen}
        onClose={() => setIsCreateModalOpen(false)}
        onSubmit={handleCreateRole}
        organizationId={organization?.organizationId || 0}
      />

      <ProjectEditRoleModal
        isOpen={isEditModalOpen}
        onClose={handleCloseEditModal}
        onSubmit={handleUpdateRole}
        role={roleToEdit}
      />

      <ProjectDeleteRoleModal
        isOpen={isDeleteModalOpen}
        onClose={handleCloseDeleteModal}
        onConfirm={handleDeleteRole}
        role={roleToDelete}
      />
    </div>
  );
};

export default ProjectRolesAndPermissions;
