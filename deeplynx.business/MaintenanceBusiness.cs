using Azure.Storage.Blobs;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.helpers.exceptions;
using deeplynx.interfaces;
using deeplynx.models;
using DotNetEnv;
using DuckDB.NET.Data;
using Microsoft.EntityFrameworkCore;

namespace deeplynx.business;

public class MaintenanceBusiness : IMaintenanceBusiness
{
    private readonly DeeplynxContext _context;
    private readonly FileAzureBusiness _fileAzureBusiness;
    private readonly IFileBusinessFactory _fileBusinessFactory;
    private readonly IObjectStorageBusiness _objectStorageBusiness;
    private readonly IRecordBusiness _recordBusiness;
    private readonly IDataSourceBusiness _dataSourceBusiness;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MetricsBusiness" /> class.
    /// </summary>
    /// <param name="context">The database context used for database retrieval</param>
    /// <param name="fileBusinessFactory">Factory to create storage-specific file business instances</param>
    /// <param name="objectStorageBusiness">Business layer service used to retrieve and decrypt object storage configuration</param>
    /// <param name="recordBusiness">Business layer service used to bulk create records from scraped files</param>
    /// <param name="dataSourceBusiness">Business layer service used to retrieve the default data source of a project</param>
    public MaintenanceBusiness(
        DeeplynxContext context,
        FileAzureBusiness fileAzureBusiness,
        IFileBusinessFactory fileBusinessFactory,
        IObjectStorageBusiness objectStorageBusiness,
        IRecordBusiness recordBusiness,
        IDataSourceBusiness dataSourceBusiness)
    {
        _context = context;
        _fileAzureBusiness = fileAzureBusiness;
        _fileBusinessFactory = fileBusinessFactory;
        _objectStorageBusiness = objectStorageBusiness;
        _recordBusiness = recordBusiness;
        _dataSourceBusiness = dataSourceBusiness;
    }
    /// <summary>
    /// Gets the records that have been uploaded using our old timeseries methods,
    /// enriched with project and datasource info so callers can group/select before migrating.
    /// </summary>
    /// <returns>List of records needing migration</returns>
    public async Task<List<TimeseriesMigrationRecordDto>> GetTimeseriesMigrationRecords()
    {
        return await _context.Records
            .Include(r => r.Class)
            .Include(r => r.Project)
            .Where(r => r.Class != null && r.Class.Name == "Timeseries"
                                        && r.ObjectStorageId == null
                                        && r.Uri != null
                                        && r.Uri.Contains("duckdb://"))
            .Select(r => new TimeseriesMigrationRecordDto
            {
                RecordId = r.Id,
                Uri = r.Uri!,
                OrganizationId = r.OrganizationId,
                ProjectId = r.ProjectId,
                ProjectName = r.Project.Name,
                DataSourceId = r.DataSourceId
            })
            .ToListAsync();
    }


    /// <summary>
    /// Exports a ducktb file table to a file and changes the record to point to it
    /// </summary>
    /// <param name="recordId">The record that refers to duckdb table</param>
    /// <returns></returns>
    /// <exception cref="NoResultsException"></exception>
    /// <exception cref="NotSupportedException"></exception>
    /// <exception cref="Exception"></exception>
    public async Task<bool> ExportDuckDbTableToFile(long recordId)
    {
        var record = await _context.Records.FirstOrDefaultAsync(r => r.Id == recordId);

        if (record == null)
            throw new NoResultsException($"Record with id {recordId} not found");

        if (record.Uri == null || !record.Uri.Contains("duckdb://"))
            throw new NoResultsException($"Record with id {recordId} does not have a uri that has the table name");

        var instanceDefaultObjectStorage =
            await _context.ObjectStorages
                .Where(os => os.OrganizationId == record.OrganizationId && os.Name == "Instance Default")
                .FirstOrDefaultAsync()
            ?? throw new NoResultsException($"Instance Default object storage for record {recordId} in org {record.OrganizationId} not found");

        var instanceDefaultObjectStorageId = instanceDefaultObjectStorage.Id;

        var organizationId = record.OrganizationId;
        var projectId = record.ProjectId;
        var datasourceId = record.DataSourceId;
        var guid = Guid.NewGuid().ToString();

        Env.Load("../.env");
        var duckDbBasePath = Environment.GetEnvironmentVariable("DUCKDB_BASE_PATH") ?? "/data/duckdb";
        var tableName = record.Uri.Substring("duckdb://".Length);

        var fileExtension = Path.GetExtension(tableName);
        var fileName = tableName.Substring(tableName.IndexOf('_') + 1);

        if (fileExtension != ".csv" && fileExtension != ".parquet")
            throw new NotSupportedException($"Unsupported file extension '{fileExtension}' for record {recordId}. Only .csv and .parquet are supported.");

        var folderPath = Path.Combine(duckDbBasePath, "organization_" + organizationId, "project_" + projectId, "datasource_" + datasourceId);
        var fullFileName = $"{guid}_{fileName}";
        var newFilePath = Path.Combine(folderPath, fullFileName);

        var query = $"SELECT * FROM '{tableName}'";

        var dbPath = Path.Combine(duckDbBasePath, "org_" + organizationId, "project_" + projectId, "datasource_" + datasourceId, "timeseries.duckdb");

        if (!File.Exists(dbPath))
            throw new FileNotFoundException($"DuckDB file not found for record {recordId}: {dbPath}");

        try
        {
            Directory.CreateDirectory(folderPath);

            // Single read-write connection for the whole flow: COPY (export), DROP, then count
            // remaining tables. DuckDB.NET's underlying handles can keep an attachment alive
            // across separate connections in the same process, so mixing read-only + read-write
            // was producing "attached in read-only mode" on the DROP.
            await using var connection = new DuckDBConnection($"Data Source={dbPath}");
            await connection.OpenAsync();

            var copyCommand = connection.CreateCommand();
            if (fileExtension == ".csv")
                copyCommand.CommandText = $"COPY ({query}) TO '{newFilePath}' (HEADER, DELIMITER ',');";
            else if (fileExtension == ".parquet")
                copyCommand.CommandText = $"COPY ({query}) TO '{newFilePath}' (FORMAT parquet);";

            await copyCommand.ExecuteNonQueryAsync();

            record.Uri = newFilePath;
            record.ObjectStorageId = instanceDefaultObjectStorageId;
            record.Description = "";

            await _context.SaveChangesAsync();

            var dropCommand = connection.CreateCommand();
            dropCommand.CommandText = $"DROP TABLE IF EXISTS \"{tableName}\";";
            await dropCommand.ExecuteNonQueryAsync();

            // Check if any tables remain, delete the file if not
            var countCommand = connection.CreateCommand();
            countCommand.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'main';";
            var remainingTableCount = Convert.ToInt64(await countCommand.ExecuteScalarAsync());

            if (remainingTableCount == 0)
            {
                await connection.CloseAsync();
                if (File.Exists(dbPath))
                    File.Delete(dbPath);
            }

            return true;
        }
        catch (Exception ex)
        {
            if (File.Exists(newFilePath))
                File.Delete(newFilePath);

            throw new Exception($"Failed to export record {recordId} to file: {ex.Message}", ex);
        }
    }

    /// <summary>
    ///     Scrapes files from a given object storage and creates records for them. 
    /// </summary>
    /// <param name="objectStorageId">The ID of the object storage to be scraped</param>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="afterCursor">Cursor returned from a previous call, or null to start from the beginning</param>
    /// <param name="batchSize">Number of records per upsert batch</param>
    /// <param name="maxBatches">Maximum number of batches to process before returning</param>
    /// <param name="sensitivityLabelIds">The IDs of the labels to attach</param>
    /// <param name="isSysAdmin">Optional param determining if the requesting user is a system admin</param>
    /// <param name="isOrgAdmin">Optional param determining if the requesting user is an organization admin</param>
    /// <param name="isProjectAdmin">Optional param determining if the requesting user is a project admin</param>
    /// <param name="cancellationToken">Token checked during the scrape; canceling stops early with whatever was processed so far still committed</param>
    /// <returns>Number of records processed this call, plus a cursor for the next call (null if complete)</returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="KeyNotFoundException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    /// <exception cref="NotSupportedException"></exception>
    public async Task<ScrapeObjectStorageResponseDto> ScrapeObjectStorageToCatalog(
        long objectStorageId,
        long currentUserId,
        string? afterCursor = null,
        int batchSize = 500,
        int maxBatches = 5,
        List<long>? sensitivityLabelIds = null,
        bool isSysAdmin = false,
        bool isOrgAdmin = false,
        bool isProjectAdmin = false,
        CancellationToken cancellationToken = default)
    {
        ValidateScraperParameters(batchSize, maxBatches);
        sensitivityLabelIds = NormalizeAndValidateSensitivityLabelIds(sensitivityLabelIds);

        cancellationToken.ThrowIfCancellationRequested();

        // Retrieve the storage and translate a missing result into an expected 404.
        var objectStorage =
            await _objectStorageBusiness.GetDecryptedObjectStorage(
                objectStorageId);

        if (objectStorage == null)
        {
            throw new KeyNotFoundException(
                $"Object storage {objectStorageId} was not found.");
        }

        if (!objectStorage.OrganizationId.HasValue)
        {
            throw new InvalidOperationException(
                $"Object storage {objectStorageId} does not have an organization.");
        }

        if (!objectStorage.ProjectId.HasValue)
        {
            throw new InvalidOperationException(
                $"Object storage {objectStorageId} is not assigned to a project.");
        }

        long organizationId = objectStorage.OrganizationId.Value;
        long projectId = objectStorage.ProjectId.Value;

        DataSourceResponseDto dataSourceResponse = await _dataSourceBusiness.GetDefaultDataSource(organizationId, projectId);
        long dataSourceId = dataSourceResponse.Id;

        // Validate every requested label before BulkCreateRecords reaches the FK.
        if (sensitivityLabelIds is { Count: > 0 })
        {
            var existingLabelIds =
                await _context.SensitivityLabels
                    .Where(label =>
                        sensitivityLabelIds.Contains(label.Id) &&
                        !label.IsArchived &&
                        label.OrganizationId == organizationId &&
                        (
                            label.ProjectId == projectId ||
                            label.ProjectId == null
                        ))
                    .Select(label => label.Id)
                    .ToListAsync(cancellationToken);

            var missingLabelIds = sensitivityLabelIds
                .Except(existingLabelIds)
                .OrderBy(id => id)
                .ToList();

            if (missingLabelIds.Count > 0)
            {
                throw new KeyNotFoundException(
                    $"Sensitivity label IDs were not found for project " +
                    $"{projectId}: {string.Join(", ", missingLabelIds)}");
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        var fileBusiness = _fileBusinessFactory.CreateFileBusiness(objectStorage.Type);

        ScrapeResult scrapeResult = await fileBusiness.ScrapeAsync(
            objectStorage,
            afterCursor,
            batchSize,
            maxBatches,
            cancellationToken);

        if (scrapeResult.Records.Count == 0)
        {
            return new ScrapeObjectStorageResponseDto
            {
                Processed = 0,
                NextCursor = scrapeResult.NextCursor
            };
        }

        cancellationToken.ThrowIfCancellationRequested();

        var recordResponseDtos =
            await _recordBusiness.BulkCreateRecords(
                currentUserId,
                organizationId,
                projectId,
                dataSourceId,
                scrapeResult.Records,
                sensitivityLabelIds,
                isSysAdmin,
                isOrgAdmin,
                isProjectAdmin);

        return new ScrapeObjectStorageResponseDto
        {
            Processed = recordResponseDtos.Count,
            NextCursor = scrapeResult.NextCursor
        };
    }

    private static void ValidateScraperParameters(
        int batchSize,
        int maxBatches)
    {

        if (batchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(batchSize),
                batchSize,
                "Batch size must be greater than zero.");
        }

        if (maxBatches <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxBatches),
                maxBatches,
                "Maximum batches must be greater than zero.");
        }
    }

    private static List<long>? NormalizeAndValidateSensitivityLabelIds(
        List<long>? sensitivityLabelIds)
    {
        sensitivityLabelIds = sensitivityLabelIds?
            .Distinct()
            .ToList();

        if (sensitivityLabelIds?.Any(id => id <= 0) == true)
        {
            var invalidIds = sensitivityLabelIds.Where(id => id <= 0);

            throw new ArgumentException(
                $"Sensitivity label IDs must be greater than zero. " +
                $"Invalid IDs: {string.Join(", ", invalidIds)}",
                nameof(sensitivityLabelIds));
        }

        return sensitivityLabelIds;
    }

}