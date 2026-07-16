// src/app/components/LoginGuard.tsx
"use client";

import { useSession } from "next-auth/react";
import { useRouter } from "next/navigation";
import { useEffect } from "react";

interface LoginGuardProps {
  children: React.ReactNode;
  redirectTo?: string;
}

export default function LoginGuard({
  children,
  redirectTo = "/",
}: LoginGuardProps) {
  const { data: session, status } = useSession();
  const router = useRouter();

  useEffect(() => {
    // If loading, don't do anything yet
    if (status === "loading") return;

    // If authenticated, redirect away from login page
    if (status === "authenticated" && session) {
      router.push(redirectTo);
      return;
    }
  }, [status, session, router, redirectTo]);

  // Show loading while checking authentication
  if (status === "loading") {
    return (
      <div className="min-h-screen flex items-center justify-center app-header">
        <div className="text-center text-white">
          <div className="loading loading-spinner loading-lg"></div>
          <p className="mt-4">Loading...</p>
        </div>
      </div>
    );
  }

  // If authenticated, show loading while redirecting
  if (status === "authenticated" && session) {
    return (
      <div className="min-h-screen flex items-center justify-center app-header">
        <div className="text-center text-white">
          <div className="loading loading-spinner loading-lg"></div>
          <p className="mt-4">Redirecting to dashboard...</p>
        </div>
      </div>
    );
  }

  // User is not authenticated, show the login page
  return <>{children}</>;
}
