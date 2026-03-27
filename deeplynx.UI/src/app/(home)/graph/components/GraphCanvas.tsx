"use client";

import React, { useCallback, useEffect, useRef, useState } from "react";
import Graph from "graphology";
import { useLanguage } from "@/app/contexts/Language";
import {
  CameraState,
  EdgeAttributes,
  GraphController,
  GraphExplorerData,
  GraphLinkSummary,
  GraphViewMode,
  NodeAttributes,
  SigmaInstance,
} from "./graphTypes";
import { getGraphDataForRecord } from "@/app/lib/client_service/record_services.client";
import { buildClassColorMap, getSizeForDepth } from "./graphStyle";

interface GraphCanvasProps {
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

const GraphCanvas = ({
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
}: GraphCanvasProps) => {
  const { t } = useLanguage();

  // Sigma instance state lives in refs so camera, layout, and animation logic
  // can update without forcing React rerenders.
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
  const flowCanvasRef = useRef<HTMLCanvasElement>(null);
  const flowFrameRef = useRef<number | null>(null);
  const draggedNodeRef = useRef<string | null>(null);
  const dragOffsetRef = useRef({ x: 0, y: 0 });
  const hasDraggedRef = useRef(false);
  const [isLayoutSettling, setIsLayoutSettling] = useState(true);

  // Camera helpers exposed back to the page so toolbar actions can fit, focus,
  // and reset without knowing about Sigma internals.
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

  const stopDragging = useCallback(() => {
    draggedNodeRef.current = null;
    sigmaRef.current?.setSetting?.("enableCameraPanning", true);

    window.setTimeout(() => {
      hasDraggedRef.current = false;
    }, 0);
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

  // Draw a lightweight canvas overlay for animated edge flow without needing a
  // custom Sigma edge program for the motion effect itself.
  const drawEdgeFlow = useCallback(
    (timestamp: number) => {
      const canvas = flowCanvasRef.current;
      const container = containerRef.current;
      const graph = graphRef.current;
      const sigma = sigmaRef.current;

      if (!canvas || !container || !graph || !sigma?.graphToViewport) return;

      const graphToViewport = (coordinates: { x: number; y: number }) =>
        sigma.graphToViewport!(coordinates);

      const context = canvas.getContext("2d");
      if (!context) return;

      const width = container.clientWidth;
      const height = container.clientHeight;
      const pixelRatio = window.devicePixelRatio || 1;

      if (
        canvas.width !== width * pixelRatio ||
        canvas.height !== height * pixelRatio
      ) {
        canvas.width = width * pixelRatio;
        canvas.height = height * pixelRatio;
        canvas.style.width = `${width}px`;
        canvas.style.height = `${height}px`;
      }

      context.setTransform(pixelRatio, 0, 0, pixelRatio, 0, 0);
      context.clearRect(0, 0, width, height);

      if (isLayoutSettling) return;

      const speed = 0.55;
      const progress = ((timestamp / 1000) * speed) % 1;

      const drawFlow = (
        fromKey: string,
        toKey: string,
        color: string,
        offset = 0,
      ) => {
        if (!graph.hasNode(fromKey) || !graph.hasNode(toKey)) return;

        const fromNode = graph.getNodeAttributes(fromKey);
        const toNode = graph.getNodeAttributes(toKey);

        const from = graphToViewport({ x: fromNode.x, y: fromNode.y });
        const to = graphToViewport({ x: toNode.x, y: toNode.y });

        const dx = to.x - from.x;
        const dy = to.y - from.y;
        const angle = Math.atan2(dy, dx);

        for (let trailIndex = 0; trailIndex < 3; trailIndex += 1) {
          const trailProgress = (progress - trailIndex * 0.12 + offset + 1) % 1;
          const x = from.x + dx * trailProgress;
          const y = from.y + dy * trailProgress;
          const alpha = 1 - trailIndex * 0.28;
          const size = trailIndex === 0 ? 8 : 6;

          context.save();
          context.translate(x, y);
          context.rotate(angle);
          context.fillStyle = color
            .replace("rgb(", "rgba(")
            .replace(")", `, ${alpha})`);
          context.beginPath();
          context.moveTo(size, 0);
          context.lineTo(-size * 0.6, size * 0.45);
          context.lineTo(-size * 0.6, -size * 0.45);
          context.closePath();
          context.fill();
          context.restore();
        }
      };

      const hoverNodeKey = hoverNodeRef.current;

      if (hoverNodeKey && graph.hasNode(hoverNodeKey)) {
        graph.forEachEdge(hoverNodeKey, (_edge, attributes, source, target) => {
          if (attributes.direction === "bidirectional") {
            drawFlow(source, target, "rgb(168, 85, 247)", 0);
            drawFlow(target, source, "rgb(168, 85, 247)", 0.5);
          } else if (target === hoverNodeKey) {
            drawFlow(source, target, "rgb(239, 68, 68)");
          } else {
            drawFlow(source, target, "rgb(37, 99, 235)");
          }
        });

        return;
      }

      if (pathNodeIdsRef.current.length < 2 || !selectedNodeIdRef.current) {
        return;
      }

      for (
        let index = 0;
        index < pathNodeIdsRef.current.length - 1;
        index += 1
      ) {
        const fromNodeKey = String(pathNodeIdsRef.current[index]);
        const toNodeKey = String(pathNodeIdsRef.current[index + 1]);

        drawFlow(fromNodeKey, toNodeKey, "rgb(249, 115, 22)");
      }
    },
    [isLayoutSettling],
  );

  // Centralize node and edge emphasis rules so hover, selection, and traced
  // path states all update through one styling pass.
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
    const bidirectionalEdges = new Set<string>();
    const pathNodeKeys = new Set(
      pathNodeIdsRef.current.map((nodeId) => String(nodeId)),
    );
    const pathEdgeIdsSet = new Set(pathEdgeIdsRef.current);
    const pathEdges = new Set<string>();
    const zoomRatio = camera?.getState?.().ratio ?? 1;
    const hasMeaningfulPath = pathNodeKeys.size > 1;

    if (hoverNodeRef.current && graph.hasNode(hoverNodeRef.current)) {
      const hoverNodeKey = hoverNodeRef.current;

      graph.forEachEdge(hoverNodeKey, (edge, attributes, source, target) => {
        connectedNodeKeys.add(source);
        connectedNodeKeys.add(target);

        if (attributes.direction === "bidirectional") {
          bidirectionalEdges.add(edge);
        } else if (target === hoverNodeKey) {
          incomingEdges.add(edge);
        } else {
          outgoingEdges.add(edge);
        }
      });
      connectedNodeKeys.delete(hoverNodeKey);
    } else if (viewModeRef.current === "path" && hasMeaningfulPath) {
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
      graph.forEachEdge(activeNodeKey, (edge, attributes, source, target) => {
        connectedNodeKeys.add(source);
        connectedNodeKeys.add(target);

        if (attributes.direction === "bidirectional") {
          bidirectionalEdges.add(edge);
        } else if (target === activeNodeKey) {
          incomingEdges.add(edge);
        } else {
          outgoingEdges.add(edge);
        }
      });
      connectedNodeKeys.delete(activeNodeKey);
    } else if (activeNodeKey && graph.hasNode(activeNodeKey)) {
      graph.forEachEdge(activeNodeKey, (edge, attributes, source, target) => {
        connectedNodeKeys.add(source);
        connectedNodeKeys.add(target);

        if (attributes.direction === "bidirectional") {
          bidirectionalEdges.add(edge);
        } else if (target === activeNodeKey) {
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

      const nextColor = originalColor;

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
      if (pathEdges.has(edge)) {
        graph.setEdgeAttribute(edge, "color", "#f97316");
        graph.setEdgeAttribute(edge, "size", 4);
      } else if (bidirectionalEdges.has(edge)) {
        graph.setEdgeAttribute(edge, "color", "#a855f7");
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

  // Keep Sigma-facing refs synchronized with the latest React state.
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

  // Build the graph, run the initial layout pass, and wire Sigma events for
  // hover, selection, dragging, and camera controls.
  useEffect(() => {
    if (!containerRef.current || !organizationId) return;

    const fetchAndRenderGraph = async () => {
      try {
        onLoadingChange(true);
        onError(null);

        const [
          { default: Sigma },
          {
            EdgeClampedProgram,
            EdgeDoubleClampedProgram,
            createEdgeArrowHeadProgram,
            createEdgeCompoundProgram,
          },
          { default: FA2Layout },
        ] = await Promise.all([
          import("sigma"),
          import("sigma/rendering"),
          import("graphology-layout-forceatlas2/worker"),
        ]);

        const data = await getGraphDataForRecord(
          organizationId,
          projectId,
          recordId,
          depth,
        );

        if (!data.nodes || data.nodes.length === 0) {
          onError(t.translations.GRAPH_NO_NODES_FOUND);
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

        const nodes = data.nodes;
        if (nodes.length === 0) {
          onError(t.translations.GRAPH_NO_NODES_FOUND);
          onLoadingChange(false);
          return;
        }

        const classColorMap = buildClassColorMap(nodes);

        nodes.forEach((node, index) => {
          const nodeId = String(node.id);
          const angle = (index / nodes.length) * Math.PI * 2;
          const nodeDepth = nodeDepths.get(node.id) || 0;
          const radius = node.type === "root" ? 0 : 6;
          const classKey = String(node.classId ?? node.className ?? "unknown");
          const displayLabel =
            node.type === "root" ? `ROOT: ${node.label}` : node.label;

          graph.addNode(nodeId, {
            x: Math.cos(angle) * radius,
            y: Math.sin(angle) * radius,
            size: getSizeForDepth(nodeDepth),
            label: displayLabel,
            color: classColorMap.get(classKey) || "#64748b",
            nodeType: node.type,
            classId: node.classId,
            className: node.className,
          });
        });

        const edgePairs = new Map<string, GraphLinkSummary[]>();

        links.forEach((link) => {
          const pairKey =
            link.source < link.target
              ? `${link.source}:${link.target}`
              : `${link.target}:${link.source}`;

          const existing = edgePairs.get(pairKey) || [];
          existing.push(link);
          edgePairs.set(pairKey, existing);
        });

        edgePairs.forEach((pairLinks) => {
          const first = pairLinks[0];
          if (!first) return;

          const forwardLinks = pairLinks.filter(
            (link) =>
              link.source === first.source && link.target === first.target,
          );
          const reverseLinks = pairLinks.filter(
            (link) =>
              link.source === first.target && link.target === first.source,
          );
          const aggregatedLabels = pairLinks
            .map((link) => link.relationshipName)
            .filter((relationshipName): relationshipName is string =>
              Boolean(relationshipName),
            );

          const sourceId = String(first.source);
          const targetId = String(first.target);

          if (!graph.hasNode(sourceId) || !graph.hasNode(targetId)) return;

          const hasBidirectionalLink =
            forwardLinks.length > 0 && reverseLinks.length > 0;

          graph.addEdge(sourceId, targetId, {
            size: 2,
            color: "#94a3b8",
            label: hasBidirectionalLink
              ? aggregatedLabels.join(" / ") || "Bidirectional"
              : aggregatedLabels.join(" / ") ||
                first.relationshipName ||
                undefined,
            relationshipId: first.relationshipId,
            edgeId: first.edgeId,
            type: hasBidirectionalLink
              ? "double-clamped-arrow"
              : "clamped-arrow",
            direction: hasBidirectionalLink ? "bidirectional" : "outgoing",
          });
        });

        sigmaRef.current?.kill?.();
        layoutRef.current?.stop?.();

        if (timeoutRef.current) {
          clearTimeout(timeoutRef.current);
        }

        const graphContainer = containerRef.current;
        if (!graphContainer) {
          onLoadingChange(false);
          return;
        }

        const ClampedArrowProgram = createEdgeCompoundProgram([
          EdgeClampedProgram,
          createEdgeArrowHeadProgram({
            extremity: "target",
            lengthToThicknessRatio: 4,
            widenessToThicknessRatio: 4,
          }),
        ]);

        const DoubleClampedArrowProgram = createEdgeCompoundProgram([
          EdgeDoubleClampedProgram,
          createEdgeArrowHeadProgram({
            extremity: "source",
            lengthToThicknessRatio: 4,
            widenessToThicknessRatio: 4,
          }),
          createEdgeArrowHeadProgram({
            extremity: "target",
            lengthToThicknessRatio: 4,
            widenessToThicknessRatio: 4,
          }),
        ]);

        const sigma = new Sigma(graph, graphContainer, {
          renderEdgeLabels: false,
          defaultEdgeType: "clamped-arrow",
          edgeProgramClasses: {
            "clamped-arrow": ClampedArrowProgram,
            "double-clamped-arrow": DoubleClampedArrowProgram,
          },
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

          if (hasDraggedRef.current) return;

          if (selectedNodeIdRef.current === nodeId) {
            onNodeOpen(nodeId);
            return;
          }

          hoverNodeRef.current = null;
          onNodeSelect(nodeId);
        });

        sigma.on?.("clickStage", () => {
          if (hasDraggedRef.current) return;

          hoverNodeRef.current = null;
          onNodeSelect(null);
        });

        sigma.on?.("downNode", ({ node, event }) => {
          const graphPoint = sigma.viewportToGraph?.({
            x: event.x,
            y: event.y,
          });
          if (!graphPoint || !graph.hasNode(node)) return;

          const nodeAttributes = graph.getNodeAttributes(node);

          draggedNodeRef.current = node;
          dragOffsetRef.current = {
            x: nodeAttributes.x - graphPoint.x,
            y: nodeAttributes.y - graphPoint.y,
          };
          hasDraggedRef.current = false;

          sigma.setSetting?.("enableCameraPanning", false);
          event.preventSigmaDefault();
        });

        sigma.on?.("moveBody", ({ event }) => {
          const draggedNode = draggedNodeRef.current;
          const graphPoint = sigma.viewportToGraph?.({
            x: event.x,
            y: event.y,
          });

          if (!draggedNode || !graphPoint || !graph.hasNode(draggedNode)) {
            return;
          }

          hasDraggedRef.current = true;

          graph.setNodeAttribute(
            draggedNode,
            "x",
            graphPoint.x + dragOffsetRef.current.x,
          );
          graph.setNodeAttribute(
            draggedNode,
            "y",
            graphPoint.y + dragOffsetRef.current.y,
          );

          sigma.refresh?.();
          event.preventSigmaDefault();
        });

        sigma.on?.("upNode", stopDragging);
        sigma.on?.("upStage", stopDragging);

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
          err instanceof Error
            ? err.message
            : t.translations.GRAPH_FAILED_TO_LOAD,
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
    stopDragging,
  ]);

  // Keep the edge-flow overlay moving independently from Sigma's WebGL render
  // loop.
  useEffect(() => {
    const animate = (timestamp: number) => {
      drawEdgeFlow(timestamp);
      flowFrameRef.current = window.requestAnimationFrame(animate);
    };

    flowFrameRef.current = window.requestAnimationFrame(animate);

    return () => {
      if (flowFrameRef.current !== null) {
        window.cancelAnimationFrame(flowFrameRef.current);
      }
    };
  }, [drawEdgeFlow]);

  return (
    <div className="relative h-full min-h-[720px] w-full">
      {isLayoutSettling && (
        <div className="absolute inset-0 z-10 flex flex-col items-center justify-center gap-3 bg-base-100/70 backdrop-blur-sm">
          <span className="loading loading-spinner loading-lg text-info" />
          <span className="text-sm text-base-content/70">
            {t.translations.GRAPH_CALCULATING_LAYOUT}
          </span>
        </div>
      )}

      {/* Canvas-only animation layer for directional flow indicators */}
      <canvas
        ref={flowCanvasRef}
        className="pointer-events-none absolute inset-0 z-[1] h-full w-full"
      />

      {/* Sigma mounts into this container */}
      <div
        ref={containerRef}
        className="h-full min-h-[720px] w-full transition-opacity duration-300"
        style={{ opacity: isLayoutSettling ? 0.15 : 1, cursor: "grab" }}
      />
    </div>
  );
};

export default GraphCanvas;
