// src/app/(home)/components/LayoutShell.tsx
"use client";

import { useLanguage } from "@/app/contexts/Language";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import { useProjectSession } from "@/app/contexts/ProjectSessionProvider";
import { useSafeSession } from "@/app/hooks/useSafeSession";
import {
  getAllOrganizationsForUser,
  getOrganizationLogoUrl,
} from "@/app/lib/client_service/organization_services.client";
import { isRunHidden } from "@/app/lib/feature_flags";
import {
  AdjustmentsHorizontalIcon,
  ArrowRightStartOnRectangleIcon,
  Bars3Icon,
  BookOpenIcon,
  ChevronDownIcon,
  Cog6ToothIcon,
  CommandLineIcon,
  GlobeAmericasIcon,
  PlayIcon,
  QuestionMarkCircleIcon,
  UserCircleIcon,
  UserGroupIcon,
} from "@heroicons/react/24/outline";
import { signOut } from "next-auth/react";
import Image from "next/image";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, useState, type ReactNode } from "react";
import { OrgAdminRoute, SysAdminRoute } from "../rbac/RBACComponents";
import { useRBAC } from "../rbac/useRBAC";
import { OrganizationResponseDto } from "../types/responseDTOs";
import AvatarCell from "./Avatar";
import { Banner } from "./Banner";
import SideMenu from "./SideMenu";
import TopBanner from "./VulnerabilityBanner";

const LayoutShell = ({ children }: { children: ReactNode }) => {
  const { t } = useLanguage();
  const router = useRouter();
  const pathname = usePathname();

  const isAuthDisabled =
    process.env.NEXT_PUBLIC_DISABLE_FRONTEND_AUTHENTICATION === "true";

  const { data: session } = useSafeSession();
  const { user } = useRBAC();
  const { organization, setOrganization } = useOrganizationSession();
  const { project, clearProject } = useProjectSession();

  const [organizations, setOrganizations] = useState<OrganizationResponseDto[]>(
    [],
  );
  const [loadingOrgs, setLoadingOrgs] = useState(false);
  const [isUserDropdownOpen, setIsUserDropdownOpen] = useState(false);
  const [isMobileNavOpen, setIsMobileNavOpen] = useState(false);
  const [orgLogoUrl, setOrgLogoUrl] = useState<string | null>(null);

  // Handle menu toggle
  const [isMenuCollapsed, setIsMenuCollapsed] = useState(false);
  const displayName = isAuthDisabled
    ? (user?.name ?? "")
    : (session?.user?.name ?? "");
  const displayEmail = isAuthDisabled
    ? (user?.email ?? "")
    : (session?.user?.email ?? "");

  // Fetch organizations for the switcher
  useEffect(() => {
    const fetchOrganizations = async () => {
      try {
        setLoadingOrgs(true);
        const orgs = await getAllOrganizationsForUser(true);
        setOrganizations(orgs);
      } catch (error) {
        console.error("Failed to fetch organizations:", error);
      } finally {
        setLoadingOrgs(false);
      }
    };

    fetchOrganizations();
  }, []);

  // Load organization logo
  useEffect(() => {
    const loadOrganizationLogo = async () => {
      if (!organization?.organizationId) {
        setOrgLogoUrl(null);
        return;
      }

      try {
        const logoUrl = await getOrganizationLogoUrl(
          organization.organizationId as number,
        );
        setOrgLogoUrl(logoUrl);
      } catch (error) {
        console.error("Failed to load organization logo:", error);
        setOrgLogoUrl(null);
      }
    };

    loadOrganizationLogo();
  }, [organization?.organizationId]);

  useEffect(() => {
    setIsMobileNavOpen(false);
    setIsUserDropdownOpen(false);
  }, [pathname]);

  const handleMenuToggle = (isCollapsed: boolean) => {
    setIsMenuCollapsed(isCollapsed);
  };

  const handleLogout = async () => {
    try {
      if (isAuthDisabled) {
        // If auth is disabled, just redirect to home
        router.push("/");
        return;
      }

      await signOut({
        callbackUrl: "/login/signin",
        redirect: true,
      });
    } catch (error) {
      console.error("Logout error:", error);
    }
  };

  const handleOrganizationSwitch = (org: OrganizationResponseDto) => {
    clearProject();

    setOrganization({
      organizationId: org.id,
      organizationName: org.name,
      banner: org.banner ?? null,
      themeName: org.theme ?? "default",
    });

    router.push("/");
  };

  const formatUserName = (fullName?: string | null): string => {
    if (!fullName) return "";

    const parts = fullName.trim().split(/\s+/);
    const firstName = parts[0] ?? "";
    const lastName = parts[parts.length - 1] ?? "";
    return [firstName, lastName].filter(Boolean).join(" ");
  };

  const displayImage = session?.user?.image;

  return (
    <div className="flex flex-col min-h-screen bg-base-100 text-base-content">
      {/* Top Banner */}
      <TopBanner />
      {/* Header */}
      <header className="app-header text-neutral-content flex justify-between items-center gap-2 px-3 sm:px-5 py-2 sm:py-3 z-50 fixed w-full top-6">
        {/* Organization Switcher */}
        <div className="flex items-center gap-2 min-w-0">
          <button
            type="button"
            onClick={() => setIsMobileNavOpen((prev) => !prev)}
            className="btn btn-ghost btn-sm btn-circle lg:hidden shrink-0"
            aria-label="Toggle navigation"
          >
            <Bars3Icon className="size-6" />
          </button>
          <div className="dropdown min-w-0 group">
            <div
              tabIndex={0}
              role="button"
              className="flex items-center gap-3 min-w-0 cursor-pointer py-2"
            >
              {/* Organization Logo (if exists) */}
              {orgLogoUrl ? (
                <div className="avatar">
                  <div className="w-10 h-10 rounded-lg overflow-hidden bg-base-100 flex items-center justify-center relative">
                    <Image
                      src={orgLogoUrl}
                      alt={organization?.organizationName ?? "No Organization"}
                      fill
                      sizes="40px"
                      className="object-contain p-1"
                      onError={() => {
                        // If image fails to load, hide it
                        setOrgLogoUrl(null);
                      }}
                    />
                  </div>
                </div>
              ) : (
                // Fallback to UserGroupIcon if no logo
                <UserGroupIcon className="size-7 sm:size-8 shrink-0" />
              )}

              <div className="flex flex-col min-w-0">
                <span className="text-xs opacity-70">
                  {t.translations.ORGANIZATION}
                </span>
                <h1 className="text-base sm:text-lg font-bold truncate max-w-[45vw] sm:max-w-[18rem]">
                  {organization?.organizationName ?? "No Organization"}
                </h1>
              </div>
              <ChevronDownIcon className="size-7 shrink-0 transition-transform group-focus-within:rotate-180" />
            </div>
            <ul
              tabIndex={0}
              className="dropdown-content menu bg-base-100 text-base-content rounded-box z-[100] w-72 max-w-[90vw] p-2 shadow-xl border border-base-300 mt-2"
            >
              {loadingOrgs ? (
                <li>
                  <div className="flex justify-center p-4">
                    <span className="loading loading-spinner loading-sm"></span>
                  </div>
                </li>
              ) : (
                <>
                  <li className="menu-title">
                    <span className="text-base-content/70">
                      {t.translations.SWITCH_ORGANIZATION}
                    </span>
                  </li>
                  {organizations.map((org) => (
                    <li key={org.id} className="w-full">
                      <a
                        onClick={() => handleOrganizationSwitch(org)}
                        className={`flex items-center gap-2 w-full max-w-full ${
                          organization?.organizationId === org.id
                            ? "active bg-info/60"
                            : ""
                        }`}
                      >
                        <div className="min-w-0 flex-1 overflow-hidden">
                          <div className=" font-medium truncate">
                            {org.name}
                          </div>
                          {org.description && (
                            <div className="text-xs opacity-70 truncate">
                              {org.description}
                            </div>
                          )}
                        </div>
                        {organization?.organizationId === org.id && (
                          <span className="badge badge-sm shrink-0 whitespace-nowrap !text-base-content">
                            {t.translations.CURRENT}
                          </span>
                        )}
                      </a>
                    </li>
                  ))}
                  <div className="divider my-1"></div>
                  <li>
                    <Link href="/select-org" className="hover:bg-base-200">
                      <UserGroupIcon className="size-5" />
                      {t.translations.VIEW_ALL_ORGANIZATIONS}
                    </Link>
                  </li>
                </>
              )}
            </ul>
          </div>
        </div>
        <div className="shrink-0">
          <Image
            src="/assets/nexusWhite.png"
            alt="Logo"
            height={20}
            width={150}
            className="rounded cursor-pointer w-[120px] sm:w-[150px] h-auto"
            onClick={() => router.push("/")}
          />
        </div>
      </header>
      {/* Page Content */}
      <div className="flex h-full z-0 mt-6 w-full overflow-x-auto">
        {isMobileNavOpen && (
          <button
            type="button"
            className="fixed inset-0 bg-black/40 z-30 lg:hidden"
            onClick={() => setIsMobileNavOpen(false)}
            aria-label="Close navigation overlay"
          />
        )}
        {/* Side Menu */}
        <div
          className={`fixed top-20 bottom-0 hidden lg:flex ${
            isUserDropdownOpen ? "z-[70]" : "z-[55]"
          }`}
        >
          <aside
            className={
              "h-full shadow-xl w-18 app-header-inverted text-primary-content p-4 transition-all duration-300 flex flex-col"
            }
          >
            <ul className="mt-20 flex-grow">
              <li>
                <Link href={"/"} onClick={clearProject}>
                  <GlobeAmericasIcon className="size-10" />
                </Link>
              </li>
              <li className="mt-5">
                <Link href="/data_catalog/all_records">
                  <BookOpenIcon className="size-10" />
                </Link>
              </li>
              {!isRunHidden() && (
                <li className="mt-5">
                  <Link href="/run">
                    <PlayIcon className="size-10" />
                  </Link>
                </li>
              )}
              <OrgAdminRoute>
                <li className="mt-5">
                  <Link href="/organization_management">
                    <AdjustmentsHorizontalIcon className="size-10" />
                  </Link>
                </li>
              </OrgAdminRoute>
            </ul>

            {/* Bottom section */}
            <ul className="mt-auto">
              <li className="mt-5">
                <SysAdminRoute>
                  <Link href={"/site_management"} prefetch={false}>
                    <Cog6ToothIcon className="size-10" />
                  </Link>
                </SysAdminRoute>
              </li>
              <li className="mt-5 id-tooltip group relative">
                <Link
                  target="_blank"
                  href={
                    process.env.NEXT_PUBLIC_API_URL
                      ? `${process.env.NEXT_PUBLIC_API_URL}/scalar`
                      : "/api/v1/scalar"
                  }
                  prefetch={false}
                >
                  <CommandLineIcon className="size-10" />
                </Link>
                <div
                  role="tooltip"
                  className="invisible opacity-0 group-hover:visible group-hover:opacity-100 transition-opacity duration-150 absolute left-full top-1/2 -translate-y-1/2 ml-3 z-[60] bg-base-100 text-base-content rounded-box border border-base-300 shadow-xl p-4 min-w-[22rem] pointer-events-none"
                >
                  <p className="text-xs text-base-content/70 mb-3 leading-snug">
                    {t.translations.API_ID_TOOLTIP_DESCRIPTION}
                  </p>
                  <div className="flex flex-col gap-2">
                    <div className="flex items-baseline justify-between gap-3">
                      <span className="text-xs font-semibold text-base-content/70 uppercase tracking-wide whitespace-nowrap">
                        {t.translations.ORGANIZATION_ID}
                      </span>
                      <span className="text-sm font-mono text-base-content break-all text-right">
                        {organization?.organizationId ?? "—"}
                      </span>
                    </div>
                    <div className="divider my-0"></div>
                    <div className="flex items-baseline justify-between gap-3">
                      <span className="text-xs font-semibold text-base-content/70 uppercase tracking-wide whitespace-nowrap">
                        {t.translations.PROJECT_ID}
                      </span>
                      <span className="text-sm font-mono text-base-content break-all text-right">
                        {project?.projectId ?? "—"}
                      </span>
                    </div>
                  </div>
                </div>
              </li>
              <li className="mt-5">
                <div className="relative flex justify-center">
                  <button
                    type="button"
                    className="cursor-pointer"
                    onClick={() => setIsUserDropdownOpen((open) => !open)}
                  >
                    <UserCircleIcon className="size-10" />
                  </button>
                  {isUserDropdownOpen && (
                    <>
                      <div
                        className="fixed inset-0 z-[100]"
                        onClick={() => setIsUserDropdownOpen(false)}
                      />
                      <ul className="menu bg-base-100 text-base-content rounded-box w-auto min-w-52 max-w-[90vw] p-2 shadow-xl border border-base-300 fixed right-4 lg:right-auto lg:left-20 bottom-4 z-[101]">
                        <li>
                          <div className="flex bg-base-100">
                            <AvatarCell
                              image={displayImage ?? undefined}
                              name={displayName}
                              size={20}
                            />
                            <div className="flex-1 min-w-0">
                              <h1 className="font-bold text-lg text-base-content">
                                {formatUserName(displayName)}
                              </h1>
                              <p className="text-base-content/70 text-sm">
                                {displayEmail}
                              </p>
                            </div>
                          </div>
                        </li>
                        <li className="mt-2">
                          <Link
                            href="/settings"
                            className="text-base-content hover:bg-base-200"
                          >
                            <Cog6ToothIcon className="size-6" />
                            {t.translations.SETTINGS}
                          </Link>
                        </li>
                        <li>
                          <button
                            className="text-base-content hover:bg-base-200"
                            onClick={() => {
                              setIsUserDropdownOpen(false);
                              handleLogout();
                            }}
                          >
                            <ArrowRightStartOnRectangleIcon className="size-6" />
                            {t.translations.LOGOUT}
                          </button>
                        </li>
                      </ul>
                    </>
                  )}
                </div>
              </li>
              <li className="mt-5 mb-16">
                <Link href={process.env.NEXT_PUBLIC_DOCS_PATH ?? "/docs"}>
                  <QuestionMarkCircleIcon className="size-10" />
                </Link>
              </li>
              <span className="text-xs font-bold text-base-200/50">v0.7.0</span>
            </ul>
          </aside>
        </div>
        <SideMenu
          onToggle={handleMenuToggle}
          mobileOpen={isMobileNavOpen}
          onMobileClose={() => setIsMobileNavOpen(false)}
        />
        <main
          className={`transition-all duration-300 min-w-[750px] flex-1 w-full mt-20 ml-0 ${
            isMenuCollapsed ? "lg:ml-40" : "lg:ml-82"
          }`}
        >
          {/* Organization Banner */}
          <div className="sticky top-25 z-20">
            <Banner />
          </div>

          {/* Page Content */}
          {children}
        </main>
      </div>
    </div>
  );
};

export default LayoutShell;
