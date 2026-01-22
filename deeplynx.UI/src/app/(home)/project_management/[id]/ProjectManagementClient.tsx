// src/app/(home)/project_management/[id]/ProjectManagementClient.tsx

"use client";

import React, { useState } from "react";
import Tabs from "@/app/(home)/components/Tabs";
import {
  GroupResponseDto,
  ProjectResponseDto,
  RoleResponseDto,
  PermissionResponseDto,
  ProjectMemberResponseDto,
} from "@/app/(home)/types/responseDTOs";
import { useLanguage } from "@/app/contexts/Language";
import { useProjectSession } from "@/app/contexts/ProjectSessionProvider";
import ProjectUsersTable from "./users/ProjectUsersTable";
import ProjectRolesAndPermissions from "./roles_and_permissions/ProjectRolesAndPermissions";
import DataSources from "./data_source/DataSourcesClient";
import ProjectTagAndLabelManagementClient from "./tag_management/ProjectTagAndLabelManagementClient";
import ProjectSettings from "./settings/ProjectSettings";

interface ProjectManagementProps {
  project: ProjectResponseDto | null;
  projectMembers: ProjectMemberResponseDto[];
  projectGroups: GroupResponseDto[];
  projectRoles: RoleResponseDto[];
  projectPermissions: PermissionResponseDto[];
}

const ProjectManagementClient = ({
  project,
  projectMembers,
  projectGroups,
  projectRoles,
  projectPermissions,
}: ProjectManagementProps) => {
  const [activeTab, setActiveTab] = useState("");
  const { t } = useLanguage();
  const { project: sessionProject } = useProjectSession();

  const handleTabChange = (label: string) => {
    setActiveTab(label);
  };

  const tabData = [
    {
      label: t.translations.USERS,
      content: (
        <ProjectUsersTable
          members={projectMembers}
          roles={projectRoles}
          project={project}
        />
      ),
    },
    {
      label: t.translations.ROLES_AND_PERMISSIONS,
      content: (
        <ProjectRolesAndPermissions
          initialRoles={projectRoles}
          initialPermissions={projectPermissions}
          projectId={project?.id as number}
        />
      ),
    },
    {
      label: t.translations.DATA_SOURCES,
      content: <DataSources projectId={project?.id as number} />,
    },
    {
      label: t.translations.TAGS_AND_SECURITY_LABELS,
      content: (
        <ProjectTagAndLabelManagementClient
          project={project as ProjectResponseDto}
          orgTagsLocked={false}
        />
      ),
    },
    {
      label: t.translations.SETTINGS,
      content: <ProjectSettings project={project} />,
    },
  ];

  return (
    <>
      <div className="bg-base-200/40 pl-12 p-6">
        <h1 className="text-2xl font-bold text-base-content">
          {t.translations.PROJECT_MANAGEMENT || "Project Management"}
        </h1>
        {(project || sessionProject) && (
          <p className="text-sm text-base-content/70 mt-1">
            {t.translations.MANAGING_SETTINGS_FOR_PROJECT}:{" "}
            <span className="font-semibold">
              {project?.name || sessionProject?.projectName}
            </span>
          </p>
        )}
      </div>

      {/* Tabs */}
      <div className="p-2">
        <Tabs
          tabs={tabData}
          className="tabs tabs-border ml-5"
          onTabChange={handleTabChange}
          activeTab={activeTab}
        />
      </div>
    </>
  );
};

export default ProjectManagementClient;
