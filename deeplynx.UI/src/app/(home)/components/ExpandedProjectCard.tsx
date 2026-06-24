"use client";

import { useLanguage } from "@/app/contexts/Language";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import {
  getProjectMembers,
  getProjectStats,
} from "@/app/lib/client_service/projects_services.client";
import { formatLocalDateTime } from "@/app/lib/date_time";
import {
  ArrowsRightLeftIcon,
  CircleStackIcon,
  RectangleGroupIcon,
  XMarkIcon,
} from "@heroicons/react/24/outline";
import { useRouter } from "next/navigation";
import React, { type ComponentType, type SVGProps, useEffect, useState } from "react";
import {
  ProjectMemberResponseDto,
  ProjectResponseDto,
} from "../types/responseDTOs";
import { ContactAvatarCell } from "./Avatar";

const MAX_VISIBLE_CONTACTS = 2;
const MAX_VISIBLE_MEMBERS = 5;

const SECTION_TITLE_CLASS = "mb-2 text-sm font-medium text-base-content/80";
const AVATAR_FRAME_CLASS =
  "relative h-10 w-10 overflow-hidden rounded-full ring-2 ring-base-300/30";

type ProjectStatsSummary = {
  classes: number;
  records: number;
  connections: number;
};

type StatIcon = ComponentType<SVGProps<SVGSVGElement>>;

interface Props {
  project: ProjectResponseDto;
  onClose: () => void;
}

interface ProjectStatCardProps {
  title: string;
  value: number;
  Icon: StatIcon;
}

function ProjectStatCard({ title, value, Icon }: ProjectStatCardProps) {
  return (
    <div className="rounded-lg border border-base-300/30 bg-base-200 p-3">
      <div className="flex items-center gap-3">
        <Icon className="size-8 text-secondary" />
        <div>
          <div className="text-xs font-medium text-base-content/60">
            {title}
          </div>
          <div className="text-lg font-bold text-base-content">{value}</div>
        </div>
      </div>
    </div>
  );
}

const ExpandedProjectCard: React.FC<Props> = ({ project, onClose }) => {
  const router = useRouter();
  const { t } = useLanguage();
  const { organization } = useOrganizationSession();

  const [stats, setStats] = useState<ProjectStatsSummary | null>(null);
  const [members, setMembers] = useState<ProjectMemberResponseDto[]>([]);

  const organizationId = organization?.organizationId;
  const projectId = project.id;
  const numericOrganizationId = organizationId ? Number(organizationId) : null;
  const numericProjectId = projectId ? Number(projectId) : null;
  const formattedLastUpdatedAt = project.lastUpdatedAt
    ? formatLocalDateTime(
        project.lastUpdatedAt instanceof Date
          ? project.lastUpdatedAt.toISOString()
          : String(project.lastUpdatedAt),
      )
    : t.translations.UNKNOWN;

  const projectContacts = members
    .filter((member) => member.isProjectAdmin === true)
    .slice(0, MAX_VISIBLE_CONTACTS);
  const visibleMembers = members.slice(0, MAX_VISIBLE_MEMBERS);
  const hiddenMemberCount = Math.max(members.length - visibleMembers.length, 0);
  const statCards: ProjectStatCardProps[] = [
    {
      title: t.translations.CLASSES,
      value: stats?.classes ?? 0,
      Icon: RectangleGroupIcon,
    },
    {
      title: t.translations.RECORDS,
      value: stats?.records ?? 0,
      Icon: CircleStackIcon,
    },
    {
      title: t.translations.DATA_SOURCES,
      value: stats?.connections ?? 0,
      Icon: ArrowsRightLeftIcon,
    },
  ];

  useEffect(() => {
    if (!numericProjectId || !numericOrganizationId) {
      setStats(null);
      setMembers([]);
      return;
    }

    let isActive = true;

    const fetchProjectDetails = async () => {
      const [statsResult, membersResult] = await Promise.allSettled([
        getProjectStats(numericOrganizationId, numericProjectId),
        getProjectMembers(numericOrganizationId, numericProjectId),
      ]);

      if (!isActive) {
        return;
      }

      if (statsResult.status === "fulfilled") {
        setStats({
          classes: statsResult.value.classes,
          records: statsResult.value.records,
          connections: statsResult.value.datasources,
        });
      } else {
        console.error("Failed to fetch project stats:", statsResult.reason);
        setStats(null);
      }

      if (membersResult.status === "fulfilled") {
        setMembers(membersResult.value);
      } else {
        console.error("Failed to fetch project members:", membersResult.reason);
        setMembers([]);
      }
    };

    void fetchProjectDetails();

    return () => {
      isActive = false;
    };
  }, [numericOrganizationId, numericProjectId]);

  return (
    <div>
      <div className="mb-6 flex items-start justify-between gap-4">
        <div className="flex-1">
          <h2 className="text-2xl font-bold text-base-content">
            {project.name}
          </h2>
          <p className="mt-1 text-sm text-base-content/70">
            {project.description}
          </p>
          <p className="mt-2 text-xs text-base-content/50">
            {t.translations.LAST_EDIT}: {formattedLastUpdatedAt}
          </p>
        </div>
        <button
          onClick={onClose}
          aria-label="Close details"
          className="rounded-lg p-1 transition-colors hover:bg-base-300/30"
          data-tour={`project-row-${project.id ?? 0}-close`}
        >
          <XMarkIcon className="size-6 text-base-content/60 hover:text-base-content" />
        </button>
      </div>

      <div className="mb-6 flex items-start justify-between gap-6">
        <section className="space-y-2">
          <p className={SECTION_TITLE_CLASS}>{t.translations.PROJECT_CONTACTS}</p>
          <div className="flex flex-wrap gap-2">
            {projectContacts.map((member) => (
              <ContactAvatarCell
                key={member.memberId ?? member.email}
                name={member.name}
                email={member.email}
                avatarClassName={AVATAR_FRAME_CLASS}
              />
            ))}
            {projectContacts.length === 0 && (
              <p className="text-sm text-base-content/60">
                {t.translations.NO_ADMIN_CONTACTS}
              </p>
            )}
          </div>
        </section>

        <section className="space-y-2">
          <p className={SECTION_TITLE_CLASS}>
            {t.translations.TEAM_MEMBERS} ({members.length})
          </p>
          <div className="flex flex-wrap gap-2">
            {visibleMembers.map((member) => (
              <ContactAvatarCell
                key={member.memberId ?? member.email}
                name={member.name}
                email={member.email}
                avatarClassName={AVATAR_FRAME_CLASS}
              />
            ))}
            {hiddenMemberCount > 0 && (
              <div className="avatar">
                <div className="flex h-10 w-10 items-center justify-center rounded-full bg-secondary text-sm font-semibold text-secondary-content ring-2 ring-base-300/30">
                  +{hiddenMemberCount}
                </div>
              </div>
            )}
          </div>
        </section>
      </div>

      {!stats ? (
        <div className="py-8 text-center text-base-content/60">
          {t.translations.NO_STATS}
        </div>
      ) : (
        <div className="mb-6 grid gap-4 sm:grid-cols-3">
          {statCards.map((card) => (
            <ProjectStatCard
              key={card.title}
              title={card.title}
              value={card.value}
              Icon={card.Icon}
            />
          ))}
        </div>
      )}

      <div className="flex justify-end pt-2">
        <button
          className="btn btn-secondary btn-sm"
          onClick={() => router.push(`/project/${projectId}`)}
        >
          {t.translations.EXPLORE}
        </button>
      </div>
    </div>
  );
};

export default ExpandedProjectCard;
