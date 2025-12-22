"use client";

import React, { useState } from "react";
import Tabs from "../components/Tabs";
import {
  GroupResponseDto,
  ProjectResponseDto,
  UserResponseDto,
  RoleResponseDto,
  PermissionResponseDto,
} from "../types/responseDTOs";
import UsersTable from "../components/users/UsersTable";
import { useLanguage } from "@/app/contexts/Language";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import InlineGroupsTable from "./groups/InlineGroupsTable";
import RolesAndPermissions from "./roles_and_permissions/RolesAndPermissions";
import OrganizationSettings from "./settings/OrganizationSettings";
import TagManagementClient from "./tag_management/TagManagementClient";
import OptionThree from "./tag_management/OptionThree";
import SettingsOne from "./settings/SettingsOne";
import SettingsTwo from "./settings/SettingsTwo";
import SettingsThree from "./settings/SettingsThree";
import { getAllGroups } from "@/app/lib/client_service/group_services.client"; // Add this import

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
  const [groups, setGroups] = useState<GroupResponseDto[]>(initialGroups);
  const { t } = useLanguage();
  const { organization } = useOrganizationSession();

  const refreshGroups = async () => {
    if (!organization?.organizationId) return;

    try {
      const updatedGroups = await getAllGroups(
        organization.organizationId as number
      );
      setGroups(updatedGroups);
    } catch (err) {
      console.error("Failed to refresh groups:", err);
    }
  };

  const tabData = [
    {
      label: t.translations.USERS,
      content: (
        <UsersTable
          initialMembers={members}
          header={"Organization Users"}
          description={
            "Manage users in your organization. Invite new users via email or add them directly."
          }
        />
      ),
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
          initialGroups={groups}
          availableUsers={members}
          organizationId={organization?.organizationId}
          onGroupsChange={refreshGroups}
        />
      ),
    },
    {
      label: t.translations.TAGS_AND_SECURITY_LABELS,
      content: <OptionThree projects={initialProjects} />,
    },
    {
      label: t.translations.SETTINGS,
      content: organization ? (
        <OrganizationSettings organization={organization} />
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
