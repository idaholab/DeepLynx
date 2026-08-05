using System.Runtime.InteropServices;
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
    private readonly IObjectStorageBusiness _objectStorageBusiness;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MetricsBusiness" /> class.
    /// </summary>
    /// <param name="context">The database context used for database retrieval</param>
    /// <param name="fileBusinessFactory">Factory to create storage-specific file business instances</param>
    public MaintenanceBusiness(
        DeeplynxContext context,
        FileAzureBusiness fileAzureBusiness,
        IObjectStorageBusiness objectStorageBusiness)
    {
        _context = context;
        _fileAzureBusiness = fileAzureBusiness;
        _objectStorageBusiness = objectStorageBusiness;
    }

    /// <summary>
    /// Copies regular file-backed records from a mounted filesystem object storage to Azure Blob Storage.
    /// Source files are never deleted. Each record is updated only after its destination SHA-256 is verified.
    /// Directory/appended records are intentionally skipped by this minimal migration.
    /// </summary>
    public async Task<FileStorageMigrationResponseDto> MigrateFilesystemRecordsToAzure(
        FileStorageMigrationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ValidationHelper.ValidateModel(request);

        if (request.SourceObjectStorageId == request.TargetObjectStorageId)
            throw new ArgumentException("Source and target object storage IDs must be different.");

        var sourceStorage = await _objectStorageBusiness.GetDecryptedObjectStorage(request.SourceObjectStorageId);
        var targetStorage = await _objectStorageBusiness.GetDecryptedObjectStorage(request.TargetObjectStorageId);

        if (sourceStorage.Type != "filesystem")
            throw new ArgumentException("Source object storage must have type 'filesystem'.");
        if (targetStorage.Type != "azure_object")
            throw new ArgumentException("Target object storage must have type 'azure_object'.");
        if (sourceStorage.OrganizationId != request.OrganizationId || targetStorage.OrganizationId != request.OrganizationId)
            throw new ArgumentException("Source and target object storages must belong to the requested organization.");
        if (string.IsNullOrWhiteSpace(sourceStorage.Config.MountPath))
            throw new InvalidOperationException("Source filesystem mount path is missing.");
        if (targetStorage.Config.AzureObjectConfig == null)
            throw new InvalidOperationException("Target Azure object storage configuration is missing.");

        var azureConfig = targetStorage.Config.AzureObjectConfig;
        if (string.IsNullOrWhiteSpace(azureConfig.AzureConnectionString) ||
            string.IsNullOrWhiteSpace(azureConfig.AzureContainerName))
            throw new InvalidOperationException("Target Azure connection string or container name is missing.");

        var container = new BlobContainerClient(azureConfig.AzureConnectionString, azureConfig.AzureContainerName);
        if (!request.DryRun)
            await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var query = _context.Records
            .AsNoTracking()
            .Where(r => r.OrganizationId == request.OrganizationId &&
                        r.ObjectStorageId == request.SourceObjectStorageId &&
                        r.Uri != null);

        if (request.RecordId.HasValue)
            query = query.Where(r => r.Id == request.RecordId.Value);
        else if (request.AfterRecordId.HasValue)
            query = query.Where(r => r.Id > request.AfterRecordId.Value);

        var records = await query
            .OrderBy(r => r.Id)
            .Take(request.BatchSize)
            .ToListAsync(cancellationToken);

        var response = new FileStorageMigrationResponseDto
        {
            DryRun = request.DryRun,
            Scanned = records.Count,
            LastRecordId = records.Count == 0 ? request.AfterRecordId : records[^1].Id
        };

        var normalizedMount = Path.GetFullPath(sourceStorage.Config.MountPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        foreach (var record in records)
        {
            var item = new FileStorageMigrationItemDto
            {
                RecordId = record.Id,
                SourceUri = record.Uri
            };
            response.Items.Add(item);

            try
            {
                var sourcePath = Path.GetFullPath(record.Uri!);
                EnsurePathIsInsideMount(sourcePath, normalizedMount);

                if (Directory.Exists(sourcePath))
                    throw new NotSupportedException("Directory/appended records require the follow-up prefix migration.");
                if (!File.Exists(sourcePath))
                    throw new FileNotFoundException("The source file does not exist. Source Path: " + sourcePath);

                var fileName = Path.GetFileName(sourcePath);
                if (string.IsNullOrWhiteSpace(fileName))
                    throw new InvalidOperationException("Could not determine the source file name.");

                var destinationUri =
                    $"organization_{record.OrganizationId}/project_{record.ProjectId}/datasource_{record.DataSourceId}/{fileName}";
                item.DestinationUri = destinationUri;

                if (request.DryRun)
                {
                    item.Status = "ready";
                    continue;
                }

                string sourceHash;
                await using (var hashStream = new FileStream(
                                 sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                                 bufferSize: 1024 * 1024, useAsync: true))
                {
                    sourceHash = await Sha256HashHelper.ComputeHexAsync(hashStream, cancellationToken);
                }

                var blob = container.GetBlobClient(destinationUri);
                await using (var uploadStream = new FileStream(
                                 sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                                 bufferSize: 1024 * 1024, useAsync: true))
                {
                    await blob.UploadAsync(uploadStream, overwrite: true, cancellationToken: cancellationToken);
                }

                var blobProperties = await blob.GetPropertiesAsync(cancellationToken: cancellationToken);
                var sourceLength = new FileInfo(sourcePath).Length;
                if (blobProperties.Value.ContentLength != sourceLength)
                    throw new InvalidDataException("Destination content length does not match the source file.");

                var download = await blob.DownloadStreamingAsync(cancellationToken: cancellationToken);
                await using var destinationStream = download.Value.Content;
                var destinationHash = await Sha256HashHelper.ComputeHexAsync(destinationStream, cancellationToken);
                if (!string.Equals(sourceHash, destinationHash, StringComparison.Ordinal))
                    throw new InvalidDataException("Destination SHA-256 does not match the source file.");

                // Optimistic predicate: do not overwrite a record changed while its file was being copied.
                var updated = await _context.Records
                    .Where(r => r.Id == record.Id &&
                                r.ObjectStorageId == request.SourceObjectStorageId &&
                                r.Uri == record.Uri)
                    .ExecuteUpdateAsync(setters => setters
                            .SetProperty(r => r.Uri, destinationUri)
                            .SetProperty(r => r.ObjectStorageId, request.TargetObjectStorageId)
                            .SetProperty(r => r.FileContentHash, sourceHash),
                        cancellationToken);

                if (updated != 1)
                    throw new InvalidOperationException("Record changed during migration; its database locator was not updated.");

                item.Status = "migrated";
                response.Migrated++;
            }
            catch (Exception ex)
            {
                item.Status = "failed";
                item.Error = ex.Message;
                response.Failed++;
            }
        }

        return response;
    }

    private static void EnsurePathIsInsideMount(string sourcePath, string normalizedMount)
    {
        var mountPrefix = normalizedMount + Path.DirectorySeparatorChar;

        var comparison = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!sourcePath.Equals(normalizedMount, comparison) &&
            !sourcePath.StartsWith(mountPrefix, comparison))
        {
            throw new InvalidOperationException("Record URI is outside the configured filesystem mount path.");
        }
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


}
