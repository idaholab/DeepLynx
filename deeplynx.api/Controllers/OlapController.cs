using deeplynx.helpers;
using deeplynx.helpers.Context;
using deeplynx.helpers.exceptions;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace deeplynx.api.Controllers;

[ApiController]
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
    /// <returns></returns>
    [HttpPost("query", Name = "api_execute_olap_query")]
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
    ///     Append Tabular File
    /// </summary>
    /// <param name="organizationId">ID of organization the tabular data is associated with</param>
    /// <param name="projectId">ID of project the tabular data is associated with</param>
    /// <param name="recordId"> ID of the record being appended to</param>
    /// <param name="partNumber"> Part number of the file being appended</param>
    /// <param name="file"> Tabular file to append</param>
    /// <returns></returns>
    [HttpPatch("append", Name = "api_append_tabular_file")]
    [Auth("read", "record")]
    [Auth("update", "file")]
    [Sensitivity("update file")]
    public async Task<ActionResult<string>> AppendTabularFile(
        long organizationId, long projectId, long recordId, [FromQuery] long partNumber, IFormFile file)
    {
        try
        {
            await _olapBusiness.AppendTabularBlob(organizationId, projectId, recordId, partNumber, file);
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
    ///     Exports Table to File
    /// </summary>
    /// <param name="organizationId">ID of organization the tabular data is associated with</param>
    /// <param name="projectId">ID of project that timeseries data is associated with</param>
    /// <param name="dataSourceId">ID of data source that timeseries data is associated with</param>
    /// <param name="tableName">Name of the duckDB table on which the timeseries data is encoded</param>
    /// <param name="fileType">The type of file to convert query to</param>
    /// <returns></returns>
    // [HttpGet("export", Name = "api_export_timeseries_table")]
    // [Auth("read", "record")]
    // [Auth("read", "file")]
    // public async Task<IActionResult> ExportTimeseriesTable(
    //     long organizationId, long projectId, long dataSourceId, [FromQuery] string tableName, string fileType)
    // {
    //     try
    //     {
    //         var currentUserId = UserContextStorage.UserId;
    //         var timeseriesUploadRecord =
    //             await _timeseriesBusiness.ExportTimeseriesTable(currentUserId, organizationId, projectId, dataSourceId,
    //                 tableName,
    //                 fileType);
    //         return Ok(new { TimeseriesUploadRecord = timeseriesUploadRecord });
    //     }
    //     catch (Exception e)
    //     {
    //         var message = $"An error occurred while querying a timeseries table {tableName}: {e}";
    //         _logger.LogError(message);
    //         return StatusCode(StatusCodes.Status500InternalServerError, message);
    //     }
    // }


    /// <summary>
    ///     Get a View of Data Points
    /// </summary>
    /// <param name="organizationId">ID of organization the tabular data is associated with</param>
    /// <param name="projectId">ID of project the tabular data is associated with</param>
    /// <param name="recordId">ID of the record pointing to the file or folder to plot</param>
    /// <param name="request">Windowing, column selection, and downsampling options</param>
    /// <returns>JSON: { PlotData: { columns: [], data: [][] } }</returns>
    [HttpGet("plot", Name = "api_plot_data")]
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
    ///     Get Highest Part Number (For Appending)
    /// </summary>
    /// <param name="organizationId">ID of organization the tabular data is associated with</param>
    /// <param name="projectId">ID of project the tabular data is associated with</param>
    /// <param name="recordId">ID of the record pointing to the file or folder to data</param>
    [HttpGet("part", Name = "api_highest_part_number")]
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
}
