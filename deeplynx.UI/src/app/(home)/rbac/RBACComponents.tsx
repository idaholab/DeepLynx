// src/app/(home)/rbac/RBACComponents.tsx
"use client";

import React, { ReactNode } from "react";
import { useRBAC } from "./useRBAC";
import { PERMISSIONS } from "./rbacConfig";
import type { AnyRBACRole } from "./rbacConfig";

/* -------------------------------------------------------------------------- */
/*                               CanAccess                                    */
/* -------------------------------------------------------------------------- */

interface CanAccessProps {
  permission?: string;
  permissions?: string[];
  requireAll?: boolean;
  fallback?: ReactNode;
  children: ReactNode;
}

/**
 * For UI Elements & Features
 * Use this for small pieces of UI like buttons, menu items, sections, or features within a page.
 *
 * Examples:
 * - Show/hide buttons (Create, Edit, Delete)
 * - Show/hide menu items in navigation
 * - Show/hide specific sections or cards on a page
 * - Enable/disable form fields
 * - Show/hide table columns or actions
 */
export function CanAccess({
  permission,
  permissions,
  requireAll = false,
  fallback = null,
  children,
}: CanAccessProps) {
  const { hasPermission, hasAnyPermission, hasAllPermissions } = useRBAC();

  let canAccess = false;

  if (permission) {
    canAccess = hasPermission(permission);
  } else if (permissions) {
    canAccess = requireAll
      ? hasAllPermissions(permissions)
      : hasAnyPermission(permissions);
  }

  return canAccess ? <>{children}</> : <>{fallback}</>;
}

/* -------------------------------------------------------------------------- */
/*                              RestrictedRoute                               */
/* -------------------------------------------------------------------------- */

interface RestrictedRouteProps {
  permission: string;
  fallback?: ReactNode;
  children: ReactNode;
}

/**
 * For Entire Pages
 * Use this to protect whole pages or routes. If user doesn't have permission,
 * they see "Access Denied" (or your custom fallback) instead of the page content.
 *
 * Use cases:
 * - Admin-only pages
 * - Settings pages
 * - Reports pages
 * - Any full page that requires specific permissions
 */
export function RestrictedRoute({
  permission,
  fallback,
  children,
}: RestrictedRouteProps) {
  const { hasPermission } = useRBAC();

  if (!hasPermission(permission)) {
    if (fallback) {
      return <>{fallback}</>;
    }

    return null;
  }

  return <>{children}</>;
}

/* -------------------------------------------------------------------------- */
/*                                  RoleGate                                  */
/* -------------------------------------------------------------------------- */

interface RoleGateProps {
  role: AnyRBACRole;
  fallback?: ReactNode;
  children: ReactNode;
}

/**
 * For Role-Based Rendering
 * Use this when you need to check the user's role rather than individual permissions.
 *
 * Use cases:
 * - Admin-only dashboards or sections
 * - Different UI layouts for different roles
 * - Role-specific welcome messages
 */
export function RoleGate({ role, fallback = null, children }: RoleGateProps) {
  const { hasRole } = useRBAC();

  return hasRole(role) ? <>{children}</> : <>{fallback}</>;
}

/* -------------------------------------------------------------------------- */
/*                        Convenience Portal Components                       */
/* -------------------------------------------------------------------------- */

/**
 * Wrap a full page or layout that should only be visible to SysAdmins.
 *
 * Example:
 * <SysAdminRoute>
 *   <SiteManagementPortal />
 * </SysAdminRoute>
 */
export function SysAdminRoute({ children }: { children: ReactNode }) {
  return (
    <RestrictedRoute permission={PERMISSIONS.SYSADMIN_PAGE}>
      {children}
    </RestrictedRoute>
  );
}

/**
 * Wrap a full page or layout that should only be visible to Org Admins and above.
 * (SysAdmins also get access because they have all permissions.)
 *
 * Example:
 * <OrgAdminRoute>
 *   <OrganizationPortal />
 * </OrgAdminRoute>
 */
export function OrgAdminRoute({ children }: { children: ReactNode }) {
  return (
    <RestrictedRoute permission={PERMISSIONS.ORGANIZATION_PORTAL}>
      {children}
    </RestrictedRoute>
  );
}

/**
 * Wrap a full page or layout that should only be visible to Project Admins and above.
 * (OrgAdmins and SysAdmins also get access.)
 *
 * Example:
 * <ProjectAdminRoute>
 *   <ProjectPortal />
 * </ProjectAdminRoute>
 */
export function ProjectAdminRoute({ children }: { children: ReactNode }) {
  return (
    <RestrictedRoute permission={PERMISSIONS.PROJECT_PORTAL}>
      {children}
    </RestrictedRoute>
  );
}
