// app/(home)/HomeDashboardClient.tsx
"use client";

import CreateWidget from "@/app/(home)/components/CreateWidgetsModal";
import { ExpandableTable } from "@/app/(home)/components/ExpandableTable";
import ExpandedProjectCard from "@/app/(home)/components/ExpandedProjectCard";
import OrganizationOverviewCard from "@/app/(home)/components/OrganizationOverviewCard";
import SavedSearchesWidget from "@/app/(home)/components/SavedSearchesWidget";
import { PlusIcon, QuestionMarkCircleIcon } from "@heroicons/react/24/outline";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState, useEffect, useCallback, useMemo, useRef } from "react";
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
import type { SortOption } from "./hooks/useSortedItems";

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
        true,
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

  const filteredProjects = projects.filter((project) => {
    const term = searchTerm.toLowerCase();
    return (
      project.name.toLowerCase().includes(term) ||
      project.description?.toLowerCase().includes(term)
    );
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

  const compareOptionalText = (
    left?: string | null,
    right?: string | null,
    direction: "asc" | "desc" = "asc",
  ) => {
    const a = left?.trim();
    const b = right?.trim();

    if (!a && !b) return 0;
    if (!a) return 1;
    if (!b) return -1;

    return direction === "asc"
      ? a.localeCompare(b, undefined, { sensitivity: "base" })
      : b.localeCompare(a, undefined, { sensitivity: "base" });
  };

  const toTime = (value?: string | Date | null) =>
    value ? new Date(value).getTime() : 0;

  const projectSortOptions = useMemo<SortOption<ProjectResponseDto>[]>(
    () => [
      {
        value: "nameAZ",
        label: t.translations.SORT_NAME_A_TO_Z,
        compare: (a, b) =>
          a.name.localeCompare(b.name, undefined, { sensitivity: "base" }),
      },
      {
        value: "nameZA",
        label: t.translations.SORT_NAME_Z_TO_A,
        compare: (a, b) =>
          b.name.localeCompare(a.name, undefined, { sensitivity: "base" }),
      },
      {
        value: "descriptionAZ",
        label: t.translations.SORT_DESCRIPTION_A_TO_Z,
        compare: (a, b) =>
          compareOptionalText(a.description, b.description, "asc"),
      },
      {
        value: "descriptionZA",
        label: t.translations.SORT_DESCRIPTION_Z_TO_A,
        compare: (a, b) =>
          compareOptionalText(a.description, b.description, "desc"),
      },
      {
        value: "dateNew",
        label: t.translations.SORT_DATE_NEWEST,
        compare: (a, b) => toTime(b.lastUpdatedAt) - toTime(a.lastUpdatedAt),
      },
      {
        value: "dateOld",
        label: t.translations.SORT_DATE_OLDEST,
        compare: (a, b) => toTime(a.lastUpdatedAt) - toTime(b.lastUpdatedAt),
      },
    ],
    [t],
  );

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
          {row.name.length > 50 ? row.name.slice(0, 50) + "..." : row.name}
        </Link>
      ),
    },
    {
      header: t.translations.DESCRIPTION,
      isExpandTrigger: (row: ProjectResponseDto) =>
        (row.description?.length ?? 0) > 200,
      data: (row: ProjectResponseDto) => {
        const isLong = (row.description?.length ?? 0) > 200;
        return (
          <span className="text-base-content/80">
            {isLong
              ? row.description!.slice(0, 80) + "..."
              : row.description || "—"}
          </span>
        );
      },
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
    <main className="min-h-screen bg-base-200/30">
      <section className="border-b border-base-300 bg-base-100">
        <div className="mx-auto flex w-full max-w-7xl flex-col gap-5 px-3 py-5 sm:px-6 lg:px-8">
          <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
            <div className="min-w-0">
              <p className="text-xs font-semibold uppercase tracking-wide text-base-content/60">
                {t.translations.YOUR_PROJECTS}
              </p>
              <div className="flex flex-wrap items-center gap-3">
                <h1 className="min-w-0 break-words text-2xl font-bold text-base-content sm:text-3xl">
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
            </div>

            <div data-tour="search-input" className="w-full lg:max-w-xl">
              <SearchInput
                placeholder="Search Projects"
                className="w-full"
                onChange={(e) => setSearchTerm(e.target.value)}
              />
            </div>
          </div>
        </div>
      </section>

      <section className="mx-auto w-full max-w-7xl px-3 py-5 sm:px-6 lg:px-8">
        <div className="">
          {/* Main content: projects table + saved searches side by side */}
          <div className="flex flex-col 2xl:flex-row gap-4 justify-center items-start">
            {/* Projects table */}
            <div
              className="max-w-5xl 2xl:flex-1 card card-border shadow-md shadow-base-content/10 p-4 overflow-auto"
              data-tour="projects-section"
            >
              <div className="flex flex-col sm:flex-row sm:justify-between sm:items-center gap-3 mb-4">
                <h3 className="text-base-content text-lg font-semibold">
                  {t.translations.YOUR_PROJECTS}
                </h3>

                <div className="flex flex-col sm:flex-row gap-2 w-full sm:w-auto">
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
                sortOptions={projectSortOptions}
                defaultSortValue="dateNew"
              />
            </div>

            <div className="w-full 2xl:w-[420px] 2xl:shrink-0 flex flex-col gap-4">
              <OrganizationOverviewCard />
              <div className="h-180">
                <SavedSearchesWidget scope="org" projects={projects} />
              </div>
            </div>
          </div>
        </div>
      </section>

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
    </main>
  );
}
