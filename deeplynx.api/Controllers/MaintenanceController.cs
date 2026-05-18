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
    ///     Get Timeseries Migration Record
    /// </summary>
    /// <returns>The total number of bytes of file data stored in Nexus-registered object storages.</returns>
    [HttpGet("timeseries/records", Name = "api_get_timeseries_record_ids")]
    [SysAdmin]
    public async Task<IActionResult> GetTimeseriesMigrationRecords()
    {
        try
        {
            var records = await _maintenanceBusiness.GetTimeseriesMigrationRecords();
            return Ok(records);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while retrieving timeseries migration records: {exc}";
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