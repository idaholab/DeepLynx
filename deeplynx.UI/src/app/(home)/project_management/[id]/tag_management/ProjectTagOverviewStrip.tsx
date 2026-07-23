import { useLanguage } from "@/app/contexts/Language";
import {
  InformationCircleIcon,
  ShieldCheckIcon,
  TagIcon,
} from "@heroicons/react/24/outline";
import React from "react";

interface Props {
  inheritedOrganizationLabelCount: number;
  projectManagedLabelCount: number;
  inheritedOrganizationTagCount: number;
  projectManagedTagCount: number;
  organizationTagsLocked: boolean;
  organizationLabelsLocked: boolean;
}

const ProjectTagOverviewStrip: React.FC<Props> = ({
  inheritedOrganizationLabelCount,
  projectManagedLabelCount,
  inheritedOrganizationTagCount,
  projectManagedTagCount,
  organizationTagsLocked,
  organizationLabelsLocked,
}) => {
  const { t } = useLanguage();

  return (
    <div className="grid grid-cols-1 md:grid-cols-4 gap-3 mb-6">
      <div className="stat bg-base-100 border border-base-300/50 rounded-xl">
        <div className="stat-title flex items-center gap-1 text-xs">
          <ShieldCheckIcon className="w-4 h-4 text-secondary" />
          {t.translations.ORGANIZATION_SECURITY_LABELS}
        </div>
        <div className="stat-value text-secondary text-xl">
          {inheritedOrganizationLabelCount}
        </div>
        <div className="stat-desc text-xs text-base-content/70 flex items-center gap-1">
          <InformationCircleIcon className="w-4 h-4" />
          <span>{t.translations.INHERITED_FROM_ORGANIZATION}</span>
        </div>
      </div>

      <div className="stat bg-base-100 border border-base-300/50 rounded-xl">
        <div className="stat-title flex items-center gap-1 text-xs">
          <ShieldCheckIcon className="w-4 h-4 text-secondary" />
          {t.translations.PROJECT_SECURITY_LABELS}
        </div>
        <div className="stat-value text-secondary text-xl">
          {projectManagedLabelCount}
        </div>
      </div>

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
