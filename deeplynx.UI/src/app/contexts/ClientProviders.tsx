// src/app/contexts/ClientProviders.tsx
"use client";

import { SessionProvider } from "next-auth/react";
import { LanguageProvider } from "./Language";
import { ToastProvider } from "./ToastProvider";
import { Toaster } from "react-hot-toast";

export default function ClientProviders({
  children,
}: {
  children: React.ReactNode;
}) {
  // Always render SessionProvider to avoid hook errors
  return (
    <SessionProvider refetchOnWindowFocus={false}>
      <LanguageProvider>
        <ToastProvider>
          {children}
          <Toaster />
        </ToastProvider>
      </LanguageProvider>
    </SessionProvider>
  );
}
