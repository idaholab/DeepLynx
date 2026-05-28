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
  sensitivityLabelCount: number;
  projectsWithSensitivityLabelsCount: number;
  organizationTagCount: number;
  projectsWithTagsCount: number;
  organizationTagsLocked: boolean;
  organizationLabelsLocked: boolean;
}

const OrganizationTagOverviewStrip: React.FC<Props> = ({
  sensitivityLabelCount,
  projectsWithSensitivityLabelsCount,
  organizationTagCount,
  projectsWithTagsCount,
  organizationTagsLocked,
  organizationLabelsLocked,
}) => {
  const { t } = useLanguage();

  return (
    <div className="grid grid-cols-1 md:grid-cols-4 gap-3 mb-6">
      <div className="stat bg-base-100 shadow-lg rounded-xl">
        <div className="stat-title flex items-center gap-1 text-xs">
          <ShieldCheckIcon className="w-4 h-4 text-secondary" />
          {t.translations.SENSITIVITY_LABELS}
        </div>
        <div className="stat-value text-secondary text-xl">
          {sensitivityLabelCount}
        </div>
        <div className="stat-desc text-xs flex items-center gap-1">
          {organizationLabelsLocked ? (
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
          <ShieldCheckIcon className="w-4 h-4 text-secondary" />
          {t.translations.PROJECTS_WITH_LABELS}
        </div>
        <div className="stat-value text-secondary text-xl">
          {projectsWithSensitivityLabelsCount}
        </div>
        <div className="stat-desc text-xs text-base-content/70 flex items-center gap-1">
          <span>{t.translations.INHERITING_ORGANIZATION_LEVEL_LABELS}</span>
        </div>
      </div>

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
