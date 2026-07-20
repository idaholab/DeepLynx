// src/app/(home)/organization_management/OrganizationManagementClient.tsx
"use client";

import Tabs from "@/app/(home)/components/Tabs";
import {
  GroupResponseDto,
  PermissionResponseDto,
  ProjectResponseDto,
  RoleResponseDto,
  SensitivityLabelsDto,
  UserResponseDto,
} from "@/app/(home)/types/responseDTOs";
import { useLanguage } from "@/app/contexts/Language";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import { useEffect, useState } from "react";
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
  initialLabels: SensitivityLabelsDto[];
}

const OrganizationManagementClient = ({
  members,
  initialGroups,
  initialRoles,
  initialPermissions,
  initialProjects,
  initialLabels,
}: OrganizationManagementProps) => {
  const [activeTab, setActiveTab] = useState("");
  const { t } = useLanguage();
  const { organization } = useOrganizationSession();
  const [labels, setLabels] = useState<SensitivityLabelsDto[]>(initialLabels);
  const [permissions, setPermissions] =
    useState<PermissionResponseDto[]>(initialPermissions);

  useEffect(() => {
    setLabels(initialLabels);
  }, [initialLabels]);

  useEffect(() => {
    setPermissions(initialPermissions);
  }, [initialPermissions]);

  const tabData = [
    {
      label: t.translations.USERS,
      content: <UsersTable members={members} scope="org" />,
    },
    {
      label: t.translations.ROLES_AND_PERMISSIONS,
      content: (
        <RolesAndPermissions
          initialRoles={initialRoles}
          initialPermissions={permissions}
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
      content: (
        <TagManagementClient
          projects={initialProjects}
          initialLabels={labels}
        />
      ),
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
    <main className="min-h-screen bg-base-200/30">
      <section className="border-b border-base-300 bg-base-100">
        <div className="mx-auto flex w-full max-w-7xl flex-col gap-5 px-3 py-5 sm:px-6 lg:px-8">
          <div>
            <p className="text-xs font-semibold uppercase tracking-wide text-base-content/60">
              {t.translations.ORGANIZATION}
            </p>
            <h1 className="text-2xl font-bold text-base-content sm:text-3xl">
              {t.translations.ORGANIZATION_MANAGEMENT}
            </h1>
          </div>
        </div>
      </section>

      <section className="mx-auto w-full max-w-7xl px-3 py-5 sm:px-6 lg:px-8">
        <Tabs
          tabs={tabData}
          className="mx-0"
          onTabChange={handleTabChange}
          activeTab={activeTab}
        />
      </section>
    </main>
  );
};

export default OrganizationManagementClient;
