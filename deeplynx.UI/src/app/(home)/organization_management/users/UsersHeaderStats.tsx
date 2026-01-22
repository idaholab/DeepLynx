// src/app/(home)/organization_management/users/UsersHeaderStats.tsx

import React from "react";
import {
  EnvelopeIcon,
  UserGroupIcon,
  UserIcon,
} from "@heroicons/react/24/outline";
import { useLanguage } from "@/app/contexts/Language";

/* -------------------------------------------------------------------------- */
/*                             Header & Stats Block                           */
/* -------------------------------------------------------------------------- */

interface UsersHeaderStatsProps {
  activeUserCount: number;
  pendingCount: number;
  totalCount: number;
  loading: boolean;
  onInviteClick: () => void;
}

const UsersHeaderStats: React.FC<UsersHeaderStatsProps> = ({
  activeUserCount,
  pendingCount,
  totalCount,
  loading,
  onInviteClick,
}) => {
  const { t } = useLanguage();
  return (
    <>
      {/* Header */}
      <div className="flex justify-between items-center mb-6">
        <div>
          <h2 className="text-2xl font-bold">
            {t.translations.ORGANIZATION_USERS}
          </h2>
          <p className="text-base-content/70 text-sm mt-1">
            {t.translations.MANAGE_USERS_IN_ORG_DESCRIPTION}
          </p>
        </div>
        <button
          className="btn btn-primary gap-2"
          onClick={onInviteClick}
          disabled={loading}
        >
          <UserIcon className="w-5 h-5" />
          {t.translations.INVITE_USER}
        </button>
      </div>

      {/* Stats Cards */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mb-6">
        <div className="stat bg-base-200 rounded-lg">
          <div className="stat-figure text-primary">
            <UserIcon className="w-8 h-8" />
          </div>
          <div className="stat-title">{t.translations.ACTIVE_USERS}</div>
          <div className="stat-value text-primary">{activeUserCount}</div>
          <div className="stat-desc">
            {t.translations.USER_WITH_ACTIVE_ACCESS}
          </div>
        </div>
        <div className="stat bg-base-200 rounded-lg">
          <div className="stat-figure text-warning">
            <EnvelopeIcon className="w-8 h-8" />
          </div>
          <div className="stat-title">{t.translations.PENDING_INVITES}</div>
          <div className="stat-value text-warning">{pendingCount}</div>
          <div className="stat-desc">{t.translations.AWAITING_ACCEPTANCE}</div>
        </div>
        <div className="stat bg-base-200 rounded-lg">
          <div className="stat-figure text-secondary">
            <UserGroupIcon className="w-8 h-8" />
          </div>
          <div className="stat-title">{t.translations.TOTAL}</div>
          <div className="stat-value text-secondary">{totalCount}</div>
          <div className="stat-desc">{t.translations.ACTIVE_PLUS_PENDING}</div>
        </div>
      </div>
    </>
  );
};

export default UsersHeaderStats;
