"use client";

import React, {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from "react";
import Graph from "graphology";
import { Attributes } from "graphology-types";
import {
  ArrowPathIcon,
  ArrowTopRightOnSquareIcon,
  ArrowsPointingOutIcon,
  InformationCircleIcon,
  MagnifyingGlassIcon,
} from "@heroicons/react/24/outline";
import { useRouter } from "next/navigation";
import { RecordResponseDto } from "@/app/(home)/types/responseDTOs";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import {
  getGraphDataForRecord,
  getRecord,
} from "@/app/lib/client_service/record_services.client";

interface NodeAttributes extends Attributes {
  x: number;
  y: number;
  size: number;
  label: string;
  color: string;
  nodeType: string;
}

interface EdgeAttributes extends Attributes {
  size?: number;
  color?: string;
  label?: string;
  relationshipId?: number | null;
  edgeId?: number;
}

interface GraphClientPageProps {
  projectId: number;
  recordId: number;
  depth?: number;
}

interface GraphNodeSummary {
  id: number;
  label: string;
  type: string;
  depth: number;
}

interface GraphLinkSummary {
  source: number;
  target: number;
  relationshipId: number | null;
  relationshipName: string | null;
  edgeId: number;
}

interface GraphExplorerData {
  nodes: GraphNodeSummary[];
  links: GraphLinkSummary[];
  rootNodeId: number | null;
}

interface GraphController {
  fitGraph: () => void;
  focusNode: (nodeId: number) => void;
  resetView: () => void;
}

type GraphViewMode = "all" | "incoming" | "outgoing" | "path";

type CameraState = {
  x?: number;
  y?: number;
  ratio?: number;
  angle?: number;
};

type CameraInstance = {
  getState?: () => CameraState;
  setState?: (state: CameraState) => void;
  animate?: (
    state: CameraState,
    options?: { duration?: number; easing?: string },
  ) => void;
  animatedReset?: (options?: { duration?: number }) => void;
  on?: (event: string, handler: () => void) => void;
};

type SigmaInstance = {
  getCamera?: () => CameraInstance;
  on?: (event: string, handler: (payload: any) => void) => void;
  refresh?: () => void;
  kill?: () => void;
};

const GraphClientPage = ({
  projectId,
  recordId,
  depth = 3,
}: GraphClientPageProps) => {
  const router = useRouter();
  const { organization } = useOrganizationSession();

  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [graphData, setGraphData] = useState<GraphExplorerData | null>(null);
  const [selectedNodeId, setSelectedNodeId] = useState<number | null>(null);
  const [selectedRecord, setSelectedRecord] =
    useState<RecordResponseDto | null>(null);
  const [isDetailsLoading, setIsDetailsLoading] = useState(false);
  const [searchQuery, setSearchQuery] = useState("");
  const [showAllLabels, setShowAllLabels] = useState(false);
  const [viewMode, setViewMode] = useState<GraphViewMode>("all");

  const controllerRef = useRef<GraphController | null>(null);

  useEffect(() => {
    if (!graphData) return;

    setSelectedNodeId((current) => {
      if (current && graphData.nodes.some((node) => node.id === current)) {
        return current;
      }

      return (
        graphData.nodes.find((node) => node.id === recordId)?.id ??
        graphData.rootNodeId ??
        graphData.nodes[0]?.id ??
        null
      );
    });
  }, [graphData, recordId]);

  useEffect(() => {
    let cancelled = false;

    const loadSelectedRecord = async () => {
      if (!selectedNodeId || !organization?.organizationId) {
        setSelectedRecord(null);
        return;
      }

      try {
        setIsDetailsLoading(true);
        const record = await getRecord(
          organization.organizationId as number,
          projectId,
          selectedNodeId,
          true,
        );

        if (!cancelled) {
          setSelectedRecord(record);
        }
      } catch (err) {
        if (!cancelled) {
          setSelectedRecord(null);
        }
      } finally {
        if (!cancelled) {
          setIsDetailsLoading(false);
        }
      }
    };

    loadSelectedRecord();

    return () => {
      cancelled = true;
    };
  }, [organization?.organizationId, projectId, selectedNodeId]);

  const nodeLookup = useMemo(() => {
    return new Map(graphData?.nodes.map((node) => [node.id, node]) ?? []);
  }, [graphData]);

  const rootNode = useMemo(() => {
    if (!graphData?.rootNodeId) return null;
    return nodeLookup.get(graphData.rootNodeId) ?? null;
  }, [graphData?.rootNodeId, nodeLookup]);

  const selectedNode = useMemo(() => {
    if (!selectedNodeId) return null;
    return nodeLookup.get(selectedNodeId) ?? null;
  }, [nodeLookup, selectedNodeId]);

  const selectedNodeConnections = useMemo(() => {
    if (!graphData || !selectedNodeId) return [];

    return graphData.links
      .filter(
        (link) =>
          link.source === selectedNodeId || link.target === selectedNodeId,
      )
      .map((link) => {
        const isOutgoing = link.source === selectedNodeId;
        const connectedNodeId = isOutgoing ? link.target : link.source;

        return {
          edgeId: link.edgeId,
          direction: isOutgoing ? "Outgoing" : "Incoming",
          relationshipName: link.relationshipName || "Related to",
          connectedNodeId,
          connectedNodeLabel:
            nodeLookup.get(connectedNodeId)?.label ||
            `Record ${connectedNodeId}`,
          connectedNodeDepth: nodeLookup.get(connectedNodeId)?.depth ?? null,
        };
      });
  }, [graphData, nodeLookup, selectedNodeId]);

  const filteredConnections = useMemo(() => {
    if (viewMode === "all" || viewMode === "path") {
      return selectedNodeConnections;
    }

    return selectedNodeConnections.filter((connection) =>
      viewMode === "incoming"
        ? connection.direction === "Incoming"
        : connection.direction === "Outgoing",
    );
  }, [selectedNodeConnections, viewMode]);

  const pathToSelected = useMemo(() => {
    if (!graphData || !selectedNodeId || !graphData.rootNodeId) {
      return { nodeIds: [] as number[], edgeIds: [] as number[] };
    }

    if (selectedNodeId === graphData.rootNodeId) {
      return { nodeIds: [selectedNodeId], edgeIds: [] as number[] };
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
        } else if (
          link.target === current.nodeId &&
          !visited.has(link.source)
        ) {
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
      edgeIds: [] as number[],
    };
  }, [graphData, selectedNodeId]);

  const pathNodes = useMemo(() => {
    return pathToSelected.nodeIds.map((nodeId, index) => ({
      id: nodeId,
      label: nodeLookup.get(nodeId)?.label || `Record ${nodeId}`,
      type: nodeLookup.get(nodeId)?.type || "record",
      isRoot: index === 0,
      isSelected: nodeId === selectedNodeId,
    }));
  }, [nodeLookup, pathToSelected.nodeIds, selectedNodeId]);

  const pathSegments = useMemo(() => {
    if (!graphData || pathToSelected.nodeIds.length <= 1) return [];

    return pathToSelected.edgeIds.map((edgeId, index) => {
      const link = graphData.links.find(
        (candidate) => candidate.edgeId === edgeId,
      );
      const fromNodeId = pathToSelected.nodeIds[index];
      const toNodeId = pathToSelected.nodeIds[index + 1];

      return {
        edgeId,
        fromNodeId,
        toNodeId,
        fromLabel: nodeLookup.get(fromNodeId)?.label || `Record ${fromNodeId}`,
        toLabel: nodeLookup.get(toNodeId)?.label || `Record ${toNodeId}`,
        relationshipName: link?.relationshipName || "Related to",
      };
    });
  }, [graphData, nodeLookup, pathToSelected.edgeIds, pathToSelected.nodeIds]);

  const searchResults = useMemo(() => {
    const query = searchQuery.trim().toLowerCase();
    if (!graphData || !query) return [];

    return graphData.nodes
      .filter((node) => node.label.toLowerCase().includes(query))
      .slice(0, 6);
  }, [graphData, searchQuery]);

  const handleOpenRecord = useCallback(
    (nodeId: number) => {
      router.push(`/record?recordId=${nodeId}&projectId=${projectId}`);
    },
    [projectId, router],
  );

  const handleSelectNode = useCallback((nodeId: number | null) => {
    setSelectedNodeId(nodeId);
  }, []);

  const handleResetView = useCallback(() => {
    const targetNodeId = rootNode?.id ?? recordId;
    setSearchQuery("");
    setSelectedNodeId(targetNodeId);
    controllerRef.current?.resetView();
  }, [recordId, rootNode?.id]);

  const handleSearchSubmit = useCallback(() => {
    const firstResult = searchResults[0];
    if (!firstResult) return;

    setSearchQuery(firstResult.label);
    handleSelectNode(firstResult.id);
  }, [handleSelectNode, searchResults]);

  const handleControlsReady = useCallback(
    (controller: GraphController | null) => {
      controllerRef.current = controller;
    },
    [],
  );

  const stats = useMemo(() => {
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
  }, [graphData, selectedNodeId]);

  return (
    <div className="mt-4 p-4">
      <section className="rounded-2xl bg-base-100 shadow-sm">
        <div className="space-y-4 p-4">
          <section className="overflow-hidden rounded-2xl border border-base-300 bg-base-100 shadow-sm">
            <div className="flex flex-col gap-4 border-b border-base-300 px-4 py-4 lg:flex-row lg:items-center lg:justify-between">
              <div className="relative w-full max-w-xl">
                <label className="input input-bordered flex items-center gap-2 rounded-xl">
                  <MagnifyingGlassIcon className="size-4 text-base-content/50" />
                  <input
                    type="text"
                    className="grow"
                    placeholder="Search nodes by label"
                    value={searchQuery}
                    onChange={(event) => setSearchQuery(event.target.value)}
                    onKeyDown={(event) => {
                      if (event.key === "Enter") {
                        event.preventDefault();
                        handleSearchSubmit();
                      }
                    }}
                  />
                </label>

                {searchQuery.trim() && (
                  <div className="absolute z-20 mt-2 w-full overflow-hidden rounded-xl border border-base-300 bg-base-100 shadow-xl">
                    {searchResults.length > 0 ? (
                      searchResults.map((node) => (
                        <button
                          key={node.id}
                          type="button"
                          className="flex w-full items-center justify-between px-4 py-3 text-left transition hover:bg-base-200"
                          onClick={() => {
                            setSearchQuery(node.label);
                            handleSelectNode(node.id);
                          }}
                        >
                          <span>
                            <span className="block font-medium text-base-content">
                              {node.label}
                            </span>
                            <span className="text-xs text-base-content/60">
                              {node.type} • depth {node.depth}
                            </span>
                          </span>
                          <span className="text-xs text-base-content/50">
                            #{node.id}
                          </span>
                        </button>
                      ))
                    ) : (
                      <div className="px-4 py-3 text-sm text-base-content/60">
                        No nodes match this search.
                      </div>
                    )}
                  </div>
                )}
              </div>

              <div className="flex flex-wrap items-center gap-2">
                <button
                  type="button"
                  className="btn btn-outline btn-sm"
                  onClick={handleResetView}
                >
                  <ArrowPathIcon className="size-4" />
                  Reset
                </button>
                <button
                  type="button"
                  className={`btn btn-sm ${
                    showAllLabels ? "btn-primary" : "btn-outline"
                  }`}
                  onClick={() => setShowAllLabels((current) => !current)}
                >
                  {showAllLabels ? "Hide extra labels" : "Show all labels"}
                </button>
              </div>
            </div>

            {error && (
              <div className="mx-4 mt-4 rounded-xl border border-error/30 bg-error/10 px-4 py-3 text-sm text-error">
                {error}
              </div>
            )}

            <div className="min-h-[720px] overflow-hidden bg-[radial-gradient(circle_at_top,_rgba(14,116,144,0.08),_transparent_45%),linear-gradient(180deg,_rgba(248,250,252,0.82),_rgba(226,232,240,0.45))]">
              <MyGraph
                organizationId={organization?.organizationId as number | null}
                projectId={projectId}
                recordId={recordId}
                depth={depth}
                selectedNodeId={selectedNodeId}
                showAllLabels={showAllLabels}
                viewMode="path"
                pathNodeIds={pathToSelected.nodeIds}
                pathEdgeIds={pathToSelected.edgeIds}
                onLoadingChange={setIsLoading}
                onError={setError}
                onNodeSelect={handleSelectNode}
                onNodeOpen={handleOpenRecord}
                onDataLoaded={setGraphData}
                onControlsReady={handleControlsReady}
              />
            </div>

            <div className="flex flex-col gap-3 border-t border-base-300 px-4 py-4 text-sm text-base-content/70 lg:flex-row lg:items-center lg:justify-between">
              <div className="flex flex-wrap gap-3 text-xs uppercase tracking-wide text-base-content/50">
                <span className="flex items-center gap-2">
                  <span className="size-2.5 rounded-full bg-teal-700" />
                  Root
                </span>
                <span className="flex items-center gap-2">
                  <span className="size-2.5 rounded-full bg-sky-600" />
                  Depth 1
                </span>
                <span className="flex items-center gap-2">
                  <span className="size-2.5 rounded-full bg-amber-600" />
                  Depth 2
                </span>
                <span className="flex items-center gap-2">
                  <span className="size-2.5 rounded-full bg-slate-500" />
                  Depth 3+
                </span>
              </div>
            </div>
          </section>

          <section className="grid gap-4 xl:grid-cols-[320px_minmax(0,1fr)_340px]">
            <aside className="rounded-2xl border border-base-300 bg-base-100 p-5 shadow-sm">
              <div className="flex flex-wrap gap-2 items-center justify-between">
                <p className="text-sm font-medium uppercase tracking-[0.16em] text-base-content/50">
                  Selected Node
                </p>
                <button
                  type="button"
                  className="btn btn-secondary btn-sm"
                  onClick={() => handleOpenRecord(selectedNode.id)}
                >
                  <ArrowTopRightOnSquareIcon className="size-4" />
                </button>
              </div>

              <h3 className="mt-2 text-xl font-semibold text-base-content">
                {selectedNode?.label || "No node selected"}
              </h3>
              <p className="mt-1 text-sm text-base-content/70">
                {selectedNode
                  ? ""
                  : "Choose a node in the graph to populate the summary and relationship table."}
              </p>

              {selectedNode ? (
                <div className="mt-5 space-y-4">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="badge badge-outline">
                      Depth {selectedNode.depth}
                    </span>
                    <span className="badge badge-outline">
                      Record ID: {selectedNode.id}
                    </span>
                  </div>

                  <div className="rounded-2xl border border-base-300 bg-base-100 p-4">
                    {isDetailsLoading ? (
                      <div className="space-y-2">
                        <div className="h-4 w-2/3 animate-pulse rounded bg-base-300" />
                        <div className="h-4 w-full animate-pulse rounded bg-base-300" />
                        <div className="h-4 w-5/6 animate-pulse rounded bg-base-300" />
                      </div>
                    ) : (
                      <div className="space-y-3 text-sm">
                        <div>
                          <p className="text-xs uppercase tracking-wide text-base-content/50">
                            Name
                          </p>
                          <p className="text-base-content">
                            {selectedRecord?.name || selectedNode.label}
                          </p>
                        </div>
                        <div>
                          <p className="text-xs uppercase tracking-wide text-base-content/50">
                            Description
                          </p>
                          <p className="text-base-content/70">
                            {selectedRecord?.description ||
                              "No description available."}
                          </p>
                        </div>
                        <div className="grid grid-cols-2 gap-3">
                          <div>
                            <p className="text-xs uppercase tracking-wide text-base-content/50">
                              URI
                            </p>
                            <p className="truncate text-base-content/70">
                              {selectedRecord?.uri || "Not set"}
                            </p>
                          </div>
                          <div>
                            <p className="text-xs uppercase tracking-wide text-base-content/50">
                              File Type
                            </p>
                            <p className="text-base-content/70">
                              {selectedRecord?.fileType || "Unknown"}
                            </p>
                          </div>
                        </div>
                      </div>
                    )}
                  </div>
                </div>
              ) : (
                <div className="mt-5 rounded-2xl border border-dashed border-base-300 px-4 py-8 text-center text-sm text-base-content/60">
                  No node is selected.
                </div>
              )}
            </aside>

            <div className="rounded-2xl border border-base-300 bg-base-100 shadow-sm">
              <div className="flex flex-col gap-4 border-b border-base-300 px-5 py-4 lg:flex-row lg:items-center lg:justify-between">
                <div>
                  <h3 className="text-lg font-semibold text-base-content">
                    Relationship Table
                  </h3>
                  <p className="text-sm text-base-content/70">
                    {selectedNode
                      ? `Exact connections for ${selectedNode.label}.`
                      : "Select a node to inspect its exact relationships."}
                  </p>
                </div>
              </div>

              <div className="max-h-[520px] overflow-auto">
                <table className="table">
                  <thead className="sticky top-0 bg-base-200">
                    <tr className="text-base-content">
                      <th>Direction</th>
                      <th></th>
                      <th>Connected Record</th>
                      <th>Depth</th>
                      <th>ID</th>
                      <th className="text-right">Action</th>
                    </tr>
                  </thead>
                  <tbody>
                    {filteredConnections.length > 0 ? (
                      filteredConnections.map((connection) => (
                        <tr key={connection.edgeId} className="hover">
                          <td>
                            <span
                              className={`badge ${
                                connection.direction === "Incoming"
                                  ? "badge-error badge-outline"
                                  : "badge-info badge-outline"
                              }`}
                            >
                              {connection.direction}
                            </span>
                          </td>
                          <td className="font-medium text-base-content">
                            {connection.relationshipName}
                          </td>
                          <td>
                            <button
                              type="button"
                              className="link link-hover text-left font-medium text-primary"
                              onClick={() =>
                                handleSelectNode(connection.connectedNodeId)
                              }
                            >
                              {connection.connectedNodeLabel}
                            </button>
                          </td>
                          <td className="text-base-content/70">
                            {connection.connectedNodeDepth ?? "-"}
                          </td>
                          <td className="text-base-content/60">
                            #{connection.connectedNodeId}
                          </td>
                          <td className="text-right">
                            <button
                              type="button"
                              className="btn btn-ghost btn-sm"
                              onClick={() =>
                                handleOpenRecord(connection.connectedNodeId)
                              }
                            >
                              Open
                            </button>
                          </td>
                        </tr>
                      ))
                    ) : (
                      <tr>
                        <td
                          colSpan={6}
                          className="py-10 text-center text-sm text-base-content/60"
                        >
                          {selectedNode
                            ? "No relationships match the current filter."
                            : "Select a node to populate the relationship table."}
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>
            </div>

            <aside className="rounded-2xl border border-base-300 bg-base-100 p-5 shadow-sm">
              <p className="text-sm font-medium uppercase tracking-[0.16em] text-base-content/50">
                Traced Path
              </p>
              <h3 className="mt-2 text-xl font-semibold text-base-content border-b pb-2">
                {selectedNode
                  ? `${Math.max(pathToSelected.nodeIds.length - 1, 0)} hops`
                  : "No trace"}
              </h3>
              <p className="mt-1 text-sm text-base-content/70">
                {selectedNode
                  ? ""
                  : "Select a node to compute and display the traced path from the root."}
              </p>

              {pathNodes.length > 0 ? (
                <div className="mt-5 space-y-4">
                  <div className="flex flex-col items-center">
                    {pathNodes.map((node, index) => (
                      <React.Fragment key={node.id}>
                        <button
                          type="button"
                          className={`max-w-full rounded-full border px-4 py-2 text-center text-sm transition ${
                            node.isSelected
                              ? "border-primary bg-primary text-primary-content"
                              : node.isRoot
                                ? "bg-secondary text-secondary-content"
                                : "border-base-300 bg-base-100 text-base-content hover:bg-base-200"
                          }`}
                          onClick={() => handleSelectNode(node.id)}
                        >
                          <span className="block truncate">{node.label}</span>
                        </button>
                        {index < pathNodes.length - 1 && (
                          <span className="py-2 text-lg leading-none text-base-content/30">
                            ↓
                          </span>
                        )}
                      </React.Fragment>
                    ))}
                  </div>

                  <div className="space-y-2">
                    {pathSegments.length > 0 ? (
                      pathSegments.map((segment, index) => (
                        <div
                          key={segment.edgeId}
                          className="rounded-xl border border-base-300 bg-base-100 px-3 py-3"
                        >
                          <p className="text-xs uppercase tracking-wide text-base-content/50">
                            Hop {index + 1}
                          </p>
                          <p className="mt-1 text-sm font-medium text-base-content">
                            {segment.fromLabel} → {segment.toLabel}
                          </p>
                          <p className="mt-1 text-sm text-base-content/70">
                            {segment.relationshipName}
                          </p>
                        </div>
                      ))
                    ) : (
                      <div className="rounded-xl border border-dashed border-base-300 px-4 py-6 text-center text-sm text-base-content/60">
                        The selected node is the root, so there is no multi-hop
                        trace yet.
                      </div>
                    )}
                  </div>
                </div>
              ) : (
                <div className="mt-5 rounded-2xl border border-dashed border-base-300 px-4 py-8 text-center text-sm text-base-content/60">
                  No path is available for the current selection.
                </div>
              )}
            </aside>
          </section>
        </div>
      </section>
    </div>
  );
};

interface MyGraphProps {
  organizationId: number | null;
  projectId: number;
  recordId: number;
  depth?: number;
  selectedNodeId: number | null;
  showAllLabels: boolean;
  viewMode: GraphViewMode;
  pathNodeIds: number[];
  pathEdgeIds: number[];
  onLoadingChange: (loading: boolean) => void;
  onError: (error: string | null) => void;
  onNodeSelect: (nodeId: number | null) => void;
  onNodeOpen: (nodeId: number) => void;
  onDataLoaded: (data: GraphExplorerData) => void;
  onControlsReady: (controller: GraphController | null) => void;
}

const MyGraph = ({
  organizationId,
  projectId,
  recordId,
  depth = 2,
  selectedNodeId,
  showAllLabels,
  viewMode,
  pathNodeIds,
  pathEdgeIds,
  onLoadingChange,
  onError,
  onNodeSelect,
  onNodeOpen,
  onDataLoaded,
  onControlsReady,
}: MyGraphProps) => {
  const containerRef = useRef<HTMLDivElement>(null);
  const sigmaRef = useRef<SigmaInstance | null>(null);
  const layoutRef = useRef<{ stop?: () => void } | null>(null);
  const graphRef = useRef<Graph<NodeAttributes, EdgeAttributes> | null>(null);
  const timeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const originalLabelsRef = useRef<Map<string, string>>(new Map());
  const originalNodeColorsRef = useRef<Map<string, string>>(new Map());
  const originalNodeSizesRef = useRef<Map<string, number>>(new Map());
  const originalEdgeColorsRef = useRef<Map<string, string>>(new Map());
  const nodeDepthsRef = useRef<Map<number, number>>(new Map());
  const hoverNodeRef = useRef<string | null>(null);
  const selectedNodeIdRef = useRef<number | null>(selectedNodeId);
  const showAllLabelsRef = useRef(showAllLabels);
  const viewModeRef = useRef<GraphViewMode>(viewMode);
  const pathNodeIdsRef = useRef<number[]>(pathNodeIds);
  const pathEdgeIdsRef = useRef<number[]>(pathEdgeIds);
  const initialCameraStateRef = useRef<CameraState | null>(null);
  const [isLayoutSettling, setIsLayoutSettling] = useState(true);

  const focusNode = useCallback((nodeId: number) => {
    const graph = graphRef.current;
    const sigma = sigmaRef.current;
    const camera = sigma?.getCamera?.();

    if (!graph || !camera) return;

    const nodeKey = String(nodeId);
    if (!graph.hasNode(nodeKey)) return;

    const node = graph.getNodeAttributes(nodeKey);
    const currentRatio = camera.getState?.().ratio ?? 1;
    const nextState: CameraState = {
      x: node.x,
      y: node.y,
      ratio: currentRatio,
      angle: 0,
    };

    if (typeof camera.animate === "function") {
      camera.animate(nextState, { duration: 300 });
    } else if (typeof camera.setState === "function") {
      camera.setState(nextState);
    }
  }, []);

  const fitGraph = useCallback(() => {
    const graph = graphRef.current;
    const camera = sigmaRef.current?.getCamera?.();
    if (!graph || !camera) return;

    let minX = Number.POSITIVE_INFINITY;
    let maxX = Number.NEGATIVE_INFINITY;
    let minY = Number.POSITIVE_INFINITY;
    let maxY = Number.NEGATIVE_INFINITY;

    graph.forEachNode((node) => {
      const attributes = graph.getNodeAttributes(node);
      minX = Math.min(minX, attributes.x);
      maxX = Math.max(maxX, attributes.x);
      minY = Math.min(minY, attributes.y);
      maxY = Math.max(maxY, attributes.y);
    });

    if (!Number.isFinite(minX) || !Number.isFinite(minY)) return;

    const spanX = Math.max(maxX - minX, 1);
    const spanY = Math.max(maxY - minY, 1);
    const nextState: CameraState = {
      x: minX + spanX / 2,
      y: minY + spanY / 2,
      ratio: Math.max(spanX, spanY) * 0.1,
      angle: 0,
    };

    if (typeof camera.animate === "function") {
      camera.animate(nextState, { duration: 300 });
    } else if (typeof camera.setState === "function") {
      camera.setState(nextState);
    }
  }, []);

  const resetView = useCallback(() => {
    const camera = sigmaRef.current?.getCamera?.();
    const initialState = initialCameraStateRef.current;
    if (!camera || !initialState) return;

    if (typeof camera.animate === "function") {
      camera.animate(initialState, { duration: 300 });
    } else if (typeof camera.setState === "function") {
      camera.setState(initialState);
    }
  }, []);

  const refreshGraphAppearance = useCallback(() => {
    const graph = graphRef.current;
    const sigma = sigmaRef.current;
    const camera = sigma?.getCamera?.();

    if (!graph || !sigma) return;

    const selectedNodeKey = selectedNodeIdRef.current
      ? String(selectedNodeIdRef.current)
      : null;
    const activeNodeKey = hoverNodeRef.current || selectedNodeKey;
    const connectedNodeKeys = new Set<string>();
    const incomingEdges = new Set<string>();
    const outgoingEdges = new Set<string>();
    const pathNodeKeys = new Set(
      pathNodeIdsRef.current.map((nodeId) => String(nodeId)),
    );
    const pathEdgeIdsSet = new Set(pathEdgeIdsRef.current);
    const pathEdges = new Set<string>();
    const zoomRatio = camera?.getState?.().ratio ?? 1;
    const hasMeaningfulPath = pathNodeKeys.size > 1;

    if (hoverNodeRef.current && graph.hasNode(hoverNodeRef.current)) {
      const hoverNodeKey = hoverNodeRef.current;

      graph.forEachEdge(hoverNodeKey, (edge, _attributes, source, target) => {
        connectedNodeKeys.add(source);
        connectedNodeKeys.add(target);

        if (target === hoverNodeKey) {
          incomingEdges.add(edge);
        } else {
          outgoingEdges.add(edge);
        }
      });
      connectedNodeKeys.delete(hoverNodeKey);
    } else if (viewModeRef.current === "path" && pathNodeKeys.size > 0) {
      graph.forEachEdge((edge, attributes, source, target) => {
        if (pathEdgeIdsSet.has(attributes.edgeId || -1)) {
          pathEdges.add(edge);
          connectedNodeKeys.add(source);
          connectedNodeKeys.add(target);
        }
      });
    } else if (
      activeNodeKey &&
      graph.hasNode(activeNodeKey) &&
      viewModeRef.current !== "all"
    ) {
      graph.forEachEdge(activeNodeKey, (edge, _attributes, source, target) => {
        const isIncoming = target === activeNodeKey;
        const isOutgoing = source === activeNodeKey;

        if (viewModeRef.current === "incoming" && isIncoming) {
          incomingEdges.add(edge);
          connectedNodeKeys.add(source);
          connectedNodeKeys.add(target);
        } else if (viewModeRef.current === "outgoing" && isOutgoing) {
          outgoingEdges.add(edge);
          connectedNodeKeys.add(source);
          connectedNodeKeys.add(target);
        }
      });
      connectedNodeKeys.delete(activeNodeKey);
    } else if (activeNodeKey && graph.hasNode(activeNodeKey)) {
      graph.forEachEdge(activeNodeKey, (edge, _attributes, source, target) => {
        connectedNodeKeys.add(source);
        connectedNodeKeys.add(target);

        if (target === activeNodeKey) {
          incomingEdges.add(edge);
        } else {
          outgoingEdges.add(edge);
        }
      });
      connectedNodeKeys.delete(activeNodeKey);
    }

    graph.forEachNode((node) => {
      const nodeId = Number(node);
      const depthValue = nodeDepthsRef.current.get(nodeId) || 0;
      const originalLabel = originalLabelsRef.current.get(node) || "";
      const originalColor =
        originalNodeColorsRef.current.get(node) || "#64748b";
      const originalSize = originalNodeSizesRef.current.get(node) || 14;
      const isSelected = node === selectedNodeKey;
      const isHovered = node === hoverNodeRef.current;
      const isConnected = connectedNodeKeys.has(node);
      const isPathNode = pathNodeKeys.has(node);
      const shouldDim =
        viewModeRef.current === "path"
          ? hasMeaningfulPath && !isSelected && !isHovered && !isPathNode
          : Boolean(activeNodeKey) && !isSelected && !isHovered && !isConnected;

      const nextLabel =
        showAllLabelsRef.current ||
        zoomRatio < 0.8 ||
        depthValue <= 1 ||
        isSelected ||
        isHovered ||
        isConnected ||
        isPathNode
          ? originalLabel
          : "";

      let nextColor = originalColor;
      if (isSelected) {
        nextColor = "#115e59";
      } else if (isHovered) {
        nextColor = "#0369a1";
      } else if (
        viewModeRef.current === "path" &&
        hasMeaningfulPath &&
        isPathNode
      ) {
        nextColor = "#f97316";
      } else if (shouldDim) {
        nextColor = "#cbd5e1";
      }

      let nextSize = originalSize;
      if (isSelected) {
        nextSize = originalSize * 1.65;
      } else if (isHovered) {
        nextSize = originalSize * 1.45;
      } else if (
        viewModeRef.current === "path" &&
        hasMeaningfulPath &&
        isPathNode
      ) {
        nextSize = originalSize * 1.2;
      } else if (isConnected) {
        nextSize = originalSize * 1.15;
      } else if (shouldDim) {
        nextSize = Math.max(originalSize * 0.9, 10);
      }

      graph.setNodeAttribute(node, "label", nextLabel);
      graph.setNodeAttribute(node, "color", nextColor);
      graph.setNodeAttribute(node, "size", nextSize);
    });

    graph.forEachEdge((edge) => {
      if (viewModeRef.current === "path" && pathEdges.has(edge)) {
        graph.setEdgeAttribute(edge, "color", "#f97316");
        graph.setEdgeAttribute(edge, "size", 4);
      } else if (incomingEdges.has(edge)) {
        graph.setEdgeAttribute(edge, "color", "#ef4444");
        graph.setEdgeAttribute(edge, "size", 4);
      } else if (outgoingEdges.has(edge)) {
        graph.setEdgeAttribute(edge, "color", "#2563eb");
        graph.setEdgeAttribute(edge, "size", 4);
      } else if (
        (viewModeRef.current === "path" && hasMeaningfulPath) ||
        activeNodeKey
      ) {
        graph.setEdgeAttribute(edge, "color", "#dbe4ee");
        graph.setEdgeAttribute(edge, "size", 1);
      } else {
        graph.setEdgeAttribute(
          edge,
          "color",
          originalEdgeColorsRef.current.get(edge) || "#94a3b8",
        );
        graph.setEdgeAttribute(edge, "size", 2);
      }
    });

    sigma.refresh?.();
  }, []);

  useEffect(() => {
    selectedNodeIdRef.current = selectedNodeId;
    refreshGraphAppearance();
  }, [refreshGraphAppearance, selectedNodeId]);

  useEffect(() => {
    showAllLabelsRef.current = showAllLabels;
    refreshGraphAppearance();
  }, [refreshGraphAppearance, showAllLabels]);

  useEffect(() => {
    viewModeRef.current = viewMode;
    pathNodeIdsRef.current = pathNodeIds;
    pathEdgeIdsRef.current = pathEdgeIds;
    refreshGraphAppearance();
  }, [pathEdgeIds, pathNodeIds, refreshGraphAppearance, viewMode]);

  useEffect(() => {
    if (!containerRef.current || !organizationId) return;

    const fetchAndRenderGraph = async () => {
      try {
        onLoadingChange(true);
        onError(null);

        const [{ default: Sigma }, { default: FA2Layout }] = await Promise.all([
          import("sigma"),
          import("graphology-layout-forceatlas2/worker"),
        ]);

        const data = await getGraphDataForRecord(
          organizationId,
          projectId,
          recordId,
          depth,
        );

        if (!data.nodes || data.nodes.length === 0) {
          onError("No nodes found in the graph data.");
          onLoadingChange(false);
          return;
        }

        const graph = new Graph<NodeAttributes, EdgeAttributes>();
        const nodeDepths = new Map<number, number>();
        const rootNode = data.nodes.find((node) => node.type === "root");
        const links: GraphLinkSummary[] = (data.links || []).map((link) => ({
          source: link.source,
          target: link.target,
          relationshipId: link.relationshipId ?? null,
          relationshipName: link.relationshipName ?? null,
          edgeId: link.edgeId,
        }));

        if (rootNode) {
          const queue: Array<{ id: number; depth: number }> = [
            { id: rootNode.id, depth: 0 },
          ];
          const visited = new Set<number>([rootNode.id]);
          nodeDepths.set(rootNode.id, 0);

          while (queue.length > 0) {
            const current = queue.shift();
            if (!current) continue;

            links.forEach((link) => {
              let nextNodeId: number | null = null;

              if (link.source === current.id && !visited.has(link.target)) {
                nextNodeId = link.target;
              } else if (
                link.target === current.id &&
                !visited.has(link.source)
              ) {
                nextNodeId = link.source;
              }

              if (nextNodeId !== null) {
                visited.add(nextNodeId);
                nodeDepths.set(nextNodeId, current.depth + 1);
                queue.push({ id: nextNodeId, depth: current.depth + 1 });
              }
            });
          }
        }

        const getColorByDepth = (nodeId: number): string => {
          const nodeDepth = nodeDepths.get(nodeId) || 0;
          const colors = ["#0f766e", "#0284c7", "#d97706", "#64748b"];
          return colors[Math.min(nodeDepth, colors.length - 1)];
        };

        const nodes = data.nodes;

        if (!nodes || nodes.length === 0) {
          onError("No nodes found in the graph data.");
          onLoadingChange(false);
          return;
        }

        nodes.forEach((node, index) => {
          const nodeId = String(node.id);
          const angle = (index / nodes.length) * Math.PI * 2;
          const radius =
            node.type === "root" ? 0 : 3 + (nodeDepths.get(node.id) || 0) * 2;

          graph.addNode(nodeId, {
            x: Math.cos(angle) * radius,
            y: Math.sin(angle) * radius,
            size: node.type === "root" ? 24 : 14,
            label: node.label,
            color: getColorByDepth(node.id),
            nodeType: node.type,
          });
        });

        links.forEach((link) => {
          const sourceId = String(link.source);
          const targetId = String(link.target);

          if (graph.hasNode(sourceId) && graph.hasNode(targetId)) {
            graph.addEdge(sourceId, targetId, {
              size: 2,
              color: "#94a3b8",
              label: link.relationshipName || undefined,
              relationshipId: link.relationshipId,
              edgeId: link.edgeId,
            });
          }
        });

        if (sigmaRef.current) {
          sigmaRef.current.kill?.();
        }

        if (layoutRef.current) {
          layoutRef.current.stop?.();
        }

        if (timeoutRef.current) {
          clearTimeout(timeoutRef.current);
        }

        const graphContainer = containerRef.current;
        if (!graphContainer) {
          onLoadingChange(false);
          return;
        }

        const sigma = new Sigma(graph, graphContainer, {
          renderEdgeLabels: false,
          defaultEdgeType: "arrow",
          labelSize: 14,
          labelWeight: "500",
          allowInvalidContainer: true,
        }) as SigmaInstance;

        sigmaRef.current = sigma;
        graphRef.current = graph;
        nodeDepthsRef.current = nodeDepths;
        originalLabelsRef.current = new Map();
        originalNodeColorsRef.current = new Map();
        originalNodeSizesRef.current = new Map();
        originalEdgeColorsRef.current = new Map();
        hoverNodeRef.current = null;

        graph.forEachNode((node) => {
          originalLabelsRef.current.set(
            node,
            graph.getNodeAttribute(node, "label"),
          );
          originalNodeColorsRef.current.set(
            node,
            graph.getNodeAttribute(node, "color"),
          );
          originalNodeSizesRef.current.set(
            node,
            graph.getNodeAttribute(node, "size"),
          );
        });

        graph.forEachEdge((edge, attributes) => {
          originalEdgeColorsRef.current.set(
            edge,
            attributes.color || "#94a3b8",
          );
        });

        const camera = sigma.getCamera?.();
        camera?.on?.("updated", refreshGraphAppearance);

        const layout = new FA2Layout(graph, {
          settings: {
            barnesHutOptimize: true,
            strongGravityMode: true,
            gravity: 0.45,
            scalingRatio: 8,
            slowDown: 2,
          },
        });

        layoutRef.current = layout;
        layout.start();
        setIsLayoutSettling(true);

        timeoutRef.current = setTimeout(() => {
          layout.stop?.();
          initialCameraStateRef.current = camera?.getState?.() ?? null;
          setIsLayoutSettling(false);
          refreshGraphAppearance();
          onLoadingChange(false);
        }, 2500);

        sigma.on?.("enterNode", ({ node }) => {
          hoverNodeRef.current = node;
          if (containerRef.current) {
            containerRef.current.style.cursor = "pointer";
          }
          refreshGraphAppearance();
        });

        sigma.on?.("leaveNode", () => {
          hoverNodeRef.current = null;
          if (containerRef.current) {
            containerRef.current.style.cursor = "grab";
          }
          refreshGraphAppearance();
        });

        sigma.on?.("clickNode", ({ node }) => {
          const nodeId = Number(node);

          if (selectedNodeIdRef.current === nodeId) {
            onNodeOpen(nodeId);
            return;
          }

          onNodeSelect(nodeId);
        });

        sigma.on?.("clickStage", () => {
          hoverNodeRef.current = null;
          onNodeSelect(null);
        });

        onDataLoaded({
          nodes: data.nodes.map((node) => ({
            ...node,
            depth: nodeDepths.get(node.id) || 0,
          })),
          links,
          rootNodeId: rootNode?.id ?? data.nodes[0]?.id ?? null,
        });

        onControlsReady({
          fitGraph,
          focusNode,
          resetView,
        });

        refreshGraphAppearance();
      } catch (err) {
        onError(
          err instanceof Error ? err.message : "Failed to load graph data.",
        );
        onLoadingChange(false);
      }
    };

    fetchAndRenderGraph();

    return () => {
      if (timeoutRef.current) {
        clearTimeout(timeoutRef.current);
      }
      layoutRef.current?.stop?.();
      sigmaRef.current?.kill?.();
      graphRef.current = null;
      onControlsReady(null);
    };
  }, [
    depth,
    fitGraph,
    focusNode,
    resetView,
    onControlsReady,
    onDataLoaded,
    onError,
    onLoadingChange,
    onNodeOpen,
    onNodeSelect,
    organizationId,
    projectId,
    recordId,
    refreshGraphAppearance,
  ]);

  return (
    <div className="relative h-full min-h-[720px] w-full">
      {isLayoutSettling && (
        <div className="absolute inset-0 z-10 flex flex-col items-center justify-center gap-3 bg-base-100/70 backdrop-blur-sm">
          <span className="loading loading-spinner loading-lg text-info" />
          <span className="text-sm text-base-content/70">
            Calculating graph layout...
          </span>
        </div>
      )}

      <div
        ref={containerRef}
        className="h-full min-h-[720px] w-full transition-opacity duration-300"
        style={{ opacity: isLayoutSettling ? 0.15 : 1, cursor: "grab" }}
      />
    </div>
  );
};

export default GraphClientPage;
