import React, { useState, useEffect } from "react";
import { useLanguage } from "@/app/contexts/Language";
import { getAllUsers } from "@/app/lib/client_service/user_services.client";
import { getAllRoles } from "@/app/lib/client_service/role_services.client";
import {
  RoleResponseDto,
  UserResponseDto,
} from "@/app/(home)/types/responseDTOs";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import { addMemberToProject } from "@/app/lib/client_service/projects_services.client";

interface AddMemberModalProps {
  isOpen: boolean;
  onClose: () => void;
  projectId: number;
  onMemberAdded: () => void;
}

const AddProjectMember = ({
  isOpen,
  onClose,
  projectId,
  onMemberAdded,
}: AddMemberModalProps) => {
  const { t } = useLanguage();
  const [users, setUsers] = useState<UserResponseDto[]>([]);
  const [roles, setRoles] = useState<RoleResponseDto[]>([]);
  const [selectedUser, setSelectedUser] = useState<number | null>(null);
  const [selectedRole, setSelectedRole] = useState<number | null>(null);
  const { organization, hasLoaded } = useOrganizationSession();

  useEffect(() => {
    if (isOpen && organization?.organizationId) {
      // Fetch users
      getAllUsers()
        .then((response: UserResponseDto[]) => {
          setUsers(response);
        })
        .catch((error) => {
          console.error("Error fetching users:", error);
        });

      // Fetch roles for the specific project
      getAllRoles(organization.organizationId as number, projectId)
        .then((response: RoleResponseDto[]) => {
          setRoles(response);
        })
        .catch((error) => {
          console.error("Error fetching roles:", error);
        });
    }
  }, [isOpen, projectId, organization?.organizationId]);

  const handleUserChange = (event: React.ChangeEvent<HTMLSelectElement>) => {
    const userId = parseInt(event.target.value, 10);
    setSelectedUser(isNaN(userId) ? null : userId);
  };

  const handleRoleChange = (event: React.ChangeEvent<HTMLSelectElement>) => {
    const roleId = parseInt(event.target.value, 10);
    setSelectedRole(isNaN(roleId) ? null : roleId);
  };

  const handleSave = async () => {
    if (selectedUser) {
      const user = users.find((u) => u.id === selectedUser);
      if (user) {
        try {
          await addMemberToProject(
            projectId,
            selectedUser,
            selectedRole ? { roleId: selectedRole } : {}
          );
          onMemberAdded();
          onClose();
        } catch (error) {
          console.error("Error adding member:", error);
        }
      }
    }
  };

  return (
    <>
      {isOpen && (
        <dialog className="modal modal-open">
          <div className="modal-box max-w-lg">
            <h3 className="font-bold text-lg mb-4 text-neutral">
              {t.translations.ADD_NEW_MEMBER}
            </h3>
            <form method="dialog" className="flex flex-col gap-4">
              <select
                value={selectedUser || ""}
                onChange={handleUserChange}
                className="w-full select select-primary text-neutral"
              >
                <option value="" disabled>
                  {t.translations.SELECT_A_MEMBER}
                </option>
                {users.map((user) => (
                  <option key={user.id} value={user.id}>
                    {user.email}
                  </option>
                ))}
              </select>
              <select
                value={selectedRole || ""}
                onChange={handleRoleChange}
                className="w-full select select-primary text-neutral"
              >
                <option value="" disabled>
                  {t.translations.SELECT_A_ROLE} (optional)
                </option>
                {roles.map((role) => (
                  <option key={role.id} value={role.id}>
                    {role.name}
                  </option>
                ))}
              </select>
            </form>
            <div className="modal-action">
              <button className="btn" onClick={onClose}>
                {t.translations.CANCEL}
              </button>
              <button className="btn btn-primary" onClick={handleSave}>
                {t.translations.SAVE}
              </button>
            </div>
          </div>
        </dialog>
      )}
    </>
  );
};

export default AddProjectMember;
