// src/app/(home)/rbac/useRBAC.tsx
import { useContext } from "react";
import { RBACContext } from "./RBACContext";
import { PERMISSIONS, ROLE_PERMISSIONS } from "./rbacConfig";
import type { RBACRole } from "./RBACContext";

export function useRBAC() {
  const context = useContext(RBACContext);

  if (!context) {
    throw new Error("useRBAC must be used within RBACProvider");
  }

  const { user, refreshUser } = context;

  /* ------------------------------------------------------------------------ */
  /*                              Role Helpers                                */
  /* ------------------------------------------------------------------------ */

  // Primary role for simple checks / UI (used if you just want one string)
  const getPrimaryRole = ():
    | "sysAdmin"
    | "orgAdmin"
    | "projectAdmin"
    | "viewer" => {
    if (!user) return "viewer";

    // Prefer explicit roles array
    if (user.roles && user.roles.length > 0) {
      if (user.roles.includes("sysAdmin")) return "sysAdmin";
      if (user.roles.includes("orgAdmin")) return "orgAdmin";
      if (user.roles.includes("projectAdmin")) return "projectAdmin";
    }

    // Fallback to flags (defensive)
    if (user.isSysAdmin) return "sysAdmin";
    if (user.isOrgAdmin) return "orgAdmin";
    if (user.isProjectAdmin) return "projectAdmin";

    return "viewer";
  };

  // All roles the user has
  const getAllRoles = (): RBACRole[] => {
    if (!user) return [];

    const roles = new Set<RBACRole>();

    if (user.roles && user.roles.length > 0) {
      user.roles.forEach((r) => roles.add(r));
    } else {
      if (user.isSysAdmin) roles.add("sysAdmin");
      if (user.isOrgAdmin) roles.add("orgAdmin");
      if (user.isProjectAdmin) roles.add("projectAdmin");
    }

    return Array.from(roles);
  };

  /* ------------------------------------------------------------------------ */
  /*                           Permission Checking                            */
  /* ------------------------------------------------------------------------ */

  const hasPermission = (permission: string): boolean => {
    if (!user || !user.isActive || user.isArchived) return false;

    const roles = getAllRoles();

    // Viewer / regular user
    if (roles.length === 0) {
      const viewerPerms = ROLE_PERMISSIONS["viewer"] || [];
      return viewerPerms.includes(permission);
    }

    // Union of all permissions from all roles
    const userPermissions = roles.reduce<string[]>((acc, role) => {
      const perms = ROLE_PERMISSIONS[role] || [];
      return acc.concat(perms);
    }, []);

    return userPermissions.includes(permission);
  };

  const hasAnyPermission = (permissions: string[]): boolean =>
    permissions.some((p) => hasPermission(p));

  const hasAllPermissions = (permissions: string[]): boolean =>
    permissions.every((p) => hasPermission(p));

  const hasRole = (role: string): boolean => {
    if (!user || !user.isActive || user.isArchived) return false;

    if (role === "viewer") {
      return getAllRoles().length === 0;
    }

    return getAllRoles().includes(role as RBACRole);
  };

  const isUserActive = (): boolean =>
    user ? user.isActive && !user.isArchived : false;

  return {
    user,
    hasPermission,
    hasAnyPermission,
    hasAllPermissions,
    hasRole,
    isUserActive,
    refreshUser,
    role: getPrimaryRole(), // e.g. "sysAdmin" | "orgAdmin" | "projectAdmin" | "viewer"
    roles: getAllRoles(), // e.g. ["orgAdmin", "projectAdmin"]
    PERMISSIONS,
  };
}
