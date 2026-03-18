import { useLanguage } from "@/app/contexts/Language";

const GraphLegend = () => {
  const { t } = useLanguage();

  return (
    // Depth colors match the node styling used inside the Sigma canvas.
    <div className="flex flex-col gap-3 border-t border-base-300 px-4 py-4 text-sm text-base-content/70 lg:flex-row lg:items-center lg:justify-between">
      <div className="flex flex-wrap gap-3 text-xs uppercase tracking-wide text-base-content/50">
        <span className="flex items-center gap-2">
          <span className="size-2.5 rounded-full bg-teal-700" />
          {t.translations.ROOT}
        </span>
        <span className="flex items-center gap-2">
          <span className="size-2.5 rounded-full bg-sky-600" />
          {t.translations.GRAPH_DEPTH_1}
        </span>
        <span className="flex items-center gap-2">
          <span className="size-2.5 rounded-full bg-amber-600" />
          {t.translations.GRAPH_DEPTH_2}
        </span>
        <span className="flex items-center gap-2">
          <span className="size-2.5 rounded-full bg-slate-500" />
          {t.translations.GRAPH_DEPTH_3_PLUS}
        </span>
      </div>
    </div>
  );
};

export default GraphLegend;
