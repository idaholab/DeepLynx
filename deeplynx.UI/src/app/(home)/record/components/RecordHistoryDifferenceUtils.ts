import { HistoricalRecordResponseDto } from "@/app/(home)/types/responseDTOs";

// Comparison mode used by history controls.
export type CompareMode = "previous" | "latest" | "manual";

// Flat field-level difference row before tree grouping.
export interface DifferenceRow {
  field: string;
  current: string;
  compare: string;
  changed: boolean;
}

export interface DifferenceTreeNode {
  id: string;
  label: string;
  changed: boolean;
  current?: string;
  compare?: string;
  children: DifferenceTreeNode[];
  leafCount: number;
  isLeaf: boolean;
}

// Render-friendly flattened tree row with indentation depth.
export interface FlatDifferenceRow {
  node: DifferenceTreeNode;
  depth: number;
}

// Safely parse JSON-like strings while tolerating non-JSON input.
export function parseJsonProperties(value: unknown): unknown {
  if (typeof value !== "string") return value;
  const trimmed = value.trim();
  if (!trimmed) return value;

  const looksLikeJson =
    (trimmed.startsWith("{") && trimmed.endsWith("}")) ||
    (trimmed.startsWith("[") && trimmed.endsWith("]"));

  if (!looksLikeJson) return value;

  try {
    return JSON.parse(trimmed);
  } catch {
    return value;
  }
}

function flattenObject(
  value: unknown,
  prefix: string,
  out: Record<string, string>,
): void {
  if (value === null) {
    out[prefix] = "null";
    return;
  }

  if (typeof value === "undefined") return;

  if (Array.isArray(value)) {
    if (value.length === 0) {
      out[prefix] = "[]";
      return;
    }

    value.forEach((item, index) => {
      flattenObject(item, `${prefix}[${index}]`, out);
    });
    return;
  }

  if (typeof value === "object") {
    const entries = Object.entries(value as Record<string, unknown>);
    if (entries.length === 0) {
      out[prefix] = "{}";
      return;
    }

    entries.forEach(([key, nested]) => {
      flattenObject(nested, `${prefix}.${key}`, out);
    });
    return;
  }

  out[prefix] = String(value);
}

// Normalize historical record fields into a flattened key/value map for difference checks.
export function normalizeRecord(
  record: HistoricalRecordResponseDto | null,
): Record<string, string> {
  if (!record) return {};

  const normalized: Record<string, string> = {};

  const set = (key: string, value: unknown) => {
    if (value === null || typeof value === "undefined") return;
    const stringValue = String(value);
    if (stringValue.length === 0) return;
    normalized[key] = stringValue;
  };

  set("record.id", record.id);
  set("record.name", record.name);
  set("record.description", record.description);
  set("record.uri", record.uri);
  set("record.originalId", record.originalId);
  set("record.classId", record.classId);
  set("record.className", record.className);
  set("record.dataSourceId", record.dataSourceId);
  set("record.dataSourceName", record.dataSourceName);
  set("record.projectId", record.projectId);
  set("record.projectName", record.projectName);
  set("record.objectStorageId", record.objectStorageId);
  set("record.objectStorageName", record.objectStorageName);
  set("record.lastUpdatedAt", record.lastUpdatedAt);
  set("record.lastUpdatedBy", record.lastUpdatedBy);
  set("record.isArchived", record.isArchived);

  const parsedProperties = parseJsonProperties(record.properties);
  if (parsedProperties && typeof parsedProperties === "object") {
    flattenObject(parsedProperties, "properties", normalized);
  } else {
    set("properties", parsedProperties);
  }

  const parsedTags = parseJsonProperties(record.tags);
  if (parsedTags && typeof parsedTags === "object") {
    flattenObject(parsedTags, "tags", normalized);
  } else {
    set("tags", parsedTags);
  }

  return normalized;
}

// Display helper: convert path tokens into readable labels.
function prettifyField(field: string): string {
  return field
    .replace(/\./g, " / ")
    .replace(/_/g, " ")
    .replace(/\b\w/g, (c) => c.toUpperCase());
}

function parseFieldSegments(field: string): string[] {
  const matches = field.match(/([^[.\]]+)|(\[\d+\])/g);
  return matches ?? [field];
}

function prettifySegment(segment: string): string {
  if (segment.startsWith("[") && segment.endsWith("]")) return segment;
  return prettifyField(segment);
}

// Build a hierarchical difference tree from flat dot/bracket path fields.
export function buildDifferenceTree(rows: DifferenceRow[]): DifferenceTreeNode[] {
  const roots = new Map<string, DifferenceTreeNode>();

  const ensureChild = (
    container: Map<string, DifferenceTreeNode> | DifferenceTreeNode,
    segment: string,
    id: string,
  ) => {
    if (container instanceof Map) {
      if (!container.has(id)) {
        container.set(id, {
          id,
          label: prettifySegment(segment),
          changed: false,
          children: [],
          leafCount: 0,
          isLeaf: false,
        });
      }
      return container.get(id)!;
    }

    let child = container.children.find((c) => c.id === id);
    if (!child) {
      child = {
        id,
        label: prettifySegment(segment),
        changed: false,
        children: [],
        leafCount: 0,
        isLeaf: false,
      };
      container.children.push(child);
    }
    return child;
  };

  rows.forEach((row) => {
    const segments = parseFieldSegments(row.field);
    let currentPath = "";
    let currentNode: DifferenceTreeNode | null = null;

    segments.forEach((segment, index) => {
      currentPath = currentPath ? `${currentPath}.${segment}` : segment;

      if (index === 0) {
        currentNode = ensureChild(roots, segment, currentPath);
      } else if (currentNode) {
        currentNode = ensureChild(currentNode, segment, currentPath);
      }

      if (index === segments.length - 1 && currentNode) {
        currentNode.current = row.current;
        currentNode.compare = row.compare;
        currentNode.changed = row.changed;
        currentNode.isLeaf = true;
      }
    });
  });

  const finalize = (node: DifferenceTreeNode): DifferenceTreeNode => {
    if (node.children.length === 0) {
      return {
        ...node,
        leafCount: 1,
        isLeaf: true,
      };
    }

    const finalizedChildren = node.children
      .map(finalize)
      .sort((a, b) => a.label.localeCompare(b.label));
    const leafCount = finalizedChildren.reduce(
      (sum, child) => sum + child.leafCount,
      0,
    );
    const changed = finalizedChildren.some((child) => child.changed);

    return {
      ...node,
      children: finalizedChildren,
      changed,
      leafCount,
      isLeaf: false,
    };
  };

  return Array.from(roots.values())
    .map(finalize)
    .sort((a, b) => a.label.localeCompare(b.label));
}

// Keep only changed leaves and their parent path segments.
export function filterTreeForChanges(
  nodes: DifferenceTreeNode[],
): DifferenceTreeNode[] {
  return nodes
    .map((node) => {
      if (node.isLeaf) return node.changed ? node : null;

      const filteredChildren = filterTreeForChanges(node.children);
      if (filteredChildren.length === 0 && !node.changed) return null;

      return {
        ...node,
        children: filteredChildren,
      };
    })
    .filter((node): node is DifferenceTreeNode => node !== null);
}

// Flatten expanded tree nodes into render rows with depth info.
export function flattenVisibleTree(
  nodes: DifferenceTreeNode[],
  expandedRows: Set<string>,
  depth = 0,
): FlatDifferenceRow[] {
  const rows: FlatDifferenceRow[] = [];

  nodes.forEach((node) => {
    rows.push({ node, depth });
    if (node.children.length > 0 && expandedRows.has(node.id)) {
      rows.push(...flattenVisibleTree(node.children, expandedRows, depth + 1));
    }
  });

  return rows;
}
