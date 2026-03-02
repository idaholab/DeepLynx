"use client";

import { useLanguage } from "@/app/contexts/Language";
import { ChevronDownIcon, ChevronUpIcon } from "@heroicons/react/24/outline";
import toast from "react-hot-toast";

const SUCCESS_DURATION_MS = 3000;
const ERROR_DURATION_MS = 5000;
const MESSAGE_DURATION_MS = 3000;

export type UploadToastState = {
  title: string;
  message: string;
  percent?: number;
  chunksCompleted?: number;
  totalChunks?: number;
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
  const chunksLabel = t.translations.CHUNKS;
  const leftLabel = t.translations.LEFT;
  const progress =
    typeof props.percent === "number"
      ? Math.max(0, Math.min(100, props.percent))
      : 0;
  const hasProgress = typeof props.percent === "number";
  const hasChunkInfo =
    typeof props.chunksCompleted === "number" &&
    typeof props.totalChunks === "number";
  const completedChunks = props.chunksCompleted ?? 0;
  const totalChunks = props.totalChunks ?? 0;
  const remainingChunks = Math.max(totalChunks - completedChunks, 0);
  const chunkSummary = hasChunkInfo
    ? `${completedChunks} / ${totalChunks} ${chunksLabel}`
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
        {hasChunkInfo && (
          <p className="mt-1 text-[11px] text-base-content/65">
            {remainingChunks} {leftLabel}
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
      <p className="mt-1 text-[11px] text-base-content/65">{chunkSummary}</p>
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
