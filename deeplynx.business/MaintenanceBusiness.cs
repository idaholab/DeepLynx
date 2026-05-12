using Azure.Storage.Blobs;
using deeplynx.datalayer.Models;
using deeplynx.helpers.exceptions;
using deeplynx.interfaces;
using DotNetEnv;
using DuckDB.NET.Data;
using Microsoft.EntityFrameworkCore;

namespace deeplynx.business;

public class MaintenanceBusiness : IMaintenanceBusiness
{
    private readonly DeeplynxContext _context;
    private readonly FileAzureBusiness _fileAzureBusiness;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MetricsBusiness" /> class.
    /// </summary>
    /// <param name="context">The database context used for database retrieval</param>
    /// <param name="fileBusinessFactory">Factory to create storage-specific file business instances</param>
    public MaintenanceBusiness(
        DeeplynxContext context,
        FileAzureBusiness fileAzureBusiness)
    {
        _context = context;
        _fileAzureBusiness = fileAzureBusiness;
    }
    /// <summary>
    /// Gets the ids of the records that have been uploaded using our old timeseries methods
    /// </summary>
    /// <returns>List of ids</returns>
    public async Task<List<long>> GetTimeseriesRecordIds()
    {
        var recordIds = await _context.Records.Include(r => r.Class)
            .Where(r => r.Class != null && r.Class.Name == "Timeseries"
                                        && r.ObjectStorageId == null
                                        && r.Uri != null 
                                        && r.Uri.Contains("duckdb://"))
            .Select(r => r.Id)
            .ToListAsync();
        
        return recordIds;
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

        try
        {
            Directory.CreateDirectory(folderPath);
    
            await using var readOnlyConnection = await GetReadOnlyDuckDbConnection(organizationId, projectId, datasourceId, duckDbBasePath);

            var command = readOnlyConnection.CreateCommand();

            if (fileExtension == ".csv")
                command.CommandText = $"COPY ({query}) TO '{newFilePath}' (HEADER, DELIMITER ',');";
            else if (fileExtension == ".parquet")
                command.CommandText = $"COPY ({query}) TO '{newFilePath}' (FORMAT parquet);";

            await command.ExecuteNonQueryAsync();

            record.Uri = newFilePath;
            record.ObjectStorageId = instanceDefaultObjectStorageId;

            await _context.SaveChangesAsync();

            // Drop the table using a read-write connection
            var dbPath = Path.Combine(duckDbBasePath, "org_" + organizationId, "project_" + projectId, "datasource_" + datasourceId, "timeseries.duckdb");
            var rwConnectionString = $"Data Source={dbPath}";
            await using var readWriteConnection = new DuckDBConnection(rwConnectionString);
            await readWriteConnection.OpenAsync();

            var dropCommand = readWriteConnection.CreateCommand();
            dropCommand.CommandText = $"DROP TABLE IF EXISTS \"{tableName}\";";
            await dropCommand.ExecuteNonQueryAsync();

            // Check if any tables remain, delete the file if not
            var countCommand = readWriteConnection.CreateCommand();
            countCommand.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'main';";
            var remainingTableCount = Convert.ToInt64(await countCommand.ExecuteScalarAsync());

            if (remainingTableCount == 0 && File.Exists(dbPath))
                File.Delete(dbPath);

            return true;
        }
        catch (Exception ex)
        {
            if (File.Exists(newFilePath))
                File.Delete(newFilePath);

            throw new Exception($"Failed to export record {recordId} to file: {ex.Message}", ex);
        }
    }
    
    private static async Task<DuckDBConnection> GetReadOnlyDuckDbConnection(long organizationId, long projectId,
        long dataSourceId, string duckDbBasePath)
    {
        var baseDir = Path.Combine(duckDbBasePath, "org_" + organizationId, "project_" + projectId,
            "datasource_" + dataSourceId);
        var dbPath = Path.Combine(baseDir, "timeseries.duckdb");

        if (!File.Exists(dbPath))
            throw new FileNotFoundException(
                $"DuckDB file not found for project {projectId}, datasource {dataSourceId}: {dbPath}");

        var connectionString = $"Data Source={dbPath};ACCESS_MODE=READ_ONLY";
        var connection = new DuckDBConnection(connectionString);
        await connection.OpenAsync();

        return connection;
    }
    
    
}