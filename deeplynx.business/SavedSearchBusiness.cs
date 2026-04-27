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
    private readonly QueryBusiness _queryBusiness;

    /// <summary>
    ///     Filter record request
    /// </summary>
    /// <param name="context">The database context to be used for filter operations.</param>
    /// <param name="queryBusiness">The business class needed to execute the saved search.</param>
    public SavedSearchBusiness(DeeplynxContext context, QueryBusiness queryBusiness)
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
    /// <returns>List of saved searches for the user</returns>
    public async Task<List<CustomQueryDtos.CustomQueryResponseDto>> GetSavedSearches(long userId)
    {
        var savedSearches = await _context.SavedSearches
            .Where(s => s.UserId == userId)
            .ToListAsync();

        var result = new List<CustomQueryDtos.CustomQueryResponseDto>();

        foreach (var search in savedSearches)
        {
            // Deserialize the JSON string back to the original structure
            var searchData = JsonSerializer.Deserialize<CustomQueryDtos.CustomQueryResponseDto>(search.Search);

            Console.WriteLine($"Filters count: {searchData?.Filter?.Length ?? 0}");

            result.Add(new CustomQueryDtos.CustomQueryResponseDto
            {
                textSearch = searchData?.textSearch,
                Filter = searchData?.Filter
            });
        }

        return result;
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
    public async Task<IEnumerable<HistoricalRecordResponseDto>> ExecuteSavedSearch(
        long savedSearchId, long currentUserId, long organizationId, long[] projectIds,
        bool isSysAdmin = false, bool isOrgAdmin = false, bool isProjectAdmin = false)
    {
        var savedSearchResult = await _context.SavedSearches
            .FirstOrDefaultAsync(s => s.Id == savedSearchId && s.UserId == currentUserId);

        if (savedSearchResult == null)
            throw new KeyNotFoundException("Saved Search does not exist");

        var userQuery = savedSearchResult.Search;

        var queryResult = await _queryBusiness.Search(
            currentUserId, userQuery, organizationId, projectIds, isSysAdmin, isOrgAdmin, isProjectAdmin);

        return queryResult;
    }
}