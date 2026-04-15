using System.Text;
using System.Text.Json.Nodes;
using deeplynx.business;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.helpers.BigData;
using deeplynx.helpers.Hubs;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests;

[Collection("Test Suite Collection")]
public class TimeseriesBusinessTests : IntegrationTestBase, IAsyncLifetime
{
    private static readonly string _testDuckDbBasePath;

    // Static constructor runs before anything else, ensuring env var is set
    // before TimeseriesBusiness static field is initialized
    static TimeseriesBusinessTests()
    {
        _testDuckDbBasePath = Path.Combine(Path.GetTempPath(), "deeplynx_test_duckdb", Guid.NewGuid().ToString());
        Environment.SetEnvironmentVariable("DUCKDB_BASE_PATH", _testDuckDbBasePath);
    }
    private ClassBusiness _classBusiness = null!;
    private EventBusiness _eventBusiness = null!;
    private SensitivityLabelBusiness _sensitivityLabelBusiness = null!;
    private Mock<IHubContext<EventNotificationHub>> _mockHubContext = null!;
    private Mock<ILogger<NotificationBusiness>> _mockNotificationLogger = null!;
    private Mock<ILogger<TimeseriesBusiness>> _mockTimeseriesLogger = null!;
    private Mock<IServiceScopeFactory> _mockServiceScopeFactory = null!;
    private BulkCopyUpsertExecutor _mockBulkCopyUpsertExecutor = null!;
    private INotificationBusiness _notificationBusiness = null!;
    private RecordBusiness _recordBusiness = null!;
    private RelationshipBusiness _relationshipBusiness = null!;
    private TagBusiness _tagBusiness = null!;
    private TimeseriesBusiness _timeseriesBusiness = null!;
    private ISensitivityLabelService _sensitivityLabelService = null!;

    private long _organizationId;
    private long _projectId;
    private long _dataSourceId;
    private long _userId;
    private long _classId;

    public TimeseriesBusinessTests(TestSuiteFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        // Ensure test directory exists
        Directory.CreateDirectory(_testDuckDbBasePath);

        // Set up mocks
        _mockHubContext = new Mock<IHubContext<EventNotificationHub>>();
        _mockNotificationLogger = new Mock<ILogger<NotificationBusiness>>();
        _mockTimeseriesLogger = new Mock<ILogger<TimeseriesBusiness>>();
        _mockServiceScopeFactory = new Mock<IServiceScopeFactory>();

        // Set up service scope factory mock
        var mockScope = new Mock<IServiceScope>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        mockServiceProvider.Setup(sp => sp.GetService(typeof(DeeplynxContext))).Returns(Context);
        mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);
        _mockServiceScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);
        _mockBulkCopyUpsertExecutor = new BulkCopyUpsertExecutor();
        _sensitivityLabelService = new SensitivityLabelService(Context);

        // Set up business layer dependencies
        _notificationBusiness = new NotificationBusiness(Context, _mockNotificationLogger.Object, _mockHubContext.Object);
        _eventBusiness = new EventBusiness(Context, _notificationBusiness, _mockBulkCopyUpsertExecutor);
        _tagBusiness = new TagBusiness(Context, _eventBusiness);
        _sensitivityLabelBusiness = new SensitivityLabelBusiness(Context, _eventBusiness);
        _recordBusiness = new RecordBusiness(Context, _eventBusiness, _mockBulkCopyUpsertExecutor, _tagBusiness, _sensitivityLabelBusiness, _sensitivityLabelService);
        _classBusiness = new ClassBusiness(Context, _recordBusiness, _relationshipBusiness, _eventBusiness);

        _timeseriesBusiness = new TimeseriesBusiness(
            Context,
            _recordBusiness,
            _classBusiness,
            _mockTimeseriesLogger.Object,
            _mockServiceScopeFactory.Object);

        // Set up test data
        await SetupTestDataAsync();
    }

    public new async Task DisposeAsync()
    {
        // Clean up test DuckDB directory
        if (Directory.Exists(_testDuckDbBasePath))
        {
            try
            {
                Directory.Delete(_testDuckDbBasePath, true);
            }
            catch
            {
                // Ignore cleanup errors in tests
            }
        }

        await base.DisposeAsync();
    }

    private async Task SetupTestDataAsync()
    {
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
        var config = new JsonObject();
        var objectStorage = new ObjectStorage
        {
            Name = "Timeseries Default",
            Type = "filesystem",
            Config = config.ToString(),
            ProjectId = _projectId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = _userId,
            OrganizationId = _organizationId
        };
        Context.ObjectStorages.Add(objectStorage);
        await Context.SaveChangesAsync();
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

    private static IFormFile CreateLargeTestCsvFile(int rowCount, string fileName = "large_test.csv")
    {
        var sb = new StringBuilder();
        sb.AppendLine("timestamp,value,sensor_id,temperature,pressure");

        var baseTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var random = new Random(42); // Fixed seed for reproducibility

        for (int i = 0; i < rowCount; i++)
        {
            var timestamp = baseTime.AddSeconds(i).ToString("yyyy-MM-ddTHH:mm:ss");
            var value = Math.Round(random.NextDouble() * 100, 2);
            var sensorId = $"sensor_{i % 5}";
            var temperature = Math.Round(20 + random.NextDouble() * 10, 2);
            var pressure = Math.Round(1000 + random.NextDouble() * 50, 2);
            sb.AppendLine($"{timestamp},{value},{sensorId},{temperature},{pressure}");
        }

        var content = sb.ToString();
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        stream.Position = 0;
        return new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/csv"
        };
    }
    /// <summary>
    /// Generates CSV content with specified number of rows
    /// </summary>
    private static string GenerateLargeCsvContent(int rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Timestamp,Temperature_K,Data_1,Data_2,Data_3");

        var random = new Random(42); // Fixed seed for reproducibility
        var baseTime = new DateTime(2024, 7, 15, 7, 32, 27, DateTimeKind.Utc);

        for (int i = 0; i < rows; i++)
        {
            var timestamp = baseTime.AddMilliseconds(i * 10).ToString("yyyy-MM-dd HH:mm:ss.fffffff");
            var temperature = Math.Round(873.15 + random.NextDouble() * 100, 2);
            sb.AppendLine($"{timestamp},{temperature},{i},{i * 2},{i * 3}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Splits byte array into chunks of specified size
    /// </summary>
    private static List<byte[]> SplitIntoChunks(byte[] data, int chunkSize)
    {
        var chunks = new List<byte[]>();
        for (int i = 0; i < data.Length; i += chunkSize)
        {
            int size = Math.Min(chunkSize, data.Length - i);
            var chunk = new byte[size];
            Array.Copy(data, i, chunk, 0, size);
            chunks.Add(chunk);
        }
        return chunks;
    }

    /// <summary>
    /// Creates an IFormFile from raw bytes (for chunk uploads)
    /// </summary>
    private static IFormFile CreateChunkFile(byte[] bytes, string fileName)
    {
        var stream = new MemoryStream(bytes);
        stream.Position = 0;
        return new FormFile(stream, 0, bytes.Length, "chunk", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/octet-stream"
        };
    }

    private static IFormFile CreateTestParquetFile(string fileName = "test.parquet")
    {
        // Create minimal valid parquet-like content for testing
        // In real tests, you'd use a parquet library to create valid files
        var bytes = new byte[] { 0x50, 0x41, 0x52, 0x31 }; // PAR1 magic bytes
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/octet-stream"
        };
    }

    #region UploadFile Tests

    [Fact]
    public async Task UploadFile_WithValidCsv_CreatesTableAndRecord()
    {
        // Arrange
        var csvContent = "timestamp,value,sensor_id\n2024-01-01T00:00:00,42.5,sensor_1\n2024-01-01T00:01:00,43.2,sensor_1";
        var file = CreateTestCsvFile(csvContent, "sensor_data.csv");

        // Act
        var result = await _timeseriesBusiness.UploadFile(
            _userId, _organizationId, _projectId, _dataSourceId, file);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("sensor_data.csv", result.Name);
        Assert.Equal("csv", result.FileType);
        Assert.StartsWith("duckdb://", result.Uri);
        Assert.NotNull(result.ClassId);
    }

    [Fact]
    public async Task UploadFile_WithNullFile_ThrowsArgumentException()
    {
        // Arrange
        IFormFile? file = null;

        // Act & Assert
        await Assert.ThrowsAsync<NullReferenceException>(() =>
            _timeseriesBusiness.UploadFile(_userId, _organizationId, _projectId, _dataSourceId, file!));
    }

    [Fact]
    public async Task UploadFile_WithEmptyFile_ThrowsArgumentException()
    {
        // Arrange
        var stream = new MemoryStream();
        var file = new FormFile(stream, 0, 0, "file", "empty.csv")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/csv"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _timeseriesBusiness.UploadFile(_userId, _organizationId, _projectId, _dataSourceId, file));
    }

    [Fact]
    public async Task UploadFile_WithUnsupportedFileType_ThrowsArgumentException()
    {
        // Arrange
        var content = "some content";
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        var file = new FormFile(stream, 0, bytes.Length, "file", "data.json")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/json"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _timeseriesBusiness.UploadFile(_userId, _organizationId, _projectId, _dataSourceId, file));
        Assert.Contains("Only .csv and .parquet files are supported", ex.Message);
    }

    [Fact]
    public async Task UploadFile_WithInvalidDataSource_ThrowsException()
    {
        // Arrange
        var csvContent = "timestamp,value\n2024-01-01,42.5";
        var file = CreateTestCsvFile(csvContent);
        var invalidDataSourceId = 99999L;

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _timeseriesBusiness.UploadFile(_userId, _organizationId, _projectId, invalidDataSourceId, file));
    }

    #endregion

    #region StartUpload Tests

    [Fact]
    public async Task StartUpload_WithValidCsvFileName_ReturnsUploadId()
    {
        // Act
        var uploadId = await _timeseriesBusiness.StartUpload(
            _organizationId, _projectId, _dataSourceId, "large_file.csv");

        // Assert
        Assert.NotNull(uploadId);
        Assert.True(Guid.TryParse(uploadId, out _));
    }

    [Fact]
    public async Task StartUpload_WithValidParquetFileName_ReturnsUploadId()
    {
        // Act
        var uploadId = await _timeseriesBusiness.StartUpload(
            _organizationId, _projectId, _dataSourceId, "large_file.parquet");

        // Assert
        Assert.NotNull(uploadId);
        Assert.True(Guid.TryParse(uploadId, out _));
    }

    [Fact]
    public async Task StartUpload_WithUnsupportedFileType_ThrowsArgumentException()
    {
        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _timeseriesBusiness.StartUpload(_organizationId, _projectId, _dataSourceId, "data.xlsx"));
        Assert.Contains("Only .csv and .parquet files are supported", ex.Message);
    }

    [Fact]
    public async Task StartUpload_CreatesUploadDirectory()
    {
        // Act
        var uploadId = await _timeseriesBusiness.StartUpload(
            _organizationId, _projectId, _dataSourceId, "test.csv");

        // Assert
        var expectedPath = Path.Combine(_testDuckDbBasePath,
            $"org_{_organizationId}", $"project_{_projectId}",
            $"datasource_{_dataSourceId}", uploadId);
        Assert.True(Directory.Exists(expectedPath));
    }

    #endregion

    #region UploadChunk Tests

    [Fact]
    public async Task UploadChunk_WithValidChunk_ReturnsSuccess()
    {
        // Arrange
        var uploadId = await _timeseriesBusiness.StartUpload(
            _organizationId, _projectId, _dataSourceId, "chunked.csv");

        // Create a realistic chunk from CSV bytes
        var csvContent = "timestamp,value\n2024-01-01T00:00:00,42.5\n2024-01-01T00:01:00,43.2";
        var chunkBytes = Encoding.UTF8.GetBytes(csvContent);
        var chunk = CreateChunkFile(chunkBytes, "chunked.csv");

        // Act
        var result = await _timeseriesBusiness.UploadChunk(
            _organizationId, _projectId, _dataSourceId, chunk, uploadId, 0);

        // Assert
        Assert.Equal("success", result);
    }

    [Fact]
    public async Task UploadChunk_WithNullChunk_ThrowsArgumentException()
    {
        // Arrange
        var uploadId = await _timeseriesBusiness.StartUpload(
            _organizationId, _projectId, _dataSourceId, "chunked.csv");

        // Act & Assert
        await Assert.ThrowsAsync<NullReferenceException>(() =>
            _timeseriesBusiness.UploadChunk(_organizationId, _projectId, _dataSourceId, null!, uploadId, 0));
    }

    [Fact]
    public async Task UploadChunk_WithEmptyChunk_ThrowsArgumentException()
    {
        // Arrange
        var uploadId = await _timeseriesBusiness.StartUpload(
            _organizationId, _projectId, _dataSourceId, "chunked.csv");
        var stream = new MemoryStream();
        var chunk = new FormFile(stream, 0, 0, "chunk", "chunked.csv");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _timeseriesBusiness.UploadChunk(_organizationId, _projectId, _dataSourceId, chunk, uploadId, 0));
    }

    #endregion

    #region CompleteUpload Tests

    [Fact]
    public async Task CompleteUpload_WithValidChunks_CreatesTableAndRecord()
    {
        // Arrange - create CSV content and split into byte chunks
        var csvContent = GenerateLargeCsvContent(500); // 500 rows should give us decent size
        var csvBytes = Encoding.UTF8.GetBytes(csvContent);
        var chunkSize = 1024; // 1KB chunks for testing (real usage is 5MB)
        var chunks = SplitIntoChunks(csvBytes, chunkSize);

        // Step 1: Initialize upload
        var uploadId = await _timeseriesBusiness.StartUpload(
            _organizationId, _projectId, _dataSourceId, "chunked_upload.csv");

        Assert.NotNull(uploadId);
        Assert.True(Guid.TryParse(uploadId, out _));

        // Step 2: Upload each chunk
        for (int i = 0; i < chunks.Count; i++)
        {
            var chunkFile = CreateChunkFile(chunks[i], "chunked_upload.csv");
            var result = await _timeseriesBusiness.UploadChunk(
                _organizationId, _projectId, _dataSourceId, chunkFile, uploadId, i);
            Assert.Equal("success", result);
        }

        // Step 3: Complete upload
        var request = new TimeseriesUploadCompleteRequestDto
        {
            UploadId = uploadId,
            FileName = "chunked_upload.csv",
            TotalChunks = chunks.Count
        };

        var completeResult = await _timeseriesBusiness.CompleteUpload(
            _userId, _organizationId, _projectId, _dataSourceId, request);

        // Assert
        Assert.NotNull(completeResult);
        Assert.Equal("chunked_upload.csv", completeResult.Name);
        Assert.StartsWith("duckdb://", completeResult.Uri);

        // Verify the table was created and has data
        var latestRow = await _timeseriesBusiness.GetLatestRow(
            _userId, _organizationId, _projectId, _dataSourceId, completeResult.Id);
        Assert.NotEmpty(latestRow);
    }

    [Fact]
    public async Task ChunkedUpload_FullWorkflow()
    {
        // 1. Initialize upload (get uploadId)
        // 2. Upload chunks sequentially with chunk numbers
        // 3. Complete upload (merge chunks on server)

        // Arrange - simulate a larger file split into multiple chunks
        var csvContent = GenerateLargeCsvContent(1000);
        var csvBytes = Encoding.UTF8.GetBytes(csvContent);
        var chunkSize = 2048; // 2KB chunks
        var chunks = SplitIntoChunks(csvBytes, chunkSize);
        var fileName = "large_upload.csv";

        // Step 1: Initialize Upload
        var uploadId = await _timeseriesBusiness.StartUpload(
            _organizationId, _projectId, _dataSourceId, fileName);

        Assert.NotNull(uploadId);

        // Step 2: Upload Chunks
        for (int chunkNumber = 0; chunkNumber < chunks.Count; chunkNumber++)
        {
            var chunkFile = CreateChunkFile(chunks[chunkNumber], fileName);
            var chunkResult = await _timeseriesBusiness.UploadChunk(
                _organizationId, _projectId, _dataSourceId,
                chunkFile, uploadId, chunkNumber);

            Assert.Equal("success", chunkResult);
        }

        // Step 3: Complete Upload
        var completeRequest = new TimeseriesUploadCompleteRequestDto
        {
            UploadId = uploadId,
            FileName = fileName,
            TotalChunks = chunks.Count
        };

        var result = await _timeseriesBusiness.CompleteUpload(
            _userId, _organizationId, _projectId, _dataSourceId, completeRequest);

        // Verify
        Assert.NotNull(result);
        Assert.Equal(fileName, result.Name);
        Assert.Equal("csv", result.FileType);
        Assert.StartsWith("duckdb://", result.Uri);

        // Verify data integrity - check we can query the table
        var plotData = await _timeseriesBusiness.GetPlotData(
            _userId, _organizationId, _projectId, _dataSourceId, result.Id, 10, 1);
        Assert.NotNull(plotData.Data);
    }

    #endregion

    #region GetPlotData Tests

    [Fact]
    public async Task GetPlotData_WithValidRecord_ReturnsData()
    {
        // Arrange - upload a file with enough data to plot
        var file = CreateLargeTestCsvFile(50, "plot_test.csv");
        var uploadResult = await _timeseriesBusiness.UploadFile(
            _userId, _organizationId, _projectId, _dataSourceId, file);

        // Act
        var result = await _timeseriesBusiness.GetPlotData(
            _userId, _organizationId, _projectId, _dataSourceId, uploadResult.Id, 100, 1);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Data);
        Assert.NotEmpty(result.Data);
    }

    [Fact]
    public async Task GetPlotData_WithInvalidRecordId_ThrowsException()
    {
        // Arrange - first upload a valid file to create the DuckDB database
        var csvContent = "timestamp,value\n2024-01-01,42.5";
        var file = CreateTestCsvFile(csvContent, "setup.csv");
        await _timeseriesBusiness.UploadFile(_userId, _organizationId, _projectId, _dataSourceId, file);

        // Act & Assert - use a record ID that doesn't exist
        await Assert.ThrowsAnyAsync<Exception>(() =>
            _timeseriesBusiness.GetPlotData(_userId, _organizationId, _projectId, _dataSourceId, -1, 100, 1));
    }

    #endregion

    #region GetLatestRow Tests

    [Fact]
    public async Task GetLatestRow_WithValidRecord_ReturnsLatestRow()
    {
        // Arrange - use the large file generator for consistent data
        var file = CreateLargeTestCsvFile(20, "latest_row_test.csv");
        var uploadResult = await _timeseriesBusiness.UploadFile(
            _userId, _organizationId, _projectId, _dataSourceId, file);

        // Act
        var result = await _timeseriesBusiness.GetLatestRow(
            _userId, _organizationId, _projectId, _dataSourceId, uploadResult.Id);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.True(result.ContainsKey("value"));
        Assert.True(result.ContainsKey("timestamp"));
    }

    [Fact]
    public async Task GetLatestRow_WithInvalidRecordId_ThrowsException()
    {
        // Arrange - create db first
        var csvContent = "timestamp,value\n2024-01-01,42.5";
        var file = CreateTestCsvFile(csvContent, "setup2.csv");
        await _timeseriesBusiness.UploadFile(_userId, _organizationId, _projectId, _dataSourceId, file);

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(() =>
            _timeseriesBusiness.GetLatestRow(_userId, _organizationId, _projectId, _dataSourceId, -1));
    }

    #endregion

    #region AppendTimeseriesTable Tests

    [Fact]
    public async Task AppendTimeseriesTable_WithValidData_AppendsRows()
    {
        // Arrange - create initial table
        var initialCsv = "timestamp,value\n2024-01-01T00:00:00,42.5";
        var initialFile = CreateTestCsvFile(initialCsv, "append_test.csv");
        var uploadResult = await _timeseriesBusiness.UploadFile(
            _userId, _organizationId, _projectId, _dataSourceId, initialFile);
        var tableName = uploadResult.Uri!.Replace("duckdb://", "");

        // Create append data
        var appendCsv = "2024-01-01T00:01:00,99.9";
        var appendFile = CreateTestCsvFile(appendCsv, "append_data.csv");

        // Act
        await _timeseriesBusiness.AppendTimeseriesTable(
            _organizationId, _projectId, _dataSourceId, appendFile, tableName);

        // Assert - verify by getting latest row
        var latestRow = await _timeseriesBusiness.GetLatestRow(
            _userId, _organizationId, _projectId, _dataSourceId, uploadResult.Id);
        Assert.Equal(99.9, Convert.ToDouble(latestRow["value"]));
    }

    [Fact]
    public async Task AppendTimeseriesTable_WithInvalidTableName_ThrowsArgumentException()
    {
        // Arrange
        var csvContent = "timestamp,value\n2024-01-01,42.5";
        var file = CreateTestCsvFile(csvContent, "append.csv");

        // Create db first
        var setupFile = CreateTestCsvFile(csvContent, "setup3.csv");
        await _timeseriesBusiness.UploadFile(_userId, _organizationId, _projectId, _dataSourceId, setupFile);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _timeseriesBusiness.AppendTimeseriesTable(
                _organizationId, _projectId, _dataSourceId, file, "nonexistent_table"));
        Assert.Contains("Invalid table name", ex.Message);
    }

    [Fact]
    public async Task AppendTimeseriesTable_WithUnsupportedFileType_ThrowsArgumentException()
    {
        // Arrange
        var bytes = Encoding.UTF8.GetBytes("data");
        var stream = new MemoryStream(bytes);
        var file = new FormFile(stream, 0, bytes.Length, "file", "data.json");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _timeseriesBusiness.AppendTimeseriesTable(
                _organizationId, _projectId, _dataSourceId, file, "any_table"));
    }

    #endregion

    #region ExportTimeseriesTable Tests

    [Fact]
    public async Task ExportTimeseriesTable_WithCsvFormat_CreatesReportRecord()
    {
        // Arrange
        var csvContent = "timestamp,value\n2024-01-01,42.5";
        var file = CreateTestCsvFile(csvContent, "export_test.csv");
        var uploadResult = await _timeseriesBusiness.UploadFile(
            _userId, _organizationId, _projectId, _dataSourceId, file);
        var tableName = uploadResult.Uri!.Replace("duckdb://", "");

        // Act
        var result = await _timeseriesBusiness.ExportTimeseriesTable(
            _userId, _organizationId, _projectId, _dataSourceId, tableName, "csv");

        // Assert
        Assert.NotNull(result);
        Assert.EndsWith(".csv", result.Name);
        Assert.Equal("csv", result.FileType);
    }

    [Fact]
    public async Task ExportTimeseriesTable_WithParquetFormat_CreatesReportRecord()
    {
        // Arrange
        var csvContent = "timestamp,value\n2024-01-01,42.5";
        var file = CreateTestCsvFile(csvContent, "export_parquet_test.csv");
        var uploadResult = await _timeseriesBusiness.UploadFile(
            _userId, _organizationId, _projectId, _dataSourceId, file);
        var tableName = uploadResult.Uri!.Replace("duckdb://", "");

        // Act
        var result = await _timeseriesBusiness.ExportTimeseriesTable(
            _userId, _organizationId, _projectId, _dataSourceId, tableName, "parquet");

        // Assert
        Assert.NotNull(result);
        Assert.EndsWith(".parquet", result.Name);
        Assert.Equal("parquet", result.FileType);
    }

    [Fact]
    public async Task ExportTimeseriesTable_WithUnsupportedFormat_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            _timeseriesBusiness.ExportTimeseriesTable(
                _userId, _organizationId, _projectId, _dataSourceId, "any_table", "xlsx"));
    }

    #endregion

    #region QueryTimeseries Tests

    [Fact]
    public async Task QueryTimeseries_WithValidQuery_CreatesReportRecord()
    {
        // Arrange
        var csvContent = "timestamp,value\n2024-01-01,42.5\n2024-01-02,43.2";
        var file = CreateTestCsvFile(csvContent, "query_test.csv");
        var uploadResult = await _timeseriesBusiness.UploadFile(
            _userId, _organizationId, _projectId, _dataSourceId, file);
        var tableName = uploadResult.Uri!.Replace("duckdb://", "");

        var request = new TimeseriesQueryRequestDto
        {
            Query = $"SELECT * FROM '{tableName}' WHERE value > 42"
        };

        // Act
        var result = await _timeseriesBusiness.QueryTimeseries(
            _userId, request, _organizationId, _projectId, _dataSourceId, "csv");

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Properties);
        Assert.Contains("in progress", result.Properties.ToLowerInvariant());
    }

    #endregion

    #region InterpolateRows Tests

    [Fact]
    public async Task InterpolateRows_WithValidParameters_CreatesReportRecord()
    {
        // Arrange - create table with enough rows to interpolate
        var file = CreateLargeTestCsvFile(100, "interpolate_test.csv");
        var uploadResult = await _timeseriesBusiness.UploadFile(
            _userId, _organizationId, _projectId, _dataSourceId, file);
        var tableName = uploadResult.Uri!.Replace("duckdb://", "");

        // Act - get every 10th row
        var result = await _timeseriesBusiness.InterpolateRows(
            _userId, _organizationId, _projectId, _dataSourceId, "10", tableName, "csv");

        // Assert
        Assert.NotNull(result);
        Assert.EndsWith(".csv", result.Name);
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public async Task UploadFile_WithNonExistentDataSource_ThrowsKeyNotFoundException()
    {
        // Arrange
        var csvContent = "timestamp,value\n2024-01-01,42.5";
        var file = CreateTestCsvFile(csvContent);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _timeseriesBusiness.UploadFile(_userId, _organizationId, _projectId, 999999L, file));
    }

    [Fact]
    public async Task GetPlotData_WithValidRecord_ReturnsColumnsAndData()
    {
        // Arrange - upload a file with known data
        var csvContent = "timestamp,value,temperature\n2024-01-01T00:00:00,42.5,20.1\n2024-01-01T00:01:00,43.0,20.5\n2024-01-01T00:02:00,44.5,21.0";
        var file = CreateTestCsvFile(csvContent, "plot_data_test.csv");
        var uploadResult = await _timeseriesBusiness.UploadFile(
            _userId, _organizationId, _projectId, _dataSourceId, file);

        // Act
        var result = await _timeseriesBusiness.GetPlotData(
            _userId, _organizationId, _projectId, _dataSourceId, uploadResult.Id, 100, 1);

        // Assert
        Assert.NotNull(result);

        // Verify columns
        Assert.NotNull(result.Columns);
        Assert.Equal(3, result.Columns.Length);
        Assert.Contains("timestamp", result.Columns);
        Assert.Contains("value", result.Columns);
        Assert.Contains("temperature", result.Columns);

        // Verify data
        Assert.NotNull(result.Data);
        Assert.Equal(3, result.Data.Length);

        // Each row should have 3 values
        foreach (var row in result.Data)
        {
            Assert.Equal(3, row.Length);
        }
    }

    [Fact]
    public async Task MultipleUploads_CreateSeparateTables()
    {
        // Arrange & Act
        var csv1 = "timestamp,value\n2024-01-01,1";
        var file1 = CreateTestCsvFile(csv1, "file1.csv");
        var result1 = await _timeseriesBusiness.UploadFile(
            _userId, _organizationId, _projectId, _dataSourceId, file1);

        var csv2 = "timestamp,value\n2024-01-01,2";
        var file2 = CreateTestCsvFile(csv2, "file2.csv");
        var result2 = await _timeseriesBusiness.UploadFile(
            _userId, _organizationId, _projectId, _dataSourceId, file2);

        // Assert
        Assert.NotEqual(result1.Uri, result2.Uri);
        Assert.NotEqual(result1.OriginalId, result2.OriginalId);
    }

    #endregion
    
    #region FileSize Tests

    [Fact]
    public async Task UploadFile_CapturesFileSize()
    {
        // Arrange
        var csvContent = GenerateLargeCsvContent(100); // 100 rows
        var expectedSize = System.Text.Encoding.UTF8.GetBytes(csvContent).Length;
        var file = CreateTestCsvFile(csvContent, "filesize_test.csv");

        // Act
        var result = await _timeseriesBusiness.UploadFile(
            _userId, _organizationId, _projectId, _dataSourceId, file);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.FileSize);
        Assert.Equal(expectedSize, result.FileSize);
        
        // Verify file size persisted in database
        var dbRecord = await Context.Records.FindAsync(result.Id);
        Assert.NotNull(dbRecord);
        Assert.Equal(expectedSize, dbRecord.FileSize);
    }

    [Fact]
    public async Task UploadFile_LargeTimeseries_CapturesCorrectFileSize()
    {
        // Arrange - Create a large CSV file (10,000 rows)
        var csvContent = GenerateLargeCsvContent(10000);
        var expectedSize = System.Text.Encoding.UTF8.GetBytes(csvContent).Length;
        var file = CreateTestCsvFile(csvContent, "large_timeseries.csv");

        // Act
        var result = await _timeseriesBusiness.UploadFile(
            _userId, _organizationId, _projectId, _dataSourceId, file);

        // Assert
        Assert.NotNull(result.FileSize);
        Assert.Equal(expectedSize, result.FileSize);
        Assert.True(result.FileSize > 500000); // Should be over 500KB for 10k rows
    }

    [Fact]
    public async Task CompleteUpload_ChunkedTimeseries_CapturesCorrectFileSize()
    {
        // Arrange - Create CSV content and split into chunks
        var csvContent = GenerateLargeCsvContent(1000);
        var expectedSize = System.Text.Encoding.UTF8.GetBytes(csvContent).Length;
        var csvBytes = System.Text.Encoding.UTF8.GetBytes(csvContent);
        var chunkSize = 2048;
        var chunks = SplitIntoChunks(csvBytes, chunkSize);
        var fileName = "chunked_timeseries_size.csv";

        // Step 1: Initialize Upload
        var uploadId = await _timeseriesBusiness.StartUpload(
            _organizationId, _projectId, _dataSourceId, fileName);

        // Step 2: Upload Chunks
        for (int i = 0; i < chunks.Count; i++)
        {
            var chunkFile = CreateChunkFile(chunks[i], fileName);
            await _timeseriesBusiness.UploadChunk(
                _organizationId, _projectId, _dataSourceId, chunkFile, uploadId, i);
        }

        // Step 3: Complete Upload
        var completeRequest = new TimeseriesUploadCompleteRequestDto
        {
            UploadId = uploadId,
            FileName = fileName,
            TotalChunks = chunks.Count
        };

        // Act
        var result = await _timeseriesBusiness.CompleteUpload(
            _userId, _organizationId, _projectId, _dataSourceId, completeRequest);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.FileSize);
        Assert.Equal(expectedSize, result.FileSize);
        
        // Verify in database
        var dbRecord = await Context.Records.FindAsync(result.Id);
        Assert.Equal(expectedSize, dbRecord.FileSize);
    }

    [Fact]
    public async Task GetRecord_TimeseriesFile_ReturnsFileSize()
    {
        // Arrange
        var csvContent = GenerateLargeCsvContent(50);
        var expectedSize = System.Text.Encoding.UTF8.GetBytes(csvContent).Length;
        var file = CreateTestCsvFile(csvContent, "get_size_test.csv");
        var uploadedRecord = await _timeseriesBusiness.UploadFile(
            _userId, _organizationId, _projectId, _dataSourceId, file);

        // Act
        var retrievedRecord = await _recordBusiness.GetRecord(
            _userId, _organizationId, _projectId, uploadedRecord.Id, true);

        // Assert
        Assert.NotNull(retrievedRecord);
        Assert.NotNull(retrievedRecord.FileSize);
        Assert.Equal(expectedSize, retrievedRecord.FileSize);
    }

    [Fact]
    public async Task MultipleUploads_DifferentSizes_CapturesAllCorrectly()
    {
        // Arrange & Act - Upload multiple files with different sizes
        var smallCsv = GenerateLargeCsvContent(10);
        var mediumCsv = GenerateLargeCsvContent(100);
        var largeCsv = GenerateLargeCsvContent(1000);
        
        var smallSize = System.Text.Encoding.UTF8.GetBytes(smallCsv).Length;
        var mediumSize = System.Text.Encoding.UTF8.GetBytes(mediumCsv).Length;
        var largeSize = System.Text.Encoding.UTF8.GetBytes(largeCsv).Length;

        var result1 = await _timeseriesBusiness.UploadFile(
            _userId, _organizationId, _projectId, _dataSourceId, 
            CreateTestCsvFile(smallCsv, "small.csv"));
        
        var result2 = await _timeseriesBusiness.UploadFile(
            _userId, _organizationId, _projectId, _dataSourceId, 
            CreateTestCsvFile(mediumCsv, "medium.csv"));
        
        var result3 = await _timeseriesBusiness.UploadFile(
            _userId, _organizationId, _projectId, _dataSourceId, 
            CreateTestCsvFile(largeCsv, "large.csv"));

        // Assert
        Assert.Equal(smallSize, result1.FileSize);
        Assert.Equal(mediumSize, result2.FileSize);
        Assert.Equal(largeSize, result3.FileSize);
        
        // Verify ordering
        Assert.True(result1.FileSize < result2.FileSize);
        Assert.True(result2.FileSize < result3.FileSize);
    }

    [Fact]
    public async Task ExportTimeseriesTable_CreatesReportWithFileSize()
    {
        // Arrange - Upload initial file
        var csvContent = GenerateLargeCsvContent(50);
        var file = CreateTestCsvFile(csvContent, "export_source.csv");
        var uploadResult = await _timeseriesBusiness.UploadFile(
            _userId, _organizationId, _projectId, _dataSourceId, file);
        var tableName = uploadResult.Uri!.Replace("duckdb://", "");

        // Act - Export the table
        var exportResult = await _timeseriesBusiness.ExportTimeseriesTable(
            _userId, _organizationId, _projectId, _dataSourceId, tableName, "csv");

        // Wait a bit for background job to complete
        await Task.Delay(1000);

        // Assert - Get the completed record
        Context.ChangeTracker.Clear();
        var completedRecord = await Context.Records.FindAsync(exportResult.Id);
        Assert.NotNull(completedRecord);
        Assert.NotNull(completedRecord.FileSize);
        Assert.True(completedRecord.FileSize > 0);
    }

    #endregion
}