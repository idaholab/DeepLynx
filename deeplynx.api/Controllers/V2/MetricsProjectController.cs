using Asp.Versioning;
using deeplynx.helpers;
using deeplynx.interfaces;
using deeplynx.models.MetricsDTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;

namespace deeplynx.api.Controllers.V2;

/// <summary>
///     Controller for retrieving summary statistics at a project level
/// </summary>
/// <remarks>
///     This controller provides endpoints to populate the DeepLynx metrics pages for Nexus project admins.
/// </remarks>
[ApiController]
[ApiVersion(2)]
[Route("organizations/{organizationId:long}/projects/{projectId:long}/metrics")]
[Authorize]
[Tags("Project - Metrics")]
public class MetricsProjectController : ControllerBase
{
    private readonly ILogger<MetricsController> _logger;
    private readonly IMetricsBusiness _metricsBusiness;

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
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<ActionResult<StorageSizeDto>> GetProjectStorageSize(
        long organizationId,
        long projectId
    )
    {
        var byteSum = await _metricsBusiness.GetProjectStorageSize(
            organizationId, projectId);
        return Ok(byteSum);
    }


    
    /// <summary>
    ///     Get Project Data Source Count
    /// </summary>
    /// <param name="projectId">The ID of the project whose data sources are to be retrieved</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived data sources from the result (Default true)</param>
    /// <returns>A count of data sources for the given project.</returns>
    [HttpGet("count", Name = "api_count_data_sources_for_project")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<ActionResult<int>> GetDataSourceCount(
        long projectId,
        [FromQuery] bool hideArchived = true)
    {
        var dataSources = await _metricsBusiness.GetProjectDataSourceCount(projectId, hideArchived);
        return Ok(dataSources);
    }
    

    
    /// <summary>
    ///     Get record count for project
    /// </summary>
    /// <param name="organizationId">The ID of the organization the records belong</param>
    /// <param name="projectId">The ID of the project the records belong</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived records from the result</param>
    /// <returns>The record count for the given scope</returns>
    [HttpGet("records/count", Name = "api_record_count_project")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<ActionResult<int>> GetProjectRecordCount(
        long organizationId, 
        long projectId, 
        [FromQuery] bool hideArchived = true)
    {
        var count = await _metricsBusiness.GetRecordCount(organizationId, projectId, hideArchived: false);
        return Ok(count);
    }
    

    
    /// <summary>
    ///     Get file count for project
    /// </summary>
    /// <param name="organizationId">The ID of the organization the files belong</param>
    /// <param name="projectId">The ID of the project the files belong</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived files from the result</param>
    /// <returns>The file count for the given scope</returns>
    [HttpGet("files/count", Name = "api_file_count_project")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<ActionResult<int>> GetProjectFileCount(
        long organizationId, 
        long projectId, 
        [FromQuery] bool hideArchived = true)
    {
        var count = await _metricsBusiness.GetFileCount(organizationId, projectId, hideArchived: false);
        return Ok(count);
    }


    
    /// <summary>
    ///     Gets the count of data modalities for a project
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="projectId"></param>
    /// <returns></returns>
    [HttpGet("modalities/count", Name = "api_count_data_modality_for_project")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    public async Task<ActionResult<int>> GetProjectDataModalityCount(
        long organizationId,
        long projectId)
    {
        var dataSources = await _metricsBusiness.GetOrganizationDataModalityCount(organizationId, projectId);
        return Ok(dataSources);
    }
}