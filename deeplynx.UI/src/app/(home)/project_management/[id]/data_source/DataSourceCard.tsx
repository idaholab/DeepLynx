// src/app/(home)/project_management/data_source/DataSourceCard.tsx

import {
  ArrowPathIcon,
  ClipboardDocumentIcon,
  EyeIcon,
  EyeSlashIcon,
  PencilIcon,
  StarIcon,
  TrashIcon,
  CheckIcon,
} from "@heroicons/react/24/outline";
import { StarIcon as StarIconSolid } from "@heroicons/react/24/solid";

import DetailsEditor from "./DetailsEditor";
import {
  ExtendedDataSource,
  formatRecordCount,
  maskKey,
} from "./DataSourcesClient";

type CardProps = {
  source: ExtendedDataSource;
  isDefault: boolean;
  recordCount: number | null;
  saving: boolean;
  projectId: number;
  showKey: Record<string, boolean>;
  copiedKey: string | null;
  expanded: boolean;
  onToggleExpand: (id: number) => void;
  onArchiveToggle: (s: ExtendedDataSource) => Promise<void>;
  onSetDefault: (id: number) => Promise<void>;
  onToggleKeyVisibility: (id: string) => void;
  onCopyKey: (id: string, key: string) => void;
  onDetailsSaved: () => Promise<void> | void; // 🔹 new
  setError: (msg: string | null) => void;
};

const DataSourceCard = ({
  source,
  isDefault,
  recordCount,
  saving,
  projectId,
  showKey,
  copiedKey,
  expanded,
  onToggleExpand,
  onArchiveToggle,
  onSetDefault,
  onToggleKeyVisibility,
  onCopyKey,
  onDetailsSaved,
  setError,
}: CardProps) => {
  const hasId = source.id != null;
  const idNum = typeof source.id === "number" ? source.id : undefined;
  const apiKey = source.apiKey;
  const health = Number(source.health);
  const lastSync = source.lastSync;

  return (
    <div
      className={`card bg-base-100 shadow-xl border-l-4"
        }`}
    >
      <div className="card-body">
        {/* Header row */}
        <div className="flex justify-between items-center mb-4">
          <div className="flex-1">
            <div className="flex items-center gap-3 mb-2">
              <div>
                <div className="flex items-center gap-2">
                  <h3 className="text-xl font-bold">{source.name}</h3>
                  {isDefault}
                  {source.isArchived && (
                    <div className="badge badge-warning">Archived</div>
                  )}
                </div>
                <div className="flex gap-2 items-center mt-1">
                  {source.type && (
                    <div className="badge badge-ghost">{source.type}</div>
                  )}
                  {source.baseuri && (
                    <div className="badge badge-ghost">URI</div>
                  )}
                </div>
              </div>
            </div>

            {/* Connection String */}
            {source.baseuri && (
              <div className="bg-base-200 rounded-lg p-3 mb-3">
                <div className="text-xs text-base-content/60 mb-1">
                  Connection
                </div>
                <code className="text-xs">{source.baseuri}</code>
              </div>
            )}

            {/* Stats Row */}
            <div className="grid grid-cols-1 sm:grid-cols-4 gap-3 mb-3">
              <div className="bg-base-200 rounded-lg p-2 text-center">
                <div className="text-xs text-base-content/60">Records</div>
                <div className="text-lg font-bold text-primary">
                  {formatRecordCount(recordCount)}
                </div>
              </div>
              <div className="bg-base-200 rounded-lg p-2 text-center">
                <div className="text-xs text-base-content/60">Health</div>
                <div
                  className={`text-lg font-bold ${Number.isFinite(health)
                    ? health > 80
                      ? "text-success"
                      : health > 50
                        ? ""
                        : "text-error"
                    : ""
                    }`}
                >
                  {Number.isFinite(health) ? `${health}%` : "—"}
                </div>
              </div>
              <div className="bg-base-200 rounded-lg p-2 text-center">
                <div className="text-xs text-base-content/60">Last Sync</div>
                <div className="text-sm font-semibold">{lastSync ?? "—"}</div>
              </div>
              <div className="bg-base-200 rounded-lg p-2 text-center">
                <div className="text-xs text-base-content/60">Last Updated</div>
                <div className="text-sm font-semibold">
                  {source.lastUpdatedAt
                    ? new Date(source.lastUpdatedAt).toLocaleString()
                    : "—"}
                </div>
              </div>
            </div>
          </div>

          {/* Actions */}
          <div className="flex flex-col gap-2 ml-2 mt-10">
            <button
              className="btn btn-ghost btn-sm gap-2"
              onClick={() => hasId && onToggleExpand(idNum!)}
              disabled={!hasId}
            >
              <PencilIcon className="w-4 h-4" />
              Details
            </button>
            <button
              className="btn btn-ghost btn-sm gap-2"
              onClick={() => hasId && onArchiveToggle(source)}
              disabled={saving || !hasId}
            >
              {source.isArchived ? (
                <>
                  <ArrowPathIcon className="w-4 h-4" />
                  Unarchive
                </>
              ) : (
                <>
                  <TrashIcon className="w-4 h-4" />
                  Archive
                </>
              )}
            </button>
          </div>
        </div>

        {/* Expanded Details Editor */}
        {expanded && idNum != null && (
          <DetailsEditor
            projectId={projectId}
            source={source}
            onClose={() => onToggleExpand(idNum)}
            onSaved={onDetailsSaved}
            setError={setError}
          />
        )}

        {/* API Key Section */}
        {apiKey?.key && (
          <>
            <div className="divider my-2">API Integration Key</div>
            <div className="bg-base-200/50 rounded-lg p-4">
              <div className="grid grid-cols-2 gap-3 mb-3 text-sm">
                {apiKey.created && (
                  <div>
                    <span className="text-base-content/60">Created:</span>
                    <span className="ml-2 font-semibold">{apiKey.created}</span>
                  </div>
                )}
                {apiKey.expiresIn && (
                  <div>
                    <span className="text-base-content/60">Expires:</span>
                    <span
                      className={`ml-2 font-semibold ${apiKey.expiresIn === "Expired" ? "text-error" : ""
                        }`}
                    >
                      {apiKey.expiresIn}
                    </span>
                  </div>
                )}
                {apiKey.lastUsed && (
                  <div>
                    <span className="text-base-content/60">Last Used:</span>
                    <span className="ml-2 font-semibold">
                      {apiKey.lastUsed}
                    </span>
                  </div>
                )}
                {apiKey.permissions && apiKey.permissions.length > 0 && (
                  <div>
                    <span className="text-base-content/60">Permissions:</span>
                    <div className="inline-flex gap-1 ml-2">
                      {apiKey.permissions.map((perm) => (
                        <div
                          key={perm}
                          className="badge badge-primary badge-xs"
                        >
                          {perm}
                        </div>
                      ))}
                    </div>
                  </div>
                )}
              </div>

              <div className="form-control">
                <label className="label py-1">
                  <span className="label-text font-semibold text-xs">
                    API Key
                  </span>
                </label>
                <div className="flex gap-2">
                  <div className="flex-1 bg-base-300 rounded-lg p-3 font-mono text-xs overflow-x-auto">
                    {showKey[String(source.id)]
                      ? apiKey.key
                      : maskKey(apiKey.key)}
                  </div>
                  <button
                    className="btn btn-sm btn-ghost btn-square"
                    onClick={() => onToggleKeyVisibility(String(source.id))}
                    title={showKey[String(source.id)] ? "Hide key" : "Show key"}
                  >
                    {showKey[String(source.id)] ? (
                      <EyeSlashIcon className="w-5 h-5" />
                    ) : (
                      <EyeIcon className="w-5 h-5" />
                    )}
                  </button>
                  <button
                    className="btn btn-sm btn-primary gap-1"
                    onClick={() => onCopyKey(String(source.id), apiKey.key)}
                  >
                    {copiedKey === String(source.id) ? (
                      <>
                        <CheckIcon className="w-4 h-4" />
                        Copied
                      </>
                    ) : (
                      <>
                        <ClipboardDocumentIcon className="w-4 h-4" />
                        Copy
                      </>
                    )}
                  </button>
                </div>
                <label className="label py-1">
                  <span className="label-text-alt text-warning text-xs">
                    🔐 Keep this key secure - it provides access to this data
                    source
                  </span>
                </label>
              </div>
            </div>
          </>
        )}
      </div>
    </div>
  );
};

export default DataSourceCard;
