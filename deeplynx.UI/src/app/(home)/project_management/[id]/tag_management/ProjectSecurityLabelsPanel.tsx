import React from "react";
import {
  ShieldCheckIcon,
  LockClosedIcon,
  LockOpenIcon,
  MagnifyingGlassIcon,
  InformationCircleIcon,
} from "@heroicons/react/24/outline";

type SecurityLabelDto = {
  id: number;
  name: string;
  description?: string | null;
  projectId: number;
  isArchived: boolean;
  lastUpdatedAt: string | null;
  lastUpdatedBy: string | null;
  archivedAt: string | null;
};

interface Props {
  labels: SecurityLabelDto[];
  labelsLockedByOrg: boolean;
  labelsLoading: boolean;
  labelsError: string | null;
  filteredLabels: SecurityLabelDto[];
  labelSearch: string;
  setLabelSearch: (value: string) => void;
  filteredCount: number;
  labelCount: number;
  projectId?: number;
  archivingLabelId: number | null;
  onCreateLabel: () => void;
  onEditLabel: (id: number) => void;
  onArchiveClick: (label: SecurityLabelDto) => void;
}

const ProjectSecurityLabelsPanel: React.FC<Props> = ({
  labelsLockedByOrg,
  labelsLoading,
  labelsError,
  filteredLabels,
  labelSearch,
  setLabelSearch,
  filteredCount,
  labelCount,
  projectId,
  archivingLabelId,
  onCreateLabel,
  onEditLabel,
  onArchiveClick,
}) => {
  return (
    <div className="card bg-base-100 shadow-lg">
      <div className="card-body">
        {/* Header + Controls */}
        <div className="flex items-start justify-between gap-4 mb-3">
          <div className="flex-1">
            <div className="flex items-center gap-2">
              <ShieldCheckIcon className="w-5 h-5 text-secondary" />
              <h3 className="font-semibold text-base">
                Project Security Labels
              </h3>
            </div>
            <p className="text-xs text-base-content/70 mt-1 max-w-md">
              Security labels (e.g., CUI) for attribute-based access control at
              the project level. This project also inherits any labels defined
              at the organization level.
            </p>
          </div>

          <div className="flex flex-col items-end gap-2">
            {/* Lock indicator (read-only, controlled by org) */}
            <button
              type="button"
              className={`btn btn-xs gap-1 ${
                labelsLockedByOrg ? "btn-error" : "btn-ghost"
              }`}
              disabled
            >
              {labelsLockedByOrg ? (
                <>
                  <LockClosedIcon className="w-4 h-4" />
                  Locked by Org
                </>
              ) : (
                <>
                  <LockOpenIcon className="w-4 h-4" />
                  Project-managed
                </>
              )}
            </button>

            {/* Search input */}
            <div className="form-control w-40">
              <div className="input input-xs input-bordered flex items-center gap-1 px-2">
                <MagnifyingGlassIcon className="w-3 h-3 text-base-content/60" />
                <input
                  type="text"
                  className="grow text-[0.7rem] bg-transparent focus:outline-none"
                  placeholder="Search labels..."
                  value={labelSearch}
                  onChange={(e) => setLabelSearch(e.target.value)}
                />
              </div>
            </div>

            {/* Add button */}
            <button
              type="button"
              className="btn btn-primary btn-xs gap-1"
              onClick={onCreateLabel}
              disabled={labelsLockedByOrg || !projectId}
              title={
                !projectId
                  ? "No project selected"
                  : labelsLockedByOrg
                    ? "Security labels are locked at the organization level"
                    : "Create new security label"
              }
            >
              + New Label
            </button>
          </div>
        </div>

        {/* n of m line */}
        <div className="flex justify-between items-center mb-3 text-[0.7rem] text-base-content/60">
          <span>
            Showing <span className="font-semibold">{filteredCount}</span> of{" "}
            <span className="font-semibold">{labelCount}</span> labels
          </span>
          {labelSearch.trim() && (
            <span className="italic">
              Filtered by:{" "}
              <span className="font-medium break-all">{labelSearch}</span>
            </span>
          )}
        </div>

        {/* Info text */}
        <div className="flex items-start gap-2 mb-3 text-xs text-base-content/70">
          <InformationCircleIcon className="w-4 h-4" />
          <p>
            When locked at the organization level, project administrators{" "}
            <span className="font-semibold">
              cannot define additional security labels
            </span>{" "}
            and must use only labels defined at the organization level.
          </p>
        </div>

        {/* Label list */}
        <div className="space-y-2 max-h-72 overflow-y-auto">
          {labelsLoading ? (
            <div className="py-6 text-center text-xs text-base-content/60">
              Loading project security labels…
            </div>
          ) : labelsError ? (
            <div className="py-6 text-center text-xs text-error">
              {labelsError}
            </div>
          ) : filteredLabels.length === 0 ? (
            <div className="py-6 text-center text-xs text-base-content/60 border border-dashed border-base-300 rounded-lg">
              {labelSearch.trim()
                ? "No security labels match your search."
                : "No project labels defined. When unlocked, you can define project-specific security labels in addition to any organization-level labels."}
            </div>
          ) : (
            filteredLabels.map((label) => (
              <div
                key={label.id}
                className="flex items-center justify-between bg-base-200/70 hover:bg-base-300/80 transition rounded-lg px-3 py-2"
              >
                <div className="flex flex-col gap-0.5">
                  <div className="flex items-center gap-2">
                    <span className="badge badge-secondary badge-outline badge-sm">
                      {label.name}
                    </span>
                    <span className="text-[0.7rem] text-base-content/70">
                      Project-level security label
                    </span>
                  </div>
                  {label.description && (
                    <span className="text-[0.65rem] text-base-content/60 line-clamp-2">
                      {label.description}
                    </span>
                  )}
                </div>
                <div className="flex items-center gap-1">
                  <button
                    type="button"
                    className="btn btn-ghost btn-xs"
                    onClick={() => onEditLabel(label.id)}
                    disabled={labelsLockedByOrg}
                    title={
                      labelsLockedByOrg
                        ? "Security labels are locked by the organization"
                        : "Edit"
                    }
                  >
                    Edit
                  </button>
                  <button
                    type="button"
                    className="btn btn-ghost btn-xs text-error"
                    onClick={() => onArchiveClick(label)}
                    disabled={
                      labelsLockedByOrg || archivingLabelId === label.id
                    }
                    title={
                      labelsLockedByOrg
                        ? "Security labels are locked by the organization"
                        : "Archive (soft delete) label"
                    }
                  >
                    {archivingLabelId === label.id ? "Archiving..." : "Delete"}
                  </button>
                </div>
              </div>
            ))
          )}
        </div>
      </div>
    </div>
  );
};

export default ProjectSecurityLabelsPanel;
