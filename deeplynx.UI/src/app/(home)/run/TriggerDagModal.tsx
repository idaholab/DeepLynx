"use client";

import { useLanguage } from "@/app/contexts/Language";
import { triggerDagRun } from "@/app/lib/client_service/airflow_services.client";
import { TriggerDagRunRequestDto } from "../types/requestDTOs";
import { AirflowDagResponseDto } from "../types/responseDTOs";
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

  // Reset the form whenever a new DAG is opened.
  useEffect(() => {
    if (dag) {
      setRunId("");
      setLogicalDate("");
      setDataIntervalStart("");
      setDataIntervalEnd("");
      setRunAfter("");
      setConf("");
      setNote("");
      setConfError(null);
      setSubmitting(false);
    }
  }, [dag]);

  if (!dag) return null;

  const dagLabel = dag.dag_display_name || dag.dag_id;

  const handleSubmit = async () => {
    if (submitting) return;

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

    try {
      setSubmitting(true);
      const run = await triggerDagRun(dag.dag_id, dto);
      toast.success(
        `${t.translations.TRIGGERED} ${dagLabel}${
          run.dag_run_id ? ` (${run.dag_run_id})` : ""
        }`,
      );
      onClose();
    } catch {
      toast.error(`${t.translations.FAILED_TO_TRIGGER} ${dagLabel}.`);
    } finally {
      setSubmitting(false);
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
              className={`textarea w-full h-32 font-mono ${
                confError ? "textarea-error" : ""
              }`}
              value={conf}
              onChange={(e) => setConf(e.target.value)}
            />
            <p
              className={`mt-1 text-xs ${
                confError ? "text-error" : "text-base-content/60"
              }`}
            >
              {confError ?? t.translations.CONF_HELP}
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
              disabled={submitting}
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
