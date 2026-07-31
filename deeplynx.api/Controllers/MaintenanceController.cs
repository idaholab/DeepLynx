using Asp.Versioning;
using deeplynx.business;
using deeplynx.helpers;
using deeplynx.interfaces;
using deeplynx.models;
using deeplynx.models.ResponseDTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;

namespace deeplynx.api.Controllers;

/// <summary>
///     Sysadmin controller for executing maintenance operations.
/// </summary>
/// <remarks>
///     This mostly entails one-time jobs that cannot be resolved via EF migration.
/// </remarks>
[ApiController]
[ApiVersion(1)]
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
    /// <returns></returns>
    [HttpPut("backfill-file-sizes", Name = "api_backfill_file_sizes")]
    [MapToApiVersion(1)]
    [SysAdmin]
    public async Task<IActionResult> BackfillFileSizes(
        long? organizationId,
        long? projectId,
        long? afterRecordId = null,
        int batchSize = 500,
        int maxBatches = 5)
    {
        try
        {
            var result = await _fileBusiness.BackfillFileSizes(
                organizationId,
                projectId,
                afterRecordId,
                batchSize,
                maxBatches);
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            var message = $"Error while backfilling file sizes: {ex.Message}";
            _logger.LogError(ex, message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
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
    [MapToApiVersion(2)]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [SysAdmin]
    public async Task<ActionResult<BackfillFileSizesResponseDto>> BackfillFileSizesV2(
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
    /// Copy regular file records from mounted filesystem storage to Azure Blob Storage.
    /// </summary>
    /// <remarks>
    /// Dry-run is enabled by default. Source files are retained and records are changed only
    /// after the uploaded blob passes length and SHA-256 verification.
    /// </remarks>
    [HttpPost("files/migrate-to-azure", Name = "api_migrate_filesystem_records_to_azure")]
    [MapToApiVersion(2)]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [SysAdmin]
    public async Task<ActionResult<FileStorageMigrationResponseDto>> MigrateFilesystemRecordsToAzure(
        [FromBody] FileStorageMigrationRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _maintenanceBusiness.MigrateFilesystemRecordsToAzure(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    ///     Get Timeseries Migration Record
    /// </summary>
    /// <returns>The total number of bytes of file data stored in Nexus-registered object storages.</returns>
    [HttpGet("timeseries/records", Name = "api_get_timeseries_record_ids")]
    [MapToApiVersion(1)]
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
    ///     Get Timeseries Migration Record
    /// </summary>
    /// <returns>Timeseries migration record response dto</returns>
    [HttpGet("timeseries/records", Name = "api_get_timeseries_record_ids")]
    [MapToApiVersion(2)]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [SysAdmin]
    public async Task<ActionResult<TimeseriesMigrationRecordDto>> GetTimeseriesMigrationRecordsV2()
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
    [MapToApiVersion(1)]
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
    
    /// <summary>
    /// Export DuckDB Table to File
    /// </summary>
    /// <param name="recordId"></param>
    /// <returns></returns>
    [HttpPut("timeseries/export", Name = "api_export_timeseries_to_file")]
    [MapToApiVersion(2)]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [SysAdmin]
    public async Task<ActionResult<bool>> ExportDuckDbTableToFileV2([FromQuery] long recordId)
    {
        var successfullyExported = await _maintenanceBusiness.ExportDuckDbTableToFile(recordId);
        return Ok(successfullyExported);
    }
}
