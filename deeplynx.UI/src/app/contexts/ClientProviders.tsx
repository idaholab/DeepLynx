// src/app/contexts/ClientProviders.tsx
"use client";

import { SessionProvider } from "next-auth/react";
import { LanguageProvider } from "./Language";
import { Toaster } from "react-hot-toast";

export default function ClientProviders({
  children,
}: {
  children: React.ReactNode;
}) {
  // Always render SessionProvider to avoid hook errors
  return (
    <SessionProvider
      refetchOnWindowFocus={false}>
      <LanguageProvider>
        {children}
        <Toaster />
      </LanguageProvider>
    </SessionProvider>
  );
}