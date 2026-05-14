// src/app/lib/user_services.client.ts
"use client";

import {
  UserAdminInfoDto,
  UserResponseDto,
  UserActivityCountsDto
} from "@/app/(home)/types/responseDTOs";
import api from "./api";

/** ---- Browser calls (with session cookies) ---- */

export async function getAllUsers(organizationId?: number | string, projectId?: number | string) {
  try {
    const params: Record<string, string | number | boolean> = {};

    if (organizationId !== undefined) {
      params.organizationId = organizationId;
    }

    if (projectId !== undefined) {
      params.projectId = projectId;
    }

    const res = await api.get(`/users`, { params });
    return res.data;
  } catch (error) {
    console.error("API call failed error getting all users:", error);
    throw error;
  }
}

/**
 * Get the current authenticated user.
 *
 * If organizationId and/or projectId are provided, the backend will populate
 * isOrgAdmin / isProjectAdmin booleans for that org/project context.
 */
export async function getCurrentUser(
  organizationId?: number,
  projectId?: number
): Promise<UserAdminInfoDto> {
  try {
    const res = await api.get<UserAdminInfoDto>("/users/current", {
      params: {
        // Only send if defined, to match optional query params
        ...(organizationId !== undefined && { organizationId }),
        ...(projectId !== undefined && { projectId }),
      },
    });

    return res.data;
  } catch (error) {
    console.error("API call failed getting current user:", error);
    throw error;
  }
}


// user_services.client.ts (wherever your service is located)

export async function getLocalDevUser() {
  try {
    const res = await api.get(`/users/superuser`);
    return res.data;
  } catch (error) {
    console.error("API call failed getting local dev user:", error);
    throw error;
  }
}

export async function getDataOverview(userId: string) {
  try {
    const res = await api.get(`/users/${userId}/overview`);
    return res.data;
  } catch (error) {
    console.error("API call failed:", error);
    throw error;
  }
}

export async function updateUser(
  userId: number,
  data: {
    name?: string | null;
    username?: string | null;
    isArchived?: boolean | null;
    projectId?: number | null;
    isActive?: boolean | null;
  }
): Promise<UserResponseDto> {
  try {
    const res = await api.put<UserResponseDto>(`/users/${userId}`, data);
    return res.data;
  } catch (error) {
    console.error("API call failed:", error);
    throw error;
  }
}

export async function setSysAdmin(
  userId: number,
  isAdmin: boolean
): Promise<{ message: string }> {
  try {
    const res = await api.patch(`/users/${userId}/admin`, null, {
      params: { isAdmin }
    });
    return res.data;
  } catch (error) {
    console.error("API call failed setting sys admin:", error);
    throw error;
  }
}

export async function archiveUser(
  userId: number,
  archive: boolean = true,
): Promise<{ message: string }> {
  try {
    const res = await api.patch(`/users/${userId}`, null, {
      params: { archive },
    })
    return res.data;
  } catch (error) {
    console.error("API call failed archiving user:", error);
    throw error;
  }
}

export async function getActiveUserCounts(
  organizationId?: number | string,
  projectId?: number | string
): Promise<UserActivityCountsDto> {
  try {
    const params: Record<string, string | number | boolean> = {};

    if (organizationId !== undefined) {
      params.organizationId = organizationId;
    }

    if (projectId !== undefined) {
      params.projectId = projectId;
    }

    const res = await api.get<UserActivityCountsDto>(`/users/active-counts`, {
      params,
    });
    return res.data;
  } catch (error) {
    console.error("API call failed getting active user counts:", error);
    throw error;
  }
}