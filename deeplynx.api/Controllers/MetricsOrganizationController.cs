using deeplynx.helpers;
using deeplynx.interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace deeplynx.api.Controllers;

[ApiController]
[Route("organization/{organizationId:long}/metrics")]
[Authorize]
[Tags("Organization - Metrics")]
public class MetricsOrganizationController : ControllerBase
{
    private readonly IMetricsBusiness _metricsBusiness;
    private readonly ILogger<MetricsController> _logger;
    
    public MetricsOrganizationController(
        IMetricsBusiness metricsBusiness, 
        ILogger<MetricsController> logger)
    {
        _metricsBusiness = metricsBusiness;
        _logger = logger;
    }

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