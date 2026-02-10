import React from "react";
import {
  ShieldCheckIcon,
  LockClosedIcon,
  LockOpenIcon,
  MagnifyingGlassIcon,
  InformationCircleIcon,
} from "@heroicons/react/24/outline";
import type { SensitivityLabelsDto } from "@/app/(home)/types/responseDTOs";

interface Props {
  labels: SensitivityLabelsDto[];
  labelsLocked: boolean;
  labelsLoading: boolean;
  labelsError: string | null;
  filteredLabels: SensitivityLabelsDto[];
  labelSearch: string;
  setLabelSearch: (value: string) => void;
  filteredCount: number;
  labelCount: number;
  orgId?: number;
  archivingLabelId: number | null;
  onToggleLock: () => void;
  onCreateLabel: () => void;
  onEditLabel: (id: number) => void;
  onArchiveClick: (label: SensitivityLabelsDto) => void;
}

const SecurityLabelsOrg: React.FC<Props> = ({
  labelsLocked,
  labelsLoading,
  labelsError,
  filteredLabels,
  labelSearch,
  setLabelSearch,
  filteredCount,
  labelCount,
  orgId,
  archivingLabelId,
  onToggleLock,
  onCreateLabel,
  onEditLabel,
  onArchiveClick,
}) => {
  return (
    <div className="card bg-base-100 border border-secondary/60 shadow-sm">
      <div className="card-body">
        {/* Header + Controls */}
        <div className="flex items-start justify-between gap-4 mb-3">
          <div className="flex-1">
            <div className="flex items-center gap-2">
              <ShieldCheckIcon className="w-5 h-5 text-secondary" />
              <h3 className="font-semibold text-base">
                Organization Security Labels
              </h3>
            </div>
            <p className="text-xs text-base-content/70 mt-1 max-w-md">
              Security labels for attribute-based access control. All projects
              inherit these and can optionally add their own.
            </p>
          </div>

          <div className="flex flex-col items-end gap-2">
            {/* Lock toggle */}
            <button
              type="button"
              className={`btn btn-xs gap-1 ${
                labelsLocked ? "btn-error" : "btn-ghost"
              }`}
              onClick={onToggleLock}
            >
              {labelsLocked ? (
                <>
                  <LockClosedIcon className="w-4 h-4" />
                  Locked
                </>
              ) : (
                <>
                  <LockOpenIcon className="w-4 h-4" />
                  Unlocked
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
              disabled={labelsLocked || !orgId}
              title={
                !orgId
                  ? "No organization selected"
                  : labelsLocked
                    ? "Labels are locked at the org level"
                    : "Create new label"
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
            When locked, projects{" "}
            <span className="font-semibold">cannot define new labels</span> and
            must use only the labels defined at the organization level.
          </p>
        </div>

        {/* Labels list */}
        <div className="space-y-2 max-h-72 overflow-y-auto">
          {labelsLoading ? (
            <div className="py-6 text-center text-xs text-base-content/60">
              Loading organization labels…
            </div>
          ) : labelsError ? (
            <div className="py-6 text-center text-xs text-error">
              {labelsError}
            </div>
          ) : filteredLabels.length === 0 ? (
            <div className="py-6 text-center text-xs text-base-content/60 border border-dashed border-base-300 rounded-lg">
              {labelSearch.trim()
                ? "No labels match your search."
                : "No labels defined. Create labels to standardize access control across all projects."}
            </div>
          ) : (
            filteredLabels.map((label) => (
              <div
                key={label.id}
                className="flex items-center justify-between bg-base-200/70 hover:bg-base-300/80 transition rounded-lg px-3 py-2"
              >
                <div className="flex items-center gap-2">
                  <span className="badge badge-secondary badge-outline badge-sm">
                    {label.name}
                  </span>
                  <span className="text-[0.7rem] text-base-content/70">
                    Inherited by all projects
                  </span>
                </div>
                <div className="flex items-center gap-1">
                  <button
                    type="button"
                    className="btn btn-ghost btn-xs"
                    onClick={() => onEditLabel(label.id)}
                    disabled={labelsLocked}
                    title={labelsLocked ? "Labels are locked" : "Edit"}
                  >
                    Edit
                  </button>
                  <button
                    type="button"
                    className="btn btn-ghost btn-xs text-error"
                    onClick={() => onArchiveClick(label)}
                    disabled={labelsLocked || archivingLabelId === label.id}
                    title={
                      labelsLocked
                        ? "Labels are locked"
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

export default SecurityLabelsOrg;
