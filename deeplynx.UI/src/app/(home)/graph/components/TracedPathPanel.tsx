import { useLanguage } from "@/app/contexts/Language";
import { GraphNodeSummary, GraphPathNode, GraphPathSegment } from "./graphTypes";

interface TracedPathPanelProps {
  selectedNode: GraphNodeSummary | null;
  pathNodeCount: number;
  pathNodes: GraphPathNode[];
  pathSegments: GraphPathSegment[];
  onSelectNode: (nodeId: number) => void;
}

const TracedPathPanel = ({
  selectedNode,
  pathNodeCount,
  pathNodes,
  pathSegments,
  onSelectNode,
}: TracedPathPanelProps) => {
  const { t } = useLanguage();

  return (
    <aside className="card border border-base-300 bg-base-100 shadow-sm">
      <div className="card-body p-5">
        {/* Summary of the currently traced route */}
        <p className="text-sm font-medium uppercase tracking-[0.16em] text-base-content/50">
          {t.translations.GRAPH_TRACED_PATH}
        </p>
        <h3 className="mt-2 border-b pb-2 text-xl font-semibold text-base-content">
          {selectedNode
            ? t.translations.GRAPH_HOPS.replace(
                "{count}",
                String(Math.max(pathNodeCount - 1, 0)),
              )
            : t.translations.GRAPH_NO_TRACE}
        </h3>
        <p className="mt-1 text-sm text-base-content/70">
          {selectedNode
            ? ""
            : t.translations.GRAPH_SELECT_NODE_FOR_TRACE}
        </p>

        {pathNodes.length > 0 ? (
          <div className="mt-5 space-y-4">
            {/* Node chain from root to the selected record */}
            <div className="flex flex-col items-center">
              {pathNodes.map((node, index) => (
                <div key={node.id} className="contents">
                  <button
                    type="button"
                    className={`btn max-w-full rounded-full text-center text-sm ${
                      node.isSelected
                        ? "btn-primary"
                        : node.isRoot
                          ? "btn-secondary"
                          : "btn-outline"
                    }`}
                    onClick={() => onSelectNode(node.id)}
                  >
                    <span className="truncate">{node.label}</span>
                  </button>
                  {index < pathNodes.length - 1 && (
                    <span className="py-2 text-lg leading-none text-base-content/30">
                      ↓
                    </span>
                  )}
                </div>
              ))}
            </div>

            <div className="space-y-2">
              {/* Human-readable hop breakdown for the highlighted route */}
              {pathSegments.length > 0 ? (
                pathSegments.map((segment, index) => (
                  <div
                    key={segment.edgeId}
                    className="card border border-base-300 bg-base-100 shadow-sm"
                  >
                    <div className="card-body px-3 py-3">
                      <p className="text-xs uppercase tracking-wide text-base-content/50">
                        {t.translations.GRAPH_HOP.replace(
                          "{count}",
                          String(index + 1),
                        )}
                      </p>
                      <p className="mt-1 text-sm font-medium text-base-content">
                        {segment.fromLabel} → {segment.toLabel}
                      </p>
                      <p className="mt-1 text-sm text-base-content/70">
                        {segment.relationshipName}
                      </p>
                    </div>
                  </div>
                ))
              ) : (
                <div className="rounded-box border border-dashed border-base-300 px-4 py-6 text-center text-sm text-base-content/60">
                  {t.translations.GRAPH_NO_MULTI_HOP_TRACE}
                </div>
              )}
            </div>
          </div>
        ) : (
          <div className="mt-5 rounded-box border border-dashed border-base-300 px-4 py-8 text-center text-sm text-base-content/60">
            {t.translations.GRAPH_NO_PATH_AVAILABLE}
          </div>
        )}
      </div>
    </aside>
  );
};

export default TracedPathPanel;
