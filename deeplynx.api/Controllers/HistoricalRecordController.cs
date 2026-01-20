using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using deeplynx.helpers;

namespace deeplynx.api.Controllers;

/// <summary>
///     Controller for managing historical records.
/// </summary>
/// <remarks>
///     This controller provides endpoints to retrieve historical record information and record history.
/// </remarks>
[ApiController]
[Route("organizations/{organizationId:long}/projects/{projectId:long}/records/historical")]
[Authorize]
[Tags("Historical Record")]
public class HistoricalRecordController : ControllerBase
{
    private readonly IHistoricalRecordBusiness _historicalRecordBusiness;
    private readonly ILogger<HistoricalRecordController> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="HistoricalRecordController" /> class
    /// </summary>
    /// <param name="historicalRecordBusiness">The business logic interface for handling historical record operations.</param>
    /// <param name="logger">Error/Info logging interface for database log table.</param>
    public HistoricalRecordController(IHistoricalRecordBusiness historicalRecordBusiness,
        ILogger<HistoricalRecordController> logger)
    {
        _historicalRecordBusiness = historicalRecordBusiness;
        _logger = logger;
    }

    /// <summary>
    ///     Get All Historical Records
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project whose historical records are to be retrieved</param>
    /// <param name="dataSourceId">(Optional) The ID of the datasource by which to filter records</param>
    /// <param name="pointInTime">(Optional) Find the most current records that existed before this point in time</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived records from the result (Default true)</param>
    /// <returns>A list of historical records based on the applied filters.</returns>
    [HttpGet(Name = "api_get_all_historical_records")]
    [Auth("read", "record")]
    public async Task<ActionResult<IEnumerable<HistoricalRecordResponseDto>>> GetAllHistoricalRecords(
        long organizationId,
        long projectId,
        [FromQuery] long? dataSourceId = null,
        [FromQuery] DateTime? pointInTime = null,
        [FromQuery] bool hideArchived = true)
    {
        try
        {
            var records =
                await _historicalRecordBusiness.GetAllHistoricalRecords(projectId, organizationId, dataSourceId,
                    pointInTime,
                    hideArchived);
            return Ok(records);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while listing historical records: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Get a Historical Record
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the record belongs</param>
    /// <param name="recordId">The ID of the record to retrieve</param>
    /// <param name="pointInTime">(Optional) Find the most current record that existed before this point in time</param>
    /// <param name="hideArchived">Flag indicating whether to hide archived records from the result (Default true)</param>
    /// <returns>The historical record at the specified point in time</returns>
    [HttpGet("{recordId:long}", Name = "api_get_a_historical_record")]
    [Auth("read", "record")]
    public async Task<ActionResult<HistoricalRecordResponseDto>> GetHistoricalRecord(
        long organizationId,
        long projectId,
        long recordId,
        [FromQuery] DateTime? pointInTime = null,
        [FromQuery] bool hideArchived = true)
    {
        try
        {
            var record =
                await _historicalRecordBusiness.GetHistoricalRecord(recordId, organizationId, pointInTime,
                    hideArchived);
            return Ok(record);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while retrieving historical record {recordId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Get Record History
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the record belongs</param>
    /// <param name="recordId">The ID of the record for which to retrieve history</param>
    /// <returns>A list of all previous versions of the record</returns>
    [HttpGet("{recordId:long}/history", Name = "api_get_record_history")]
    [Auth("read", "record")]
    public async Task<ActionResult<IEnumerable<HistoricalRecordResponseDto>>> GetRecordHistory(
        long organizationId,
        long projectId,
        long recordId)
    {
        try
        {
            var history = await _historicalRecordBusiness.GetHistoryForRecord(recordId, organizationId);
            return Ok(history);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while retrieving history for record {recordId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }
}