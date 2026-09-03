import React from "react";
import {
  TagIcon,
  LockClosedIcon,
  LockOpenIcon,
} from "@heroicons/react/24/outline";
import { useLanguage } from "@/app/contexts/Language";

interface Props {
  organizationTagCount: number;
  projectsWithTagsCount: number;
  organizationTagsLocked: boolean;
}

const OrganizationTagOverviewStrip: React.FC<Props> = ({
  organizationTagCount,
  projectsWithTagsCount,
  organizationTagsLocked,
}) => {
  const { t } = useLanguage();

  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 mb-6 max-w-2xl">
      <div className="stat bg-base-100 shadow-lg rounded-xl">
        <div className="stat-title flex items-center gap-1 text-xs">
          <TagIcon className="w-4 h-4 text-primary" />
          {t.translations.ORG_TAGS}
        </div>
        <div className="stat-value text-primary text-xl">
          {organizationTagCount}
        </div>
        <div className="stat-desc text-xs flex items-center gap-1">
          {organizationTagsLocked ? (
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

      <div className="stat bg-base-100 shadow-lg rounded-xl">
        <div className="stat-title flex items-center gap-1 text-xs">
          <TagIcon className="w-4 h-4 text-secondary" />
          {t.translations.PROJECTS_WITH_TAGS}
        </div>
        <div className="stat-value text-secondary text-xl">
          {projectsWithTagsCount}
        </div>
        <div className="stat-desc text-xs text-base-content/70">
          {t.translations.INHERITING_ORGANIZATION_LEVEL_TAGS}
        </div>
      </div>
    </div>
  );
};

export default OrganizationTagOverviewStrip;
