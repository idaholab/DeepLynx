using Asp.Versioning;
using deeplynx.helpers;
using deeplynx.helpers.Context;
using deeplynx.helpers.exceptions;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;

namespace deeplynx.api.Controllers;

[ApiController]
[ApiVersion(1, Deprecated = true)]
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
    /// <returns>Query respoonse</returns>
    [HttpPost("query", Name = "api_execute_olap_query")]
    [MapToApiVersion(1)]
    [Auth("read", "record")]
    [Auth("read", "file")]
    [Sensitivity("download file")]
    public async Task<ActionResult<PlotDataDto>> ExecuteOlapQuery(long organizationId, long projectId, long recordId,
        [FromQuery] string viewName, [FromBody] OlapQueryRequestDto request)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var reportRecordResponse =
                await _olapBusiness.QueryTabularFile(currentUserId, organizationId, projectId, recordId, request,
                    viewName);
            return Ok(reportRecordResponse);
        }
        catch (NoResultsException nrException)
        {
            return Ok(nrException.Message);
        }
        catch (ArgumentException e)
        {
            _logger.LogWarning(e, "Invalid OLAP query request");
            return BadRequest(e.Message);
        }
        catch (InvalidOperationException e)
        {
            _logger.LogWarning(e, "Invalid OLAP query request");
            return BadRequest(e.Message);
        }
        catch (Exception e)
        {
            var message = $"An error occurred while querying tabular data {e}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
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
    [MapToApiVersion(2)]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "record")]
    [Auth("read", "file")]
    [Sensitivity("download file")]
    public async Task<ActionResult<PlotDataDto>> ExecuteOlapQueryV2(long organizationId, long projectId, long recordId,
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
    /// <returns>String "Data Appended"</returns>
    [HttpPatch("append", Name = "api_append_tabular_file")]
    [MapToApiVersion(1)]
    [Auth("read", "record")]
    [Auth("update", "file")]
    [Sensitivity("update file")]
    public async Task<ActionResult<string>> AppendTabularFile(
        long organizationId, long projectId, long recordId, [FromQuery] long partNumber, IFormFile file)
    {
        var currentUserId = UserContextStorage.UserId;

        try
        {
            await _olapBusiness.AppendTabularBlob(currentUserId, organizationId, projectId, recordId, partNumber, file);
            return Ok("Data appended");
        }
        catch (Exception e)
        {
            var message = $"An error occurred while appending to a tabular file for {file.FileName}: {e}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
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
    [MapToApiVersion(2)]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "record")]
    [Auth("update", "file")]
    [Sensitivity("update file")]
    public async Task<ActionResult<string>> AppendTabularFileV2(
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
    /// <returns>JSON: { PlotData: { columns: [], data: [][] } }</returns>
    [HttpGet("plot", Name = "api_plot_data")]
    [MapToApiVersion(1)]
    [Auth("read", "record")]
    [Auth("read", "file")]
    [Sensitivity("download file")]
    public async Task<IActionResult> GetPlotData(long organizationId, long projectId, long recordId,
        [FromQuery] OlapQueryRequestDto request)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var plotData =
                await _olapBusiness.GetPlotData(currentUserId, organizationId, projectId, recordId, request);
            return Ok(new { PlotData = plotData });
        }
        catch (ArgumentException e)
        {
            _logger.LogWarning(e, "Invalid request for plot data");
            return BadRequest(e.Message);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error retrieving plot data for record {RecordId}", recordId);
            return StatusCode(StatusCodes.Status500InternalServerError, e.Message);
        }
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
    [MapToApiVersion(2)]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "record")]
    [Auth("read", "file")]
    [Sensitivity("download file")]
    public async Task<ActionResult<PlotDataDto>> GetPlotDataV2(long organizationId, long projectId, long recordId,
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
    /// <returns>Part number</returns>
    [HttpGet("part", Name = "api_highest_part_number")]
    [MapToApiVersion(1)]
    [Auth("read", "record")]
    [Auth("read", "file")]
    [Sensitivity("download file")]
    public async Task<IActionResult> GetHighestPartNumber(long organizationId, long projectId, long recordId)
    {
        try
        {
            var partNumber = await _olapBusiness.GetHighestPartNumber(organizationId, projectId, recordId);
            return Ok(partNumber);
        }
        catch (ArgumentException e)
        {
            _logger.LogWarning(e, "Invalid request to get highest part number");
            return BadRequest(e.Message);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error retrieving highest part number for record {RecordId}", recordId);
            return StatusCode(StatusCodes.Status500InternalServerError, e.Message);
        }
    }
    
    /// <summary>
    ///     Get Highest Part Number (For Appending)
    /// </summary>
    /// <param name="organizationId">ID of organization the tabular data is associated with</param>
    /// <param name="projectId">ID of project the tabular data is associated with</param>
    /// <param name="recordId">ID of the record pointing to the file or folder to data</param>
    /// <returns>Part Number</returns>
    [HttpGet("part", Name = "api_highest_part_number")]
    [MapToApiVersion(2)]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [Auth("read", "record")]
    [Auth("read", "file")]
    [Sensitivity("download file")]
    public async Task<ActionResult<long>> GetHighestPartNumberV2(long organizationId, long projectId, long recordId)
    {
       var partNumber = await _olapBusiness.GetHighestPartNumber(organizationId, projectId, recordId);
            return Ok(partNumber);
    }
}
