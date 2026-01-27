// src/app/(home)/organization_management/roles_and_permissions/RolesAndPermissions.tsx

import React, { useCallback, useEffect, useRef, useState } from "react";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import { useProjectSession } from "@/app/contexts/ProjectSessionProvider";
import {
  archiveRole,
  createRole,
  getOrgRolePermissions,
  setPermissionsForRole,
  updateRole,
} from "@/app/lib/client_service/role_services.client";
import { ExclamationCircleIcon } from "@heroicons/react/24/outline";
import toast from "react-hot-toast";

import {
  CreateRoleRequestDto,
  UpdateRoleRequestDto,
} from "../../types/requestDTOs";
import {
  PermissionResponseDto,
  RoleResponseDto,
} from "../../types/responseDTOs";

import CreateRoleModal from "./CreateRoleModal";
import DeleteRoleModal from "./DeleteRoleModal";
import EditRoleModal from "./EditRoleModal";
import MatrixViewLayout from "./MatrixViewLayout";
import SplitViewLayout from "./SplitViewLayout";

/* -------------------------------------------------------------------------- */
/*                                   Types                                    */
/* -------------------------------------------------------------------------- */

interface RolesAndPermissionsProps {
  initialRoles: RoleResponseDto[];
  initialPermissions: PermissionResponseDto[];
}

export interface PermissionCategory {
  id: string;
  label: string;
  permissions: PermissionResponseDto[];
}

/* -------------------------------------------------------------------------- */
/*                           Main Component Definition                        */
/* -------------------------------------------------------------------------- */

const RolesAndPermissions = ({
  initialRoles,
  initialPermissions,
}: RolesAndPermissionsProps) => {
  /* ------------------------------------------------------------------------ */
  /*                               Core State                                */
  /* ------------------------------------------------------------------------ */

  const [activeLayout, setActiveLayout] = useState<"split-view" | "matrix">(
    "split-view"
  );
  const [rolesLocked, setRolesLocked] = useState(false);
  const [selectedRoleId, setSelectedRoleId] = useState<number | null>(
    initialRoles[0]?.id || null
  );

  const [roles, setRoles] = useState(initialRoles);
  const [permissions, setPermissions] = useState(initialPermissions);

  const [rolePermissions, setRolePermissions] = useState<
    Record<number, PermissionResponseDto[]>
  >({});
  const [isLoadingPermissions, setIsLoadingPermissions] = useState(false);
  const [initialLoadComplete, setInitialLoadComplete] = useState(false);

  /* ------------------------------------------------------------------------ */
  /*                           Context: Org / Project                         */
  /* ------------------------------------------------------------------------ */

  const { organization } = useOrganizationSession();
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
      const updatePromises = roles.map((role) => {
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
      roles.forEach((role) => {
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
  /*                      Permission Grouping / Helpers                       */
  /* ------------------------------------------------------------------------ */

  const groupPermissionsByResource = (): PermissionCategory[] => {
    const grouped = permissions.reduce((acc, perm) => {
      const resource = perm.resource || "General";
      if (!acc[resource]) {
        acc[resource] = [];
      }
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
  const currentRole = roles.find((r) => r.id === selectedRoleId);

  /* ------------------------------------------------------------------------ */
  /*                    Fetching Role Permissions (API Calls)                 */
  /* ------------------------------------------------------------------------ */

  const fetchRolePermissions = useCallback(async (roleId: number) => {
    if (rolePermissions[roleId]) return;
    if (!organization?.organizationId) return;

    setIsLoadingPermissions(true);
    try {
      const perms = await getOrgRolePermissions(
        organization.organizationId as number,
        roleId
      );
      setRolePermissions((prev) => ({
        ...prev,
        [roleId]: perms,
      }));
    } catch (error) {
      console.error(`Error fetching permissions for role ${roleId}:`, error);
    } finally {
      setIsLoadingPermissions(false);
    }
  }, [organization?.organizationId, rolePermissions]);

  const fetchAllRolePermissions = useCallback(async () => {
    if (!organization?.organizationId) return;

    setIsLoadingPermissions(true);
    try {
      const promises = roles.map((role) =>
        getOrgRolePermissions(organization.organizationId as number, role.id)
          .then((perms) => {
            return { roleId: role.id, perms };
          })
          .catch((error) => {
            console.error(
              `❌ Error fetching permissions for role ${role.name} (ID: ${role.id}):`,
              error
            );
            return { roleId: role.id, perms: [] };
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
      console.error("❌ Error fetching all role permissions:", error);
    } finally {
      setIsLoadingPermissions(false);
    }
  }, [organization?.organizationId, roles]);

  /* ------------------------------------------------------------------------ */
  /*                               useEffect Hooks                            */
  /* ------------------------------------------------------------------------ */

  useEffect(() => {
    if (!initialLoadComplete && roles.length > 0) {
      fetchAllRolePermissions();
    }
  }, [roles, initialLoadComplete, fetchAllRolePermissions]);

  useEffect(() => {
    if (selectedRoleId && initialLoadComplete) {
      fetchRolePermissions(selectedRoleId);
    }
  }, [selectedRoleId, initialLoadComplete, fetchRolePermissions]);

  /* ------------------------------------------------------------------------ */
  /*                          Permission Check Helper                         */
  /* ------------------------------------------------------------------------ */

  const roleHasPermission = (roleId: number, permissionId: number): boolean => {
    const hasPermission =
      rolePermissions[roleId]?.some((p) => p.id === permissionId) || false;

    return hasPermission;
  };

  const isStandardRole = (role: RoleResponseDto): boolean => {
    return (
      role.name === "Admin" || role.name === "User" || role.name === "Viewer"
    );
  };

  /* ------------------------------------------------------------------------ */
  /*                               Main Render                                */
  /* ------------------------------------------------------------------------ */

  return (
    <div className="p-6 mx-auto">
      {/* Page Header */}
      <div className="mb-6">
        <div className="flex items-center justify-between mb-2">
          <h1 className="text-2xl font-bold">Roles & Permissions</h1>
          {/* Locking Roles (currently disabled) */}
          {/* <button
            onClick={() => setRolesLocked(!rolesLocked)}
            className={`btn gap-2 ${rolesLocked ? "btn-error" : "btn-primary"}`}
          >
            ...
          </button> */}
        </div>
        <p className="text-base-content/70">
          Define and manage organization-level roles and permissions. These
          settings will propagate to all projects.
        </p>
      </div>

      {/* Lock Warning Banner */}
      {rolesLocked && (
        <div className="alert alert-warning mb-6">
          <ExclamationCircleIcon className="w-5 h-5 flex-shrink-0" />
          <div>
            <h3 className="font-bold">Roles & Permissions Locked</h3>
            <div className="text-xs">
              Projects cannot create or modify roles at the project level.
            </div>
          </div>
        </div>
      )}

      {/* Layout Selector */}
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

      {/* Selected Layout */}
      {activeLayout === "split-view" && (
        <SplitViewLayout
          roles={roles}
          rolesLocked={rolesLocked}
          selectedRoleId={selectedRoleId}
          currentRole={currentRole || null}
          isEditingPermissions={isEditingPermissions}
          isLoadingPermissions={isLoadingPermissions}
          permissionCategories={permissionCategories}
          tempPermissions={tempPermissions}
          onRoleSelection={handleRoleSelection}
          onEditClick={handleEditClick}
          onDeleteClick={handleDeleteClick}
          onStartEditingPermissions={handleStartEditingPermissions}
          onCancelEditingPermissions={handleCancelEditingPermissions}
          onSavePermissions={handleSavePermissions}
          onTogglePermission={handleTogglePermission}
          roleHasPermission={roleHasPermission}
          isStandardRole={isStandardRole}
        />
      )}

      {activeLayout === "matrix" && (
        <MatrixViewLayout
          roles={roles}
          rolesLocked={rolesLocked}
          isLoadingPermissions={isLoadingPermissions}
          initialLoadComplete={initialLoadComplete}
          permissionCategories={permissionCategories}
          tableContainerRef={tableContainerRef}
          isEditingMatrix={isEditingMatrix}
          onStartEditingMatrix={handleStartEditingMatrix}
          onCancelEditingMatrix={handleCancelEditingMatrix}
          onSaveMatrixPermissions={handleSaveMatrixPermissions}
          matrixRoleHasPermission={matrixRoleHasPermission}
          onEditClick={handleEditClick}
          onToggleMatrixPermission={handleToggleMatrixPermission}
          isStandardRole={isStandardRole}
        />
      )}

      {/* Modals */}
      <CreateRoleModal
        isOpen={isCreateModalOpen}
        onClose={() => setIsCreateModalOpen(false)}
        onSubmit={handleCreateRole}
        organizationId={organization?.organizationId || 0}
      />

      <EditRoleModal
        isOpen={isEditModalOpen}
        onClose={handleCloseEditModal}
        onSubmit={handleUpdateRole}
        role={roleToEdit}
      />

      <DeleteRoleModal
        isOpen={isDeleteModalOpen}
        onClose={handleCloseDeleteModal}
        onConfirm={handleDeleteRole}
        role={roleToDelete}
      />
    </div>
  );
};

export default RolesAndPermissions;
