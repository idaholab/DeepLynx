// src/app/(home)/project_management/[id]/ProjectManagementClient.tsx

"use client";

import React, { useEffect, useState } from "react";
import Tabs from "@/app/(home)/components/Tabs";
import {
  GroupResponseDto,
  ProjectResponseDto,
  RoleResponseDto,
  PermissionResponseDto,
  ProjectMemberResponseDto,
  SensitivityLabelsDto,
} from "@/app/(home)/types/responseDTOs";
import { useLanguage } from "@/app/contexts/Language";
import { useProjectSession } from "@/app/contexts/ProjectSessionProvider";
import { ProjectAdminRoute } from "@/app/(home)/rbac/RBACComponents";
import ProjectUsersTable from "./users/ProjectUsersTable";
import ProjectRolesAndPermissions from "./roles_and_permissions/ProjectRolesAndPermissions";
import DataSources from "./data_source/DataSourcesClient";
import ProjectTagAndLabelManagementClient from "./tag_management/ProjectTagAndLabelManagementClient";
import ProjectSettings from "./settings/ProjectSettings";
import { getAllPermissions } from "@/app/lib/client_service/permission_services.client";
import { getAllSensitivityLabelsProject } from "@/app/lib/client_service/sensitivity_labels_services.client";

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
  const { project: sessionProject, setProject } = useProjectSession();
  const [editingProject, setEditingProject] = useState(project); 
  const [labels, setLabels] = useState<SensitivityLabelsDto[]>([]);
  const [permissions, setPermissions] = useState(projectPermissions);

  useEffect(() => {    
    if (!editingProject?.id || !editingProject?.name) {
      return;
    }

    loadLabels();

    if (
      sessionProject?.projectId?.toString() === editingProject.id.toString() &&
      sessionProject.projectName === editingProject.name
    ) {
      return;
    }

    setProject({
      projectId: editingProject.id.toString(),
      projectName: editingProject.name,
    });
  }, [editingProject?.id, editingProject?.name, sessionProject, setProject]);

  const handleTabChange = (label: string) => {
    setActiveTab(label);
  };

  const loadLabels = async () => {
    try {
      const dtoList: SensitivityLabelsDto[] = await getAllSensitivityLabelsProject(Number(project?.id));
      setLabels(dtoList);
    } catch(e) {
      console.error("getAllSensitivityLabels failed: ", e);
    }
  }

  const refreshLabels = async () => {
    loadLabels();
    try {
      const updatedPermissions = await getAllPermissions(Number(project?.organizationId), Number(project?.id), undefined, true);
      setPermissions(updatedPermissions);
    } catch (e) {
      console.error("getAllPermissions failed: ", e);
    }
  }

  const tabData = [
    {
      label: t.translations.USERS,
      content: (
        <ProjectUsersTable
          members={projectMembers}
          roles={projectRoles}
          project={editingProject}
        />
      ),
    },
    {
      label: t.translations.ROLES_AND_PERMISSIONS,
      content: (
        <ProjectRolesAndPermissions
          initialRoles={projectRoles}
          initialPermissions={permissions}
          projectId={editingProject?.id as number}
        />
      ),
    },
    {
      label: t.translations.DATA_SOURCES,
      content: <DataSources projectId={editingProject?.id as number} />,
    },
    {
      label: t.translations.TAGS_AND_SECURITY_LABELS,
      content: (
        <ProjectTagAndLabelManagementClient
          project={editingProject as ProjectResponseDto}
          orgTagsLocked={false}
          initialLabels={labels}
          refreshLabels={refreshLabels}
        />
      ),
    },
    {
      label: t.translations.SETTINGS,
      content: <ProjectSettings project={editingProject} setProject={setEditingProject} />,
    },
  ];

  return (
    <ProjectAdminRoute>
      <main className="min-h-screen bg-base-200/30">
        <section className="border-b border-base-300/50 bg-base-100">
          <div className="mx-auto flex w-full max-w-7xl flex-col gap-5 px-3 py-5 sm:px-6 lg:px-8">
            <div>
              <p className="text-xs font-semibold uppercase tracking-wide text-base-content/60">
                {t.translations.PROJECT}
              </p>
              <h1 className="text-2xl font-bold text-base-content sm:text-3xl">
                {t.translations.PROJECT_MANAGEMENT}
              </h1>
              {(editingProject || sessionProject) && (
                <p className="mt-3 max-w-3xl text-base-content/70">
                  {t.translations.MANAGING_SETTINGS_FOR_PROJECT}:{" "}
                  <span className="font-semibold">
                    {editingProject?.name || sessionProject?.projectName}
                  </span>
                </p>
              )}
            </div>
          </div>
        </section>

        {/* Tabs */}
        <section className="mx-auto w-full max-w-7xl px-3 py-5 sm:px-6 lg:px-8">
          <Tabs
            tabs={tabData}
            className="mx-0"
            onTabChange={handleTabChange}
            activeTab={activeTab}
          />
        </section>
      </main>
    </ProjectAdminRoute>
  );
};

export default ProjectManagementClient;
