"use client";

import { SavedSearchFilterRequest } from "@/app/(home)/types/requestDTOs";
import {
  SavedSearchesResponseDto,
  PaginatedSavedSearchesResponseDto,
  HistoricalRecordResponseDto,
} from "@/app/(home)/types/responseDTOs";
import { CustomQueryRequestDto } from "@/app/(home)/types/requestDTOs";
import api from "./api";

/**
 * Fetch the current user's saved searches with optional filters.
 * Returns a paginated wrapper around the results.
 *
 * POST /saved-searches/search
 * Body: FilterSavedQueryRequestDto (all fields optional)
 */
export async function getSavedSearches(
  filters?: SavedSearchFilterRequest
): Promise<PaginatedSavedSearchesResponseDto> {
  try {
    const res = await api.post<PaginatedSavedSearchesResponseDto>(
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
 * Fetch a single saved search by ID.
 *
 * GET /saved-searches?savedSearchId={id}
 */
export async function getSavedSearchById(
  savedSearchId: number
): Promise<SavedSearchesResponseDto> {
  try {
    const res = await api.get<SavedSearchesResponseDto>("saved-searches", {
      params: { savedSearchId },
    });
    return res.data;
  } catch (error) {
    console.error("Error fetching saved search by ID:", error);
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
  filterArray: CustomQueryRequestDto[],
  textSearch?: string,
  alias?: string
): Promise<boolean> {
  try {
    const params = new URLSearchParams();
    if (textSearch) params.set("textSearch", textSearch);
    if (alias) params.set("alias", alias);

    const res = await api.post<boolean>(
      `saved-searches?${params.toString()}`,
      filterArray
    );
    return res.data;
  } catch (error) {
    console.error("Error saving search:", error);
    throw error;
  }
}

/**
 * Execute a saved search and return matching records.
 *
 * GET /saved-searches/organizations/{organizationId}?savedSearchId=&projectIds=
 */
export async function executeSavedSearch(
  savedSearchId: number,
  organizationId: number,
  projectIds: number[]
): Promise<HistoricalRecordResponseDto[]> {
  try {
    const res = await api.get<HistoricalRecordResponseDto[]>(
      `saved-searches/organizations/${organizationId}`,
      {
        params: { savedSearchId, projectIds },
        // .NET expects repeated params: projectIds=1&projectIds=2
        // axios default bracket format (projectIds[]=1) would be rejected
        paramsSerializer: (p) => {
          const sp = new URLSearchParams();
          sp.set("savedSearchId", String(p.savedSearchId));
          (p.projectIds as number[]).forEach((id) =>
            sp.append("projectIds", String(id))
          );
          return sp.toString();
        },
      }
    );
    return res.data;
  } catch (error) {
    console.error("Error executing saved search:", error);
    throw error;
  }
}

/**
 * Delete a saved search by ID for the current user.
 *
 * DELETE /saved-searches?savedSearchId={id}
 */
export async function deleteSavedSearch(savedSearchId: number): Promise<boolean> {
  try {
    const res = await api.delete<boolean>("saved-searches", {
      params: { savedSearchId },
    });
    return res.data;
  } catch (error) {
    console.error("Error deleting saved search:", error);
    throw error;
  }
}