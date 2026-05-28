import { ProjectMemberResponseDto } from "@/app/(home)/types/responseDTOs";
import { Column } from "@/app/(home)/types/types";
import { useLanguage } from "@/app/contexts/Language";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import { useProjectSession } from "@/app/contexts/ProjectSessionProvider";
import { getProjectMembers } from "@/app/lib/client_service/projects_services.client";
import React, { useEffect, useState } from "react";
import AvatarCell from "../Avatar";
import GenericTable from "../GenericTable";
import Link from "next/link";

const TeamMembersWidget: React.FC = () => {
  const [users, setUsers] = useState<ProjectMemberResponseDto[]>([]);
  const { project } = useProjectSession();
  const { t } = useLanguage();
  const { organization } = useOrganizationSession();

  useEffect(() => {
    const fetchAllUsers = async () => {
      try {
        const data = await getProjectMembers(
          organization?.organizationId as number,
          project?.projectId as number,
        );
        setUsers(data);
      } catch (error) {
        console.error("Failed to fetch projects:", error);
      }
    };

    if (organization?.organizationId && project?.projectId) {
      fetchAllUsers();
    }
  }, [organization?.organizationId, project?.projectId]);

  const teamMemberRows: ProjectMemberResponseDto[] = users.map((user) => ({
    id: user.memberId,
    name: user.name,
    email: user.email,
    role: user.role ?? t.translations.NOT_AVAILABLE,
  }));

  const teamMemberColumns: Column<ProjectMemberResponseDto>[] = [
    {
      header: t.translations.NAME,
      data: "name",
      cell: (row) => (
        <div className="flex items-center gap-3">
          <div className="avatar">
            <div className="mask mask-circle h-10 w-10">
              <AvatarCell name={row.name} />
            </div>
          </div>
          <div>{row.name}</div>
        </div>
      ),
      sortable: true,
    },
    {
      header: t.translations.EMAIL,
      data: "email",
      sortable: true,
    },
    {
      header: t.translations.ROLE,
      data: "role",
      sortable: true,
    },
  ];

  return (
    <div className="card">
      <div className="card-body">
        <div className="flex justify-between">
          <h2 className="card-title flex items-center">
            {t.translations.TEAM_MEMBERS}
          </h2>
          <Link
            href={`/project_management/${project?.projectId}`}
            className="btn btn-outline btn-secondary"
          >
            {t.translations.MANAGE}
          </Link>
        </div>
        <GenericTable
          columns={teamMemberColumns}
          data={teamMemberRows}
          enablePagination
          rowsPerPage={4}
        />
      </div>
    </div>
  );
};

export default TeamMembersWidget;
