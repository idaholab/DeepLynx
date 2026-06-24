"use client";

import { useLanguage } from "@/app/contexts/Language";
import {
  getDagDetails,
  getDagRun,
  triggerDagRun,
} from "@/app/lib/client_service/airflow_services.client";
import { TriggerDagRunRequestDto } from "../types/requestDTOs";
import {
  AirflowDagResponseDto,
  AirflowDagRunResponseDto,
} from "../types/responseDTOs";
import { useEffect, useState } from "react";
import toast from "react-hot-toast";

interface TriggerDagModalProps {
  // The DAG to trigger. When null the modal is closed.
  dag: AirflowDagResponseDto | null;
  onClose: () => void;
}

// Convert a value from a <input type="datetime-local"> into an ISO string the
// backend can parse. Returns undefined for empty input.
function toIso(local: string): string | undefined {
  if (!local) return undefined;
  const date = new Date(local);
  return Number.isNaN(date.getTime()) ? undefined : date.toISOString();
}

const DAG_RUN_POLL_INTERVAL_MS = 2000;
const DAG_RUN_MAX_POLLS = 45;

function delay(ms: number) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function normalizeDagParams(
  params: Record<string, unknown>,
): Record<string, unknown> {
  return Object.fromEntries(
    Object.entries(params).map(([key, value]) => {
      if (
        value &&
        typeof value === "object" &&
        !Array.isArray(value) &&
        "value" in value
      ) {
        return [key, (value as { value: unknown }).value];
      }

      return [key, value];
    }),
  );
}

function formatDagParams(
  params?: Record<string, unknown> | null,
): string | null {
  if (!params || Object.keys(params).length === 0) return null;
  return JSON.stringify(normalizeDagParams(params), null, 2);
}

function getDagRunState(run: AirflowDagRunResponseDto) {
  return run.state?.trim().toLowerCase() ?? "";
}

function isTerminalDagRun(run: AirflowDagRunResponseDto) {
  const state = getDagRunState(run);
  return state === "success" || state === "failed";
}

async function waitForDagRunCompletion(
  dagId: string,
  dagRunId: string,
  initialRun: AirflowDagRunResponseDto,
): Promise<AirflowDagRunResponseDto> {
  let latestRun = initialRun;

  for (let poll = 0; poll < DAG_RUN_MAX_POLLS; poll += 1) {
    if (isTerminalDagRun(latestRun)) return latestRun;
    await delay(DAG_RUN_POLL_INTERVAL_MS);
    latestRun = await getDagRun(dagId, dagRunId);
  }

  return latestRun;
}

const TriggerDagModal = ({ dag, onClose }: TriggerDagModalProps) => {
  const { t } = useLanguage();
  const [runId, setRunId] = useState("");
  const [logicalDate, setLogicalDate] = useState("");
  const [dataIntervalStart, setDataIntervalStart] = useState("");
  const [dataIntervalEnd, setDataIntervalEnd] = useState("");
  const [runAfter, setRunAfter] = useState("");
  const [conf, setConf] = useState("");
  const [note, setNote] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [confError, setConfError] = useState<string | null>(null);
  const [dagDetailsLoading, setDagDetailsLoading] = useState(false);

  // Reset the form whenever a new DAG is opened.
  useEffect(() => {
    if (!dag) return;

    setRunId("");
    setLogicalDate("");
    setDataIntervalStart("");
    setDataIntervalEnd("");
    setRunAfter("");
    setConf("");
    setNote("");
    setConfError(null);
    setSubmitting(false);
    setDagDetailsLoading(true);

    let cancelled = false;
    getDagDetails(dag.dag_id)
      .then((details) => {
        if (cancelled) return;
        const defaultConf = formatDagParams(details.params);
        if (!defaultConf) return;
        setConf((current) => (current.trim() ? current : defaultConf));
      })
      .catch((error) => {
        console.warn(`Failed to load DAG details for '${dag.dag_id}':`, error);
      })
      .finally(() => {
        if (!cancelled) setDagDetailsLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [dag]);

  if (!dag) return null;

  const dagLabel = dag.dag_display_name || dag.dag_id;

  const handleSubmit = async () => {
    if (submitting || dagDetailsLoading) return;

    // Parse and validate the optional conf JSON.
    let parsedConf: Record<string, unknown> | undefined;
    if (conf.trim()) {
      try {
        const parsed = JSON.parse(conf);
        if (
          typeof parsed !== "object" ||
          parsed === null ||
          Array.isArray(parsed)
        ) {
          setConfError(t.translations.CONF_MUST_BE_OBJECT);
          return;
        }
        parsedConf = parsed as Record<string, unknown>;
      } catch {
        setConfError(t.translations.CONF_INVALID_JSON);
        return;
      }
    }
    setConfError(null);

    const dto: TriggerDagRunRequestDto = {
      dag_run_id: runId.trim() || undefined,
      logical_date: toIso(logicalDate),
      data_interval_start: toIso(dataIntervalStart),
      data_interval_end: toIso(dataIntervalEnd),
      run_after: toIso(runAfter),
      conf: parsedConf,
      note: note.trim() || undefined,
    };

    setSubmitting(true);
    const toastId = toast.loading(
      `${t.translations.DAG_RUN_SUBMITTED} ${dagLabel}...`,
    );

    let run: AirflowDagRunResponseDto;
    try {
      run = await triggerDagRun(dag.dag_id, dto);
    } catch {
      toast.error(`${t.translations.FAILED_TO_TRIGGER} ${dagLabel}.`, {
        id: toastId,
      });
      setSubmitting(false);
      return;
    }

    setSubmitting(false);
    onClose();

    if (!run.dag_run_id) {
      toast(
        `${t.translations.DAG_RUN_STATUS_UNAVAILABLE} ${dagLabel}.`,
        { id: toastId },
      );
      return;
    }

    try {
      const completedRun = await waitForDagRunCompletion(
        dag.dag_id,
        run.dag_run_id,
        run,
      );
      const state = getDagRunState(completedRun);
      const runSuffix = ` (${run.dag_run_id})`;

      if (state === "success") {
        toast.success(
          `${t.translations.DAG_RUN_SUCCEEDED} ${dagLabel}${runSuffix}`,
          { id: toastId },
        );
      } else if (state === "failed") {
        toast.error(
          `${t.translations.DAG_RUN_FAILED} ${dagLabel}${runSuffix}`,
          { id: toastId },
        );
      } else {
        toast(
          `${t.translations.DAG_RUN_STILL_RUNNING} ${dagLabel}${runSuffix}`,
          { id: toastId },
        );
      }
    } catch {
      toast.error(
        `${t.translations.FAILED_TO_CHECK_DAG_RUN_STATUS} ${dagLabel}.`,
        { id: toastId },
      );
    }
  };

  return (
    <dialog className="modal modal-open">
      <div className="modal-box max-w-2xl">
        <h3 className="text-lg font-bold">{t.translations.TRIGGER_DAG}</h3>
        <p className="mb-5 text-sm text-base-content/60">{dagLabel}</p>

        <form
          onSubmit={(e) => {
            e.preventDefault();
            handleSubmit();
          }}
        >
          <div className="mb-4">
            <label className="mb-1 block text-sm font-medium">
              {t.translations.RUN_ID}
            </label>
            <input
              type="text"
              placeholder={t.translations.RUN_ID_PLACEHOLDER}
              className="input w-full"
              value={runId}
              onChange={(e) => setRunId(e.target.value)}
            />
          </div>

          <div className="mb-4">
            <label className="mb-1 block text-sm font-medium">
              {t.translations.CONFIGURATION_JSON}
            </label>
            <textarea
              placeholder={'{\n  "key": "value"\n}'}
              className={`textarea min-h-56 w-full font-mono ${
                confError ? "textarea-error" : ""
              }`}
              value={conf}
              onChange={(e) => setConf(e.target.value)}
            />
            <p
              className={`mt-1 flex items-center gap-2 text-xs ${
                confError ? "text-error" : "text-base-content/60"
              }`}
            >
              {confError ? (
                confError
              ) : dagDetailsLoading ? (
                <>
                  <span className="loading loading-spinner loading-xs" />
                  <span>{t.translations.LOADING_DAG_PARAMETERS}</span>
                </>
              ) : (
                t.translations.CONF_HELP
              )}
            </p>
          </div>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div>
              <label className="mb-1 block text-sm font-medium">
                {t.translations.LOGICAL_DATE}
              </label>
              <input
                type="datetime-local"
                className="input w-full"
                value={logicalDate}
                onChange={(e) => setLogicalDate(e.target.value)}
              />
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium">
                {t.translations.RUN_AFTER}
              </label>
              <input
                type="datetime-local"
                className="input w-full"
                value={runAfter}
                onChange={(e) => setRunAfter(e.target.value)}
              />
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium">
                {t.translations.DATA_INTERVAL_START}
              </label>
              <input
                type="datetime-local"
                className="input w-full"
                value={dataIntervalStart}
                onChange={(e) => setDataIntervalStart(e.target.value)}
              />
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium">
                {t.translations.DATA_INTERVAL_END}
              </label>
              <input
                type="datetime-local"
                className="input w-full"
                value={dataIntervalEnd}
                onChange={(e) => setDataIntervalEnd(e.target.value)}
              />
            </div>
          </div>

          <div className="mb-4 mt-4">
            <label className="mb-1 block text-sm font-medium">
              {t.translations.NOTE}
            </label>
            <textarea
              placeholder={t.translations.NOTE_PLACEHOLDER}
              className="textarea w-full"
              value={note}
              onChange={(e) => setNote(e.target.value)}
            />
          </div>

          <div className="modal-action">
            <button
              type="button"
              className="btn"
              onClick={onClose}
              disabled={submitting}
            >
              {t.translations.CANCEL}
            </button>
            <button
              type="submit"
              className="btn btn-primary"
              disabled={submitting || dagDetailsLoading}
            >
              {submitting && (
                <span className="loading loading-spinner loading-sm" />
              )}
              {t.translations.TRIGGER}
            </button>
          </div>
        </form>
      </div>
      <div className="modal-backdrop" onClick={onClose} />
    </dialog>
  );
};

export default TriggerDagModal;
