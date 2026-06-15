import "server-only";

import { getCurrentUserServer } from "./user_services.server";
import { redirect } from "next/navigation";

const UNAUTHORIZED_ROUTE = "/unauthorized";

export async function requireSystemAdminServer(organizationId?: number) {
  const currentUser = await getCurrentUserServer(organizationId);

  if (!currentUser.isSysAdmin) {
    redirect(UNAUTHORIZED_ROUTE);
  }
}

export async function requireOrgAdminServer(organizationId?: number) {
  const currentUser = await getCurrentUserServer(organizationId);

  if (!currentUser.isSysAdmin && !currentUser.isOrgAdmin) {
    redirect(UNAUTHORIZED_ROUTE);
  }
}

export async function requireProjectAdminServer(
  organizationId: number,
  projectId: number,
) {
  const currentUser = await getCurrentUserServer(organizationId, projectId);

  if (
    !currentUser.isSysAdmin &&
    !currentUser.isOrgAdmin &&
    !currentUser.isProjectAdmin
  ) {
    redirect(UNAUTHORIZED_ROUTE);
  }
}
