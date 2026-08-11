"use client";
import { useLanguage } from "@/app/contexts/Language";

interface UploadProgressBarProps {
  progress: number; // 0-100
  current: number;
  total: number;
}

export default function UploadProgressBar({
  progress,
  current,
  total,
}: UploadProgressBarProps) {
  const { t } = useLanguage();

  return (
    <div className="space-y-2">
      <div className="flex items-center justify-between text-sm">
        <span className="font-semibold">{t.translations.UPLOADING_RECORDS}</span>
        <span className="text-base-content/70">
          {current} / {total}
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
