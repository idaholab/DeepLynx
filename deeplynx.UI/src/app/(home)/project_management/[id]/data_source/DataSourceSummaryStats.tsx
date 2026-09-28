// src/app/(home)/project_management/data_source/DataSourceSummaryStats.tsx

import {
  ChartBarIcon,
  CircleStackIcon,
  KeyIcon,
  ShieldCheckIcon,
} from "@heroicons/react/24/outline";
import type {
  DataSourceResponseDto,
  ProjectStatResponseDto,
} from "@/app/(home)/types/responseDTOs";
import { formatRecordCount } from "./DataSourcesClient";
import { useLanguage } from "@/app/contexts/Language";

type SummaryProps = {
  loading: boolean;
  stats: ProjectStatResponseDto | null;
  sources: DataSourceResponseDto[];
  userKeys: string[] | null;
  avgHealth: number;
};

const DataSourceSummaryStats = ({
  loading,
  stats,
  sources,
  userKeys,
  avgHealth,
}: SummaryProps) => {
  const { t } = useLanguage();
  return (
    <div className="grid grid-cols-1 md:grid-cols-4 gap-4 mb-6">
      <div className="stat bg-base-200 rounded-lg">
        <div className="stat-figure">
          <CircleStackIcon className="w-8 h-8" />
        </div>
        <div className="stat-title">{t.translations.DATA_SOURCES}</div>
        <div className="stat-value text-2xl">
          {loading ? "…" : stats?.datasources ?? sources.length}
        </div>
        <div className="stat-desc">
          {sources.filter((s) => !s.isArchived).length} {t.translations.ACTIVE}
        </div>
      </div>

      <div className="stat bg-base-200 rounded-lg">
        <div className="stat-figure">
          <ChartBarIcon className="w-8 h-8" />
        </div>
        <div className="stat-title">{t.translations.TOTAL_RECORDS}</div>
        <div className="stat-value text-2xl">
          {loading ? "…" : formatRecordCount(stats?.records)}
        </div>
        <div className="stat-desc">
          {t.translations.ACROSS_ALL_SOURCES}
        </div>
      </div>

      <div className="stat bg-base-200 rounded-lg">
        <div className="stat-figure">
          <KeyIcon className="w-8 h-8" />
        </div>
        <div className="stat-title">{t.translations.API_KEYPAIRS}</div>
        <div className="stat-value text-2xl">
          {loading ? "…" : userKeys?.length ?? 0}
        </div>
        <div className="stat-desc">{t.translations.FOR_CURRENT_USER}</div>
      </div>

      <div className="stat bg-base-200 rounded-lg">
        <div className="stat-figure">
          <ShieldCheckIcon className="w-8 h-8" />
        </div>
        <div className="stat-title">{t.translations.AVG_HEALTH}</div>
        <div className="stat-value text-2xl">{avgHealth}%</div>
        <div className="stat-desc">{t.translations.SYSTEM_HEALTH}</div>
      </div>
    </div>
  );
};

export default DataSourceSummaryStats;
