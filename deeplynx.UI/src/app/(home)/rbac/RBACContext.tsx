// src/app/(home)/rbac/RBACContext.tsx
"use client";

import React, {
  createContext,
  useState,
  useEffect,
  ReactNode,
  useCallback,
} from "react";
import { Session } from "next-auth";
import Image from "next/image";

import {
  getCurrentUser,
  getLocalDevUser,
} from "@/app/lib/client_service/user_services.client";
import { UserAdminInfoDto } from "../types/responseDTOs";
import { useSafeSession } from "@/app/hooks/useSafeSession";
import type { CoreRBACRole } from "./rbacConfig";

/* -------------------------------------------------------------------------- */
/*                                   Types                                    */
/* -------------------------------------------------------------------------- */

export type RBACRole = CoreRBACRole;

export type RBACUser = UserAdminInfoDto & {
  roles: RBACRole[];
};

interface RBACContextType {
  user: RBACUser | null;
  setUser: React.Dispatch<React.SetStateAction<RBACUser | null>>;
  refreshUser: () => void;
  session: Session | null;
}

/* Props for provider: org/project context is passed in from outside */
interface RBACProviderProps {
  children: ReactNode;
  orgId?: number;
  projectId?: number;
}

/* -------------------------------------------------------------------------- */
/*                                RBAC Context                                */
/* -------------------------------------------------------------------------- */

export const RBACContext = createContext<RBACContextType | undefined>(
  undefined
);

/* -------------------------------------------------------------------------- */
/*                               RBAC Provider                                */
/* -------------------------------------------------------------------------- */

export function RBACProvider({
  children,
  orgId,
  projectId,
}: RBACProviderProps) {
  const disableAuth =
    process.env.NEXT_PUBLIC_DISABLE_FRONTEND_AUTHENTICATION === "true";

  const { data: session, status, update } = useSafeSession();
  const [user, setUser] = useState<RBACUser | null>(null);
  const [loading, setLoading] = useState(true);

  /* ------------------------------------------------------------------------ */
  /*                            Role Derivation                               */
  /* ------------------------------------------------------------------------ */

  const deriveRoles = (user: UserAdminInfoDto): RBACRole[] => {
    const roles: RBACRole[] = [];

    if (user.isSysAdmin) roles.push("sysAdmin");
    if (user.isOrgAdmin) roles.push("orgAdmin");
    if (user.isProjectAdmin) roles.push("projectAdmin");

    return roles;
  };

  /* ------------------------------------------------------------------------ */
  /*                            User Fetch Helpers                            */
  /* ------------------------------------------------------------------------ */

  const fetchDevUser = useCallback(async () => {
    setLoading(true);
    try {
      const response = await getLocalDevUser();
      const baseUser = response as UserAdminInfoDto;
      const userData: RBACUser = {
        ...baseUser,
        roles: deriveRoles(baseUser),
      };
      setUser(userData);
    } catch (error) {
      console.error("Failed to fetch dev user:", error);
      if (error instanceof Error) {
        console.error("Error details:", error.message);
      }
      setUser(null);
    } finally {
      setLoading(false);
    }
  }, []);

  const fetchUserData = useCallback(async () => {
    setLoading(true);
    try {
      const response = await getCurrentUser(orgId, projectId);

      const userData: RBACUser = {
        ...response,
        roles: deriveRoles(response),
      };
      setUser(userData);
    } catch (error) {
      console.error("Failed to fetch user data:", error);
      if (error instanceof Error) {
        console.error("Error details:", error.message);
      }
      setUser(null);
    } finally {
      setLoading(false);
    }
  }, [orgId, projectId]);

  /* ------------------------------------------------------------------------ */
  /*                           Initial User Loading                           */
  /* ------------------------------------------------------------------------ */

  useEffect(() => {
    if (disableAuth) {
      fetchDevUser();
    } else if (status === "authenticated") {
      fetchUserData();
    } else if (status === "unauthenticated") {
      setUser(null);
      setLoading(false);
    }
  }, [status, disableAuth, fetchDevUser, fetchUserData]);

  /* ------------------------------------------------------------------------ */
  /*                              Refresh Handler                             */
  /* ------------------------------------------------------------------------ */

  const refreshUser = useCallback(async () => {
    if (disableAuth) {
      fetchDevUser();
    } else {
      // Force NextAuth to re-validate the session with the server
      const refreshedSession = await update();

      if (refreshedSession) {
        fetchUserData();
      } else {
        // Session is truly expired/invalid, sign them out
        const { signOut } = await import("next-auth/react");
        signOut({ callbackUrl: "/login" });
      }
    }
  }, [disableAuth, update, fetchDevUser, fetchUserData]);

  // Handle session errors (token refresh failures)
  useEffect(() => {
    if (
      session?.error === "RefreshAccessTokenError" ||
      session?.error === "NoRefreshToken"
    ) {
      // Token refresh failed, sign them out
      const handleSessionError = async () => {
        const { signOut } = await import("next-auth/react");
        signOut({ callbackUrl: "/login/signin" });
      };
      handleSessionError();
    }
  }, [session?.error]);

  /* ------------------------------------------------------------------------ */
  /*                               Load States                                */
  /* ------------------------------------------------------------------------ */

  if (loading) {
    return (
      <div
        className="login"
        style={{
          display: "flex",
          justifyContent: "center",
          alignItems: "center",
          height: "100vh",
        }}
      >
        <Image
          src="/assets/nexusWhite.png"
          alt="DeepLynx logo"
          width={265.8}
          height={113.9}
          priority
        />
      </div>
    );
  }

  if (status === "unauthenticated" && !disableAuth) {
    return <>{children}</>;
  }

  if (!user && (status === "authenticated" || disableAuth)) {
    return (
      <div
        style={{
          display: "flex",
          justifyContent: "center",
          alignItems: "center",
          height: "100vh",
          flexDirection: "column",
          gap: "16px",
        }}
      >
        <div className="text-error font-semibold text-lg">
          Failed to load user data
        </div>
        <button onClick={refreshUser} className="btn btn-primary">
          Retry
        </button>
      </div>
    );
  }

  /* ------------------------------------------------------------------------ */
  /*                               Main Render                                */
  /* ------------------------------------------------------------------------ */

  return (
    <RBACContext.Provider value={{ user, setUser, refreshUser, session }}>
      {children}
    </RBACContext.Provider>
  );
}
