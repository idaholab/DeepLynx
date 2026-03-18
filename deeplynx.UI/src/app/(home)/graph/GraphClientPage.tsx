"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { RecordResponseDto } from "@/app/(home)/types/responseDTOs";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import { useLanguage } from "@/app/contexts/Language";
import { getRecord } from "@/app/lib/client_service/record_services.client";
import GraphCanvas from "./components/GraphCanvas";
import GraphLegend from "./components/GraphLegend";
import GraphToolbar from "./components/GraphToolbar";
import RelationshipTable from "./components/RelationshipTable";
import SelectedNodePanel from "./components/SelectedNodePanel";
import TracedPathPanel from "./components/TracedPathPanel";
import {
  buildFilteredConnections,
  buildNodeLookup,
  buildPathNodes,
  buildPathSegments,
  buildPathToSelected,
  buildSearchResults,
  buildSelectedNodeConnections,
} from "./components/graphSelectors";
import {
  GraphExplorerData,
  GraphClientPageProps,
  GraphController,
  GraphViewMode,
} from "./components/graphTypes";

const GraphClientPage = ({
  projectId,
  recordId,
  depth = 3,
}: GraphClientPageProps) => {
  const router = useRouter();
  const { organization } = useOrganizationSession();
  const { t } = useLanguage();

  const [error, setError] = useState<string | null>(null);
  const [graphData, setGraphData] = useState<GraphExplorerData | null>(null);
  const [selectedNodeId, setSelectedNodeId] = useState<number | null>(null);
  const [selectedRecord, setSelectedRecord] =
    useState<RecordResponseDto | null>(null);
  const [isDetailsLoading, setIsDetailsLoading] = useState(false);
  const [searchQuery, setSearchQuery] = useState("");
  const [isSearchOpen, setIsSearchOpen] = useState(false);
  const [showAllLabels, setShowAllLabels] = useState(false);

  const controllerRef = useRef<GraphController | null>(null);
  const relationshipViewMode: GraphViewMode = "all";

  // Reset selection when the loaded graph changes and the current node no
  // longer exists in the new payload.
  useEffect(() => {
    if (!graphData) return;

    setSelectedNodeId((current) => {
      if (current && graphData.nodes.some((node) => node.id === current)) {
        return current;
      }

      return null;
    });
  }, [graphData, recordId]);

  // Load record metadata for the selected node so the side panel can show more
  // than the graph payload alone provides.
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
      } catch {
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

  // Derive lookup maps and view models once from the graph payload so the UI
  // panels stay simple and render from precomputed data.
  const nodeLookup = useMemo(() => buildNodeLookup(graphData), [graphData]);

  const rootNode = useMemo(() => {
    if (!graphData?.rootNodeId) return null;
    return nodeLookup.get(graphData.rootNodeId) ?? null;
  }, [graphData?.rootNodeId, nodeLookup]);

  const selectedNode = useMemo(() => {
    if (!selectedNodeId) return null;
    return nodeLookup.get(selectedNodeId) ?? null;
  }, [nodeLookup, selectedNodeId]);

  const selectedNodeConnections = useMemo(
    () =>
      buildSelectedNodeConnections(
        graphData,
        selectedNodeId,
        nodeLookup,
        t.translations,
      ),
    [graphData, nodeLookup, selectedNodeId, t.translations],
  );

  const filteredConnections = useMemo(
    () =>
      buildFilteredConnections(selectedNodeConnections, relationshipViewMode),
    [relationshipViewMode, selectedNodeConnections],
  );

  const pathToSelected = useMemo(
    () => buildPathToSelected(graphData, selectedNodeId),
    [graphData, selectedNodeId],
  );

  const pathNodes = useMemo(
    () =>
      buildPathNodes(pathToSelected, nodeLookup, selectedNodeId, t.translations),
    [nodeLookup, pathToSelected, selectedNodeId, t.translations],
  );

  const pathSegments = useMemo(
    () =>
      buildPathSegments(graphData, nodeLookup, pathToSelected, t.translations),
    [graphData, nodeLookup, pathToSelected, t.translations],
  );

  const searchResults = useMemo(
    () => buildSearchResults(graphData, searchQuery),
    [graphData, searchQuery],
  );

  // Keep navigation and graph control actions stable so the Sigma canvas does
  // not refetch when parent renders change.
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
    setIsSearchOpen(false);
  }, [handleSelectNode, searchResults]);

  const handleSearchSelect = useCallback(
    (node: { id: number; label: string }) => {
      setSearchQuery(node.label);
      handleSelectNode(node.id);
      setIsSearchOpen(false);
    },
    [handleSelectNode],
  );

  const handleControlsReady = useCallback(
    (controller: GraphController | null) => {
      controllerRef.current = controller;
    },
    [],
  );

  const handleGraphLoadingChange = useCallback((_loading: boolean) => {}, []);

  return (
    <div className="mt-4 p-4">
      <section className="card bg-base-100 shadow-sm">
        <div className="card-body space-y-4 p-4">
          <section className="card overflow-hidden border border-base-300 bg-base-100 shadow-sm">
            {/* Graph controls and search */}
            <GraphToolbar
              searchQuery={searchQuery}
              isSearchOpen={isSearchOpen}
              searchResults={searchResults}
              showAllLabels={showAllLabels}
              onSearchQueryChange={setSearchQuery}
              onSearchOpenChange={setIsSearchOpen}
              onSearchSubmit={handleSearchSubmit}
              onSearchSelect={handleSearchSelect}
              onResetView={handleResetView}
              onToggleShowAllLabels={() =>
                setShowAllLabels((current) => !current)
              }
            />

            {error && (
              <div className="alert alert-error mx-4 mt-4 text-sm shadow-sm">
                {error}
              </div>
            )}

            <div className="min-h-[720px] overflow-hidden bg-[radial-gradient(circle_at_top,_rgba(14,116,144,0.08),_transparent_45%),linear-gradient(180deg,_rgba(248,250,252,0.82),_rgba(226,232,240,0.45))]">
              {/* Interactive Sigma canvas */}
              <GraphCanvas
                organizationId={organization?.organizationId as number | null}
                projectId={projectId}
                recordId={recordId}
                depth={depth}
                selectedNodeId={selectedNodeId}
                showAllLabels={showAllLabels}
                viewMode="path"
                pathNodeIds={pathToSelected.nodeIds}
                pathEdgeIds={pathToSelected.edgeIds}
                onLoadingChange={handleGraphLoadingChange}
                onError={setError}
                onNodeSelect={handleSelectNode}
                onNodeOpen={handleOpenRecord}
                onDataLoaded={setGraphData}
                onControlsReady={handleControlsReady}
              />
            </div>

            <GraphLegend />
          </section>

          {/* Supporting analysis panels under the graph */}
          <section className="grid gap-4 xl:grid-cols-[320px_minmax(0,1fr)_340px]">
            <SelectedNodePanel
              selectedNode={selectedNode}
              selectedRecord={selectedRecord}
              isDetailsLoading={isDetailsLoading}
              onOpenRecord={handleOpenRecord}
            />

            <RelationshipTable
              selectedNode={selectedNode}
              filteredConnections={filteredConnections}
              onSelectNode={(nodeId) => handleSelectNode(nodeId)}
              onOpenRecord={handleOpenRecord}
            />

            <TracedPathPanel
              selectedNode={selectedNode}
              pathNodeCount={pathToSelected.nodeIds.length}
              pathNodes={pathNodes}
              pathSegments={pathSegments}
              onSelectNode={(nodeId) => handleSelectNode(nodeId)}
            />
          </section>
        </div>
      </section>
    </div>
  );
};

export default GraphClientPage;
