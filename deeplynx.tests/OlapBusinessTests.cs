using System.Text;
using System.Text.Json.Nodes;
using Azure.Storage.Blobs;
using deeplynx.business;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.helpers.BigData;
using deeplynx.helpers.Hubs;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json;
using Parquet;
using Parquet.Data;
using Parquet.Schema;
using Testcontainers.Azurite;
using Record = deeplynx.datalayer.Models.Record;

namespace deeplynx.tests;

// Fixture specifically for this test class only
public class OlapAzuriteFixture : IAsyncLifetime
{
    private AzuriteContainer _azuriteContainer = null!;

    public string AzuriteConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _azuriteContainer = new AzuriteBuilder()
            .WithImage("mcr.microsoft.com/azure-storage/azurite:latest")
            .Build();

        await _azuriteContainer.StartAsync();
        AzuriteConnectionString = _azuriteContainer.GetConnectionString();
    }

    public async Task DisposeAsync()
    {
        await _azuriteContainer.DisposeAsync();
    }
}

[Collection("Test Suite Collection")]
public class OlapBusinessTests : IntegrationTestBase, IClassFixture<OlapAzuriteFixture>
{
    private static readonly string _tempFileSystemBasePath;
    private readonly OlapAzuriteFixture _azuriteFixture;
    private readonly string _containerName = "test-container";
    private long _azureObjectStorageId;
    private ClassBusiness _classBusiness = null!;
    private long _classId;
    private string _connectionString = null!;
    private DataSourceBusiness _dataSourceBusiness = null!;
    private long _dataSourceId;
    private Mock<IEdgeBusiness> _edgeBusiness = null!;
    private EventBusiness _eventBusiness = null!;
    private FileBusiness _fileBusiness = null!;
    private UserBusiness _userBusiness = null!;
    private Mock<IFileBusinessFactory> _fileBusinessFactory = null!;
    private long _fileSystemObjectStorageId;
    private BulkCopyUpsertExecutor _mockBulkCopyUpsertExecutor = null!;
    private Mock<IHubContext<EventNotificationHub>> _mockHubContext = null!;
    private Mock<ILogger<NotificationBusiness>> _mockNotificationLogger = null!;
    private Mock<IRelationshipBusiness> _mockRelationshipBusiness = null!;
    private Mock<IServiceScopeFactory> _mockServiceScopeFactory = null!;
    private Mock<ILogger<OlapBusiness>> _mockTimeseriesLogger = null!;
    private INotificationBusiness _notificationBusiness = null!;
    private ObjectStorageBusiness _objectStorageBusiness = null!;
    private ObjectStorageConfigDto _objectStorageConfig = null!;
    private OlapBusiness _olapBusiness = null!;
    private Mock<IInsightBusiness> _insightBusiness = null!;
    private EncryptionHelper _encryptionHelper = null!;

    private long _organizationId;
    private long _projectId;
    private RecordBusiness _recordBusiness = null!;
    private RelationshipBusiness _relationshipBusiness = null!;
    private SensitivityLabelBusiness _sensitivityLabelBusiness = null!;
    private ISensitivityLabelService _sensitivityLabelService = null!;
    private TagBusiness _tagBusiness = null!;
    private long _userId;
    private const string CsvHeaders = "timestamp,sensor_id,value,temperature,pressure";

    // Static constructor runs before anything else, ensuring env var is set
    // before TimeseriesBusiness static field is initialized
    static OlapBusinessTests()
    {
        _tempFileSystemBasePath = Path.Combine(Path.GetTempPath(), "olap_test");
    }

    public OlapBusinessTests(TestSuiteFixture fixture, OlapAzuriteFixture azuriteFixture) : base(fixture)
    {
        _azuriteFixture = azuriteFixture;
    }

    public override async Task InitializeAsync()
    {
        // Generate valid keys once and reuse them
        // These are pre-generated valid AES-256 keys for testing
        Environment.SetEnvironmentVariable("ENCRYPTION_KEY", "SU5TRUNVUkVfREVWX0tFWV8zMl9CWVRFU19MT05HISE="); // 32 bytes
        Environment.SetEnvironmentVariable("ENCRYPTION_IV", "SU5TRUNVUkVfREVWX0lWIQ=="); // 16 bytes

        _encryptionHelper = new EncryptionHelper();
        await base.InitializeAsync();

        // Set up mocks
        _edgeBusiness = new Mock<IEdgeBusiness>();
        _mockRelationshipBusiness = new Mock<IRelationshipBusiness>();
        _fileBusinessFactory = new Mock<IFileBusinessFactory>();
        _mockHubContext = new Mock<IHubContext<EventNotificationHub>>();
        _mockNotificationLogger = new Mock<ILogger<NotificationBusiness>>();
        _mockTimeseriesLogger = new Mock<ILogger<OlapBusiness>>();
        _mockServiceScopeFactory = new Mock<IServiceScopeFactory>();

        // Set up service scope factory mock
        var mockScope = new Mock<IServiceScope>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        mockServiceProvider.Setup(sp => sp.GetService(typeof(DeeplynxContext))).Returns(Context);
        mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);
        _mockServiceScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);
        _mockBulkCopyUpsertExecutor = new BulkCopyUpsertExecutor();
        _sensitivityLabelService = new SensitivityLabelService(Context);
        _insightBusiness = new Mock<IInsightBusiness>();

        // Set up business layer dependencies
        _objectStorageBusiness = new ObjectStorageBusiness(Context, _encryptionHelper);
        _notificationBusiness =
            new NotificationBusiness(Context, _mockNotificationLogger.Object, _mockHubContext.Object);
        _dataSourceBusiness = new DataSourceBusiness(Context, _edgeBusiness.Object, _recordBusiness, _eventBusiness);
        _eventBusiness = new EventBusiness(Context, _notificationBusiness, _mockBulkCopyUpsertExecutor);
        _classBusiness = new ClassBusiness(Context, _recordBusiness, _mockRelationshipBusiness.Object, _eventBusiness);
        _tagBusiness = new TagBusiness(Context, _eventBusiness);
        _userBusiness = new UserBusiness(Context);
        _sensitivityLabelBusiness = new SensitivityLabelBusiness(Context, _eventBusiness, _userBusiness);
        _recordBusiness = new RecordBusiness(Context, _eventBusiness, _mockBulkCopyUpsertExecutor, _tagBusiness,
            _sensitivityLabelBusiness, _sensitivityLabelService, _fileBusiness);

        // Wire up the real filesystem implementation via the factory mock
        var realFileFilesystemBusiness =
            new FileFilesystemBusiness(Context, _objectStorageBusiness, _classBusiness, _recordBusiness);
        _fileBusinessFactory
            .Setup(x => x.CreateFileBusiness("filesystem"))
            .Returns(realFileFilesystemBusiness);

        // Wire up the real filesystem implementation via the factory mock
        var realFileAzureBusiness = new FileAzureBusiness();
        _fileBusinessFactory
            .Setup(x => x.CreateFileBusiness("azure_object"))
            .Returns(realFileAzureBusiness);

        _olapBusiness = new OlapBusiness(
            Context,
            _recordBusiness,
            _objectStorageBusiness,
            _mockTimeseriesLogger.Object);
        _connectionString = _azuriteFixture.AzuriteConnectionString;

        _fileBusiness = new FileBusiness(
            Context,
            _fileBusinessFactory.Object,
            _dataSourceBusiness,
            _classBusiness,
            _recordBusiness,
            _insightBusiness.Object,
            _olapBusiness,
            _objectStorageBusiness,
            NullLogger<FileBusiness>.Instance
        );
    }

    public override async Task DisposeAsync()
    {
        // Clean up test DuckDB directory
        if (Directory.Exists(_tempFileSystemBasePath))
            try
            {
                Directory.Delete(_tempFileSystemBasePath, true);
            }
            catch
            {
                // Ignore cleanup errors in tests
            }

        var blobServiceClient = new BlobServiceClient(_connectionString);

        // Get all containers
        await foreach (var containerItem in blobServiceClient.GetBlobContainersAsync())
        {
            var container = blobServiceClient.GetBlobContainerClient(containerItem.Name);
            await container.DeleteIfExistsAsync();
        }

        await base.DisposeAsync();
    }

    protected override async Task SeedTestDataAsync()
    {
        await base.SeedTestDataAsync();

        // Create user
        var user = new User
        {
            Name = "Test User",
            Email = "timeseries-test@test.com",
            Password = "test_password",
            IsArchived = false
        };
        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        _userId = user.Id;

        // Create organization
        var org = new Organization { Name = "Test Org" };
        Context.Organizations.Add(org);
        await Context.SaveChangesAsync();
        _organizationId = org.Id;

        // Create project
        var project = new Project
        {
            Name = "Timeseries Test Project",
            Description = "Test project for timeseries unit tests",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = _userId,
            OrganizationId = _organizationId
        };
        Context.Projects.Add(project);
        await Context.SaveChangesAsync();
        _projectId = project.Id;

        // Create data source
        var dataSource = new DataSource
        {
            Name = "Timeseries Test DataSource",
            Description = "Test data source for timeseries unit tests",
            ProjectId = _projectId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = _userId,
            OrganizationId = _organizationId
        };
        Context.DataSources.Add(dataSource);
        await Context.SaveChangesAsync();
        _dataSourceId = dataSource.Id;

        // Create default timeseries class
        var testClass = new Class
        {
            Name = "Timeseries",
            Description = "",
            Uuid = "uuid-1",
            ProjectId = _projectId,
            OrganizationId = _organizationId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = _userId,
            IsArchived = false
        };
        Context.Classes.Add(testClass);
        await Context.SaveChangesAsync();
        _classId = testClass.Id;

        // Create object storage for reports
        var azureConfig = new ObjectStorageConfigDto
        {
            AzureObjectConfig = new AzureObjectConfigDto
            {
                AzureConnectionString = _azuriteFixture.AzuriteConnectionString,
                AzureContainerName = "test-container"
            }
        };
        var azureObjectStorage = new ObjectStorage
        {
            Name = "Azurite",
            Type = "azure_object",
            ConfigEncrypted = _encryptionHelper.SerializeAndEncrypt(azureConfig),
            ProjectId = _projectId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = _userId,
            OrganizationId = _organizationId
        };
        Context.ObjectStorages.Add(azureObjectStorage);
        await Context.SaveChangesAsync();
        _azureObjectStorageId = azureObjectStorage.Id;

        var config = new ObjectStorageConfigDto
        {
            MountPath = _tempFileSystemBasePath
        };
        var objectStorage = new ObjectStorage
        {
            Name = "Test File System",
            Type = "filesystem",
            ConfigEncrypted = _encryptionHelper.SerializeAndEncrypt(config),
            ProjectId = _projectId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = _userId,
            OrganizationId = _organizationId
        };
        Context.ObjectStorages.Add(objectStorage);
        await Context.SaveChangesAsync();
        _fileSystemObjectStorageId = objectStorage.Id;
    }

    private static IFormFile CreateTestCsvFile(string content, string fileName = "test.csv")
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        stream.Position = 0;
        return new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/csv"
        };
    }

    private static async Task<IFormFile> CreateLargeTestParquetFile(
        int rowCount,
        string fileName = "large_test.parquet",
        int timestampOffsetSeconds = 0)
    {
        var baseTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            .AddSeconds(timestampOffsetSeconds);
        var random = new Random(42);

        var schema = new ParquetSchema(
            new DataField<DateTime>("timestamp"),
            new DataField<double>("value"),
            new DataField<string>("sensor_id"),
            new DataField<double>("temperature"),
            new DataField<double>("pressure")
        );

        var timestamps = new DateTime[rowCount];
        var values = new double[rowCount];
        var sensorIds = new string[rowCount];
        var temperatures = new double[rowCount];
        var pressures = new double[rowCount];

        for (var i = 0; i < rowCount; i++)
        {
            timestamps[i] = baseTime.AddSeconds(i);
            values[i] = Math.Round(random.NextDouble() * 100, 2);
            sensorIds[i] = $"sensor_{i % 5}";
            temperatures[i] = Math.Round(20 + random.NextDouble() * 10, 2);
            pressures[i] = Math.Round(1000 + random.NextDouble() * 50, 2);
        }

        // Write to a temp stream, then extract bytes before it's disposed
        byte[] parquetBytes;
        using (var writeStream = new MemoryStream())
        {
            using (var writer = await ParquetWriter.CreateAsync(schema, writeStream))
            {
                using var groupWriter = writer.CreateRowGroup();
                await groupWriter.WriteColumnAsync(new DataColumn(schema.DataFields[0], timestamps));
                await groupWriter.WriteColumnAsync(new DataColumn(schema.DataFields[1], values));
                await groupWriter.WriteColumnAsync(new DataColumn(schema.DataFields[2], sensorIds));
                await groupWriter.WriteColumnAsync(new DataColumn(schema.DataFields[3], temperatures));
                await groupWriter.WriteColumnAsync(new DataColumn(schema.DataFields[4], pressures));
            }

            parquetBytes = writeStream.ToArray(); // safe to call even after ParquetWriter closes the stream
        }

        var readStream = new MemoryStream(parquetBytes);

        return new FormFile(readStream, 0, readStream.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/octet-stream"
        };
    }

    private static IFormFile CreateTestCsvFile(string headers, int rowCount, string fileName = "test.csv")
    {
        var sb = new StringBuilder();
        sb.AppendLine(headers);
        for (var i = 0; i < rowCount; i++)
            sb.AppendLine($"2024-01-01T00:00:0{i},sensor_{i % 5},{i}.{i},{20 + i}.0,{1000 + i}.0");

        return CreateTestCsvFile(sb.ToString(), fileName);
    }

    private async Task<RecordResponseDto> UploadFilesystemCsv(string headers, int rowCount, string fileName = "base.csv")
    {
        var file = CreateTestCsvFile(headers, rowCount, fileName);
        return await _fileBusiness.UploadFile(
            _userId, _organizationId, _projectId, _dataSourceId, _fileSystemObjectStorageId, file);
    }

    private async Task<RecordResponseDto> UploadAzureCsv(string headers, int rowCount, string fileName = "base.csv")
    {
        var file = CreateTestCsvFile(headers, rowCount, fileName);
        return await _fileBusiness.UploadFile(
            _userId, _organizationId, _projectId, _dataSourceId, _azureObjectStorageId, file);
    }

    private static async Task<IFormFile> CreateParquetFileWithSchema(
        ParquetSchema schema,
        Dictionary<string, Array> columnData,
        string fileName)
    {
        byte[] parquetBytes;

        using (var writeStream = new MemoryStream())
        {
            using (var writer = await ParquetWriter.CreateAsync(schema, writeStream))
            {
                using var groupWriter = writer.CreateRowGroup();

                foreach (var field in schema.GetDataFields())
                    await groupWriter.WriteColumnAsync(
                        new DataColumn(field, columnData[field.Name]));
            }

            parquetBytes = writeStream.ToArray();
        }

        var readStream = new MemoryStream(parquetBytes);

        return new FormFile(readStream, 0, readStream.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/octet-stream"
        };
    }

    private static async Task<IFormFile> CreateSchemaMismatchParquetFile_ExtraColumn(
        int rowCount,
        string fileName = "extra_column.parquet")
    {
        var schema = new ParquetSchema(
            new DataField<DateTime>("timestamp"),
            new DataField<double>("value"),
            new DataField<string>("sensor_id"),
            new DataField<double>("temperature"),
            new DataField<double>("pressure"),
            new DataField<string>("unexpected")
        );

        var baseTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var data = new Dictionary<string, Array>
        {
            ["timestamp"] = Enumerable.Range(0, rowCount).Select(i => baseTime.AddSeconds(i)).ToArray(),
            ["value"] = Enumerable.Range(0, rowCount).Select(i => (double)i).ToArray(),
            ["sensor_id"] = Enumerable.Range(0, rowCount).Select(i => $"sensor_{i % 2}").ToArray(),
            ["temperature"] = Enumerable.Range(0, rowCount).Select(i => 20.0 + i).ToArray(),
            ["pressure"] = Enumerable.Range(0, rowCount).Select(i => 1000.0 + i).ToArray(),
            ["unexpected"] = Enumerable.Range(0, rowCount).Select(i => $"x{i}").ToArray()
        };

        return await CreateParquetFileWithSchema(schema, data, fileName);
    }

    private static async Task<IFormFile> CreateSchemaMismatchParquetFile_MissingColumn(
        int rowCount,
        string fileName = "missing_column.parquet")
    {
        var schema = new ParquetSchema(
            new DataField<DateTime>("timestamp"),
            new DataField<double>("value"),
            new DataField<string>("sensor_id"),
            new DataField<double>("temperature")
        );

        var baseTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var data = new Dictionary<string, Array>
        {
            ["timestamp"] = Enumerable.Range(0, rowCount).Select(i => baseTime.AddSeconds(i)).ToArray(),
            ["value"] = Enumerable.Range(0, rowCount).Select(i => (double)i).ToArray(),
            ["sensor_id"] = Enumerable.Range(0, rowCount).Select(i => $"sensor_{i % 2}").ToArray(),
            ["temperature"] = Enumerable.Range(0, rowCount).Select(i => 20.0 + i).ToArray()
        };

        return await CreateParquetFileWithSchema(schema, data, fileName);
    }

    private static async Task<IFormFile> CreateSchemaMismatchParquetFile_TypeMismatch(
        int rowCount,
        string fileName = "type_mismatch.parquet")
    {
        var schema = new ParquetSchema(
            new DataField<DateTime>("timestamp"),
            new DataField<int>("value"),
            new DataField<string>("sensor_id"),
            new DataField<double>("temperature"),
            new DataField<double>("pressure")
        );

        var baseTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var data = new Dictionary<string, Array>
        {
            ["timestamp"] = Enumerable.Range(0, rowCount).Select(i => baseTime.AddSeconds(i)).ToArray(),
            ["value"] = Enumerable.Range(0, rowCount).ToArray(),
            ["sensor_id"] = Enumerable.Range(0, rowCount).Select(i => $"sensor_{i % 2}").ToArray(),
            ["temperature"] = Enumerable.Range(0, rowCount).Select(i => 20.0 + i).ToArray(),
            ["pressure"] = Enumerable.Range(0, rowCount).Select(i => 1000.0 + i).ToArray()
        };

        return await CreateParquetFileWithSchema(schema, data, fileName);
    }

    private async Task<RecordResponseDto> UploadFilesystemParquet(int rowCount, string fileName = "base.parquet")
    {
        var file = await CreateLargeTestParquetFile(rowCount, fileName);
        return await _fileBusiness.UploadFile(
            _userId, _organizationId, _projectId, _dataSourceId, _fileSystemObjectStorageId, file);
    }

    private async Task<RecordResponseDto> UploadAzureParquet(int rowCount, string fileName = "base.parquet")
    {
        var file = await CreateLargeTestParquetFile(rowCount, fileName);
        return await _fileBusiness.UploadFile(
            _userId, _organizationId, _projectId, _dataSourceId, _azureObjectStorageId, file);
    }

    private static long FindColumnIndex(PlotDataDto plotData, string columnName)
    {
        var idx = Array.FindIndex(plotData.Columns, c => c == columnName);
        Assert.True(idx >= 0, $"Column '{columnName}' was not found.");
        return idx;
    }

    private async Task<Record> GetRecordEntity(long recordId)
    {
        var record = await Context.Records.FirstOrDefaultAsync(r => r.Id == recordId);
        Assert.NotNull(record);
        return record!;
    }

    #region Append Tabular Blob

    [Fact]
    public async Task AppendTabularBlob_SecondAppend_Azure_AddsNewPartWithoutChangingUri()
    {
        var result = await UploadAzureParquet(5, "dataset.parquet");

        // First append — migrates to folder
        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, result.Id, 1,
            await CreateLargeTestParquetFile(3, "append1.parquet"));

        var afterFirstAppend = await GetRecordEntity(result.Id);
        var folderUri = afterFirstAppend.Uri!;
        Assert.EndsWith("/", folderUri);

        // Second append — should add part 2 without touching the URI
        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, result.Id, 2,
            await CreateLargeTestParquetFile(4, "append2.parquet"));

        var afterSecondAppend = await GetRecordEntity(result.Id);
        Assert.Equal(folderUri, afterSecondAppend.Uri);

        var blobServiceClient = new BlobServiceClient(_connectionString);
        var container = blobServiceClient.GetBlobContainerClient(_containerName);

        Assert.True(await container.GetBlobClient($"{folderUri}0.parquet").ExistsAsync());
        Assert.True(await container.GetBlobClient($"{folderUri}1.parquet").ExistsAsync());
        Assert.True(await container.GetBlobClient($"{folderUri}2.parquet").ExistsAsync());

        var queryResult = await _olapBusiness.QueryTabularFile(
            _userId, _organizationId, _projectId, result.Id,
            "SELECT COUNT(*) AS total FROM data", "data");

        Assert.Single(queryResult.Data);
        Assert.Equal(12L, Convert.ToInt64(queryResult.Data[0][0]));
    }

    [Fact]
    public async Task AppendTabularBlob_Azure_DuplicatePartOnSecondAppend_LeavesExistingPartsUntouched()
    {
        var result = await UploadAzureParquet(5, "dataset.parquet");

        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, result.Id, 1,
            await CreateLargeTestParquetFile(3, "append1.parquet"));

        var record = await GetRecordEntity(result.Id);
        var folderUri = record.Uri!;

        var testFile = await CreateLargeTestParquetFile(4, "append_dup.parquet");

        // Attempt to append to a part number that already exists
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _olapBusiness.AppendTabularBlob(
                _userId, _organizationId, _projectId, result.Id, 1,
                testFile));

        Assert.Contains("Part 1 already exists", ex.Message);

        // URI must be unchanged
        var recordAfter = await Context.Records.AsNoTracking().FirstAsync(r => r.Id == result.Id);
        Assert.Equal(folderUri, recordAfter.Uri);

        var blobServiceClient = new BlobServiceClient(_connectionString);
        var container = blobServiceClient.GetBlobContainerClient(_containerName);

        // Existing parts must still be intact, no spurious part 2 written
        Assert.True(await container.GetBlobClient($"{folderUri}0.parquet").ExistsAsync());
        Assert.True(await container.GetBlobClient($"{folderUri}1.parquet").ExistsAsync());
        Assert.False(await container.GetBlobClient($"{folderUri}2.parquet").ExistsAsync());

        // Row count must be unchanged
        var queryResult = await _olapBusiness.QueryTabularFile(
            _userId, _organizationId, _projectId, result.Id,
            "SELECT COUNT(*) AS total FROM data", "data");

        Assert.Equal(8L, Convert.ToInt64(queryResult.Data[0][0]));
    }

    [Fact]
    public async Task AppendTabularBlob_Azure_SchemaMismatch_ExtraColumn_LeavesOriginalBlobIntact()
    {
        var result = await UploadAzureParquet(5, "dataset.parquet");
        var originalRecord = await Context.Records.AsNoTracking().FirstAsync(r => r.Id == result.Id);
        var originalUri = originalRecord.Uri!;

        var appendFile = await CreateSchemaMismatchParquetFile_ExtraColumn(3);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _olapBusiness.AppendTabularBlob(_userId, _organizationId, _projectId, result.Id, 1, appendFile));

        Assert.Contains("unexpected column", ex.Message);

        var recordAfter = await Context.Records.AsNoTracking().FirstAsync(r => r.Id == result.Id);
        Assert.Equal(originalUri, recordAfter.Uri);

        var blobServiceClient = new BlobServiceClient(_connectionString);
        var container = blobServiceClient.GetBlobContainerClient(_containerName);

        Assert.True(await container.GetBlobClient(originalUri).ExistsAsync());

        var folderPrefix = originalUri[..^".parquet".Length] + "/";
        Assert.False(await container.GetBlobClient($"{folderPrefix}0.parquet").ExistsAsync());
        Assert.False(await container.GetBlobClient($"{folderPrefix}1.parquet").ExistsAsync());
    }

    [Fact]
    public async Task AppendTabularBlob_Azure_SchemaMismatch_MissingColumn_LeavesOriginalBlobIntact()
    {
        var result = await UploadAzureParquet(5, "dataset.parquet");
        var originalRecord = await Context.Records.AsNoTracking().FirstAsync(r => r.Id == result.Id);
        var originalUri = originalRecord.Uri!;

        var appendFile = await CreateSchemaMismatchParquetFile_MissingColumn(3);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _olapBusiness.AppendTabularBlob(_userId, _organizationId, _projectId, result.Id, 1, appendFile));

        Assert.Contains("missing column", ex.Message);

        var recordAfter = await Context.Records.AsNoTracking().FirstAsync(r => r.Id == result.Id);
        Assert.Equal(originalUri, recordAfter.Uri);

        var blobServiceClient = new BlobServiceClient(_connectionString);
        var container = blobServiceClient.GetBlobContainerClient(_containerName);

        Assert.True(await container.GetBlobClient(originalUri).ExistsAsync());

        var folderPrefix = originalUri[..^".parquet".Length] + "/";
        Assert.False(await container.GetBlobClient($"{folderPrefix}0.parquet").ExistsAsync());
    }

    [Fact]
    public async Task AppendTabularBlob_Azure_SchemaMismatch_TypeMismatch_LeavesOriginalBlobIntact()
    {
        var result = await UploadAzureParquet(5, "dataset.parquet");
        var originalRecord = await Context.Records.AsNoTracking().FirstAsync(r => r.Id == result.Id);
        var originalUri = originalRecord.Uri!;

        var appendFile = await CreateSchemaMismatchParquetFile_TypeMismatch(3);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _olapBusiness.AppendTabularBlob(_userId, _organizationId, _projectId, result.Id, 1, appendFile));

        Assert.Contains("type mismatch", ex.Message);

        var recordAfter = await Context.Records.AsNoTracking().FirstAsync(r => r.Id == result.Id);
        Assert.Equal(originalUri, recordAfter.Uri);

        var blobServiceClient = new BlobServiceClient(_connectionString);
        var container = blobServiceClient.GetBlobContainerClient(_containerName);

        Assert.True(await container.GetBlobClient(originalUri).ExistsAsync());

        var folderPrefix = originalUri[..^".parquet".Length] + "/";
        Assert.False(await container.GetBlobClient($"{folderPrefix}0.parquet").ExistsAsync());
    }

    [Fact]
    public async Task AppendTabularBlob_FirstAppend_Filesystem_MigratesToFolderAndQueriesAllRows()
    {
        var baseRows = 5;
        var appendRows = 3;

        var result = await UploadFilesystemParquet(baseRows, "dataset.parquet");
        var appendFile = await CreateLargeTestParquetFile(appendRows, "append.parquet");

        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, result.Id, 1, appendFile);

        var record = await GetRecordEntity(result.Id);

        Assert.EndsWith(Path.DirectorySeparatorChar.ToString(), record.Uri);
        Assert.True(Directory.Exists(record.Uri));

        var part0 = Path.Combine(record.Uri!, "0.parquet");
        var part1 = Path.Combine(record.Uri!, "1.parquet");

        Assert.True(File.Exists(part0));
        Assert.True(File.Exists(part1));

        var queryResult = await _olapBusiness.QueryTabularFile(
            _userId, _organizationId, _projectId, result.Id,
            "SELECT * FROM data", "data");

        Assert.Equal(baseRows + appendRows, queryResult.Data.Length);
        Assert.Equal(5, queryResult.Columns.Length);
    }

    [Fact]
    public async Task AppendTabularBlob_SecondAppend_Filesystem_AddsNewPartWithoutChangingUri()
    {
        var result = await UploadFilesystemParquet(5, "dataset.parquet");

        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, result.Id, 1,
            await CreateLargeTestParquetFile(3, "append1.parquet"));

        var afterFirstAppend = await GetRecordEntity(result.Id);
        var folderUri = afterFirstAppend.Uri!;

        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, result.Id, 2,
            await CreateLargeTestParquetFile(4, "append2.parquet"));

        var afterSecondAppend = await GetRecordEntity(result.Id);

        Assert.Equal(folderUri, afterSecondAppend.Uri);
        Assert.True(File.Exists(Path.Combine(folderUri, "0.parquet")));
        Assert.True(File.Exists(Path.Combine(folderUri, "1.parquet")));
        Assert.True(File.Exists(Path.Combine(folderUri, "2.parquet")));

        var queryResult = await _olapBusiness.QueryTabularFile(
            _userId, _organizationId, _projectId, result.Id,
            "SELECT COUNT(*) AS total FROM data", "data");

        Assert.Single(queryResult.Data);
        Assert.Equal(12L, Convert.ToInt64(queryResult.Data[0][0]));
    }

    [Fact]
    public async Task AppendTabularBlob_FirstAppend_Azure_MigratesToFolderAndQueriesAllRows()
    {
        var baseRows = 5;
        var appendRows = 3;

        var result = await UploadAzureParquet(baseRows, "dataset.parquet");
        var appendFile = await CreateLargeTestParquetFile(appendRows, "append.parquet");

        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, result.Id, 1, appendFile);

        var record = await GetRecordEntity(result.Id);

        Assert.EndsWith("/", record.Uri);

        var blobServiceClient = new BlobServiceClient(_connectionString);
        var container = blobServiceClient.GetBlobContainerClient(_containerName);

        Assert.True(await container.GetBlobClient($"{record.Uri}0.parquet").ExistsAsync());
        Assert.True(await container.GetBlobClient($"{record.Uri}1.parquet").ExistsAsync());
        Assert.False(await container.GetBlobClient("dataset.parquet").ExistsAsync());

        var queryResult = await _olapBusiness.QueryTabularFile(
            _userId, _organizationId, _projectId, result.Id,
            "SELECT * FROM data", "data");

        Assert.Equal(baseRows + appendRows, queryResult.Data.Length);
        Assert.Equal(5, queryResult.Columns.Length);
    }

    [Fact]
    public async Task AppendTabularBlob_Azure_UpdatesLastUpdatedAt()
    {
        var result = await UploadAzureParquet(5, "dataset.parquet");
        var initUpdatedAt = result.LastUpdatedAt;

        // for mitigating potential low precision failures
        await Task.Delay(1000);

        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, result.Id, 1,
            await CreateLargeTestParquetFile(3, "append1.parquet"));

        var afterAppend = await GetRecordEntity(result.Id);
        var afterUpdatedAt = afterAppend.LastUpdatedAt;

        Assert.True(initUpdatedAt < afterUpdatedAt);
    }

    [Fact]
    public async Task AppendTabularBlob_PartZero_Throws()
    {
        var result = await UploadFilesystemParquet(5, "dataset.parquet");
        var appendFile = await CreateLargeTestParquetFile(3, "append.parquet");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _olapBusiness.AppendTabularBlob(_userId, _organizationId, _projectId, result.Id, 0, appendFile));

        Assert.Contains("Part number 0 is reserved", ex.Message);
    }

    [Fact]
    public async Task AppendTabularBlob_NonParquet_Throws()
    {
        var result = await UploadFilesystemParquet(5, "dataset.parquet");
        var csv = CreateTestCsvFile("timestamp,value\n2024-01-01T00:00:00,1", "append.csv");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _olapBusiness.AppendTabularBlob(_userId, _organizationId, _projectId, result.Id, 1, csv));

        Assert.Contains("File types differ: csv/parquet", ex.Message);
    }

    [Fact]
    public async Task AppendTabularBlob_EmptyFile_Throws()
    {
        var result = await UploadFilesystemParquet(5, "dataset.parquet");

        var stream = new MemoryStream();
        var empty = new FormFile(stream, 0, 0, "file", "empty.parquet")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/octet-stream"
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _olapBusiness.AppendTabularBlob(_userId, _organizationId, _projectId, result.Id, 1, empty));

        Assert.Contains("Cannot append an empty file", ex.Message);
    }

    [Fact]
    public async Task AppendTabularBlob_RecordDoesNotExist_Throws()
    {
        var appendFile = await CreateLargeTestParquetFile(3, "append.parquet");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _olapBusiness.AppendTabularBlob(_userId, _organizationId, _projectId, -999, 1, appendFile));

        Assert.Contains("does not exist", ex.Message);
    }

    [Fact]
    public async Task AppendTabularBlob_FileTypeMismatch_Throws()
    {
        var csv = CreateTestCsvFile("timestamp,value\n2024-01-01T00:00:00,1", "base.csv");
        var uploaded = await _fileBusiness.UploadFile(
            _userId, _organizationId, _projectId, _dataSourceId, _fileSystemObjectStorageId, csv);

        var appendFile = await CreateLargeTestParquetFile(3, "append.parquet");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _olapBusiness.AppendTabularBlob(_userId, _organizationId, _projectId, uploaded.Id, 1, appendFile));

        Assert.Contains("File types differ", ex.Message);
    }

    [Fact]
    public async Task AppendTabularBlob_RecordUriMissing_Throws()
    {
        var result = await UploadFilesystemParquet(5, "dataset.parquet");

        var record = await GetRecordEntity(result.Id);
        record.Uri = null;
        await Context.SaveChangesAsync();

        var appendFile = await CreateLargeTestParquetFile(3, "append.parquet");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _olapBusiness.AppendTabularBlob(_userId, _organizationId, _projectId, result.Id, 1, appendFile));

        Assert.Contains("Record has no URI", ex.Message);
    }

    [Fact]
    public async Task AppendTabularBlob_FirstAppend_DuplicatePart_CleansUpAndLeavesOriginalIntact()
    {
        var result = await UploadFilesystemParquet(5, "dataset.parquet");
        var originalRecord = await Context.Records.AsNoTracking().FirstAsync(r => r.Id == result.Id);
        var originalUri = originalRecord.Uri!;

        var expectedFolder = originalUri[..^".parquet".Length] + Path.DirectorySeparatorChar;
        Directory.CreateDirectory(expectedFolder);
        File.WriteAllBytes(Path.Combine(expectedFolder, "1.parquet"), new byte[] { 1, 2, 3 });

        var appendFile = await CreateLargeTestParquetFile(3, "append.parquet");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _olapBusiness.AppendTabularBlob(_userId, _organizationId, _projectId, result.Id, 1, appendFile));

        Assert.Contains("Part 1 already exists", ex.Message);

        var recordAfter = await Context.Records.AsNoTracking().FirstAsync(r => r.Id == result.Id);
        Assert.Equal(originalUri, recordAfter.Uri);

        Assert.True(File.Exists(originalUri));
        Assert.False(File.Exists(Path.Combine(expectedFolder, "0.parquet")));
        Assert.True(File.Exists(Path.Combine(expectedFolder, "1.parquet")));
    }

    [Fact]
    public async Task AppendTabularBlob_ExistingFolder_DuplicatePart_LeavesExistingPartsUntouched()
    {
        var result = await UploadFilesystemParquet(5, "dataset.parquet");

        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, result.Id, 1,
            await CreateLargeTestParquetFile(3, "append1.parquet"));

        var record = await GetRecordEntity(result.Id);
        var folderUri = record.Uri!;

        var testFile = await CreateLargeTestParquetFile(4, "append_duplicate.parquet");
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _olapBusiness.AppendTabularBlob(
                _userId, _organizationId, _projectId, result.Id, 1,
                testFile));

        Assert.Contains("Part 1 already exists", ex.Message);

        Assert.True(File.Exists(Path.Combine(folderUri, "0.parquet")));
        Assert.True(File.Exists(Path.Combine(folderUri, "1.parquet")));
        Assert.False(File.Exists(Path.Combine(folderUri, "2.parquet")));

        var queryResult = await _olapBusiness.QueryTabularFile(
            _userId, _organizationId, _projectId, result.Id,
            "SELECT COUNT(*) AS total FROM data", "data");

        Assert.Equal(8L, Convert.ToInt64(queryResult.Data[0][0]));
    }

    [Fact]
    public async Task AppendTabularBlob_SchemaMismatch_ExtraColumn_DoesNotMutateState()
    {
        var result = await UploadFilesystemParquet(5, "dataset.parquet");
        var originalRecord = await Context.Records.AsNoTracking().FirstAsync(r => r.Id == result.Id);
        var originalUri = originalRecord.Uri!;

        var appendFile = await CreateSchemaMismatchParquetFile_ExtraColumn(3);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _olapBusiness.AppendTabularBlob(_userId, _organizationId, _projectId, result.Id, 1, appendFile));

        Assert.Contains("unexpected column", ex.Message);

        var recordAfter = await Context.Records.AsNoTracking().FirstAsync(r => r.Id == result.Id);
        Assert.Equal(originalUri, recordAfter.Uri);
        Assert.True(File.Exists(originalUri));

        var expectedFolder = originalUri[..^".parquet".Length] + Path.DirectorySeparatorChar;
        Assert.False(Directory.Exists(expectedFolder));
    }

    [Fact]
    public async Task AppendTabularBlob_SchemaMismatch_MissingColumn_ThrowsAndDoesNotMutateState()
    {
        var result = await UploadFilesystemParquet(5, "dataset.parquet");
        var originalRecord = await Context.Records.AsNoTracking().FirstAsync(r => r.Id == result.Id);
        var originalUri = originalRecord.Uri!;

        var appendFile = await CreateSchemaMismatchParquetFile_MissingColumn(3);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _olapBusiness.AppendTabularBlob(_userId, _organizationId, _projectId, result.Id, 1, appendFile));

        Assert.Contains("missing column", ex.Message);

        var recordAfter = await Context.Records.AsNoTracking().FirstAsync(r => r.Id == result.Id);
        Assert.Equal(originalUri, recordAfter.Uri);
        Assert.True(File.Exists(originalUri));
    }

    [Fact]
    public async Task AppendTabularBlob_SchemaMismatch_TypeMismatch_ThrowsAndDoesNotMutateState()
    {
        var result = await UploadFilesystemParquet(5, "dataset.parquet");
        var originalRecord = await Context.Records.AsNoTracking().FirstAsync(r => r.Id == result.Id);
        var originalUri = originalRecord.Uri!;

        var appendFile = await CreateSchemaMismatchParquetFile_TypeMismatch(3);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _olapBusiness.AppendTabularBlob(_userId, _organizationId, _projectId, result.Id, 1, appendFile));

        Assert.Contains("type mismatch", ex.Message);

        var recordAfter = await Context.Records.AsNoTracking().FirstAsync(r => r.Id == result.Id);
        Assert.Equal(originalUri, recordAfter.Uri);
        Assert.True(File.Exists(originalUri));
    }

    [Fact]
    public async Task AppendTabularBlob_Azure_DuplicatePartOnFirstAppend_LeavesOriginalBlobIntact()
    {
        var result = await UploadAzureParquet(5, "dataset.parquet");

        var originalRecord = await Context.Records.AsNoTracking().FirstAsync(r => r.Id == result.Id);
        var originalUri = originalRecord.Uri!;
        Assert.EndsWith(".parquet", originalUri);

        var blobServiceClient = new BlobServiceClient(_connectionString);
        var container = blobServiceClient.GetBlobContainerClient(_containerName);
        await container.CreateIfNotExistsAsync();

        var folderPrefix = originalUri[..^".parquet".Length] + "/";
        var preCreatedPartUri = $"{folderPrefix}1.parquet";

        var preCreatedPart = container.GetBlobClient(preCreatedPartUri);
        await preCreatedPart.UploadAsync(
            BinaryData.FromBytes(new byte[] { 1, 2, 3 }).ToStream(),
            true);

        var appendFile = await CreateLargeTestParquetFile(3, "append.parquet");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _olapBusiness.AppendTabularBlob(_userId, _organizationId, _projectId, result.Id, 1, appendFile));

        Assert.Contains("Part 1 already exists", ex.Message);

        var recordAfter = await Context.Records.AsNoTracking().FirstAsync(r => r.Id == result.Id);
        Assert.Equal(originalUri, recordAfter.Uri);

        Assert.True(await container.GetBlobClient(originalUri).ExistsAsync());
        Assert.False(await container.GetBlobClient($"{folderPrefix}0.parquet").ExistsAsync());
        Assert.True(await container.GetBlobClient($"{folderPrefix}1.parquet").ExistsAsync());
    }

    // ── Happy path ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AppendTabularBlob_FirstAppend_Filesystem_Csv_MigratesToFolderAndQueriesAllRows()
    {
        var result = await UploadFilesystemCsv(CsvHeaders, 5, "dataset.csv");
        var appendFile = CreateTestCsvFile(CsvHeaders, 3, "append.csv");

        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, result.Id, 1, appendFile);

        var record = await GetRecordEntity(result.Id);
        Assert.EndsWith(Path.DirectorySeparatorChar.ToString(), record.Uri);
        Assert.True(File.Exists(Path.Combine(record.Uri!, "0.csv")));
        Assert.True(File.Exists(Path.Combine(record.Uri!, "1.csv")));

        var queryResult = await _olapBusiness.QueryTabularFile(
            _userId, _organizationId, _projectId, result.Id,
            "SELECT * FROM data", "data");

        Assert.Equal(8, queryResult.Data.Length); // 5 + 3
    }

    [Fact]
    public async Task AppendTabularBlob_SecondAppend_Filesystem_Csv_AddsNewPartWithoutChangingUri()
    {
        var result = await UploadFilesystemCsv(CsvHeaders, 5, "dataset.csv");

        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, result.Id, 1,
            CreateTestCsvFile(CsvHeaders, 3, "append1.csv"));

        var afterFirstAppend = await GetRecordEntity(result.Id);
        var folderUri = afterFirstAppend.Uri!;

        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, result.Id, 2,
            CreateTestCsvFile(CsvHeaders, 4, "append2.csv"));

        var afterSecondAppend = await GetRecordEntity(result.Id);
        Assert.Equal(folderUri, afterSecondAppend.Uri);
        Assert.True(File.Exists(Path.Combine(folderUri, "0.csv")));
        Assert.True(File.Exists(Path.Combine(folderUri, "1.csv")));
        Assert.True(File.Exists(Path.Combine(folderUri, "2.csv")));

        var queryResult = await _olapBusiness.QueryTabularFile(
            _userId, _organizationId, _projectId, result.Id,
            "SELECT COUNT(*) AS total FROM data", "data");

        Assert.Equal(12L, Convert.ToInt64(queryResult.Data[0][0]));
    }

    [Fact]
    public async Task AppendTabularBlob_FirstAppend_Azure_Csv_MigratesToFolderAndQueriesAllRows()
    {
        var result = await UploadAzureCsv(CsvHeaders, 5, "dataset.csv");
        var appendFile = CreateTestCsvFile(CsvHeaders, 3, "append.csv");

        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, result.Id, 1, appendFile);

        var record = await GetRecordEntity(result.Id);
        Assert.EndsWith("/", record.Uri);

        var container = new BlobServiceClient(_connectionString).GetBlobContainerClient(_containerName);
        Assert.True(await container.GetBlobClient($"{record.Uri}0.csv").ExistsAsync());
        Assert.True(await container.GetBlobClient($"{record.Uri}1.csv").ExistsAsync());
        Assert.False(await container.GetBlobClient("dataset.csv").ExistsAsync());

        var queryResult = await _olapBusiness.QueryTabularFile(
            _userId, _organizationId, _projectId, result.Id,
            "SELECT * FROM data", "data");

        Assert.Equal(8, queryResult.Data.Length);
    }

    [Fact]
    public async Task AppendTabularBlob_SecondAppend_Azure_Csv_AddsNewPartWithoutChangingUri()
    {
        var result = await UploadAzureCsv(CsvHeaders, 5, "dataset.csv");

        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, result.Id, 1,
            CreateTestCsvFile(CsvHeaders, 3, "append1.csv"));

        var afterFirstAppend = await GetRecordEntity(result.Id);
        var folderUri = afterFirstAppend.Uri!;
        Assert.EndsWith("/", folderUri);

        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, result.Id, 2,
            CreateTestCsvFile(CsvHeaders, 4, "append2.csv"));

        var afterSecondAppend = await GetRecordEntity(result.Id);
        Assert.Equal(folderUri, afterSecondAppend.Uri);

        var container = new BlobServiceClient(_connectionString).GetBlobContainerClient(_containerName);
        Assert.True(await container.GetBlobClient($"{folderUri}0.csv").ExistsAsync());
        Assert.True(await container.GetBlobClient($"{folderUri}1.csv").ExistsAsync());
        Assert.True(await container.GetBlobClient($"{folderUri}2.csv").ExistsAsync());

        var queryResult = await _olapBusiness.QueryTabularFile(
            _userId, _organizationId, _projectId, result.Id,
            "SELECT COUNT(*) AS total FROM data", "data");

        Assert.Equal(12L, Convert.ToInt64(queryResult.Data[0][0]));
    }

    // ── Schema validation ─────────────────────────────────────────────────────────

    [Fact]
    public async Task AppendTabularBlob_Csv_ExtraColumn_LeavesOriginalIntact()
    {
        var result = await UploadFilesystemCsv(CsvHeaders, 5, "dataset.csv");
        var originalRecord = await Context.Records.AsNoTracking().FirstAsync(r => r.Id == result.Id);
        var originalUri = originalRecord.Uri!;

        var appendFile = CreateTestCsvFile("timestamp,sensor_id,value,temperature,pressure,unexpected", 3, "extra_col.csv");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _olapBusiness.AppendTabularBlob(_userId, _organizationId, _projectId, result.Id, 1, appendFile));

        Assert.Contains("unexpected column", ex.Message);

        var recordAfter = await Context.Records.AsNoTracking().FirstAsync(r => r.Id == result.Id);
        Assert.Equal(originalUri, recordAfter.Uri);
        Assert.True(File.Exists(originalUri));

        var folderPrefix = originalUri[..^".csv".Length] + Path.DirectorySeparatorChar;
        Assert.False(File.Exists(Path.Combine(folderPrefix, "0.csv")));
    }

    [Fact]
    public async Task AppendTabularBlob_Csv_MissingColumn_LeavesOriginalIntact()
    {
        var result = await UploadFilesystemCsv(CsvHeaders, 5, "dataset.csv");
        var originalRecord = await Context.Records.AsNoTracking().FirstAsync(r => r.Id == result.Id);
        var originalUri = originalRecord.Uri!;

        var appendFile = CreateTestCsvFile("timestamp,sensor_id,value", 3, "missing_col.csv");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _olapBusiness.AppendTabularBlob(_userId, _organizationId, _projectId, result.Id, 1, appendFile));

        Assert.Contains("missing column", ex.Message);

        var recordAfter = await Context.Records.AsNoTracking().FirstAsync(r => r.Id == result.Id);
        Assert.Equal(originalUri, recordAfter.Uri);
        Assert.True(File.Exists(originalUri));
    }

    [Fact]
    public async Task AppendTabularBlob_Csv_NoStoredColumns_Throws()
    {
        var result = await UploadFilesystemCsv(CsvHeaders, 5, "dataset.csv");

        var record = await GetRecordEntity(result.Id);
        record.Properties = "{}";
        await Context.SaveChangesAsync();

        var appendFile = CreateTestCsvFile(CsvHeaders, 3, "append.csv");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _olapBusiness.AppendTabularBlob(_userId, _organizationId, _projectId, result.Id, 1, appendFile));

        Assert.Contains("no stored column schema", ex.Message);
    }

    // ── Duplicate part ────────────────────────────────────────────────────────────

    [Fact]
    public async Task AppendTabularBlob_Csv_DuplicatePartOnSecondAppend_LeavesExistingPartsUntouched()
    {
        var result = await UploadFilesystemCsv(CsvHeaders, 5, "dataset.csv");

        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, result.Id, 1,
            CreateTestCsvFile(CsvHeaders, 3, "append1.csv"));

        var record = await GetRecordEntity(result.Id);
        var folderUri = record.Uri!;

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _olapBusiness.AppendTabularBlob(
                _userId, _organizationId, _projectId, result.Id, 1,
                CreateTestCsvFile(CsvHeaders, 4, "append_dup.csv")));

        Assert.Contains("Part 1 already exists", ex.Message);

        Assert.True(File.Exists(Path.Combine(folderUri, "0.csv")));
        Assert.True(File.Exists(Path.Combine(folderUri, "1.csv")));
        Assert.False(File.Exists(Path.Combine(folderUri, "2.csv")));

        var queryResult = await _olapBusiness.QueryTabularFile(
            _userId, _organizationId, _projectId, result.Id,
            "SELECT COUNT(*) AS total FROM data", "data");

        Assert.Equal(8L, Convert.ToInt64(queryResult.Data[0][0]));
    }

    #endregion

    #region Query Tabular File

    [Fact]
    public async Task QueryTabularFile_AppendedCsvFolder_Filesystem_ReadsAcrossAllParts()
    {
        var result = await UploadFilesystemCsv(CsvHeaders, 5, "dataset.csv");

        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, result.Id, 1,
            CreateTestCsvFile(CsvHeaders, 3, "append1.csv"));

        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, result.Id, 2,
            CreateTestCsvFile(CsvHeaders, 2, "append2.csv"));

        var queryResult = await _olapBusiness.QueryTabularFile(
            _userId, _organizationId, _projectId, result.Id,
            "SELECT COUNT(*) AS total FROM data", "data");

        Assert.Single(queryResult.Data);
        Assert.Equal(10L, Convert.ToInt64(queryResult.Data[0][0]));
    }

    [Fact]
    public async Task QueryTabularFile_AppendedCsvFolder_Filesystem_CorrectColumnCount()
    {
        var result = await UploadFilesystemCsv(CsvHeaders, 5, "dataset.csv");

        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, result.Id, 1,
            CreateTestCsvFile(CsvHeaders, 3, "append1.csv"));

        var queryResult = await _olapBusiness.QueryTabularFile(
            _userId, _organizationId, _projectId, result.Id,
            "SELECT * FROM data", "data");

        Assert.Equal(8, queryResult.Data.Length);
        Assert.Equal(5, queryResult.Columns.Length);
    }

    [Fact]
    public async Task QueryTabularFile_AppendedCsvFolder_Azure_ReadsAcrossAllParts()
    {
        var result = await UploadAzureCsv(CsvHeaders, 5, "dataset.csv");

        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, result.Id, 1,
            CreateTestCsvFile(CsvHeaders, 3, "append1.csv"));

        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, result.Id, 2,
            CreateTestCsvFile(CsvHeaders, 2, "append2.csv"));

        var queryResult = await _olapBusiness.QueryTabularFile(
            _userId, _organizationId, _projectId, result.Id,
            "SELECT COUNT(*) AS total FROM data", "data");

        Assert.Single(queryResult.Data);
        Assert.Equal(10L, Convert.ToInt64(queryResult.Data[0][0]));
    }

    [Fact]
    public async Task QueryTabularFile_SingleCsv_Success_ReturnsData()
    {
        var csvContent =
            "timestamp,value,sensor_id,temperature,pressure\n" +
            "2024-01-01T00:00:00,42.5,sensor_1,21.1,1001.2\n" +
            "2024-01-01T00:01:00,43.2,sensor_2,21.3,1001.4\n";

        var file = CreateTestCsvFile(csvContent, "sensor_data.csv");

        var result = await _fileBusiness.UploadFile(
            _userId, _organizationId, _projectId, _dataSourceId, _fileSystemObjectStorageId, file);

        var queryResult = await _olapBusiness.QueryTabularFile(
            _userId, _organizationId, _projectId, result.Id,
            "SELECT * FROM data", "data");

        Assert.NotNull(queryResult);
        Assert.Equal(2, queryResult.Data.Length);
        Assert.Equal(5, queryResult.Columns.Length);
    }

    [Fact]
    public async Task QueryTabularFile_AppendedFolder_Success_ReadsAcrossAllParts()
    {
        var result = await UploadFilesystemParquet(5, "dataset.parquet");

        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, result.Id, 1,
            await CreateLargeTestParquetFile(3, "append1.parquet"));

        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, result.Id, 2,
            await CreateLargeTestParquetFile(4, "append2.parquet"));

        var queryResult = await _olapBusiness.QueryTabularFile(
            _userId, _organizationId, _projectId, result.Id,
            "SELECT COUNT(*) AS total FROM data", "data");

        Assert.Single(queryResult.Data);
        Assert.Equal(12L, Convert.ToInt64(queryResult.Data[0][0]));
    }

    [Fact]
    public async Task QueryTabularFile_EmptyViewName_Throws()
    {
        var result = await UploadFilesystemParquet(5, "dataset.parquet");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _olapBusiness.QueryTabularFile(
                _userId, _organizationId, _projectId, result.Id,
                "SELECT * FROM data", ""));

        Assert.Contains("View name is required", ex.Message);
    }

    [Fact]
    public async Task QueryTabularFile_EmptyQuery_Throws()
    {
        var result = await UploadFilesystemParquet(5, "dataset.parquet");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _olapBusiness.QueryTabularFile(
                _userId, _organizationId, _projectId, result.Id,
                "", "data"));

        Assert.Contains("Query is required", ex.Message);
    }

    [Fact]
    public async Task QueryTabularFile_QueryDoesNotReferenceView_Throws()
    {
        var result = await UploadFilesystemParquet(5, "dataset.parquet");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _olapBusiness.QueryTabularFile(
                _userId, _organizationId, _projectId, result.Id,
                "SELECT 1", "data"));

        Assert.Contains("must reference the view", ex.Message);
    }

    [Fact]
    public async Task QueryTabularFile_InvalidViewName_Throws()
    {
        var result = await UploadFilesystemParquet(5, "dataset.parquet");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _olapBusiness.QueryTabularFile(
                _userId, _organizationId, _projectId, result.Id,
                "SELECT * FROM bad_name", "bad-name"));

        Assert.Contains("Query must reference the view", ex.Message);
    }

    [Theory]
    [InlineData("SELECT * FROM data;")]
    [InlineData("COPY data TO 'x.csv'")]
    [InlineData("SELECT * FROM read_parquet('abc.parquet')")]
    [InlineData("DELETE FROM data")]
    [InlineData("ATTACH 'foo.db'")]
    public async Task QueryTabularFile_DangerousOrMultiStatementQuery_Throws(string query)
    {
        var result = await UploadFilesystemParquet(5, "dataset.parquet");

        await Assert.ThrowsAnyAsync<Exception>(() =>
            _olapBusiness.QueryTabularFile(
                _userId, _organizationId, _projectId, result.Id,
                query, "data"));
    }

    [Fact]
    public async Task QueryTabularFile_RecordDoesNotExist_Throws()
    {
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _olapBusiness.QueryTabularFile(
                _userId, _organizationId, _projectId, -999,
                "SELECT * FROM data", "data"));
    }

    [Fact]
    public async Task QueryTabularFile_SelectSpecificColumns_ReturnsExpectedProjection()
    {
        var result = await UploadFilesystemParquet(5, "dataset.parquet");

        var queryResult = await _olapBusiness.QueryTabularFile(
            _userId, _organizationId, _projectId, result.Id,
            "SELECT sensor_id, value FROM data", "data");

        Assert.Equal(2, queryResult.Columns.Length);
        Assert.Equal("sensor_id", queryResult.Columns[0]);
        Assert.Equal("value", queryResult.Columns[1]);
        Assert.Equal(5, queryResult.Data.Length);
    }

    [Fact]
    public async Task QueryTabularFile_RequestDto_WindowAndColumnsWithoutQuery_ReturnsRequestedProjection()
    {
        var result = await UploadFilesystemParquet(10, "dataset.parquet");

        var queryResult = await _olapBusiness.QueryTabularFile(
            _userId,
            _organizationId,
            _projectId,
            result.Id,
            new OlapQueryRequestDto
            {
                StartRow = 3,
                StopRow = 6,
                RowStride = 2,
                Columns = ["timestamp", "value"]
            },
            "data");

        Assert.Equal(["timestamp", "value"], queryResult.Columns);
        Assert.Equal(2, queryResult.Data.Length);

        var timestampIndex = (int)FindColumnIndex(queryResult, "timestamp");
        var timestamps = queryResult.Data
            .Select(r => Convert.ToDateTime(r[timestampIndex]))
            .ToArray();

        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 3), timestamps[0]);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 5), timestamps[1]);
    }

    [Fact]
    public async Task QueryTabularFile_RequestDto_QueryUsesWindowedView()
    {
        var result = await UploadFilesystemParquet(10, "dataset.parquet");

        var queryResult = await _olapBusiness.QueryTabularFile(
            _userId,
            _organizationId,
            _projectId,
            result.Id,
            new OlapQueryRequestDto
            {
                Query = "SELECT COUNT(*) AS total FROM data",
                Limit = 3
            },
            "data");

        Assert.Single(queryResult.Data);
        Assert.Equal(3L, Convert.ToInt64(queryResult.Data[0][0]));
    }

    [Fact]
    public async Task QueryTabularFile_RequestDto_QueryUsesColumnSelection()
    {
        var result = await UploadFilesystemParquet(10, "dataset.parquet");

        var queryResult = await _olapBusiness.QueryTabularFile(
            _userId,
            _organizationId,
            _projectId,
            result.Id,
            new OlapQueryRequestDto
            {
                Query = "SELECT * FROM data",
                Limit = 2,
                Columns = ["timestamp,pressure"]
            },
            "data");

        Assert.Equal(["timestamp", "pressure"], queryResult.Columns);
        Assert.Equal(2, queryResult.Data.Length);

        var timestampIndex = (int)FindColumnIndex(queryResult, "timestamp");
        var timestamps = queryResult.Data
            .Select(r => Convert.ToDateTime(r[timestampIndex]))
            .ToArray();

        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 8), timestamps[0]);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 9), timestamps[1]);
    }

    [Fact]
    public async Task QueryTabularFile_RequestDto_StartRowGreaterThanStopRow_Throws()
    {
        var result = await UploadFilesystemParquet(10, "dataset.parquet");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _olapBusiness.QueryTabularFile(
                _userId,
                _organizationId,
                _projectId,
                result.Id,
                new OlapQueryRequestDto
                {
                    Query = "SELECT * FROM data",
                    StartRow = 6,
                    StopRow = 3
                },
                "data"));

        Assert.Contains("Start row cannot be greater than stop row", ex.Message);
    }

    [Fact]
    public async Task QueryTabularFile_RequestDto_MissingColumn_Throws()
    {
        var result = await UploadFilesystemParquet(10, "dataset.parquet");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _olapBusiness.QueryTabularFile(
                _userId,
                _organizationId,
                _projectId,
                result.Id,
                new OlapQueryRequestDto
                {
                    Query = "SELECT * FROM data",
                    Columns = ["timestamp", "missing_column"]
                },
                "data"));

        Assert.Contains("Column(s) not found: missing_column", ex.Message);
    }

    [Fact]
    public async Task QueryTablularFile_Success_ReturnsData()
    {
        var rows = 100;
        // //Arrange
        // var csvContent = "timestamp,value,sensor_id\n2024-01-01T00:00:00,42.5,sensor_1\n2024-01-01T00:01:00,43.2,sensor_1";
        var file = await CreateLargeTestParquetFile(rows, "sensor_data.parquet");
        //
        // // Act
        var result = await _fileBusiness.UploadFile(
            _userId, _organizationId, _projectId, _dataSourceId, _fileSystemObjectStorageId, file);

        // // Assert
        Assert.NotNull(result);
        Assert.Equal("sensor_data.parquet", result.Name);
        Assert.Equal("parquet", result.FileType);

        var queryResult = await _olapBusiness.QueryTabularFile(_userId, _organizationId, _projectId, result.Id,
            "Select * From data", "data");
        Assert.NotNull(queryResult);
        Assert.Equal(rows, queryResult.Data.Length);
        Assert.Equal(5, queryResult.Columns.Length);
    }

    #endregion

    #region Get Plot Data

    [Fact]
    public async Task GetPlotData_AppendedCsvFolder_Filesystem_ReturnsCorrectRowCount()
    {
        var result = await UploadFilesystemCsv(CsvHeaders, 5, "dataset.csv");

        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, result.Id, 1,
            CreateTestCsvFile(CsvHeaders, 3, "append1.csv"));

        var plotData = await _olapBusiness.GetPlotData(
            _userId, _organizationId, _projectId, result.Id, 4, 1);

        Assert.Equal(4, plotData.Data.Length);
        Assert.Equal(5, plotData.Columns.Length);
    }

    [Fact]
    public async Task GetPlotData_AppendedCsvFolder_Filesystem_UsesGlobalOrderingAcrossParts()
    {
        var result = await UploadFilesystemCsv(CsvHeaders, 5, "dataset.csv");

        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, result.Id, 1,
            CreateTestCsvFile(CsvHeaders, 3, "append1.csv"));

        // Request all 8 rows to verify part 0 rows come before part 1 rows
        var plotData = await _olapBusiness.GetPlotData(
            _userId, _organizationId, _projectId, result.Id, 8, 1);

        Assert.Equal(8, plotData.Data.Length);

        var sensorIdIndex = (int)FindColumnIndex(plotData, "sensor_id");

        // Part 0 rows use sensor_0..sensor_4, part 1 rows use sensor_0..sensor_2.
        // If ordering is wrong the rows from different parts will be interleaved incorrectly.
        Assert.Equal("sensor_0", plotData.Data[0][sensorIdIndex].ToString());
        Assert.Equal("sensor_0", plotData.Data[5][sensorIdIndex].ToString());
    }

    [Fact]
    public async Task GetPlotData_AppendedCsvFolder_Azure_ReturnsCorrectRowCount()
    {
        var result = await UploadAzureCsv(CsvHeaders, 5, "dataset.csv");

        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, result.Id, 1,
            CreateTestCsvFile(CsvHeaders, 3, "append1.csv"));

        var plotData = await _olapBusiness.GetPlotData(
            _userId, _organizationId, _projectId, result.Id, 4, 1);

        Assert.Equal(4, plotData.Data.Length);
        Assert.Equal(5, plotData.Columns.Length);
    }

    [Fact]
    public async Task GetPlotData_RecordDoesNotExist_Throws()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _olapBusiness.GetPlotData(
                _userId, _organizationId, _projectId, -999, 5, 1));
    }

    [Fact]
    public async Task GetPlotData_Azure_SingleFile_ReturnsLastNRowsInAscendingOrder()
    {
        var result = await UploadAzureParquet(10, "dataset.parquet");

        var plotData = await _olapBusiness.GetPlotData(
            _userId, _organizationId, _projectId, result.Id, 3, 1);

        Assert.NotNull(plotData);
        Assert.Equal(3, plotData.Data.Length);

        var timestampIndex = (int)FindColumnIndex(plotData, "timestamp");

        var timestamps = plotData.Data
            .Select(r => Convert.ToDateTime(r[timestampIndex]))
            .ToArray();

        // Last 3 rows of 10, returned in ascending order
        Assert.True(timestamps[0] < timestamps[1]);
        Assert.True(timestamps[1] < timestamps[2]);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 7), timestamps[0]);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 8), timestamps[1]);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 9), timestamps[2]);
    }

    [Fact]
    public async Task GetPlotData_Azure_AppendedFolder_UsesGlobalOrderingAcrossParts()
    {
        var result = await UploadAzureParquet(5, "dataset.parquet");

        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, result.Id, 1,
            await CreateLargeTestParquetFile(3, "append1.parquet"));

        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, result.Id, 2,
            await CreateLargeTestParquetFile(4, "append2.parquet"));

        var plotData = await _olapBusiness.GetPlotData(
            _userId, _organizationId, _projectId, result.Id, 4, 1);

        Assert.Equal(4, plotData.Data.Length);

        var timestampIndex = (int)FindColumnIndex(plotData, "timestamp");

        var timestamps = plotData.Data
            .Select(r => Convert.ToDateTime(r[timestampIndex]))
            .ToArray();

        // The last 4 rows globally (across all three parts) in ascending order
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 0), timestamps[0]);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 1), timestamps[1]);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 2), timestamps[2]);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 3), timestamps[3]);
    }

    [Fact]
    public async Task GetPlotData_SingleFile_ReturnsLastNRowsInAscendingOrder()
    {
        var result = await UploadFilesystemParquet(10, "dataset.parquet");

        var plotData = await _olapBusiness.GetPlotData(
            _userId, _organizationId, _projectId, result.Id, 3, 1);

        Assert.NotNull(plotData);
        Assert.Equal(3, plotData.Data.Length);

        var timestampIndex = (int)FindColumnIndex(plotData, "timestamp");

        var timestamps = plotData.Data
            .Select(r => Convert.ToDateTime(r[timestampIndex]))
            .ToArray();

        Assert.True(timestamps[0] < timestamps[1]);
        Assert.True(timestamps[1] < timestamps[2]);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 7), timestamps[0]);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 8), timestamps[1]);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 9), timestamps[2]);
    }

    [Fact]
    public async Task GetPlotData_RowStride_AppliesEveryNthRow()
    {
        var result = await UploadFilesystemParquet(10, "dataset.parquet");

        var plotData = await _olapBusiness.GetPlotData(
            _userId, _organizationId, _projectId, result.Id, 10, 2);

        Assert.NotNull(plotData);
        Assert.Equal(5, plotData.Data.Length);

        var timestampIndex = (int)FindColumnIndex(plotData, "timestamp");

        var timestamps = plotData.Data
            .Select(r => Convert.ToDateTime(r[timestampIndex]))
            .ToArray();

        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 1), timestamps[0]);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 3), timestamps[1]);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 5), timestamps[2]);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 7), timestamps[3]);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 9), timestamps[4]);
    }

    [Fact]
    public async Task GetPlotData_RequestDto_WindowAndColumns_ReturnsRequestedProjection()
    {
        var result = await UploadFilesystemParquet(10, "dataset.parquet");

        var plotData = await _olapBusiness.GetPlotData(
            _userId,
            _organizationId,
            _projectId,
            result.Id,
            new OlapQueryRequestDto
            {
                StartRow = 3,
                StopRow = 6,
                RowStride = 2,
                Columns = ["timestamp", "value"]
            });

        Assert.Equal(["timestamp", "value"], plotData.Columns);
        Assert.Equal(2, plotData.Data.Length);

        var timestampIndex = (int)FindColumnIndex(plotData, "timestamp");
        var timestamps = plotData.Data
            .Select(r => Convert.ToDateTime(r[timestampIndex]))
            .ToArray();

        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 3), timestamps[0]);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 5), timestamps[1]);
    }

    [Fact]
    public async Task GetPlotData_RequestDto_CommaSeparatedColumns_ReturnsRequestedProjection()
    {
        var result = await UploadFilesystemParquet(10, "dataset.parquet");

        var plotData = await _olapBusiness.GetPlotData(
            _userId,
            _organizationId,
            _projectId,
            result.Id,
            new OlapQueryRequestDto
            {
                Limit = 2,
                Columns = ["timestamp,pressure"]
            });

        Assert.Equal(["timestamp", "pressure"], plotData.Columns);
        Assert.Equal(2, plotData.Data.Length);

        var timestampIndex = (int)FindColumnIndex(plotData, "timestamp");
        var timestamps = plotData.Data
            .Select(r => Convert.ToDateTime(r[timestampIndex]))
            .ToArray();

        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 8), timestamps[0]);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 9), timestamps[1]);
    }

    [Fact]
    public async Task GetPlotData_RequestDto_StartRowGreaterThanStopRow_Throws()
    {
        var result = await UploadFilesystemParquet(10, "dataset.parquet");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _olapBusiness.GetPlotData(
                _userId,
                _organizationId,
                _projectId,
                result.Id,
                new OlapQueryRequestDto
                {
                    StartRow = 6,
                    StopRow = 3
                }));

        Assert.Contains("Start row cannot be greater than stop row", ex.Message);
    }

    [Fact]
    public async Task GetPlotData_RequestDto_MissingColumn_Throws()
    {
        var result = await UploadFilesystemParquet(10, "dataset.parquet");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _olapBusiness.GetPlotData(
                _userId,
                _organizationId,
                _projectId,
                result.Id,
                new OlapQueryRequestDto
                {
                    Columns = ["timestamp", "missing_column"]
                }));

        Assert.Contains("Column(s) not found: missing_column", ex.Message);
    }

    [Fact]
    public async Task GetPlotData_AppendedFolder_UsesGlobalOrderingAcrossParts()
    {
        var result = await UploadFilesystemParquet(5, "dataset.parquet");

        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, result.Id, 1,
            await CreateLargeTestParquetFile(3, "append1.parquet"));

        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, result.Id, 2,
            await CreateLargeTestParquetFile(4, "append2.parquet"));

        var plotData = await _olapBusiness.GetPlotData(
            _userId, _organizationId, _projectId, result.Id, 4, 1);

        Assert.Equal(4, plotData.Data.Length);

        var timestampIndex = (int)FindColumnIndex(plotData, "timestamp");

        var timestamps = plotData.Data
            .Select(r => Convert.ToDateTime(r[timestampIndex]))
            .ToArray();

        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 0), timestamps[0]);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 1), timestamps[1]);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 2), timestamps[2]);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 3), timestamps[3]);
    }

    [Fact]
    public async Task GetPlotData_RecordHasNoUri_Throws()
    {
        var result = await UploadFilesystemParquet(5, "dataset.parquet");

        var record = await GetRecordEntity(result.Id);
        record.Uri = null;
        await Context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _olapBusiness.GetPlotData(
                _userId, _organizationId, _projectId, result.Id, 5, 1));

        Assert.Contains("does not have a URI", ex.Message);
    }

    #endregion

    #region Extract Tabular Columns

    [Fact]
    public async Task ExtractTabularColumns_AzureCsv_ReturnsExpectedColumns()
    {
        var csvContent =
            "timestamp,value,sensor_id,temperature,pressure\n" +
            "2024-01-01T00:00:00,42.5,sensor_1,21.1,1001.2\n" +
            "2024-01-01T00:01:00,43.2,sensor_2,21.3,1001.4\n";

        var file = CreateTestCsvFile(csvContent, "sensor_data.csv");

        var result = await _fileBusiness.UploadFile(
            _userId, _organizationId, _projectId, _dataSourceId, _azureObjectStorageId, file);

        var record = await GetRecordEntity(result.Id);

        var objectStorage = await _objectStorageBusiness.GetDecryptedObjectStorage(_azureObjectStorageId);

        var columns = await _olapBusiness.ExtractTabularColumns(
            objectStorage.Type,
            objectStorage.Config,
            record.Uri!);

        Assert.NotNull(columns);
        Assert.Equal(5, columns!.Count);

        var names = columns
            .Select(c => ((JsonObject)c!)["name"]!.ToString())
            .ToArray();

        Assert.Contains("timestamp", names);
        Assert.Contains("value", names);
        Assert.Contains("sensor_id", names);
        Assert.Contains("temperature", names);
        Assert.Contains("pressure", names);
    }

    [Fact]
    public async Task ExtractTabularColumns_FilesystemParquet_ReturnsExpectedColumns()
    {
        var result = await UploadFilesystemParquet(5, "dataset.parquet");
        var record = await GetRecordEntity(result.Id);

        var objectStorage = await _objectStorageBusiness.GetDecryptedObjectStorage(_fileSystemObjectStorageId);

        var columns = await _olapBusiness.ExtractTabularColumns(
            objectStorage.Type,
            objectStorage.Config,
            record.Uri!);

        Assert.NotNull(columns);
        Assert.Equal(5, columns!.Count);

        var first = (JsonObject)columns[0]!;
        Assert.Equal("timestamp", first["name"]!.ToString());
        Assert.Equal("DateTime", first["type"]!.ToString());
    }

    [Fact]
    public async Task ExtractTabularColumns_FilesystemCsv_ReturnsExpectedColumns()
    {
        var csvContent =
            "timestamp,value,sensor_id,temperature,pressure\n" +
            "2024-01-01T00:00:00,42.5,sensor_1,21.1,1001.2\n" +
            "2024-01-01T00:01:00,43.2,sensor_2,21.3,1001.4\n";

        var file = CreateTestCsvFile(csvContent, "sensor_data.csv");

        var result = await _fileBusiness.UploadFile(
            _userId, _organizationId, _projectId, _dataSourceId, _fileSystemObjectStorageId, file);

        var record = await GetRecordEntity(result.Id);

        var objectStorage = await _objectStorageBusiness.GetDecryptedObjectStorage(_fileSystemObjectStorageId);

        var columns = await _olapBusiness.ExtractTabularColumns(
            objectStorage.Type,
            objectStorage.Config,
            record.Uri!);

        Assert.NotNull(columns);
        Assert.Equal(5, columns!.Count);

        var names = columns
            .Select(c => ((JsonObject)c!)["name"]!.ToString())
            .ToArray();

        Assert.Contains("timestamp", names);
        Assert.Contains("value", names);
        Assert.Contains("sensor_id", names);
        Assert.Contains("temperature", names);
        Assert.Contains("pressure", names);
    }

    [Fact]
    public async Task ExtractTabularColumns_AzureParquet_ReturnsExpectedColumns()
    {
        var result = await UploadAzureParquet(5, "dataset.parquet");
        var record = await GetRecordEntity(result.Id);

        var objectStorage = await _objectStorageBusiness.GetDecryptedObjectStorage(_azureObjectStorageId);

        var columns = await _olapBusiness.ExtractTabularColumns(
            objectStorage.Type,
            objectStorage.Config,
            record.Uri!);

        Assert.NotNull(columns);
        Assert.Equal(5, columns!.Count);

        var first = (JsonObject)columns[0]!;
        Assert.Equal("timestamp", first["name"]!.ToString());
    }

    [Fact]
    public async Task ExtractTabularColumns_InvalidFilesystemFile_ReturnsNull()
    {
        var objectStorage = await _objectStorageBusiness.GetDecryptedObjectStorage(_fileSystemObjectStorageId);

        var columns = await _olapBusiness.ExtractTabularColumns(
            objectStorage.Type,
            objectStorage.Config,
            Path.Combine(_tempFileSystemBasePath, "does_not_exist.parquet"));

        Assert.Null(columns);
    }

    [Fact]
    public async Task AppendTabularBlob_ThenQueryTabularFile_SelectCountAndProjection_WorksEndToEnd()
    {
        var result = await UploadFilesystemParquet(6, "dataset.parquet");

        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, result.Id, 1,
            await CreateLargeTestParquetFile(4, "append1.parquet"));

        var countResult = await _olapBusiness.QueryTabularFile(
            _userId, _organizationId, _projectId, result.Id,
            "SELECT COUNT(*) AS total FROM data", "data");

        Assert.Single(countResult.Data);
        Assert.Equal(10L, Convert.ToInt64(countResult.Data[0][0]));

        var projectionResult = await _olapBusiness.QueryTabularFile(
            _userId, _organizationId, _projectId, result.Id,
            "SELECT sensor_id, pressure FROM data", "data");

        Assert.Equal(2, projectionResult.Columns.Length);
        Assert.Equal("sensor_id", projectionResult.Columns[0]);
        Assert.Equal("pressure", projectionResult.Columns[1]);
        Assert.Equal(10, projectionResult.Data.Length);
    }

    #endregion

    #region Get Highest Part Number

    [Fact]
    public async Task GetHighestPartNumber_FlatFile_ReturnsZero()
    {
        // A freshly uploaded file has not been appended to yet.
        // 0 represents the original file (part 0 in the folder convention),
        // so highest + 1 = 1 is the correct first part number with no special casing.
        var result = await UploadFilesystemParquet(5, "dataset.parquet");

        var highest = await _olapBusiness.GetHighestPartNumber(
            _organizationId, _projectId, result.Id);

        Assert.Equal(0L, highest);
    }

    [Fact]
    public async Task GetHighestPartNumber_Filesystem_AfterFirstAppend_ReturnsOne()
    {
        var result = await UploadFilesystemParquet(5, "dataset.parquet");

        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, result.Id, 1,
            await CreateLargeTestParquetFile(3, "append1.parquet"));

        var highest = await _olapBusiness.GetHighestPartNumber(
            _organizationId, _projectId, result.Id);

        Assert.Equal(1L, highest);
    }

    [Fact]
    public async Task GetHighestPartNumber_Filesystem_AfterMultipleAppends_ReturnsHighest()
    {
        var result = await UploadFilesystemParquet(5, "dataset.parquet");

        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, result.Id, 1,
            await CreateLargeTestParquetFile(3, "append1.parquet"));

        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, result.Id, 2,
            await CreateLargeTestParquetFile(4, "append2.parquet"));

        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, result.Id, 3,
            await CreateLargeTestParquetFile(2, "append3.parquet"));

        var highest = await _olapBusiness.GetHighestPartNumber(
            _organizationId, _projectId, result.Id);

        Assert.Equal(3L, highest);
    }

    [Fact]
    public async Task GetHighestPartNumber_Azure_AfterFirstAppend_ReturnsOne()
    {
        var result = await UploadAzureParquet(5, "dataset.parquet");

        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, result.Id, 1,
            await CreateLargeTestParquetFile(3, "append1.parquet"));

        var highest = await _olapBusiness.GetHighestPartNumber(
            _organizationId, _projectId, result.Id);

        Assert.Equal(1L, highest);
    }

    [Fact]
    public async Task GetHighestPartNumber_Azure_AfterMultipleAppends_ReturnsHighest()
    {
        var result = await UploadAzureParquet(5, "dataset.parquet");

        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, result.Id, 1,
            await CreateLargeTestParquetFile(3, "append1.parquet"));

        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, result.Id, 2,
            await CreateLargeTestParquetFile(4, "append2.parquet"));

        var highest = await _olapBusiness.GetHighestPartNumber(
            _organizationId, _projectId, result.Id);

        Assert.Equal(2L, highest);
    }

    [Fact]
    public async Task GetHighestPartNumber_RecordDoesNotExist_Throws()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _olapBusiness.GetHighestPartNumber(_organizationId, _projectId, -999));

        Assert.Contains("does not exist", ex.Message);
    }

    [Fact]
    public async Task GetHighestPartNumber_RecordUriMissing_Throws()
    {
        var result = await UploadFilesystemParquet(5, "dataset.parquet");
        var record = await GetRecordEntity(result.Id);
        record.Uri = null;
        await Context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _olapBusiness.GetHighestPartNumber(_organizationId, _projectId, result.Id));

        Assert.Contains("Record has no URI", ex.Message);
    }

    [Fact]
    public async Task GetHighestPartNumber_NextPartFormula_IsAlwaysSafe()
    {
        // The intended usage is always: next = GetHighestPartNumber(...) + 1
        // This works uniformly whether the record is still a flat file (returns 0)
        // or already has parts (returns the highest part number found).
        var result = await UploadFilesystemParquet(5, "dataset.parquet");

        // Flat file: returns 0, so next part = 1
        var highest = await _olapBusiness.GetHighestPartNumber(
            _organizationId, _projectId, result.Id);
        Assert.Equal(0L, highest);

        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, result.Id, highest + 1,
            await CreateLargeTestParquetFile(3, "append1.parquet"));

        // After first append: returns 1, so next part = 2
        highest = await _olapBusiness.GetHighestPartNumber(
            _organizationId, _projectId, result.Id);
        Assert.Equal(1L, highest);

        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, result.Id, highest + 1,
            await CreateLargeTestParquetFile(2, "append2.parquet"));

        // After second append: returns 2, so next part = 3
        highest = await _olapBusiness.GetHighestPartNumber(
            _organizationId, _projectId, result.Id);
        Assert.Equal(2L, highest);
    }

    [Fact]
    public async Task GetHighestPartNumber_FlatCsvFile_ReturnsZero()
    {
        var result = await UploadFilesystemCsv(CsvHeaders, 5, "dataset.csv");

        var highest = await _olapBusiness.GetHighestPartNumber(
            _organizationId, _projectId, result.Id);

        Assert.Equal(0L, highest);
    }

    [Fact]
    public async Task GetHighestPartNumber_Filesystem_Csv_AfterFirstAppend_ReturnsOne()
    {
        var result = await UploadFilesystemCsv(CsvHeaders, 5, "dataset.csv");

        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, result.Id, 1,
            CreateTestCsvFile(CsvHeaders, 3, "append1.csv"));

        var highest = await _olapBusiness.GetHighestPartNumber(
            _organizationId, _projectId, result.Id);

        Assert.Equal(1L, highest);
    }

    [Fact]
    public async Task GetHighestPartNumber_Filesystem_Csv_AfterMultipleAppends_ReturnsHighest()
    {
        var result = await UploadFilesystemCsv(CsvHeaders, 3, "dataset.csv");

        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, result.Id, 1,
            CreateTestCsvFile(CsvHeaders, 2, "append1.csv"));

        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, result.Id, 2,
            CreateTestCsvFile(CsvHeaders, 2, "append2.csv"));

        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, result.Id, 3,
            CreateTestCsvFile(CsvHeaders, 2, "append3.csv"));

        var highest = await _olapBusiness.GetHighestPartNumber(
            _organizationId, _projectId, result.Id);

        Assert.Equal(3L, highest);
    }

    [Fact]
    public async Task GetHighestPartNumber_Azure_Csv_AfterFirstAppend_ReturnsOne()
    {
        var result = await UploadAzureCsv(CsvHeaders, 5, "dataset.csv");

        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, result.Id, 1,
            CreateTestCsvFile(CsvHeaders, 3, "append1.csv"));

        var highest = await _olapBusiness.GetHighestPartNumber(
            _organizationId, _projectId, result.Id);

        Assert.Equal(1L, highest);
    }

    [Fact]
    public async Task GetHighestPartNumber_Azure_Csv_AfterMultipleAppends_ReturnsHighest()
    {
        var result = await UploadAzureCsv(CsvHeaders, 3, "dataset.csv");

        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, result.Id, 1,
            CreateTestCsvFile(CsvHeaders, 2, "append1.csv"));

        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, result.Id, 2,
            CreateTestCsvFile(CsvHeaders, 2, "append2.csv"));

        var highest = await _olapBusiness.GetHighestPartNumber(
            _organizationId, _projectId, result.Id);

        Assert.Equal(2L, highest);
    }
    #endregion

    #region Ecosystem

    [Fact]
    public async Task Ecosystem_Filesystem_UploadAppendQueryPlotAndAppendByPartNumber()
    {
        // ── Step 1: Upload base parquet file (5 rows) ────────────────────────
        var baseFile = await CreateLargeTestParquetFile(5, "dataset.parquet");
        var uploadResult = await _fileBusiness.UploadFile(
            _userId, _organizationId, _projectId, _dataSourceId, _fileSystemObjectStorageId, baseFile);

        Assert.NotNull(uploadResult);
        Assert.Equal("parquet", uploadResult.FileType);

        var recordAfterUpload = await GetRecordEntity(uploadResult.Id);
        Assert.EndsWith(".parquet", recordAfterUpload.Uri, StringComparison.OrdinalIgnoreCase);

        // ── Step 2: Append part 1 (3 rows, timestamps continue from row 5) ───
        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, uploadResult.Id, 1,
            await CreateLargeTestParquetFile(3, "append1.parquet", 5));

        var recordAfterFirstAppend = await GetRecordEntity(uploadResult.Id);
        Assert.EndsWith(Path.DirectorySeparatorChar.ToString(), recordAfterFirstAppend.Uri);
        Assert.True(File.Exists(Path.Combine(recordAfterFirstAppend.Uri!, "0.parquet")));
        Assert.True(File.Exists(Path.Combine(recordAfterFirstAppend.Uri!, "1.parquet")));

        // ── Step 3: Query all rows across the folder ──────────────────────────
        var queryResult = await _olapBusiness.QueryTabularFile(
            _userId, _organizationId, _projectId, uploadResult.Id,
            "SELECT * FROM data", "data");

        Assert.Equal(8, queryResult.Data.Length); // 5 base + 3 appended
        Assert.Equal(5, queryResult.Columns.Length);

        // ── Step 4: Get plot data and verify ascending temporal order ─────────
        var plotData = await _olapBusiness.GetPlotData(
            _userId, _organizationId, _projectId, uploadResult.Id, 4, 1);

        Assert.Equal(4, plotData.Data.Length);

        var timestampIndex = (int)FindColumnIndex(plotData, "timestamp");
        var timestamps = plotData.Data
            .Select(r => Convert.ToDateTime(r[timestampIndex]))
            .ToArray();

        // Last 4 of 8 rows in ascending order: 00:00:04, 00:00:05, 00:00:06, 00:00:07
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 4), timestamps[0]);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 5), timestamps[1]);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 6), timestamps[2]);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 7), timestamps[3]);

        // ── Step 5: Get highest part number and derive next part ──────────────
        var highestPart = await _olapBusiness.GetHighestPartNumber(
            _organizationId, _projectId, uploadResult.Id);

        Assert.Equal(1L, highestPart);
        var nextPart = highestPart + 1; // = 2

        // ── Step 6: Append using the derived part number (4 rows, timestamps continue from row 8) ──
        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, uploadResult.Id, nextPart,
            await CreateLargeTestParquetFile(4, "append2.parquet", 8));

        Assert.True(File.Exists(Path.Combine(recordAfterFirstAppend.Uri!, "2.parquet")));

        var finalCount = await _olapBusiness.QueryTabularFile(
            _userId, _organizationId, _projectId, uploadResult.Id,
            "SELECT COUNT(*) AS total FROM data", "data");

        Assert.Single(finalCount.Data);
        Assert.Equal(12L, Convert.ToInt64(finalCount.Data[0][0])); // 5 + 3 + 4
    }

    [Fact]
    public async Task Ecosystem_Azure_UploadAppendQueryPlotAndAppendByPartNumber()
    {
        // ── Step 1: Upload base parquet file (5 rows) ────────────────────────
        var baseFile = await CreateLargeTestParquetFile(5, "dataset.parquet");
        var uploadResult = await _fileBusiness.UploadFile(
            _userId, _organizationId, _projectId, _dataSourceId, _azureObjectStorageId, baseFile);

        Assert.NotNull(uploadResult);
        Assert.Equal("parquet", uploadResult.FileType);

        var recordAfterUpload = await GetRecordEntity(uploadResult.Id);
        Assert.EndsWith(".parquet", recordAfterUpload.Uri, StringComparison.OrdinalIgnoreCase);

        // ── Step 2: Append part 1 (3 rows, timestamps continue from row 5) ───
        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, uploadResult.Id, 1,
            await CreateLargeTestParquetFile(3, "append1.parquet", 5));

        var recordAfterFirstAppend = await GetRecordEntity(uploadResult.Id);
        Assert.EndsWith("/", recordAfterFirstAppend.Uri);

        var blobServiceClient = new BlobServiceClient(_connectionString);
        var container = blobServiceClient.GetBlobContainerClient(_containerName);
        Assert.True(await container.GetBlobClient($"{recordAfterFirstAppend.Uri}0.parquet").ExistsAsync());
        Assert.True(await container.GetBlobClient($"{recordAfterFirstAppend.Uri}1.parquet").ExistsAsync());

        // ── Step 3: Query all rows across the folder ──────────────────────────
        var queryResult = await _olapBusiness.QueryTabularFile(
            _userId, _organizationId, _projectId, uploadResult.Id,
            "SELECT * FROM data", "data");

        Assert.Equal(8, queryResult.Data.Length); // 5 base + 3 appended
        Assert.Equal(5, queryResult.Columns.Length);

        // ── Step 4: Get plot data and verify ascending temporal order ─────────
        var plotData = await _olapBusiness.GetPlotData(
            _userId, _organizationId, _projectId, uploadResult.Id, 4, 1);

        Assert.Equal(4, plotData.Data.Length);

        var timestampIndex = (int)FindColumnIndex(plotData, "timestamp");
        var timestamps = plotData.Data
            .Select(r => Convert.ToDateTime(r[timestampIndex]))
            .ToArray();

        // Last 4 of 8 rows in ascending order: 00:00:04, 00:00:05, 00:00:06, 00:00:07
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 4), timestamps[0]);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 5), timestamps[1]);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 6), timestamps[2]);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 7), timestamps[3]);

        // ── Step 5: Get highest part number and derive next part ──────────────
        var highestPart = await _olapBusiness.GetHighestPartNumber(
            _organizationId, _projectId, uploadResult.Id);

        Assert.Equal(1L, highestPart);
        var nextPart = highestPart + 1; // = 2

        // ── Step 6: Append using the derived part number (4 rows, timestamps continue from row 8) ──
        await _olapBusiness.AppendTabularBlob(
            _userId, _organizationId, _projectId, uploadResult.Id, nextPart,
            await CreateLargeTestParquetFile(4, "append2.parquet", 8));

        Assert.True(await container.GetBlobClient($"{recordAfterFirstAppend.Uri}2.parquet").ExistsAsync());

        var finalCount = await _olapBusiness.QueryTabularFile(
            _userId, _organizationId, _projectId, uploadResult.Id,
            "SELECT COUNT(*) AS total FROM data", "data");

        Assert.Single(finalCount.Data);
        Assert.Equal(12L, Convert.ToInt64(finalCount.Data[0][0])); // 5 + 3 + 4
    }

    #endregion
}
