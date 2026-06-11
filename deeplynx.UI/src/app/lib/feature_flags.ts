export function isInsightHidden(): boolean {
  // Hidden by default. Trim + lowercase so this matches the backend's
  // bool.TryParse semantics (case-insensitive, whitespace-tolerant) and the
  // UI never disagrees with the API about whether Insight is enabled.
  return process.env.NEXT_PUBLIC_HIDE_INSIGHT?.trim().toLowerCase() !== "false";
}
