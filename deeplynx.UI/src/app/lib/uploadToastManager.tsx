"use client";

import { useLanguage } from "@/app/contexts/Language";
import { ChevronDownIcon, ChevronUpIcon } from "@heroicons/react/24/outline";
import toast from "react-hot-toast";

const SUCCESS_DURATION_MS = 3000;
const ERROR_DURATION_MS = 5000;
const MESSAGE_DURATION_MS = 3000;

function formatBytes(bytes: number): string {
  if (!isFinite(bytes) || bytes < 0) return "0 B";
  if (bytes >= 1024 ** 3) return `${(bytes / 1024 ** 3).toFixed(2)} GB`;
  if (bytes >= 1024 ** 2) return `${(bytes / 1024 ** 2).toFixed(1)} MB`;
  if (bytes >= 1024) return `${(bytes / 1024).toFixed(0)} KB`;
  return `${Math.round(bytes)} B`;
}

function formatSpeed(bytesPerSec?: number): string | null {
  if (!bytesPerSec || !isFinite(bytesPerSec) || bytesPerSec <= 0) return null;
  return `${formatBytes(bytesPerSec)}/s`;
}

export type UploadToastState = {
  title: string;
  message: string;
  percent?: number;
  bytesUploaded?: number;
  totalBytes?: number;
  speedBytesPerSec?: number;
  isCancelling?: boolean;
  onCancel?: () => void;
  cancelDisabled?: boolean;
};

export type UploadToastManager = {
  show: (state: UploadToastState) => void;
  dismiss: () => void;
  success: (message: string) => void;
  error: (message: string) => void;
  message: (message: string) => void;
};

export function createUploadToastManager(): UploadToastManager {
  let toastId: string | undefined;
  let state: UploadToastState | null = null;
  let minimized = false;

  const dismiss = () => {
    if (toastId) {
      toast.dismiss(toastId);
      toastId = undefined;
    }
    state = null;
    minimized = false;
  };

  const render = () => {
    if (!state) return;
    const currentState = state;

    toastId = toast.custom(
      () => (
        <UploadProgressToast
          {...currentState}
          minimized={minimized}
          toggleMinimized={() => {
            minimized = !minimized;
            render();
          }}
        />
      ),
      {
        id: toastId,
        duration: Infinity,
      },
    );
  };

  const notify = (kind: "success" | "error" | "message", message: string) => {
    dismiss();
    if (kind === "success") {
      toast.success(message, { duration: SUCCESS_DURATION_MS });
      return;
    }

    if (kind === "error") {
      toast.error(message, { duration: ERROR_DURATION_MS });
      return;
    }

    toast(message, { duration: MESSAGE_DURATION_MS });
  };

  return {
    show(nextState: UploadToastState) {
      state = nextState;
      if (!toastId) minimized = false;
      render();
    },

    dismiss,

    success(message: string) {
      notify("success", message);
    },

    error(message: string) {
      notify("error", message);
    },

    message(message: string) {
      notify("message", message);
    },
  };
}

type UploadProgressToastProps = UploadToastState & {
  minimized: boolean;
  toggleMinimized: () => void;
};

function UploadProgressToast(props: UploadProgressToastProps) {
  const { t } = useLanguage();
  const progress =
    typeof props.percent === "number"
      ? Math.max(0, Math.min(100, props.percent))
      : 0;
  const hasProgress = typeof props.percent === "number";
  const hasByteInfo =
    typeof props.bytesUploaded === "number" &&
    typeof props.totalBytes === "number";
  const bytesUploaded = props.bytesUploaded ?? 0;
  const totalBytes = props.totalBytes ?? 0;
  const remainingBytes = Math.max(totalBytes - bytesUploaded, 0);
  const speedLabel = formatSpeed(props.speedBytesPerSec);

  // Falls back to props.message (e.g. "Preparing upload...") when byte totals aren't known yet.
  const byteSummary = hasByteInfo
    ? `${formatBytes(bytesUploaded)} / ${formatBytes(totalBytes)}${speedLabel ? ` · ${speedLabel}` : ""}`
    : props.message;

  const status = props.isCancelling
    ? t.translations.CANCELLING_SHORT
    : hasProgress
      ? `${t.translations.UPLOADING_PERCENT_PREFIX} ${Math.round(progress)}%`
      : props.title;

  if (props.minimized) {
    return (
      <div className="w-[230px] rounded-lg border border-base-300 bg-base-100 p-2 shadow-lg">
        <div className="mb-1 flex items-center justify-between gap-2">
          <p className="truncate text-xs font-semibold text-base-content">
            {status}
          </p>
          <button
            type="button"
            className="btn btn-xs bg-base-100 btn-soft text-base-content"
            onClick={props.toggleMinimized}
            aria-label={t.translations.EXPAND_UPLOAD_TOAST}
          >
            <ChevronDownIcon className="size-4" />
          </button>
        </div>
        {hasByteInfo && (
          <p className="mt-1 text-[11px] text-base-content/65">
            {formatBytes(remainingBytes)} {t.translations.LEFT}
          </p>
        )}
      </div>
    );
  }

  return (
    <div className="w-[260px] rounded-lg border border-base-300 bg-base-100 p-3 shadow-lg">
      <div className="mb-2 flex items-center justify-between gap-2">
        <p className="truncate text-xs font-semibold text-base-content">
          {status}
        </p>
        <button
          type="button"
          className="btn btn-xs bg-base-100 btn-soft text-base-content"
          onClick={props.toggleMinimized}
          aria-label={t.translations.MINIMIZE_UPLOAD_TOAST}
        >
          <ChevronUpIcon className="size-4" />
        </button>
      </div>
      {hasProgress && (
        <progress
          className="progress progress-primary h-1.5 w-full"
          value={progress}
          max="100"
        />
      )}
      <p className="mt-1 text-[11px] text-base-content/65">{byteSummary}</p>
      {props.isCancelling && (
        <p className="mt-1 text-[11px] font-medium text-warning">
          {t.translations.CANCELLING_SHORT}
        </p>
      )}
      {props.onCancel && (
        <button
          type="button"
          className="btn btn-xs btn-outline btn-error mt-2 w-full"
          onClick={props.onCancel}
          disabled={props.cancelDisabled}
        >
          {props.cancelDisabled ? (
            <>
              <span className="loading loading-spinner loading-xs"></span>
              {t.translations.CANCELLING_SHORT}
            </>
          ) : (
            t.translations.CANCEL
          )}
        </button>
      )}
    </div>
  );
}