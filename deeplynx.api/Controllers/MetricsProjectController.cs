using deeplynx.helpers;
using deeplynx.interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace deeplynx.api.Controllers;

[ApiController]
[Route("organizations/{organizationId:long}/projects/{projectId:long}/metrics")]
[Authorize]
[Tags("Project - Metrics")]
public class MetricsProjectController : ControllerBase
{
    private readonly IMetricsBusiness _metricsBusiness;
    private readonly ILogger<MetricsController> _logger;
    
    public MetricsProjectController(
        IMetricsBusiness metricsBusiness, 
        ILogger<MetricsController> logger)
    {
        _metricsBusiness = metricsBusiness;
        _logger = logger;
    }

    [HttpGet("storage/size", Name = "api_storage_size_project")]
    [SysAdmin]
    public async Task<IActionResult> GetProjectStorageSize(
        long organizationId, long projectId)
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
}