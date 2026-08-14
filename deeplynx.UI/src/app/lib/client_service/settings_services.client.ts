import { CreateTokenDto } from "@/app/(home)/types/requestDTOs";
import { PaginatedResponse } from "@/app/(home)/types/responseDTOs";
import api from "./api";


/**
 * Create a JWT token
 * @param tokenDto - Token creation request with API key, secret, and optional expiration
 * @returns Promise with JWT token string
 */
export async function createToken(tokenDto: CreateTokenDto): Promise<string> {
  try {
    const res = await api.post(
      `/oauth/tokens`,
      tokenDto,
      { headers: { "Content-Type": "application/json" } }
    );
    return res.data;
  } catch (error) {
    console.error("Error creating token:", error);
    throw error;
  }
}

/**
 * Create an API key and secret
 * @param clientId - Optional OAuth client ID to associate with the API key
 * @returns Promise with API key and secret (secret only returned once)
 */
export async function createApiKey(clientId?: string): Promise<{ apiKey: string; apiSecret: string }> {
  try {
    const res = await api.post(
      `/oauth/keys`,
      null,
      {
        params: { clientId },
        headers: { "Content-Type": "application/json" }
      }
    );
    return res.data;
  } catch (error) {
    console.error("Error creating API key:", error);
    throw error;
  }
}

/**
 * Delete an API key
 * @param key - API key to be deleted
 * @returns Promise with success message
 */
export async function deleteApiKey(key: string): Promise<{ message: string }> {
  try {
    const res = await api.delete(
      `/oauth/keys/${key}`,
      { headers: { "Content-Type": "application/json" } }
    );
    return res.data;
  } catch (error) {
    console.error(`Error deleting API key ${key}:`, error);
    throw error;
  }
}

/**
 * Get all API keys associated with the current user
 * @returns Promise with array of API key strings (secrets are never returned)
 */
export async function getAllKeysByUser(): Promise<string[]> {
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

/**
 * Revoke all active tokens for the current user
 * @returns Promise with message and count of revoked tokens
 */
export async function revokeAllUserTokens(): Promise<{ message: string; revokedCount: number }> {
  try {
    const res = await api.delete(
      `/oauth/tokens/revoke`,
      { headers: { "Content-Type": "application/json" } }
    );
    return res.data;
  } catch (error) {
    console.error("Error revoking all user tokens:", error);
    throw error;
  }
}