// src/app/contexts/OrganizationSessionProvider.tsx
"use client";

import React, {
  createContext,
  useContext,
  useEffect,
  useState,
  useCallback,
} from "react";
import type { ReactNode, Context } from "react";
import { getOrganization } from "@/app/lib/client_service/organization_services.client";
import { applyOrganizationTheme } from "@/app/lib/themes/themeMode";

export interface OrganizationSession {
  organizationId: string | number;
  organizationName: string;
  logoUrl?: string;
  banner?: string | null;
  themeName?: string | null;
}

interface OrganizationSessionContextType {
  organization: OrganizationSession | null;
  setOrganization: (organization: OrganizationSession) => void;
  clearOrganization: () => void;
  hasLoaded: boolean;
}

const OrganizationSessionContext: Context<
  OrganizationSessionContextType | undefined
> = createContext<OrganizationSessionContextType | undefined>(undefined);

export const useOrganizationSession = (): OrganizationSessionContextType => {
  const context = useContext(OrganizationSessionContext);
  if (!context) {
    throw new Error(
      "useOrganizationSession must be used within an OrganizationSessionProvider",
    );
  }
  return context;
};

export const OrganizationSessionProvider = ({
  children,
}: {
  children: ReactNode;
}): React.JSX.Element => {
  const [organization, setOrganizationState] =
    useState<OrganizationSession | null>(null);
  const [hasLoaded, setHasLoaded] = useState(false);

  // On mount, restore from localStorage OR cookies
  useEffect(() => {
    const storedLocal = localStorage.getItem("organizationSession");
    const storedCookie = document.cookie
      .split("; ")
      .find((row) => row.startsWith("organizationSession="))
      ?.split("=")[1];

    const stored =
      storedLocal || (storedCookie ? decodeURIComponent(storedCookie) : null);

    if (stored) {
      try {
        const parsed: OrganizationSession = JSON.parse(stored);
        setOrganizationState(parsed);
      } catch {
        console.warn("Failed to parse stored organization session.");
      }
    }
    setHasLoaded(true);
  }, []);

  useEffect(() => {
    if (!hasLoaded) return;

    applyOrganizationTheme(organization?.themeName ?? "default");
  }, [hasLoaded, organization?.themeName]);

  // Fetch full organization data when organizationId changes
  useEffect(() => {
    const fetchFullOrganization = async () => {
      if (!organization?.organizationId) return;

      try {
        // Fetch the complete organization data from the backend
        const fullOrg = await getOrganization(
          Number(organization.organizationId),
        );

        setOrganizationState((prev) => {
          if (!prev) return prev;

          const updated = {
            ...prev,
            banner: fullOrg.banner ?? null,
            themeName: fullOrg.theme ?? "default",
          };
          const serialized = JSON.stringify(updated);
          localStorage.setItem("organizationSession", serialized);

          const maxAge = 30 * 24 * 60 * 60;
          document.cookie = `organizationSession=${encodeURIComponent(
            serialized,
          )}; path=/; max-age=${maxAge}; SameSite=Lax`;

          return updated;
        });
      } catch (error) {
        console.error("Failed to fetch full organization data:", error);
      }
    };

    fetchFullOrganization();
  }, [organization?.organizationId]);

  // Save to BOTH localStorage and cookies
  const setOrganization = useCallback((org: OrganizationSession) => {
    setOrganizationState(org);
    const serialized = JSON.stringify(org);

    // Save to localStorage
    localStorage.setItem("organizationSession", serialized);

    // Save to cookie (expires in 30 days, accessible by server)
    const maxAge = 30 * 24 * 60 * 60; // 30 days in seconds
    document.cookie = `organizationSession=${encodeURIComponent(
      serialized,
    )}; path=/; max-age=${maxAge}; SameSite=Lax`;
  }, []);

  const clearOrganization = useCallback(() => {
    setOrganizationState(null);
    localStorage.removeItem("organizationSession");
    document.cookie = "organizationSession=; path=/; max-age=0";
  }, []);

  return (
    <OrganizationSessionContext.Provider
      value={{ organization, setOrganization, clearOrganization, hasLoaded }}
    >
      {children}
    </OrganizationSessionContext.Provider>
  );
};
