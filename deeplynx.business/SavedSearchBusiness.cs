using System.Text.Json;
using deeplynx.datalayer.Models;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.EntityFrameworkCore;

namespace deeplynx.business;

/// <summary>
///     Filter record request
/// </summary>
public class SavedSearchBusiness : ISavedSearchBusiness
{
    private readonly DeeplynxContext _context;
    private readonly IQueryBusiness _queryBusiness;

    /// <summary>
    ///     Filter record request
    /// </summary>
    /// <param name="context">The database context to be used for filter operations.</param>
    /// <param name="queryBusiness">The business class needed to execute the saved search.</param>
    public SavedSearchBusiness(DeeplynxContext context, IQueryBusiness queryBusiness)
    {
        _context = context;
        _queryBusiness = queryBusiness;
    }

    /// <summary>
    ///     Save search for user
    /// </summary>
    /// <param name="userId">The ID of the user</param>
    /// <param name="textSearch">Full text search string</param>
    /// <param name="filters">Query filter object array</param>
    /// <param name="alias">Name for saved search</param>
    /// <returns>True if successfully saved</returns>
    public async Task<bool> SaveSearch(long userId, string alias, string textSearch,
        CustomQueryDtos.CustomQueryRequestDto[] filters)
    {
        if (filters == null)
            throw new ArgumentNullException(nameof(filters), "Query filters cannot be null");
        // Create an object that wraps both the textSearch and filters array
        var searchData = new CustomQueryDtos.CustomQueryResponseDto
        {
            TextSearch = textSearch,
            Filter = filters
        };

        var queryBuilt = JsonSerializer.Serialize(searchData);
        var savedSearch = new SavedSearch
        {
            Name = alias,
            Search = queryBuilt,
            UserId = userId
        };
        _context.SavedSearches.Add(savedSearch);
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    ///     Get saved searches
    /// </summary>
    /// <param name="userId">The ID of the user</param>
    /// <param name="searchFilters">Optional filters to query for specific saved searches</param>
    /// <returns>List of saved searches for the user</returns>
    public async Task<PaginatedResponse<SavedSearchResponseDto>> GetSavedSearches(long userId, SavedSearchRequestDtos.FilterSavedQueryRequestDto? searchFilters = null)
    {
        var query = _context.SavedSearches
            .Where(s => s.UserId == userId);

        if (searchFilters != null)
        {
            if (!string.IsNullOrWhiteSpace(searchFilters.Name))
                query = query.Where(s => s.Name.ToLower().Contains(searchFilters.Name.ToLower()));

            if (searchFilters.LastUpdatedBefore != null)
                query = query.Where(s => s.LastUpdatedAt <= searchFilters.LastUpdatedBefore);

            if (searchFilters.LastUpdatedAfter != null)
                query = query.Where(s => s.LastUpdatedAt >= searchFilters.LastUpdatedAfter);
        }

        var pageNumber = searchFilters?.PageNumber ?? 1;
        var pageSize = searchFilters?.GetValidatedPageSize() ?? 25;

        var totalCount = await query.CountAsync();

        var savedSearches = await query
            .OrderByDescending(s => s.LastUpdatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var results = savedSearches
            .Select(s =>
            {
                var customQuery = JsonSerializer.Deserialize<CustomQueryDtos.CustomQueryResponseDto>(
                    s.Search, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (customQuery == null) return null;
                return new SavedSearchResponseDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    LastUpdatedAt = s.LastUpdatedAt,
                    Query = customQuery
                };
            })
            .Where(s => s != null)
            .Where(s => string.IsNullOrWhiteSpace(searchFilters?.TextSearch) ||
                (s!.Query.TextSearch != null &&
                 s.Query.TextSearch.Contains(searchFilters.TextSearch, StringComparison.OrdinalIgnoreCase)))
            .ToList()!;

        return new PaginatedResponse<SavedSearchResponseDto>
        {
            Items = results!,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize,
        };
    }

    /// <summary>
    ///     Get a saved search by ID
    /// </summary>
    /// <param name="currentUserId">The ID of the user</param>
    /// <param name="savedSearchId">The ID of the saved search to be fetched</param>
    /// <returns>The saved search with the matching user and ID</returns>
    public async Task<SavedSearchResponseDto> GetSavedSearchById(long currentUserId, long savedSearchId)
    {
        var savedSearch = await _context.SavedSearches.FirstOrDefaultAsync(s => s.Id == savedSearchId && s.UserId == currentUserId);

        if (savedSearch == null)
            throw new KeyNotFoundException("Saved Search not found");

        var customQuery = JsonSerializer.Deserialize<CustomQueryDtos.CustomQueryResponseDto>(
            savedSearch.Search, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException($"Saved search '{savedSearch.Id}' contains invalid or null query data.");

        return new SavedSearchResponseDto
        {
            Id = savedSearch.Id,
            Name = savedSearch.Name,
            LastUpdatedAt = savedSearch.LastUpdatedAt,
            Query = customQuery
        };
    }

    /// <summary>
    ///     Execute a saved search
    /// </summary>
    /// <param name="savedSearchId">The ID of the saved search that will be executed</param>
    /// <param name="currentUserId">The ID of the user</param>
    /// <param name="organizationId">The ID of organization</param>
    /// <param name="projectIds">List of project ID's that the query will take place in</param>
    /// <param name="isSysAdmin">Boolean value determining if the user is a System admin</param>
    /// <param name="isOrgAdmin">Boolean value determining if the user is a organization admin</param>
    /// <param name="isProjectAdmin">Boolean value determining if the user is a admin for all the project ID's referenced</param>
    /// <returns>List of records retrieved by the query</returns>
    public async Task<IEnumerable<QueryRecordViewResponseDto>> ExecuteSavedSearch(
        long savedSearchId, long currentUserId, long organizationId, long[] projectIds,
        bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false)
    {
        var savedSearchResult = await _context.SavedSearches
            .FirstOrDefaultAsync(s => s.Id == savedSearchId && s.UserId == currentUserId);

        if (savedSearchResult == null)
            throw new KeyNotFoundException("Saved Search does not exist");

        var savedSearch = JsonSerializer.Deserialize<SavedSearchRequestDtos.SavedSearchRequestDto>(
            savedSearchResult.Search,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (savedSearch?.Filter == null)
            throw new ArgumentException("Saved search contains an invalid or empty query.");

        var queryResult = await _queryBusiness.QueryBuilder(
            currentUserId, savedSearch.Filter, organizationId, projectIds,
            savedSearch.TextSearch, isSysAdmin, isOrgAdmin, isProjectAdmin);

        return queryResult;
    }

    /// <summary>
    ///     Delete a saved Search
    /// </summary>
    /// <param name="currentUserId">The ID of the user making the request</param>
    /// <param name="savedSearchId">The ID of the saved search that will be executed</param>
    public async Task<bool> DeleteSavedSearch(long currentUserId, long savedSearchId)
    {
        var savedSearch = await _context.SavedSearches.FirstOrDefaultAsync(s => s.Id == savedSearchId && s.UserId == currentUserId);

        if (savedSearch == null)
            throw new KeyNotFoundException("Saved search not found");

        _context.SavedSearches.Remove(savedSearch);
        await _context.SaveChangesAsync();

        return true;
    }
}