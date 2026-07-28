// src/app/(home)/project_management/data_source/DataSourceList.tsx

import { PlusIcon } from "@heroicons/react/24/outline";
import { ExtendedDataSource } from "./DataSourcesClient";
import DataSourceCard from "./DataSourceCard";

type ListProps = {
  loading: boolean;
  sources: ExtendedDataSource[];
  defaultSourceId: number | null;
  recordCounts: Record<number, number | null>;
  saving: boolean;
  showKey: Record<string, boolean>;
  copiedKey: string | null;
  expandedSource: number | null;
  projectId: number;
  onArchiveToggle: (s: ExtendedDataSource) => Promise<void>;
  onSetDefault: (id: number) => Promise<void>;
  onToggleExpand: (id: number) => void;
  onToggleKeyVisibility: (id: string) => void;
  onCopyKey: (id: string, key: string) => void;
  setError: (msg: string | null) => void;
  onDetailsSaved: () => Promise<void> | void;
};

const DataSourceList = ({
  loading,
  sources,
  defaultSourceId,
  recordCounts,
  saving,
  showKey,
  copiedKey,
  expandedSource,
  projectId,
  onArchiveToggle,
  onSetDefault,
  onToggleExpand,
  onToggleKeyVisibility,
  onCopyKey,
  setError,
  onDetailsSaved,
}: ListProps) => {
  if (loading) {
    return (
      <div className="flex items-center gap-3">
        <span className="loading loading-spinner loading-md" />
        <span>Loading data sources…</span>
      </div>
    );
  }

  if (sources.length === 0) {
    return (
      <div className="card border-2 border-dashed border-base-300/50 bg-base-100 shadow-sm">
        <div className="card-body items-center justify-center py-12">
          <PlusIcon className="w-16 h-16 text-base-content/30" />
          <h3 className="text-lg font-semibold text-base-content/60">
            No data sources yet
          </h3>
          <p className="text-sm text-base-content/40">
            Create your first data source to get started.
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {sources.map((source) => {
        const idNum = typeof source.id === "number" ? source.id : undefined;
        const recordCount = idNum !== undefined ? recordCounts[idNum] : null;
        const isDefault =
          idNum != null && defaultSourceId != null && idNum === defaultSourceId;

        return (
          <DataSourceCard
            key={source.id ?? `ds-${source.name}`}
            source={source}
            isDefault={isDefault}
            recordCount={recordCount}
            saving={saving}
            projectId={projectId}
            showKey={showKey}
            copiedKey={copiedKey}
            expanded={expandedSource === idNum}
            onToggleExpand={onToggleExpand}
            onArchiveToggle={onArchiveToggle}
            onSetDefault={onSetDefault}
            onToggleKeyVisibility={onToggleKeyVisibility}
            onCopyKey={onCopyKey}
            setError={setError}
            onDetailsSaved={onDetailsSaved}
          />
        );
      })}
    </div>
  );
};

export default DataSourceList;
