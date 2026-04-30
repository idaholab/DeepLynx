// src/app/lib/client_service/saved_search_services.client.ts
"use client";

import {
  SavedSearchFilterRequest
} from "@/app/(home)/types/requestDTOs";
import { SavedSearchesResponseDto } from "@/app/(home)/types/responseDTOs";
import api from "./api";

/**
 * Fetch the current user's saved searches.
 * Accepts optional filters to narrow results.
 *
 * POST /saved-searches/search
 * Body: FilterSavedQueryRequestDto (all fields optional)
 */
export async function getSavedSearches(
  filters?: SavedSearchFilterRequest
): Promise<SavedSearchesResponseDto[]> {
  try {
    const res = await api.post<SavedSearchesResponseDto[]>(
      "saved-searches/search",
      filters ?? {}
    );
    return res.data;
  } catch (error) {
    console.error("Error fetching saved searches:", error);
    throw error;
  }
}

/**
 * Save a new search for the current user.
 *
 * POST /saved-searches?textSearch=&alias=
 * Body: CustomQueryRequestDto[]
 */
export async function saveSearch(
  dto: SavedSearchesResponseDto
): Promise<boolean> {
  try {
    const params = new URLSearchParams();
    if (dto.query.textSearch) params.set("textSearch", dto.query.textSearch);
    if (dto.name)      params.set("alias", dto.name);

    const res = await api.post<boolean>(
      `/saved-searches?${params.toString()}`,
      dto.query.filter
    );
    return res.data;
  } catch (error) {
    console.error("Error saving search:", error);
    throw error;
  }
}

/**
 * Delete a saved search by ID for the current user.
 *
 * DELETE /saved-searches/:id
 */
export async function deleteSavedSearch(id: string): Promise<boolean> {
  try {
    const res = await api.delete<boolean>(`/saved-searches/${id}`);
    return res.data;
  } catch (error) {
    console.error("Error deleting saved search:", error);
    throw error;
  }
}