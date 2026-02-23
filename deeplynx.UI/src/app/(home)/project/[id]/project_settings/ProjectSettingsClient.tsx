"use client";

import ProjectDropdownSingleSelect from "@/app/(home)/components/ProjectDropdownSingleSelect";
import AddProjectMember from "@/app/(home)/components/ProjectSettingsTable/ProjectModals/ProjectMemberModal";
import ProjectSettingsMemberSkeleton from "@/app/(home)/components/skeletons/projectsettingsmemberskeleton";
import Tabs from "@/app/(home)/components/Tabs";
import {
  ProjectMemberResponseDto,
  ProjectResponseDto,
  RoleResponseDto,
} from "@/app/(home)/types/responseDTOs";
import { useLanguage } from "@/app/contexts/Language";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import { getProjectMembers } from "@/app/lib/client_service/projects_services.client";
import { getAllRoles } from "@/app/lib/client_service/role_services.client";
import { PlusIcon } from "@heroicons/react/24/outline";
import { useRouter, useSearchParams } from "next/navigation";
import React, { useCallback, useEffect, useState } from "react";
interface ProjectSettingsProps {
  projects: ProjectResponseDto[];
  initialProject: ProjectResponseDto | null;
}

export default function ProjectSettingsClient({
  projects,
  initialProject,
}: ProjectSettingsProps) {
  const { t } = useLanguage();
  const [addProjectMemberModal, setAddProjectMemberModal] = useState(false);
  const [activeTab, setActiveTab] = useState("Members");
  const router = useRouter();
  const searchParams = useSearchParams();
  const [project, setProject] = useState<ProjectResponseDto | null>(
    initialProject
  );
  const [selectedProjectId, setSelectedProjectId] = useState<string | null>(
    initialProject?.id.toString() || null
  );
  const [projectMembers, setProjectMembers] = useState<
    ProjectMemberResponseDto[]
  >([]);

  const [roles, setRoles] = useState<RoleResponseDto[]>([]);
  const [isMembersLoading, setIsMembersLoading] = useState(true);
  const { organization } = useOrganizationSession();

  useEffect(() => {
    const fetchRoles = async () => {
      const rolesData = await getAllRoles(
        organization?.organizationId as number,
        Number(selectedProjectId)
      );
      setRoles(rolesData);
    };
    fetchRoles();
  }, [selectedProjectId, organization?.organizationId]);

  useEffect(() => {
    if (!selectedProjectId) return;

    (async () => {
      try {
        const users = await getProjectMembers(
          organization?.organizationId as number,
          Number(selectedProjectId)
        );
        setProjectMembers(users);
        setIsMembersLoading(false);
      } catch (err) {
        console.error(err);
      }
    })();
  }, [selectedProjectId, organization?.organizationId]);

  const refreshMembers = async () => {
    if (selectedProjectId) {
      const users = await getProjectMembers(
        organization?.organizationId as number,
        Number(selectedProjectId)
      );
      setProjectMembers(users);
    }
  };

  const memberConent = isMembersLoading ? (
    <ProjectSettingsMemberSkeleton />
  ) : (
    <div></div>
  );

  const tabData = [
    {
      label: "Members",
      content: memberConent,
    },
    {
      label: "Roles",
      content: <div></div>,
    }
  ];

  const handleTabChange = (label: string) => {
    setActiveTab(label);
  };

  const handleAddButtonClick = (event: React.MouseEvent<HTMLElement>) => {
    event.preventDefault();
    if (activeTab === "Roles") {
      router.push(
        `/project/${selectedProjectId}/project_settings/project_roles/new_role`
      );
    } else if (activeTab === "Members") {
      setAddProjectMemberModal(true);
    }
  };

  const handleProjectChange = useCallback((newProjectId: string) => {
    setSelectedProjectId(newProjectId);
  }, []);

  // Effect to set the active tab from the query parameter
  useEffect(() => {
    const tab = searchParams.get("tab");
    if (tab) {
      setActiveTab(tab);
    }
  }, [searchParams]);

  return (
    <div>
      <div className="bg-base-200/40 px-3 sm:px-6 lg:px-12 p-6">
        <h1 className="text-xl sm:text-2xl font-bold text-base-content">
          {t.translations.PROJECT_SETTINGS}
        </h1>
        <div className="mt-2">
          <ProjectDropdownSingleSelect
            projects={projects}
            onSelectionChange={handleProjectChange}
            defaultSelectedId={
              selectedProjectId === undefined || selectedProjectId === null
                ? undefined
                : String(selectedProjectId)
            }
          />
        </div>
      </div>
      <div className="p-2 sm:p-3 flex flex-col sm:flex-row justify-between sm:items-center gap-3">
        <Tabs
          tabs={tabData}
          className="mx-1 sm:mx-3 flex-1"
          onTabChange={handleTabChange}
          activeTab={activeTab}
        />
        <button
          onClick={handleAddButtonClick}
          className="btn btn-secondary text-white self-start sm:self-auto"
        >
          <PlusIcon className="size-6" />
          {activeTab === "Members"
            ? t.translations.MEMBER
            : t.translations.ROLE}
        </button>
      </div>

      <AddProjectMember
        projectId={Number(selectedProjectId)}
        isOpen={addProjectMemberModal}
        onClose={() => setAddProjectMemberModal(false)}
        onMemberAdded={refreshMembers}
      />
    </div>
  );
}
