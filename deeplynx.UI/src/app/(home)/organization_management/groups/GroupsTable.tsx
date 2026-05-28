// src/app/(home)/organization_management/groups/GroupsTable.tsx
"use client";

import React from "react";
import {
  CheckIcon,
  ChevronDownIcon,
  ChevronUpIcon,
  PencilIcon,
  PlusIcon,
  TrashIcon,
  UserGroupIcon,
  XMarkIcon,
} from "@heroicons/react/24/outline";

import AvatarCell from "../../components/Avatar";
import { GroupResponseDto, UserResponseDto } from "../../types/responseDTOs";

/* -------------------------------------------------------------------------- */
/*                                   Types                                    */
/* -------------------------------------------------------------------------- */

interface GroupsTableProps {
  groups: GroupResponseDto[];
  loading: boolean;
  selectedGroups: Set<string | number>;
  expandedGroup: string | number | null;
  editingGroup: string | number | null;
  loadingMembers: Set<string | number>;
  memberSearchTerm: string;
  editName: string;
  editDescription: string;
  groupMembers: Map<string | number, UserResponseDto[]>;
  availableUsers: UserResponseDto[];
  getAvailableUsers: (groupId: string | number) => UserResponseDto[];
  filterUsersBySearch: (users: UserResponseDto[]) => UserResponseDto[];

  onToggleSelectAll: () => void;
  onToggleSelectGroup: (groupId: string | number) => void;
  onArchiveSelected: () => void;

  onToggleExpand: (groupId: string | number) => void;
  onStartEdit: (group: GroupResponseDto) => void;
  onCancelEdit: () => void;
  onUpdateGroup: (groupId: string | number) => void;
  onChangeEditName: (value: string) => void;
  onChangeEditDescription: (value: string) => void;

  onRemoveMember: (groupId: string | number, userId: number) => void;
  onAddMember: (groupId: string | number, userId: number) => void;
  onChangeMemberSearch: (value: string) => void;

  onArchiveGroup: (groupId: string | number) => void;
}

interface GroupRowProps {
  group: GroupResponseDto;
  loading: boolean;
  isSelected: boolean;
  isExpanded: boolean;
  isEditing: boolean;
  isLoadingMembers: boolean;
  hasMembersLoaded: boolean;
  memberSearchTerm: string;
  editName: string;
  editDescription: string;
  currentMembers: UserResponseDto[];
  availableUsersForGroup: UserResponseDto[];
  filteredAvailable: UserResponseDto[];

  onToggleSelect: () => void;
  onToggleExpand: () => void;
  onStartEdit: () => void;
  onCancelEdit: () => void;
  onUpdateGroup: () => void;
  onChangeEditName: (value: string) => void;
  onChangeEditDescription: (value: string) => void;

  onRemoveMember: (userId: number) => void;
  onAddMember: (userId: number) => void;
  onChangeMemberSearch: (value: string) => void;

  onArchiveGroup: () => void;
}

/* -------------------------------------------------------------------------- */
/*                             GroupsTable Component                          */
/* -------------------------------------------------------------------------- */

const GroupsTable: React.FC<GroupsTableProps> = ({
  groups,
  loading,
  selectedGroups,
  expandedGroup,
  editingGroup,
  loadingMembers,
  memberSearchTerm,
  editName,
  editDescription,
  groupMembers,
  getAvailableUsers,
  filterUsersBySearch,
  onToggleSelectAll,
  onToggleSelectGroup,
  onArchiveSelected,
  onToggleExpand,
  onStartEdit,
  onCancelEdit,
  onUpdateGroup,
  onChangeEditName,
  onChangeEditDescription,
  onRemoveMember,
  onAddMember,
  onChangeMemberSearch,
  onArchiveGroup,
}) => {
  return (
    <div className="rounded-lg shadow-xl overflow-hidden border-2 border-primary">
      <table className="table w-full">
        <thead>
          <tr>
            <th className="w-12">
              <input
                type="checkbox"
                className="checkbox checkbox-sm"
                checked={
                  selectedGroups.size === groups.length && groups.length > 0
                }
                onChange={onToggleSelectAll}
              />
            </th>
            <th>Group Name</th>
            <th>Description</th>
            <th>Members</th>
            <th className="w-32">
              {selectedGroups.size > 0 && (
                <button
                  onClick={onArchiveSelected}
                  className="btn btn-ghost btn-sm text-error"
                  disabled={loading}
                >
                  <TrashIcon className="size-6" />
                  Delete ({selectedGroups.size})
                </button>
              )}
            </th>
            <th className="w-12" />
          </tr>
        </thead>
        <tbody>
          {groups.length === 0 ? (
            <tr>
              <td colSpan={6} className="text-center py-8 text-base-content/70">
                No groups found. Create your first group to get started.
              </td>
            </tr>
          ) : (
            groups.map((group) => {
              const currentMembers = groupMembers.get(group.id) || [];
              const availableUsersForGroup = getAvailableUsers(group.id);
              const filteredAvailable = filterUsersBySearch(
                availableUsersForGroup
              );
              const isLoadingMembers = loadingMembers.has(group.id);

              return (
                <GroupRow
                  key={group.id}
                  group={group}
                  loading={loading}
                  isSelected={selectedGroups.has(group.id)}
                  isExpanded={expandedGroup === group.id}
                  isEditing={editingGroup === group.id}
                  isLoadingMembers={isLoadingMembers}
                  hasMembersLoaded={groupMembers.has(group.id)}
                  memberSearchTerm={memberSearchTerm}
                  editName={editName}
                  editDescription={editDescription}
                  currentMembers={currentMembers}
                  availableUsersForGroup={availableUsersForGroup}
                  filteredAvailable={filteredAvailable}
                  onToggleSelect={() => onToggleSelectGroup(group.id)}
                  onToggleExpand={() => onToggleExpand(group.id)}
                  onStartEdit={() => onStartEdit(group)}
                  onCancelEdit={onCancelEdit}
                  onUpdateGroup={() => onUpdateGroup(group.id)}
                  onChangeEditName={onChangeEditName}
                  onChangeEditDescription={onChangeEditDescription}
                  onRemoveMember={(userId) => onRemoveMember(group.id, userId)}
                  onAddMember={(userId) => onAddMember(group.id, userId)}
                  onChangeMemberSearch={onChangeMemberSearch}
                  onArchiveGroup={() => onArchiveGroup(group.id)}
                />
              );
            })
          )}
        </tbody>
      </table>
    </div>
  );
};

/* -------------------------------------------------------------------------- */
/*                               GroupRow Component                           */
/* -------------------------------------------------------------------------- */

const GroupRow: React.FC<GroupRowProps> = ({
  group,
  loading,
  isSelected,
  isExpanded,
  isEditing,
  isLoadingMembers,
  hasMembersLoaded,
  memberSearchTerm,
  editName,
  editDescription,
  currentMembers,
  availableUsersForGroup,
  filteredAvailable,
  onToggleSelect,
  onToggleExpand,
  onStartEdit,
  onCancelEdit,
  onUpdateGroup,
  onChangeEditName,
  onChangeEditDescription,
  onRemoveMember,
  onAddMember,
  onChangeMemberSearch,
  onArchiveGroup,
}) => {
  return (
    <>
      {/* Main Row */}
      <tr
        className={`hover cursor-pointer ${isExpanded ? "bg-base-200" : ""}`}
        onClick={onToggleExpand}
      >
        <td
          onClick={(e) => {
            e.stopPropagation();
          }}
        >
          <input
            type="checkbox"
            className="checkbox checkbox-sm"
            checked={isSelected}
            onChange={onToggleSelect}
          />
        </td>
        <td className="font-semibold">{group.name}</td>
        <td className="text-base-content/70">
          {group.description || "No description"}
        </td>
        <td>
          <div className="flex items-center gap-2">
            <UserGroupIcon className="size-6" />
            <span className="badge badge-ghost">
              {hasMembersLoaded ? currentMembers.length : group.memberCount ?? 0}
            </span>
          </div>
        </td>
        <td
          onClick={(e) => {
            e.stopPropagation();
          }}
        >
          <div className="flex gap-2">
            <button
              className="btn btn-ghost btn-sm"
              onClick={() => {
                onStartEdit();
              }}
              disabled={loading}
            >
              <PencilIcon className="size-6" />
            </button>
            <button
              className="btn btn-ghost btn-sm"
              onClick={onArchiveGroup}
              disabled={loading}
            >
              <TrashIcon className="size-6 text-error" />
            </button>
          </div>
        </td>
        <td>
          {isExpanded ? (
            <ChevronUpIcon className="size-6" />
          ) : (
            <ChevronDownIcon className="size-6" />
          )}
        </td>
      </tr>

      {/* Expanded Row */}
      {isExpanded && (
        <tr>
          <td colSpan={6} className="bg-base-100 p-0">
            <div className="p-6 border-t-2 border-primary/20">
              {/* Edit Group Section */}
              {isEditing ? (
                <div className="space-y-4">
                  <div className="flex justify-between items-center mb-4">
                    <h4 className="font-bold text-lg">Edit Group Details</h4>
                    <div className="flex gap-2">
                      <button
                        className="btn btn-sm btn-ghost"
                        onClick={onCancelEdit}
                        disabled={loading}
                      >
                        Cancel
                      </button>
                      <button
                        className="btn btn-sm btn-primary"
                        onClick={onUpdateGroup}
                        disabled={loading || !editName.trim()}
                      >
                        {loading ? (
                          <span className="loading loading-spinner loading-sm" />
                        ) : (
                          <>
                            <CheckIcon className="w-4 h-4" />
                            Save Changes
                          </>
                        )}
                      </button>
                    </div>
                  </div>

                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <div className="form-control">
                      <label className="label">
                        <span className="label-text font-semibold">
                          Group Name
                        </span>
                      </label>
                      <input
                        type="text"
                        className="input input-bordered w-full"
                        value={editName}
                        onChange={(e) => onChangeEditName(e.target.value)}
                        disabled={loading}
                      />
                    </div>
                    <div className="form-control">
                      <label className="label">
                        <span className="label-text font-semibold">
                          Description
                        </span>
                      </label>
                      <input
                        type="text"
                        className="input input-bordered w-full"
                        value={editDescription}
                        onChange={(e) =>
                          onChangeEditDescription(e.target.value)
                        }
                        disabled={loading}
                      />
                    </div>
                  </div>
                </div>
              ) : (
                <div className="flex justify-between items-start mb-4">
                  <div>
                    <h4 className="font-bold text-lg">{group.name}</h4>
                    <p className="text-base-content/70">
                      {group.description || "No description"}
                    </p>
                  </div>
                  <button
                    className="btn btn-sm btn-ghost"
                    onClick={onStartEdit}
                    disabled={loading}
                  >
                    <PencilIcon className="w-4 h-4" />
                    Edit Details
                  </button>
                </div>
              )}

              {/* Member Management Section */}
              <div className="divider my-4">Member Management</div>

              {isLoadingMembers ? (
                <div className="flex justify-center items-center py-8">
                  <span className="loading loading-spinner loading-lg" />
                </div>
              ) : (
                <div className="grid grid-cols-1 md:grid-cols-2 gap-6 max-h-96">
                  {/* Current Members */}
                  <div className="flex flex-col">
                    <div className="flex justify-between items-center mb-3">
                      <h5 className="font-semibold flex items-center gap-2">
                        <UserGroupIcon className="w-5 h-5" />
                        Current Members ({currentMembers.length})
                      </h5>
                    </div>
                    <div className="space-y-2 max-h-80 overflow-y-auto">
                      {currentMembers.length === 0 ? (
                        <div className="text-center py-4 text-base-content/60">
                          No members in this group yet
                        </div>
                      ) : (
                        currentMembers.map((user) => (
                          <div
                            key={user.id}
                            className="flex items-center justify-between p-3 bg-base-200 rounded-lg hover:bg-base-300 transition"
                          >
                            <div className="flex items-center gap-3">
                              <AvatarCell name={user.name} />
                              <div>
                                <p className="font-medium">{user.name}</p>
                                <p className="text-sm text-base-content/60">
                                  {user.email}
                                </p>
                              </div>
                            </div>
                            <button
                              className="btn btn-ghost btn-sm text-error hover:bg-error/20"
                              onClick={() => onRemoveMember(user.id!)}
                              disabled={loading}
                            >
                              <XMarkIcon className="size-6" />
                            </button>
                          </div>
                        ))
                      )}
                    </div>
                  </div>

                  {/* Add Members */}
                  <div className="flex flex-col">
                    <div className="flex justify-between items-center mb-3">
                      <h5 className="font-semibold">Add Members</h5>
                    </div>
                    <div className="form-control mb-3">
                      <div className="input-group">
                        <span className="bg-base-300" />
                        <input
                          type="text"
                          placeholder="Search users..."
                          className="input input-bordered w-full"
                          value={memberSearchTerm}
                          onChange={(e) => onChangeMemberSearch(e.target.value)}
                        />
                      </div>
                    </div>
                    <div className="space-y-2 max-h-64 overflow-y-auto">
                      {filteredAvailable.length === 0 ? (
                        <div className="text-center py-4 text-base-content/60">
                          {availableUsersForGroup.length === 0
                            ? "All users are already in this group"
                            : "No users found"}
                        </div>
                      ) : (
                        filteredAvailable.map((user) => (
                          <div
                            key={user.id}
                            className="flex items-center justify-between p-3 bg-base-200 rounded-lg hover:bg-base-300 transition"
                          >
                            <div className="flex items-center gap-3">
                              <AvatarCell name={user.name} />
                              <div>
                                <p className="font-medium">{user.name}</p>
                                <p className="text-sm text-base-content/60">
                                  {user.email}
                                </p>
                              </div>
                            </div>
                            <button
                              className="btn btn-ghost btn-sm text-success hover:bg-success/20"
                              onClick={() => onAddMember(user.id!)}
                              disabled={loading}
                            >
                              <PlusIcon className="size-6" />
                            </button>
                          </div>
                        ))
                      )}
                    </div>
                  </div>
                </div>
              )}
            </div>
          </td>
        </tr>
      )}
    </>
  );
};

export default GroupsTable;
