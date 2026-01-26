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
    [HttpPost("query", Name = "api_query_timeseries")]
    [Auth("read", "record")]
    public async Task<ActionResult<RecordResponseDto>> QueryTimeseries(
        long organizationId, long projectId, long dataSourceId,
        [FromQuery] string fileType, [FromBody] TimeseriesQueryRequestDto request)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var reportRecordResponse =
                await _timeseriesBusiness.QueryTimeseries(currentUserId, request, organizationId, projectId,
                    dataSourceId, fileType);
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
    ///     Upload Timeseries File
    /// </summary>
    /// <param name="organizationId">ID of organization that timeseries data is associated with</param>
    /// <param name="projectId">ID of project that timeseries data is associated with</param>
    /// <param name="dataSourceId">ID of data source that timeseries data is associated with</param>
    /// <param name="file">Timeseries file</param>
    /// <returns>Record response DTO</returns>
    [HttpPost("upload", Name = "api_upload_timeseries_file")]
    [Auth("write", "record")]
    public async Task<ActionResult<RecordResponseDto>> UploadFile(
        long organizationId, long projectId, long dataSourceId, IFormFile file)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var timeSeriesUploadInfo =
                await _timeseriesBusiness.UploadFile(currentUserId, organizationId, projectId, dataSourceId, file);
            return Ok(timeSeriesUploadInfo);
        }
        catch (Exception e)
        {
            var message = $"An error occurred while uploading timeseries file {file.FileName}: {e}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Start Timeseries Upload
    /// </summary>
    /// <param name="organizationId">ID of organization that timeseries data is associated with</param>
    /// <param name="projectId">ID of project that timeseries data is associated with</param>
    /// <param name="dataSourceId">ID of data source that timeseries data is associated with</param>
    /// <param name="request">Timeseries request DTO</param>
    /// <returns>{UploadId}</returns>
    [HttpPost("upload/start", Name = "api_start_timeseries_upload")]
    [Auth("write", "record")]
    public async Task<IActionResult> StartUpload(
        long organizationId, long projectId, long dataSourceId, [FromBody] TimeseriesUploadInitRequestDto request)
    {
        try
        {
            var uploadId =
                await _timeseriesBusiness.StartUpload(organizationId, projectId, dataSourceId, request.FileName);
            return Ok(new { UploadId = uploadId });
        }
        catch (Exception e)
        {
            var message = $"An error occurred while starting an upload for timeseries file {request.FileName}: {e}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Upload Timeseries Chunk
    /// </summary>
    /// <param name="organizationId">ID of organization that timeseries data is associated with</param>
    /// <param name="projectId">ID of project that timeseries data is associated with</param>
    /// <param name="dataSourceId">ID of data source that timeseries data is associated with</param>
    /// <param name="chunk">Chunk from form</param>
    /// <param name="uploadId">ID of upload</param>
    /// <param name="chunkNumber">Chunk number from form</param>
    /// <returns>{ChunkUploadStatus}</returns>
    [HttpPost("upload/chunk", Name = "api_upload_timeseries_chunk")]
    [Auth("write", "record")]
    public async Task<IActionResult> UploadChunk(
        long organizationId, long projectId, long dataSourceId,
        IFormFile chunk, [FromForm] string uploadId, [FromForm] int chunkNumber)
    {
        try
        {
            var chunkUploadStatus =
                await _timeseriesBusiness.UploadChunk(organizationId, projectId, dataSourceId, chunk, uploadId,
                    chunkNumber);
            return Ok(new { ChunkUploadStatus = chunkUploadStatus });
        }
        catch (Exception e)
        {
            var message = $"An error occurred while uploading a chunk for timeseries file {uploadId}: {e}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Complete Timeseries Upload
    /// </summary>
    /// <param name="organizationId">ID of organization that timeseries data is associated with</param>
    /// <param name="projectId">ID of project that timeseries data is associated with</param>
    /// <param name="dataSourceId">ID of data source that timeseries data is associated with</param>
    /// <param name="request">Timeseries request DTO</param>
    /// <returns>{TimeseriesUploadRecord}</returns>
    [HttpPost("upload/complete", Name = "api_complete_timeseries_upload")]
    [Auth("write", "record")]
    public async Task<ActionResult<RecordResponseDto>> CompleteUpload(
        long organizationId, long projectId, long dataSourceId,
        [FromBody] TimeseriesUploadCompleteRequestDto request)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var timeseriesUploadRecord =
                await _timeseriesBusiness.CompleteUpload(currentUserId, organizationId, projectId, dataSourceId,
                    request);
            return Ok(new { TimeseriesUploadRecord = timeseriesUploadRecord });
        }
        catch (Exception e)
        {
            var message =
                $"An error occurred while completing a timeseries file upload for {request.FileName}: {e}";
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
    [Auth("write", "record")]
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
    ///     Get Every Nth Timeseries Table Row
    /// </summary>
    /// <param name="organizationId">ID of organization that timeseries data is associated with</param>
    /// <param name="projectId">ID of project that timeseries data is associated with</param>
    /// <param name="dataSourceId">ID of data source that timeseries data is associated with</param>
    /// <param name="tableName">Name of the duckDB table on which the timeseries data is encoded</param>
    /// <param name="rowNumber">every nth row to get (row number 4 = every 4th row)</param>
    /// <param name="fileType">The type of file to convert query to</param>
    /// <returns></returns>
    [HttpGet("interpolate", Name = "api_interpolate_timeseries_rows")]
    [Auth("read", "record")]
    public async Task<IActionResult> InterpolateRows(
        long organizationId, long projectId, long dataSourceId,
        [FromQuery] string tableName, [FromQuery] string rowNumber, [FromQuery] string fileType)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var timeseriesUploadRecord =
                await _timeseriesBusiness.InterpolateRows(currentUserId, organizationId, projectId, dataSourceId,
                    rowNumber, tableName,
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
    public async Task<IActionResult> GetPlotData(long organizationId, long projectId, long dataSourceId, [FromQuery] long recordId, [FromQuery] long limit, [FromQuery] long rowStride)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var timeseriesPlotData = await _timeseriesBusiness.GetPlotData(organizationId, projectId, dataSourceId, recordId, limit, rowStride);
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

    /// <summary>
    ///     Get the most recent row
    /// </summary>
    /// <param name="organizationId">ID of organization that timeseries data is associated with</param>
    /// <param name="projectId">ID of project that timeseries data is associated with</param>
    /// <param name="dataSourceId">ID of data source that timeseries data is associated with</param>
    /// <param name="recordId">Name of the duckDB table on which the timeseries data is encoded</param>
    /// <returns>JSON object with column names as keys and the latest row's values</returns>
    [HttpGet("latest", Name = "api_latest_row")]
    [Auth("read", "record")]
    public async Task<IActionResult> GetLatestRow(long organizationId, long projectId, long dataSourceId, [FromQuery] long recordId)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var latestRow = await _timeseriesBusiness.GetLatestRow(organizationId, projectId, dataSourceId, recordId);
            return Ok(new { LatestRowData = latestRow });
        }
        catch (ArgumentException e)
        {
            _logger.LogWarning(e, "Invalid request for latest row");
            return BadRequest(e.Message);
        }
        catch (Exception e)
        {
            if (_logger.IsEnabled(LogLevel.Error))
                _logger.LogError(e, "Error retrieving latest row for record {RecordId}", recordId);
            return StatusCode(StatusCodes.Status500InternalServerError, e.Message);
        }
    }
}