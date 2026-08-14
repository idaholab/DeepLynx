// src/app/lib/group_services.client.ts
"use client";

import { CreateGroupRequestDto, UpdateGroupRequestDto } from "@/app/(home)/types/requestDTOs";
import { GroupResponseDto, PaginatedResponse, UserResponseDto } from "@/app/(home)/types/responseDTOs";
import api from "./api";



/**
 * Get all groups within an organization
 * @param organizationId - The ID of the organization
 * @param hideArchived - Flag to hide archived groups (default: true)
 * @param pageNumber - Page number to fetch (default: 1)
 * @param pageSize - Page size; -1 fetches all classes (default: -1)
 * @returns Promise with array of GroupResponseDto
 */
export async function getAllGroups(
  organizationId: number,
  hideArchived: boolean = true,
  pageNumber: number = 1,
  pageSize: number = -1
): Promise<PaginatedResponse<GroupResponseDto>> {
  try {
    const res = await api.get(
      `/organizations/${organizationId}/groups`,
      { params: { hideArchived, pageNumber, pageSize } }
    );
    return res.data;
  } catch (error) {
    console.error("Error getting all groups:", error);
    throw error;
  }
}

/**
 * Get a specific group by ID
 * @param organizationId - The ID of the organization
 * @param groupId - The ID of the group
 * @param hideArchived - Flag to hide archived groups (default: true)
 * @returns Promise with GroupResponseDto
 */
export async function getGroup(
  organizationId: number,
  groupId: number,
  hideArchived: boolean = true
): Promise<GroupResponseDto> {
  try {
    const res = await api.get(
      `/organizations/${organizationId}/groups/${groupId}`,
      { params: { hideArchived } }
    );
    return res.data;
  } catch (error) {
    console.error(`Error getting group ${groupId}:`, error);
    throw error;
  }
}

/**
 * Get all members of a group
 * @param organizationId - The ID of the organization
 * @param groupId - The ID of the group
 * @returns Promise with array of UserResponseDto
 */
export async function getGroupMembers(
  organizationId: number,
  groupId: number
): Promise<UserResponseDto[]> {
  try {
    const res = await api.get(
      `/organizations/${organizationId}/groups/${groupId}/users`
    );
    return res.data;
  } catch (error) {
    console.error(`Error getting members for group ${groupId}:`, error);
    throw error;
  }
}

/**
 * Create a new group
 * @param organizationId - The ID of the organization
 * @param dto - The group creation request DTO
 * @returns Promise with GroupResponseDto
 */
export async function createGroup(
  organizationId: number,
  dto: CreateGroupRequestDto
): Promise<GroupResponseDto> {
  try {
    const res = await api.post(
      `/organizations/${organizationId}/groups`,
      dto,
      { headers: { "Content-Type": "application/json" } }
    );
    return res.data;
  } catch (error) {
    console.error("Error creating group:", error);
    throw error;
  }
}

/**
 * Update a group
 * @param organizationId - The ID of the organization
 * @param groupId - The ID of the group to update
 * @param dto - The group update request DTO
 * @returns Promise with GroupResponseDto
 */
export async function updateGroup(
  organizationId: number,
  groupId: number,
  dto: UpdateGroupRequestDto
): Promise<GroupResponseDto> {
  try {
    const res = await api.put(
      `/organizations/${organizationId}/groups/${groupId}`,
      dto,
      { headers: { "Content-Type": "application/json" } }
    );
    return res.data;
  } catch (error) {
    console.error(`Error updating group ${groupId}:`, error);
    throw error;
  }
}

/**
 * Delete a group
 * @param organizationId - The ID of the organization
 * @param groupId - The ID of the group to delete
 * @returns Promise with success message
 */
export async function deleteGroup(
  organizationId: number,
  groupId: number
): Promise<{ message: string }> {
  try {
    const res = await api.delete(
      `/organizations/${organizationId}/groups/${groupId}`
    );
    return res.data;
  } catch (error) {
    console.error(`Error deleting group ${groupId}:`, error);
    throw error;
  }
}

/**
 * Archive or unarchive a group
 * @param organizationId - The ID of the organization
 * @param groupId - The ID of the group to archive/unarchive
 * @param archive - True to archive, false to unarchive
 * @returns Promise with success message
 */
export async function archiveGroup(
  organizationId: number,
  groupId: number,
  archive: boolean
): Promise<{ message: string }> {
  try {
    const res = await api.patch(
      `/organizations/${organizationId}/groups/${groupId}`,
      null,
      { params: { archive } }
    );
    return res.data;
  } catch (error) {
    console.error(`Error ${archive ? 'archiving' : 'unarchiving'} group ${groupId}:`, error);
    throw error;
  }
}

/**
 * Add a user to a group
 * @param organizationId - The ID of the organization
 * @param groupId - The ID of the group
 * @param userId - The ID of the user to add
 * @returns Promise with success message
 */
export async function addUserToGroup(
  organizationId: number,
  groupId: number,
  userId: number
): Promise<{ message: string }> {
  try {
    const res = await api.post(
      `/organizations/${organizationId}/groups/${groupId}/users`,
      null,
      { params: { userId } }
    );
    return res.data;
  } catch (error) {
    console.error(`Error adding user ${userId} to group ${groupId}:`, error);
    throw error;
  }
}

/**
 * Remove a user from a group
 * @param organizationId - The ID of the organization
 * @param groupId - The ID of the group
 * @param userId - The ID of the user to remove
 * @returns Promise with success message
 */
export async function removeUserFromGroup(
  organizationId: number,
  groupId: number,
  userId: number
): Promise<{ message: string }> {
  try {
    const res = await api.delete(
      `/organizations/${organizationId}/groups/${groupId}/users/${userId}`
    );
    return res.data;
  } catch (error) {
    console.error(`Error removing user ${userId} from group ${groupId}:`, error);
    throw error;
  }
}