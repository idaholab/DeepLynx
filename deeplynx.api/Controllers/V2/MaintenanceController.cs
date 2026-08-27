using Asp.Versioning;
using deeplynx.business;
using deeplynx.helpers;
using deeplynx.helpers.Context;
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

    /// <summary>
    ///     Scrape Object Storage To Catalog
    /// </summary>
    /// <remarks>
    ///     Scrapes every file in the given object storage and creates a catalog record for each one.
    /// </remarks>
    /// <param name="objectStorageId">The ID of the object storage to be scraped.</param>
    /// <param name="afterCursor">Cursor returned from a previous call, or omitted to start from the beginning.</param>
    /// <param name="batchSize">Number of records per upsert batch.</param>
    /// <param name="maxBatches">Maximum number of batches to process before returning.</param>
    /// <param name="sensitivityLabelIds">Optional IDs of sensitivity labels to attach to each created record.</param>
    /// <returns>Number of records processed this call, plus a cursor for the next call (null if complete).</returns>
    [HttpPost("object-storages/{objectStorageId:long}/scrape", Name = "api_scrape_object_storage_to_catalog")]
    [MapToApiVersion(2)]
    [Badge("V2", BadgePosition.Before, "#72e6a1")]
    [SysAdmin]
    public async Task<IActionResult> ScrapeObjectStorageToCatalog(
        long objectStorageId,
        [FromQuery] string? afterCursor = null,
        [FromQuery] int batchSize = 500,
        [FromQuery] int maxBatches = 5,
        [FromQuery] List<long>? sensitivityLabelIds = null)
    {
        long currentUserId = UserContextStorage.UserId;
        bool isSysAdmin = UserContextStorage.IsSysAdmin;
        bool isOrgAdmin = UserContextStorage.IsOrgAdmin;
        bool isProjectAdmin = UserContextStorage.IsProjectAdmin;

        var result = await _maintenanceBusiness.ScrapeObjectStorageToCatalog(
                objectStorageId,
                currentUserId,
                afterCursor,
                batchSize,
                maxBatches,
                sensitivityLabelIds,
                isSysAdmin,
                isOrgAdmin,
                isProjectAdmin,
                HttpContext.RequestAborted);

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
}