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
  defaultModelLabel: string,
): string[] {
  return [
    `Q: ${
      selectedInsightModels.queryModelConfigId === null
        ? defaultModelLabel
        : selectedInsightModels.queryModelName ?? defaultModelLabel
    }`,
    `U: ${
      selectedInsightModels.uploadModelConfigId === null
        ? defaultModelLabel
        : selectedInsightModels.uploadModelName ?? defaultModelLabel
    }`,
    `E: ${
      selectedInsightModels.embeddingModelConfigId === null
        ? defaultModelLabel
        : selectedInsightModels.embeddingModelName ?? defaultModelLabel
    }`,
  ];
}
