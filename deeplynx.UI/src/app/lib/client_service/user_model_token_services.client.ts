"use client";

import type {
  CreateUserModelTokenRequestDto,
  UpdateUserModelTokenRequestDto,
} from "@/app/(home)/types/requestDTOs";
import type { UserModelTokenResponseDto } from "@/app/(home)/types/responseDTOs";
import api from "./api";

function buildUserModelTokenRoute(userId: number): string {
  return `/users/${userId}/model-tokens`;
}

export async function getUserModelTokens(
  userId: number,
  aiModelConfigId?: number,
): Promise<UserModelTokenResponseDto[]> {
  const response = await api.get<UserModelTokenResponseDto[]>(
    buildUserModelTokenRoute(userId),
    { params: aiModelConfigId ? { aiModelConfigId } : undefined },
  );

  return response.data;
}

export async function createUserModelToken(
  userId: number,
  createUserModelTokenRequest: CreateUserModelTokenRequestDto,
): Promise<UserModelTokenResponseDto> {
  const response = await api.post<UserModelTokenResponseDto>(
    buildUserModelTokenRoute(userId),
    createUserModelTokenRequest,
  );

  return response.data;
}

export async function updateUserModelToken(
  userId: number,
  userModelTokenId: number,
  updateUserModelTokenRequest: UpdateUserModelTokenRequestDto,
): Promise<UserModelTokenResponseDto> {
  const response = await api.put<UserModelTokenResponseDto>(
    `${buildUserModelTokenRoute(userId)}/${userModelTokenId}`,
    updateUserModelTokenRequest,
  );

  return response.data;
}
