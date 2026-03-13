"use client";

import React, { useState } from "react";
import { Project } from "../types/types";
import Tabs from "../components/Tabs";
import {
  OauthApplicationResponseDto,
  OrganizationResponseDto,
  UserResponseDto,
} from "../types/responseDTOs";
import OAuthManagement from "../components/SiteManagementPortal/OAuthTable";
import SiteOrganizationManagement from "../components/SiteManagementPortal/SiteOrgTable";
import { getAllOrganizations } from "@/app/lib/client_service/organization_services.client";
import { getAllOauthApplications } from "@/app/lib/client_service/oauth_services.client";
import { getAllUsers } from "@/app/lib/client_service/user_services.client";
import EventsHistoryClient from "../event_management/EventHistoryClient";
import UsersTable from "../organization_management/users/UsersTable";
import { useLanguage } from "@/app/contexts/Language";

interface SysAdminProps {
  organizations: OrganizationResponseDto[];
  applications: OauthApplicationResponseDto[];
  members: UserResponseDto[];
  initialProjects: Project[];
  initialSelectedProjects: string[];
}

const SysAdminClient = ({
  organizations: initialOrganizations,
  applications: initialApplications,
  members: initialMembers,
  initialProjects,
  initialSelectedProjects,
}: SysAdminProps) => {
  const [activeTab, setActiveTab] = useState("");
  const [organizations, setOrganizations] =
    useState<OrganizationResponseDto[]>(initialOrganizations);
  const [applications, setApplications] =
    useState<OauthApplicationResponseDto[]>(initialApplications);
  const [members, setMembers] = useState<UserResponseDto[]>(initialMembers);
  const { t } = useLanguage();
  const refreshOrganizations = async () => {
    try {
      const updatedData = await getAllOrganizations();
      setOrganizations(updatedData);
    } catch (err) {
      console.error("Failed to refresh organizations:", err);
    }
  };

  const refreshApplications = async () => {
    try {
      const updatedData = await getAllOauthApplications();
      setApplications(updatedData);
    } catch (err) {
      console.error("Failed to refresh applications:", err);
    }
  };

  const refreshUsers = async () => {
    try {
      const updatedData = await getAllUsers();
      setMembers(updatedData);
    } catch (err) {
      console.error("Failed to refresh users:", err);
    }
  };

  const tabData = [
    {
      label: t.translations.ORGANIZATION_MANAGEMENT,
      content: (
        <SiteOrganizationManagement
          initialOrganizations={organizations}
          onOrganizationsChange={refreshOrganizations}
        />
      ),
    },
    {
      label: t.translations.OAUTH_APPLICATION,
      content: (
        <OAuthManagement
          initialApplications={applications}
          onApplicationsChange={refreshApplications}
        />
      ),
    },
    {
      label: t.translations.MEMBER_MANAGEMENT,
      content: (
        <UsersTable
          members={members}
          scope="site"
          availableOrganizations={organizations}
        />
      ),
    },
    {
      label: t.translations.EVENT_HISTORY,
      content: (
        <EventsHistoryClient
          initialProjects={initialProjects}
          initialSelectedProjects={initialSelectedProjects}
        />
      ),
    },
  ];

  const handleTabChange = (label: string) => {
    setActiveTab(label);
  };

  return (
    <div>
      <div className="bg-base-200/40 px-3 sm:px-6 lg:px-12 p-6">
        <h1 className="text-xl sm:text-2xl font-bold text-base-content">
          {t.translations.SITE_MANAGEMENT}
        </h1>
      </div>
      <div className="p-2 sm:p-3">
        <Tabs
          tabs={tabData}
          className="mx-1 sm:mx-3"
          onTabChange={handleTabChange}
          activeTab={activeTab}
        />
      </div>
    </div>
  );
};

export default SysAdminClient;
