using deeplynx.models;

namespace deeplynx.interfaces;

public interface ISavedSearchBusiness
{
    Task<bool> SaveSearch(
        long userId, string alias, string textSearch, CustomQueryDtos.CustomQueryRequestDto[] filters);

    Task<PaginatedResponse<SavedSearchResponseDto>> GetSavedSearches(long userId, SavedSearchRequestDtos.FilterSavedQueryRequestDto? searchFilters = null);

    Task<IEnumerable<HistoricalRecordResponseDto>> ExecuteSavedSearch(
        long savedSearchId, long currentUserId, long organizationId, long[] projectIds,
        bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false);

    Task<bool> DeleteSavedSearch(long currentUserId, long savedSearchId);
}