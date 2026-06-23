// src/app/lib/user_services.server.ts
import "server-only";
import {
  UserAdminInfoDto,
  UserResponseDto,
} from "../../(home)/types/responseDTOs";
import { apiFetch, asJson } from "./api.server";


/** ---- Server-safe calls (no browser cookies; safe in prerender/SSR) ---- */

export async function getAllUsersServer(projectId?: number, organizationId?: number): Promise<UserResponseDto[]> {
  const params: Record<string, string> = {};
  if (projectId !== undefined) {
    params.projectId = String(projectId);
  }
  if (organizationId !== undefined) {
    params.organizationId = String(organizationId);
  }
  const qs = new URLSearchParams(params);
  const res = await apiFetch(`users?${qs.toString()}`);
  return asJson<UserResponseDto[]>(res);
}

export async function getCurrentUserServer(
  organizationId?: number,
  projectId?: number,
): Promise<UserAdminInfoDto> {
  const params: Record<string, string> = {};
  if (organizationId !== undefined) {
    params.organizationId = String(organizationId);
  }
  if (projectId !== undefined) {
    params.projectId = String(projectId);
  }
  const query = new URLSearchParams(params).toString();
  const suffix = query ? `?${query}` : "";
  const res = await apiFetch(`users/current${suffix}`);
  return asJson<UserAdminInfoDto>(res);
}

export async function getLocalDevUserServer(): Promise<UserResponseDto> {
  const res = await apiFetch(`users/superuser`);
  return asJson<UserResponseDto>(res);
}

export async function updateUserServer<T = UserResponseDto>(
  userId: number,
  name?: string
): Promise<T> {
  const res = await apiFetch(`users/${userId}`, {
    method: 'PUT',
    body: JSON.stringify({ name }),
  });
  return asJson<T>(res);
}

export async function deleteUserServer<T = void>(userId: number): Promise<T> {
  const res = await apiFetch(`users/${userId}`, {
    method: 'DELETE',
  });
  return asJson<T>(res);
}
