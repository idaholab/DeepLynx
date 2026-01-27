import React, { useState, useEffect } from "react";
import { useLanguage } from "@/app/contexts/Language";
import RoleDetails from "./RoleDetails";
import { useRouter, useSearchParams } from "next/navigation";
import {
  getAllRoles,
  updateRole,
} from "@/app/lib/client_service/role_services.client";
import {
  RoleResponseDto,
  PermissionResponseDto,
} from "../../../types/responseDTOs";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import { useProjectSession } from "@/app/contexts/ProjectSessionProvider";
import { UpdateRoleRequestDto } from "@/app/(home)/types/requestDTOs";

interface RoleSettingsProps {
  id?: string | string[];
}

const RoleSettings = ({ id }: RoleSettingsProps) => {
  const { t } = useLanguage();
  const router = useRouter();
  const searchParams = useSearchParams();

  const [role, setRole] = useState<RoleResponseDto | null>(null);
  const [permissions, setPermissions] = useState<PermissionResponseDto[]>([]);
  const { organization, hasLoaded } = useOrganizationSession();
  const { project, setProject } = useProjectSession();

  // Fetch role and permissions data on component mount
  useEffect(() => {
    const roleId = searchParams.get("roleId");
    if (roleId && organization?.organizationId && project?.projectId) {
      getAllRoles(
        organization?.organizationId as number,
        project?.projectId as number
      )
        .then((data) => {
          setRole(
            data.find((r: RoleResponseDto) => r.id === Number(roleId)) || null
          );
          // Add API call to get permissions based on roleId
        })
        .catch((error) => {
          console.error("Error fetching role data:", error);
        });
    }
  }, [searchParams, organization?.organizationId, project?.projectId]);

  const onCancel = () => {
    router.push(`/project/${id}/project_settings?tab=Roles`);
  };

  //Function for saving roles and permissions
  const onSave = async () => {
    if (role) {
      try {
        const dto: UpdateRoleRequestDto = {
          name: role.name,
          description: role.description!,
        };
        await updateRole(
          organization?.organizationId as number,
          project?.projectId as number,
          role.id,
          dto
        );
        const updatedRoles = await getAllRoles(
          organization?.organizationId as number,
          project?.projectId as number
        );
        setRole(
          updatedRoles.find(
            (updatedRole: { id: number }) => updatedRole.id === role.id
          ) || null
        );
        router.push(`/project/${id}/project_settings?tab=Roles`);
      } catch (error) {
        console.error("Error updating role:", error);
      }
    }
  };

  return (
    <div className="bg-base-100 text-accent-content rounded-xl p-0 shadow-md card">
      <div className="card-body">
        <div className="w-full">
          {role && (
            <RoleDetails
              role={role}
              setRole={setRole}
              permissions={permissions}
              setPermissions={setPermissions}
              onCancel={onCancel}
              onSave={onSave}
            />
          )}
        </div>
      </div>
    </div>
  );
};

export default RoleSettings;
