using deeplynx.models;

namespace deeplynx.interfaces;

public interface ISavedSearchBusiness
{
    Task<bool> SaveSearch(long userId, string alias, string textSearch, CustomQueryDtos.CustomQueryRequestDto[] filters,
        bool favorite);

    Task<List<CustomQueryDtos.CustomQueryResponseDto>> GetSavedSearches(long userId, CustomQueryDtos.FilterSavedQueryRequestDto? searchFilters);
}