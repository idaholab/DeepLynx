"use client";

import { useEffect, useMemo, useState } from "react";
import toast from "react-hot-toast";
import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import { sendEmail } from "@/app/lib/client_service/notification_services.client";
import { getAllUsers } from "@/app/lib/client_service/user_services.client";
import { getAllGroups } from "@/app/lib/client_service/group_services.client";
import {
  addMemberToProject,
  removeMemberFromProject,
  updateProjectMemberRole,
  inviteUserToProject,
  getProjectMembers,
} from "@/app/lib/client_service/projects_services.client";
import { InviteUserToProjectRequestDto } from "@/app/(home)/types/requestDTOs";
import {
  ProjectMemberResponseDto,
  UserResponseDto,
  GroupResponseDto,
  RoleResponseDto,
  ProjectResponseDto,
} from "@/app/(home)/types/responseDTOs";
import ProjectUsersHeader from "./ProjectUsersHeader";
import ProjectUsersListTable from "./ProjectUsersListTable";
import AddProjectMemberModal from "./AddProjectMemberModal";
import RemoveProjectMemberModal from "./RemoveProjectMemberModal";
import EditProjectMemberRoleModal from "./EditProjectMemberRoleModal";
import InviteProjectUserModal from "./InviteProjectUserModal";
import {
  AddMemberModalState,
  ConfirmModalState,
  EditRoleModalState,
  MemberType,
  ProjectMemberTableRow,
  buildTableData,
} from "../../types/projectUsersTypes";
import { useLanguage } from "@/app/contexts/Language";

/* -------------------------------------------------------------------------- */
/*                         ProjectUsersTable Component                        */
/* -------------------------------------------------------------------------- */

interface Props {
  members: ProjectMemberResponseDto[];
  roles: RoleResponseDto[];
  project: ProjectResponseDto | null;
}

const ProjectUsersTable = ({ members, roles, project }: Props) => {
  /* ------------------------------------------------------------------------ */
  /*                               Core State                                */
  /* ------------------------------------------------------------------------ */

  const [tableData, setTableData] = useState<ProjectMemberTableRow[]>(() =>
    buildTableData(members),
  );
  const [loading, setLoading] = useState(false);
  const { t } = useLanguage();

  /* ------------------------------------------------------------------------ */
  /*                           Invite Modal State                             */
  /* ------------------------------------------------------------------------ */

  const [showInviteModal, setShowInviteModal] = useState(false);
  const [inviteEmail, setInviteEmail] = useState("");
  const [inviteRoleId, setInviteRoleId] = useState("");
  const [inviteModalLoading, setInviteModalLoading] = useState(false);

  /* ------------------------------------------------------------------------ */
  /*                           Add Member Modal State                         */
  /* ------------------------------------------------------------------------ */

  const [addModal, setAddModal] = useState<AddMemberModalState>({
    isOpen: false,
    memberType: "user",
  });
  const [selectedMemberId, setSelectedMemberId] = useState<string>("");
  const [selectedRoleId, setSelectedRoleId] = useState<string>("");

  const [availableUsers, setAvailableUsers] = useState<UserResponseDto[]>([]);
  const [availableGroups, setAvailableGroups] = useState<GroupResponseDto[]>(
    [],
  );
  const [modalLoading, setModalLoading] = useState(false);

  /* ------------------------------------------------------------------------ */
  /*                        Confirm Remove / Future Use                       */
  /* ------------------------------------------------------------------------ */

  const [confirmModal, setConfirmModal] = useState<ConfirmModalState>({
    isOpen: false,
    memberId: null,
    memberName: "",
    memberType: null,
    isPending: false,
  });

  /* ------------------------------------------------------------------------ */
  /*                        Edit Role Modal State & Handlers                  */
  /* ------------------------------------------------------------------------ */

  const [editRoleModal, setEditRoleModal] = useState<EditRoleModalState>({
    isOpen: false,
    memberId: null,
    memberName: "",
    memberType: null,
    currentRoleId: null,
  });
  const [editRoleSelectedId, setEditRoleSelectedId] = useState<string>("");

  /* ------------------------------------------------------------------------ */
  /*                          Org / Project Context                           */
  /* ------------------------------------------------------------------------ */

  const { organization } = useOrganizationSession();

  const organizationId = organization?.organizationId
    ? Number(organization.organizationId)
    : undefined;
  const projectId = project?.id ? Number(project.id) : undefined;

  /* ------------------------------------------------------------------------ */
  /*                    Sync server-provided members -> table                 */
  /* ------------------------------------------------------------------------ */

  useEffect(() => {
    setTableData(buildTableData(members));
  }, [members]);

  /* ------------------------------------------------------------------------ */
  /*                               Derived Stats                              */
  /* ------------------------------------------------------------------------ */

  const totalMembers = tableData.length;
  const userCount = useMemo(
    () => tableData.filter((m) => m.memberType === "user").length,
    [tableData],
  );
  const groupCount = useMemo(
    () => tableData.filter((m) => m.memberType === "group").length,
    [tableData],
  );

  /* ------------------------------------------------------------------------ */
  /*                     Invite User: Open Modal & Send                       */
  /* ------------------------------------------------------------------------ */

  const handleOpenInviteModal = () => {
    setShowInviteModal(true);
  };

  const handleInviteUser = async () => {
    if (!inviteEmail) {
      toast.error(t.translations.PLEASE_ENTER_EMAIL_ADDRESS);
      return;
    }

    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!emailRegex.test(inviteEmail)) {
      toast.error(t.translations.PLEASE_ENTER_VALID_EMAIL_ADDRESS);
      return;
    }

    if (!inviteRoleId) {
      toast.error(t.translations.PLEASE_SELECT_A_ROLE);
      return;
    }

    if (!organizationId || !projectId) {
      toast.error(t.translations.MISSING_ORG_OR_PROJECT);
      return;
    }

    try {
      setInviteModalLoading(true);

      const inviteData: InviteUserToProjectRequestDto = {
        userEmail: inviteEmail,
        userName: inviteEmail.split("@")[0],
        roleId: Number(inviteRoleId),
      };

      await inviteUserToProject(organizationId, projectId, inviteData);

      toast.success(`${t.translations.INVITATION_SENT_TO_} ${inviteEmail}`);

      // Refresh the members list
      try {
        const updatedMembers = await getProjectMembers(
          organizationId,
          projectId,
        );

        setTableData(buildTableData(updatedMembers));
      } catch (refreshError) {
        console.error("Failed to refresh members list:", refreshError);
        // Don't show error to user - invite was successful
      }

      setShowInviteModal(false);
      setInviteEmail("");
      setInviteRoleId("");
    } catch (error) {
      console.error("Error inviting user to project:", error);
      toast.error(t.translations.FAILED_TO_SEND_INVITATION);
    } finally {
      setInviteModalLoading(false);
    }
  };
  /* ------------------------------------------------------------------------ */
  /*                     Edit Role: open & save handlers                      */
  /* ------------------------------------------------------------------------ */

  const handleOpenEditRoleModal = (row: ProjectMemberTableRow) => {
    setEditRoleModal({
      isOpen: true,
      memberId: row.memberId,
      memberName: row.name,
      memberType: row.memberType,
      currentRoleId: row.roleId ?? null,
    });
    setEditRoleSelectedId(row.roleId ? String(row.roleId) : "");
  };

  const handleSaveMemberRole = async () => {
    if (!organizationId || !projectId) {
      toast.error(t.translations.MISSING_ORG_OR_PROJECT);
      return;
    }

    if (
      !editRoleModal.memberId ||
      !editRoleModal.memberType ||
      !editRoleSelectedId
    ) {
      toast.error(t.translations.PLEASE_SELECT_A_ROLE);
      return;
    }

    try {
      setLoading(true);

      const roleId = Number(editRoleSelectedId);
      const memberId = editRoleModal.memberId;

      if (editRoleModal.memberType === "user") {
        await updateProjectMemberRole(
          organizationId,
          projectId,
          roleId,
          memberId,
          undefined,
        );
      } else {
        await updateProjectMemberRole(
          organizationId,
          projectId,
          roleId,
          undefined,
          memberId,
        );
      }

      const selectedRole = roles.find((r) => r.id === roleId);

      setTableData((prev) =>
        prev.map((row) =>
          row.memberId === memberId
            ? { ...row, role: selectedRole?.name ?? null, roleId }
            : row,
        ),
      );

      toast.success(t.translations.MEMBER_ROLE_UPDATED);
    } catch (error) {
      console.error("Failed to update member role:", error);
      toast.error(t.translations.FAILED_TO_UPDATE_MEMBER_ROLE);
    } finally {
      setLoading(false);
      setEditRoleModal({
        isOpen: false,
        memberId: null,
        memberName: "",
        memberType: null,
        currentRoleId: null,
      });
      setEditRoleSelectedId("");
    }
  };

  /* ------------------------------------------------------------------------ */
  /*                          Add Member: Open Modal                          */
  /* ------------------------------------------------------------------------ */

  const handleOpenAddMemberModal = async (memberType: MemberType = "user") => {
    if (!organizationId) {
      toast.error(t.translations.NO_ORG_SELECTED);
      return;
    }

    setAddModal({ isOpen: true, memberType });
    setModalLoading(true);

    try {
      const [users, groups] = await Promise.all([
        getAllUsers(organizationId),
        getAllGroups(organizationId),
      ]);
      setAvailableUsers(users);
      setAvailableGroups(groups);
    } catch (error) {
      console.error("Failed to load options for Add Member:", error);
      toast.error(t.translations.UNABLE_TO_LOAD_USERS_OR_GROUPS);
    } finally {
      setModalLoading(false);
    }
  };

  /* ------------------------------------------------------------------------ */
  /*                        Add Member: Confirm Action                        */
  /* ------------------------------------------------------------------------ */

  const handleAddMember = async () => {
    if (!organizationId || !projectId) {
      toast.error(t.translations.MISSING_ORG_OR_PROJECT);
      return;
    }

    if (!selectedMemberId) {
      toast.error(
        addModal.memberType === "user"
          ? t.translations.PLEASE_SELECT_A_USER
          : t.translations.PLEASE_SELECT_A_GROUP,
      );
      return;
    }

    if (!selectedRoleId) {
      toast.error(t.translations.PLEASE_SELECT_A_ROLE_FOR_MEMBER);
      return;
    }

    try {
      setModalLoading(true);

      const roleId = Number(selectedRoleId);
      const memberId = Number(selectedMemberId);

      if (addModal.memberType === "user") {
        await addMemberToProject(organizationId, projectId, {
          roleId,
          userId: memberId,
        });
      } else {
        await addMemberToProject(organizationId, projectId, {
          roleId,
          groupId: memberId,
        });
      }

      if (addModal.memberType === "user") {
        const user = availableUsers.find((u) => u.id === memberId);
        if (user?.email) {
          try {
            await sendEmail(
              user.email,
              t.translations.YOUVE_BEEN_ADDED_TO_A_PROJECT_IN_DEEPLYNX_NEXUS,
            );
          } catch (emailError) {
            console.error("Failed to send notification email:", emailError);
          }
        }
      }

      toast.success(t.translations.MEMBER_ADDED_TO_PROJECT);

      const selectedRole = roles.find((r) => r.id === roleId);
      const nameSource =
        addModal.memberType === "user"
          ? (availableUsers.find((u) => u.id === memberId)?.name ?? "")
          : (availableGroups.find((g) => g.id === memberId)?.name ?? "");

      const emailSource =
        addModal.memberType === "user"
          ? (availableUsers.find((u) => u.id === memberId)?.email ?? null)
          : null;

      setTableData((prev) => [
        ...prev,
        {
          memberId,
          name: nameSource,
          email: emailSource,
          role: selectedRole?.name ?? null,
          roleId,
          memberType: addModal.memberType,
        },
      ]);

      setAddModal((prev) => ({ ...prev, isOpen: false }));
      setSelectedMemberId("");
      setSelectedRoleId("");
    } catch (error) {
      console.error("Failed to add member to project:", error);
      toast.error(t.translations.FAILED_TO_ADD_MEMBER);
    } finally {
      setModalLoading(false);
    }
  };

  /* ------------------------------------------------------------------------ */
  /*                        Remove Member: Confirm Action                     */
  /* ------------------------------------------------------------------------ */

  const handleRemoveMember = async () => {
    if (!organizationId || !projectId) {
      toast.error(t.translations.MISSING_ORG_OR_PROJECT);
      return;
    }

    if (!confirmModal.memberId || !confirmModal.memberType) {
      toast.error(t.translations.NO_MEMBER_SELECTED_TO_REMOVE);
      return;
    }

    try {
      setLoading(true);

      const memberId = confirmModal.memberId;

      if (confirmModal.memberType === "user") {
        await removeMemberFromProject(
          organizationId,
          projectId,
          memberId,
          undefined,
        );
      } else {
        await removeMemberFromProject(
          organizationId,
          projectId,
          undefined,
          memberId,
        );
      }

      setTableData((prev) => prev.filter((row) => row.memberId !== memberId));

      toast.success(t.translations.MEMBER_REMOVED_FROM_PROJECT);
    } catch (error) {
      console.error("Failed to remove member from project:", error);
      toast.error(t.translations.FAILED_TO_REMOVE_MEMBER);
    } finally {
      setLoading(false);
      setConfirmModal({
        isOpen: false,
        memberId: null,
        memberName: "",
        memberType: null,
        isPending: false,
      });
    }
  };

  /* ------------------------------------------------------------------------ */
  /*                          Main Render: Header + Table                     */
  /* ------------------------------------------------------------------------ */

  return (
    <div className="p-6">
      <div className="">
        <div className="">
          <ProjectUsersHeader
            totalMembers={totalMembers}
            userCount={userCount}
            groupCount={groupCount}
            loading={loading}
            onAddUser={() => handleOpenAddMemberModal("user")}
            onAddGroup={() => handleOpenAddMemberModal("group")}
            onInviteUser={handleOpenInviteModal}
          />

          <ProjectUsersListTable
            tableData={tableData}
            loading={loading}
            onEditRole={handleOpenEditRoleModal}
            onOpenRemoveModal={({ memberId, memberName, memberType }) =>
              setConfirmModal({
                isOpen: true,
                memberId,
                memberName,
                memberType,
                isPending: false,
              })
            }
          />

          {/* Invite User Modal */}
          <InviteProjectUserModal
            isOpen={showInviteModal}
            inviteEmail={inviteEmail}
            selectedRoleId={inviteRoleId}
            roles={roles}
            modalLoading={inviteModalLoading}
            onClose={() => {
              setShowInviteModal(false);
              setInviteEmail("");
              setInviteRoleId("");
            }}
            onInvite={handleInviteUser}
            onChangeEmail={setInviteEmail}
            onChangeRole={setInviteRoleId}
          />

          {/* Remove Member Modal */}
          <RemoveProjectMemberModal
            confirmModal={confirmModal}
            loading={loading}
            onCancel={() =>
              setConfirmModal({
                isOpen: false,
                memberId: null,
                memberName: "",
                memberType: null,
                isPending: false,
              })
            }
            onConfirm={handleRemoveMember}
          />

          {/* Add Member Modal */}
          <AddProjectMemberModal
            addModal={addModal}
            roles={roles}
            availableUsers={availableUsers}
            availableGroups={availableGroups}
            selectedMemberId={selectedMemberId}
            selectedRoleId={selectedRoleId}
            modalLoading={modalLoading}
            onClose={() => {
              setAddModal((prev) => ({ ...prev, isOpen: false }));
              setSelectedMemberId("");
              setSelectedRoleId("");
            }}
            onChangeMember={setSelectedMemberId}
            onChangeRole={setSelectedRoleId}
            onConfirm={handleAddMember}
          />

          {/* Edit Role Modal */}
          <EditProjectMemberRoleModal
            editRoleModal={editRoleModal}
            roles={roles}
            loading={loading}
            selectedRoleId={editRoleSelectedId}
            onChangeRole={setEditRoleSelectedId}
            onCancel={() => {
              setEditRoleModal({
                isOpen: false,
                memberId: null,
                memberName: "",
                memberType: null,
                currentRoleId: null,
              });
              setEditRoleSelectedId("");
            }}
            onSave={handleSaveMemberRole}
          />
        </div>
      </div>
    </div>
  );
};

export default ProjectUsersTable;
