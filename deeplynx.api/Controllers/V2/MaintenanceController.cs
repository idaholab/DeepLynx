using Asp.Versioning;
using deeplynx.business;
using deeplynx.helpers;
using deeplynx.interfaces;
using deeplynx.models;
using deeplynx.models.ResponseDTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;

namespace deeplynx.api.Controllers.V2;

/// <summary>
///     Sysadmin controller for executing maintenance operations.
/// </summary>
/// <remarks>
///     This mostly entails one-time jobs that cannot be resolved via EF migration.
/// </remarks>
[ApiController]
[ApiVersion(2)]
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
    /// <returns>Backfill file sizes response</returns>
    [HttpPut("backfill-file-sizes", Name = "api_backfill_file_sizes")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [SysAdmin]
    public async Task<ActionResult<BackfillFileSizesResponseDto>> BackfillFileSizes(
        long? organizationId,
        long? projectId,
        long? afterRecordId = null,
        int batchSize = 500,
        int maxBatches = 5)
    {
        var result = await _fileBusiness.BackfillFileSizes(
            organizationId,
            projectId,
            afterRecordId,
            batchSize,
            maxBatches);
        return Ok(result);
    }


    
    /// <summary>
    ///     Get Timeseries Migration Record
    /// </summary>
    /// <returns>Timeseries migration record response dto</returns>
    [HttpGet("timeseries/records", Name = "api_get_timeseries_record_ids")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [SysAdmin]
    public async Task<ActionResult<TimeseriesMigrationRecordDto>> GetTimeseriesMigrationRecords()
    {
        var records = await _maintenanceBusiness.GetTimeseriesMigrationRecords();
        return Ok(records);
    }


    
    /// <summary>
    /// Export DuckDB Table to File
    /// </summary>
    /// <param name="recordId"></param>
    /// <returns></returns>
    [HttpPut("timeseries/export", Name = "api_export_timeseries_to_file")]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [SysAdmin]
    public async Task<ActionResult<bool>> ExportDuckDbTableToFile([FromQuery] long recordId)
    {
        var successfullyExported = await _maintenanceBusiness.ExportDuckDbTableToFile(recordId);
        return Ok(successfullyExported);
    }
}