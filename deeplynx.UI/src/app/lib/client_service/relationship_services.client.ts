// src/app/lib/relationship_services.client.ts

import { CreateRelationshipRequestDto, UpdateRelationshipRequestDto } from "@/app/(home)/types/requestDTOs";
import { RelationshipResponseDto } from "@/app/(home)/types/responseDTOs";
import api from "./api";

/**
 * Get all relationships for a project within an organization
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param hideArchived - Flag to hide archived relationships (default: true)
 * @returns Promise with array of RelationshipResponseDto
 */
export const getAllRelationships = async (
  organizationId: number,
  projectId: number,
  hideArchived: boolean = true
): Promise<RelationshipResponseDto[]> => {
  try {
    const { data } = await api.get(
      `/organizations/${organizationId}/projects/${projectId}/relationships`,
      { params: { hideArchived } }
    );
    return data;
  } catch (error) {
    console.error("Error getting all relationships:", error);
    throw error;
  }
};

/**
 * Get a specific relationship by ID within an organization and project
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param relationshipId - The ID of the relationship
 * @param hideArchived - Flag to hide archived relationships (default: true)
 * @returns Promise with RelationshipResponseDto
 */
export const getRelationship = async (
  organizationId: number,
  projectId: number,
  relationshipId: number,
  hideArchived: boolean = true
): Promise<RelationshipResponseDto> => {
  try {
    const { data } = await api.get(
      `/organizations/${organizationId}/projects/${projectId}/relationships/${relationshipId}`,
      { params: { hideArchived } }
    );
    return data;
  } catch (error) {
    console.error(`Error getting relationship ${relationshipId}:`, error);
    throw error;
  }
};

/**
 * Create a new relationship within an organization and project
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param dto - The relationship creation request DTO
 * @returns Promise with RelationshipResponseDto
 */
export const createRelationship = async (
  organizationId: number,
  projectId: number,
  dto: CreateRelationshipRequestDto
): Promise<RelationshipResponseDto> => {
  try {
    const { data } = await api.post(
      `/organizations/${organizationId}/projects/${projectId}/relationships`,
      dto
    );
    return data;
  } catch (error) {
    console.error("Error creating relationship:", error);
    throw error;
  }
};

/**
 * Bulk create relationships within an organization and project
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param relationships - Array of relationship creation request DTOs
 * @returns Promise with array of RelationshipResponseDto
 */
export const bulkCreateRelationships = async (
  organizationId: number,
  projectId: number,
  relationships: CreateRelationshipRequestDto[]
): Promise<RelationshipResponseDto[]> => {
  try {
    const { data } = await api.post(
      `/organizations/${organizationId}/projects/${projectId}/relationships/bulk`,
      relationships
    );
    return data;
  } catch (error) {
    console.error("Error bulk creating relationships:", error);
    throw error;
  }
};

/**
 * Update a relationship within an organization and project
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param relationshipId - The ID of the relationship to update
 * @param dto - The relationship update request DTO
 * @returns Promise with RelationshipResponseDto
 */
export const updateRelationship = async (
  organizationId: number,
  projectId: number,
  relationshipId: number,
  dto: UpdateRelationshipRequestDto
): Promise<RelationshipResponseDto> => {
  try {
    const { data } = await api.put(
      `/organizations/${organizationId}/projects/${projectId}/relationships/${relationshipId}`,
      dto
    );
    return data;
  } catch (error) {
    console.error(`Error updating relationship ${relationshipId}:`, error);
    throw error;
  }
};

/**
 * Delete a relationship within an organization and project
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param relationshipId - The ID of the relationship to delete
 * @returns Promise with success message
 */
export const deleteRelationship = async (
  organizationId: number,
  projectId: number,
  relationshipId: number
): Promise<{ message: string }> => {
  try {
    const { data } = await api.delete(
      `/organizations/${organizationId}/projects/${projectId}/relationships/${relationshipId}`
    );
    return data;
  } catch (error) {
    console.error(`Error deleting relationship ${relationshipId}:`, error);
    throw error;
  }
};

/**
 * Archive or unarchive a relationship within an organization and project
 * @param organizationId - The ID of the organization
 * @param projectId - The ID of the project
 * @param relationshipId - The ID of the relationship to archive/unarchive
 * @param archive - True to archive, false to unarchive
 * @returns Promise with success message
 */
export const archiveRelationship = async (
  organizationId: number,
  projectId: number,
  relationshipId: number,
  archive: boolean
): Promise<{ message: string }> => {
  try {
    const { data } = await api.patch(
      `/organizations/${organizationId}/projects/${projectId}/relationships/${relationshipId}`,
      null,
      { params: { archive } }
    );
    return data;
  } catch (error) {
    console.error(`Error ${archive ? 'archiving' : 'unarchiving'} relationship ${relationshipId}:`, error);
    throw error;
  }
};

/**
 * Create a new relationship at organization level (no project)
 * @param organizationId - The ID of the organization
 * @param dto - The relationship creation request DTO
 * @returns Promise with RelationshipResponseDto
 */
export const createRelationshipOrg = async (
  organizationId: number,
  dto: CreateRelationshipRequestDto
): Promise<RelationshipResponseDto> => {
  try {
    const { data } = await api.post(
      `/organizations/${organizationId}/relationships`,
      dto
    );
    return data;
  } catch (error) {
    console.error("Error creating relationship for organization:", error);
    throw error;
  }
};