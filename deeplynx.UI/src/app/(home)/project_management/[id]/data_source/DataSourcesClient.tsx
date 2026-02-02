// src/app/(home)/project_management/[id]/data_source/DataSourcesClient.tsx
"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { ArrowPathIcon, KeyIcon, PlusIcon } from "@heroicons/react/24/outline";

import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";

import type {
  DataSourceResponseDto,
  ProjectStatResponseDto,
} from "@/app/(home)/types/responseDTOs";
import type { CreateDataSourceRequestDto } from "@/app/(home)/types/requestDTOs";

import {
  archiveDataSource,
  createDataSource,
  getAllDataSources,
  getRecordCountForDataSource,
  setDefaultDataSource,
} from "@/app/lib/client_service/data_source_services.client";
import { getProjectStats } from "@/app/lib/client_service/projects_services.client";
import { getUserApiKeys } from "@/app/lib/client_service/token_services.client";

import DataSourceCreateForm from "./DataSourceCreateForm";
import DataSourceHeader from "./DataSourceHeader";
import DataSourceList from "./DataSourceList";
import DataSourceSummaryStats from "./DataSourceSummaryStats";

/* -------------------------------------------------------------------------- */
/*                                   Types                                    */
/* -------------------------------------------------------------------------- */

type Props = {
  projectId: number;
};

// Define API Key type
export interface APIKeyData {
  key: string;
  created?: string;
  expires?: string;
  expiresIn?: string;
  lastUsed?: string;
  requests?: string | number;
  permissions?: string[];
}

// Extend DataSourceResponseDto with optional fields we use in the UI
export interface ExtendedDataSource extends DataSourceResponseDto {
  icon?: string;
  health?: number;
  lastSync?: string;
  apiKey?: APIKeyData;
}

/* -------------------------------------------------------------------------- */
/*                            Helper / Utility Fns                            */
/* -------------------------------------------------------------------------- */

/**
 * Compute a heuristic health score (0–100) for a given data source.
 * Factors:
 * - Archived → 0
 * - Last sync freshness (age of `lastSync`)
 * - API key expiration (`apiKey.expiresIn`)
 */
export function computeHealth(ds: ExtendedDataSource): number {
  if (ds.isArchived) return 0;

  let score = 100;

  // Freshness (lastSync)
  if (!ds.lastSync) {
    score -= 30;
  } else {
    const last = new Date(ds.lastSync).getTime();
    const now = Date.now();
    const ageHours = (now - last) / (1000 * 60 * 60);

    if (ageHours > 168) score -= 50; // >7 days
    else if (ageHours > 24) score -= 30; // >1 day
    else if (ageHours > 6) score -= 10; // >6 hours
  }

  // API key expiration status
  if (ds.apiKey?.expiresIn === "Expired") {
    score -= 40;
  }

  return Math.max(0, Math.min(100, score));
}

/**
 * Format record counts into a friendly display:
 * - < 2,000 → "1,234"
 * - ≥ 2,000 → "1.2K"
 */
export const formatRecordCount = (n: number | null | undefined) => {
  if (n == null || Number.isNaN(Number(n))) return "—";
  const value = Number(n);
  return value < 2000
    ? value.toLocaleString()
    : `${(value / 1000).toFixed(1)}K`;
};

/**
 * Mask an API key, preserving a small prefix and suffix.
 */
export const maskKey = (key: string) =>
  key.substring(0, 15) + "•".repeat(25) + key.substring(key.length - 6);

/* -------------------------------------------------------------------------- */
/*                              Main Component                                */
/* -------------------------------------------------------------------------- */

const DataSources = ({ projectId }: Props) => {
  const { organization } = useOrganizationSession();

  /* --------------------------------- State -------------------------------- */

  const [sources, setSources] = useState<DataSourceResponseDto[]>([]);
  const [defaultSourceId, setDefaultSourceId] = useState<number | null>(null);
  const [hideArchived, setHideArchived] = useState(true);

  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [stats, setStats] = useState<ProjectStatResponseDto | null>(null);
  const [userKeys, setUserKeys] = useState<string[] | null>(null);

  const [showKey, setShowKey] = useState<Record<string, boolean>>({});
  const [copiedKey, setCopiedKey] = useState<string | null>(null);
  const [expandedSource, setExpandedSource] = useState<number | null>(null);
  const [showCreate, setShowCreate] = useState(false);

  const [recordCounts, setRecordCounts] = useState<
    Record<number, number | null>
  >({});

  const [createForm, setCreateForm] = useState<CreateDataSourceRequestDto>({
    name: "",
    description: "",
    abbreviation: "",
    type: "",
    baseUri: "",
    config: {},
    default: false,
  });

  /* ------------------------------------------------------------------------ */
  /*                        Data Fetching / Side Effects                      */
  /* ------------------------------------------------------------------------ */

  const fetchRecordCounts = useCallback(
    async (dataSourceList: DataSourceResponseDto[]) => {
      const orgId = organization?.organizationId;
      if (!orgId) return;

      try {
        const entries = await Promise.all(
          dataSourceList
            .filter((ds) => typeof ds.id === "number")
            .map(async (ds) => {
              const idNum = ds.id as number;
              const count = await getRecordCountForDataSource(
                orgId as number,
                projectId,
                idNum,
                true
              );
              return [idNum, count] as [number, number | null];
            })
        );

        setRecordCounts(Object.fromEntries(entries));
      } catch (err) {
        console.error("Failed to fetch record counts per data source:", err);
      }
    },
    [organization?.organizationId, projectId]
  );

  const fetchAll = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      const [dataSourceList, projectStats, keys] = await Promise.all([
        getAllDataSources(projectId, hideArchived),
        getProjectStats(
          organization?.organizationId as number,
          projectId
        ).catch((err) => {
          console.warn("⚠️ getProjectStats failed, defaulting to null:", err);
          return null;
        }),
        getUserApiKeys().catch((err) => {
          console.warn("⚠️ getUserApiKeys failed, defaulting to []:", err);
          return [] as string[];
        }),
      ]);

      setStats(projectStats ?? null);

      const list = dataSourceList ?? [];
      setSources(list);
      setUserKeys(keys ?? null);

      const defaultFromList = list.find((ds) => ds.default === true);
      setDefaultSourceId(
        defaultFromList && typeof defaultFromList.id === "number"
          ? defaultFromList.id
          : null
      );

      await fetchRecordCounts(list);
    } catch (e) {
      const errorMessage =
        e instanceof Error ? e.message : "Failed to load data sources.";
      setError(errorMessage);
    } finally {
      setLoading(false);
    }
  }, [
    projectId,
    hideArchived,
    organization?.organizationId,
    fetchRecordCounts,
  ]);

  useEffect(() => {
    fetchAll();
  }, [fetchAll]);

  /* ------------------------------------------------------------------------ */
  /*                             Derived / Computed                           */
  /* ------------------------------------------------------------------------ */

  const sourcesWithHealth: ExtendedDataSource[] = useMemo(() => {
    const extendedSources = sources as ExtendedDataSource[];
    return extendedSources.map((s) => ({
      ...s,
      health: s.health ?? computeHealth(s),
    }));
  }, [sources]);

  const sortedSources = useMemo(() => {
    const defId = defaultSourceId ?? -1;

    return [...sourcesWithHealth].sort((a, b) => {
      const aIsDef = Number(a.id) === defId;
      const bIsDef = Number(b.id) === defId;

      if (aIsDef !== bIsDef) return aIsDef ? -1 : 1;
      if (a.isArchived !== b.isArchived) return a.isArchived ? 1 : -1;

      return (a.name ?? "").localeCompare(b.name ?? "");
    });
  }, [sourcesWithHealth, defaultSourceId]);

  const avgHealth = useMemo(() => {
    const vals = sourcesWithHealth
      .map((s) => Number(s.health))
      .filter((n) => !Number.isNaN(n));

    if (!vals.length) return 100;

    return Math.round(vals.reduce((a, b) => a + b, 0) / vals.length);
  }, [sourcesWithHealth]);

  /* ------------------------------------------------------------------------ */
  /*                           Event Handlers / Actions                       */
  /* ------------------------------------------------------------------------ */

  const handleCopyKey = (keyId: string, key: string) => {
    navigator.clipboard.writeText(key);
    setCopiedKey(keyId);
    setTimeout(() => setCopiedKey(null), 2000);
  };

  const toggleKeyVisibility = (keyId: string) => {
    setShowKey((prev) => ({ ...prev, [keyId]: !prev[keyId] }));
  };

  const toggleExpand = (sourceId: number) => {
    setExpandedSource((prev) => (prev === sourceId ? null : sourceId));
  };

  const onArchiveToggle = async (s: DataSourceResponseDto) => {
    setSaving(true);
    try {
      await archiveDataSource(projectId, Number(s.id), !s.isArchived);
      await fetchAll();
    } catch (e) {
      const errorMessage = e instanceof Error ? e.message : "Operation failed.";
      setError(errorMessage);
    } finally {
      setSaving(false);
    }
  };

  const onSetDefault = async (id: number) => {
    setSaving(true);
    try {
      await setDefaultDataSource(projectId, id);
      await fetchAll();
    } catch (e) {
      const errorMessage =
        e instanceof Error ? e.message : "Could not set default.";
      setError(errorMessage);
    } finally {
      setSaving(false);
    }
  };

  const onCreate = async () => {
    if (!createForm.name?.trim()) {
      setError("Name is required.");
      return;
    }

    setSaving(true);
    setError(null);

    try {
      await createDataSource(projectId, createForm);

      setShowCreate(false);
      setCreateForm({
        name: "",
        description: "",
        abbreviation: "",
        type: "",
        baseUri: "",
        config: {},
        default: false,
      });

      await fetchAll();
    } catch (e) {
      const errorMessage = e instanceof Error ? e.message : "Create failed.";
      setError(errorMessage);
    } finally {
      setSaving(false);
    }
  };

  /* ------------------------------------------------------------------------ */
  /*                                Render                                    */
  /* ------------------------------------------------------------------------ */

  return (
    <div className="p-6 mx-auto">
      <DataSourceHeader
        hideArchived={hideArchived}
        setHideArchived={setHideArchived}
      />

      {error && (
        <div className="alert alert-error mb-4">
          <span>{error}</span>
        </div>
      )}

      <DataSourceSummaryStats
        loading={loading}
        stats={stats}
        sources={sources}
        userKeys={userKeys}
        avgHealth={avgHealth}
      />

      {/* Quick Actions */}
      <div className="flex justify-end gap-2 mb-6">
        <button
          className="btn btn-ghost gap-2"
          onClick={fetchAll}
          disabled={loading || saving}
        >
          <ArrowPathIcon
            className={`w-5 h-5 ${loading ? "animate-spin" : ""}`}
          />
          Refresh
        </button>
        <button
          className="btn btn-primary gap-2"
          onClick={() => setShowCreate((s) => !s)}
          disabled={saving}
        >
          <PlusIcon className="w-5 h-5" />
          Add Data Source
        </button>
      </div>

      {showCreate && (
        <DataSourceCreateForm
          createForm={createForm}
          setCreateForm={setCreateForm}
          saving={saving}
          onCreate={onCreate}
          onCancel={() => setShowCreate(false)}
        />
      )}

      <DataSourceList
        loading={loading}
        sources={sortedSources}
        defaultSourceId={defaultSourceId}
        recordCounts={recordCounts}
        saving={saving}
        showKey={showKey}
        copiedKey={copiedKey}
        expandedSource={expandedSource}
        projectId={projectId}
        onArchiveToggle={onArchiveToggle}
        onSetDefault={onSetDefault}
        onToggleExpand={toggleExpand}
        onToggleKeyVisibility={toggleKeyVisibility}
        onCopyKey={handleCopyKey}
        setError={setError}
        onDetailsSaved={fetchAll}
      />
    </div>
  );
};

export default DataSources;
