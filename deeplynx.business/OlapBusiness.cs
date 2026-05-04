using System.Data.Common;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Azure.Storage.Blobs;
using deeplynx.datalayer.Models;
using deeplynx.interfaces;
using deeplynx.models;
using DuckDB.NET.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Parquet;

namespace deeplynx.business;

public class OlapBusiness : IOlapBusiness
{
    private readonly DeeplynxContext _context;
    private readonly IRecordBusiness _recordBusiness;
    private readonly ILogger<OlapBusiness> _logger;
    private readonly IObjectStorageBusiness _objectStorageBusiness;
    
    public OlapBusiness(
        DeeplynxContext context, 
        IRecordBusiness recordBusiness,
        IObjectStorageBusiness objectStorageBusiness,
        ILogger<OlapBusiness> logger)
    {
        _recordBusiness = recordBusiness;
        _context = context;
        _logger = logger;
        _objectStorageBusiness = objectStorageBusiness;
    }

    /// <summary>
    ///     Appends a new Parquet part file to an existing Parquet dataset in Azure Blob Storage
    ///     or the local filesystem, validating schema compatibility before writing.
    ///     On the first append, migrates the original flat file into a folder-based part structure.
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="projectId"></param>
    /// <param name="recordId"></param>
    /// <param name="partNumber"></param>
    /// <param name="file"></param>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task AppendTabularBlob(
        long organizationId,
        long projectId,
        long recordId,
        long partNumber,
        IFormFile file)
    {
        var fileType = Path.GetExtension(file.FileName).TrimStart('.').ToLower();
        if (fileType != "parquet")
            throw new ArgumentException("Only Parquet files are supported for append.");

        if (file.Length == 0)
            throw new ArgumentException("Cannot append an empty file.");

        // Part 0 is always reserved — block it unconditionally, not only on first append.
        if (partNumber == 0)
            throw new ArgumentException("Part number 0 is reserved for the original file. Start at 1.");

        var record = await _context.Records.FirstOrDefaultAsync(r =>
            r.OrganizationId == organizationId &&
            r.ProjectId == projectId &&
            r.Id == recordId);

        if (record == null)
            throw new ArgumentException($"Record with ID {recordId} does not exist.");

        if (record.FileType == null || record.FileType.ToLower() != fileType)
            throw new ArgumentException($"File types differ: {fileType}/{record.FileType}");

        if (string.IsNullOrWhiteSpace(record.Uri))
            throw new ArgumentException("Record has no URI.");

        var objectStorage = _context.ObjectStorages.FirstOrDefault(os =>
            os.OrganizationId == organizationId &&
            (os.ProjectId == projectId || os.ProjectId == null) &&
            os.Id == record.ObjectStorageId);

        if (objectStorage == null)
            throw new ArgumentException($"Object storage not found for project {projectId}.");

        if (objectStorage.Type != "azure_object" && objectStorage.Type != "filesystem")
            throw new InvalidOperationException($"Unsupported object storage type: {objectStorage.Type}");

        var objectStorageConfig = JsonConvert.DeserializeObject<ObjectStorageConfigDto>(objectStorage.Config)
                                  ?? throw new InvalidOperationException("Object storage config is null or invalid.");

        if (objectStorage.Type == "azure_object")
            await AppendToAzureBlob(record, objectStorageConfig, file, partNumber);
        else
            await AppendToFilesystemAsync(record, file, partNumber);
    }

    /// <summary>
    ///     Generic select all for given table
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method</param>
    /// <param name="projectId">The project ID</param>
    /// <param name="organizationId">The organization ID</param>
    /// <param name="dataSourceId">The data source ID</param>
    /// <param name="tableName">The table to export</param>
    /// <param name="fileType">The type of file to convert query to</param>
    /// <returns>All data for given table</returns>
    // public async Task<RecordResponseDto> ExportTimeseriesTable(long currentUserId, long organizationId, long projectId,
    //     long dataSourceId,
    //     string tableName, string fileType)
    // {
    // await ExistenceHelper.EnsureDataSourceExistsForProjectAsync(_context, dataSourceId, projectId);
    // var request = new TimeseriesQueryRequestDto
    // {
    //     Query = $"SELECT * FROM '{tableName}'"
    // };
    //
    // var queryId = Guid.NewGuid().ToString();
    // string fileName;
    //
    // if (fileType == "csv")
    //     fileName = queryId + "_record.csv";
    // else if (fileType == "parquet")
    //     fileName = queryId + "_record.parquet";
    // else
    //     throw new NotSupportedException($"file type {fileType} not supported");
    //
    // var reportClass = await _classBusiness.GetOrCreateClass(
    //     currentUserId, organizationId, projectId, "Report");
    // var timeseriesObjectStorageMethod =
    //     await _context.ObjectStorages.FirstOrDefaultAsync(os =>
    //         os.ProjectId == projectId && os.Name == "Timeseries Default");
    // if (timeseriesObjectStorageMethod == null)
    //     throw new KeyNotFoundException("Default timeseries object storage method not found");
    //
    // var recordRequest = new CreateRecordRequestDto
    // {
    //     Properties = new JsonObject
    //     {
    //         ["status"] = Status.InProgress,
    //         ["query"] = request.Query
    //     },
    //     Name = fileName,
    //     Description = $"Timeseries result report for {fileName}",
    //     OriginalId = queryId,
    //     ClassId = reportClass.Id,
    //     ClassName = reportClass.Name,
    //     ObjectStorageId = timeseriesObjectStorageMethod.Id,
    //     FileType = fileType
    // };
    //
    // var recordResponse =
    //     await _recordBusiness.CreateRecord(currentUserId, organizationId, projectId, dataSourceId, recordRequest);
    //
    // // meant to run in background so don't await!
    // RunBackgroundJob(recordResponse, request.Query, organizationId, projectId, dataSourceId, fileName, fileType);
    //     return new RecordResponseDto();
    // }


    /// <summary>
    ///     Queries single tabular files and across multiple files within the same folder
    /// </summary>
    /// <param name="currentUserId"></param>
    /// <param name="organizationId"></param>
    /// <param name="projectId"></param>
    /// <param name="recordId"></param>
    /// <param name="userQuery"></param>
    /// <param name="viewName"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task<PlotDataDto> QueryTabularFile(
        long currentUserId,
        long organizationId,
        long projectId,
        long recordId,
        string userQuery,
        string viewName)
    {
        if (string.IsNullOrWhiteSpace(viewName))
            throw new ArgumentException("View name is required", nameof(viewName));
        if (string.IsNullOrWhiteSpace(userQuery))
            throw new ArgumentException("Query is required, must not be empty or missing");

        // Check view is referenced
        if (!userQuery.Contains(viewName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Query must reference the view '{viewName}'");

        // Block file access patterns only
        var dangerousPatterns = new[]
        {
            "COPY", // Prevent writing files
            "EXPORT", // Prevent exporting database/data
            "IMPORT", // Prevent importing
            "INSERT", // Prevent inserting to other tables
            "UPDATE", // Prevent updates
            "DELETE", // Prevent deletes  
            "DROP", // Prevent dropping objects
            "ALTER", // Prevent schema changes
            "CREATE TABLE", // Prevent creating tables
            "CREATE VIEW", // Prevent creating views (besides temp view we control)
            "az://", // Azure blob paths
            "read_parquet(", // File reading functions
            "read_csv(",
            "read_json(",
            "ATTACH", // Database attachment
            "CREATE SECRET", // Secret manipulation
            ".parquet'", // File extensions in quotes
            ".csv'",
            ".json'"
        };

        foreach (var pattern in dangerousPatterns)
            if (userQuery.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Query contains unauthorized pattern: {pattern}");

        // Block multi-statement queries
        if (userQuery.Count(c => c == ';') > 0)
            throw new InvalidOperationException("Multi-statement queries are not allowed");

        var record = await _recordBusiness.GetRecord(currentUserId, organizationId, projectId, recordId, true);

        if (string.IsNullOrWhiteSpace(record.Uri))
            throw new ArgumentException("File path is required", nameof(record.Uri));

        var objectStorage = _context.ObjectStorages.FirstOrDefault(os => os.OrganizationId == organizationId &&
                                                                         (os.ProjectId == projectId ||
                                                                          os.ProjectId == null) &&
                                                                         os.Id == record.ObjectStorageId);

        if (objectStorage == null)
            throw new ArgumentException(
                $"Object storage with ID {record.ObjectStorageId} does not exist for project with ID of {projectId}");

        var objectStorageConfig = JsonConvert.DeserializeObject<ObjectStorageConfigDto>(objectStorage.Config);
        if (objectStorageConfig == null)
            throw new InvalidOperationException("Config data for object storage is null or invalid");

        // Sanitize viewName - only allow alphanumeric and underscore
        if (!Regex.IsMatch(viewName, @"^[a-zA-Z_][a-zA-Z0-9_]*$"))
            throw new ArgumentException("View name contains invalid characters", nameof(viewName));

        // Determine storage type and get appropriate connection
        DuckDBConnection connection;
        string fileUrl;
        bool isFolder;

        if (objectStorage.Type == "azure_object")
        {
            // Azure Blob Storage
            var containerName = objectStorageConfig.AzureObjectConfig?.AzureContainerName;
            if (string.IsNullOrWhiteSpace(containerName))
                throw new ArgumentException("Container name is required for Azure storage");

            connection = await GetAzureDuckDbConnection(objectStorageConfig);

            var escapedContainer = containerName.Replace("'", "''");
            var escapedPath = record.Uri.Replace("'", "''");
            fileUrl = $"az://{escapedContainer}/{escapedPath}";

            // Azure append-blob folders always end with '/'
            isFolder = record.Uri.EndsWith("/", StringComparison.Ordinal);
        }
        else if (objectStorage.Type == "filesystem")
        {
            // Local filesystem
            connection = await GetLocalDuckDbConnection();

            var escapedPath = record.Uri.Replace("'", "''");
            fileUrl = escapedPath;

            // A folder URI ends with a separator, or points to an existing directory
            isFolder = record.Uri.EndsWith(Path.DirectorySeparatorChar)
                       || record.Uri.EndsWith('/')
                       || Directory.Exists(record.Uri);
        }
        else
        {
            throw new InvalidOperationException("Object storage type is not supported for timeseries file queries");
        }

        // Build the SQL expression that backs the view.
        // For a folder (appended-blob dataset) we glob all part files and union by name so
        // the schema is resolved across every part even if columns were added over time.
        // For a single file we keep the simple quoted-path form.
        var viewSourceSql = isFolder
            ? $"SELECT * EXCLUDE filename FROM read_parquet(['{fileUrl.TrimEnd('/')}/*.parquet'], union_by_name = true, filename = true) ORDER BY CAST(regexp_extract(filename, '(\\d+)\\.parquet$', 1) AS BIGINT)"
            : $"SELECT * FROM '{fileUrl}'";

        await using (connection)
        {
            // Create a temporary view pointing to the file or glob dataset
            await using (var createViewCmd = connection.CreateCommand())
            {
                createViewCmd.CommandText = $"CREATE OR REPLACE TEMP VIEW {viewName} AS {viewSourceSql};";
                await createViewCmd.ExecuteNonQueryAsync();
            }

            // Execute the user query and read results directly
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = userQuery;

            await using var reader = await cmd.ExecuteReaderAsync();

            return await ReaderToPlotData(reader);
        }
    }

    /// <summary>
    ///     Returns the highest numeric part number that currently exists
    ///     in the append folder for the given record, or <c>0</c> if the
    ///     record has not been appended to yet (its URI still points to a
    ///     flat <c>.parquet</c> file rather than a folder).
    ///     <para>
    ///         Part 0 is the original file as migrated by the first call
    ///         to <see cref="AppendTabularBlob" />, so <c>0</c> reliably
    ///         means "no appends have occurred yet" without ambiguity.
    ///     </para>
    ///     <para>
    ///         Only <c>.parquet</c> files whose stem can be parsed as a
    ///         non-negative integer are considered.  Any other files in
    ///         the folder are silently ignored so that stray files never
    ///         corrupt the result.
    ///     </para>
    /// </summary>
    /// <param name="organizationId">ID of the owning organization.</param>
    /// <param name="projectId">ID of the owning project.</param>
    /// <param name="recordId">ID of the record whose folder to inspect.</param>
    /// <returns>
    ///     The highest part number found in the folder, or <c>0</c> when
    ///     the record is still a flat file.  The next safe part number to
    ///     pass to <see cref="AppendTabularBlob" /> is always
    ///     <c>GetHighestPartNumber(...) + 1</c> with no special casing.
    /// </returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when the record does not exist, has no URI, or its
    ///     object storage cannot be found.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the object storage type is not supported, or when
    ///     the folder exists but contains no recognisable part files.
    /// </exception>
    public async Task<long> GetHighestPartNumber(
        long organizationId,
        long projectId,
        long recordId)
    {
        var record = await _context.Records.FirstOrDefaultAsync(r =>
            r.OrganizationId == organizationId &&
            r.ProjectId == projectId &&
            r.Id == recordId);

        if (record == null)
            throw new ArgumentException($"Record with ID {recordId} does not exist.");

        if (string.IsNullOrWhiteSpace(record.Uri))
            throw new ArgumentException("Record has no URI.");

        var extension = Path.GetExtension(record.Uri);

        if (!string.IsNullOrEmpty(extension))
        {
            if (!extension.Equals(".parquet", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(
                    $"Only Parquet files are supported for part number tracking. Record URI has extension '{extension}'.");

            // Flat file — no appends have been made yet. Return 0 because the original
            // file is conceptually part 0 (the value AppendTabularBlob uses when it
            // migrates it), so highest + 1 = 1 is always the correct next part.
            return 0;
        }

        var objectStorage = _context.ObjectStorages.FirstOrDefault(os =>
            os.OrganizationId == organizationId &&
            (os.ProjectId == projectId || os.ProjectId == null) &&
            os.Id == record.ObjectStorageId);

        if (objectStorage == null)
            throw new ArgumentException($"Object storage not found for project {projectId}.");

        var objectStorageConfig = JsonConvert.DeserializeObject<ObjectStorageConfigDto>(objectStorage.Config)
                                  ?? throw new InvalidOperationException("Object storage config is null or invalid.");

        IEnumerable<long> partNumbers;

        if (objectStorage.Type == "azure_object")
        {
            var containerName = objectStorageConfig.AzureObjectConfig?.AzureContainerName
                                ?? throw new ArgumentException("Azure container name is required.");

            var containerClient = new BlobServiceClient(objectStorageConfig.AzureObjectConfig!.AzureConnectionString)
                .GetBlobContainerClient(containerName);

            partNumbers = containerClient
                .GetBlobs(prefix: record.Uri)
                .Where(b => b.Name.EndsWith(".parquet", StringComparison.OrdinalIgnoreCase))
                .Select(b => Path.GetFileNameWithoutExtension(b.Name))
                .Where(stem => long.TryParse(stem, out _))
                .Select(long.Parse);
        }
        else if (objectStorage.Type == "filesystem")
        {
            partNumbers = Directory
                .EnumerateFiles(record.Uri, "*.parquet")
                .Select(p => Path.GetFileNameWithoutExtension(p))
                .Where(stem => long.TryParse(stem, out _))
                .Select(long.Parse);
        }
        else
        {
            throw new InvalidOperationException($"Unsupported object storage type: {objectStorage.Type}");
        }

        var parts = partNumbers.ToList();

        if (parts.Count == 0)
            throw new InvalidOperationException(
                "No numeric part files found in the dataset folder.");

        return parts.Max();
    }

    /// <summary>
    ///     Extracts column names and types from a tabular file (CSV or Parquet) stored in object storage.
    ///     For Parquet files, reads only the file footer via Parquet.Net — no row data is loaded into memory.
    ///     For CSV files, uses DuckDB to infer column types from the file content.
    /// </summary>
    /// <param name="objectStorageType">Type of object storage entity</param>
    /// <param name="objectStorageConfig">Object storage configuration</param>
    /// <param name="fileUri">URI/path to the file</param>
    /// <returns>JsonArray of { name, type } objects, or null if extraction fails</returns>
    public async Task<JsonArray?> ExtractTabularColumns(
        string objectStorageType,
        ObjectStorageConfigDto objectStorageConfig,
        string fileUri)
    {
        try
        {
            var isParquet = fileUri.EndsWith(".parquet", StringComparison.OrdinalIgnoreCase);

            if (isParquet)
            {
                // Use Parquet.Net to read only the file footer — no row data loaded regardless of file size
                Stream parquetStream;

                if (objectStorageType == "azure_object")
                {
                    var containerName = objectStorageConfig.AzureObjectConfig?.AzureContainerName;
                    if (string.IsNullOrWhiteSpace(containerName))
                        return null;

                    var containerClient =
                        new BlobServiceClient(objectStorageConfig.AzureObjectConfig!.AzureConnectionString)
                            .GetBlobContainerClient(containerName);

                    parquetStream = await containerClient
                        .GetBlobClient(fileUri)
                        .OpenReadAsync();
                }
                else if (objectStorageType == "filesystem")
                {
                    parquetStream = File.OpenRead(fileUri);
                }
                else
                {
                    _logger.LogDebug("Unsupported object storage type for Parquet column extraction");
                    return null;
                }

                await using (parquetStream)
                {
                    using var reader = await ParquetReader.CreateAsync(parquetStream);
                    return new JsonArray(reader.Schema.GetDataFields()
                        .Select(f => new JsonObject
                        {
                            ["name"] = f.Name,
                            ["type"] = f.ClrType.Name
                        })
                        .ToArray<JsonNode?>());
                }
            }

            // CSV — use DuckDB to infer column types
            DuckDBConnection connection;
            string fileUrl;

            if (objectStorageType == "azure_object")
            {
                var containerName = objectStorageConfig.AzureObjectConfig?.AzureContainerName;
                if (string.IsNullOrWhiteSpace(containerName))
                    return null;

                connection = await GetAzureDuckDbConnection(objectStorageConfig);

                var escapedContainer = containerName.Replace("'", "''");
                var escapedPath = fileUri.Replace("'", "''");
                fileUrl = $"az://{escapedContainer}/{escapedPath}";
            }
            else if (objectStorageType == "filesystem")
            {
                connection = await GetLocalDuckDbConnection();

                var escapedPath = fileUri.Replace("'", "''");
                fileUrl = escapedPath;
            }
            else
            {
                _logger.LogDebug("Unsupported object storage type for CSV column extraction");
                return null;
            }

            await using (connection)
            {
                await using var cmd = connection.CreateCommand();
                cmd.CommandText = $"DESCRIBE SELECT * FROM '{fileUrl}' LIMIT 0;";

                await using var reader = await cmd.ExecuteReaderAsync();

                var columns = new JsonArray();

                // DESCRIBE returns: column_name, column_type, null, key, default, extra
                while (await reader.ReadAsync())
                    columns.Add(new JsonObject
                    {
                        ["name"] = reader.GetString(0),
                        ["type"] = reader.GetString(1)
                    });

                return columns.Count > 0 ? columns : null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error while extracting columns: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Get a view of data points from a parquet/csv file stored in Azure Blob or local filesystem
    /// </summary>
    /// <param name="organizationId">ID of organization that timeseries data is associated with</param>
    /// <param name="projectId">ID of project that timeseries data is associated with</param>
    /// <param name="recordId">ID of record pointing to the parquet/csv file</param>
    /// <param name="limit">Maximum number of data points to include</param>
    /// <param name="rowStride">every nth row to get (row number 4 = every 4th row)</param>
    /// <returns>A json array of plot data</returns>
    public async Task<PlotDataDto> GetPlotData(long currentUserId, long organizationId, long projectId, long recordId,
        long limit, long rowStride)
    {
        var record = await _recordBusiness.GetRecord(currentUserId, organizationId, projectId, recordId, true);

        if (string.IsNullOrWhiteSpace(record.Uri))
            throw new ArgumentException($"Record {recordId} does not have a URI");

        var objectStorage = _context.ObjectStorages.FirstOrDefault(os => os.OrganizationId == organizationId &&
                                                                         (os.ProjectId == projectId ||
                                                                          os.ProjectId == null) &&
                                                                         os.Id == record.ObjectStorageId);

        if (objectStorage == null)
            throw new ArgumentException(
                $"Object storage with ID {record.ObjectStorageId} does not exist for project with ID of {projectId}");

        var objectStorageConfig = JsonConvert.DeserializeObject<ObjectStorageConfigDto>(objectStorage.Config);
        if (objectStorageConfig == null)
            throw new InvalidOperationException("Config data for object storage is null or invalid");

        // Determine storage type and get appropriate connection
        DuckDBConnection connection;
        string fileUrl;
        bool isFolder;

        if (objectStorage.Type == "azure_object")
        {
            // Azure Blob Storage
            var containerName = objectStorageConfig.AzureObjectConfig?.AzureContainerName;
            if (string.IsNullOrWhiteSpace(containerName))
                throw new ArgumentException("Container name is required for Azure storage");

            connection = await GetAzureDuckDbConnection(objectStorageConfig);

            var escapedContainer = containerName.Replace("'", "''");
            var escapedPath = record.Uri.Replace("'", "''");
            fileUrl = $"az://{escapedContainer}/{escapedPath}";

            isFolder = record.Uri.EndsWith("/", StringComparison.Ordinal);
        }
        else if (objectStorage.Type == "filesystem")
        {
            // Local filesystem
            connection = await GetLocalDuckDbConnection();

            var escapedPath = record.Uri.Replace("'", "''");
            fileUrl = escapedPath;

            isFolder = record.Uri.EndsWith(Path.DirectorySeparatorChar)
                       || record.Uri.EndsWith('/')
                       || Directory.Exists(record.Uri);
        }
        else
        {
            throw new InvalidOperationException("Object storage type is not supported for timeseries file queries");
        }

        // For folders, read all part files ordered by part number first so that
        // row_number() reflects true insertion order across all parts, and the
        // final ORDER BY rn DESC + LIMIT gives the correct last N rows globally.
        var fromClause = isFolder
            ? $"(SELECT * EXCLUDE filename FROM read_parquet(['{fileUrl.TrimEnd('/')}/*.parquet'], union_by_name = true, filename = true) ORDER BY CAST(regexp_extract(filename, '(\\d+)\\.parquet$', 1) AS BIGINT))"
            : $"'{fileUrl}'";

        await using (connection)
        {
            // Query the file directly with rowStride and limit
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = $@"
                                WITH numbered AS (
                                    SELECT *, row_number() OVER () as rn 
                                    FROM {fromClause}
                                )
                                SELECT * EXCLUDE rn
                                FROM numbered 
                                WHERE rn % {rowStride} = 0 
                                ORDER BY rn DESC 
                                LIMIT {limit}";

            await using var reader = await cmd.ExecuteReaderAsync();

            return await ReaderToPlotData(reader, true);
        }
    }

    /// <summary>
    ///     Appends a Parquet part file to an Azure Blob Storage dataset.
    ///     <para>
    ///         Safety guarantees:
    ///         <list type="bullet">
    ///             <item>
    ///                 The original blob is COPIED (not moved) into the folder as part 0, so it
    ///                 remains intact until after <see cref="DbContext.SaveChangesAsync()" /> succeeds.
    ///             </item>
    ///             <item>
    ///                 If any storage write fails, all blobs written so far in this operation are
    ///                 deleted and <c>record.Uri</c> is restored.
    ///             </item>
    ///             <item>
    ///                 If <see cref="DbContext.SaveChangesAsync()" /> fails, both newly written blobs
    ///                 are deleted and <c>record.Uri</c> is restored.
    ///             </item>
    ///             <item>
    ///                 The original blob is only deleted after the DB commit succeeds. A failure at
    ///                 that point leaves an orphaned blob but causes no data loss or broken record state.
    ///             </item>
    ///         </list>
    ///     </para>
    /// </summary>
    private async Task AppendToAzureBlob(
        Record record,
        ObjectStorageConfigDto objectStorageConfig,
        IFormFile file,
        long partNumber)
    {
        var containerName = objectStorageConfig.AzureObjectConfig?.AzureContainerName
                            ?? throw new ArgumentException("Azure container name is required.");

        var containerClient = new BlobServiceClient(objectStorageConfig.AzureObjectConfig!.AzureConnectionString)
            .GetBlobContainerClient(containerName);

        if (string.IsNullOrWhiteSpace(record.Uri))
            throw new ArgumentException("Record has no URI.");

        var isFirstAppend = record.Uri.EndsWith(".parquet", StringComparison.OrdinalIgnoreCase);

        string folderUri;
        string schemaSourceBlobName;

        if (isFirstAppend)
        {
            folderUri = record.Uri[..^".parquet".Length] + "/";
            schemaSourceBlobName = record.Uri;
        }
        else
        {
            folderUri = record.Uri;
            schemaSourceBlobName = containerClient
                                       .GetBlobs(prefix: record.Uri)
                                       .FirstOrDefault(b =>
                                           b.Name.EndsWith(".parquet", StringComparison.OrdinalIgnoreCase))
                                       ?.Name
                                   ?? throw new InvalidOperationException(
                                       "No existing Parquet files found in the dataset folder.");
        }

        // Validate schema — Parquet.Net reads only the file footer; no row data is loaded
        // into memory regardless of file size.
        await using var existingStream = await containerClient
            .GetBlobClient(schemaSourceBlobName)
            .OpenReadAsync();

        await using var incomingStream = file.OpenReadStream();
        await ValidateParquetSchema(existingStream, incomingStream);

        BlobClient? firstPartBlobClient = null;
        var newPartBlobClient = containerClient.GetBlobClient($"{folderUri}{partNumber}.parquet");

        var wroteFirstPart = false;
        var wroteNewPart = false;

        // Wrap all storage writes together so that a failure mid-upload triggers cleanup of
        // any blobs already written in this operation.
        try
        {
            // First append: COPY the original blob into the folder as part 0.
            // The original is left untouched here; it is only deleted after the DB commit succeeds.
            if (isFirstAppend)
            {
                firstPartBlobClient = containerClient.GetBlobClient($"{folderUri}0.parquet");

                if (await firstPartBlobClient.ExistsAsync())
                    throw new ArgumentException($"Part 0 already exists in folder {folderUri}.");

                var sourceBlob = containerClient.GetBlobClient(schemaSourceBlobName);
                await using var migrationStream = await sourceBlob.OpenReadAsync();
                await firstPartBlobClient.UploadAsync(migrationStream, false);
                wroteFirstPart = true;
            }

            if (await newPartBlobClient.ExistsAsync())
                throw new ArgumentException($"Part {partNumber} already exists in folder {folderUri}.");

            incomingStream.Seek(0, SeekOrigin.Begin);
            await newPartBlobClient.UploadAsync(incomingStream, false);
            wroteNewPart = true;
        }
        catch
        {
            // Compensate: delete any blobs written before the failure.
            // Only delete blobs that were created by this operation so that we do not
            // remove pre-existing parts when the failure was caused by a duplicate.
            if (wroteFirstPart && firstPartBlobClient is not null)
                await firstPartBlobClient.DeleteIfExistsAsync();

            if (wroteNewPart)
                await newPartBlobClient.DeleteIfExistsAsync();

            throw;
        }

        // Commit the URI change only after all storage writes have succeeded.
        if (isFirstAppend)
        {
            var originalUri = record.Uri;
            record.Uri = folderUri;
            try
            {
                await _context.SaveChangesAsync();
            }
            catch
            {
                record.Uri = originalUri;

                // Roll back only the blobs created by this operation.
                if (wroteFirstPart && firstPartBlobClient is not null)
                    await firstPartBlobClient.DeleteIfExistsAsync();

                if (wroteNewPart)
                    await newPartBlobClient.DeleteIfExistsAsync();

                throw;
            }

            // Original blob deleted only after the DB commit is confirmed.
            // A failure here leaves an orphaned blob but causes no data loss or broken record state.
            await containerClient.GetBlobClient(schemaSourceBlobName).DeleteIfExistsAsync();
        }
    }

    /// <summary>
    ///     Appends a Parquet part file to a filesystem dataset.
    ///     <para>
    ///         Safety guarantees:
    ///         <list type="bullet">
    ///             <item>
    ///                 The original file is COPIED (not moved) into the folder as part 0, so it
    ///                 remains intact until after <see cref="DbContext.SaveChangesAsync()" /> succeeds.
    ///             </item>
    ///             <item>
    ///                 The incoming stream is copied via the already-open handle, avoiding a Windows
    ///                 file-lock conflict that would occur with <see cref="File.Copy(string,string)" />
    ///                 on an open file.
    ///             </item>
    ///             <item>
    ///                 If any storage write fails, all files written so far in this operation are
    ///                 deleted and <c>record.Uri</c> is restored.
    ///             </item>
    ///             <item>
    ///                 If <see cref="DbContext.SaveChangesAsync()" /> fails, both newly written files
    ///                 and the created directory are deleted and <c>record.Uri</c> is restored.
    ///             </item>
    ///             <item>
    ///                 The original file is only deleted after the DB commit succeeds. A failure at
    ///                 that point leaves an orphaned file but causes no data loss or broken record state.
    ///             </item>
    ///         </list>
    ///     </para>
    /// </summary>
    private async Task AppendToFilesystemAsync(
        Record record,
        IFormFile file,
        long partNumber)
    {
        if (string.IsNullOrWhiteSpace(record.Uri))
            throw new InvalidOperationException("Record has null or empty URI.");

        var fullPath = record.Uri;
        var isFirstAppend = record.Uri.EndsWith(".parquet", StringComparison.OrdinalIgnoreCase);

        string folderPath;
        string schemaSourcePath;

        if (isFirstAppend)
        {
            folderPath = fullPath[..^".parquet".Length] + Path.DirectorySeparatorChar;
            schemaSourcePath = fullPath;
        }
        else
        {
            folderPath = fullPath;
            schemaSourcePath = Directory
                                   .EnumerateFiles(fullPath, "*.parquet")
                                   .FirstOrDefault()
                               ?? throw new InvalidOperationException(
                                   "No existing Parquet files found in the dataset folder.");
        }

        // Validate schema — Parquet.Net reads only the file footer; no row data is loaded
        // into memory regardless of file size.
        await using var existingStream = File.OpenRead(schemaSourcePath);
        await using var incomingStream = file.OpenReadStream();
        await ValidateParquetSchema(existingStream, incomingStream);

        string? firstPartPath = null;
        var newPartPath = Path.Combine(folderPath, $"{partNumber}.parquet");
        var directoryCreated = false;
        var wroteFirstPart = false;
        var wroteNewPart = false;

        // Wrap all storage writes together so that a failure mid-write triggers cleanup of
        // any files already written in this operation.
        try
        {
            // First append: COPY the original into the folder as part 0 by streaming through
            // the already-open handle. Using the open stream rather than File.Copy avoids a
            // Windows file-lock error. The original file is left untouched until the DB commit succeeds.
            if (isFirstAppend)
            {
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                    directoryCreated = true;
                }

                firstPartPath = Path.Combine(folderPath, "0.parquet");

                if (File.Exists(firstPartPath))
                    throw new ArgumentException($"Part 0 already exists in folder {folderPath}.");

                existingStream.Seek(0, SeekOrigin.Begin);
                await using var firstPartStream = File.Create(firstPartPath);
                await existingStream.CopyToAsync(firstPartStream);
                wroteFirstPart = true;
            }

            if (File.Exists(newPartPath))
                throw new ArgumentException($"Part {partNumber} already exists in folder {folderPath}.");

            incomingStream.Seek(0, SeekOrigin.Begin);
            await using var outputStream = File.Create(newPartPath);
            await incomingStream.CopyToAsync(outputStream);
            wroteNewPart = true;
        }
        catch
        {
            // Compensate: delete any files written before the failure.
            // Only delete files that were created by this operation so that we do not
            // remove pre-existing parts when the failure was caused by a duplicate.
            if (wroteFirstPart && firstPartPath is not null && File.Exists(firstPartPath))
                File.Delete(firstPartPath);

            if (wroteNewPart && File.Exists(newPartPath))
                File.Delete(newPartPath);

            // Use recursive: true so a spurious file in the folder can't mask the original
            // exception. Only attempt deletion if this operation created the directory.
            if (directoryCreated && Directory.Exists(folderPath))
                try
                {
                    Directory.Delete(folderPath, true);
                }
                catch
                {
                    /* Swallow — the original exception is more important. */
                }

            throw;
        }

        // Commit the URI change only after all storage writes have succeeded.
        if (isFirstAppend)
        {
            var originalUri = record.Uri;
            record.Uri = folderPath;
            try
            {
                await _context.SaveChangesAsync();
            }
            catch
            {
                record.Uri = originalUri;

                // Roll back only the files created by this operation.
                if (wroteFirstPart && firstPartPath is not null && File.Exists(firstPartPath))
                    File.Delete(firstPartPath);

                if (wroteNewPart && File.Exists(newPartPath))
                    File.Delete(newPartPath);

                if (directoryCreated && Directory.Exists(folderPath))
                    try
                    {
                        Directory.Delete(folderPath, true);
                    }
                    catch
                    {
                        /* Swallow — the SaveChangesAsync exception is more important. */
                    }

                throw;
            }

            // Original file deleted only after the DB commit is confirmed.
            // A failure here leaves an orphaned file but causes no data loss or broken record state.
            File.Delete(fullPath);
        }
    }

    /// <summary>
    ///     Validates that the incoming Parquet stream has a schema exactly compatible with the
    ///     existing one. Uses Parquet.Net on both sides so types are directly comparable with no
    ///     mapping layer. Only the file footer is read from each stream — no row data is loaded
    ///     into memory.
    ///     <para>
    ///         Validation is bidirectional: the incoming file must have every column in the existing
    ///         schema (no missing columns) and must not introduce any columns absent from it (no extra
    ///         columns). Either violation would produce a structurally inconsistent dataset.
    ///     </para>
    /// </summary>
    /// <param name="existingStream">Stream of the current Parquet file.</param>
    /// <param name="incomingStream">Stream of the Parquet file to append.</param>
    /// <exception cref="InvalidOperationException"></exception>
    private static async Task ValidateParquetSchema(Stream existingStream, Stream incomingStream)
    {
        using var existingReader = await ParquetReader.CreateAsync(existingStream);
        var existingFields = existingReader.Schema.GetDataFields()
            .ToDictionary(f => f.Name, f => f.ClrType);

        using var incomingReader = await ParquetReader.CreateAsync(incomingStream);
        var incomingFields = incomingReader.Schema.GetDataFields()
            .ToDictionary(f => f.Name, f => f.ClrType);

        // The incoming file must not introduce columns absent from the existing schema.
        foreach (var name in incomingFields.Keys)
            if (!existingFields.ContainsKey(name))
                throw new InvalidOperationException(
                    $"Incoming file has unexpected column '{name}' not present in the existing schema.");

        // Every column in the existing schema must be present in the incoming file with a
        // matching type.
        foreach (var (name, type) in existingFields)
        {
            if (!incomingFields.TryGetValue(name, out var incomingType))
                throw new InvalidOperationException(
                    $"Incoming file is missing column '{name}'.");

            if (incomingType != type)
                throw new InvalidOperationException(
                    $"Column '{name}' type mismatch: existing is '{type}', incoming is '{incomingType}'.");
        }
    }

    /// <summary>
    ///     Creates an in-memory DuckDB connection configured with the Azure extension
    ///     Supports both reading (read_parquet/read_csv) and writing (COPY statements) to Azure Blob Storage
    /// </summary>
    private static async Task<DuckDBConnection> GetAzureDuckDbConnection(ObjectStorageConfigDto objectStorageConfig)
    {
        if (objectStorageConfig.AzureObjectConfig == null)
            throw new ArgumentException("Azure object config is required");

        // Create in-memory connection
        var connectionString = "Data Source=:memory:";
        var connection = new DuckDBConnection(connectionString);
        await connection.OpenAsync();

        try
        {
            // Install and load the Azure extension. Should happen automatically, but this is just in case.
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "INSTALL azure; LOAD azure;";
                await cmd.ExecuteNonQueryAsync();
            }

            // Create a secret for Azure authentication
            var secretName = $"azure_secret_{Guid.NewGuid():N}";

            // Escape single quotes in connection string to prevent SQL injection
            var escapedConnectionString = objectStorageConfig.AzureObjectConfig.AzureConnectionString
                .Replace("'", "''");

            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = $@"CREATE SECRET {secretName} (
                TYPE azure,
                PROVIDER config,
                CONNECTION_STRING '{escapedConnectionString}'
            );";
                await cmd.ExecuteNonQueryAsync();
            }

            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    ///     Creates an in-memory DuckDB connection for reading local files
    ///     No extensions or authentication needed for local filesystem access
    /// </summary>
    private static async Task<DuckDBConnection> GetLocalDuckDbConnection()
    {
        // Create in-memory connection
        var connectionString = "Data Source=:memory:";
        var connection = new DuckDBConnection(connectionString);
        await connection.OpenAsync();

        return connection;
    }

    /// <summary>
    ///     Converts a DbDataReader to PlotDataDto format
    /// </summary>
    /// <param name="reader">The data reader to convert</param>
    /// <returns>PlotDataDto with columns and data</returns>
    private static async Task<PlotDataDto> ReaderToPlotData(DbDataReader reader, bool reverse = false)
    {
        var columnCount = reader.FieldCount;
        var columns = new string[columnCount];
        for (var i = 0; i < columnCount; i++)
            columns[i] = reader.GetName(i);

        var points = new List<object[]>();

        while (await reader.ReadAsync())
        {
            var row = new object[columnCount];
            for (var i = 0; i < columnCount; i++)
                row[i] = reader.GetValue(i);
            points.Add(row);
        }

        if (reverse)
            points.Reverse();

        return new PlotDataDto { Columns = columns, Data = [.. points] };
    }
}