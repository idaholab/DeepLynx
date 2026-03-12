// src/app/(home)/rbac/RBACWrapper.tsx
"use client";

import React from "react";
import { usePathname } from "next/navigation";
import { RBACProvider } from "./RBACContext";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import { useProjectSession } from "@/app/contexts/ProjectSessionProvider";

type Props = {
  children: React.ReactNode;
};

const normalizeId = (
  value: string | number | undefined | null
): number | undefined => {
  if (value === undefined || value === null) return undefined;
  const num = Number(value);
  return Number.isFinite(num) ? num : undefined;
};

export function RBACWrapper({ children }: Props) {
  const pathname = usePathname();
  const { organization, hasLoaded: hasLoadedOrganization } =
    useOrganizationSession();
  const { project, hasLoaded: hasLoadedProject } = useProjectSession();

  const orgId = normalizeId(organization?.organizationId);
  const routeProjectId = pathname?.match(
    /^\/(?:project|project_management)\/(\d+)(?:\/|$)/,
  )?.[1];
  const projectId = normalizeId(project?.projectId ?? routeProjectId);

  if (!hasLoadedOrganization || !hasLoadedProject) {
    return null;
  }

  return (
    <RBACProvider orgId={orgId} projectId={projectId}>
      {children}
    </RBACProvider>
  );
}
