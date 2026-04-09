"use client";

import { useLanguage } from "@/app/contexts/Language";
import { CheckIcon, DocumentDuplicateIcon } from "@heroicons/react/24/outline";
import React, { useEffect, useRef, useState } from "react";

const COPY_FEEDBACK_DURATION_MS = 1500;

interface CopyToClipboardButtonProps {
  value: string;
  tooltipLabel: string;
  ariaLabel: string;
  className?: string;
  idleIconClassName?: string;
  copiedIconClassName?: string;
}

export default function CopyToClipboardButton({
  value,
  tooltipLabel,
  ariaLabel,
  className,
  idleIconClassName = "size-4 text-base-content/70",
  copiedIconClassName = "size-4 text-success",
}: CopyToClipboardButtonProps) {
  const { t } = useLanguage();
  const [isCopied, setIsCopied] = useState(false);
  const copyFeedbackTimeoutRef = useRef<number | null>(null);

  const handleCopy = async () => {
    try {
      if (copyFeedbackTimeoutRef.current) {
        window.clearTimeout(copyFeedbackTimeoutRef.current);
      }

      await navigator.clipboard.writeText(value);
      setIsCopied(true);

      copyFeedbackTimeoutRef.current = window.setTimeout(() => {
        setIsCopied(false);
      }, COPY_FEEDBACK_DURATION_MS);
    } catch (error) {
      console.error("Failed to copy value:", error);
    }
  };

  useEffect(() => {
    return () => {
      if (copyFeedbackTimeoutRef.current) {
        window.clearTimeout(copyFeedbackTimeoutRef.current);
      }
    };
  }, []);

  return (
    <div
      className="tooltip tooltip-top copy-tooltip"
      data-tip={isCopied ? t.translations.COPIED : tooltipLabel}
    >
      <button
        type="button"
        onClick={handleCopy}
        aria-label={ariaLabel}
        className={
          className ??
          "btn btn-ghost btn-xs btn-square shrink-0 transition-all duration-150 hover:scale-110 active:scale-95"
        }
      >
        {isCopied ? (
          <CheckIcon className={copiedIconClassName} />
        ) : (
          <DocumentDuplicateIcon className={idleIconClassName} />
        )}
      </button>
    </div>
  );
}
