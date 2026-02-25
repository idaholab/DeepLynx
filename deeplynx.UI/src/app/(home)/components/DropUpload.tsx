"use client";
import { useLanguage } from "@/app/contexts/Language";
import React, { useCallback, useRef, useState } from "react";

type Props = {
  multiple: boolean;
  disabled?: boolean;
  files: File[];
  onFilesChange: (files: File[]) => void;
  accept?: string; // optional, e.g. ".csv,.json"
};

export default function DropUpload({
  multiple,
  disabled,
  files,
  onFilesChange,
  accept,
}: Props) {
  const { t } = useLanguage();
  const inputRef = useRef<HTMLInputElement>(null);
  const [isDragging, setIsDragging] = useState(false);

  const triggerPicker = () => {
    if (!disabled) inputRef.current?.click();
  };

  const mergeFiles = useCallback(
    (incoming: File[]) => {
      if (!multiple) {
        onFilesChange(incoming.slice(0, 1));
        return;
      }
      // merge + de‑dupe by name/size/lastModified
      const key = (f: File) => `${f.name}-${f.size}-${f.lastModified}`;
      const map = new Map<string, File>();
      [...files, ...incoming].forEach((f) => map.set(key(f), f));
      onFilesChange([...map.values()]);
    },
    [files, multiple, onFilesChange]
  );

  const handleInput = (e: React.ChangeEvent<HTMLInputElement>) => {
    const list = Array.from(e.target.files || []);
    mergeFiles(list);
    // reset input so selecting the same file again fires change
    e.currentTarget.value = "";
  };

  const handleDrop = (e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDragging(false);
    if (disabled) return;

    const dropped = Array.from(e.dataTransfer.files || []);
    if (dropped.length === 0) return;
    mergeFiles(dropped);
  };

  const handleDragOver = (e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.stopPropagation();
    if (!disabled) setIsDragging(true);
  };

  const handleDragLeave = (e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDragging(false);
  };

  const handleKey = (e: React.KeyboardEvent<HTMLDivElement>) => {
    if (disabled) return;
    if (e.key === "Enter" || e.key === " ") {
      e.preventDefault();
      triggerPicker();
    }
  };

  return (
    <div
      tabIndex={disabled ? -1 : 0}
      aria-disabled={disabled}
      onClick={triggerPicker}
      onKeyDown={handleKey}
      onDrop={handleDrop}
      onDragOver={handleDragOver}
      onDragEnter={handleDragOver}
      onDragLeave={handleDragLeave}
      className={[
        "btn rounded-xl border p-10 transition",
        "flex flex-col items-center justify-center text-center gap-2",
        disabled
          ? "opacity-30 pointer-events-none"
          : isDragging
            ? "border-secondary/70 bg-secondary/10"
            : "border-base-300 hover:bg-base-200/40",
      ].join(" ")}
    >
      {/* Hidden input that the container triggers */}
      <input
        ref={inputRef}
        type="file"
        multiple={multiple}
        accept={accept}
        className="hidden"
        onChange={handleInput}
        disabled={disabled}
      />

      <div className="text-lg font-medium">
        {t.translations.DRAG_N_DROP_FILES_HERE}
      </div>
      <div className="text-sm opacity-70">
        {t.translations.OR}{" "}
        <span className="link link-secondary">
          {t.translations.CLICK_TO_BROWSE}
        </span>
      </div>
      {accept && (
        <div className="text-xs opacity-60">
          {t.translations.ACCEPTED} {accept}
        </div>
      )}
    </div>
  );
}
