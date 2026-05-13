import { useLanguage } from "@/app/contexts/Language";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import { useProjectSession } from "@/app/contexts/ProjectSessionProvider";
import {
  getProjectDataModalityCount,
  getProjectDataSourceCount,
  getProjectStorageSize,
} from "@/app/lib/client_service/projects_services.client";
import {
  FolderIcon,
  ServerStackIcon,
  Squares2X2Icon,
} from "@heroicons/react/24/outline";
import { useEffect, useState } from "react";

const getMetricNumber = (value: unknown): number => {
  if (typeof value === "number") return Number.isFinite(value) ? value : 0;
  if (typeof value === "string") {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : 0;
  }
  if (value && typeof value === "object") {
    const record = value as Record<string, unknown>;
    return getMetricNumber(
      record.value ??
        record.count ??
        record.bytes ??
        record.byteSum ??
        record.size ??
        record.storageSize,
    );
  }

  return 0;
};

const formatBytes = (bytes: unknown): string => {
  const normalizedBytes = getMetricNumber(bytes);
  if (normalizedBytes <= 0) return "0 B";

  const k = 1024;
  const sizes = ["B", "KB", "MB", "GB", "TB"];
  const i = Math.min(
    Math.floor(Math.log(normalizedBytes) / Math.log(k)),
    sizes.length - 1,
  );
  const value = normalizedBytes / Math.pow(k, i);

  return `${parseFloat(value.toFixed(2))} ${sizes[i]}`;
};

const ProjectOverviewWidget = () => {
  const { t } = useLanguage();
  const { project } = useProjectSession();
  const { organization } = useOrganizationSession();
  const [stats, setStats] = useState<{
    dataSources: number;
    storageSize: number;
    dataModalities: number;
  } | null>(null);

  useEffect(() => {
    if (!project?.projectId || !organization?.organizationId) {
      setStats(null);
      return;
    }

    let isActive = true;

    const fetchStats = async () => {
      const organizationId = organization.organizationId as number;
      const projectId = project.projectId as number;

      const [dataSources, storageSize, dataModalities] =
        await Promise.allSettled([
          getProjectDataSourceCount(organizationId, projectId),
          getProjectStorageSize(organizationId, projectId),
          getProjectDataModalityCount(organizationId, projectId),
        ]);

      if (!isActive) return;

      setStats({
        dataSources:
          dataSources.status === "fulfilled"
            ? getMetricNumber(dataSources.value)
            : 0,
        storageSize:
          storageSize.status === "fulfilled"
            ? getMetricNumber(storageSize.value)
            : 0,
        dataModalities:
          dataModalities.status === "fulfilled"
            ? getMetricNumber(dataModalities.value)
            : 0,
      });

      if (dataSources.status === "rejected") {
        console.error(
          "Failed to fetch project data source count:",
          dataSources.reason,
        );
      }

      if (storageSize.status === "rejected") {
        console.error(
          "Failed to fetch project storage size:",
          storageSize.reason,
        );
      }

      if (dataModalities.status === "rejected") {
        console.error(
          "Failed to fetch project data modality count:",
          dataModalities.reason,
        );
      }
    };

    void fetchStats();

    return () => {
      isActive = false;
    };
  }, [project?.projectId, organization?.organizationId]);

  const metrics = [
    {
      title: t.translations.STORAGE_SIZE,
      value: formatBytes(stats?.storageSize ?? 0),
      Icon: ServerStackIcon,
    },
    {
      title: t.translations.DATA_SOURCES,
      value: stats?.dataSources ?? 0,
      Icon: FolderIcon,
    },
    {
      title: t.translations.DATA_MODALITIES,
      value: stats?.dataModalities ?? 0,
      Icon: Squares2X2Icon,
    },
  ];

  return (
    <div className="card-body">
      <div className="flex justify-between">
        <h2 className="card-title">{t.translations.PROJECT_OVERVIEW}</h2>
        {/* Peter wanted this button. DL-1564.  */}
        {/* <button className="btn btn-outline btn-secondary btn-disabled">
          Explore
        </button> */}
      </div>

      {/* Show project metrics provided by the stats and metrics APIs. */}
      <div className="grid grid-cols-1 gap-4 p-4 rounded-lg sm:grid-cols-3">
        {metrics.map(({ title, value, Icon }) => (
          <div key={title}>
            <div className="text-base-content opacity-70 text-sm">{title}</div>
            <div className="text-secondary flex items-center text-xl font-bold mt-1">
              <Icon className="size-7 mr-2 shrink-0" />
              <div className="text-base-content break-words">{value}</div>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
};

export default ProjectOverviewWidget;
