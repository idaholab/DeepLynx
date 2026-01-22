import AddMember from "@/app/(home)/components/WidgetCards/WidgetCardModals/AddMemberModal";
import { peopleData } from "@/app/(home)/dummy_data/data";
import { Column, TeamMember } from "@/app/(home)/types/types";
import { useLanguage } from "@/app/contexts/Language";
import { useProjectSession } from "@/app/contexts/ProjectSessionProvider";
import { getAllUsers } from "@/app/lib/client_service/user_services.client";
import { ChevronDownIcon } from "@heroicons/react/24/outline";
import { PlusCircleIcon } from "@heroicons/react/24/solid";
import React, { useEffect, useState } from "react";
import AvatarCell from "../Avatar";
import GenericTable from "../GenericTable";
import AvatarCarousel from "./WidgetCardModals/AvatarCarousel";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";

const TeamMembersWidget: React.FC = () => {
  const [showTable, setShowTable] = useState(false);
  const [addMemberModal, setAddMemberModal] = useState(false);
  const [users, setUsers] = useState<{ name: string; email: string }[]>([]);
  const { project } = useProjectSession();
  const { t } = useLanguage();
  const { organization } = useOrganizationSession();

  const handleToggle = () => {
    setShowTable((prev) => !prev);
  };

  useEffect(() => {
    const fetchAllUsers = async () => {
      try {
        const data = await getAllUsers(
          organization?.organizationId,
          project?.projectId
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

  const teamMemberColumns: Column<TeamMember>[] = [
    {
      header: "Name",
      data: "name",
      cell: (row) => (
        <div className="flex items-center gap-3">
          <div className="avatar">
            <div className="mask mask-circle h-10 w-10">
              <AvatarCell name={row.name} image={row.image} />
            </div>
          </div>
          <div>{row.name}</div>
        </div>
      ),
      sortable: true,
    },
    {
      header: "Role",
      data: "role",
      sortable: true,
    },
    {
      header: "Last Login",
      data: "lastLogin",
      sortable: true,
    },
  ];

  return (
    <div className="card-body">
      <div className="flex justify-between">
        <h2 className="card-title flex items-center">
          {t.translations.TEAM_MEMBERS}
          {showTable && (
            <button onClick={() => setAddMemberModal(true)} className="ml-1">
              <PlusCircleIcon className="w-7 h-7 text-secondary" />
            </button>
          )}
        </h2>
        <button onClick={handleToggle} className="btn btn-sm btn-ghost">
          {showTable ? (
            <ChevronDownIcon className="size-6 rotate-180" />
          ) : (
            <ChevronDownIcon className="size-6" />
          )}
        </button>
      </div>

      {!showTable ? (
        <AvatarCarousel people={users} />
      ) : (
        <GenericTable
          columns={teamMemberColumns}
          data={peopleData}
          enablePagination
          rowsPerPage={4}
        />
      )}

      <AddMember
        isOpen={addMemberModal}
        onClose={() => setAddMemberModal(false)}
      />
    </div>
  );
};

export default TeamMembersWidget;
