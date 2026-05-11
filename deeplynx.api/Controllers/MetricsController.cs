using deeplynx.helpers;
using deeplynx.interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace deeplynx.api.Controllers;

/// <summary>
///     Controller for retrieving summary statistics at a system-wide level.
/// </summary>
/// <remarks>
///     This controller provides endpoints to populate the DeepLynx metrics pages for Nexus admins.
/// </remarks>
[ApiController]
[Route("metrics")]
[Authorize]
[Tags("Metrics")]
public class MetricsController : ControllerBase
{
    private readonly IMetricsBusiness _metricsBusiness;
    private readonly ILogger<MetricsController> _logger;
    
    /// <summary>
    ///     Initializes a new instance of the <see cref="MetricsController" /> class
    /// </summary>
    /// <param name="metricsBusiness">The business logic interface for handling metrics retrievals</param>
    /// <param name="logger">Error/info logging interface for database log table</param>
    public MetricsController(
        IMetricsBusiness metricsBusiness, 
        ILogger<MetricsController> logger)
    {
        _metricsBusiness = metricsBusiness;
        _logger = logger;
    }

    /// <summary>
    ///     Get Bytes Ingested
    /// </summary>
    /// <returns>The total number of bytes of file data stored in Nexus-registered object storages.</returns>
    [HttpGet("storage/size", Name = "api_storage_size_system")]
    [SysAdmin]
    public async Task<IActionResult> GetSystemStorageSize()
    {
        try
        {
            var byteSum = await _metricsBusiness.GetSystemStorageSize();
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
    ///     Get System Data Source Count 
    /// </summary>
    /// <param name="hideArchived">Flag indicating whether to hide archived data sources from the result (Default true)</param>
    /// <returns>A count of data sources for the given project.</returns>
    [HttpGet("datasources/count", Name = "api_datasource_count_system")]
    [SysAdmin]
    public async Task<IActionResult> GetSystemDataSourceCount(bool hideArchived = true)
    {
        try
        {
            var byteSum = await _metricsBusiness.GetSystemDataSourceCount(hideArchived);
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
    ///     Get record count for system
    /// </summary>
    /// <param name="hideArchived">Flag indicating whether to hide archived records from the result</param>
    /// <returns>The record count for the given scope</returns>
    [HttpGet("records/count", Name = "api_record_count_system")]
    [SysAdmin]
    public async Task<IActionResult> GetSystemRecordCount(bool hideArchived = true)
    {
        try
        {
            var count = await _metricsBusiness.GetRecordCount(organizationId: null, projectIds: null, hideArchived: false);
            return Ok(count);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while retrieving record count for system: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }
    
    /// <summary>
    ///     Get file count for system
    /// </summary>
    /// <param name="hideArchived">Flag indicating whether to hide archived files from the result</param>
    /// <returns>The file count for the given scope</returns>
    [HttpGet("files/count", Name = "api_file_count_system")]
    [SysAdmin]
    public async Task<IActionResult> GetSystemFileCount(bool hideArchived = true)
    {
        try
        {
            var count = await _metricsBusiness.GetFileCount(organizationId: null, projectIds: null, hideArchived: false);
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
