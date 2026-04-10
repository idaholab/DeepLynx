import {
  ArrowPathIcon,
  MagnifyingGlassIcon,
} from "@heroicons/react/24/outline";
import { useLanguage } from "@/app/contexts/Language";
import { GraphNodeSummary } from "./graphTypes";

interface GraphToolbarProps {
  searchQuery: string;
  isSearchOpen: boolean;
  searchResults: GraphNodeSummary[];
  showAllLabels: boolean;
  onSearchQueryChange: (query: string) => void;
  onSearchOpenChange: (open: boolean) => void;
  onSearchSubmit: () => void;
  onSearchSelect: (node: GraphNodeSummary) => void;
  onResetView: () => void;
  onToggleShowAllLabels: () => void;
}

const GraphToolbar = ({
  searchQuery,
  isSearchOpen,
  searchResults,
  showAllLabels,
  onSearchQueryChange,
  onSearchOpenChange,
  onSearchSubmit,
  onSearchSelect,
  onResetView,
  onToggleShowAllLabels,
}: GraphToolbarProps) => {
  const { t } = useLanguage();

  return (
    <div className="flex flex-col gap-4 border-b border-base-300 px-4 py-4 lg:flex-row lg:items-center lg:justify-between">
      {/* Search stays tied to the currently loaded graph payload */}
      <div className="relative w-full max-w-xl">
        <label className="input input-bordered flex items-center gap-2 rounded-xl">
          <MagnifyingGlassIcon className="size-4 text-base-content/50" />
          <input
            type="text"
            className="grow"
            placeholder={t.translations.GRAPH_SEARCH_NODES_BY_LABEL}
            value={searchQuery}
            onChange={(event) => {
              onSearchQueryChange(event.target.value);
              onSearchOpenChange(true);
            }}
            onFocus={() => {
              if (searchQuery.trim()) onSearchOpenChange(true);
            }}
            onKeyDown={(event) => {
              if (event.key === "Enter") {
                event.preventDefault();
                onSearchSubmit();
              }
            }}
          />
        </label>

        {isSearchOpen && searchQuery.trim() && (
          <div className="absolute z-20 mt-2 overflow-hidden rounded-box border border-base-300 bg-base-100 shadow-xl">
            {searchResults.length > 0 ? (
              <ul className="menu w-full p-2">
                {searchResults.map((node) => (
                  <li key={node.id}>
                    <button
                      type="button"
                      className="flex w-full items-center justify-between gap-3"
                      onClick={() => onSearchSelect(node)}
                    >
                      <div className="min-w-0 text-left">
                        <span className="block font-medium text-base-content">
                          {node.label}
                        </span>
                        <span className="text-xs text-base-content/60">
                          {node.type} • depth {node.depth}
                        </span>
                      </div>
                      <span className="shrink-0 text-xs text-base-content/50">
                        #{node.id}
                      </span>
                    </button>
                  </li>
                ))}
              </ul>
            ) : (
              <div className="px-4 py-3 text-sm text-base-content/60">
                {t.translations.GRAPH_NO_NODES_MATCH_SEARCH}
              </div>
            )}
          </div>
        )}
      </div>

      {/* Graph-wide view controls */}
      <div className="flex flex-wrap items-center gap-2">
        <button
          type="button"
          className="btn btn-outline btn-sm"
          onClick={onResetView}
        >
          <ArrowPathIcon className="size-4" />
          {t.translations.RESET}
        </button>
        <button
          type="button"
          className={`btn btn-sm ${showAllLabels ? "btn-primary" : "btn-outline"}`}
          onClick={onToggleShowAllLabels}
        >
          {showAllLabels
            ? t.translations.GRAPH_HIDE_EXTRA_LABELS
            : t.translations.GRAPH_SHOW_ALL_LABELS}
        </button>
      </div>
    </div>
  );
};

export default GraphToolbar;
