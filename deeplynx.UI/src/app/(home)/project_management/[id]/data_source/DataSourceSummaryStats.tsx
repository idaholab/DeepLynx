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
  return (
    <div className="grid grid-cols-1 md:grid-cols-4 gap-4 mb-6">
      <div className="stat bg-base-200 rounded-lg">
        <div className="stat-figure">
          <CircleStackIcon className="w-8 h-8" />
        </div>
        <div className="stat-title">Data Sources</div>
        <div className="stat-value text-2xl">
          {loading ? "…" : stats?.datasources ?? sources.length}
        </div>
        <div className="stat-desc">
          {sources.filter((s) => !s.isArchived).length} active
        </div>
      </div>

      <div className="stat bg-base-200 rounded-lg">
        <div className="stat-figure">
          <ChartBarIcon className="w-8 h-8" />
        </div>
        <div className="stat-title">Total Records</div>
        <div className="stat-value text-2xl">
          {loading ? "…" : formatRecordCount(stats?.records)}
        </div>
        <div className="stat-desc">
          Across all sources
        </div>
      </div>

      <div className="stat bg-base-200 rounded-lg">
        <div className="stat-figure">
          <KeyIcon className="w-8 h-8" />
        </div>
        <div className="stat-title">API Keys</div>
        <div className="stat-value text-2xl">
          {loading ? "…" : userKeys?.length ?? 0}
        </div>
        <div className="stat-desc">For current user</div>
      </div>

      <div className="stat bg-base-200 rounded-lg">
        <div className="stat-figure">
          <ShieldCheckIcon className="w-8 h-8" />
        </div>
        <div className="stat-title">Avg Health</div>
        <div className="stat-value text-2xl">{avgHealth}%</div>
        <div className="stat-desc">System health</div>
      </div>
    </div>
  );
};

export default DataSourceSummaryStats;
