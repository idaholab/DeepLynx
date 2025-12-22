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
    /// <param name="favorite">Boolean for if favorite</param>
    /// <param name="alias">Name for saved search</param>
    /// <returns>True if successfully saved</returns>
    public async Task<bool> SaveSearch(long userId, string alias, string textSearch,
        CustomQueryDtos.CustomQueryRequestDto[] filters, bool favorite = false)
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
            IsFavorite = favorite,
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
}