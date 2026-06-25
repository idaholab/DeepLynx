// src/app/(home)/project_management/[id]/users/ProjectUsersHeader.tsx

import React from "react";
import { useLanguage } from "@/app/contexts/Language";
import {
  UserPlusIcon,
  UserGroupIcon,
  UsersIcon,
  UserIcon,
} from "@heroicons/react/24/outline";

/* -------------------------------------------------------------------------- */
/*                        Project Users Header Component                      */
/* -------------------------------------------------------------------------- */

interface ProjectUsersHeaderProps {
  totalMembers: number;
  userCount: number;
  groupCount: number;
  loading: boolean;
  onInviteUser: () => void;
  onAddGroup: () => void;
}

const ProjectUsersHeader: React.FC<ProjectUsersHeaderProps> = ({
  totalMembers,
  userCount,
  groupCount,
  loading,
  onInviteUser,
  onAddGroup,
}) => {
  const { t } = useLanguage();
  return (
    <>
      {/* Header */}
      <div className="flex justify-between items-center mb-6 border-b border-base-300/50 pb-4">
        <div>
          <h2 className="text-2xl font-bold">
            {t.translations.PROJECT_MEMBERS}
          </h2>
          <p className="text-base-content/70 text-sm mt-1">
            {t.translations.MANAGE_USERS_AND_GROUPS_WITH_ACCESS_TO_THIS_PROJECT}
          </p>
        </div>
        <div className="flex gap-2">
          <button
            className="btn btn-primary gap-2"
            onClick={onInviteUser}
            disabled={loading}
          >
            <UserPlusIcon className="w-5 h-5" />
            {t.translations.ADD_USERS}
          </button>
          <button
            className="btn btn-outline btn-primary gap-2"
            onClick={onAddGroup}
            disabled={loading}
          >
            <UserGroupIcon className="w-5 h-5" />
            {t.translations.ADD_GROUPS}
          </button>
        </div>
      </div>

      {/* Stats Cards */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mb-6">
        <div className="stat bg-base-200 rounded-lg">
          <div className="stat-figure text-primary">
            <UsersIcon className="w-8 h-8" />
          </div>
          <div className="stat-title text-primary">
            {t.translations.TOTAL_MEMBERS}
          </div>
          <div className="stat-value text-primary">{totalMembers}</div>
          <div className="stat-desc text-primary">
            {t.translations.USERS_PLUS_GROUP}
          </div>
        </div>
        <div className="stat bg-base-200 rounded-lg">
          <div className="stat-figure text-primary">
            <UserIcon className="w-8 h-8" />
          </div>
          <div className="stat-title text-primary">{t.translations.USERS}</div>
          <div className="stat-value text-primary">{userCount}</div>
          <div className="stat-desc text-primary">
            {t.translations.INDIVIDUAL_MEMBERS}
          </div>
        </div>
        <div className="stat bg-base-200 rounded-lg">
          <div className="stat-figure text-primary">
            <UserGroupIcon className="w-8 h-8" />
          </div>
          <div className="stat-title text-primary">{t.translations.GROUPS}</div>
          <div className="stat-value text-primary">{groupCount}</div>
          <div className="stat-desc text-primary">
            {t.translations.GROUP_MEMBERSHIP}
          </div>
        </div>
      </div>
    </>
  );
};

export default ProjectUsersHeader;