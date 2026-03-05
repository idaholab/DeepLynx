using deeplynx.helpers.Context;
using deeplynx.helpers.exceptions;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using deeplynx.helpers;

namespace deeplynx.api.Controllers;

[ApiController]
[Route("organizations/{organizationId:long}/projects/{projectId:long}/datasources/{dataSourceId:long}/timeseries")]
[Authorize]
public class TimeseriesController : ControllerBase
{
    private readonly ILogger<TimeseriesController> _logger;
    private readonly ITimeseriesBusiness _timeseriesBusiness;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TimeseriesController" /> class
    /// </summary>
    /// <param name="timeseriesBusiness">The business logic interface for handling time series operations.</param>
    /// <param name="logger">Error/Info logging interface for database log table.</param>
    public TimeseriesController(ITimeseriesBusiness timeseriesBusiness, ILogger<TimeseriesController> logger)
    {
        _timeseriesBusiness = timeseriesBusiness;
        _logger = logger;
    }

    /// <summary>
    ///     Query Timeseries
    /// </summary>
    /// <param name="organizationId">ID of organization that timeseries data is associated with</param>
    /// <param name="projectId">ID of project that timeseries data is associated with</param>
    /// <param name="dataSourceId">ID of data source that timeseries data is associated with</param>
    /// <param name="request"> The request containing an sql query string</param>
    /// <param name="fileType">The type of file to convert query to</param>
    /// <returns></returns>
    [HttpPost("query", Name = "api_query_timeseries_blob")]
    [Auth("read", "record")]
    public async Task<ActionResult<PlotDataDto>> QueryTabularFile(long organizationId, long projectId, [FromQuery]long recordId, [FromQuery] string viewName, [FromBody] TimeseriesQueryRequestDto request)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var reportRecordResponse =
                await _timeseriesBusiness.QueryTabularFile(currentUserId,organizationId, projectId, recordId, request.Query, viewName);
            return Ok(reportRecordResponse);
        }
        catch (NoResultsException nrException)
        {
            return Ok(nrException.Message);
        }
        catch (Exception e)
        {
            var message = $"An error occurred while querying timeseries table {e}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Append File to DuckDB Table
    /// </summary>
    /// <param name="organizationId">ID of organization that timeseries data is associated with</param>
    /// <param name="projectId">ID of project that timeseries data is associated with</param>
    /// <param name="dataSourceId">ID of data source that timeseries data is associated with</param>
    /// <param name="file">Timeseries file</param>
    /// <param name="tableName">Name of the duckDB table on which the timeseries data is encoded</param>
    /// <returns></returns>
    [HttpPatch("append", Name = "api_append_timeseries_file")]
    [Auth("update", "file")]
    public async Task<ActionResult<string>> AppendTimeseriesTable(
        long organizationId, long projectId, long dataSourceId, IFormFile file, string tableName)
    {
        try
        {
            await _timeseriesBusiness.AppendTimeseriesTable(organizationId, projectId, dataSourceId, file, tableName);
            return Ok("Data appended");
        }
        catch (Exception e)
        {
            var message = $"An error occurred while appending to a timeseries file for {file.FileName}: {e}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Exports Table to File
    /// </summary>
    /// <param name="organizationId">ID of organization that timeseries data is associated with</param>
    /// <param name="projectId">ID of project that timeseries data is associated with</param>
    /// <param name="dataSourceId">ID of data source that timeseries data is associated with</param>
    /// <param name="tableName">Name of the duckDB table on which the timeseries data is encoded</param>
    /// <param name="fileType">The type of file to convert query to</param>
    /// <returns></returns>
    [HttpGet("export", Name = "api_export_timeseries_table")]
    [Auth("read", "record")]
    [Auth("read", "file")]
    public async Task<IActionResult> ExportTimeseriesTable(
        long organizationId, long projectId, long dataSourceId, [FromQuery] string tableName, string fileType)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var timeseriesUploadRecord =
                await _timeseriesBusiness.ExportTimeseriesTable(currentUserId, organizationId, projectId, dataSourceId,
                    tableName,
                    fileType);
            return Ok(new { TimeseriesUploadRecord = timeseriesUploadRecord });
        }
        catch (Exception e)
        {
            var message = $"An error occurred while querying a timeseries table {tableName}: {e}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Get a view of data points
    /// </summary>
    /// <param name="organizationId">ID of organization that timeseries data is associated with</param>
    /// <param name="projectId">ID of project that timeseries data is associated with</param>
    /// <param name="dataSourceId">ID of data source that timeseries data is associated with</param>
    /// <param name="recordId">Name of the duckDB table on which the timeseries data is encoded</param>
    /// <param name="limit">Maximum number of data points to include</param>
    /// <param name="rowStride">every nth row to get (row number 4 = every 4th row)</param>
    /// <returns>JSON: { timeseriesPlotData: { columns: [], data: [][] } }</returns>
    [HttpGet("plot", Name = "api_plot_data")]
    [Auth("read", "record")]
    [Auth("read", "file")]
    public async Task<IActionResult> GetPlotData(long organizationId, long projectId, long dataSourceId, [FromQuery] long recordId, [FromQuery] long limit, [FromQuery] long rowStride)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var timeseriesPlotData = await _timeseriesBusiness.GetPlotData(currentUserId, organizationId, projectId, dataSourceId, recordId, limit, rowStride);
            return Ok(new { TimeseriesPlotData = timeseriesPlotData });
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
}