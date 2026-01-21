// src/app/(home)/components/LayoutShell.tsx
"use client";

import { useLanguage } from "@/app/contexts/Language";
import {
  AdjustmentsHorizontalIcon,
  ArrowRightStartOnRectangleIcon,
  BookOpenIcon,
  ChevronDownIcon,
  ChevronUpIcon,
  Cog6ToothIcon,
  GlobeAmericasIcon,
  QuestionMarkCircleIcon,
  UserCircleIcon,
  UserGroupIcon,
  CommandLineIcon,
} from "@heroicons/react/24/outline";
import Image from "next/image";
import Link from "next/link";
import { useRouter } from "next/navigation";
import React, { useState, useEffect } from "react";
import SideMenu from "./SideMenu";
import AvatarCell from "./Avatar";
import { signOut } from "next-auth/react";
import { OrgAdminRoute, RoleGate, SysAdminRoute } from "../rbac/RBACComponents";
import { useRBAC } from "../rbac/useRBAC";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import { OrganizationResponseDto } from "../types/responseDTOs";
import { useSafeSession } from "@/app/hooks/useSafeSession";
import {
  getAllOrganizationsForUser,
  getOrganizationLogoUrl,
} from "@/app/lib/client_service/organization_services.client";
import TopBanner from "./VulnerabilityBanner";

const LayoutShell: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const { t } = useLanguage();
  const router = useRouter();

  const isAuthDisabled =
    process.env.NEXT_PUBLIC_DISABLE_FRONTEND_AUTHENTICATION === "true";

  const { data: session } = useSafeSession();
  const { user } = useRBAC();
  const { organization, setOrganization } = useOrganizationSession();

  const [selectedItem, setSelectedItem] = useState<string>("");
  const [organizations, setOrganizations] = useState<OrganizationResponseDto[]>(
    [],
  );
  const [loadingOrgs, setLoadingOrgs] = useState(false);
  const [isOrgDropdownOpen, setIsOrgDropdownOpen] = useState(false);
  const [isUserDropdownOpen, setIsUserDropdownOpen] = useState(false);
  const [orgLogoUrl, setOrgLogoUrl] = useState<string | null>(null);

  // Handle menu toggle
  const [isMenuCollapsed, setIsMenuCollapsed] = React.useState(false);

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
    setOrganization({
      organizationId: org.id,
      organizationName: org.name,
    });

    // Close dropdown
    setIsOrgDropdownOpen(false);

    // Navigate to home page - this will trigger a full server-side re-render
    router.push("/");
  };

  const formatUserName = (fullName?: string | null): string => {
    if (!fullName) return "";

    const parts = fullName.trim().split(/\s+/);
    const firstName = parts[0] ?? "";
    const lastName = parts[parts.length - 1] ?? "";
    return [firstName, lastName].filter(Boolean).join(" ");
  };

  // When auth is disabled, use RBAC user. When enabled, use session.
  const displayName = isAuthDisabled
    ? user?.name || ""
    : session?.user?.name || "";

  const displayEmail = isAuthDisabled
    ? user?.email || ""
    : session?.user?.email || "";

  const displayImage = session?.user?.image;

  // Function to handle item click events
  const handleItemClick = (
    item: string,
    event: React.MouseEvent<HTMLElement>,
  ) => {
    event.preventDefault();
    setSelectedItem(item);
    router.push(item);
  };

  return (
    <div className="flex flex-col min-h-screen bg-base-100 text-base-content">
      {/* Top Banner */}
      <TopBanner />
      {/* Banner/Header */}
      <header className="bg-[var(--base-400)] text-primary-content flex justify-between items-center px-5 py-3 z-50 fixed w-full top-6">
        {/* Organization Switcher */}
        <div className="dropdown">
          <div className="flex items-center">
            <div className="flex items-center py-2 gap-3">
              {/* Organization Logo (if exists) */}
              {orgLogoUrl ? (
                <div className="avatar">
                  <div className="w-10 h-10 rounded-lg overflow-hidden bg-base-100 flex items-center justify-center relative">
                    <Image
                      src={orgLogoUrl}
                      alt={organization?.organizationName || "Organization"}
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
                <UserGroupIcon className="size-8" />
              )}

              <div className="flex flex-col">
                <span className="text-xs opacity-70">
                  {t.translations.ORGANIZATION}
                </span>
                <h1 className="text-lg font-bold truncate">
                  {organization?.organizationName || "No Organization"}
                </h1>
              </div>
            </div>
            <button
              tabIndex={0}
              onClick={() => setIsOrgDropdownOpen(!isOrgDropdownOpen)}
              className="btn btn-ghost btn-sm btn-circle"
            >
              {isOrgDropdownOpen ? (
                <ChevronUpIcon className="size-5" />
              ) : (
                <ChevronDownIcon className="size-5" />
              )}
            </button>
          </div>
          {isOrgDropdownOpen && (
            <ul
              tabIndex={0}
              className="dropdown-content menu bg-base-100 text-base-content rounded-box z-[100] w-72 max-w-72 p-2 shadow-xl border border-base-300 mt-2"
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
                    <Link
                      href="/select-org"
                      className="hover:bg-base-200"
                      onClick={() => setIsOrgDropdownOpen(false)}
                    >
                      <UserGroupIcon className="size-5" />
                      {t.translations.VIEW_ALL_ORGANIZATIONS}
                    </Link>
                  </li>
                </>
              )}
            </ul>
          )}
        </div>
        <div>
          <Image
            src="/assets/nexusWhite.png"
            alt="Logo"
            height={20}
            width={150}
            className="rounded cursor-pointer"
            onClick={() => router.push("/")}
          />
        </div>
      </header>
      {/* Page Content */}
      <div className="flex h-full z-0 mt-6">
        {/* Side Menu */}
        <div className="fixed top-20 bottom-0 flex z-40">
          <aside
            className={
              "h-full shadow-xl w-18 login text-primary-content p-4 transition-all duration-300 flex flex-col"
            }
          >
            <ul className="mt-20 flex-grow">
              <li>
                <Link href={"/"}>
                  <GlobeAmericasIcon className="size-10" />
                </Link>
              </li>
              <li className="mt-5">
                <Link
                  href="/data_catalog"
                  onClick={(e) => handleItemClick("/data_catalog", e)}
                >
                  <BookOpenIcon className="size-10" />
                </Link>
              </li>
              <OrgAdminRoute>
                <li className="mt-5">
                  <Link
                    href="/organization_management"
                    onClick={(e) =>
                      handleItemClick("/organization_management", e)
                    }
                  >
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
              <li className="mt-5">
                <Link
                  href={
                    process.env.NEXT_PUBLIC_API_URL
                      ? `${process.env.NEXT_PUBLIC_API_URL}/scalar`
                      : "/api/v1/scalar"
                  }
                  prefetch={false}
                >
                  <CommandLineIcon className="size-10" />
                </Link>
              </li>
              <li className="mt-5">
                <div className="relative">
                  <div
                    role="button"
                    className="cursor-pointer"
                    onClick={() => setIsUserDropdownOpen(!isUserDropdownOpen)}
                  >
                    <UserCircleIcon className="size-10" />
                  </div>
                  {isUserDropdownOpen && (
                    <>
                      {/* Backdrop to close dropdown when clicking outside */}
                      <div
                        className="fixed inset-0 z-[100]"
                        onClick={() => setIsUserDropdownOpen(false)}
                      />
                      <ul className="menu bg-base-100 text-base-content rounded-box w-auto min-w-52 max-w-[90vw] p-2 shadow-xl border border-base-300 fixed left-20 bottom-4 z-[101]">
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
                            onClick={() => setIsUserDropdownOpen(false)}
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
                <Link
                  href={
                    process.env.NEXT_PUBLIC_DOCS_PATH
                      ? `${process.env.NEXT_PUBLIC_DOCS_PATH}`
                      : "/docs"
                  }
                >
                  <QuestionMarkCircleIcon className="size-10" />
                </Link>
              </li>
              <span className="text-xs font-bold text-base-200/50">V0.3.0</span>
            </ul>
          </aside>
        </div>
        <SideMenu onToggle={handleMenuToggle} />
        <main
          className={`transition-all duration-300 w-full mt-20 ${
            isMenuCollapsed ? "ml-40" : "ml-82"
          }`}
        >
          {children}
        </main>
      </div>
    </div>
  );
};

export default LayoutShell;
