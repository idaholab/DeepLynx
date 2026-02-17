import React from "react";
import {
  ShieldCheckIcon,
  TagIcon,
  LockClosedIcon,
  LockOpenIcon,
  InformationCircleIcon,
} from "@heroicons/react/24/outline";
import { useLanguage } from "@/app/contexts/Language";

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
  const { t } = useLanguage();
  return (
    <div className="grid grid-cols-1 md:grid-cols-4 gap-3 mb-6">
      {/* Security Labels */}
      <div className="stat bg-base-100 shadow-lg rounded-xl">
        <div className="stat-title flex items-center gap-1 text-xs">
          <ShieldCheckIcon className="w-4 h-4 text-secondary" />
          {t.translations.ORG_SECURITY_LABELS}
        </div>
        <div className="stat-value text-secondary text-xl">{labelCount}</div>
        <div className="stat-desc text-xs flex items-center gap-1">
          {labelsLocked ? (
            <>
              <LockClosedIcon className="w-4 h-4 text-error" />
              <span>{t.translations.LOCKED_FOR_ALL_PROJECTS}</span>
            </>
          ) : (
            <>
              <LockOpenIcon className="w-4 h-4 text-success" />
              <span>{t.translations.PROJECTS_MAY_DEFINE_THEIR_OWN}</span>
            </>
          )}
        </div>
      </div>

      {/* Projects with Labels */}
      <div className="stat bg-base-100 shadow-lg rounded-xl">
        <div className="stat-title flex items-center gap-1 text-xs">
          <ShieldCheckIcon className="w-4 h-4 text-secondary" />
          {t.translations.PROJECTS_WITH_LABELS}
        </div>
        <div className="stat-value text-secondary text-xl">
          {projectsWithLabels}
        </div>
        <div className="stat-desc text-xs text-base-content/70 flex items-center gap-1">
          <InformationCircleIcon className="w-4 h-4" />
          <span>{t.translations.PROJECT_USAGE_TRACKING_COMING_SOON}</span>
        </div>
      </div>

      {/* Org Tags */}
      <div className="stat bg-base-100 shadow-lg rounded-xl">
        <div className="stat-title flex items-center gap-1 text-xs">
          <TagIcon className="w-4 h-4 text-primary" />
          {t.translations.ORG_TAGS}
        </div>
        <div className="stat-value text-primary text-xl">{tagCount}</div>
        <div className="stat-desc text-xs flex items-center gap-1">
          {tagsLocked ? (
            <>
              <LockClosedIcon className="w-4 h-4 text-error" />
              <span>{t.translations.LOCKED_FOR_ALL_PROJECTS}</span>
            </>
          ) : (
            <>
              <LockOpenIcon className="w-4 h-4 text-success" />
              <span>{t.translations.PROJECTS_MAY_DEFINE_THEIR_OWN}</span>
            </>
          )}
        </div>
      </div>

      {/* Projects with Tags */}
      <div className="stat bg-base-100 shadow-lg rounded-xl">
        <div className="stat-title flex items-center gap-1 text-xs">
          <TagIcon className="w-4 h-4 text-secondary" />
          {t.translations.PROJECTS_WITH_TAGS}
        </div>
        <div className="stat-value text-secondary text-xl">
          {projectsWithTags}
        </div>
        <div className="stat-desc text-xs text-base-content/70">
          {t.translations.INHERITING_ORGANIZATION_LEVEL_TAGS}
        </div>
      </div>
    </div>
  );
};

export default TagOverviewStrip;
