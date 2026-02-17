import React from "react";
import {
  TagIcon,
  LockClosedIcon,
  LockOpenIcon,
  MagnifyingGlassIcon,
  InformationCircleIcon,
} from "@heroicons/react/24/outline";
import type { TagResponseDto } from "@/app/(home)/types/responseDTOs";

interface Props {
  tags: TagResponseDto[];
  orgTagsLocked: boolean;
  tagsLoading: boolean;
  tagsError: string | null;
  filteredTags: TagResponseDto[];
  tagSearch: string;
  setTagSearch: (value: string) => void;
  filteredCount: number;
  tagCount: number;
  projectId?: number;
  archivingTagId: number | null;
  onCreateTag: () => void;
  onEditTag: (id: number) => void;
  onArchiveClick: (tag: TagResponseDto) => void;
}

const ProjectTagsPanel: React.FC<Props> = ({
  orgTagsLocked,
  tagsLoading,
  tagsError,
  filteredTags,
  tagSearch,
  setTagSearch,
  filteredCount,
  tagCount,
  projectId,
  archivingTagId,
  onCreateTag,
  onEditTag,
  onArchiveClick,
}) => {
  return (
    <div className="card bg-base-100 shadow-lg">
      <div className="card-body">
        {/* Header + Controls */}
        <div className="flex items-start justify-between gap-4 mb-3">
          <div className="flex-1">
            <div className="flex items-center gap-2">
              <TagIcon className="w-5 h-5 text-secondary" />
              <h3 className="font-semibold text-base">Project Tags</h3>
            </div>
            <p className="text-xs text-base-content/70 mt-1 max-w-md">
              Tags for classification, workflows, and search at the project
              level. This project always inherits the tags defined at the
              organization level and may define additional tags when not locked.
            </p>
          </div>

          <div className="flex flex-col items-end gap-2">
            {/* Lock indicator (read-only, controlled by org) */}
            <button
              type="button"
              className={`btn btn-xs gap-1 ${
                orgTagsLocked ? "btn-error" : "btn-ghost"
              }`}
              disabled
            >
              {orgTagsLocked ? (
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
                  placeholder="Search tags..."
                  value={tagSearch}
                  onChange={(e) => setTagSearch(e.target.value)}
                />
              </div>
            </div>

            {/* Add button */}
            <button
              type="button"
              className="btn btn-primary btn-xs gap-1"
              onClick={onCreateTag}
              disabled={orgTagsLocked || !projectId}
              title={
                !projectId
                  ? "No project selected"
                  : orgTagsLocked
                    ? "Tags are locked at the organization level"
                    : "Create new project tag"
              }
            >
              + New Tag
            </button>
          </div>
        </div>

        {/* n of m line */}
        <div className="flex justify-between items-center mb-3 text-[0.7rem] text-base-content/60">
          <span>
            Showing <span className="font-semibold">{filteredCount}</span> of{" "}
            <span className="font-semibold">{tagCount}</span> project tags
          </span>
          {tagSearch.trim() && (
            <span className="italic">
              Filtered by:{" "}
              <span className="font-medium break-all">{tagSearch}</span>
            </span>
          )}
        </div>

        {/* Info text */}
        <div className="flex items-start gap-2 mb-3 text-xs text-base-content/70">
          <InformationCircleIcon className="w-4 h-4" />
          <p>
            When tags are{" "}
            <span className="font-semibold">
              locked at the organization level
            </span>
            , project administrators{" "}
            <span className="font-semibold">
              cannot define additional project tags
            </span>{" "}
            and must use only the tags defined at the organization level.
          </p>
        </div>

        {/* Tag list */}
        <div className="space-y-2 max-h-72 overflow-y-auto">
          {tagsLoading ? (
            <div className="py-6 text-center text-xs text-base-content/60">
              Loading project tags…
            </div>
          ) : tagsError ? (
            <div className="py-6 text-center text-xs text-error">
              {tagsError}
            </div>
          ) : filteredTags.length === 0 ? (
            <div className="py-6 text-center text-xs text-base-content/60 border border-dashed border-base-300 rounded-lg">
              {tagSearch.trim()
                ? "No project tags match your search."
                : "No project tags defined. When unlocked, you can extend the organization tag set with project-specific tags."}
            </div>
          ) : (
            filteredTags.map((tag) => (
              <div
                key={tag.id}
                className="flex items-center justify-between bg-base-200/70 hover:bg-base-300/80 transition rounded-lg px-3 py-2"
              >
                <div className="flex items-center gap-2">
                  <span className="badge badge-secondary badge-outline badge-sm">
                    {tag.name}
                  </span>
                  {!tag.projectId && (
                    <span className="text-[0.7rem] text-base-content/70">
                      (Organization Tag)
                    </span>
                  )}
                </div>
                <div className="flex items-center gap-1">
                  <button
                    type="button"
                    className="btn btn-ghost btn-xs"
                    onClick={() => onEditTag(tag.id)}
                    disabled={orgTagsLocked}
                    title={
                      orgTagsLocked
                        ? "Tags are locked by the organization"
                        : "Edit"
                    }
                  >
                    Edit
                  </button>
                  <button
                    type="button"
                    className="btn btn-ghost btn-xs text-error"
                    onClick={() => onArchiveClick(tag)}
                    disabled={orgTagsLocked || archivingTagId === tag.id}
                    title={
                      orgTagsLocked
                        ? "Tags are locked by the organization"
                        : "Archive (soft delete) tag"
                    }
                  >
                    {archivingTagId === tag.id ? "Archiving..." : "Delete"}
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

export default ProjectTagsPanel;
