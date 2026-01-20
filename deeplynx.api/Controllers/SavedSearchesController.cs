using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace deeplynx.api.Controllers;

/// <summary>
///     Controller for managing classes.
/// </summary>
/// <remarks>
///     This controller provides endpoints to create, update, delete, and retrieve class information.
/// </remarks>
[ApiController]
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
    ///     Save Searches
    /// </summary>
    /// <param name="filterArray">Array of QueryComponent dtos</param>
    /// <param name="textSearch">Full text search phrase</param>
    /// <param name="alias">Name for saved search</param>
    /// <param name="favorite">Boolean for if favorite search or not</param>
    /// <returns>True if successfully saved</returns>
    [HttpPost(Name = "api_save_search")]
    public async Task<ActionResult<bool>> SaveSearch(
        [FromQuery] string? textSearch, [FromQuery] string? alias, [FromQuery] bool favorite,
        [FromBody] CustomQueryDtos.CustomQueryRequestDto[] filterArray)
    {
        try
        {
            // get user ID from the middleware context
            var currentUserId = UserContextStorage.UserId;
            var result = await _savedSearchBusiness.SaveSearch(currentUserId, alias, textSearch, filterArray, favorite);
            return Ok(result);
        }
        catch (Exception exc)
        {
            var message = $"An unexpected error occurred while searching for records.: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Get Saved Searches
    /// </summary>
    /// <returns>A list of saved searches belonging to the user.</returns>
    [HttpGet(Name = "api_query_get_saved_searches")]
    public async Task<ActionResult<IEnumerable<TagResponseDto>>> GetSavedSearches()
    {
        try
        {
            // get user ID from the middleware context
            var currentUserId = UserContextStorage.UserId;
            var savedSearches = await _savedSearchBusiness.GetSavedSearches(currentUserId);
            return Ok(savedSearches);
        }
        catch (Exception exception)
        {
            var message = $"An error occurred while listing all saved searches: {exception}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }
}