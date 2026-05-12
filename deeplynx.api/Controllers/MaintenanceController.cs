using deeplynx.business;
using deeplynx.helpers;
using deeplynx.interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace deeplynx.api.Controllers;

/// <summary>
///     Sysadmin controller for executing maintenance operations.
/// </summary>
/// <remarks>
///     This mostly entails one-time jobs that cannot be resolved via EF migration.
/// </remarks>
[ApiController]
[Route("maintenance")]
[Authorize]
[Tags("Maintenance")]
public class MaintenanceController : ControllerBase
{
    private readonly IMaintenanceBusiness _maintenanceBusiness;
    private readonly FileBusiness _fileBusiness;
    private readonly ILogger<MaintenanceController> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MaintenanceController" /> class
    /// </summary>
    /// <param name="maintenanceBusiness"></param>
    /// <param name="fileBusiness"></param>
    /// <param name="logger"></param>
    public MaintenanceController(
        IMaintenanceBusiness maintenanceBusiness,
        FileBusiness fileBusiness,
        ILogger<MaintenanceController> logger)
    {
        _maintenanceBusiness = maintenanceBusiness;
        _fileBusiness = fileBusiness;
        _logger = logger;
    }

    /// <summary>
    ///     Backfill file size properties
    /// </summary>
    /// <remarks>
    ///     For a given org and/or project, backfill the file size property for
    ///     existing files.
    /// </remarks>
    /// <param name="organizationId"></param>
    /// <param name="projectId"></param>
    /// <returns></returns>
    [HttpPut("backfill-file-sizes", Name = "api_backfill_file_sizes")]
    [SysAdmin]
    public async Task<IActionResult> BackfillFileSizes(
        long? organizationId,
        long? projectId)
    {
        try
        {
            await _fileBusiness.BackfillFileSizes(organizationId, projectId);
            return Ok(new { message = $"Backfilled file sizes for org {organizationId}, project {projectId}" });
        }
        catch (Exception ex)
        {
            var message = $"Error while backfilling file sizes: {ex.Message}";
            _logger.LogError(ex, message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Get Timeseries Record IDs
    /// </summary>
    /// <returns>The ids of the records pointing to timeseries duckdb tables</returns>
    [HttpGet("timeseries/records", Name = "api_get_timeseries_record_ids")]
    [SysAdmin]
    public async Task<IActionResult> GetTimeseriesRecordIds()
    {
        try
        {
            var recordIds = await _maintenanceBusiness.GetTimeseriesRecordIds();
            return Ok(recordIds);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while retrieving timeseries record ids: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    /// Export DuckDB Table to File
    /// </summary>
    /// <param name="recordId"></param>
    /// <returns></returns>
    [HttpPut("timeseries/export", Name = "api_export_timeseries_to_file")]
    [SysAdmin]
    public async Task<IActionResult> ExportDuckDbTableToFile([FromQuery] long recordId)
    {
        try
        {
            var successfullyExported = await _maintenanceBusiness.ExportDuckDbTableToFile(recordId);
            return Ok(successfullyExported);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while exporting duckdb table to file: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }
}