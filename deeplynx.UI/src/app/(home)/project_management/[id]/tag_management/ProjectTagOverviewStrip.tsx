import { useLanguage } from "@/app/contexts/Language";
import { InformationCircleIcon, TagIcon } from "@heroicons/react/24/outline";
import React from "react";

interface Props {
  inheritedOrganizationTagCount: number;
  projectManagedTagCount: number;
}

const ProjectTagOverviewStrip: React.FC<Props> = ({
  inheritedOrganizationTagCount,
  projectManagedTagCount,
}) => {
  const { t } = useLanguage();

  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 mb-6 max-w-2xl">
      <div className="stat bg-base-100 border border-base-300/50 rounded-xl">
        <div className="stat-title flex items-center gap-1 text-xs">
          <TagIcon className="w-4 h-4 text-primary" />
          {t.translations.ORGANIZATION_TAGS}
        </div>
        <div className="stat-value text-primary text-xl">
          {inheritedOrganizationTagCount}
        </div>
        <div className="stat-desc text-xs text-base-content/70 flex items-center gap-1">
          <InformationCircleIcon className="w-4 h-4" />
          <span>{t.translations.INHERITED_FROM_ORGANIZATION}</span>
        </div>
      </div>

      <div className="stat bg-base-100 border border-base-300/50 rounded-xl">
        <div className="stat-title flex items-center gap-1 text-xs">
          <TagIcon className="w-4 h-4 text-secondary" />
          {t.translations.PROJECT_TAGS}
        </div>
        <div className="stat-value text-secondary text-xl">
          {projectManagedTagCount}
        </div>
      </div>
    </div>
  );
};

export default ProjectTagOverviewStrip;
