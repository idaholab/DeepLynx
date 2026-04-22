using deeplynx.helpers;
using deeplynx.interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace deeplynx.api.Controllers;

[ApiController]
[Route("metrics")]
[Authorize]
[Tags("Metrics")]
public class MetricsController : ControllerBase
{
    private readonly IMetricsBusiness _metricsBusiness;
    private readonly ILogger<MetricsController> _logger;
    
    public MetricsController(
        IMetricsBusiness metricsBusiness, 
        ILogger<MetricsController> logger)
    {
        _metricsBusiness = metricsBusiness;
        _logger = logger;
    }

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
}
