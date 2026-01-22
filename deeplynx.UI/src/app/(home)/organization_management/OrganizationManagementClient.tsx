// src/app/(home)/organization_management/OrganizationManagementClient.tsx
"use client";

import { useLanguage } from "@/app/contexts/Language";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import { useState } from "react";
import Tabs from "../components/Tabs";
import {
  GroupResponseDto,
  PermissionResponseDto,
  ProjectResponseDto,
  RoleResponseDto,
  UserResponseDto,
} from "../types/responseDTOs";
import InlineGroupsTable from "./groups/InlineGroupsTable";
import RolesAndPermissions from "./roles_and_permissions/RolesAndPermissions";
import OrganizationSettings from "./settings/OrganizationSettings";
import TagManagementClient from "./tag_management/TagManagementClient";
import UsersTable from "./users/UsersTable";

interface OrganizationManagementProps {
  members: UserResponseDto[];
  initialProjects: ProjectResponseDto[];
  initialGroups: GroupResponseDto[];
  initialRoles: RoleResponseDto[];
  initialSelectedProject?: ProjectResponseDto;
  initialPermissions: PermissionResponseDto[];
}

const OrganizationManagementClient = ({
  members,
  initialGroups,
  initialRoles,
  initialPermissions,
  initialProjects,
}: OrganizationManagementProps) => {
  const [activeTab, setActiveTab] = useState("");
  const { t } = useLanguage();
  const { organization } = useOrganizationSession();

  const tabData = [
    {
      label: t.translations.USERS,
      content: <UsersTable members={members} />,
    },
    {
      label: t.translations.ROLES_AND_PERMISSIONS,
      content: (
        <RolesAndPermissions
          initialRoles={initialRoles}
          initialPermissions={initialPermissions}
        />
      ),
    },
    {
      label: t.translations.GROUPS,
      content: (
        <InlineGroupsTable
          initialGroups={initialGroups}
          availableUsers={members}
          organizationId={organization?.organizationId}
        />
      ),
    },
    {
      label: t.translations.TAGS_AND_SECURITY_LABELS,
      content: <TagManagementClient projects={initialProjects} />,
    },
    {
      label: t.translations.SETTINGS,
      content: organization ? (
        <OrganizationSettings />
      ) : (
        <div>{t.translations.NO_ORG_SELECTED}</div>
      ),
    },
  ];

  const handleTabChange = (label: string) => {
    setActiveTab(label);
  };

  return (
    <div>
      <div className="bg-base-200/40 pl-12 p-6">
        <h1 className="text-2xl font-bold text-base-content">
          {t.translations.ORGANIZATION_MANAGEMENT}
        </h1>
      </div>
      <div className="p-2">
        <Tabs
          tabs={tabData}
          className="tabs tabs-border ml-5"
          onTabChange={handleTabChange}
          activeTab={activeTab}
        />
      </div>
    </div>
  );
};

export default OrganizationManagementClient;
