// src/app/(home)/record/components/RecordTagPanel.tsx

"use client";

import React from "react";
import { TagIcon, XMarkIcon } from "@heroicons/react/24/outline";

import TagButton from "./TagButton";
import type { TagResponseDto } from "@/app/(home)/types/responseDTOs";

interface Props {
  // Data
  tags: TagResponseDto[];
  selectedTags: TagResponseDto[];
  selectedIds: string[];

  // Callbacks
  onSelectionChange: (selectedIds: string[]) => void;
  onRemoveTag: (tagId: number) => void;

  // Tag mutation helpers (for TagButton)
  projectId: number;
  recordId: number;
  setTags: React.Dispatch<React.SetStateAction<TagResponseDto[]>>;
  setSelectedTags: React.Dispatch<React.SetStateAction<TagResponseDto[]>>;
  setSelectedIds: React.Dispatch<React.SetStateAction<string[]>>;

  // Translations
  title: string;
}

const RecordTagsPanel: React.FC<Props> = ({
  tags,
  selectedTags,
  selectedIds,
  onSelectionChange,
  onRemoveTag,
  projectId,
  recordId,
  setTags,
  setSelectedTags,
  setSelectedIds,
  title,
}) => {
  return (
    <div className="card bg-base-100 shadow-lg">
      <div className="card-body">
        {/* Header + Controls */}
        <div className="flex items-start justify-between gap-4 mb-3">
          <div className="flex-1">
            <div className="flex items-center gap-2">
              <TagIcon className="w-5 h-5 text-secondary" />
              <h3 className="font-semibold text-base">{title}</h3>
            </div>
          </div>

          <div className="flex flex-col items-end gap-2">
            {/* Tag picker / creator */}
            <TagButton
              tags={tags}
              onSelectionChange={onSelectionChange}
              projectId={projectId}
              recordId={recordId}
              selectedIds={selectedIds}
              setSelectedIds={setSelectedIds}
              setTags={setTags}
              setSelectedTags={setSelectedTags}
            />
          </div>
        </div>

        {/* Tag list - one row per tag */}
        <div className="space-y-2 max-h-48 overflow-y-auto rounded-lg px-3 py-2">
          {selectedTags.length === 0 ? (
            <div className="py-4 text-center text-xs text-base-content/60">
              No tags attached to this record yet. Use the selector above to add
              tags.
            </div>
          ) : (
            selectedTags.map((tag) => (
              <div
                key={tag.id}
                className="flex items-center justify-between gap-3 bg-base-200/60 hover:bg-base-200 rounded-lg px-3 py-1.5"
              >
                {/* Left: tag badge / label */}
                <div className="flex items-center gap-2">
                  <span className="badge badge-secondary badge-outline badge-sm">
                    {tag.name}
                  </span>
                </div>

                {/* Right: remove button */}
                {tag.id != null && (
                  <button
                    type="button"
                    className="btn btn-ghost btn-xs text-error gap-1"
                    onClick={() => onRemoveTag(tag.id as number)}
                  >
                    <XMarkIcon className="w-3 h-3" />
                    <span className="hidden sm:inline">Remove</span>
                  </button>
                )}
              </div>
            ))
          )}
        </div>
      </div>
    </div>
  );
};

export default RecordTagsPanel;
