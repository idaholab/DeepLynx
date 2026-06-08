import { resolveDaisyThemeName } from "./organizationTheme";

export const THEME_MODE_STORAGE_KEY = "dlx-theme-mode";

export type ThemeMode = "light" | "dark";

export const getStoredThemeMode = (): ThemeMode => {
  if (typeof window === "undefined") return "light";

  return localStorage.getItem(THEME_MODE_STORAGE_KEY) === "dark"
    ? "dark"
    : "light";
};

export const applyOrganizationTheme = (
  organizationTheme: string | null | undefined,
  mode: ThemeMode = getStoredThemeMode(),
) => {
  const daisyTheme = resolveDaisyThemeName(organizationTheme, mode);
  if (typeof document === "undefined") return;

  document.documentElement.setAttribute("data-theme", daisyTheme);
};
