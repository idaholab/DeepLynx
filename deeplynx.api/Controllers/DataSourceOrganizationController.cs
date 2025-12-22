using deeplynx.helpers;
using deeplynx.helpers.Context;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace deeplynx.api.Controllers;

/// <summary>
///     Controller for managing data sources on org level.
/// </summary>
/// <remarks>
///     This controller provides endpoints to create, update, delete, and retrieve data source information.
/// </remarks>
[ApiController]
[Route("organizations/{organizationId:long}/datasources")]
[Authorize]
[Tags("Organization - DataSource")]
public class DataSourceOrganizationController : ControllerBase
{
    private readonly IDataSourceBusiness _dataSourceBusiness;
    private readonly ILogger<DataSourceProjectController> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DataSourceProjectController" /> class
    /// </summary>
    /// <param name="dataSourceBusiness">The business logic interface for handling data source operations.</param>
    /// <param name="logger">Error/Info logging interface for database log table.</param>
    public DataSourceOrganizationController(IDataSourceBusiness dataSourceBusiness,
        ILogger<DataSourceProjectController> logger)
    {
        _dataSourceBusiness = dataSourceBusiness;
        _logger = logger;
    }

    /// <summary>
    ///     Get All Data Sources 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the projectID belongs</param>
    /// <param name="projectIds">(Optional)An array of project IDs within the organization to filter by</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived data sources from the result (Default true)</param>
    /// <returns>A list of data sources for the given project.</returns>
    [HttpGet(Name = "api_get_all_data_sources_organization")]
    [Auth("read", "data_source")]
    public async Task<ActionResult<IEnumerable<DataSourceResponseDto>>> GetAllDataSources(
        long organizationId,
        [FromQuery] long[]? projectIds,
        [FromQuery] bool hideArchived = true)
    {
        try
        {
            var dataSources = await _dataSourceBusiness.GetAllDataSources(organizationId, projectIds, hideArchived);
            return Ok(dataSources);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while listing all data sources: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Get a Data Source 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the projectID belongs</param>
    /// <param name="dataSourceId">The ID whereby to fetch the data source</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived data sources from the result (Default true)</param>
    /// <returns>The data source associated with the given ID</returns>
    [HttpGet("{dataSourceId:long}", Name = "api_get_a_data_source_organization")]
    [Auth("read", "data_source")]
    public async Task<ActionResult<DataSourceResponseDto>> GetDataSource(
        long organizationId,
        long dataSourceId,
        [FromQuery] bool hideArchived = true)
    {
        try
        {
            var dataSource =
                await _dataSourceBusiness.GetDataSource(organizationId, null, dataSourceId, hideArchived);
            return Ok(dataSource);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while retrieving data source {dataSourceId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Get Default Data Source 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the projectID belongs</param>
    /// <returns>The default data source for the project</returns>
    [HttpGet("default", Name = "api_get_default_data_source_for_organization")]
    [Auth("read", "data_source")]
    public async Task<ActionResult<DataSourceResponseDto>> GetDefaultDataSource(long organizationId)
    {
        try
        {
            var dataSource = await _dataSourceBusiness.GetDefaultDataSource(organizationId, null);
            return Ok(dataSource);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while retrieving default data source for org {organizationId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Create a Data Source 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the projectID belongs</param>
    /// <param name="dto">The data transfer object containing data source details</param>
    /// <returns>The created data source</returns>
    [HttpPost(Name = "api_create_a_data_source_for_organization")]
    [Auth("write", "data_source")]
    public async Task<ActionResult<DataSourceResponseDto>> CreateDataSource(
        long organizationId,
        [FromBody] CreateDataSourceRequestDto dto)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var dataSource = await _dataSourceBusiness.CreateDataSource(organizationId, null, currentUserId, dto);
            return Ok(dataSource);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while creating data source: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }


    /// <summary>
    ///     Update a Data Source 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the projectID belongs</param>
    /// <param name="dataSourceId">The ID of the data source to update</param>
    /// <param name="dto">The data transfer object containing updated data source details</param>
    /// <returns>The newly updated data source</returns>
    [HttpPut("{dataSourceId:long}", Name = "api_update_a_data_source_for_organization")]
    [Auth("write", "data_source")]
    public async Task<ActionResult<DataSourceResponseDto>> UpdateDataSource(
        long organizationId,
        long dataSourceId,
        [FromBody] UpdateDataSourceRequestDto dto)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var dataSource =
                await _dataSourceBusiness.UpdateDataSource(organizationId, null, currentUserId, dataSourceId, dto);
            return Ok(dataSource);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while updating data source {dataSourceId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Delete a Data Source 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the projectID belongs</param>
    /// <param name="dataSourceId">The ID of the data source to delete</param>
    /// <returns>A message stating the data source was successfully deleted.</returns>
    [HttpDelete("{dataSourceId:long}", Name = "api_delete_a_data_source_for_organization")]
    [Auth("write", "data_source")]
    public async Task<IActionResult> DeleteDataSource(
        long organizationId,
        long dataSourceId)
    {
        try
        {
            await _dataSourceBusiness.DeleteDataSource(organizationId, null, dataSourceId);
            return Ok(new { message = $"Deleted data source {dataSourceId}" });
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while deleting data source {dataSourceId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Archive or Unarchive a Data Source 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the projectID belongs</param>
    /// <param name="dataSourceId">The ID of the data source to archive or unarchive</param>
    /// <param name="archive">True to archive the data source, false to unarchive it.</param>
    /// <returns>A message stating the data source was successfully archived or unarchived.</returns>
    [HttpPatch("{dataSourceId:long}", Name = "api_archive_data_source_for_organization")]
    [Auth("write", "data_source")]
    public async Task<IActionResult> ArchiveDataSource(
        long organizationId,
        long dataSourceId,
        [FromQuery] bool archive)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            if (archive)
            {
                await _dataSourceBusiness.ArchiveDataSource(organizationId, null, currentUserId, dataSourceId);
                return Ok(new { message = $"Archived data source {dataSourceId}" });
            }

            await _dataSourceBusiness.UnarchiveDataSource(organizationId, null, currentUserId, dataSourceId);
            return Ok(new { message = $"Unarchived data source {dataSourceId}" });
        }
        catch (Exception exc)
        {
            var action = archive ? "archiving" : "unarchiving";
            var message = $"An error occurred while {action} data source {dataSourceId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Set Default Data Source 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the projectID belongs</param>
    /// <param name="dataSourceId">The ID of the data source to set as default</param>
    /// <returns>The updated data source</returns>
    [HttpPatch("{dataSourceId:long}/default", Name = "api_set_default_data_source_for_organization")]
    [Auth("write", "data_source")]
    public async Task<ActionResult<DataSourceResponseDto>> SetDefaultDataSource(
        long organizationId,
        long dataSourceId)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var dataSource =
                await _dataSourceBusiness.SetDefaultDataSource(organizationId, null, currentUserId, dataSourceId);
            return Ok(dataSource);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while setting default data source {dataSourceId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }
}