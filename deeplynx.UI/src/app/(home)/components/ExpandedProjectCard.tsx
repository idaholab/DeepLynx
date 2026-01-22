// src/app/(home)/components/ExpandableProjectCard.tsx
"use client";
import { useLanguage } from "@/app/contexts/Language";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import { getProjectStats } from "@/app/lib/client_service/projects_services.client";
import { getAllUsers } from "@/app/lib/client_service/user_services.client";
import {
  ArrowsRightLeftIcon,
  CircleStackIcon,
  RectangleGroupIcon,
  XMarkIcon,
} from "@heroicons/react/24/outline";
import { format } from "date-fns";
import { useRouter } from "next/navigation";
import React, { useEffect, useState } from "react";
import { ProjectResponseDto } from "../types/responseDTOs";
import AvatarCell from "./Avatar";

interface Props {
  project: ProjectResponseDto;
  onClose: () => void;
}

const ExpandedProjectCard: React.FC<Props> = ({ project, onClose }) => {
  const router = useRouter();
  const { t } = useLanguage();
  const { organization, hasLoaded } = useOrganizationSession();

  const [stats, setStats] = useState<{
    classes: number;
    records: number;
    connections: number;
  } | null>(null);
  const [users, setUsers] = useState<{ name: string }[]>([]);

  useEffect(() => {
    if (!project.id || !organization?.organizationId) return;

    const fetchStats = async () => {
      try {
        const data = await getProjectStats(
          organization?.organizationId as number,
          project.id as number
        );
        setStats({
          classes: data.classes,
          records: data.records,
          connections: data.datasources,
        });
      } catch (error) {
        console.error("Failed to fetch project stats:", error);
      }
    };
    fetchStats();
  }, [project.id, organization?.organizationId]);

  useEffect(() => {
    const fetchAllUsers = async () => {
      try {
        const data = await getAllUsers(Number(project?.id));
        setUsers(data);
      } catch (error) {
        console.error("Failed to fetch projects:", error);
      }
    };

    fetchAllUsers();
  }, [project]);

  return (
    <div>
      {/* Header Section */}
      <div className="flex justify-between items-start mb-4">
        <div className="flex-1">
          <h2 className="text-2xl font-bold text-base-content">
            {project.name}
          </h2>
          <p className="text-sm text-base-content/70 mt-1">
            {project.description}
          </p>
          <p className="text-xs text-base-content/50 mt-2">
            {t.translations.LAST_EDIT}{" "}
            {format(new Date(project.lastUpdatedAt!), "MM/dd/yyyy hh:mm:s")}
          </p>
        </div>
        <button
          onClick={onClose}
          aria-label="Close details"
          className="p-1 rounded-lg hover:bg-base-300/30 transition-colors"
          data-tour={`project-row-${project.id ?? 0}-close`}
        >
          <XMarkIcon className="size-6 text-base-content/60 hover:text-base-content" />
        </button>
      </div>

      {/* Team Members Section */}
      <div className="mb-6">
        <p className="text-sm font-medium text-base-content/80 mb-3">
          {t.translations.TEAM_MEMBERS}
        </p>
        <div className="flex flex-wrap gap-2">
          {users.map((person, index) => (
            <div key={index} className="avatar">
              <div className="w-10 h-10 relative overflow-hidden rounded-full ring-2 ring-base-300/30">
                <AvatarCell name={person.name} />
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* Stats Section */}
      {!stats ? (
        <div className="text-center py-8 text-base-content/60">
          {t.translations.NO_STATS}
        </div>
      ) : (
        <div className="grid grid-cols-3 gap-4 mb-6">
          {[
            {
              title: "Classes",
              value: stats?.classes,
              Icon: RectangleGroupIcon,
            },
            {
              title: "Records",
              value: stats?.records,
              Icon: CircleStackIcon,
            },
            {
              title: "Data Sources",
              value: stats?.connections,
              Icon: ArrowsRightLeftIcon,
            },
          ].map(({ title, value, Icon }, idx) => (
            <div
              key={idx}
              className="bg-base-200 rounded-lg p-3 border border-base-300/30"
            >
              <div className="flex items-center gap-3">
                <Icon className="size-8 text-secondary" />
                <div>
                  <div className="text-xs text-base-content/60 font-medium">
                    {title}
                  </div>
                  <div className="text-lg font-bold text-base-content">
                    {value}
                  </div>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Action Button */}
      <div className="flex justify-end pt-2 border-base-300/20">
        <button
          className="btn btn-secondary btn-sm"
          onClick={() => router.push(`/project/${project.id}`)}
        >
          {t.translations.EXPLORE}
        </button>
      </div>
    </div>
  );
};

export default ExpandedProjectCard;
