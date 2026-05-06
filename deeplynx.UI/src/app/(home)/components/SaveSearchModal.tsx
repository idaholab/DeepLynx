"use client";

import {
  XMarkIcon,
  BookmarkIcon,
} from "@heroicons/react/24/outline";

export interface SaveSearchModalProps {
  isOpen: boolean;
  isSaving: boolean;
  alias: string;
  onAliasChange: (v: string) => void;
  onSave: () => void;
  onClose: () => void;
}

export default function SaveSearchModal({
  isOpen,
  isSaving,
  alias,
  onAliasChange,
  onSave,
  onClose,
}: SaveSearchModalProps) {
  if (!isOpen) return null;

  return (
    <div className="modal modal-open">
      <div className="modal-box bg-base-100 border border-base-content/10 max-w-md">
        <button
          onClick={onClose}
          className="btn btn-sm btn-ghost absolute right-3 top-3"
        >
          <XMarkIcon className="w-4 h-4" />
        </button>

        <h3 className="font-bold text-lg text-base-content mb-1">Save Search</h3>
        <p className="text-sm text-base-content/50 mb-5">
          Give this search a name so you can run it again later.
        </p>

        <div className="form-control gap-1 mb-6">
          <label className="label py-0">
            <span className="label-text text-xs font-semibold uppercase tracking-wide text-base-content/60">
              Search name
            </span>
          </label>
          <input
            type="text"
            autoFocus
            placeholder="e.g. Timeseries Records"
            value={alias}
            onChange={(e) => onAliasChange(e.target.value)}
            onKeyDown={(e) => e.key === "Enter" && alias.trim() && onSave()}
            className="input input-bordered bg-base-100 text-base-content placeholder:text-base-content/40 focus:outline-primary"
          />
        </div>

        <div className="modal-action mt-0">
          <button onClick={onClose} className="btn btn-ghost btn-sm">
            Cancel
          </button>
          <button
            onClick={onSave}
            disabled={!alias.trim() || isSaving}
            className="btn btn-primary btn-sm gap-2"
          >
            {isSaving ? (
              <span className="loading loading-spinner loading-xs" />
            ) : (
              <BookmarkIcon className="w-4 h-4" />
            )}
            Save Search
          </button>
        </div>
      </div>
      <div className="modal-backdrop bg-black/40" onClick={onClose} />
    </div>
  );
}