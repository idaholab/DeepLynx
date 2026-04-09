import { ArrowTopRightOnSquareIcon } from "@heroicons/react/24/outline";
import { useLanguage } from "@/app/contexts/Language";
import { RecordResponseDto } from "@/app/(home)/types/responseDTOs";
import { GraphNodeSummary } from "./graphTypes";

interface SelectedNodePanelProps {
  selectedNode: GraphNodeSummary | null;
  selectedRecord: RecordResponseDto | null;
  isDetailsLoading: boolean;
  onOpenRecord: (nodeId: number) => void;
}

const SelectedNodePanel = ({
  selectedNode,
  selectedRecord,
  isDetailsLoading,
  onOpenRecord,
}: SelectedNodePanelProps) => {
  const { t } = useLanguage();

  return (
    <aside className="card border border-base-300 bg-base-100 shadow-sm">
      <div className="card-body p-5">
        {/* Panel header and quick action */}
        <div className="flex flex-wrap items-center justify-between gap-2">
          <p className="text-sm font-medium uppercase tracking-[0.16em] text-base-content/50">
            {t.translations.GRAPH_SELECTED_NODE}
          </p>
          <button
            type="button"
            className="btn btn-secondary btn-sm"
            onClick={() => {
              if (selectedNode) {
                onOpenRecord(selectedNode.id);
              }
            }}
            disabled={!selectedNode}
          >
            <ArrowTopRightOnSquareIcon className="size-4" />
          </button>
        </div>

        <h3 className="mt-2 text-xl font-semibold text-base-content">
          {selectedNode?.label || t.translations.GRAPH_NO_NODE_SELECTED}
        </h3>
        <p className="mt-1 text-sm text-base-content/70">
          {selectedNode
            ? ""
            : t.translations.GRAPH_SELECT_NODE_TO_POPULATE_SUMMARY}
        </p>

        {selectedNode ? (
          <div className="mt-5 space-y-4">
            {/* Lightweight facts from the graph payload */}
            <div className="flex flex-wrap items-center gap-2">
              <span className="badge badge-outline">
                {t.translations.DEPTH} {selectedNode.depth}
              </span>
              <span className="badge badge-outline">
                Record ID: {selectedNode.id}
              </span>
            </div>

            <div className="card border border-base-300 bg-base-100 shadow-sm">
              <div className="card-body p-4">
                {/* Fetched record details for the selected node */}
                {isDetailsLoading ? (
                  <div className="space-y-2">
                    <div className="skeleton h-4 w-2/3" />
                    <div className="skeleton h-4 w-full" />
                    <div className="skeleton h-4 w-5/6" />
                  </div>
                ) : (
                  <div className="space-y-3 text-sm">
                    <div>
                      <p className="text-xs uppercase tracking-wide text-base-content/50">
                        {t.translations.NAME}
                      </p>
                      <p className="text-base-content">
                        {selectedRecord?.name || selectedNode.label}
                      </p>
                    </div>
                    <div>
                      <p className="text-xs uppercase tracking-wide text-base-content/50">
                        {t.translations.DESCRIPTION}
                      </p>
                      <p className="text-base-content/70">
                        {selectedRecord?.description ||
                          t.translations.GRAPH_NO_DESCRIPTION_AVAILABLE}
                      </p>
                    </div>
                    <div className="grid grid-cols-2 gap-3">
                      <div>
                        <p className="text-xs uppercase tracking-wide text-base-content/50">
                          {t.translations.URI}
                        </p>
                        <p className="truncate text-base-content/70">
                          {selectedRecord?.uri || t.translations.NOT_SET}
                        </p>
                      </div>
                      <div>
                        <p className="text-xs uppercase tracking-wide text-base-content/50">
                          {t.translations.FILE_TYPE}
                        </p>
                        <p className="text-base-content/70">
                          {selectedRecord?.fileType || t.translations.UNKNOWN}
                        </p>
                      </div>
                    </div>
                  </div>
                )}
              </div>
            </div>
          </div>
        ) : (
          <div className="mt-5 rounded-box border border-dashed border-base-300 px-4 py-8 text-center text-sm text-base-content/60">
            {t.translations.GRAPH_NO_NODE_SELECTED}
          </div>
        )}
      </div>
    </aside>
  );
};

export default SelectedNodePanel;
