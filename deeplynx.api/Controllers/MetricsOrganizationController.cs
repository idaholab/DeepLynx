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
}