import React from "react";
import {
  ShieldCheckIcon,
  TagIcon,
  LockClosedIcon,
  LockOpenIcon,
  InformationCircleIcon,
} from "@heroicons/react/24/outline";

interface Props {
  labelCount: number;
  projectsWithLabels: number;
  tagCount: number;
  projectsWithTags: number;
  tagsLocked: boolean;
  labelsLocked: boolean;
}

const TagOverviewStrip: React.FC<Props> = ({
  labelCount,
  projectsWithLabels,
  tagCount,
  projectsWithTags,
  tagsLocked,
  labelsLocked,
}) => {
  return (
    <div className="grid grid-cols-1 md:grid-cols-4 gap-3 mb-6">
      {/* Security Labels */}
      <div className="stat bg-base-100 shadow-lg rounded-xl">
        <div className="stat-title flex items-center gap-1 text-xs">
          <ShieldCheckIcon className="w-4 h-4 text-secondary" />
          Org Security Labels
        </div>
        <div className="stat-value text-secondary text-xl">{labelCount}</div>
        <div className="stat-desc text-xs flex items-center gap-1">
          {labelsLocked ? (
            <>
              <LockClosedIcon className="w-4 h-4 text-error" />
              <span>Locked for all projects</span>
            </>
          ) : (
            <>
              <LockOpenIcon className="w-4 h-4 text-success" />
              <span>Projects may define their own</span>
            </>
          )}
        </div>
      </div>

      {/* Projects with Labels */}
      <div className="stat bg-base-100 shadow-lg rounded-xl">
        <div className="stat-title flex items-center gap-1 text-xs">
          <ShieldCheckIcon className="w-4 h-4 text-secondary" />
          Projects with Labels
        </div>
        <div className="stat-value text-secondary text-xl">
          {projectsWithLabels}
        </div>
        <div className="stat-desc text-xs text-base-content/70 flex items-center gap-1">
          <InformationCircleIcon className="w-4 h-4" />
          <span>Project usage tracking coming soon</span>
        </div>
      </div>

      {/* Org Tags */}
      <div className="stat bg-base-100 shadow-lg rounded-xl">
        <div className="stat-title flex items-center gap-1 text-xs">
          <TagIcon className="w-4 h-4 text-primary" />
          Org Tags
        </div>
        <div className="stat-value text-primary text-xl">{tagCount}</div>
        <div className="stat-desc text-xs flex items-center gap-1">
          {tagsLocked ? (
            <>
              <LockClosedIcon className="w-4 h-4 text-error" />
              <span>Locked for all projects</span>
            </>
          ) : (
            <>
              <LockOpenIcon className="w-4 h-4 text-success" />
              <span>Projects may define their own</span>
            </>
          )}
        </div>
      </div>

      {/* Projects with Tags */}
      <div className="stat bg-base-100 shadow-lg rounded-xl">
        <div className="stat-title flex items-center gap-1 text-xs">
          <TagIcon className="w-4 h-4 text-secondary" />
          Projects with Tags
        </div>
        <div className="stat-value text-secondary text-xl">
          {projectsWithTags}
        </div>
        <div className="stat-desc text-xs text-base-content/70">
          Inheriting organization-level tags
        </div>
      </div>
    </div>
  );
};

export default TagOverviewStrip;
