"use client";

import { XMarkIcon } from "@heroicons/react/24/outline";
import React, { useCallback, useContext, useState } from "react";

type ToastType = "success" | "error" | "info" | "warning";

type AppToast = {
  id: number;
  type: ToastType;
  message: string;
  position?: string;
};

type ToastContextValue = {
  showToast: (type: ToastType, message: string, position?: string) => void;
};

const ToastContext = React.createContext<ToastContextValue | null>(null);

const alertClassByType: Record<ToastType, string> = {
  success: "alert-success",
  error: "alert-error",
  info: "alert-info",
  warning: "alert-warning",
};

export function ToastProvider({ children }: { children: React.ReactNode }) {
  const [toasts, setToasts] = useState<AppToast[]>([]);

  const dismissToast = useCallback((id: number) => {
    setToasts((current) => current.filter((toast) => toast.id !== id));
  }, []);

  const showToast = useCallback(
    (type: ToastType, message: string, position = "toast-top toast-end") => {
      const id = Date.now();
      setToasts((current) => [...current, { id, type, message, position }]);

      window.setTimeout(() => dismissToast(id), 4000);
    },
    [dismissToast],
  );

  return (
    <ToastContext.Provider value={{ showToast }}>
      {children}

      {toasts.map((toast) => (
        <div className={`toast z-50 pt-16 ${toast.position}`}>
          <div
            key={toast.id}
            role={toast.type === "error" ? "alert" : "status"}
            className={`alert ${alertClassByType[toast.type]} shadow-lg`}
          >
            <span>{toast.message}</span>
            <button
              type="button"
              className="btn btn-ghost btn-xs"
              onClick={() => dismissToast(toast.id)}
            >
              <XMarkIcon className="size-4" />
            </button>
          </div>
        </div>
      ))}
    </ToastContext.Provider>
  );
}

export function useToast() {
  const context = useContext(ToastContext);

  if (!context) {
    throw new Error("useToast must be used inside ToastProvider");
  }
  return context;
}
