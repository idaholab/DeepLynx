using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace deeplynx.api.Controllers;

/// <summary>
///     Controller for retrieving summary statistics at a project level
/// </summary>
/// <remarks>
///     This controller provides endpoints to populate the DeepLynx metrics pages for Nexus project admins.
/// </remarks>
[ApiController]
[Route("organizations/{organizationId:long}/projects/{projectId:long}/metrics")]
[Authorize]
[Tags("Project - Metrics")]
public class MetricsProjectController : ControllerBase
{
    private readonly IMetricsBusiness _metricsBusiness;
    private readonly ILogger<MetricsController> _logger;
    
    /// <summary>
    ///     Initializes a new instance of the <see cref="MetricsProjectController" /> class
    /// </summary>
    /// <param name="metricsBusiness">The business logic interface for handling metrics retrievals</param>
    /// <param name="logger">Error/info logging interface for database log table</param>
    public MetricsProjectController(
        IMetricsBusiness metricsBusiness, 
        ILogger<MetricsController> logger)
    {
        _metricsBusiness = metricsBusiness;
        _logger = logger;
    }

    /// <summary>
    ///     Get Bytes Ingested
    /// </summary>
    /// <param name="organizationId">The organization to which the project belongs</param>
    /// <param name="projectId">The project from which to retrieve the summary statistic</param>
    /// <returns>The total number of bytes of file data stored in this project's registered object storages.</returns>
    [HttpGet("storage/size", Name = "api_storage_size_project")]
    [SysAdmin]
    public async Task<IActionResult> GetProjectStorageSize(
        long organizationId, 
        long projectId
        )
    {
        try
        {
            var byteSum = await _metricsBusiness.GetProjectStorageSize(
                organizationId, projectId);
            return Ok(byteSum);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while retrieving byte count for the system: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Get Project Data Source Count 
    /// </summary>
    /// <param name="projectId">The ID of the project whose data sources are to be retrieved</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived data sources from the result (Default true)</param>
    /// <returns>A count of data sources for the given project.</returns>
    [HttpGet("count", Name = "api_count_data_sources_for_project")]
    [Auth("read", "data_source")]
    public async Task<ActionResult<int>> GetDataSourceCount(
        long projectId, 
        [FromQuery] bool hideArchived = true)
    {
        try
        {
            var dataSources = await _metricsBusiness.GetProjectDataSourceCount(projectId, hideArchived);
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
    ///     Get record count for project
    /// </summary>
    /// <param name="organizationId">The ID of the organization the records belong</param>
    /// <param name="projectId">The ID of the project the records belong</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived records from the result</param>
    /// <returns>The record count for the given scope</returns>
    [HttpGet("records/count", Name = "api_record_count_project")]
    [SysAdmin]
    public async Task<IActionResult> GetProjectRecordCount(
        long organizationId, 
        long projectId, 
        [FromQuery] bool hideArchived = true)
    {
        try
        {
            var count = await _metricsBusiness.GetRecordCount(organizationId, projectId, hideArchived: false);
            return Ok(count);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while retrieving record count for organization: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }
    
    /// <summary>
    ///     Get file count for project
    /// </summary>
    /// <param name="organizationId">The ID of the organization the files belong</param>
    /// <param name="projectId">The ID of the project the files belong</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived files from the result</param>
    /// <returns>The file count for the given scope</returns>
    [HttpGet("files/count", Name = "api_file_count_project")]
    [SysAdmin]
    public async Task<IActionResult> GetProjectFileCount(
        long organizationId, 
        long projectId, 
        [FromQuery] bool hideArchived = true)
    {
        try
        {
            var count = await _metricsBusiness.GetFileCount(organizationId, projectId, hideArchived: false);
            return Ok(count);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while retrieving file count for system: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }
}