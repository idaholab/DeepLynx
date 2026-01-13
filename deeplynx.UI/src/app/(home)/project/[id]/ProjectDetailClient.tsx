// src/app/(home)/project/[id]/ProjectDetailClient.tsx
"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState, useCallback } from "react";
import { format } from "date-fns";

import SearchBar from "@/app/(home)/components/SearchBar";
import WidgetCard from "@/app/(home)/components/Widgets";
import { WidgetType } from "../../types/types";
import RecentRecordsCard from "../../components/RecentRecordsCard";
import { ProjectResponseDto } from "@/app/(home)/types/responseDTOs";
import { useLanguage } from "@/app/contexts/Language";
import { useProjectSession } from "@/app/contexts/ProjectSessionProvider";

type Props = {
  initialProject: ProjectResponseDto | null;
  projectId: string;
};

export default function ProjectDetailClient({
  initialProject,
  projectId,
}: Props) {
  const { t } = useLanguage();
  const router = useRouter();
  const {
    project: sessionProject,
    setProject: setProjectSession,
    hasLoaded,
  } = useProjectSession();

  // State
  const [project, setProject] = useState<ProjectResponseDto | null>(
    initialProject
  );
  const [canCustomize, setCanCustomize] = useState(false);
  const [projectWidgets, setProjectWidgets] = useState<WidgetType[]>([
    "RecentActivity",
    "ProjectOverview",
    "TeamMembers",
  ]);

  // Memoize the sync function to prevent it from changing on every render
  const syncProjectSession = useCallback(() => {
    if (initialProject) {
      setProject(initialProject);
      setProjectSession({
        projectId: initialProject.id.toString(),
        projectName: initialProject.name,
      });
    }
  }, [initialProject, setProjectSession]);

  // Sync project session with initial project on mount
  useEffect(() => {
    syncProjectSession();
  }, [syncProjectSession]);

  // Save widget configuration to localStorage
  const handleSave = (newWidgets: WidgetType[]) => {
    setProjectWidgets(newWidgets);
    localStorage.setItem(
      `projectWidgets-${projectId}`,
      JSON.stringify(newWidgets)
    );
  };

  // Navigate to data catalog with search term
  const handleSearchEnter = (searchTerm: string) => {
    const query = new URLSearchParams({
      fromProject: projectId,
      search: searchTerm,
    }).toString();

    router.push(`/data_catalog?${query}`);
  };

  // Loading states
  if (!hasLoaded) return <p className="p-4">{t.translations.LOADING}</p>;
  if (!project) return <p className="p-4">{t.translations.NO_PROJECT_FOUND}</p>;

  return (
    <div className="min-h-screen bg-base-100">
      {/* Project Header */}
      <div className="bg-base-200/50 border-b border-base-300/30 py-4 px-6 lg:px-12">
        <h1 className="text-2xl font-bold text-base-content">{project.name}</h1>
        <p className="mt-2 text-base-content/70">{project.description}</p>
        <p className="mt-2 text-sm text-base-content/60">
          <span className="font-semibold">{t.translations.CREATED}: </span>
          {project.lastUpdatedAt &&
            format(new Date(project.lastUpdatedAt), "MM/dd/yyyy")}
        </p>
      </div>

      {/* Main Content */}
      <div className="flex flex-col lg:flex-row gap-6 px-4 lg:px-6 mt-6">
        {/* Left Column */}
        <div
          className={`flex-1 lg:w-3/5 transition-opacity duration-300 ${canCustomize ? "opacity-50 pointer-events-none" : ""
            }`}
        >
          {/* Search Bar */}
          <div className="mb-6">
            <SearchBar className="w-full" onEnter={handleSearchEnter} />
          </div>

          {/* Data Catalog Card */}
          <div className="card bg-base-200/30 border border-base-300/50 shadow-sm mb-6">
            <div className="card-body">
              <div className="flex justify-between items-center mb-4">
                <h2 className="text-xl font-semibold text-base-content">
                  {t.translations.DATA_CATALOG_OVERVIEW}
                </h2>
                <Link
                  className="btn btn-secondary btn-sm"
                  href={{
                    pathname: "/data_catalog",
                    query: {
                      fromProject: projectId,
                    },
                  }}
                >
                  {t.translations.VISIT}
                </Link>
              </div>

              <RecentRecordsCard
                selectedProjects={[projectId]}
                border={false}
              />
            </div>
          </div>
        </div>

        {/* Right Column - Widgets */}
        <aside className="lg:w-2/5">
          <div className="sticky top-6">
            <WidgetCard
              widgets={projectWidgets}
              onSave={handleSave}
              onCustomizeChange={setCanCustomize}
            />
          </div>
        </aside>
      </div>
    </div>
  );
}
