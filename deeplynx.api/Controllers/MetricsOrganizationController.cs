using deeplynx.helpers;
using deeplynx.interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace deeplynx.api.Controllers;

/// <summary>
///     Controller for retrieving summary statistics at an org level
/// </summary>
/// <remarks>
///     This controller provides endpoints to populate the DeepLynx metrics pages for Nexus org admins.
/// </remarks>
[ApiController]
[Route("organization/{organizationId:long}/metrics")]
[Authorize]
[Tags("Organization - Metrics")]
public class MetricsOrganizationController : ControllerBase
{
    private readonly IMetricsBusiness _metricsBusiness;
    private readonly ILogger<MetricsController> _logger;
    
    /// <summary>
    ///     Initializes a new instance of the <see cref="MetricsOrganizationController" /> class
    /// </summary>
    /// <param name="metricsBusiness">The business logic interface for handling metrics retrievals</param>
    /// <param name="logger">Error/info logging interface for database log table</param>
    public MetricsOrganizationController(
        IMetricsBusiness metricsBusiness, 
        ILogger<MetricsController> logger)
    {
        _metricsBusiness = metricsBusiness;
        _logger = logger;
    }

    /// <summary>
    ///     Get Bytes Ingested
    /// </summary>
    /// <param name="organizationId">The organization from which to retrieve the summary statistic</param>
    /// <returns>The total number of bytes of file data stored in this org's registered object storages.</returns>
    [HttpGet("storage/size", Name = "api_storage_size_organization")]
    [SysAdmin]
    public async Task<IActionResult> GetOrganizationStorageSize(long organizationId)
    {
        try
        {
            var byteSum = await _metricsBusiness.GetOrganizationStorageSize(organizationId);
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
    ///     Get Organization Data Source Count 
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the projectID belongs</param>
    /// <param name="projectIds">(Optional)An array of project IDs within the organization to filter by</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived data sources from the result (Default true)</param>
    /// <returns>A count of data sources for the given organization and its projects.</returns>
    [HttpGet("count", Name = "api_count_data_sources_for_organization")]
    [Auth("read", "data_source")]
    public async Task<ActionResult<int>> GetDataSourceCount(
        long organizationId,
        [FromQuery] long[]? projectIds,
        [FromQuery] bool hideArchived = true)
    {
        try
        {
            var dataSources = await _metricsBusiness.GetOrganizationDataSourceCount(organizationId, projectIds, hideArchived);
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
    /// Get Organization Data Modality Count
    /// </summary>
    /// <param name="organizationId"></param>
    /// <returns></returns>
    [HttpGet("modalities/count", Name = "api_count_data_modality_for_organization")]
    [Auth("read", "data_source")]
    public async Task<ActionResult<int>> GetOrganizationDataModalityCount(
        long organizationId)
    {
        try
        {
            var dataSources = await _metricsBusiness.GetOrganizationDataModalityCount(organizationId, null);
            return Ok(dataSources);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while listing data modalities";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }
}
