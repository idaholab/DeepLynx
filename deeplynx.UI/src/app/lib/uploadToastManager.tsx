// app/lib/uploadToastManager.tsx

import toast from "react-hot-toast";

export type UploadToastState = {
  title: string;
  message: string;
  percent?: number;
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

  const dismissActiveToast = () => {
    if (toastId) {
      toast.dismiss(toastId);
      toastId = undefined;
    }
  };

  return {
    show(state: UploadToastState) {
      toastId = toast.custom(() => <UploadProgressToast {...state} />, {
        id: toastId,
        duration: Infinity,
      });
    },

    dismiss() {
      dismissActiveToast();
    },

    success(message: string) {
      dismissActiveToast();
      toast.success(message, { duration: 3000 });
    },

    error(message: string) {
      dismissActiveToast();
      toast.error(message, { duration: 5000 });
    },

    message(message: string) {
      dismissActiveToast();
      toast(message, { duration: 3000 });
    },
  };
}

// Inline component so it can live in this file
function UploadProgressToast(props: UploadToastState) {
  return (
    <div className="w-[320px] rounded-lg border border-base-300 bg-base-100 p-4 shadow-lg">
      <p className="text-sm font-semibold text-base-content">{props.title}</p>
      <p className="mt-1 text-xs text-base-content/70">{props.message}</p>
      {typeof props.percent === "number" && (
        <div className="mt-3">
          <div className="mb-1 flex items-center justify-between text-xs">
            <span className="text-base-content/70">Progress</span>
            <span className="font-semibold text-base-content">
              {Math.round(props.percent)}%
            </span>
          </div>
          <progress
            className="progress progress-primary w-full"
            value={props.percent}
            max="100"
          />
        </div>
      )}
      {props.isCancelling && (
        <p className="mt-2 text-xs font-medium text-warning">
          Cancelling upload...
        </p>
      )}
      {props.onCancel && (
        <button
          type="button"
          className="btn btn-sm btn-outline btn-error w-full mt-3"
          onClick={props.onCancel}
          disabled={props.cancelDisabled}
        >
          {props.cancelDisabled ? (
            <>
              <span className="loading loading-spinner loading-xs"></span>
              Cancelling and cleaning up...
            </>
          ) : (
            "Cancel Upload"
          )}
        </button>
      )}
    </div>
  );
}
