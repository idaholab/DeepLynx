// src/app/(home)/components/SideMenu.tsx
"use client";

import React, { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";

import { useLanguage } from "@/app/contexts/Language";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import { useProjectSession } from "@/app/contexts/ProjectSessionProvider";
import { getAllProjects } from "@/app/lib/client_service/projects_services.client";
import { ProjectAdminRoute } from "../rbac/RBACComponents";

import type { ProjectResponseDto } from "../types/responseDTOs";

import {
  AdjustmentsHorizontalIcon,
  ArrowUpTrayIcon,
  BellIcon,
  ChevronDownIcon,
  ChevronLeftIcon,
  ChevronRightIcon,
  ChevronUpIcon,
  FolderIcon,
  RectangleGroupIcon,
} from "@heroicons/react/24/outline";

/* -------------------------------------------------------------------------- */
/*                                   Types                                    */
/* -------------------------------------------------------------------------- */

interface SideMenuProps {
  onToggle: (isCollapsed: boolean) => void;
}

/* -------------------------------------------------------------------------- */
/*                               Helper Constants                             */
/* -------------------------------------------------------------------------- */

const orgAllowedPaths = ["/organization_management"];

/* -------------------------------------------------------------------------- */
/*                               SideMenu Component                           */
/* -------------------------------------------------------------------------- */

const SideMenu: React.FC<SideMenuProps> = ({ onToggle }) => {
  /* --------------------------------- Hooks -------------------------------- */

  const { t } = useLanguage();
  const router = useRouter();
  const pathname = usePathname();

  const { project, setProject } = useProjectSession();
  const { organization } = useOrganizationSession();

  const isOrgPortalRoute = pathname?.startsWith("/organization_management");

  /* --------------------------------- State -------------------------------- */

  const [selectedItem, setSelectedItem] = useState<string>("");
  const [isCollapsed, setIsCollapsed] = useState<boolean>(isOrgPortalRoute);
  const [isProjectsExpanded, setIsProjectsExpanded] = useState<boolean>(false);

  const [projects, setProjects] = useState<ProjectResponseDto[]>([]);
  const [loadingProjects, setLoadingProjects] = useState(false);
  const [activeProject, setActiveProject] = useState<ProjectResponseDto>();

  /* ---------------------------- Data Fetching ----------------------------- */

  const fetchProjects = useCallback(async () => {
    if (!organization) return;

    try {
      setLoadingProjects(true);
      const data = await getAllProjects(
        organization.organizationId as number,
        true
      );
      setProjects(data);
    } catch (error) {
      console.error("Failed to fetch projects:", error);
    } finally {
      setLoadingProjects(false);
    }
  }, [organization]);

  /* -------------------------------- Effects ------------------------------- */

  // Fetch projects when organization changes
  useEffect(() => {
    fetchProjects();
  }, [fetchProjects]);

  // Clear project context and active project when organization changes
  useEffect(() => {
    if (organization) {
      setActiveProject(undefined);
    }
  }, [organization?.organizationId, setProject]);

  // Sync activeProject with the project context
  useEffect(() => {
    if (project?.projectId && projects.length > 0) {
      const foundProject = projects.find(
        (p) => p.id.toString() === project.projectId
      );
      if (foundProject) {
        setActiveProject(foundProject);
      } else {
        setActiveProject(undefined);
      }
    } else if (!project?.projectId) {
      setActiveProject(undefined);
    }
  }, [project, projects, setProject]);

  // Keep selectedItem in sync with pathname
  useEffect(() => {
    setSelectedItem(pathname);
  }, [pathname]);

  // Load selectedItem from localStorage on mount
  useEffect(() => {
    const savedItem = localStorage.getItem("selectedItem");
    if (savedItem) setSelectedItem(savedItem);
  }, []);

  // Persist selectedItem to localStorage
  useEffect(() => {
    localStorage.setItem("selectedItem", selectedItem);
  }, [selectedItem]);

  // Notify parent of collapse state (single source of truth)
  useEffect(() => {
    onToggle(isCollapsed);
  }, [isCollapsed, onToggle]);

  // Force collapsed state on org portal routes
  useEffect(() => {
    if (isOrgPortalRoute) {
      setIsCollapsed(true);
    }
  }, [isOrgPortalRoute]);

  /* ------------------------------- Handlers ------------------------------- */

  const toggleMenu = () => {
    // Lock collapsed on org portal
    if (isOrgPortalRoute) return;

    setIsCollapsed((prev) => !prev);
  };

  const handleProjectClick = (selectedProject: ProjectResponseDto) => {
    setProject({
      projectId: selectedProject.id.toString(),
      projectName: selectedProject.name,
    });
    setActiveProject(selectedProject);
    router.push(`/project/${selectedProject.id}`);
  };

  const handleItemClick = (
    item: string,
    event: React.MouseEvent<HTMLElement>
  ) => {
    if (isDisabled(item)) {
      event.preventDefault();
      return;
    }

    event.preventDefault();
    setSelectedItem(item);
    router.push(item);
  };

  /* -------------------------- Derived / Helpers --------------------------- */

  const isDisabled = (targetPath: string) => {
    // Original home-page rule
    if (pathname === "/" && targetPath !== "/") return true;

    // On org portal, disable anything not explicitly allowed
    if (isOrgPortalRoute && !orgAllowedPaths.includes(targetPath)) return true;

    return false;
  };

  const getItemClass = (targetPath: string) => {
    const isExactMatch = selectedItem === targetPath;
    const isDynamicProject =
      targetPath === "/project/[id]" && /^\/project\/[^/]+$/.test(pathname);
    const isSelected = isExactMatch || isDynamicProject;

    return [
      "flex items-center block py-2 px-4 rounded transition",
      isSelected ? "bg-info/30" : "hover:bg-info/30",
      isDisabled(targetPath)
        ? "pointer-events-none opacity-50 cursor-not-allowed"
        : "",
    ].join(" ");
  };

  const isProjectActive = (projectId: string | number) => {
    const isOnProjectPage = pathname.includes(`/project/${projectId}`);
    const isSessionProject = project?.projectId === projectId.toString();
    return isOnProjectPage || isSessionProject;
  };

  /* --------------------------------- Main Render ---------------------------------- */

  return (
    <div className="fixed top-18 bottom-0 left-18 flex z-30">
      <aside
        className={`h-full shadow-xl ${
          isCollapsed ? "w-22" : "w-64"
        } bg-[var(--base-400)] brightness-120 text-primary-content p-4 transition-all duration-300 flex flex-col overflow-y-auto`}
      >
        {/* ----------------------------- Projects ---------------------------- */}
        {!isOrgPortalRoute && (
          <div className="mt-5">
            {/* Projects Header */}
            <div
              className="flex items-center justify-between py-2 px-4 cursor-pointer hover:bg-info/20 rounded transition"
              onClick={() => setIsProjectsExpanded(!isProjectsExpanded)}
            >
              <div className="flex items-center min-w-0 flex-1">
                <FolderIcon className="size-6 flex-shrink-0" />
                {!isCollapsed && (
                  <div className="flex flex-col p-4 min-w-0">
                    <span className="text-xs opacity-70">
                      {t.translations.PROJECTS}
                    </span>
                    <h1 className="text-lg font-bold truncate">
                      {activeProject?.name || t.translations.NO_PROJECT}
                    </h1>
                  </div>
                )}
              </div>
              {!isCollapsed && (
                <button className="btn btn-ghost btn-xs btn-circle flex-shrink-0">
                  {isProjectsExpanded ? (
                    <ChevronUpIcon className="size-4" />
                  ) : (
                    <ChevronDownIcon className="size-4" />
                  )}
                </button>
              )}
            </div>

            {/* Projects List */}
            {!isCollapsed && isProjectsExpanded && (
              <ul className="mt-2 space-y-1 max-h-64 overflow-y-auto bg-[var(--base-400)] border border-white/10 rounded-lg ">
                {loadingProjects ? (
                  <li className="py-2 px-4 text-sm text-primary-content/70">
                    <span className="loading loading-spinner loading-sm"></span>
                    <span className="ml-2">{t.translations.LOADING}</span>
                  </li>
                ) : projects.length === 0 ? (
                  <li className="py-2 px-4 text-sm text-base-content/70">
                    {t.translations.NO_PROJECT_FOUND}
                  </li>
                ) : (
                  projects.map((proj) => (
                    <li key={proj.id}>
                      <button
                        onClick={() => handleProjectClick(proj)}
                        className={`w-full text-left py-2 px-4 rounded transition text-sm flex items-center ${
                          isProjectActive(proj.id)
                            ? "bg-info/30 text-primary-content font-semibold"
                            : "hover:bg-info/20 text-primary-content"
                        }`}
                      >
                        <span className="truncate">{proj.name}</span>
                        {isProjectActive(proj.id) && (
                          <span className="ml-auto badge badge-xs flex-shrink-0">
                            {t.translations.ACTIVE}
                          </span>
                        )}
                      </button>
                    </li>
                  ))
                )}
              </ul>
            )}
          </div>
        )}

        {/* ------------------------------ Menu ------------------------------- */}
        <ul className="mt-8">
          {/* Project Dashboard */}
          <li>
            <Link
              href={`/project/${project?.projectId}`}
              prefetch={false}
              onClick={(e) =>
                handleItemClick(`/project/${project?.projectId}`, e)
              }
              className={getItemClass(`/project/${project?.projectId}`)}
            >
              <RectangleGroupIcon className="size-6" />
              {!isCollapsed && (
                <p className="ml-2">{t.translations.PROJECT_DASHBOARD}</p>
              )}
            </Link>
          </li>

          {/* Upload Center */}
          <li className="mt-2">
            <Link
              href="/upload_center"
              className={getItemClass("/upload_center")}
            >
              <ArrowUpTrayIcon className="size-6" />
              {!isCollapsed && (
                <p className="ml-2">{t.translations.UPLOAD_CENTER}</p>
              )}
            </Link>
          </li>

          {/* Event Management */}
          <li className="mt-2">
            <Link
              href="/event_management"
              onClick={(e) => handleItemClick("/event_management", e)}
              className={getItemClass("/event_management")}
            >
              <BellIcon className="size-6" />
              {!isCollapsed && (
                <p className="ml-2">{t.translations.EVENT_MANAGEMENT}</p>
              )}
            </Link>
          </li>

          {/* Project Settings (Admin only) */}
          <ProjectAdminRoute>
            <li className="mt-2">
              <Link
                href={`/project_management/${project?.projectId || ""}`}
                onClick={(e) =>
                  handleItemClick(
                    `/project_management/${project?.projectId || ""}`,
                    e
                  )
                }
                className={getItemClass(
                  `/project_management/${project?.projectId || ""}`
                )}
              >
                <AdjustmentsHorizontalIcon className="size-6" />
                {!isCollapsed && (
                  <p className="ml-2">{t.translations.PROJECT_SETINGS}</p>
                )}
              </Link>
            </li>
          </ProjectAdminRoute>
        </ul>
      </aside>

      {/* ---------------------------- Toggle Tab ----------------------------- */}
      <div
        className="h-8 w-4 bg-base-300 brightness-120 text-primary-content flex items-center justify-center cursor-pointer rounded-r-md mt-16"
        onClick={toggleMenu}
      >
        {isCollapsed ? (
          <ChevronRightIcon className="size-6" />
        ) : (
          <ChevronLeftIcon className="size-6" />
        )}
      </div>
    </div>
  );
};

export default SideMenu;
