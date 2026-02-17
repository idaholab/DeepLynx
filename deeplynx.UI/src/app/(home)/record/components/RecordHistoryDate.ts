"use client";

import { formatLocalDateTime } from "@/app/lib/date_time";

export function formatRecordHistoryDate(
  value?: string | null,
  placeholder: string = "N/A",
): string {
  if (!value) return placeholder;

  const formatted = formatLocalDateTime(value);
  return formatted.includes("Invalid Date") ? value : formatted;
}

