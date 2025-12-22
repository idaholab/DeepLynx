using System.Text.Json.Nodes;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.interfaces;
using deeplynx.models;
using DuckDB.NET.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
    ///     Creates DuckDB table based on the table name and the file path
    /// </summary>
    /// <param name="organizationId">The organization ID</param>
    /// <param name="projectId">The project ID</param>
    /// <param name="dataSourceId">The data source ID</param>
    /// <param name="tableName">Timeseries table name</param>
    /// <param name="filePath">The path of the file being uploaded to DuckDB</param>
    /// <param name="fileType">The Type of the file (accepts CSV or Parquet)</param>
    public async Task CreateTimeseriesTable(long organizationId, long projectId, long dataSourceId, string tableName, string filePath,
        string fileType)
    {
        using var duckDbConnection = await GetDuckDbConnection(organizationId, projectId, dataSourceId);

        await using var command = duckDbConnection.CreateCommand();
        if (fileType == ".csv")
            command.CommandText = $"CREATE TABLE '{tableName}' AS SELECT * from read_csv('{filePath}'); ";
        else if (fileType == ".parquet")
            command.CommandText = $"CREATE TABLE '{tableName}' AS SELECT * from read_parquet('{filePath}'); ";

        await command.ExecuteNonQueryAsync();
        await duckDbConnection.CloseAsync();
    }

    /// <summary>
    ///     Uploads a time series file and kicks off the processing for DuckDB
    /// </summary>
    /// ///
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="organizationId">The organization ID</param>
    /// <param name="projectId">The project ID</param>
    /// <param name="dataSourceId">The data source ID</param>
    /// <param name="file">This is the entire file attached as form data in the request</param>
    /// <returns>An object of the uploaded file information</returns>
    /// <exception cref="ArgumentException">If the file is null or has no data</exception>
    /// <exception cref="InvalidOperationException">If the server cannot create the directory</exception>
    public async Task<RecordResponseDto> UploadFile(long currentUserId, long organizationId, long projectId,
        long dataSourceId,
        IFormFile file)
    {
        var fileType = Path.GetExtension(file.FileName);
        if (fileType != ".csv" && fileType != ".parquet")
            throw new ArgumentException("Only .csv and .parquet files are supported");

        if (file == null || file.Length == 0)
            throw new ArgumentException("File is required and cannot be empty or whitespace.");

        await ExistenceHelper.EnsureDataSourceExistsForProjectAsync(_context, dataSourceId, projectId);

        // folder prep
        var uploadId = Guid.NewGuid().ToString();
        var tableName = uploadId + "_" + file.FileName;
        var folderPath = Path.Combine(_duckDbBasePath, "org_" + organizationId, "project_" + projectId, "datasource_" + dataSourceId);
        var filePath = Path.Combine(folderPath, tableName);
        Directory.CreateDirectory(folderPath ?? throw new InvalidOperationException("error creating upload path"));

        var uri = "duckdb://" + tableName;

        try
        {
            // copy file to path for db
            await using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // write file as table
            await CreateTimeseriesTable(organizationId, projectId, dataSourceId, tableName, filePath, fileType);

            // delete file when its in duckdb
            File.Delete(filePath);


            // create record for file's db table
            var recordClass = await _classBusiness.GetOrCreateClass(
                currentUserId, organizationId, projectId, "Timeseries");
            var columns = await GetColumnsFromDb(organizationId, projectId, dataSourceId, tableName);
            var fileName = file.FileName;

            var recordRequest = new CreateRecordRequestDto
            {
                Properties = new JsonObject
                {
                    ["columns"] = columns,
                    ["timeUploaded"] = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                    ["fileType"] = Path.GetExtension(file.FileName).TrimStart('.').ToLower()
                },
                Name = fileName,
                Description = $"Table name: {tableName}",
                OriginalId = uploadId,
                Uri = uri,
                ClassId = recordClass.Id,
                ClassName = recordClass.Name,
                FileType = Path.GetExtension(file.FileName).TrimStart('.').ToLower()
            };

            return await _recordBusiness.CreateRecord(currentUserId, organizationId, projectId, dataSourceId,
                recordRequest);
        }
        catch (Exception)
        {
            // delete file if file exists but fails when creating table
            if (File.Exists(filePath)) File.Delete(filePath);

            var timeseriesPath = Path.Combine(folderPath, "timeseries.duckdb");
            var timeseriesWalPath = Path.Combine(folderPath, "timeseries.duckdb.wal");

            // initial check to see if there is a timeseries db
            if (File.Exists(timeseriesPath))
            {
                var connection = await GetDuckDbConnection(organizationId, projectId, dataSourceId);
                await using var command = connection.CreateCommand();

                // checks if table exists
                command.CommandText =
                    $" SELECT COUNT(*) FROM information_schema.tables WHERE table_name = '{tableName}'";
                var exists = Convert.ToInt32(command.ExecuteScalar()) > 0;

                // drop table if it exists after failure
                if (exists)
                {
                    command.CommandText = $"DROP TABLE \"{tableName}\"";
                    command.ExecuteNonQuery();
                }

                // check to see if db has any tables
                command.CommandText = " SELECT COUNT(*) FROM information_schema.tables";
                var hasTables = Convert.ToInt32(command.ExecuteScalar()) > 0;
                if (!hasTables)
                {
                    // delete unneeded timeseries files if no tables
                    File.Delete(timeseriesPath);
                    if (File.Exists(timeseriesWalPath)) File.Delete(timeseriesWalPath);
                }

                await connection.CloseAsync();
            }

            // Clean up possible empty directories up to the base file path
            if (Directory.Exists(folderPath)) CleanDirectoryUpToBasePath(folderPath);

            throw;
        }
    }

    /// <summary>
    ///     Sets up the directory for file chunks to be uploaded
    /// </summary>
    /// <param name="organizationId">The organization ID</param>
    /// <param name="projectId">The project ID</param>
    /// <param name="dataSourceId">The data source ID</param>
    /// <param name="fileName">The Data Source ID</param>
    /// <returns>The upload ID (guid format) for file chunks to go to the right directory</returns>
    public async Task<string> StartUpload(long organizationId, long projectId, long dataSourceId, string fileName)
    {
        var fileType = Path.GetExtension(fileName);
        if (fileType != ".csv" && fileType != ".parquet")
            throw new ArgumentException("Only .csv and .parquet files are supported");

        await ExistenceHelper.EnsureDataSourceExistsForProjectAsync(_context, dataSourceId, projectId);

        var uploadId = Guid.NewGuid().ToString();
        var folderPath = Path.Combine(_duckDbBasePath, "org_" + organizationId, "project_" + projectId, "datasource_" + dataSourceId, uploadId);
        Directory.CreateDirectory(folderPath);

        return uploadId;
    }

    /// <summary>
    ///     Uploads a partial file to the specified directory
    /// </summary>
    /// <param name="organizationId">The organization ID</param>
    /// <param name="projectId">The project ID</param>
    /// <param name="dataSourceId">The data source ID</param>
    /// <param name="chunk">Raw binary data, max of 30MB by default</param>
    /// <param name="uploadId">the upload guid from StartUpload</param>
    /// <param name="chunkNumber">the index for tracking the order to merge chunks together</param>
    /// <returns>A string to denote the status</returns>
    /// <exception cref="ArgumentException">If the chunk is null or has no data</exception>
    public async Task<string> UploadChunk(long organizationId, long projectId, long dataSourceId, IFormFile chunk,
        string uploadId, int chunkNumber)
    {
        var baseFolderPath = Path.Combine(_duckDbBasePath, "org_" + organizationId, "project_" + projectId, "datasource_" + dataSourceId);
        var tempFolderPath = Path.Combine(baseFolderPath, uploadId);
        var tempFilePath = Path.Combine(tempFolderPath, $"{chunkNumber}.part");

        try
        {
            var fileType = Path.GetExtension(chunk.FileName);
            if (fileType != ".csv" && fileType != ".parquet")
                throw new ArgumentException("Only .csv and .parquet files are supported");


            await ExistenceHelper.EnsureDataSourceExistsForProjectAsync(_context, dataSourceId, projectId);
            if (chunk == null || chunk.Length == 0) throw new ArgumentException("No chunk uploaded.");

            await using var stream = new FileStream(tempFilePath, FileMode.Create);
            await chunk.CopyToAsync(stream);

            return "success";
        }
        catch (Exception)
        {
            if (Directory.Exists(tempFolderPath)) Directory.Delete(tempFolderPath, true);

            if (Directory.Exists(baseFolderPath)) CleanDirectoryUpToBasePath(baseFolderPath);

            throw;
        }
    }

    /// <summary>
    ///     Merges the file chunks and creates the finalized uploaded file and kicks off the processing for DuckDB
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method</param>
    /// <param name="organizationId">The organization ID</param>
    /// <param name="projectId">The project ID</param>
    /// <param name="dataSourceId">The Data Source ID</param>
    /// <param name="request">The request, which contains the UploadID and FileName</param>
    /// <returns>An object of the uploaded file information</returns>
    public async Task<RecordResponseDto> CompleteUpload(long currentUserId, long organizationId, long projectId,
        long dataSourceId,
        TimeseriesUploadCompleteRequestDto request)
    {
        var dataSourceFolderPath = Path.Combine(_duckDbBasePath, "org_" + organizationId, "project_" + projectId, "datasource_" + dataSourceId);
        var tempFolderPath = Path.Combine(dataSourceFolderPath, request.UploadId);
        var tableName = request.UploadId + "_" + request.FileName;
        var finalFilePath = Path.Combine(tempFolderPath, request.UploadId + "_" + request.FileName);
        var uri = "duckdb://" + tableName;

        try
        {
            var fileType = Path.GetExtension(request.FileName);
            if (fileType != ".csv" && fileType != ".parquet")
                throw new ArgumentException("Only .csv and .parquet files are supported");


            await ExistenceHelper.EnsureDataSourceExistsForProjectAsync(_context, dataSourceId, projectId);
            await using (var finalFileStream = new FileStream(finalFilePath, FileMode.Create))
            {
                for (var i = 0; i < request.TotalChunks; i++)
                {
                    var partFilePath = Path.Combine(tempFolderPath, $"{i}.part");
                    await using (var partStream = new FileStream(partFilePath, FileMode.Open))
                    {
                        await partStream.CopyToAsync(finalFileStream);
                    }

                    File.Delete(partFilePath); // Clean up the chunk file
                }
            }

            await CreateTimeseriesTable(organizationId, projectId, dataSourceId, tableName, finalFilePath, fileType);

            Directory.Delete(tempFolderPath, true); // Clean up the datasource folder

            var recordClass = await _classBusiness.GetOrCreateClass(
                currentUserId, organizationId, projectId, "Timeseries");
            var columns = await GetColumnsFromDb(organizationId, projectId, dataSourceId, tableName);
            var fileName = request.FileName;

            var recordRequest = new CreateRecordRequestDto
            {
                Properties = new JsonObject
                {
                    ["columns"] = columns,
                    ["timeUploaded"] = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                    ["fileType"] = Path.GetExtension(request.FileName).TrimStart('.').ToLower()
                },
                Name = fileName,
                Description = $"Table name: {tableName}",
                OriginalId = request.UploadId,
                Uri = uri,
                ClassId = recordClass.Id,
                ClassName = recordClass.Name,
                FileType = Path.GetExtension(request.FileName).TrimStart('.').ToLower()
            };

            return await _recordBusiness.CreateRecord(currentUserId, organizationId, projectId, dataSourceId,
                recordRequest);
        }
        catch (Exception)
        {
            // delete temporary folder if exists
            if (Directory.Exists(tempFolderPath))
                // recursive = true to delete folder contents
                Directory.Delete(tempFolderPath, true);

            var timeseriesPath = Path.Combine(dataSourceFolderPath, "timeseries.duckdb");
            var timeseriesWalPath = Path.Combine(dataSourceFolderPath, "timeseries.duckdb.wal");

            // initial check to see if db exists
            if (File.Exists(timeseriesPath))
            {
                var connection = await GetDuckDbConnection(organizationId, projectId, dataSourceId);
                await using var command = connection.CreateCommand();

                //gets table from db
                command.CommandText =
                    $" SELECT COUNT(*) FROM information_schema.tables WHERE table_name = '{tableName}'";
                var exists = Convert.ToInt32(command.ExecuteScalar()) > 0;

                if (exists)
                {
                    // drop table if it still exists after failure
                    command.CommandText = $"DROP TABLE \"{tableName}\"";
                    command.ExecuteNonQuery();
                }

                // check if db has any other tables
                command.CommandText = " SELECT COUNT(*) FROM information_schema.tables";
                var hasTables = Convert.ToInt32(command.ExecuteScalar()) > 0;
                if (!hasTables)
                {
                    // delete duckdb files if db is empty
                    File.Delete(timeseriesPath);
                    if (File.Exists(timeseriesWalPath)) File.Delete(timeseriesWalPath);
                }

                await connection.CloseAsync();
            }

            //cleans up empty folders up to base path
            if (Directory.Exists(dataSourceFolderPath)) CleanDirectoryUpToBasePath(dataSourceFolderPath);

            throw;
        }
    }

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
    public async Task AppendTimeseriesTable(long organizationId, long projectId, long dataSourceId, IFormFile file, string tableName)
    {
        var fileType = Path.GetExtension(file.FileName);
        if (fileType != ".csv" && fileType != ".parquet")
            throw new ArgumentException("Only CSV and Parquet files are supported.");

        if (file.Length == 0) throw new Exception("Can not append empty file");


        await ExistenceHelper.EnsureDataSourceExistsForProjectAsync(_context, dataSourceId, projectId);

        using var duckDbConnection = await GetDuckDbConnection(organizationId, projectId, dataSourceId);

        // save file to temporary directory
        var guid = Guid.NewGuid();
        var tempFolderPath = Path.Combine(_duckDbBasePath, "org_" + organizationId, "project_" + projectId,
            "datasource_" + dataSourceId, guid.ToString());
        Directory.CreateDirectory(tempFolderPath);

        var tempFilePath = Path.Combine(tempFolderPath, file.FileName);

        // Ensure file stream is fully closed before DuckDB access
        // (stream disposal race condition causes misleading CSV parsing errors)
        {
            await using var stream = new FileStream(tempFilePath, FileMode.Create);
            await file.CopyToAsync(stream);
            await stream.FlushAsync();
        }

        try
        {
            await using var command = duckDbConnection.CreateCommand();

            if (fileType == ".csv")
                command.CommandText = $@"
                COPY '{tableName}' FROM '{tempFilePath}' (AUTO_DETECT true)";
            else
                command.CommandText = $"COPY '{tableName}' FROM '{tempFilePath}'";

            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            await duckDbConnection.CloseAsync();

            // Clean up temp file
            if (Directory.Exists(tempFolderPath)) Directory.Delete(tempFolderPath, true);
        }
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
        await ExistenceHelper.EnsureDataSourceExistsForProjectAsync(_context, dataSourceId, projectId);
        var request = new TimeseriesQueryRequestDto
        {
            Query = $"SELECT * FROM '{tableName}'"
        };

        var queryId = Guid.NewGuid().ToString();
        string fileName;

        if (fileType == "csv")
            fileName = queryId + "_record.csv";
        else if (fileType == "parquet")
            fileName = queryId + "_record.parquet";
        else
            throw new NotSupportedException($"file type {fileType} not supported");

        var reportClass = await _classBusiness.GetOrCreateClass(
            currentUserId, organizationId, projectId, "Report");
        var timeseriesObjectStorageMethod =
            await _context.ObjectStorages.FirstOrDefaultAsync(os =>
                os.ProjectId == projectId && os.Name == "Timeseries Default");
        if (timeseriesObjectStorageMethod == null)
            throw new KeyNotFoundException("Default timeseries object storage method not found");

        var recordRequest = new CreateRecordRequestDto
        {
            Properties = new JsonObject
            {
                ["status"] = Status.InProgress,
                ["query"] = request.Query
            },
            Name = fileName,
            Description = $"Timeseries result report for {fileName}",
            OriginalId = queryId,
            ClassId = reportClass.Id,
            ClassName = reportClass.Name,
            ObjectStorageId = timeseriesObjectStorageMethod.Id,
            FileType = fileType
        };

        var recordResponse =
            await _recordBusiness.CreateRecord(currentUserId, organizationId, projectId, dataSourceId, recordRequest);

        // meant to run in background so don't await!
        RunBackgroundJob(recordResponse, request.Query, organizationId, projectId, dataSourceId, fileName, fileType);
        return recordResponse;
    }

    /// <summary>
    ///     Queries a table and retrieves every nth row
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="rowNumber">Which nth row to interpolate</param>
    /// <param name="tableName">The table to query</param>
    /// <param name="projectId">The project ID</param>
    /// <param name="organizationId">The organization ID</param>
    /// <param name="dataSourceId">The data source ID</param>
    /// <param name="fileType">The type of file to convert query to</param>
    /// <returns>Data</returns>
    public async Task<RecordResponseDto> InterpolateRows(long currentUserId, long organizationId, long projectId,
        long dataSourceId,
        string rowNumber, string tableName, string fileType)
    {
        await ExistenceHelper.EnsureDataSourceExistsForProjectAsync(_context, dataSourceId, projectId);
        var queryId = Guid.NewGuid().ToString();
        string fileName;

        if (fileType == "csv")
            fileName = queryId + "_record.csv";
        else if (fileType == "parquet")
            fileName = queryId + "_record.parquet";
        else
            throw new NotSupportedException($"file type {fileType} not supported");

        var request = new TimeseriesQueryRequestDto
        {
            Query = $"""

                     SELECT * FROM
                     (
                         SELECT *, ROW_NUMBER() OVER() AS row_num
                         FROM '{tableName}'
                     ) AS numbered_table
                     WHERE row_num % {rowNumber} = 0

                     """
        };

        var reportClass = await _classBusiness.GetOrCreateClass(
            currentUserId, organizationId, projectId, "Report");
        var timeseriesObjectStorageMethod =
            await _context.ObjectStorages.FirstOrDefaultAsync(os =>
                os.ProjectId == projectId && os.Name == "Timeseries Default");
        if (timeseriesObjectStorageMethod == null)
            throw new KeyNotFoundException("Default timeseries object storage method not found");

        var recordRequest = new CreateRecordRequestDto
        {
            Properties = new JsonObject
            {
                ["status"] = Status.InProgress,
                ["query"] = request.Query
            },
            Name = fileName,
            Description = $"Timeseries result report for {fileName}",
            OriginalId = queryId,
            ClassId = reportClass.Id,
            ClassName = reportClass.Name,
            ObjectStorageId = timeseriesObjectStorageMethod.Id,
            FileType = fileType
        };

        var recordResponse =
            await _recordBusiness.CreateRecord(currentUserId, organizationId, projectId, dataSourceId, recordRequest);

        // meant to run in background so don't await!
        RunBackgroundJob(recordResponse, request.Query, organizationId, projectId, dataSourceId, fileName, fileType);

        return recordResponse;
    }

    /// <summary>
    ///     This allows the user to query timeseries data in duckDb. Creates the command using an sql string
    ///     The connection is read only so any write operations will be blocked.
    /// </summary>
    /// <param name="currentUserId">ID of the User executing this method.</param>
    /// <param name="request"> The request which includes the query string</param>
    /// <param name="projectId">The project ID</param>
    /// <param name="organizationId">The organization ID</param>
    /// <param name="dataSourceId">The data source ID</param>
    /// ///
    /// <param name="fileType">The type of file to convert query to</param>
    /// <returns></returns>
    public async Task<RecordResponseDto> QueryTimeseries(long currentUserId, TimeseriesQueryRequestDto request,
        long organizationId, long projectId, long dataSourceId, string fileType)
    {

        await ExistenceHelper.EnsureDataSourceExistsForProjectAsync(_context, dataSourceId, projectId);

        var queryId = Guid.NewGuid().ToString();
        string fileName;

        if (fileType == "csv")
            fileName = queryId + "_record.csv";
        else if (fileType == "parquet")
            fileName = queryId + "_record.parquet";
        else
            throw new NotSupportedException($"file type {fileType} not supported");

        var reportClass = await _classBusiness.GetOrCreateClass(
            currentUserId, organizationId, projectId, "Report");
        var timeseriesObjectStorageMethod =
            await _context.ObjectStorages.FirstOrDefaultAsync(os =>
                os.ProjectId == projectId && os.Name == "Timeseries Default");
        if (timeseriesObjectStorageMethod == null)
            throw new KeyNotFoundException("Default timeseries object storage method not found");

        var recordRequest = new CreateRecordRequestDto
        {
            Properties = new JsonObject
            {
                ["status"] = Status.InProgress,
                ["query"] = request.Query
            },
            Name = fileName,
            Description = $"Timeseries result report for {fileName}",
            OriginalId = queryId,
            ClassId = reportClass.Id,
            ClassName = reportClass.Name,
            ObjectStorageId = timeseriesObjectStorageMethod.Id,
            FileType = fileType
        };
        var recordResponse =
            await _recordBusiness.CreateRecord(currentUserId, organizationId, projectId, dataSourceId, recordRequest);

        // meant to run in background so don't await!
        RunBackgroundJob(recordResponse, request.Query, organizationId, projectId, dataSourceId, fileName, fileType);

        return recordResponse;
    }

    private static async Task<DuckDBConnection> GetDuckDbConnection(long organizationId, long projectId, long dataSourceId)
    {
        var baseDir = Path.Combine(_duckDbBasePath, "org_" + organizationId, "project_" + projectId, "datasource_" + dataSourceId);
        Directory.CreateDirectory(baseDir);

        var dbPath = Path.Combine(baseDir, "timeseries.duckdb");
        var connectionString = $"Data Source={dbPath}";

        var connection = new DuckDBConnection(connectionString);
        await connection.OpenAsync();

        return connection;
    }

    private static async Task<DuckDBConnection> GetReadOnlyDuckDbConnection(long organizationId, long projectId, long dataSourceId)
    {
        var baseDir = Path.Combine(_duckDbBasePath, "org_" + organizationId, "project_" + projectId, "datasource_" + dataSourceId);
        var dbPath = Path.Combine(baseDir, "timeseries.duckdb");

        if (!File.Exists(dbPath))
            throw new FileNotFoundException(
                $"DuckDB file not found for project {projectId}, datasource {dataSourceId}: {dbPath}");

        var connectionString = $"Data Source={dbPath};ACCESS_MODE=READ_ONLY";
        var connection = new DuckDBConnection(connectionString);
        await connection.OpenAsync();

        return connection;
    }

    /// <summary>
    ///     Runs a timeseries query to generate a csv from a DataTable
    /// </summary>
    /// <param name="recordResponse">The record response DTO</param>
    /// <param name="query">The timeseries query request DTO</param>
    /// <param name="organizationId">The organization ID</param>
    /// <param name="projectId">The project ID</param>
    /// <param name="dataSourceId">The data source ID</param>
    /// <param name="fileName">The name of the file to be written</param>
    /// <param name="fileType">The type of the file being written</param>
    /// <exception cref="KeyNotFoundException">Thrown when the record cannot be found</exception>
    /// <exception cref="Exception">Thrown if the report cannot be written</exception>
    private async Task RunBackgroundJob(RecordResponseDto recordResponse, string query, long organizationId, long projectId,
        long dataSourceId, string fileName, string fileType)
    {
        // Runs in the background and lets the request finish
        // https://stackoverflow.com/questions/62222712/what-is-the-simplest-way-to-run-a-single-background-task-from-a-controller-in-n
        // todo: Write csv to object storage
        await Task.Run(async () =>
        {
            // creates a background scope to create its own context so that the background task doesn't
            // have to rely on other contexts that may be destroyed or closed.
            using var scope = _serviceScopeFactory.CreateScope();
            var backgroundContext = scope.ServiceProvider.GetRequiredService<DeeplynxContext>();
            using var connection = await GetReadOnlyDuckDbConnection(organizationId, projectId, dataSourceId);
            var folderPath = Path.Combine(_duckDbBasePath, "org_" + organizationId, "project_" + projectId,
                "datasource_" + dataSourceId);
            var filePath = Path.Combine(folderPath, fileName);

            try
            {
                Directory.CreateDirectory(folderPath);

                var command = connection.CreateCommand();

                if (fileType == "csv")
                    command.CommandText = $"COPY ({query}) TO '{filePath}' (HEADER, DELIMITER ',');";
                else if (fileType == "parquet")
                    command.CommandText = $"COPY ({query}) TO '{filePath}' (FORMAT parquet);";

                await command.ExecuteNonQueryAsync();
                await connection.CloseAsync();

                var properties = new JsonObject
                {
                    ["status"] = Status.Completed,
                    ["query"] = query
                };
                var record = await backgroundContext.Records.FindAsync(recordResponse.Id);
                if (record == null || record.ProjectId != projectId || record.IsArchived)
                    throw new KeyNotFoundException($"Record with id {recordResponse.Id} not found");

                record.Properties = properties.ToString();
                record.Uri = filePath;
                record.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

                backgroundContext.Records.Update(record);
                await backgroundContext.SaveChangesAsync();
            }
            catch (Exception e)
            {
                if (File.Exists(filePath)) File.Delete(filePath);

                var properties = new JsonObject
                {
                    ["status"] = Status.Failed,
                    ["error"] = e.Message,
                    ["query"] = query
                };
                var record = await backgroundContext.Records.FindAsync(recordResponse.Id);
                if (record == null || record.ProjectId != projectId || record.IsArchived)
                    throw new KeyNotFoundException($"Record with id {recordResponse.Id} not found");

                record.Properties = properties.ToString();

                backgroundContext.Records.Update(record);
                await backgroundContext.SaveChangesAsync();
                await connection.CloseAsync();

                logger.LogError(e.Message);
                throw new Exception("Failed while writing report to csv and postgres: " + e.Message);
            }
        });
    }

    //todo: Determine how to structure query result depending on how UI needs it. This function will be commented out for now

    /// <summary>
    /// Makes a JSON like response from the table. This will be replaced by a link to the csv later.
    /// </summary>
    /// <param name="dt"> data table with response data</param>
    /// <returns></returns>
    // private List<Dictionary<string, object?>> DataTableToDictionary(DataTable dt)
    // {
    //     var preview = new List<Dictionary<string, object?>>();
    //     foreach (DataRow row in dt.Rows)
    //     {
    //         var rowDict = new Dictionary<string, object?>();
    //         foreach (DataColumn column in dt.Columns)
    //         {
    //             rowDict[column.ColumnName] = row.ItemArray[column.Ordinal];
    //         }
    //         preview.Add(rowDict);
    //     }
    //     return preview;
    // }

    //todo: Determine how to structure preview depending on how UI needs it. This function will be commented out for now

    // private JsonObject DataTableToPreview(DataTable dt)
    // {
    //     var result = new JsonObject();
    //
    //     foreach (DataColumn column in dt.Columns)
    //     {
    //         result[column.ColumnName] = new JsonArray();
    //     }
    //
    //     var maxRows = Math.Min(5, dt.Rows.Count);
    //     for (var i = 0; i < maxRows; i++)
    //     {
    //         var row = dt.Rows[i];
    //         foreach (DataColumn column in dt.Columns)
    //         {
    //             ((JsonArray)result[column.ColumnName]).Add(row[column] == DBNull.Value ? null : row[column]);
    //         }
    //     }
    //
    //     return result;
    // }

    /// <summary>
    ///     Gets all the column names and types from the table
    /// </summary>
    /// <param name="organizationId">The organization ID</param>
    /// <param name="projectId">The project ID</param>
    /// <param name="dataSourceId">The data source ID</param>
    /// <param name="tableName">Timeseries table name</param>
    /// <returns>JSON array of columns</returns>
    private static async Task<JsonArray> GetColumnsFromDb(long organizationId, long projectId, long dataSourceId, string tableName)
    {
        var columns = new JsonArray();
        using var duckDbConnection = await GetDuckDbConnection(organizationId, projectId, dataSourceId);

        await using var command = duckDbConnection.CreateCommand();
        command.CommandText =
            $"SELECT column_name, data_type FROM information_schema.columns WHERE table_name = '{tableName}';";

        using var reader = await command.ExecuteReaderAsync();

        await duckDbConnection.CloseAsync();

        while (reader.Read())
        {
            var columnName = reader[0].ToString();
            var columnType = reader[1].ToString();

            var columnObject = new JsonObject
            {
                ["name"] = columnName,
                ["type"] = columnType
            };
            columns.Add(columnObject);
        }

        return columns;
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

    private static class Status
    {
        public static string Failed { get; } = "failed";
        public static string Completed { get; } = "completed";
        public static string InProgress { get; } = "in progress";
    }
}