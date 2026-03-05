using System.Data.Common;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.interfaces;
using deeplynx.models;
using DuckDB.NET.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace deeplynx.business;

public class TimeseriesBusiness(
    DeeplynxContext context,
    IRecordBusiness recordBusiness,
    IClassBusiness classBusiness,
    ILogger<TimeseriesBusiness> logger,
    [FromServices] IServiceScopeFactory serviceScopeFactory) : ITimeseriesBusiness
{
    private static readonly string _duckDbBasePath =
        Environment.GetEnvironmentVariable("DUCKDB_BASE_PATH") ?? "/data/duckdb";

    private readonly IClassBusiness _classBusiness = classBusiness;
    private readonly DeeplynxContext _context = context;
    private readonly IRecordBusiness _recordBusiness = recordBusiness;
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;

    /// <summary>
    ///     Appends file to existing table
    /// </summary>
    /// <param name="organizationId">The organization ID</param>
    /// <param name="projectId">The project ID</param>
    /// <param name="dataSourceId">The data source ID</param>
    /// <param name="file">file data to append</param>
    /// <param name="tableName">The table to append</param>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="Exception"></exception>
    public async Task AppendTimeseriesTable(long organizationId, long projectId, long dataSourceId, IFormFile file,
        string tableName)
    {
        // file = new SanitizedFormFile(file);
        // tableName = SanitizedFormFile.SanitizeFileName(tableName);
        // var fileType = Path.GetExtension(file.FileName);
        // if (fileType != ".csv" && fileType != ".parquet")
        //     throw new ArgumentException("Only CSV and Parquet files are supported.");
        //
        // if (file.Length == 0) throw new Exception("Can not append empty file");
        //
        //
        // await ExistenceHelper.EnsureDataSourceExistsForProjectAsync(_context, dataSourceId, projectId);
        //
        // using var duckDbConnection = await GetDuckDbConnection(organizationId, projectId, dataSourceId);
        //
        // // injection protection
        // var cacheKey = $"tables_{dataSourceId}";
        // if (!_tableCache.TryGetValue(cacheKey, out HashSet<string>? validTables))
        // {
        //     validTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        //     using var tableCmd = duckDbConnection.CreateCommand();
        //     tableCmd.CommandText = "SELECT table_name FROM duckdb_tables()";
        //     using var tableReader = await tableCmd.ExecuteReaderAsync();
        //     while (await tableReader.ReadAsync())
        //     {
        //         validTables.Add(tableReader.GetString(0));
        //     }
        //     _tableCache.Set(cacheKey, validTables, TimeSpan.FromSeconds(120));
        // }
        //
        // if (!validTables!.Contains(tableName))
        //     throw new ArgumentException($"Invalid table name: {tableName}");
        //
        // // save file to temporary directory
        // var guid = Guid.NewGuid();
        // var tempFolderPath = Path.Combine(_duckDbBasePath, "org_" + organizationId, "project_" + projectId,
        //     "datasource_" + dataSourceId, guid.ToString());
        // Directory.CreateDirectory(tempFolderPath);
        //
        // var tempFilePath = Path.Combine(tempFolderPath, file.FileName);
        //
        // // Ensure file stream is fully closed before DuckDB access
        // // (stream disposal race condition causes misleading CSV parsing errors)
        // {
        //     await using var stream = new FileStream(tempFilePath, FileMode.Create);
        //     await file.CopyToAsync(stream);
        //     await stream.FlushAsync();
        // }
        //
        // try
        // {
        //     await using var command = duckDbConnection.CreateCommand();
        //
        //     if (fileType == ".csv")
        //         command.CommandText = $@"
        //         COPY '{tableName}' FROM '{tempFilePath}' (AUTO_DETECT true)";
        //     else
        //         command.CommandText = $"COPY '{tableName}' FROM '{tempFilePath}'";
        //
        //     await command.ExecuteNonQueryAsync();
        // }
        // finally
        // {
        //     await duckDbConnection.CloseAsync();
        //
        //     // Clean up temp file
        //     if (Directory.Exists(tempFolderPath)) Directory.Delete(tempFolderPath, true);
        // }
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
    public async Task<RecordResponseDto> ExportTimeseriesTable(long currentUserId, long organizationId, long projectId,
        long dataSourceId,
        string tableName, string fileType)
    {
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
        return new RecordResponseDto();
    }

    /// <summary>
    ///     Queries timeseries data directly from a blob
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
            "COPY",             // Prevent writing files
            "EXPORT",           // Prevent exporting database/data
            "IMPORT",           // Prevent importing
            "INSERT",           // Prevent inserting to other tables
            "UPDATE",           // Prevent updates
            "DELETE",           // Prevent deletes  
            "DROP",             // Prevent dropping objects
            "ALTER",            // Prevent schema changes
            "CREATE TABLE",     // Prevent creating tables
            "CREATE VIEW",      // Prevent creating views (besides temp view we control)
            "az://",            // Azure blob paths
            "read_parquet(",    // File reading functions
            "read_csv(",
            "read_json(",
            "ATTACH",           // Database attachment
            "CREATE SECRET",    // Secret manipulation
            ".parquet'",        // File extensions in quotes
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
        if (record == null)
            throw new ArgumentException($"Record with ID {recordId} does not exist");

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
        }
        else if (objectStorage.Type == "filesystem")
        {
            // Local filesystem
            connection = await GetLocalDuckDbConnection();

            var escapedPath = record.Uri.Replace("'", "''");
            fileUrl = escapedPath;
        }
        else
        {
            throw new InvalidOperationException("Object storage type is not supported for timeseries file queries");
        }

        await using (connection)
        {
            // Create a temporary view pointing to the file
            await using (var createViewCmd = connection.CreateCommand())
            {
                createViewCmd.CommandText = $"CREATE OR REPLACE TEMP VIEW {viewName} AS SELECT * FROM '{fileUrl}';";
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
    ///     Get a view of data points from a parquet/csv file stored in Azure Blob or local filesystem
    /// </summary>
    /// <param name="organizationId">ID of organization that timeseries data is associated with</param>
    /// <param name="projectId">ID of project that timeseries data is associated with</param>
    /// <param name="dataSourceId">ID of data source that timeseries data is associated with</param>
    /// <param name="recordId">ID of record pointing to the parquet/csv file</param>
    /// <param name="limit">Maximum number of data points to include</param>
    /// <param name="rowStride">every nth row to get (row number 4 = every 4th row)</param>
    /// <returns>A json array of plot data</returns>
    public async Task<PlotDataDto> GetPlotData(long currentUserId, long organizationId, long projectId,
        long dataSourceId, long recordId, long limit, long rowStride)
    {
        await ExistenceHelper.EnsureDataSourceExistsForProjectAsync(_context, dataSourceId, projectId);

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
        }
        else if (objectStorage.Type == "filesystem")
        {
            // Local filesystem
            connection = await GetLocalDuckDbConnection();

            var escapedPath = record.Uri.Replace("'", "''");
            fileUrl = escapedPath;
        }
        else
        {
            throw new InvalidOperationException("Object storage type is not supported for timeseries file queries");
        }

        await using (connection)
        {
            // Query the file directly with rowStride and limit
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = $@"
                                WITH numbered AS (
                                    SELECT *, row_number() OVER () as rn 
                                    FROM '{fileUrl}'
                                )
                                SELECT * EXCLUDE rn
                                FROM numbered 
                                WHERE rn % {rowStride} = 0 
                                ORDER BY rn DESC 
                                LIMIT {limit}";

            await using var reader = await cmd.ExecuteReaderAsync();

            return await ReaderToPlotData(reader);
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
    ///     Gets all the column names and types from the table
    /// </summary>
    /// <param name="organizationId">The organization ID</param>
    /// <param name="projectId">The project ID</param>
    /// <param name="dataSourceId">The data source ID</param>
    /// <param name="tableName">Timeseries table name</param>
    /// <returns>JSON array of columns</returns>
    private static async Task<JsonArray> GetColumnsFromDb(long organizationId, long projectId, long dataSourceId,
        string tableName)
    {
        // var columns = new JsonArray();
        // tableName = SanitizedFormFile.SanitizeFileName(tableName);
        // using var duckDbConnection = await GetDuckDbConnection(organizationId, projectId, dataSourceId);
        //
        // await using var command = duckDbConnection.CreateCommand();
        // command.CommandText =
        //     $"SELECT column_name, data_type FROM information_schema.columns WHERE table_name = '{tableName}';";
        //
        // using var reader = await command.ExecuteReaderAsync();
        //
        // await duckDbConnection.CloseAsync();
        //
        // while (reader.Read())
        // {
        //     var columnName = reader[0].ToString();
        //     var columnType = reader[1].ToString();
        //
        //     var columnObject = new JsonObject
        //     {
        //         ["name"] = columnName,
        //         ["type"] = columnType
        //     };
        //     columns.Add(columnObject);
        // }
        //
        return new JsonArray();
    }
    
    /// <summary>
    /// Converts a DbDataReader to PlotDataDto format
    /// </summary>
    /// <param name="reader">The data reader to convert</param>
    /// <returns>PlotDataDto with columns and data</returns>
    private static async Task<PlotDataDto> ReaderToPlotData(DbDataReader reader)
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
        
        points.Reverse();

        return new PlotDataDto { Columns = columns, Data = [.. points] };
    }

    private void CleanDirectoryUpToBasePath(string? startDirectoryPath)
    {
        var normalizedBasePath = Path.GetFullPath(_duckDbBasePath).TrimEnd(Path.DirectorySeparatorChar);

        while (!string.IsNullOrEmpty(startDirectoryPath) &&
               Directory.Exists(startDirectoryPath) &&
               !Path.GetFullPath(startDirectoryPath).Equals(normalizedBasePath, StringComparison.OrdinalIgnoreCase))
            if (Directory.GetFileSystemEntries(startDirectoryPath).Length == 0)
            {
                Directory.Delete(startDirectoryPath);
                startDirectoryPath = Path.GetDirectoryName(startDirectoryPath);
            }
            else
            {
                break;
            }
    }
    
    /// <summary>
    /// Extracts column names from a tabular file (CSV or Parquet) stored in object storage
    /// </summary>
    /// <param name="organizationId">Organization ID</param>
    /// <param name="projectId">Project ID</param>
    /// <param name="objectStorage">Object storage entity</param>
    /// <param name="objectStorageConfig">Object storage configuration</param>
    /// <param name="fileUri">URI/path to the file</param>
    /// <returns>List of column names, or null if not tabular</returns>
    public async Task<List<string>?> ExtractTabularColumns(
        ObjectStorage objectStorage,
        ObjectStorageConfigDto objectStorageConfig,
        string fileUri)
    {
        DuckDBConnection connection;
        string fileUrl;
        
        if (objectStorage.Type == "azure_object")
        {
            // Azure Blob Storage
            var containerName = objectStorageConfig.AzureObjectConfig?.AzureContainerName;
            if (string.IsNullOrWhiteSpace(containerName))
                return null;

            connection = await GetAzureDuckDbConnection(objectStorageConfig);
            
            var escapedContainer = containerName.Replace("'", "''");
            var escapedPath = fileUri.Replace("'", "''");
            fileUrl = $"az://{escapedContainer}/{escapedPath}";
        }
        else if (objectStorage.Type == "filesystem")
        {
            // Local filesystem
            connection = await GetLocalDuckDbConnection();
            
            var escapedPath = fileUri.Replace("'", "''");
            fileUrl = escapedPath;
        }
        else
        {
            // Unsupported storage type for column extraction
            return null;
        }

        await using (connection)
        {
            try
            {
                // Query to get column information
                await using var cmd = connection.CreateCommand();
                cmd.CommandText = $"DESCRIBE SELECT * FROM '{fileUrl}' LIMIT 0;";
                
                await using var reader = await cmd.ExecuteReaderAsync();
                
                var columns = new List<string>();
                
                // DESCRIBE returns: column_name, column_type, null, key, default, extra
                while (await reader.ReadAsync())
                {
                    var columnName = reader.GetString(0); // First column is column_name
                    columns.Add(columnName);
                }
                
                return columns.Count > 0 ? columns : null;
            }
            catch
            {
                // If we can't read the file structure, it's probably not tabular
                return null;
            }
        }
    }

    private static class Status
    {
        public static string Failed { get; } = "failed";
        public static string Completed { get; } = "completed";
        public static string InProgress { get; } = "in progress";
    }
}