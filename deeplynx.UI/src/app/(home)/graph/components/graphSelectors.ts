import {
  GraphConnectionSummary,
  GraphExplorerData,
  GraphNodeSummary,
  GraphPathNode,
  GraphPathResult,
  GraphPathSegment,
  GraphStats,
  GraphViewMode,
} from "./graphTypes";

const interpolate = (template: string, values: Record<string, string | number>) =>
  Object.entries(values).reduce(
    (result, [key, value]) => result.replace(`{${key}}`, String(value)),
    template,
  );

// Build fast lookup maps for repeated id-based access across the graph page.
export const buildNodeLookup = (graphData: GraphExplorerData | null) =>
  new Map(graphData?.nodes.map((node) => [node.id, node]) ?? []);

// Collapse raw links into one row per connected record so the relationship
// table can represent bidirectional links cleanly.
export const buildSelectedNodeConnections = (
  graphData: GraphExplorerData | null,
  selectedNodeId: number | null,
  nodeLookup: Map<number, GraphNodeSummary>,
  translations: Record<string, string>,
): GraphConnectionSummary[] => {
  if (!graphData || !selectedNodeId) return [];

  const groupedConnections = new Map<
    number,
    {
      edgeIds: number[];
      relationshipNames: Set<string>;
      hasIncoming: boolean;
      hasOutgoing: boolean;
    }
  >();

  graphData.links
    .filter(
      (link) => link.source === selectedNodeId || link.target === selectedNodeId,
    )
    .forEach((link) => {
      const isOutgoing = link.source === selectedNodeId;
      const connectedNodeId = isOutgoing ? link.target : link.source;

      const existing = groupedConnections.get(connectedNodeId) ?? {
        edgeIds: [],
        relationshipNames: new Set<string>(),
        hasIncoming: false,
        hasOutgoing: false,
      };

      existing.edgeIds.push(link.edgeId);
      existing.relationshipNames.add(
        link.relationshipName || translations.RELATED_TO,
      );
      existing.hasOutgoing ||= isOutgoing;
      existing.hasIncoming ||= !isOutgoing;

      groupedConnections.set(connectedNodeId, existing);
    });

  return Array.from(groupedConnections.entries()).map(
    ([connectedNodeId, connection]) => ({
      rowId: `${selectedNodeId}:${connectedNodeId}`,
      edgeId: connection.edgeIds[0],
      direction:
        connection.hasIncoming && connection.hasOutgoing
          ? "Bidirectional"
          : connection.hasIncoming
            ? "Incoming"
            : "Outgoing",
      relationshipName: Array.from(connection.relationshipNames).join(" / "),
      connectedNodeId,
      connectedNodeLabel:
        nodeLookup.get(connectedNodeId)?.label ||
        interpolate(translations.RECORD_WITH_ID, { id: connectedNodeId }),
      connectedNodeDepth: nodeLookup.get(connectedNodeId)?.depth ?? null,
    }),
  );
};

// Apply the table's direction filter while keeping bidirectional rows visible
// in both incoming and outgoing views.
export const buildFilteredConnections = (
  selectedNodeConnections: GraphConnectionSummary[],
  viewMode: GraphViewMode,
) => {
  if (viewMode === "all" || viewMode === "path") {
    return selectedNodeConnections;
  }

  return selectedNodeConnections.filter((connection) =>
    viewMode === "incoming"
      ? connection.direction === "Incoming" ||
        connection.direction === "Bidirectional"
      : connection.direction === "Outgoing" ||
        connection.direction === "Bidirectional",
  );
};

// Trace the shortest discovered path from the root node to the selected node
// so the graph and side panel can highlight the same lineage.
export const buildPathToSelected = (
  graphData: GraphExplorerData | null,
  selectedNodeId: number | null,
): GraphPathResult => {
  if (!graphData || !selectedNodeId || !graphData.rootNodeId) {
    return { nodeIds: [], edgeIds: [] };
  }

  if (selectedNodeId === graphData.rootNodeId) {
    return { nodeIds: [selectedNodeId], edgeIds: [] };
  }

  const visited = new Set<number>([graphData.rootNodeId]);
  const queue: Array<{
    nodeId: number;
    pathNodeIds: number[];
    pathEdgeIds: number[];
  }> = [
    {
      nodeId: graphData.rootNodeId,
      pathNodeIds: [graphData.rootNodeId],
      pathEdgeIds: [],
    },
  ];

  while (queue.length > 0) {
    const current = queue.shift();
    if (!current) continue;

    for (const link of graphData.links) {
      let nextNodeId: number | null = null;

      if (link.source === current.nodeId && !visited.has(link.target)) {
        nextNodeId = link.target;
      } else if (link.target === current.nodeId && !visited.has(link.source)) {
        nextNodeId = link.source;
      }

      if (nextNodeId === null) continue;

      const nextPathNodeIds = [...current.pathNodeIds, nextNodeId];
      const nextPathEdgeIds = [...current.pathEdgeIds, link.edgeId];

      if (nextNodeId === selectedNodeId) {
        return { nodeIds: nextPathNodeIds, edgeIds: nextPathEdgeIds };
      }

      visited.add(nextNodeId);
      queue.push({
        nodeId: nextNodeId,
        pathNodeIds: nextPathNodeIds,
        pathEdgeIds: nextPathEdgeIds,
      });
    }
  }

  return {
    nodeIds: selectedNodeId ? [selectedNodeId] : [],
    edgeIds: [],
  };
};

// Convert traced node ids into presentational objects for the path panel.
export const buildPathNodes = (
  pathToSelected: GraphPathResult,
  nodeLookup: Map<number, GraphNodeSummary>,
  selectedNodeId: number | null,
  translations: Record<string, string>,
): GraphPathNode[] =>
  pathToSelected.nodeIds.map((nodeId, index) => ({
    id: nodeId,
    label:
      nodeLookup.get(nodeId)?.label ||
      interpolate(translations.RECORD_WITH_ID, { id: nodeId }),
    type: nodeLookup.get(nodeId)?.type || translations.GRAPH_RECORD_TYPE,
    isRoot: index === 0,
    isSelected: nodeId === selectedNodeId,
  }));

// Convert traced edge ids into readable hop summaries for the side panel.
export const buildPathSegments = (
  graphData: GraphExplorerData | null,
  nodeLookup: Map<number, GraphNodeSummary>,
  pathToSelected: GraphPathResult,
  translations: Record<string, string>,
): GraphPathSegment[] => {
  if (!graphData || pathToSelected.nodeIds.length <= 1) return [];

  return pathToSelected.edgeIds.map((edgeId, index) => {
    const link = graphData.links.find((candidate) => candidate.edgeId === edgeId);
    const fromNodeId = pathToSelected.nodeIds[index];
    const toNodeId = pathToSelected.nodeIds[index + 1];

    return {
      edgeId,
      fromNodeId,
      toNodeId,
      fromLabel:
        nodeLookup.get(fromNodeId)?.label ||
        interpolate(translations.RECORD_WITH_ID, { id: fromNodeId }),
      toLabel:
        nodeLookup.get(toNodeId)?.label ||
        interpolate(translations.RECORD_WITH_ID, { id: toNodeId }),
      relationshipName: link?.relationshipName || translations.RELATED_TO,
    };
  });
};

// Keep search cheap by filtering only the current graph payload.
export const buildSearchResults = (
  graphData: GraphExplorerData | null,
  searchQuery: string,
) => {
  const query = searchQuery.trim().toLowerCase();
  if (!graphData || !query) return [];

  return graphData.nodes
    .filter((node) => node.label.toLowerCase().includes(query))
    .slice(0, 6);
};

// Provide quick graph counts for any summary UI that needs them.
export const buildGraphStats = (
  graphData: GraphExplorerData | null,
  selectedNodeId: number | null,
): GraphStats => {
  if (!graphData) {
    return { nodes: 0, links: 0, incoming: 0, outgoing: 0 };
  }

  const selectedIncoming = graphData.links.filter(
    (link) => link.target === selectedNodeId,
  ).length;
  const selectedOutgoing = graphData.links.filter(
    (link) => link.source === selectedNodeId,
  ).length;

  return {
    nodes: graphData.nodes.length,
    links: graphData.links.length,
    incoming: selectedIncoming,
    outgoing: selectedOutgoing,
  };
};
