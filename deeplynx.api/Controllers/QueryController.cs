using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using deeplynx.helpers;
using deeplynx.helpers.Context;

namespace deeplynx.api.Controllers;

/// <summary>
///     Controller for managing classes.
/// </summary>
/// <remarks>
///     This controller provides endpoints to create, update, delete, and retrieve class information.
/// </remarks>
[ApiController]
[Route("organizations/{organizationId:long}/query")]
[Authorize]
public class QueryController : ControllerBase
{
    private readonly ILogger<QueryController> _logger;
    private readonly IQueryBusiness _queryBusiness;

    /// <summary>
    /// </summary>
    /// <param name="queryBusiness">The business logic interface for handling querying operations.</param>
    /// <param name="logger">Error/Info logging interface for database log table.</param>
    public QueryController(IQueryBusiness queryBusiness, ILogger<QueryController> logger)
    {
        _queryBusiness = queryBusiness;
        _logger = logger;
    }

    /// <summary>
    ///     Full Text Search for Records
    /// </summary>
    /// <param name="organizationId">The organization to which the records/projects belong</param>
    /// <param name="userQuery">String phrase entered by user</param>
    /// <param name="projectIds">Project IDs in the organization to search across</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived records from the result</param>
    /// <returns>List of record response DTOs from the query_record view</returns>
    [HttpGet("records", Name = "api_filter_records")]
    [Auth("read", "record")]
    public async Task<ActionResult<IEnumerable<QueryRecordViewResponseDto>>> SearchRecords(
        long organizationId, 
        [FromQuery] string userQuery, 
        [FromQuery] long[] projectIds, 
        [FromQuery] bool hideArchived)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var isSysAdmin = UserContextStorage.IsSysAdmin;
            var isOrgAdmin = UserContextStorage.IsOrgAdmin;
            var isProjectAdmin = UserContextStorage.IsProjectAdmin;
            var records = await _queryBusiness.Search(
                currentUserId, userQuery, organizationId, projectIds,
                hideArchived, isSysAdmin, isOrgAdmin, isProjectAdmin);
            return Ok(records);
        }
        catch (Exception exc)
        {
            var message = $"An unexpected error occurred while searching for records.: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Build a Query for Records
    /// </summary>
    /// <param name="organizationId">The organization to which the records/projects belong</param>
    /// <param name="filterArray">Array of QueryComponent dtos</param>
    /// <param name="textSearch">Full text search phrase</param>
    /// <param name="projectIds">Project IDs in the organization to search across</param>
    /// <returns>List of record response DTOs from the query_record view</returns>
    [HttpPost("records/advanced", Name = "api_query_builder_records")]
    [Auth("read", "record")]
    public async Task<ActionResult<IEnumerable<QueryRecordViewResponseDto>>> QueryBuilder(
        long organizationId, [FromQuery] string? textSearch, [FromQuery] long[] projectIds,
        [FromBody] CustomQueryDtos.CustomQueryRequestDto[] filterArray)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var isSysAdmin = UserContextStorage.IsSysAdmin;
            var isOrgAdmin = UserContextStorage.IsOrgAdmin;
            var isProjectAdmin = UserContextStorage.IsProjectAdmin;
            var records = await _queryBusiness.QueryBuilder(currentUserId, filterArray, organizationId, projectIds,
                textSearch, isSysAdmin, isOrgAdmin, isProjectAdmin);
            return Ok(records);
        }
        catch (Exception exc)
        {
            var message = $"An unexpected error occurred while searching for records.: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Get Recent Records
    /// </summary>
    /// <param name="organizationId"> Organization Id of projects</param>
    /// <param name="projectIds">Array of project ids</param>
    /// <returns>List of record response DTOs from the query_record view sorted by most recent</returns>
    [HttpGet("recent", Name = "api_get_recent_records")]
    [Auth("read", "record")]
    public async Task<ActionResult<IEnumerable<QueryRecordViewResponseDto>>> GetRecentlyAddedRecords(
        long organizationId, [FromQuery] long[] projectIds)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var isSysAdmin = UserContextStorage.IsSysAdmin;
            var isOrgAdmin = UserContextStorage.IsOrgAdmin;
            var isProjectAdmin = UserContextStorage.IsProjectAdmin;
            var records = await _queryBusiness.GetRecentlyAddedRecords(currentUserId, organizationId, projectIds,
                isSysAdmin, isOrgAdmin, isProjectAdmin);
            return Ok(records);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while listing records: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }


    /// <summary>
    ///     Retrieve All Records for Multiple Projects
    /// </summary>
    /// <param name="organizationId">ID of the organization to which the projects belong</param>
    /// <param name="projects">Array of project ids whose records are to be retrieved</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived records from the result</param>
    /// <returns>List of record response DTOs from the query_record view</returns>
    [HttpGet("multiproject", Name = "api_multiproject_records")]
    [Auth("read", "record")]
    public async Task<ActionResult<IEnumerable<QueryRecordViewResponseDto>>> GetMultiProjectRecords(
        long organizationId,
        [FromQuery] long[] projects,
        [FromQuery] bool hideArchived = true)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var isSysAdmin = UserContextStorage.IsSysAdmin;
            var isOrgAdmin = UserContextStorage.IsOrgAdmin;
            var isProjectAdmin = UserContextStorage.IsProjectAdmin;
            var records = await _queryBusiness.GetMultiProjectRecords(currentUserId, organizationId, projects,
                hideArchived, isSysAdmin, isOrgAdmin, isProjectAdmin);
            return Ok(records);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while listing records: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }
}