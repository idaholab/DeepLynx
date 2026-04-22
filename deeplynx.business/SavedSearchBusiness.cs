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

    /// <summary>
    ///     Filter record request
    /// </summary>
    /// <param name="context">The database context to be used for filter operations.</param>
    public SavedSearchBusiness(DeeplynxContext context)
    {
        _context = context;
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
            textSearch = textSearch,
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
    public async Task<List<CustomQueryDtos.CustomQueryResponseDto>> GetSavedSearches(long userId, CustomQueryDtos.FilterSavedQueryRequestDto? searchFilters)
    {
        var query = _context.SavedSearches
            .Where(s => s.UserId == userId);

        if (searchFilters != null)
        {
            if (searchFilters.Name != null)
                query = query.Where(s => s.Name == searchFilters.Name);

            if (searchFilters.TextSearch != null)
            {
                var jsonFilter = JsonSerializer.Serialize(new { textSearch = searchFilters.TextSearch });
                query = query.Where(s => EF.Functions.JsonContains(s.Search, jsonFilter));
            }

            if (searchFilters.LastUpdatedBefore != null)
                query = query.Where(s => s.LastUpdatedAt <= searchFilters.LastUpdatedBefore);

            if (searchFilters.LastUpdatedAfter != null)
                query = query.Where(s => s.LastUpdatedAt >= searchFilters.LastUpdatedAfter);
        }

        var savedSearches = await query.ToListAsync();

        return savedSearches
            .Select(s => JsonSerializer.Deserialize<CustomQueryDtos.CustomQueryResponseDto>(s.Search))
            .Where(s => s != null)
            .ToList()!;
    }
}