import { useLanguage } from "@/app/contexts/Language";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import { useProjectSession } from "@/app/contexts/ProjectSessionProvider";
import { getProjectStats } from "@/app/lib/client_service/projects_services.client";
import {
  CircleStackIcon,
  FolderIcon,
  RectangleGroupIcon,
} from "@heroicons/react/24/outline";
import { useEffect, useState } from "react";

const ProjectOverviewWidget = () => {
  const { t } = useLanguage();
  const { project } = useProjectSession();
  const { organization } = useOrganizationSession();
  const [stats, setStats] = useState<{
    classes: number;
    records: number;
    dataSources: number;
  } | null>(null);

  useEffect(() => {
    const fetchStats = async () => {
      try {
        const data = await getProjectStats(
          organization?.organizationId as number,
          project?.projectId as number,
        );
        setStats({
          classes: data.classes,
          records: data.records,
          dataSources: data.datasources,
        });
      } catch (error) {
        console.error("Failed to fetch project stats:", error);
      }
    };
    if (project?.projectId && organization?.organizationId) fetchStats();
  }, [project?.projectId, organization?.organizationId]);

  return (
    <div className="card-body">
      <div className="flex justify-between">
        <h2 className="card-title">{t.translations.PROJECT_OVERVIEW}</h2>
        {/* Peter wanted this button. DL-1564.  */}
        <button className="btn btn-outline btn-secondary btn-disabled">
          Explore
        </button>
      </div>

      {/* Show only the project stats that are actually provided by the API. */}
      <div className="grid grid-cols-1 gap-4 p-4 rounded-lg sm:grid-cols-3">
        <div>
          <div className="text-base-content opacity-70 text-sm">
            {t.translations.CLASSES}
          </div>
          <div className="text-secondary flex items-center text-3xl font-bold mt-1">
            <RectangleGroupIcon className="size-8 mr-2" />
            <div className="text-base-content">{stats?.classes ?? 0}</div>
          </div>
        </div>

        <div>
          <div className="text-base-content opacity-70 text-sm">
            {t.translations.DATA_RECORD}
          </div>
          <div className="text-secondary flex items-center text-3xl font-bold mt-1">
            <CircleStackIcon className="size-8 mr-2" />
            <div className="text-base-content">{stats?.records ?? 0}</div>
          </div>
        </div>

        <div>
          <div className="text-base-content opacity-70 text-sm">
            {t.translations.DATA_SOURCES}
          </div>
          <div className="text-secondary flex items-center text-3xl font-bold mt-1">
            <FolderIcon className="size-8 mr-2" />
            <div className="text-base-content">{stats?.dataSources ?? 0}</div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default ProjectOverviewWidget;
