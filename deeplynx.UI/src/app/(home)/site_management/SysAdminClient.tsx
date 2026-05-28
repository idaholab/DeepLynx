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
import AdminOverviewCard from "./AdminOverviewCard";

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
    <main className="min-h-screen bg-base-200/30">
      <section className="border-b border-base-300 bg-base-100">
        <div className="mx-auto flex w-full max-w-7xl flex-col gap-5 px-3 py-5 sm:px-6 lg:px-8">
          <div>
            <p className="text-xs font-semibold uppercase tracking-wide text-base-content/60">
              Site
            </p>
            <h1 className="text-2xl font-bold text-base-content sm:text-3xl">
              {t.translations.SITE_MANAGEMENT}
            </h1>
          </div>
        </div>
      </section>

      <section className="grid grid-cols-1 gap-4 mx-auto w-full max-w-7xl px-3 py-5 sm:px-6 lg:px-8 lg:grid-cols-3">
        <div className="lg:col-span-2">
          <Tabs
            tabs={tabData}
            className="mx-0"
            onTabChange={handleTabChange}
            activeTab={activeTab}
          />
        </div>

        <div className="mx-1 sm:mx-3">
          <AdminOverviewCard />
        </div>
      </section>
    </main>
  );
};

export default SysAdminClient;
