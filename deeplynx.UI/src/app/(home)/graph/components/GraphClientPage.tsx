"use client";

import React, { useEffect, useRef, useState } from "react";
import Graph from "graphology";
// Remove this line: import Sigma from "sigma";
import { Attributes } from "graphology-types";
import { GraphResponseDto } from "@/app/(home)/types/responseDTOs";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import { getGraphDataForRecord } from "@/app/lib/client_service/record_services.client";
// Remove this line: import FA2Layout from "graphology-layout-forceatlas2/worker";

// ============================================
// TYPE DEFINITIONS
// ============================================
/**
 * Sigma.js node attributes
 * These properties control how nodes appear and are positioned
 */
interface NodeAttributes extends Attributes {
  x: number; // X coordinate position
  y: number; // Y coordinate position
  size: number; // Visual size of the node
  label: string; // Text label displayed on/near the node
  color: string; // Node color (hex format)
  nodeType: string; // Node type from backend (renamed from 'type' to avoid Sigma.js conflict)
}

/**
 * Sigma.js edge attributes
 */
interface EdgeAttributes extends Attributes {
  size?: number; // Optional: thickness of the edge line
  color?: string; // Optional: color of the edge line
  label?: string; // Edge label (relationship name)
  relationshipId?: number | null;
  edgeId?: number;
}

// ============================================
// MAIN COMPONENT
// ============================================

interface GraphClientPageProps {
  projectId: number;
  recordId: number;
  depth?: number;
}

const GraphClientPage = ({
  projectId,
  recordId,
  depth = 3,
}: GraphClientPageProps) => {
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  return (
    <div style={{ height: "800px", width: "80%", position: "relative" }}>
      {/* Error state */}
      {error && (
        <div
          style={{
            position: "absolute",
            top: "50%",
            left: "50%",
            transform: "translate(-50%, -50%)",
            color: "red",
          }}
        >
          Error: {error}
        </div>
      )}

      {/* Graph container */}
      <MyGraph
        projectId={projectId}
        recordId={recordId}
        depth={depth}
        onLoadingChange={setIsLoading}
        onError={setError}
      />
    </div>
  );
};

// ============================================
// GRAPH COMPONENT
// ============================================

interface MyGraphProps {
  projectId: number;
  recordId: number; // Required
  depth?: number;
  onLoadingChange: (loading: boolean) => void;
  onError: (error: string | null) => void;
}

const MyGraph = ({
  projectId,
  recordId,
  depth = 2,
  onLoadingChange,
  onError,
}: MyGraphProps) => {
  // Reference to the DOM element that will hold the graph
  const containerRef = useRef<HTMLDivElement>(null);

  // Reference to the Sigma instance for cleanup (using any to avoid complex types)
  const sigmaRef = useRef<unknown>(null);

  // Reference to the layout for cleanup
  const layoutRef = useRef<unknown>(null);

  const { organization, hasLoaded } = useOrganizationSession();

  // Add state to track if layout is settling
  const [isLayoutSettling, setIsLayoutSettling] = useState(true);

  useEffect(() => {
    // Safety check: ensure the container element exists
    if (!containerRef.current) return;

    // ========================================
    // FETCH AND RENDER GRAPH DATA
    // ========================================

    const fetchAndRenderGraph = async () => {
      try {
        onLoadingChange(true);
        onError(null);

        // Dynamically import Sigma and FA2Layout (client-side only)
        const [{ default: Sigma }, { default: FA2Layout }] = await Promise.all([
          import("sigma"),
          import("graphology-layout-forceatlas2/worker"),
        ]);

        // Fetch graph data from backend using the service
        const data = await getGraphDataForRecord(
          organization!.organizationId as number,
          projectId,
          recordId,
          depth
        );

        // Validate that we have data
        if (!data.nodes || data.nodes.length === 0) {
          onError("No nodes found in the graph data");
          onLoadingChange(false);
          return;
        }

        // ========================================
        // CREATE AND POPULATE GRAPH
        // ========================================

        // Initialize a new graph instance
        const graph = new Graph<NodeAttributes, EdgeAttributes>();

        // Calculate depth for each node (distance from root)
        const nodeDepths = new Map<number, number>();
        const rootNode = data.nodes.find((node) => node.type === "root");

        // BFS to calculate depths from root
        if (rootNode) {
          const queue: Array<{ id: number; depth: number }> = [
            { id: rootNode.id, depth: 0 },
          ];
          const visited = new Set<number>();

          nodeDepths.set(rootNode.id, 0);
          visited.add(rootNode.id);

          while (queue.length > 0) {
            const current = queue.shift();
            if (!current) continue;

            // Find all connected nodes
            data.links!.forEach((link) => {
              let nextNodeId: number | null = null;

              // Check if current node is source or target
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

        // Generate color based on depth from root
        const getColorByDepth = (nodeId: number): string => {
          const depth = nodeDepths.get(nodeId) || 0;
          const colors = [
            "#9b59b6", // Depth 0 (root) - Purple
            "#3498db", // Depth 1 - Blue
            "#f39c12", // Depth 2 - Orange
            "#95a5a6", // Depth 3+ - Gray
          ];
          return colors[Math.min(depth, colors.length - 1)];
        };

        // Add all nodes with random initial positions
        // The force-directed algorithm will arrange them
        data.nodes.forEach((node) => {
          const nodeId = String(node.id);

          // Start with random positions
          const x = Math.random() * 10 - 5;
          const y = Math.random() * 10 - 5;

          graph.addNode(nodeId, {
            x,
            y,
            size: node.type === "root" ? 25 : 15, // Root node is larger
            label: node.label,
            color: getColorByDepth(node.id),
            nodeType: node.type,
          });
        });

        // Add all edges (links) to the graph
        data.links!.forEach((link, index) => {
          // Convert numeric IDs to strings
          const sourceId = String(link.source);
          const targetId = String(link.target);

          // Check if both nodes exist before creating edge
          if (graph.hasNode(sourceId) && graph.hasNode(targetId)) {
            // Use edgeId, or create a unique ID
            const edgeId = link.edgeId ? String(link.edgeId) : `edge-${index}`;

            graph.addEdge(sourceId, targetId, {
              size: 2,
              color: "#999",
              label: link.relationshipName || undefined,
              relationshipId: link.relationshipId,
              edgeId: link.edgeId,
            });
          }
        });

        // ========================================
        // INITIALIZE SIGMA RENDERER
        // ========================================

        // Destroy existing Sigma instance if it exists
        if (sigmaRef.current) {
          const instance = sigmaRef.current as { kill?: () => void };
          if (typeof instance.kill === "function") {
            instance.kill();
          }
        }

        // Stop existing layout if running
        if (layoutRef.current) {
          const instance = layoutRef.current as { stop?: () => void };
          if (typeof instance.stop === "function") {
            instance.stop();
          }
        }

        // Create new Sigma instance to render the graph
        if (containerRef.current) {
          const sigma = new Sigma(graph, containerRef.current, {
            renderEdgeLabels: false,
            defaultEdgeType: "arrow",
            labelSize: 14,
            labelWeight: "normal",
          });

          sigmaRef.current = sigma;
          // ========================================
          // ADD ZOOM-BASED LABEL VISIBILITY
          // ========================================

          const camera = sigma.getCamera();

          // Store original labels before modifying
          const originalLabels = new Map<string, string>();
          graph.forEachNode((node) => {
            originalLabels.set(node, graph.getNodeAttribute(node, "label"));
          });

          // Function to update label visibility based on zoom and depth
          const updateLabelVisibility = () => {
            const zoom = camera.ratio; // Lower ratio = more zoomed in

            graph.forEachNode((node) => {
              const nodeId = parseInt(node);
              const depth = nodeDepths.get(nodeId) || 0;
              const originalLabel = originalLabels.get(node) || "";

              if (zoom < 0.8) {
                // Zoomed in - show all labels
                graph.setNodeAttribute(node, "label", originalLabel);
              } else {
                // Default/zoomed out view - show only root (depth 0) and depth 1
                if (depth === 0 || depth === 1) {
                  graph.setNodeAttribute(node, "label", originalLabel);
                } else {
                  graph.setNodeAttribute(node, "label", "");
                }
              }
            });

            sigma.refresh();
          };

          // Initial label visibility based on starting zoom
          updateLabelVisibility();

          // Update labels when camera moves (zoom or pan)
          camera.on("updated", updateLabelVisibility);

          // ========================================
          // APPLY FORCE-DIRECTED LAYOUT
          // ========================================

          // Create ForceAtlas2 layout for organic, web-like positioning
          const layout = new FA2Layout(graph, {
            settings: {
              barnesHutOptimize: true,
              strongGravityMode: true,
              gravity: 0.5,
              scalingRatio: 10,
              slowDown: 2,
            },
          });

          layoutRef.current = layout;

          // Start the layout
          layout.start();
          setIsLayoutSettling(true); // Keep hidden during layout
          onLoadingChange(false);

          // Let it run for 3 seconds then stop
          setTimeout(() => {
            layout.stop();
            setIsLayoutSettling(false); // Show graph after layout settles
          }, 3000);

          // ========================================
          // ADD HOVER INTERACTIONS
          // ========================================

          // Store original edge colors
          const originalEdgeColors = new Map<string, string>();
          graph.forEachEdge((edge, attributes) => {
            originalEdgeColors.set(edge, attributes.color || "#999");
          });

          // Mouse enter event on node
          sigma?.on("enterNode", ({ node }: { node: string }) => {
            // Always show label on hover
            const nodeLabel = originalLabels.get(node);
            if (nodeLabel) {
              graph.setNodeAttribute(node, "label", nodeLabel);
            }

            // Get all edges connected to this node
            const incomingEdges = new Set<string>();
            const outgoingEdges = new Set<string>();

            // Separate incoming and outgoing edges
            graph.forEachEdge(node, (edge, attributes, source, target) => {
              if (target === node) {
                // Edge is incoming (pointing TO this node)
                incomingEdges.add(edge);
              } else {
                // Edge is outgoing (pointing FROM this node)
                outgoingEdges.add(edge);
              }
            });

            // Update edge colors - highlight connected edges
            graph.forEachEdge((edge) => {
              if (incomingEdges.has(edge)) {
                // Highlight incoming edges (red and thicker)
                graph.setEdgeAttribute(edge, "color", "#ff6b6b");
                graph.setEdgeAttribute(edge, "size", 4);
              } else if (outgoingEdges.has(edge)) {
                // Highlight outgoing edges (blue and thicker)
                graph.setEdgeAttribute(edge, "color", "#3498db");
                graph.setEdgeAttribute(edge, "size", 4);
              } else {
                // Dim other edges (light gray and thinner)
                graph.setEdgeAttribute(edge, "color", "#ddd");
                graph.setEdgeAttribute(edge, "size", 1);
              }
            });

            // Refresh the display
            sigma.refresh();
          });

          // Mouse leave event on node
          sigma?.on("leaveNode", () => {
            // Restore label visibility based on zoom level
            updateLabelVisibility();

            // Restore original edge colors and sizes
            graph.forEachEdge((edge) => {
              graph.setEdgeAttribute(
                edge,
                "color",
                originalEdgeColors.get(edge) || "#999"
              );
              graph.setEdgeAttribute(edge, "size", 2);
            });

            // Refresh the display
            sigma.refresh();
          });

          // Optional: Add click handler for nodes
          sigma?.on("clickNode", ({ node }: { node: string }) => {});
        }

        onLoadingChange(false);
      } catch (err) {
        console.error("Error fetching graph data:", err);
        onError(
          err instanceof Error ? err.message : "Failed to load graph data"
        );
        onLoadingChange(false);
      }
    };

    fetchAndRenderGraph();

    // ========================================
    // CLEANUP FUNCTION
    // ========================================

    // This runs when the component unmounts
    return () => {
      if (layoutRef.current) {
        const instance = layoutRef.current as { stop?: () => void };
        if (typeof instance.stop === "function") {
          instance.stop();
        }
      }
      if (sigmaRef.current) {
        const instance = sigmaRef.current as { kill?: () => void };
        if (typeof instance.kill === "function") {
          instance.kill();
        }
      }
    };
  }, [projectId, recordId, depth, onLoadingChange, onError, organization]);

  // Render the container div that Sigma will attach to
  return (
    <div style={{ width: "120%", height: "150%", position: "relative" }}>
      {/* Loading spinner during layout calculation */}
      {isLayoutSettling && (
        <div
          style={{
            position: "absolute",
            top: "50%",
            left: "50%",
            transform: "translate(-50%, -50%)",
            zIndex: 10,
            display: "flex",
            flexDirection: "column",
            alignItems: "center",
            gap: "10px",
          }}
        >
          <div
            style={{
              width: "40px",
              height: "40px",
              border: "4px solid #f3f3f3",
              borderTop: "4px solid #3498db",
              borderRadius: "50%",
              animation: "spin 1s linear infinite",
            }}
          />
          <span style={{ color: "#666", fontSize: "14px" }}>
            Calculating layout...
          </span>
        </div>
      )}

      {/* Graph container */}
      <div
        ref={containerRef}
        style={{
          width: "100%",
          height: "100%",
          opacity: isLayoutSettling ? 0 : 1, // Dim during layout
          transition: "opacity 0.5s ease-in",
        }}
      />

      {/* Add CSS animation for spinner */}
      <style jsx>{`
        @keyframes spin {
          0% {
            transform: rotate(0deg);
          }
          100% {
            transform: rotate(360deg);
          }
        }
      `}</style>
    </div>
  );
};

export default GraphClientPage;
