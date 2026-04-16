import { useLanguage } from "@/app/contexts/Language";
import { GraphNodeSummary } from "./graphTypes";
import { buildClassColorMap, getUniqueClasses } from "./graphStyle";

interface Props {
  nodes: GraphNodeSummary[];
}

const GraphLegend = ({ nodes }: Props) => {
  const { t } = useLanguage();
  const classEntries = getUniqueClasses(nodes);
  const classColorMap = buildClassColorMap(nodes);

  return (
    <div className="flex max-h-40 flex-wrap gap-3 overflow-y-auto border-t border-base-300 px-4 py-4 text-xs uppercase tracking-wide text-base-content/50">
      {classEntries.length > 0 ? (
        classEntries.map((entry) => (
          <span key={entry.key} className="flex items-center gap-2">
            <span
              className="size-2.5 rounded-full"
              style={{
                backgroundColor: classColorMap.get(entry.key) || "#64748b",
              }}
            />
            {entry.label}
          </span>
        ))
      ) : (
        <span>{t.translations.GRAPH_NO_NODES_FOUND}</span>
      )}
    </div>
  );
};

export default GraphLegend;
