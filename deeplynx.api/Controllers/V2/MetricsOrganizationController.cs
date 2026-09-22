using Asp.Versioning;
using deeplynx.helpers;
using deeplynx.interfaces;
using deeplynx.models.MetricsDTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;

namespace deeplynx.api.Controllers.V2;

/// <summary>
///     Controller for retrieving summary statistics at an org level
/// </summary>
/// <remarks>
///     This controller provides endpoints to populate the DeepLynx metrics pages for Nexus org admins.
/// </remarks>
[ApiController]
[ApiVersion(2)]
[Route("organization/{organizationId:long}/metrics")]
[Authorize]
[ForbidServiceAccounts] // service accounts can only act on the project level
[Tags("Organization - Metrics")]
public class MetricsOrganizationController : ControllerBase
{
    private readonly ILogger<MetricsController> _logger;
    private readonly IMetricsBusiness _metricsBusiness;

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
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<ActionResult<StorageSizeDto>> GetOrganizationStorageSize(long organizationId)
    {
        var byteSum = await _metricsBusiness.GetOrganizationStorageSize(organizationId);
        return Ok(byteSum);
    }


    
    /// <summary>
    ///     Get Organization Data Source Count
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the projectID belongs</param>
    /// <param name="projectIds">(Optional)An array of project IDs within the organization to filter by</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived data sources from the result (Default true)</param>
    /// <returns>A count of data sources for the given organization and its projects.</returns>
    [HttpGet("count", Name = "api_count_data_sources_for_organization")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<ActionResult<int>> GetDataSourceCount(
        long organizationId,
        [FromQuery] long[]? projectIds,
        [FromQuery] bool hideArchived = true)
    {
        var dataSources =
            await _metricsBusiness.GetOrganizationDataSourceCount(organizationId, projectIds, hideArchived);
        return Ok(dataSources);
    }


    
    /// <summary>
    ///     Get record count for organization
    /// </summary>
    /// <param name="organizationId">The ID of the organization the records belong</param>
    /// <param name="projectIds">The IDs of the projects the records belong</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived records from the result</param>
    /// <returns>The record count for the given scope</returns>
    [HttpGet("records/count", Name = "api_record_count_organization")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<ActionResult<int>> GetOrganizationRecordCount(
        long organizationId,
        [FromQuery] long[]? projectIds,
        [FromQuery] bool hideArchived = true)
    {
        var count = await _metricsBusiness.GetRecordCount(organizationId, projectIds, hideArchived);
        return Ok(count);
    }


    
    /// <summary>
    ///     Get file count for organization
    /// </summary>
    /// <param name="organizationId">The ID of the organization the files belong</param>
    /// <param name="projectIds">The IDs of the projects the files belong</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived files from the result</param>
    /// <returns>The file count for the given scope</returns>
    [HttpGet("files/count", Name = "api_file_count_organization")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<ActionResult<int>> GetOrganizationFileCount(
        long organizationId,
        [FromQuery] long[]? projectIds,
        [FromQuery] bool hideArchived = true)
    {
        var count = await _metricsBusiness.GetFileCount(organizationId, projectIds, hideArchived);
        return Ok(count);
    }


    
    /// <summary>
    ///     Get Organization Data Modality Count
    /// </summary>
    /// <param name="organizationId"></param>
    /// <returns></returns>
    [HttpGet("modalities/count", Name = "api_count_data_modality_for_organization")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "data_source")]
    public async Task<ActionResult<int>> GetOrganizationDataModalityCount(
        long organizationId)
    {
       var dataSources = await _metricsBusiness.GetOrganizationDataModalityCount(organizationId, null);
            return Ok(dataSources);
    }
}