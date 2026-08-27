import api from "./api";
import type { PaginatedResponse } from "@/app/(home)/types/responseDTOs";

export async function getUserApiKeys(): Promise<string[]> {
  try {
    const res = await api.get<PaginatedResponse<string>>("/oauth/keys", {
      params: { pageSize: -1 },
    });

    return res.data.items;
  } catch (err) {
    console.error("Error fetching API keys:", err);
    throw err;
  }
}