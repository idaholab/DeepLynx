using Asp.Versioning;
using deeplynx.helpers;
using deeplynx.helpers.Context;
using deeplynx.helpers.exceptions;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;

namespace deeplynx.api.Controllers.V2;

[ApiController]

[ApiVersion(2)]
[Route("organizations/{organizationId:long}/projects/{projectId:long}/records/{recordId:long}/olap")]
[Authorize]
[Tags("Olap")]
public class OlapController : ControllerBase
{
    private readonly ILogger<OlapController> _logger;
    private readonly IOlapBusiness _olapBusiness;

    /// <summary>
    ///     Initializes a new instance of the <see cref="OlapController" /> class
    /// </summary>
    /// <param olapBusiness">The business logic interface for handling time series operations.</param>
    /// <param name="logger">Error/Info logging interface for database log table.</param>
    public OlapController(IOlapBusiness olapBusiness, ILogger<OlapController> logger)
    {
        _olapBusiness = olapBusiness;
        _logger = logger;
    }


    
    /// <summary>
    ///     Execute OLAP Query
    /// </summary>
    /// <param name="organizationId">ID of organization that tabular data is associated with</param>
    /// <param name="projectId">ID of project the tabular data is associated with</param>
    /// <param name="request"> The request containing an sql query string</param>
    /// <param name="viewName"> The request containing an sql query string</param>
    /// <param name="recordId"> ID of the record to query from</param>
    /// <returns>Query response</returns>
    [HttpPost("query", Name = "api_execute_olap_query")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "record")]
    [Auth("read", "file")]
    [Sensitivity("download file")]
    public async Task<ActionResult<PlotDataDto>> ExecuteOlapQuery(long organizationId, long projectId, long recordId,
        [FromQuery] string viewName, [FromBody] OlapQueryRequestDto request)
    {
        var currentUserId = UserContextStorage.UserId;
        var reportRecordResponse =
            await _olapBusiness.QueryTabularFile(currentUserId, organizationId, projectId, recordId, request,
                viewName);
        return Ok(reportRecordResponse);
    }


    
    /// <summary>
    ///     Append Tabular File
    /// </summary>
    /// <param name="organizationId">ID of organization the tabular data is associated with</param>
    /// <param name="projectId">ID of project the tabular data is associated with</param>
    /// <param name="recordId"> ID of the record being appended to</param>
    /// <param name="partNumber"> Part number of the file being appended</param>
    /// <param name="file"> Tabular file to append</param>
    /// <returns></returns>
    [HttpPatch("append", Name = "api_append_tabular_file")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "record")]
    [Auth("update", "file")]
    [Sensitivity("update file")]
    public async Task<ActionResult<string>> AppendTabularFile(
        long organizationId, long projectId, long recordId, [FromQuery] long partNumber, IFormFile file)
    {
        var currentUserId = UserContextStorage.UserId;
        await _olapBusiness.AppendTabularBlob(currentUserId, organizationId, projectId, recordId, partNumber, file);
        return Ok();
     
    }


    
    /// <summary>
    ///     Get a View of Data Points
    /// </summary>
    /// <param name="organizationId">ID of organization the tabular data is associated with</param>
    /// <param name="projectId">ID of project the tabular data is associated with</param>
    /// <param name="recordId">ID of the record pointing to the file or folder to plot</param>
    /// <param name="request">Windowing, column selection, and downsampling options</param>
    /// <returns>PlotDataDto</returns>
    [HttpGet("plot", Name = "api_plot_data")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "record")]
    [Auth("read", "file")]
    [Sensitivity("download file")]
    public async Task<ActionResult<PlotDataDto>> GetPlotData(long organizationId, long projectId, long recordId,
        [FromQuery] OlapQueryRequestDto request)
    {
        var currentUserId = UserContextStorage.UserId;
        var plotData =
            await _olapBusiness.GetPlotData(currentUserId, organizationId, projectId, recordId, request);
        return Ok(plotData);
    }


    
    /// <summary>
    ///     Get Highest Part Number (For Appending)
    /// </summary>
    /// <param name="organizationId">ID of organization the tabular data is associated with</param>
    /// <param name="projectId">ID of project the tabular data is associated with</param>
    /// <param name="recordId">ID of the record pointing to the file or folder to data</param>
    /// <returns>Part Number</returns>
    [HttpGet("part", Name = "api_highest_part_number")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "record")]
    [Auth("read", "file")]
    [Sensitivity("download file")]
    public async Task<ActionResult<long>> GetHighestPartNumber(long organizationId, long projectId, long recordId)
    {
       var partNumber = await _olapBusiness.GetHighestPartNumber(organizationId, projectId, recordId);
            return Ok(partNumber);
    }
}
