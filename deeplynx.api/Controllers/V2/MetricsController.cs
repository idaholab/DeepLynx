using Asp.Versioning;
using deeplynx.helpers;
using deeplynx.interfaces;
using deeplynx.models.MetricsDTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;

namespace deeplynx.api.Controllers.V2;

/// <summary>
///     Controller for retrieving summary statistics at a system-wide level.
/// </summary>
/// <remarks>
///     This controller provides endpoints to populate the DeepLynx metrics pages for Nexus admins.
/// </remarks>
[ApiController]
[ApiVersion(2)]
[Route("metrics")]
[Authorize]
[ForbidServiceAccounts] // service accounts can only act on the project level
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
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [SysAdmin]
    public async Task<ActionResult<StorageSizeDto>> GetSystemStorageSize()
    {
            var byteSum = await _metricsBusiness.GetSystemStorageSize();
            return Ok(byteSum);
    }


    
    /// <summary>
    ///     Get System Data Source Count 
    /// </summary>
    /// <param name="hideArchived">Flag indicating whether to hide archived data sources from the result (Default true)</param>
    /// <returns>A count of data sources for the given project.</returns>
    [HttpGet("datasources/count", Name = "api_datasource_count_system")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [SysAdmin]
    public async Task<ActionResult<int>> GetSystemDataSourceCount(bool hideArchived = true)
    {
            var byteSum = await _metricsBusiness.GetSystemDataSourceCount(hideArchived);
            return Ok(byteSum);
    }


    
    /// <summary>
    ///     Get record count for system
    /// </summary>
    /// <param name="hideArchived">Flag indicating whether to hide archived records from the result</param>
    /// <returns>The record count for the given scope</returns>
    [HttpGet("records/count", Name = "api_record_count_system")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [SysAdmin]
    public async Task<ActionResult<int>> GetSystemRecordCount(bool hideArchived = true)
    {
       var count = await _metricsBusiness.GetRecordCount(organizationId: null, projectIds: null, hideArchived);
            return Ok(count);
    }


    
    /// <summary>
    ///     Get file count for system
    /// </summary>
    /// <param name="hideArchived">Flag indicating whether to hide archived files from the result</param>
    /// <returns>The file count for the given scope</returns>
    [HttpGet("files/count", Name = "api_file_count_system")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [SysAdmin]
    public async Task<ActionResult<int>> GetSystemFileCount(bool hideArchived = true)
    {
       var count = await _metricsBusiness.GetFileCount(organizationId: null, projectIds: null, hideArchived);
            return Ok(count);
    }
}
