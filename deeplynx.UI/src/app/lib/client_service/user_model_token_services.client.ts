"use client";

import type {
  CreateUserModelTokenRequestDto,
  UpdateUserModelTokenRequestDto,
} from "@/app/(home)/types/requestDTOs";
import type { UserModelTokenResponseDto } from "@/app/(home)/types/responseDTOs";
import api from "./api";

export async function getUserModelTokens(
  aiModelConfigId?: number,
): Promise<UserModelTokenResponseDto[]> {
  const response = await api.get<UserModelTokenResponseDto[]>("/model-tokens", {
    params: aiModelConfigId ? { aiModelConfigId } : undefined,
  });

  return response.data;
}

export async function createUserModelToken(
  createUserModelTokenRequest: CreateUserModelTokenRequestDto,
): Promise<UserModelTokenResponseDto> {
  const response = await api.post<UserModelTokenResponseDto>(
    "/model-tokens",
    createUserModelTokenRequest,
  );

  return response.data;
}

export async function updateUserModelToken(
  userModelTokenId: number,
  updateUserModelTokenRequest: UpdateUserModelTokenRequestDto,
): Promise<UserModelTokenResponseDto> {
  const response = await api.put<UserModelTokenResponseDto>(
    `/model-tokens/${userModelTokenId}`,
    updateUserModelTokenRequest,
  );

  return response.data;
}
