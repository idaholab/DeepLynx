// app/(home)/HomeDashboardClient.tsx
"use client";

import CreateWidget from "@/app/(home)/components/CreateWidgetsModal";
import { ExpandableTable } from "@/app/(home)/components/ExpandableTable";
import ExpandedProjectCard from "@/app/(home)/components/ExpandedProjectCard";
import { WidgetType } from "./types/types";
import { PlusIcon, QuestionMarkCircleIcon } from "@heroicons/react/24/outline";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState, useEffect, useCallback, useRef } from "react";
import { useLanguage } from "../contexts/Language";
import CreateProject from "./components/CreateProjectsModal";
import SearchInput from "./components/SearchInput";
import { format } from "date-fns";
import AddRecordModal from "./components/AddRecordModal";
import { useDashboardTour } from "./tours/useDashboardTour";
import { ProjectResponseDto } from "./types/responseDTOs";
import { useOrganizationSession } from "../contexts/OrganizationSessionProvider";
import { useRBAC } from "./rbac/useRBAC";
import { useSafeSession } from "../hooks/useSafeSession";
import { useProjectSession } from "../contexts/ProjectSessionProvider";
import { getAllProjects } from "../lib/client_service/projects_services.client";

type Props = { initialProjects: ProjectResponseDto[] };

export default function HomeDashboardClient({ initialProjects }: Props) {
  const { t } = useLanguage();
  const router = useRouter();

  const isAuthDisabled =
    process.env.NEXT_PUBLIC_DISABLE_FRONTEND_AUTHENTICATION === "true";

  const { data: session } = useSafeSession();
  const { user } = useRBAC();
  const { organization, hasLoaded } = useOrganizationSession();
  const { setProject } = useProjectSession();
  const [isProjectModalOpen, setIsProjectModalOpen] = useState(false);
  const [isRecordModalOpen, setIsRecordModalOpen] = useState(false);
  const [widgetModal, setWidgetModal] = useState(false);
  const [projects, setProjects] =
    useState<ProjectResponseDto[]>(initialProjects);
  const [searchTerm, setSearchTerm] = useState("");

  const isRefreshing = useRef(false);

  const refreshProjects = useCallback(async () => {
    if (!organization || isRefreshing.current) return;

    isRefreshing.current = true;
    try {
      const data = await getAllProjects(
        organization.organizationId as number,
        true
      );
      setProjects(data);
    } catch (err) {
      console.error("Failed to refresh projects:", err);
    } finally {
      isRefreshing.current = false;
    }
  }, [organization]);

  useEffect(() => {
    if (organization && hasLoaded) {
      refreshProjects();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [organization?.organizationId, hasLoaded, refreshProjects]);

  const filteredProjects = projects
    .filter((project) => {
      const term = searchTerm.toLowerCase();
      return (
        project.name.toLowerCase().includes(term) ||
        project.description?.toLowerCase().includes(term)
      );
    })
    .sort((a, b) => {
      const dateA = new Date(a.lastUpdatedAt!).getTime();
      const dateB = new Date(b.lastUpdatedAt!).getTime();
      return dateB - dateA;
    });

  const { startTour } = useDashboardTour({
    filteredProjects,
    initialProjects,
  });

  const onExplore = (row: ProjectResponseDto) => {
    setProject({
      projectId: row.id.toString(),
      projectName: row.name,
    });
    router.push(`/project/${row.id}`);
  };

  const columns = [
    {
      header: t.translations.PROJECT_NAME,
      data: (row: ProjectResponseDto) => (
        <Link
          href={`/project/${row.id}`}
          onClick={(e) => {
            e.preventDefault();
            setProject({
              projectId: row.id.toString(),
              projectName: row.name,
            });
            router.push(`/project/${row.id}`);
          }}
          className="font-bold text-secondary hover:text-base-content/80 underline underline-offset-2 transition-colors"
        >
          {row.name}
        </Link>
      ),
    },
    {
      header: t.translations.DESCRIPTION,
      data: (row: ProjectResponseDto) => (
        <span className="text-base-content/80">{row.description || "—"}</span>
      ),
    },
    {
      header: t.translations.LAST_UPDATED_AT,
      data: (row: ProjectResponseDto) => (
        <span className="text-base-content/60 text-sm">
          {format(new Date(row.lastUpdatedAt!), "MM/dd/yyyy hh:mm:s")}
        </span>
      ),
    },
  ];


  const formatUserName = (fullName?: string | null): string => {
    if (!fullName) return "";

    const parts = fullName.trim().split(/\s+/);
    const firstName = parts[0] ?? "";
    const lastName = parts[parts.length - 1] ?? "";
    return [firstName, lastName].filter(Boolean).join(" ");
  };

  const displayName = isAuthDisabled
    ? user?.name || ""
    : session?.user?.name || "";

  if (!hasLoaded) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-base-100">
        <span className="loading loading-spinner loading-lg"></span>
      </div>
    );
  }

  if (!organization) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-base-100">
        <span className="loading loading-spinner loading-lg"></span>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-base-100">
      <header className="bg-base-200/50 border-b border-base-300/30 sticky z-10 backdrop-blur-sm">
        <div className="flex justify-between items-center px-4 sm:px-6 lg:px-12 py-4">
          <div className="flex items-center gap-3">
            <h1 className="text-2xl font-bold text-base-content">
              {`${t.translations.WELECOME}, ${formatUserName(displayName)}`}
            </h1>
            <button
              onClick={startTour}
              className="btn btn-ghost btn-sm btn-circle"
              title="Start Tour"
            >
              <QuestionMarkCircleIcon className="w-5 h-5" />
            </button>
          </div>
          <div data-tour="search-input">
            <SearchInput
              placeholder="Search Projects"
              onChange={(e) => setSearchTerm(e.target.value)}
            />
          </div>
        </div>
      </header>

      <div className="p-6">
        <div className="w-4/5 mx-auto">
          <div
            className="card card-border shadow-md shadow-dynamic-shadow p-4"
            data-tour="projects-section"
          >
            <div className="flex justify-between items-center mb-4">
              <h3 className="text-info-content text-lg font-semibold">
                {t.translations.YOUR_PROJECTS}
              </h3>

              <div className="flex gap-2 w-full sm:w-auto">
                <button
                  onClick={() => setIsRecordModalOpen(true)}
                  className="btn btn-outline btn-secondary btn-sm flex-1 sm:flex-initial"
                  data-tour="add-record"
                >
                  <PlusIcon className="size-5" />
                  <span>{t.translations.RECORD}</span>
                </button>
                <button
                  onClick={() => setIsProjectModalOpen(true)}
                  className="btn btn-secondary btn-sm flex-1 sm:flex-initial"
                  data-tour="create-project"
                >
                  <PlusIcon className="size-5" />
                  <span>{t.translations.PROJECT}</span>
                </button>
              </div>
            </div>

            <ExpandableTable
              data={filteredProjects}
              columns={columns}
              renderExpandedContent={(project, onClose) => (
                <ExpandedProjectCard project={project} onClose={onClose} />
              )}
              onExplore={onExplore}
              getRowId={(p) => p.id}
            />
          </div>
        </div>
      </div>

      <AddRecordModal
        isOpen={isRecordModalOpen}
        onClose={() => setIsRecordModalOpen(false)}
        initialProjects={projects}
      />
      <CreateProject
        isOpen={isProjectModalOpen}
        onClose={() => setIsProjectModalOpen(false)}
        onProjectCreated={refreshProjects}
      />
      <CreateWidget
        isOpen={widgetModal}
        onClose={() => setWidgetModal(false)}
      />
    </div>
  );
}
