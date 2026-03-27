import { Attributes } from "graphology-types";

// Graphology node and edge attributes used by the Sigma renderer.
export interface NodeAttributes extends Attributes {
  x: number;
  y: number;
  size: number;
  label: string;
  color: string;
  nodeType: string;
  classId?: number | null;
  className?: string | null;
}

export interface EdgeAttributes extends Attributes {
  size?: number;
  color?: string;
  label?: string;
  relationshipId?: number | null;
  edgeId?: number;
  type?:
    | "arrow"
    | "double-arrow"
    | "clamped"
    | "double-clamped"
    | "clamped-arrow"
    | "double-clamped-arrow";
  direction?: "incoming" | "outgoing" | "bidirectional";
}

// API-facing graph summaries used by the page and its supporting panels.
export interface GraphClientPageProps {
  projectId: number;
  recordId: number;
  depth?: number;
}

export interface GraphNodeSummary {
  id: number;
  label: string;
  type: string;
  depth: number;
  classId?: number | null;
  className?: string | null;
}

export interface GraphLinkSummary {
  source: number;
  target: number;
  relationshipId: number | null;
  relationshipName: string | null;
  edgeId: number;
}

export interface GraphExplorerData {
  nodes: GraphNodeSummary[];
  links: GraphLinkSummary[];
  rootNodeId: number | null;
}

export interface GraphController {
  fitGraph: () => void;
  focusNode: (nodeId: number) => void;
  resetView: () => void;
}

export type GraphViewMode = "all" | "incoming" | "outgoing" | "path";

// Small local camera/Sigma shims keep the graph component decoupled from the
// full renderer type surface.
export type CameraState = {
  x?: number;
  y?: number;
  ratio?: number;
  angle?: number;
};

export type CameraInstance = {
  getState?: () => CameraState;
  setState?: (state: CameraState) => void;
  animate?: (
    state: CameraState,
    options?: { duration?: number; easing?: string },
  ) => void;
  animatedReset?: (options?: { duration?: number }) => void;
  on?: (event: string, handler: () => void) => void;
};

export type SigmaInstance = {
  getCamera?: () => CameraInstance;
  on?: (event: string, handler: (payload: any) => void) => void;
  refresh?: () => void;
  kill?: () => void;
  graphToViewport?: (coordinates: { x: number; y: number }) => {
    x: number;
    y: number;
  };
  viewportToGraph?: (coordinates: { x: number; y: number }) => {
    x: number;
    y: number;
  };
  setSetting?: (key: string, value: unknown) => void;
};

// Derived UI models used by the relationship table and traced-path panel.
export type GraphConnectionDirection =
  | "Incoming"
  | "Outgoing"
  | "Bidirectional";

export interface GraphConnectionSummary {
  rowId: string;
  edgeId: number;
  direction: GraphConnectionDirection;
  relationshipName: string;
  connectedNodeId: number;
  connectedNodeLabel: string;
  connectedNodeDepth: number | null;
}

export interface GraphPathResult {
  nodeIds: number[];
  edgeIds: number[];
}

export interface GraphPathNode {
  id: number;
  label: string;
  type: string;
  isRoot: boolean;
  isSelected: boolean;
}

export interface GraphPathSegment {
  edgeId: number;
  fromNodeId: number;
  toNodeId: number;
  fromLabel: string;
  toLabel: string;
  relationshipName: string;
}

export interface GraphStats {
  nodes: number;
  links: number;
  incoming: number;
  outgoing: number;
}
