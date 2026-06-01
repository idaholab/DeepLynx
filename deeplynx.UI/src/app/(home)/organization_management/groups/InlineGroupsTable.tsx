// src/app/(home)/organization_management/groups/InlineGroupsTable.tsx
"use client";

import React, { useEffect, useState } from "react";
import { PlusIcon, TrashIcon } from "@heroicons/react/24/outline";
import toast from "react-hot-toast";

import { useOrganizationSession } from "@/app/contexts/OrganizationSessionProvider";
import {
  addUserToGroup,
  archiveGroup,
  createGroup,
  getGroupMembers,
  removeUserFromGroup,
  updateGroup,
} from "@/app/lib/client_service/group_services.client";

import {
  CreateGroupRequestDto,
  UpdateGroupRequestDto,
} from "../../types/requestDTOs";
import { GroupResponseDto, UserResponseDto } from "../../types/responseDTOs";

import GroupArchiveModal from "./GroupArchiveModal";
import CreateGroupInlineForm from "./CreateGroupInlineForm";
import GroupsTable from "./GroupsTable";

/* -------------------------------------------------------------------------- */
/*                                   Types                                    */
/* -------------------------------------------------------------------------- */

interface InlineGroupsTableProps {
  initialGroups: GroupResponseDto[];
  availableUsers: UserResponseDto[];
  organizationId?: number | string;
  onGroupsChange?: () => Promise<void>; // Add this prop
}

/* -------------------------------------------------------------------------- */
/*                           InlineGroupsTable Component                      */
/* -------------------------------------------------------------------------- */

const InlineGroupsTable: React.FC<InlineGroupsTableProps> = ({
  initialGroups,
  availableUsers,
  organizationId,
  onGroupsChange
}) => {
  /* ------------------------------------------------------------------------ */
  /*                               Core State                                */
  /* ------------------------------------------------------------------------ */

  const [groups, setGroups] = useState<GroupResponseDto[]>(initialGroups);
  const [expandedGroup, setExpandedGroup] = useState<string | number | null>(
    null
  );
  const [editingGroup, setEditingGroup] = useState<string | number | null>(
    null
  );
  const [showCreateForm, setShowCreateForm] = useState(false);
  const [loading, setLoading] = useState(false);
  const [, setError] = useState<string | null>(null);

  /* ------------------------------------------------------------------------ */
  /*                         Member Management State                          */
  /* ------------------------------------------------------------------------ */

  const [groupMembers, setGroupMembers] = useState<
    Map<string | number, UserResponseDto[]>
  >(new Map());
  const [loadingMembers, setLoadingMembers] = useState<Set<string | number>>(
    new Set()
  );
  const [memberSearchTerm, setMemberSearchTerm] = useState("");

  /* ------------------------------------------------------------------------ */
  /*                             Create Group State                           */
  /* ------------------------------------------------------------------------ */

  const [newGroupName, setNewGroupName] = useState("");
  const [newGroupDescription, setNewGroupDescription] = useState("");

  /* ------------------------------------------------------------------------ */
  /*                              Edit Group State                            */
  /* ------------------------------------------------------------------------ */

  const [editName, setEditName] = useState("");
  const [editDescription, setEditDescription] = useState("");

  /* ------------------------------------------------------------------------ */
  /*                           Selection / Context                            */
  /* ------------------------------------------------------------------------ */

  const [selectedGroups, setSelectedGroups] = useState<Set<string | number>>(
    new Set()
  );
  const { organization } = useOrganizationSession();

  /* ------------------------------------------------------------------------ */
  /*                           Archive Group Modal                            */
  /* ------------------------------------------------------------------------ */

  const [archiveModal, setArchiveModal] = useState<{
    isOpen: boolean;
    groupIds: Array<string | number>;
    groupNames: string[];
    totalMembers?: number;
  }>({
    isOpen: false,
    groupIds: [],
    groupNames: [],
    totalMembers: undefined,
  });

  /* ------------------------------------------------------------------------ */
  /*                      Fetching & Syncing Group Members                    */
  /* ------------------------------------------------------------------------ */

  useEffect(() => {
    const loadGroupData = async () => {
      const orgId = organization?.organizationId ?? organizationId;
      
      if (!orgId || initialGroups.length === 0) return;
      
      const preloadedGroupMembers = new Map<
          string | number,
          UserResponseDto[]
      >();
      
      // Parallel fetch members from all groups
      const groupsWithCounts = await Promise.all(
          initialGroups.map(async (group) => {
            try {
              // Check current group's member
              const members = await getGroupMembers(
                  Number(orgId),
                  Number(group.id)
              );
              
              preloadedGroupMembers.set(group.id, members);
              
              return {
                ...group,
                memberCount: members.length,
              };
            } catch (err) {
              console.error(
                  `Failed to load members for group ${group.id}`,
                  err
              );
              
              return {
                ...group,
                memberCount: group.memberCount ?? 0,
              };
            }
          })
      );
      
      setGroups(groupsWithCounts);
      setGroupMembers(preloadedGroupMembers);
    };
    loadGroupData();
  }, [initialGroups, organization?.organizationId, organizationId]);
  
  const fetchGroupMembers = async (groupId: string | number) => {
    setLoadingMembers((prev) => new Set(prev).add(groupId));

    try {
      const members = await getGroupMembers(
        organization?.organizationId as number,
        groupId as number
      );

      setGroupMembers((prev) => {
        const copy = new Map(prev);
        copy.set(groupId, members);
        return copy;
      });

      setGroups((prev) =>
        prev.map((g) =>
          g.id === groupId ? { ...g, memberCount: members.length } : g
        )
      );
    } catch (err) {
      console.error("Failed to fetch group members:", err);
      setError(err instanceof Error ? err.message : "Failed to fetch members");
    } finally {
      setLoadingMembers((prev) => {
        const copy = new Set(prev);
        copy.delete(groupId);
        return copy;
      });
    }
  };

  const toggleExpand = async (groupId: string | number) => {
    if (expandedGroup === groupId) {
      setExpandedGroup(null);
      setEditingGroup(null);
    } else {
      setExpandedGroup(groupId);
      setEditingGroup(null);
      
      if (!groupMembers.has(groupId)) {
        await fetchGroupMembers(groupId);
      }
    }
  };

  /* ------------------------------------------------------------------------ */
  /*                          Create / Edit Group Handlers                    */
  /* ------------------------------------------------------------------------ */

  const startEdit = (group: GroupResponseDto) => {
    setEditingGroup(group.id);
    setEditName(group.name);
    setEditDescription(group.description || "");
  };

  const cancelEdit = () => {
    setEditingGroup(null);
    setEditName("");
    setEditDescription("");
  };

  const handleCreateGroup = async () => {
    if (!newGroupName.trim() || !organizationId) {
      setError("Group name is required");
      return;
    }

    setLoading(true);
    setError(null);

    const dto: CreateGroupRequestDto = {
      name: newGroupName,
      description: newGroupDescription,
    };

    try {
      const newGroup = await createGroup(organizationId as number, dto);
      setGroups((prev) => [...prev, newGroup]);
      setNewGroupName("");
      setNewGroupDescription("");
      setShowCreateForm(false);
      toast.success("Group created");

      if (onGroupsChange) {
        await onGroupsChange();
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to create group");
      toast.error("Group not created");
    } finally {
      setLoading(false);
    }
  };

  const handleUpdateGroup = async (groupId: string | number) => {
    if (!editName.trim()) {
      setError("Group name is required");
      return;
    }

    setLoading(true);
    setError(null);

    const dto: UpdateGroupRequestDto = {
      name: editName,
      description: editDescription,
    };

    try {
      const updatedGroup = await updateGroup(
        organization?.organizationId as number,
        groupId as number,
        dto
      );

      setGroups((prev) =>
        prev.map((g) => (g.id === groupId ? updatedGroup : g))
      );

      setEditingGroup(null);
      setEditName("");
      setEditDescription("");

      if (onGroupsChange) {
        await onGroupsChange();
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to update group");
    } finally {
      setLoading(false);
    }
  };

  /* ------------------------------------------------------------------------ */
  /*                          Archive / Bulk Archive Logic                    */
  /* ------------------------------------------------------------------------ */

  const handleArchiveGroup = (groupId: string | number) => {
    openArchiveModalForGroups([groupId]);
  };

  const handleArchiveSelected = () => {
    if (selectedGroups.size === 0) return;
    openArchiveModalForGroups(Array.from(selectedGroups));
  };

  const handleConfirmArchiveModal = async () => {
    if (!archiveModal.groupIds.length) {
      setArchiveModal((prev) => ({ ...prev, isOpen: false }));
      return;
    }

    setLoading(true);
    setError(null);

    const selectedIds = new Set(archiveModal.groupIds);

    // Optimistic: remove them from the list immediately
    setGroups((prev) => prev.filter((g) => !selectedIds.has(g.id)));

    try {
      await Promise.all(
        archiveModal.groupIds.map((id) =>
          archiveGroup(
            organization?.organizationId as number,
            id as number,
            true
          )
        )
      );

      setSelectedGroups((prev) => {
        const copy = new Set(prev);
        archiveModal.groupIds.forEach((id) => copy.delete(id));
        return copy;
      });

      if (expandedGroup && selectedIds.has(expandedGroup)) {
        setExpandedGroup(null);
      }
      if (editingGroup && selectedIds.has(editingGroup)) {
        setEditingGroup(null);
        setEditName("");
        setEditDescription("");
      }

      toast.success(
        archiveModal.groupIds.length === 1
          ? "Group archived"
          : "Groups archived"
      );
      if (onGroupsChange) {
        await onGroupsChange();
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to archive groups");
      toast.error("Failed to archive groups");
    } finally {
      setLoading(false);
      setArchiveModal({
        isOpen: false,
        groupIds: [],
        groupNames: [],
        totalMembers: undefined,
      });
    }
  };

  const openArchiveModalForGroups = (groupIds: Array<string | number>) => {
    if (groupIds.length === 0) return;

    const selected = groups.filter((g) => groupIds.includes(g.id));
    const groupNames = selected.map((g) => g.name);

    let totalMembers: number | undefined = undefined;
    if (groupIds.length === 1) {
      const groupId = groupIds[0];
      const currentMembers = getCurrentMembers(groupId);
      const fromState = groups.find((g) => g.id === groupId)?.memberCount;
      totalMembers = currentMembers.length || fromState || 0;
    }

    setArchiveModal({
      isOpen: true,
      groupIds,
      groupNames,
      totalMembers,
    });
  };

  /* ------------------------------------------------------------------------ */
  /*                           Member Management Logic                        */
  /* ------------------------------------------------------------------------ */

  const handleAddMember = async (
    groupId: string | number,
    userId: string | number
  ) => {
    setLoading(true);
    setError(null);

    try {
      await addUserToGroup(
        organization?.organizationId as number,
        groupId as number,
        userId as number
      );

      await fetchGroupMembers(groupId);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to add member");
    } finally {
      setLoading(false);
    }
  };

  const handleRemoveMember = async (
    groupId: string | number,
    userId: string | number
  ) => {
    setLoading(true);
    setError(null);

    try {
      await removeUserFromGroup(
        organization?.organizationId as number,
        groupId as number,
        userId as number
      );

      await fetchGroupMembers(groupId);
      toast.success("Removed member");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to remove member");
      toast.error("Failed to remove member");
    } finally {
      setLoading(false);
    }
  };

  /* ------------------------------------------------------------------------ */
  /*                        Selection / Helper Computations                   */
  /* ------------------------------------------------------------------------ */

  const toggleSelectGroup = (groupId: string | number) => {
    const newSelection = new Set(selectedGroups);
    if (newSelection.has(groupId)) {
      newSelection.delete(groupId);
    } else {
      newSelection.add(groupId);
    }
    setSelectedGroups(newSelection);
  };

  const toggleSelectAll = () => {
    if (selectedGroups.size === groups.length) {
      setSelectedGroups(new Set());
    } else {
      setSelectedGroups(new Set(groups.map((g) => g.id)));
    }
  };

  const getCurrentMembers = (groupId: string | number): UserResponseDto[] => {
    return groupMembers.get(groupId) || [];
  };

  const getAvailableUsers = (groupId: string | number): UserResponseDto[] => {
    const currentMembers = getCurrentMembers(groupId);
    const memberIds = new Set(currentMembers.map((m) => m.id));

    return availableUsers.filter(
      (u) => !memberIds.has(u.id) && u.isActive && !u.isArchived
    );
  };

  const filterUsersBySearch = (users: UserResponseDto[]): UserResponseDto[] => {
    if (!memberSearchTerm.trim()) return users;

    const searchLower = memberSearchTerm.toLowerCase();

    return users.filter(
      (u) =>
        u.name?.toLowerCase().includes(searchLower) ||
        u.email?.toLowerCase().includes(searchLower)
    );
  };

  /* ------------------------------------------------------------------------ */
  /*                               Main Render                                */
  /* ------------------------------------------------------------------------ */

  return (
    <div className="p-6">
      <div className="mx-auto">
        {/* Header */}
        <div className="flex justify-between items-center mb-6">
          <div>
            <h2 className="text-2xl font-bold text-base-content">
              Group Management
            </h2>
            <p className="text-base-content/70 mt-1">
              Create and manage user groups for your organization
            </p>
          </div>
          <button
            className="btn btn-primary gap-2"
            onClick={() => setShowCreateForm(!showCreateForm)}
            disabled={loading}
          >
            <PlusIcon className="size-6" />
            Create Group
          </button>
        </div>

        {/* Create Group Inline Form */}
        <CreateGroupInlineForm
          show={showCreateForm}
          loading={loading}
          newGroupName={newGroupName}
          newGroupDescription={newGroupDescription}
          onChangeName={setNewGroupName}
          onChangeDescription={setNewGroupDescription}
          onCancel={() => {
            setShowCreateForm(false);
            setNewGroupName("");
            setNewGroupDescription("");
          }}
          onSubmit={handleCreateGroup}
        />

        {/* Groups Table */}
        <GroupsTable
          groups={groups}
          loading={loading}
          selectedGroups={selectedGroups}
          expandedGroup={expandedGroup}
          editingGroup={editingGroup}
          loadingMembers={loadingMembers}
          memberSearchTerm={memberSearchTerm}
          editName={editName}
          editDescription={editDescription}
          groupMembers={groupMembers}
          availableUsers={availableUsers}
          getAvailableUsers={getAvailableUsers}
          filterUsersBySearch={filterUsersBySearch}
          onToggleSelectAll={toggleSelectAll}
          onToggleSelectGroup={toggleSelectGroup}
          onArchiveSelected={handleArchiveSelected}
          onToggleExpand={toggleExpand}
          onStartEdit={startEdit}
          onCancelEdit={cancelEdit}
          onUpdateGroup={handleUpdateGroup}
          onChangeEditName={setEditName}
          onChangeEditDescription={setEditDescription}
          onRemoveMember={handleRemoveMember}
          onAddMember={handleAddMember}
          onChangeMemberSearch={setMemberSearchTerm}
          onArchiveGroup={handleArchiveGroup}
        />

        {/* Confirm Archive Modal */}
        <GroupArchiveModal
          isOpen={archiveModal.isOpen}
          groupNames={archiveModal.groupNames}
          totalMembers={archiveModal.totalMembers}
          loading={loading}
          onClose={() =>
            setArchiveModal({
              isOpen: false,
              groupIds: [],
              groupNames: [],
              totalMembers: undefined,
            })
          }
          onConfirm={handleConfirmArchiveModal}
        />
      </div>
    </div>
  );
};

export default InlineGroupsTable;
