import { TagResponseDto } from "@/app/(home)/types/responseDTOs";

/**
 * Escapes special RegExp metacharacters in a string so it can be safely
 * embedded inside `new RegExp(...)` without accidentally treating user-typed
 * characters like `.`, `*`, or `(` as pattern operators.
 * Private to this module — callers should use getHighlightedContent instead.
 */
function escapeRegExp(value: string) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

export function interpolateTemplate(
  template: string,
  values: Record<string, string | number>,
) {
  return Object.entries(values).reduce(
    (result, [key, value]) => result.replaceAll(`{${key}}`, String(value)),
    template,
  );
}

/**
 * Wraps any portion of `text` that matches one of the active search `queries`
 * in a styled <mark> element so users can see why a record appeared in results.
 *
 * Returns an object rather than just ReactNode so that callers can branch on
 * `matched` if needed (e.g. to apply additional styling to the whole field).
 *
 * Why `unknown` for text: record fields coming from the API can be strings,
 * numbers, or null depending on the endpoint. Accepting `unknown` and
 * converting with String() centralises that coercion here rather than at
 * every call site.
 *
 * Why split+test instead of replaceAll: React requires an array of ReactNodes
 * with stable keys, not a raw HTML string, so we split on the match, then
 * wrap matched segments in <mark> and leave non-matched segments as plain
 * strings that React renders as text nodes.
 *
 * The key uses both `part` content and `index` because the same word can
 * appear multiple times in the same string and would otherwise produce
 * duplicate keys.
 */
export function getHighlightedContent(
  text: unknown,
  queries: string[],
): { content: React.ReactNode; matched: boolean } {
  const safeText = String(text ?? "");

  // Only highlight the first matching query to keep rendering simple.
  const match = queries.find((q) =>
    safeText.toLowerCase().includes(q.toLowerCase()),
  );

  if (!match) return { content: safeText, matched: false };

  const regex = new RegExp(`(${escapeRegExp(match)})`, "gi");
  const parts = safeText.split(regex);

  return {
    matched: true,
    content: parts.map((part, index) =>
      regex.test(part) ? (
        <mark
          key={`${part}-${index}`}
          className="rounded bg-warning px-1 text-warning-content"
        >
          {part}
        </mark>
      ) : (
        part
      ),
    ),
  };
}

/**
 * Normalises the `tags` field on a record into a flat string array.
 *
 * Tags are stored as a JSON-serialised value in the database and have
 * accumulated several shapes over time:
 *   - A plain string (legacy): "finance"
 *   - An array of strings: ["finance", "approved"]
 *   - An array of TagResponseDto objects: [{ id: 1, name: "finance" }]
 *   - A single TagResponseDto: { id: 1, name: "finance" }
 *
 * The function defensively handles all of these so the UI never crashes on
 * older records and new records are automatically supported. Returns [] on
 * parse failure so callers can treat it as "no tags" without error handling.
 */
export function parseRecordTags(tags: string | null | undefined) {
  if (!tags) return [];

  try {
    const parsed = JSON.parse(tags);

    // Normalise to array regardless of whether the stored value was a single
    // object or an array.
    const arr = Array.isArray(parsed) ? parsed : [parsed];

    return arr.flatMap((item: TagResponseDto | string) => {
      if (typeof item === "string") return [item];
      if (item && typeof item === "object") {
        // Prefer the canonical `name` field; fall back to any string values
        // in the object for forward-compatibility with schema changes.
        if (typeof item.name === "string") return [item.name];
        return Object.values(item).filter(
          (value): value is string => typeof value === "string",
        );
      }
      return [];
    });
  } catch {
    return [];
  }
}

/**
 * Counts occurrences of each unique value in `values` and returns the result
 * sorted by count descending, then label ascending as a deterministic
 * tiebreaker so facet options don't jump around between renders.
 *
 * Blank / whitespace-only values are intentionally skipped because they
 * cannot be meaningfully filtered on and would clutter the sidebar.
 */
export function countFacet(values: string[]) {
  const counts = new Map<string, number>();

  values.forEach((value) => {
    const trimmed = value.trim();
    if (!trimmed) return;
    counts.set(trimmed, (counts.get(trimmed) ?? 0) + 1);
  });

  return Array.from(counts.entries())
    .map(([label, count]) => ({ label, count }))
    .sort((a, b) => b.count - a.count || a.label.localeCompare(b.label));
}
