"use client";
import { useLanguage } from "@/app/contexts/Language";

interface UploadProgressBarProps {
  bytesUploaded: number;
  totalBytes: number;
  speedBytesPerSec?: number;
}

function formatBytes(bytes: number): string {
  if (!isFinite(bytes) || bytes < 0) return "0 B";
  if (bytes >= 1024 ** 3) return `${(bytes / 1024 ** 3).toFixed(2)} GB`;
  if (bytes >= 1024 ** 2) return `${(bytes / 1024 ** 2).toFixed(1)} MB`;
  if (bytes >= 1024) return `${(bytes / 1024).toFixed(0)} KB`;
  return `${Math.round(bytes)} B`;
}

function formatSpeed(bytesPerSec: number): string {
  if (!bytesPerSec || !isFinite(bytesPerSec) || bytesPerSec <= 0) return "";
  return `${formatBytes(bytesPerSec)}/s`;
}

export default function UploadProgressBar({
  bytesUploaded,
  totalBytes,
  speedBytesPerSec,
}: UploadProgressBarProps) {
  const { t } = useLanguage();

  const progress =
    totalBytes > 0 ? Math.min(100, (bytesUploaded / totalBytes) * 100) : 0;
  const speedLabel = speedBytesPerSec ? formatSpeed(speedBytesPerSec) : "";

  return (
    <div className="space-y-2">
      <div className="flex items-center justify-between text-sm">
        <span className="font-semibold">{t.translations.UPLOADING_RECORDS}</span>
        <span className="text-base-content/70">
          {formatBytes(bytesUploaded)} / {formatBytes(totalBytes)}
          {speedLabel ? ` · ${speedLabel}` : ""}
        </span>
      </div>

      <div className="w-full bg-base-300 rounded-full h-4 overflow-hidden">
        <div
          className="bg-primary h-full transition-all duration-300 ease-out flex items-center justify-end pr-2"
          style={{ width: `${progress}%` }}
        >
          {progress > 10 && (
            <span className="text-xs font-semibold text-primary-content">
              {Math.round(progress)}%
            </span>
          )}
        </div>
      </div>

      <p className="text-xs text-center text-base-content/60">
        {t.translations.PLEASE_WAIT_DO_NOT_CLOSE_WINDOW}
      </p>
    </div>
  );
}