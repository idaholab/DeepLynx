// app/components/ThemeToggle.tsx
"use client";
import { useEffect, useState } from "react";
import { MoonIcon, SunIcon } from "@heroicons/react/24/outline";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import {
  applyOrganizationTheme,
  getStoredThemeMode,
  THEME_MODE_STORAGE_KEY,
  type ThemeMode,
} from "@/app/lib/themes/themeMode";

const THEME_KEY = THEME_MODE_STORAGE_KEY;

export default function ThemeToggle() {
  const { organization } = useOrganizationSession();
  const [isDark, setIsDark] = useState(false);

  // Sync from local storage on mount and across tabs.
  useEffect(() => {
    setIsDark(getStoredThemeMode() === "dark");

    const onStorage = (e: StorageEvent) => {
      if (e.key === THEME_KEY && e.newValue) setIsDark(e.newValue === "dark");
    };
    window.addEventListener("storage", onStorage);

    return () => {
      window.removeEventListener("storage", onStorage);
    };
  }, []);

  useEffect(() => {
    applyOrganizationTheme(organization?.themeName);
  }, [organization?.themeName]);

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const checked = e.target.checked;
    setIsDark(checked);

    const next: ThemeMode = checked ? "dark" : "light";
    localStorage.setItem(THEME_KEY, next);
    applyOrganizationTheme(organization?.themeName, next);
  };

  return (
    <label className="toggle text-base-content">
      <input
        type="checkbox"
        checked={isDark}
        onChange={handleChange}
      />
      <SunIcon className="size-4" />
      <MoonIcon className="size-4" />
    </label>
  );
}
