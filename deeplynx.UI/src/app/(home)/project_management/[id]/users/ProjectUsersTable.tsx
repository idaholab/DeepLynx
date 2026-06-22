"use client";

import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import {
  getAllGroups,
  getGroupMembers,
} from "@/app/lib/client_service/group_services.client";
import {
  addMemberToProject,
  getProjectMembers,
  removeMemberFromProject,
  updateProjectMemberRole,
} from "@/app/lib/client_service/projects_services.client";
import { getAllUsers } from "@/app/lib/client_service/user_services.client";
import { useRBAC } from "@/app/(home)/rbac/useRBAC";
import { useEffect, useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import toast from "react-hot-toast";

import { InviteUserToOrganizationRequestDto } from "@/app/(home)/types/requestDTOs";
import {
  GroupResponseDto,
  ProjectMemberResponseDto,
  ProjectResponseDto,
  RoleResponseDto,
  UserResponseDto,
} from "@/app/(home)/types/responseDTOs";
import { inviteUserToOrganization } from "@/app/lib/client_service/organization_services.client";
import AddGroupToProjectModal from "./AddGroupToProjectModal";
import AddUsersToProjectModal from "./AddUsersToProjectModal";
import EditProjectMemberRoleModal from "./EditProjectMemberRoleModal";
import ProjectUsersHeader from "./ProjectUsersHeader";
import ProjectUsersListTable from "./ProjectUsersListTable";
import RemoveProjectMemberModal from "./RemoveProjectMemberModal";
("@/app/lib/client_service/organizations_services.client");

import { useLanguage } from "@/app/contexts/Language";
import {
  ConfirmModalState,
  EditRoleModalState,
  ProjectMemberTableRow,
  buildTableData,
} from "../../types/projectUsersTypes";

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
  const router = useRouter();
  const { user, refreshUser } = useRBAC();

  /* ------------------------------------------------------------------------ */
  /*                           Add User Modal State                           */
  /* ------------------------------------------------------------------------ */

  const [showAddUserModal, setShowAddUserModal] = useState(false);
  const [availableUsers, setAvailableUsers] = useState<UserResponseDto[]>([]);
  const [userModalLoading, setUserModalLoading] = useState(false);

  /* ------------------------------------------------------------------------ */
  /*                           Add Group Modal State                          */
  /* ------------------------------------------------------------------------ */

  const [showAddGroupModal, setShowAddGroupModal] = useState(false);
  const [availableGroups, setAvailableGroups] = useState<GroupResponseDto[]>(
    [],
  );
  const [groupModalLoading, setGroupModalLoading] = useState(false);
  const [selectedGroupId, setSelectedGroupId] = useState<string>("");
  const [selectedGroupRoleId, setSelectedGroupRoleId] = useState<string>("");
  const [groupMembersCache, setGroupMembersCache] = useState<
    Map<number, UserResponseDto[]>
  >(new Map());
  const [viewGroupMembersModal, setViewGroupMembersModal] = useState<{
    isOpen: boolean;
    groupName: string;
    members: UserResponseDto[];
    loading: boolean;
  }>({
    isOpen: false,
    groupName: "",
    members: [],
    loading: false,
  });

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
    currentIsProjectAdmin: false,
  });
  const [editRoleSelectedId, setEditRoleSelectedId] = useState<string>("");
  const [editIsProjectAdmin, setEditIsProjectAdmin] = useState<boolean>(false);

  /* ------------------------------------------------------------------------ */
  /*                          Org / Project Context                           */
  /* ------------------------------------------------------------------------ */

  const { organization } = useOrganizationSession();

  const organizationId = organization?.organizationId
    ? Number(organization.organizationId)
    : undefined;
  const projectId = project?.id ? Number(project.id) : undefined;
  const currentUserId = user?.id ? Number(user.id) : undefined;

  const refreshAccessIfAffected = async (
    memberType?: "user" | "group" | null,
    memberId?: number | null,
  ) => {
    if (!currentUserId) return;

    if (memberType === "group" || memberId === currentUserId) {
      await refreshUser();
      router.refresh();
    }
  };

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
  /*                     Add Users Modal: Open & Handler                      */
  /* ------------------------------------------------------------------------ */

  const handleOpenAddUsersModal = async () => {
    if (!organizationId) {
      toast.error(t.translations.NO_ORG_SELECTED);
      return;
    }

    setShowAddUserModal(true);
    setUserModalLoading(true);

    try {
      const users = await getAllUsers(organizationId);
      setAvailableUsers(users);
    } catch (error) {
      console.error("Failed to load users:", error);
      toast.error(t.translations.UNABLE_TO_LOAD_USERS);
    } finally {
      setUserModalLoading(false);
    }
  };

  const handleAddInviteUser = async (
    emailOrUserId: string | number,
    roleId?: number,
    isProjectAdmin?: boolean,
  ) => {
    if (!organizationId) {
      throw new Error(t.translations.MISSING_ORG_ID);
    }

    // If it's a string (email), invite external user
    if (typeof emailOrUserId === "string") {
      const inviteData: InviteUserToOrganizationRequestDto = {
        userEmail: emailOrUserId,
        userName: emailOrUserId.split("@")[0],
      };

      await inviteUserToOrganization(organizationId, inviteData);
    }
    // If it's a number (userId), add existing org user to project
    else if (typeof emailOrUserId === "number" && roleId && projectId) {
      await addMemberToProject(organizationId, projectId, {
        roleId,
        userId: emailOrUserId,
        isProjectAdmin: isProjectAdmin ?? false,
      });
    }

    // Refresh the members list
    try {
      if (projectId) {
        const updatedMembers = await getProjectMembers(
          organizationId,
          projectId,
        );
        setTableData(buildTableData(updatedMembers));
      } else {
        const updatedMembers = await getAllUsers(organizationId);
        setTableData(buildTableData(updatedMembers));
      }
    } catch (refreshError) {
      console.error("Failed to refresh members list:", refreshError);
    }

    if (typeof emailOrUserId === "number") {
      await refreshAccessIfAffected("user", emailOrUserId);
    }
  };

  /* ------------------------------------------------------------------------ */
  /*                     Add Group Modal: Open & Handler                      */
  /* ------------------------------------------------------------------------ */

  const handleOpenAddGroupModal = async () => {
    if (!organizationId) {
      toast.error(t.translations.NO_ORG_SELECTED);
      return;
    }

    setShowAddGroupModal(true);
    setGroupModalLoading(true);

    try {
      const groups = await getAllGroups(organizationId);
      setAvailableGroups(groups);
    } catch (error) {
      console.error("Failed to load groups:", error);
      toast.error(t.translations.UNABLE_TO_LOAD_USERS_OR_GROUPS);
    } finally {
      setGroupModalLoading(false);
    }
  };

  const handleGetGroupMembers = (groupId: number): UserResponseDto[] => {
    if (!organizationId) return [];

    // Return cached if available
    if (groupMembersCache.has(groupId)) {
      return groupMembersCache.get(groupId)!;
    }

    // Fetch asynchronously and cache
    getGroupMembers(organizationId, groupId)
      .then((groupMembers) => {
        setGroupMembersCache((prev) =>
          new Map(prev).set(groupId, groupMembers),
        );
      })
      .catch((error) => {
        console.error(`Failed to load members for group ${groupId}:`, error);
      });

    return []; // Return empty while loading
  };
  
  const handleViewGroupMembers = async (row: ProjectMemberTableRow) => {
    if (!organizationId) {
      toast.error(t.translations.NO_ORG_SELECTED);
      return;
    }
    
    setViewGroupMembersModal({
      isOpen: true,
      groupName: row.name,
      members: [],
      loading: true,
    });
    
    try {
      // Use members from cache if it exists, if not fetch API
      const members = groupMembersCache.has(row.memberId)
        ? groupMembersCache.get(row.memberId)!
        : await getGroupMembers(organizationId, row.memberId);
      
      setGroupMembersCache((prev) => new Map(prev).set(row.memberId, members));
      
      setViewGroupMembersModal({
        isOpen: true,
        groupName: row.name,
        members,
        loading: false,
      });
    } catch (error) {
      console.error(`Failed to load members for group ${row.memberId}:`, error);
      toast.error(t.translations.UNABLE_TO_LOAD_USERS);
      
      setViewGroupMembersModal((prev) => ({
        ...prev,
        loading: false,
      }));
    }
  };

  const handleAddGroup = async () => {
    if (!organizationId || !projectId) {
      toast.error(t.translations.MISSING_ORG_OR_PROJECT);
      return;
    }

    if (!selectedGroupId) {
      toast.error(t.translations.PLEASE_SELECT_A_GROUP);
      return;
    }

    if (!selectedGroupRoleId) {
      toast.error(t.translations.PLEASE_SELECT_A_ROLE_FOR_MEMBER);
      return;
    }

    try {
      setGroupModalLoading(true);

      const roleId = Number(selectedGroupRoleId);
      const groupId = Number(selectedGroupId);

      await addMemberToProject(organizationId, projectId, {
        roleId,
        groupId,
      });

      toast.success(t.translations.MEMBER_ADDED_TO_PROJECT);

      // Refresh the members list
      const updatedMembers = await getProjectMembers(organizationId, projectId);
      setTableData(buildTableData(updatedMembers));
      await refreshAccessIfAffected("group", groupId);

      setShowAddGroupModal(false);
      setSelectedGroupId("");
      setSelectedGroupRoleId("");
    } catch (error) {
      console.error("Failed to add group to project:", error);
      toast.error(t.translations.FAILED_TO_ADD_MEMBER);
    } finally {
      setGroupModalLoading(false);
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
      currentIsProjectAdmin: row.isProjectAdmin,
    });
    setEditRoleSelectedId(row.roleId ? String(row.roleId) : "");
    setEditIsProjectAdmin(row.isProjectAdmin);
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
          editIsProjectAdmin,
        );
      } else {
        await updateProjectMemberRole(
          organizationId,
          projectId,
          roleId,
          undefined,
          memberId,
          editIsProjectAdmin,
        );
      }

      const selectedRole = roles.find((r) => r.id === roleId);

      setTableData((prev) =>
        prev.map((row) =>
          row.memberId === memberId
            ? { ...row, role: selectedRole?.name ?? null, roleId, isProjectAdmin: editIsProjectAdmin }
            : row,
        ),
      );

      await refreshAccessIfAffected(editRoleModal.memberType, memberId);
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
        currentIsProjectAdmin: false,
      });
      setEditRoleSelectedId("");
      setEditIsProjectAdmin(false);
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
      await refreshAccessIfAffected(confirmModal.memberType, memberId);

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
            onInviteUser={handleOpenAddUsersModal}
            onAddGroup={handleOpenAddGroupModal}
          />

          <ProjectUsersListTable
            tableData={tableData}
            loading={loading}
            onEditRole={handleOpenEditRoleModal}
            onViewGroupMembers={handleViewGroupMembers}
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

          {/* Add Users Modal - handles both org users and external invites */}
          <AddUsersToProjectModal
            isOpen={showAddUserModal}
            roles={roles}
            availableOrgUsers={availableUsers}
            projectMembers={members}
            modalLoading={userModalLoading}
            onClose={() => setShowAddUserModal(false)}
            onAddInviteUser={handleAddInviteUser}
          />

          {/* Add Group Modal */}
          <AddGroupToProjectModal
            isOpen={showAddGroupModal}
            roles={roles}
            availableGroups={availableGroups}
            projectMembers={members}
            selectedGroupId={selectedGroupId}
            selectedRoleId={selectedGroupRoleId}
            modalLoading={groupModalLoading}
            onClose={() => {
              setShowAddGroupModal(false);
              setSelectedGroupId("");
              setSelectedGroupRoleId("");
            }}
            onChangeGroup={setSelectedGroupId}
            onChangeRole={setSelectedGroupRoleId}
            onConfirm={handleAddGroup}
            getGroupMembers={handleGetGroupMembers}
          />

          {/* Edit Role Modal */}
          <EditProjectMemberRoleModal
            editRoleModal={editRoleModal}
            roles={roles}
            loading={loading}
            selectedRoleId={editRoleSelectedId}
            isProjectAdmin={editIsProjectAdmin}
            onChangeRole={setEditRoleSelectedId}
            onChangeIsProjectAdmin={setEditIsProjectAdmin}
            onCancel={() => {
              setEditRoleModal({
                isOpen: false,
                memberId: null,
                memberName: "",
                memberType: null,
                currentRoleId: null,
                currentIsProjectAdmin: false,
              });
              setEditRoleSelectedId("");
              setEditIsProjectAdmin(false);
            }}
            onSave={handleSaveMemberRole}
          />

          {/* View Group Members Modal*/}
          {viewGroupMembersModal.isOpen && (
              <dialog className="modal modal-open">
                <div className="modal-box">
                  <h3 className="font-bold text-lg">
                    Users in {viewGroupMembersModal.groupName}
                  </h3>

                  {viewGroupMembersModal.loading ? (
                      <div className="flex justify-center py-8">
                        <span className="loading loading-spinner loading-lg" />
                      </div>
                  ) : viewGroupMembersModal.members.length === 0 ? (
                      <p className="py-4 text-base-content/70">
                        No users in this group.
                      </p>
                  ) : (
                      <div className="py-4 space-y-2 max-h-80 overflow-y-auto">
                        {viewGroupMembersModal.members.map((user) => (
                            <div key={user.id} className="p-3 rounded-lg bg-base-200">
                              <p className="font-semibold">{user.name || user.email}</p>
                              <p className="text-sm text-base-content/70">{user.email}</p>
                            </div>
                        ))}
                      </div>
                  )}
                  
                  <div className="modal-action">
                    <button
                      className="btn"
                      onClick={() =>
                        setViewGroupMembersModal({
                          isOpen: false,
                          groupName: "",
                          members: [],
                          loading: false,
                        })
                      }
                    >
                      Close
                    </button>
                  </div>
                </div>
              </dialog>
          )}
        </div>
      </div>
    </div>
  );
};

export default ProjectUsersTable;
