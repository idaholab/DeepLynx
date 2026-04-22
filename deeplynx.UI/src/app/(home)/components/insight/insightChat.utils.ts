"use client";

import type { InsightModelSelection } from "./useInsightModelSelection";

export function formatInsightTimestamp(): string {
  return new Intl.DateTimeFormat(undefined, {
    hour: "numeric",
    minute: "2-digit",
  }).format(new Date());
}

export function buildInsightModelBadges(
  selectedInsightModels: InsightModelSelection,
): string[] {
  return [
    selectedInsightModels.queryModelName
      ? `Q: ${selectedInsightModels.queryModelName}`
      : null,
    selectedInsightModels.uploadModelName
      ? `U: ${selectedInsightModels.uploadModelName}`
      : null,
    selectedInsightModels.embeddingModelName
      ? `E: ${selectedInsightModels.embeddingModelName}`
      : null,
  ].filter((badgeLabel): badgeLabel is string => Boolean(badgeLabel));
}
