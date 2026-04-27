using deeplynx.models;

namespace deeplynx.interfaces;

public interface ISavedSearchBusiness
{
    Task<bool> SaveSearch(
        long userId, string alias, string textSearch, CustomQueryDtos.CustomQueryRequestDto[] filters);

    Task<List<CustomQueryDtos.CustomQueryResponseDto>> GetSavedSearches(long userId, CustomQueryDtos.FilterSavedQueryRequestDto? searchFilters = null);

    Task<IEnumerable<HistoricalRecordResponseDto>> ExecuteSavedSearch(
        long savedSearchId, long currentUserId, long organizationId, long[] projectIds,
        bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false);
}