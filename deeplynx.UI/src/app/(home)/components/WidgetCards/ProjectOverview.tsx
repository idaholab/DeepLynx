import { useLanguage } from "@/app/contexts/Language";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import { useProjectSession } from "@/app/contexts/ProjectSessionProvider";
import { getProjectStats } from "@/app/lib/client_service/projects_services.client";
import {
  ArrowsRightLeftIcon,
  CircleStackIcon,
  LinkIcon,
  RectangleGroupIcon,
} from "@heroicons/react/24/outline";
import { useEffect, useState } from "react";

const ProjectOverviewWidget = () => {
  const { t } = useLanguage();
  const { project, hasLoaded } = useProjectSession();
  const { organization, setOrganization } = useOrganizationSession();
  const [stats, setStats] = useState<{
    classes: number;
    records: number;
    connections: number;
    linkedSources?: number;
  } | null>(null);

  useEffect(() => {
    const fetchStats = async () => {
      try {
        const data = await getProjectStats(
          organization?.organizationId as number,
          project?.projectId as number
        );
        setStats({
          classes: data.classes,
          records: data.records,
          connections: data.datasources,
        });
      } catch (error) {
        console.error("Failed to fetch project stats:", error);
      }
    };
    if (project?.projectId && organization?.organizationId) fetchStats();
  }, [project?.projectId, organization?.organizationId]);

  return (
    <div className="card-body">
      <h2 className="card-title">{t.translations.PROJECT_OVERVIEW}</h2>
      {/* Option 1: Use grid instead of stats (no divider by default) */}
      <div className="grid grid-cols-2 gap-4 p-4 rounded-lg">
        <div>
          <div className="text-base-content opacity-70 text-sm">
            {t.translations.LINKED_SOURCES}
          </div>
          <div className="text-secondary flex items-center text-3xl font-bold mt-1">
            <LinkIcon className="size-8 mr-2" />
            <div className="text-base-content">
              {stats?.linkedSources ? stats.linkedSources : 0}
            </div>
          </div>
        </div>
        <div>
          <div className="text-base-content opacity-70 text-sm">
            {t.translations.DATA_RECORD}
          </div>
          <div className="text-secondary flex items-center text-3xl font-bold mt-1">
            <CircleStackIcon className="size-8 mr-2" />
            <div className="text-base-content">
              {stats?.records ? stats.records : 0}
            </div>
          </div>
        </div>
      </div>
      <div className="grid grid-cols-2 gap-4 p-4 rounded-lg mt-2">
        <div>
          <div className="text-base-content opacity-70 text-sm">
            {t.translations.CLASSES}
          </div>
          <div className="text-secondary flex items-center text-3xl font-bold mt-1">
            <RectangleGroupIcon className="size-8 mr-2" />
            <div className="text-base-content">
              {stats?.classes ? stats.classes : 0}
            </div>
          </div>
        </div>
        <div>
          <div className="text-base-content opacity-70 text-sm">
            {t.translations.CONNECTIONS}
          </div>
          <div className="text-secondary flex items-center text-3xl font-bold mt-1">
            <ArrowsRightLeftIcon className="size-8 mr-2" />
            <div className="text-base-content">
              {stats?.connections ? stats.connections : 0}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default ProjectOverviewWidget;
