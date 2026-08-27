using Asp.Versioning;
using deeplynx.helpers;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;

namespace deeplynx.api.Controllers.V2;

/// <summary>
///     Controller for managing classes.
/// </summary>
/// <remarks>
///     This controller provides endpoints to create, update, delete, and retrieve class information.
/// </remarks>
[ApiController]
[ApiVersion(2)]
[Route("saved-searches")]
[Authorize]
[Tags("Saved Search")]
public class SavedSearchController : ControllerBase
{
    private readonly ILogger<SavedSearchController> _logger;
    private readonly ISavedSearchBusiness _savedSearchBusiness;

    /// <summary>
    /// </summary>
    /// <param name="savedSearchBusiness">The business logic interface for handling querying operations.</param>
    /// <param name="logger">Error/Info logging interface for database log table.</param>
    public SavedSearchController(ISavedSearchBusiness savedSearchBusiness, ILogger<SavedSearchController> logger)
    {
        _savedSearchBusiness = savedSearchBusiness;
        _logger = logger;
    }


    
    /// <summary>
    ///     Save search
    /// </summary>
    /// <param name="filterArray">Array of QueryComponent dtos</param>
    /// <param name="textSearch">Full text search phrase</param>
    /// <param name="alias">Name for saved search</param>
    /// <returns>True if successfully saved</returns>
    [HttpPost(Name = "api_save_search")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<ActionResult<bool>> SaveSearch(
        [FromQuery] string? textSearch, [FromQuery] string? alias,
        [FromBody] CustomQueryDtos.CustomQueryRequestDto[] filterArray)
    {
        var currentUserId = UserContextStorage.UserId;
        var result = await _savedSearchBusiness.SaveSearch(currentUserId, alias, textSearch, filterArray);
        return Ok(result);
    }


    
    /// <summary>
    ///     Get Saved Searches
    /// </summary>
    /// <param name="searchFilters">Optional filters to narrow results of saved searches query</param>
    /// <returns>A list of saved searches belonging to the user.</returns>
    [HttpPost("search", Name = "api_query_get_saved_searches")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<ActionResult<PaginatedResponse<SavedSearchResponseDto>>> GetSavedSearches(
        [FromBody] SavedSearchRequestDtos.FilterSavedQueryRequestDto? searchFilters = null)
    {
        var currentUserId = UserContextStorage.UserId;
        var savedSearches = await _savedSearchBusiness.GetSavedSearches(currentUserId, searchFilters);
        return Ok(savedSearches);
    }


    
    /// <summary>
    ///     Get a saved search by ID
    /// </summary>
    /// <param name="savedSearchId">The ID of the saved search to be fetched</param>
    /// <returns>The saved search with the matching user and ID</returns>
    [HttpGet(Name = "api_query_get_saved_search_by_id")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<ActionResult<SavedSearchResponseDto>> GetSavedSearchById(
        [FromQuery] long savedSearchId
    )
    {
        var currentUserId = UserContextStorage.UserId;
        var savedSearch = await _savedSearchBusiness.GetSavedSearchById(currentUserId, savedSearchId);
        return Ok(savedSearch);
    }


    
    /// <summary>
    ///     Execute a saved search
    /// </summary>
    /// <param name="savedSearchId">The ID of the saved search that will be executed</param>
    /// <param name="organizationId">The ID of organization</param>
    /// <param name="paginatedDto">Pagination details</param>
    /// <param name="projects">List of project ID's that the query will take place in</param>
    /// <returns>List of records retrieved by the query</returns>
    [HttpGet("organizations/{organizationId:long}", Name = "api_query_execute_saved_search")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "record")]
    public async Task<ActionResult<PaginatedResponse<QueryRecordViewResponseDto>>> ExecuteSavedSearch(
        long organizationId, 
        [FromQuery] long[] projects, 
        [FromQuery] long savedSearchId,
        [FromQuery] PaginatedRequestDto? paginatedDto = null)
    {
        paginatedDto ??= new PaginatedRequestDto();
        var currentUserId = UserContextStorage.UserId;
        var isSysAdmin = UserContextStorage.IsSysAdmin;
        var isOrgAdmin = UserContextStorage.IsOrgAdmin;
        var records = await _savedSearchBusiness.ExecuteSavedSearchPaginated(
            savedSearchId, currentUserId, organizationId, projects, paginatedDto, isSysAdmin, isOrgAdmin);
        return Ok(records);
    }


    
    /// <summary>
    ///     Delete a saved Search
    /// </summary>
    /// <param name="savedSearchId">The ID of the saved search that will be deleted</param>
    /// <returns>True if successful</returns>
    [HttpDelete(Name = "api_query_delete_saved_search")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<ActionResult<bool>> DeleteSavedSearch(
        [FromQuery] long savedSearchId)
    {
        var currentUserId = UserContextStorage.UserId;
        var result = await _savedSearchBusiness.DeleteSavedSearch(
            currentUserId, savedSearchId);
        return Ok(result);
    }
}
