'use client';

import { PaginatedEventsResponseDto, EventResponseDto } from "@/app/(home)/types/responseDTOs";
import api from "./api";

export type EventFilterParams = {
    pageNumber?: number;
    pageSize?: number;
    projectIds?: number[];
    organizationId?: number;
    projectName?: string;
    lastUpdatedBy?: number;
    operation?: string;
    entityType?: string;
    entityName?: string;
    dataSourceName?: string;
    startDate?: string;
    endDate?: string;
};

export const getAllEventsPaginated = async (
    organizationId: number,
    projectId: number,
    params?: EventFilterParams
): Promise<PaginatedEventsResponseDto> => {
    try {
        const res = await api.get(`/organizations/${organizationId}/projects/${projectId}/events/query`, {
            params: {
                pageNumber: params?.pageNumber || 1,
                pageSize: params?.pageSize || 10,
                ...params,
            },
        });
        return res.data;
    } catch (error) {
        console.error("Error getting paginated events:", error);
        throw error;
    }
};

export const getAllEventsByUserPaginated = async (
    organizationId: number,
    projectId: number,
    params?: EventFilterParams
): Promise<PaginatedEventsResponseDto> => {
    try {
        const res = await api.get(`/organizations/${organizationId}/projects/${projectId}/events/by-user`, {
            params: {
                pageNumber: params?.pageNumber || 1,
                pageSize: params?.pageSize || 10,
                ...params,
            },
        });
        return res.data;
    } catch (error) {
        console.error("Error getting user events paginated:", error);
        throw error;
    }
};

export const queryAuthorizedEvents = async (
  organizationId: number,
  projectIds: number[],
  params?: EventFilterParams
): Promise<PaginatedEventsResponseDto> => {
  try {
    const projectIdsQuery = projectIds.map(id => `projectIds=${id}`).join('&');
    
    const res = await api.get<PaginatedEventsResponseDto>(
      `/events/QueryAuthorizedEvents/${organizationId}?${projectIdsQuery}`,
      {
        params: {
          pageNumber: params?.pageNumber,
          pageSize: params?.pageSize,
          lastUpdatedBy: params?.lastUpdatedBy,
          projectName: params?.projectName,
          operation: params?.operation,
          entityType: params?.entityType,
          entityName: params?.entityName,
          dataSourceName: params?.dataSourceName,
          startDate: params?.startDate,
          endDate: params?.endDate,
        }
      }
    );
    
    return res.data;
  } catch (error) {
    console.error("Error getting authorized events:", error);
    throw error;
  }
};

export const getAllEvents = async (
    organizationId: number,
    projectId: number,
    params?: Omit<EventFilterParams, 'pageNumber' | 'pageSize'>
): Promise<EventResponseDto[]> => {
    try {
        const res = await api.get(`/organizations/${organizationId}/projects/${projectId}/events`, {
            params,
        });
        return res.data;
    } catch (error) {
        console.error("Error getting all events:", error);
        throw error;
    }
};