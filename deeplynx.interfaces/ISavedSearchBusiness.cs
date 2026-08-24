using deeplynx.models;

namespace deeplynx.interfaces;

public interface ISavedSearchBusiness
{
    Task<bool> SaveSearch(
        long userId, string alias, string textSearch, CustomQueryDtos.CustomQueryRequestDto[] filters);

    Task<PaginatedResponse<SavedSearchResponseDto>> GetSavedSearches(long userId, SavedSearchRequestDtos.FilterSavedQueryRequestDto? searchFilters = null);

    Task<SavedSearchResponseDto> GetSavedSearchById(long currentUserId, long savedSearchId);
    
    [Obsolete("V1-only. Used by deprecated v1 saved search endpoints. Superseded by ExecuteSavedSearchPaginated. " +
              "Remove once v1 saved search endpoints are sunset.", error: false)]
    Task<IEnumerable<QueryRecordViewResponseDto>> ExecuteSavedSearch(
        long savedSearchId, long currentUserId, long organizationId, long[] projectIds,
        bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false);

    Task<PaginatedResponse<QueryRecordViewResponseDto>> ExecuteSavedSearchPaginated(
        long savedSearchId, 
        long currentUserId, 
        long organizationId, 
        long[] projectIds,
        PaginatedRequestDto paginated,
        bool isSysAdmin = false, 
        bool isOrgAdmin = false);

    Task<bool> DeleteSavedSearch(long currentUserId, long savedSearchId);
}