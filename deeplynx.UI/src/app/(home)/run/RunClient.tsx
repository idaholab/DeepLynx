"use client";

import { useLanguage } from "@/app/contexts/Language";
import { getAllDags } from "@/app/lib/client_service/airflow_services.client";
import { PlayIcon } from "@heroicons/react/24/outline";
import { useCallback, useEffect, useState } from "react";
import { AirflowDagResponseDto } from "../types/responseDTOs";
import TriggerDagModal from "./TriggerDagModal";

export default function RunClient() {
  const { t } = useLanguage();
  const [dags, setDags] = useState<AirflowDagResponseDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [dagToTrigger, setDagToTrigger] = useState<AirflowDagResponseDto | null>(
    null,
  );

  const fetchDags = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await getAllDags();
      setDags(data.dags ?? []);
    } catch (e) {
      console.error("Failed to load DAGs:", e);
      setError(t.translations.FAILED_TO_LOAD_DAGS);
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    fetchDags();
  }, [fetchDags]);

  return (
    <main className="min-h-screen bg-base-200/30">
      {/* ── Page header ───────────────────────────────────────────────────── */}
      <section className="border-b border-base-300 bg-base-100">
        <div className="mx-auto flex w-full max-w-7xl flex-col gap-5 px-3 py-5 sm:px-6 lg:px-8">
          <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
            <div className="space-y-3">
              <div>
                <p className="text-xs font-semibold uppercase tracking-wide text-base-content/60">
                  {t.translations.RUN}
                </p>
                <h1 className="text-2xl font-bold text-base-content sm:text-3xl">
                  {t.translations.ALL_DAGS}
                </h1>
                <p className="mt-2 max-w-3xl text-sm leading-6 text-base-content/65">
                  {t.translations.DAGS_OVERVIEW_DESCRIPTION}
                </p>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* ── Main content ──────────────────────────────────────────────────── */}
      <section className="mx-auto flex w-full max-w-7xl flex-col gap-4 px-3 py-5 sm:px-6 lg:px-8">
        {loading ? (
          <div className="flex justify-center py-16">
            <span className="loading loading-spinner loading-lg" />
          </div>
        ) : error ? (
          <div className="flex flex-col items-center gap-4 py-16">
            <p className="text-error">{error}</p>
            <button className="btn btn-sm" onClick={fetchDags}>
              {t.translations.RETRY}
            </button>
          </div>
        ) : dags.length === 0 ? (
          <div className="py-16 text-center text-base-content/60">
            {t.translations.NO_DAGS_FOUND}
          </div>
        ) : (
          <div className="overflow-x-auto rounded-box border border-base-300 bg-base-100">
            <table className="table">
              <thead>
                <tr>
                  <th>{t.translations.DAG}</th>
                  <th>{t.translations.DESCRIPTION}</th>
                  <th>{t.translations.STATUS}</th>
                  <th>{t.translations.OWNERS}</th>
                  <th>{t.translations.SCHEDULE}</th>
                  <th>{t.translations.TAGS}</th>
                  <th className="text-right">{t.translations.ACTIONS}</th>
                </tr>
              </thead>
              <tbody>
                {dags.map((dag) => (
                  <tr key={dag.dag_id} className="hover">
                    <td className="font-medium">
                      {dag.dag_display_name || dag.dag_id}
                    </td>
                    <td className="max-w-xs truncate text-base-content/70">
                      {dag.description || "—"}
                    </td>
                    <td>
                      <span
                        className={`badge badge-sm ${
                          dag.is_paused ? "badge-ghost" : "badge-success"
                        }`}
                      >
                        {dag.is_paused
                          ? t.translations.PAUSED
                          : t.translations.ACTIVE}
                      </span>
                    </td>
                    <td className="text-base-content/70">
                      {dag.owners?.length ? dag.owners.join(", ") : "—"}
                    </td>
                    <td className="text-base-content/70">
                      {dag.timetable_description || "—"}
                    </td>
                    <td>
                      <div className="flex flex-wrap gap-1">
                        {dag.tags?.length
                          ? dag.tags.map((tag) => (
                              <span
                                key={tag.name}
                                className="badge badge-outline badge-sm"
                              >
                                {tag.name}
                              </span>
                            ))
                          : "—"}
                      </div>
                    </td>
                    <td className="text-right">
                      <span
                        className="tooltip tooltip-left inline-flex"
                        data-tip={
                          dag.is_paused
                            ? t.translations.DAG_PAUSED_TOOLTIP
                            : t.translations.TRIGGER_DAG_TOOLTIP
                        }
                      >
                        <button
                          type="button"
                          className="btn btn-ghost btn-sm btn-circle"
                          onClick={() => setDagToTrigger(dag)}
                          disabled={dag.is_paused}
                          aria-label={`${t.translations.TRIGGER} ${dag.dag_display_name || dag.dag_id}`}
                          title={
                            dag.is_paused
                              ? t.translations.DAG_PAUSED_TOOLTIP
                              : t.translations.TRIGGER_DAG_TOOLTIP
                          }
                        >
                          <PlayIcon className="size-5" />
                        </button>
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <TriggerDagModal
        dag={dagToTrigger}
        onClose={() => setDagToTrigger(null)}
      />
    </main>
  );
}
