export const ORGANIZATION_THEMES = [
  {
    id: "default",
    label: "Default",
    swatches: ["#244c87", "#326bb8", "#d7a500"],
  },
  {
    id: "nric",
    label: "NRIC",
    swatches: ["#00bfb2", "#006e97", "#e3d935"],
  },
  {
    id: "nord",
    label: "Nord",
    swatches: ["#81a1c1", "#88c0d0", "#a3be8c"],
  },
  {
    id: "emerald",
    label: "Emerald",
    swatches: ["#42d393", "#3c64ed", "#e67d47"],
  },
] as const;

export type OrganizationThemeName = (typeof ORGANIZATION_THEMES)[number]["id"];

export const DEFAULT_ORGANIZATION_THEME: OrganizationThemeName = "default";

export const isOrganizationThemeName = (
  value: string | null | undefined,
): value is OrganizationThemeName =>
  ORGANIZATION_THEMES.some((theme) => theme.id === value);

export const resolveOrganizationTheme = (
  themeName: string | null | undefined,
): OrganizationThemeName =>
  isOrganizationThemeName(themeName) ? themeName : DEFAULT_ORGANIZATION_THEME;

export const resolveDaisyThemeName = (
  organizationTheme: string | null | undefined,
  mode: "light" | "dark",
): string => {
  const theme = resolveOrganizationTheme(organizationTheme);
  return mode === "dark" ? `${theme}-dark` : theme;
};
