import { useLanguage } from "@/app/contexts/Language";
import { GraphConnectionSummary, GraphNodeSummary } from "./graphTypes";
import { RecordResponseDto } from "@/app/(home)/types/responseDTOs";

interface RelationshipTableProps {
  selectedNode: GraphNodeSummary | null;
  filteredConnections: GraphConnectionSummary[];
  onSelectNode: (nodeId: number) => void;
  onOpenRecord: (nodeId: number) => void;
  selectedRecord: RecordResponseDto | null;
  isDetailsLoading: boolean;
}

const RelationshipTable = ({
  selectedNode,
  filteredConnections,
  onSelectNode,
  onOpenRecord,
  selectedRecord,
  isDetailsLoading,
}: RelationshipTableProps) => {
  const { t } = useLanguage();

  return (
    <div className="card border border-base-300 bg-base-100 shadow-sm">
      <div className="card-body gap-4 p-0">
        {/* Table header explains the current selection context */}
        <div className="flex flex-col gap-4 border-b border-base-300 px-5 py-4 lg:flex-row lg:items-center lg:justify-between">
          <div>
            <h3 className="text-lg font-semibold text-base-content">
              {t.translations.GRAPH_RELATIONSHIP_TABLE}
            </h3>
            <p className="text-sm text-base-content/70">
              {selectedNode
                ? t.translations.GRAPH_EXACT_CONNECTIONS_FOR.replace(
                    "{name}",
                    selectedNode.label,
                  )
                : t.translations.GRAPH_SELECT_NODE_TO_INSPECT_RELATIONSHIPS}
            </p>
          </div>
        </div>

        <div className="max-h-[520px] overflow-auto">
          {/* Relationship rows stay aligned with graph selection and open actions */}
          <table className="table">
            <thead className="sticky top-0 bg-base-200">
              <tr className="text-base-content">
                <th>{t.translations.DIRECTION}</th>
                <th></th>
                <th>{t.translations.GRAPH_CONNECTED_RECORD}</th>
                <th>{t.translations.DEPTH}</th>
                <th>{t.translations.DATA_CATALOG_ID}</th>
                <th className="text-right">{t.translations.ACTIONS}</th>
              </tr>
            </thead>
            <tbody>
              {selectedNode && (
                <tr className="bg-base-200/60">
                  <td>
                    <span className="badge badge-primary badge-outline">
                      Selected
                    </span>
                  </td>
                  <td className="font-medium text-base-content">Root</td>
                  <td className="text-base-content/70">
                    {selectedRecord?.name || selectedNode.label}
                  </td>
                  <td className="text-base-content/70">{selectedNode.depth}</td>
                  <td className="text-base-content/60">#{selectedNode.id}</td>
                  <td className="text-right">
                    <button
                      type="button"
                      className="btn btn-secondary btn-sm"
                      onClick={() => onOpenRecord(selectedNode.id)}
                    >
                      {t.translations.OPEN}
                    </button>
                  </td>
                </tr>
              )}

              {filteredConnections.length > 0 ? (
                filteredConnections.map((connection) => (
                  <tr key={connection.rowId} className="hover">
                    <td>
                      <span
                        className={`badge ${
                          connection.direction === "Bidirectional"
                            ? "border-purple-500 bg-purple-50 text-purple-700"
                            : connection.direction === "Incoming"
                              ? "badge-error badge-outline"
                              : "badge-secondary badge-outline"
                        }`}
                      >
                        {connection.direction === "Bidirectional"
                          ? t.translations.BIDIRECTIONAL
                          : connection.direction === "Incoming"
                            ? t.translations.INCOMING
                            : t.translations.OUTGOING}
                      </span>
                    </td>
                    <td className="font-medium text-base-content">
                      {connection.relationshipName}
                    </td>
                    <td>
                      <button
                        type="button"
                        className="link link-hover text-left font-medium text-primary"
                        onClick={() => onSelectNode(connection.connectedNodeId)}
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
                        onClick={() => onOpenRecord(connection.connectedNodeId)}
                      >
                        {t.translations.OPEN}
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
                      ? t.translations.GRAPH_NO_RELATIONSHIPS_MATCH_FILTER
                      : t.translations.GRAPH_SELECT_NODE_TO_POPULATE_TABLE}
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
};

export default RelationshipTable;
