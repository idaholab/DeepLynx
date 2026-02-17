import React from "react";
import {
  TagIcon,
  LockClosedIcon,
  LockOpenIcon,
  MagnifyingGlassIcon,
  InformationCircleIcon,
} from "@heroicons/react/24/outline";
import type { TagResponseDto } from "@/app/(home)/types/responseDTOs";
import { useLanguage } from "@/app/contexts/Language";

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
  const { t } = useLanguage();
  return (
    <div className="card bg-base-100 shadow-lg">
      <div className="card-body">
        {/* Header + Controls */}
        <div className="flex items-start justify-between gap-4 mb-3">
          <div className="flex-1">
            <div className="flex items-center gap-2">
              <TagIcon className="w-5 h-5 text-secondary" />
              <h3 className="font-semibold text-base">
                {t.translations.PROJECT_TAGS}
              </h3>
            </div>
            <p className="text-xs text-base-content/70 mt-1 max-w-md">
              {t.translations.PROJECT_TAGS_DESCRIPTION}
            </p>
          </div>

          <div className="flex flex-col items-end gap-2">
            {/* Search input */}
            <div className="form-control w-40">
              <div className="input input-xs input-bordered flex items-center gap-1 px-2">
                <MagnifyingGlassIcon className="w-3 h-3 text-base-content/60" />
                <input
                  type="text"
                  className="grow text-[0.7rem] bg-transparent focus:outline-none"
                  placeholder={t.translations.SEARCH_TAGS}
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
                  ? t.translations.NO_PROJECT_SELECTED
                  : orgTagsLocked
                    ? t.translations.TAGS_LOCKED_AT_ORG_LEVEL
                    : t.translations.CREATE_NEW_PROJECT_TAG
              }
            >
              + {t.translations.NEW_TAG}
            </button>
          </div>
        </div>

        {/* n of m line */}
        <div className="flex justify-between items-center mb-3 text-[0.7rem] text-base-content/60">
          <span>
            {t.translations.SHOWING}{" "}
            <span className="font-semibold">{filteredCount}</span>{" "}
            {t.translations.OF}{" "}
            <span className="font-semibold">{tagCount}</span>{" "}
            {t.translations.PROJECT_TAGS_LOWER}
          </span>
          {tagSearch.trim() && (
            <span className="italic">
              {t.translations.FILTERED_BY}
              <span className="font-medium break-all">{tagSearch}</span>
            </span>
          )}
        </div>

        {/* Tag list */}
        <div className="space-y-2 max-h-72 overflow-y-auto">
          {tagsLoading ? (
            <div className="py-6 text-center text-xs text-base-content/60">
              {t.translations.LOADING_PROJECT_TAGS}
            </div>
          ) : tagsError ? (
            <div className="py-6 text-center text-xs text-error">
              {tagsError}
            </div>
          ) : filteredTags.length === 0 ? (
            <div className="py-6 text-center text-xs text-base-content/60 border border-dashed border-base-300 rounded-lg">
              {tagSearch.trim()
                ? t.translations.NO_PROJECT_TAGS_MATCH_SEARCH
                : t.translations.NO_PROJECT_TAGS_DEFINED_WHEN_UNLOCKED}
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
                      ({t.translations.ORGANIZATION_TAG})
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
                        ? t.translations.TAGS_LOCKED_BY_ORGANIZATION
                        : t.translations.EDIT
                    }
                  >
                    {t.translations.EDIT}
                  </button>
                  <button
                    type="button"
                    className="btn btn-ghost btn-xs text-error"
                    onClick={() => onArchiveClick(tag)}
                    disabled={orgTagsLocked || archivingTagId === tag.id}
                    title={
                      orgTagsLocked
                        ? t.translations.TAGS_LOCKED_BY_ORGANIZATION
                        : t.translations.ARCHIVE_SOFT_DELETE_TAG
                    }
                  >
                    {archivingTagId === tag.id
                      ? t.translations.ARCHIVING
                      : t.translations.DELETE}
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
