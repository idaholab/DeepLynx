using System.Text.Json;
using System.Text.Json.Nodes;
using deeplynx.business;
using deeplynx.datalayer.Models;
using deeplynx.helpers.BigData;
using deeplynx.helpers.Hubs;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Record = deeplynx.datalayer.Models.Record;

namespace deeplynx.tests;

[Collection("Test Suite Collection")]
public class HistoricalRecordBusinessTests : IntegrationTestBase
{
    private EventBusiness _eventBusiness;
    private HistoricalRecordBusiness _historicalRecordBusiness = null!;
    private Mock<IHubContext<EventNotificationHub>> _mockHubContext = null!;
    private Mock<ILogger<NotificationBusiness>> _mockNotificationLogger = null!;
    private INotificationBusiness _notificationBusiness = null!;
    private RecordBusiness _recordBusiness = null!;
    private TagBusiness _tagBusiness = null!;
    private IBulkCopyUpsertExecutor _bulkCopyUpsertExecutor = null!;

    public long cid;
    public long did;
    public long did2;
    private long organizationId;
    public long os1;
    public long pid;
    public long pid2;
    public long rid;
    public long rid2;
    public long uid;

    public HistoricalRecordBusinessTests(TestSuiteFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _historicalRecordBusiness = new HistoricalRecordBusiness(Context);
        _mockHubContext = new Mock<IHubContext<EventNotificationHub>>();
        _mockNotificationLogger = new Mock<ILogger<NotificationBusiness>>();
        _notificationBusiness =
            new NotificationBusiness(Context, _mockNotificationLogger.Object, _mockHubContext.Object);
        _bulkCopyUpsertExecutor = new BulkCopyUpsertExecutor();
        _eventBusiness = new EventBusiness(Context, _notificationBusiness, _bulkCopyUpsertExecutor);
        _tagBusiness = new TagBusiness(Context, _eventBusiness);
        _recordBusiness = new RecordBusiness(Context, _eventBusiness, _bulkCopyUpsertExecutor, _tagBusiness);
    }

    protected override async Task SeedTestDataAsync()
    {
        await base.SeedTestDataAsync();

        var testUser = new User
        {
            Name = "Test User",
            Email = "test.user@test.com",
            Password = "test_password",
            IsArchived = false
        };
        Context.Users.Add(testUser);
        await Context.SaveChangesAsync();
        uid = testUser.Id;

        var organization = new Organization { Name = $"unique org {Guid.NewGuid()}" };
        Context.Organizations.Add(organization);
        await Context.SaveChangesAsync();
        organizationId = organization.Id;

        var project = new Project
        {
            Name = "Test Project",
            Description = "Test project for unit tests",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = organizationId
        };
        var project2 = new Project
        {
            Name = "Test Project 2",
            Description = "Test project 2 for unit tests",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = organizationId
        };
        Context.Projects.Add(project);
        Context.Projects.Add(project2);
        await Context.SaveChangesAsync();
        pid = project.Id;
        pid2 = project2.Id;

        var dataSource = new DataSource
        {
            Name = "Test Data Source",
            Description = "Test data source for unit tests",
            ProjectId = project.Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = organizationId
        };

        var dataSource2 = new DataSource
        {
            Name = "Test Data Source 2",
            Description = "Test data source 2 for unit tests",
            ProjectId = project2.Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = organizationId
        };

        var dataSource3 = new DataSource
        {
            Name = "Test Data Source 3",
            Description = "Test data source 3 for unit tests",
            ProjectId = project2.Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = organizationId
        };

        Context.DataSources.Add(dataSource);
        Context.DataSources.Add(dataSource2);
        Context.DataSources.Add(dataSource3);
        await Context.SaveChangesAsync();
        did = dataSource.Id;
        did2 = dataSource2.Id;

        var testClass = new Class
        {
            Name = "Test Class",
            Description = "Test class for unit tests",
            ProjectId = project.Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = organizationId
        };
        var testClass2 = new Class
        {
            Name = "Test Class 2",
            Description = "Test class 2 for unit tests",
            ProjectId = project2.Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = organizationId
        };

        Context.Classes.Add(testClass);
        Context.Classes.Add(testClass2);
        await Context.SaveChangesAsync();
        cid = testClass.Id;

        var testTag = new Tag
        {
            Name = "Test Tag",
            ProjectId = project.Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = organizationId
        };
        var testTag2 = new Tag
        {
            Name = "Test Tag 2",
            ProjectId = project2.Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = organizationId
        };

        var config = new JsonObject();
        var objectStorage = new ObjectStorage
        {
            Name = "Object Storage 1",
            Type = "filesystem",
            Config = config.ToString(),
            ProjectId = pid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = organizationId
        };
        Context.ObjectStorages.Add(objectStorage);

        await Context.SaveChangesAsync();
        os1 = objectStorage.Id;

        var testRecord = new Record
        {
            Name = "Test Record",
            Description = "Test record for unit tests",
            ObjectStorageId = os1,
            OriginalId = "og_id",
            Properties = JsonSerializer.Serialize(new { TestProperty = "TestValue" }),
            ProjectId = pid,
            DataSourceId = dataSource.Id,
            ClassId = testClass.Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            Tags = new List<Tag> { testTag },
            Uri = "localhost:8090",
            OrganizationId = organizationId
        };

        var testRecord2 = new Record
        {
            Name = "Test Record 2",
            Description = "Test record 2 for unit tests",
            OriginalId = "og_id2",
            ObjectStorageId = os1,
            Properties = JsonSerializer.Serialize(new { TestProperty = "TestValue" }),
            ProjectId = pid,
            DataSourceId = dataSource.Id,
            ClassId = testClass.Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            Uri = "localhost:8090",
            OrganizationId = organizationId
        };

        var testRecord3 = new Record
        {
            Name = "Test Record 3",
            Description = "Test record 3 for unit tests",
            OriginalId = "og_id3",
            Properties = JsonSerializer.Serialize(new { TestProperty = "TestValue" }),
            ProjectId = pid2,
            DataSourceId = dataSource2.Id,
            ClassId = testClass2.Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            Tags = new List<Tag> { testTag2 },
            Uri = "localhost:8090",
            OrganizationId = organizationId
        };

        var testRecord4 = new Record
        {
            Name = "Test Record 4",
            Description = "Test record 4 for unit tests",
            OriginalId = "og_id4",
            Properties = JsonSerializer.Serialize(new { TestProperty = "TestValue" }),
            ProjectId = pid2,
            DataSourceId = dataSource3.Id,
            ClassId = testClass2.Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            Tags = new List<Tag> { testTag2 },
            Uri = "localhost:8090",
            OrganizationId = organizationId
        };

        Context.Tags.Add(testTag);
        await Context.SaveChangesAsync();

        Context.Records.Add(testRecord);
        Context.Records.Add(testRecord2);
        Context.Records.Add(testRecord3);
        Context.Records.Add(testRecord4);
        await Context.SaveChangesAsync();

        rid = testRecord.Id;
        rid2 = testRecord2.Id;
    }

    #region GetHistoricalRecords Tests

    [Fact]
    public async Task GetHistoricalRecords_ReturnsListOfCurrentHistoricalRecordsForProject()
    {
        // Act
        var historicalRecords = await _historicalRecordBusiness.GetAllHistoricalRecords(pid, organizationId);

        // Assert
        Assert.NotNull(historicalRecords);
        Assert.Equal(2, historicalRecords.Count());
        Assert.Equal("Test Record", historicalRecords.First().Name);
        Assert.Equal("Test Record 2", historicalRecords.Last().Name);
        Assert.DoesNotContain(historicalRecords, x => x.Name == "Test Record 3");
        Assert.DoesNotContain(historicalRecords, x => x.Name == "Test Record 4");
    }

    [Fact]
    public async Task GetHistoricalRecords_ReturnsListOfUpdatedHistoricalRecords()
    {
        // Arrange
        var dto = new UpdateRecordRequestDto
        {
            Name = "Updated Test Record",
            Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { UpdatedProp = "UpdatedValue" }))!,
            Uri = "updated://uri",
            OriginalId = "updated-123",
            Description = "Updated Description",
            ClassId = cid
        };

        var dto2 = new UpdateRecordRequestDto
        {
            Name = "Updated Test Record 2",
            Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { UpdatedProp = "UpdatedValue 2" }))!,
            Uri = "updated2://uri",
            OriginalId = "updated2-123",
            Description = "Updated 2 Description",
            ClassId = cid
        };

        await _recordBusiness.UpdateRecord(uid, organizationId, pid, rid, dto);
        await _recordBusiness.UpdateRecord(uid, organizationId, pid, rid2, dto2);

        // Act
        var historicalRecords = await _historicalRecordBusiness.GetAllHistoricalRecords(pid, organizationId);

        // Assert
        Assert.NotNull(historicalRecords);
        Assert.Equal(2, historicalRecords.Count());
        Assert.Equal("Updated Test Record", historicalRecords.First().Name);
        Assert.Equal("Updated Test Record 2", historicalRecords.Last().Name);
    }

    [Fact]
    public async Task GetHistoricalRecords_ContainsArchivedHistoricalRecords()
    {
        // Arrange
        await _recordBusiness.ArchiveRecord(uid, organizationId, pid, rid);

        // Act
        var historicalRecords =
            await _historicalRecordBusiness.GetAllHistoricalRecords(pid, organizationId, null, null, false);

        // Assert
        Assert.NotNull(historicalRecords);
        Assert.Equal(2, historicalRecords.Count());
        Assert.Contains(historicalRecords, x => x.Name == "Test Record");
        Assert.Contains(historicalRecords, x => x.Name == "Test Record 2");
    }

    [Fact]
    public async Task GetHistoricalRecords_DoesNotContainArchivedHistoricalRecords()
    {
        // Arrange
        var historicalRecords = await _historicalRecordBusiness.GetAllHistoricalRecords(pid, organizationId);

        Assert.NotNull(historicalRecords);
        Assert.Equal(2, historicalRecords.Count());

        // Act
        await _recordBusiness.ArchiveRecord(uid, organizationId, pid, rid);
        var arcHistoricalRecords = await _historicalRecordBusiness.GetAllHistoricalRecords(pid, organizationId);

        // Assert
        Assert.NotNull(arcHistoricalRecords);
        Assert.Single(arcHistoricalRecords);
        Assert.DoesNotContain(arcHistoricalRecords, x => x.Name == "Test Record");
        Assert.Contains(arcHistoricalRecords, x => x.Name == "Test Record 2");
    }

    [Fact]
    public async Task GetHistoricalRecords_ReturnsEmptyListWhenNoRecords()
    {
        // Arrange
        await _recordBusiness.DeleteRecord(uid, organizationId, pid, rid);
        await _recordBusiness.DeleteRecord(uid, organizationId, pid, rid2);

        // Act
        var historicalRecords = await _historicalRecordBusiness.GetAllHistoricalRecords(pid, organizationId);

        // Assert
        Assert.NotNull(historicalRecords);
        Assert.Empty(historicalRecords);
    }

    [Fact]
    public async Task GetHistoricalRecords_FiltersByDataSource()
    {
        // Act
        var historicalRecords = await _historicalRecordBusiness.GetAllHistoricalRecords(pid2, organizationId, did2);

        // Assert
        Assert.NotNull(historicalRecords);
        Assert.Single(historicalRecords);
        Assert.Contains(historicalRecords, x => x.Name == "Test Record 3");
        Assert.DoesNotContain(historicalRecords, x => x.Name == "Test Record 4");
    }

    [Fact]
    public async Task GetHistoricalRecords_FiltersByTime()
    {
        // Arrange
        var pointInTime = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var testRecordLate = new Record
        {
            Name = "Test Record Late",
            Description = "Test record late for unit tests",
            OriginalId = "og_idlate",
            Properties = JsonSerializer.Serialize(new { TestProperty = "TestValue late" }),
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            Uri = "localhost:8090",
            OrganizationId = organizationId
        };

        Context.Records.Add(testRecordLate);
        await Context.SaveChangesAsync();

        // Act
        var historicalRecords =
            await _historicalRecordBusiness.GetAllHistoricalRecords(pid, organizationId, null, pointInTime, false);

        // Assert
        Assert.NotNull(historicalRecords);
        Assert.Equal(2, historicalRecords.Count());
        Assert.Contains(historicalRecords, x => x.Name == "Test Record");
        Assert.Contains(historicalRecords, x => x.Name == "Test Record 2");
    }

    #endregion

    #region GetHistoryForRecord Tests

    [Fact]
    public async Task GetHistoryForRecord_ReturnsFullHistory()
    {
        // Arrange
        var dto = new UpdateRecordRequestDto
        {
            Name = "Updated Test Record",
            Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { UpdatedProp = "UpdatedValue" }))!,
            Uri = "updated://uri",
            OriginalId = "updated-123",
            Description = "Updated Description",
            ClassId = cid
        };

        // Act
        await _recordBusiness.UpdateRecord(uid, organizationId, pid, rid, dto);
        await _recordBusiness.ArchiveRecord(uid, organizationId, pid, rid);
        var recordHistory = await _historicalRecordBusiness.GetHistoryForRecord(rid, organizationId);


        // Assert
        Assert.NotNull(recordHistory);
        Assert.Equal(4, recordHistory.Count());
        Assert.Contains(recordHistory, x => x.Name == "Test Record" && x.Tags == null);
        Assert.Contains(recordHistory, x => x.Name == "Test Record" && x.Tags != null);
        Assert.Contains(recordHistory, x => x.Name == "Updated Test Record" && !x.IsArchived);
        Assert.Contains(recordHistory, x => x.Name == "Updated Test Record" && x.IsArchived);
    }

    [Fact]
    public async Task GetHistoryForRecord_ThrowsError_WhenRecordDoesNotExist()
    {
        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _historicalRecordBusiness.GetHistoryForRecord(rid + 100000, organizationId));
    }

    #endregion

    #region GetHistoricalRecord Tests

    [Fact]
    public async Task GetHistoricalRecord_ReturnsAllCorrectFields()
    {
        // Arrange
        // TODO: insert tags after record to avoid race condition
        var record = await Context.Records.Where(r => r.ProjectId == pid && r.Id == rid).FirstOrDefaultAsync();
        Assert.NotNull(record);

        // Act
        var historicalRecord = await _historicalRecordBusiness.GetHistoricalRecord(rid, organizationId, null);

        // Assert
        Assert.NotNull(historicalRecord);
        Assert.Equal(record.Name, historicalRecord.Name);
        Assert.Equal(record.Id, historicalRecord.Id);
        Assert.NotNull(historicalRecord.Tags);
        Assert.Equal(record.ClassId, historicalRecord.ClassId);
        Assert.Equal("Test Class", historicalRecord.ClassName);
        Assert.Equal(record.Description, historicalRecord.Description);
        Assert.Equal(record.OriginalId, historicalRecord.OriginalId);
        Assert.Equal(record.Uri, historicalRecord.Uri);
        Assert.Equal(record.ObjectStorageId, historicalRecord.ObjectStorageId);
        Assert.Equal("Object Storage 1", historicalRecord.ObjectStorageName);
        Assert.Equal(record.ProjectId, historicalRecord.ProjectId);
        Assert.Equal("Test Project", historicalRecord.ProjectName);
        Assert.Equal(record.DataSourceId, historicalRecord.DataSourceId);
        Assert.Equal("Test Data Source", historicalRecord.DataSourceName);
    }

    [Fact]
    public async Task GetHistoricalRecord_ReturnsAllCorrectFields_AfterUpdate()
    {
        // Arrange
        var dto = new UpdateRecordRequestDto
        {
            Name = "Updated Test Record",
            Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { UpdatedProp = "UpdatedValue" }))!,
            Uri = "updated://uri",
            OriginalId = "updated-123",
            Description = "Updated Description",
            ClassId = cid
        };

        var updatedRecord = await _recordBusiness.UpdateRecord(uid, organizationId, pid, rid, dto);
        Assert.NotNull(updatedRecord);

        // Act
        var historicalRecord = await _historicalRecordBusiness.GetHistoricalRecord(rid, organizationId, null);

        // Assert
        Assert.NotNull(historicalRecord);
        Assert.Equal(updatedRecord.Name, historicalRecord.Name);
        Assert.Equal(updatedRecord.Id, historicalRecord.Id);
        Assert.NotNull(historicalRecord.Tags);
        Assert.Equal(updatedRecord.ClassId, historicalRecord.ClassId);
        Assert.Equal("Test Class", historicalRecord.ClassName);
        Assert.Equal(updatedRecord.Description, historicalRecord.Description);
        Assert.Equal(updatedRecord.OriginalId, historicalRecord.OriginalId);
        Assert.Equal(updatedRecord.Uri, historicalRecord.Uri);
        Assert.Equal(updatedRecord.ObjectStorageId, historicalRecord.ObjectStorageId);
        Assert.Equal("Object Storage 1", historicalRecord.ObjectStorageName);
        Assert.Equal(updatedRecord.ProjectId, historicalRecord.ProjectId);
        Assert.Equal("Test Project", historicalRecord.ProjectName);
        Assert.Equal(updatedRecord.DataSourceId, historicalRecord.DataSourceId);
        Assert.Equal("Test Data Source", historicalRecord.DataSourceName);
    }

    [Fact]
    public async Task GetHistoricalRecord_ReturnsAllCorrectFields_AfterArchive()
    {
        // Arrange
        var archived = await _recordBusiness.ArchiveRecord(uid, organizationId, pid, rid);
        Assert.True(archived);

        var archivedRecord = await _recordBusiness.GetRecord(organizationId, pid, rid, false);
        Assert.NotNull(archivedRecord);

        // Act
        var historicalRecord = await _historicalRecordBusiness.GetHistoricalRecord(rid, organizationId, null, false);

        // Assert
        Assert.NotNull(historicalRecord);
        Assert.Equal(archivedRecord.Name, historicalRecord.Name);
        Assert.Equal(archivedRecord.Id, historicalRecord.Id);
        Assert.NotNull(historicalRecord.Tags);
        Assert.Equal(archivedRecord.ClassId, historicalRecord.ClassId);
        Assert.Equal("Test Class", historicalRecord.ClassName);
        Assert.Equal(archivedRecord.Description, historicalRecord.Description);
        Assert.Equal(archivedRecord.OriginalId, historicalRecord.OriginalId);
        Assert.Equal(archivedRecord.Uri, historicalRecord.Uri);
        Assert.Equal(archivedRecord.ObjectStorageId, historicalRecord.ObjectStorageId);
        Assert.Equal("Object Storage 1", historicalRecord.ObjectStorageName);
        Assert.Equal(archivedRecord.ProjectId, historicalRecord.ProjectId);
        Assert.Equal("Test Project", historicalRecord.ProjectName);
        Assert.Equal(archivedRecord.DataSourceId, historicalRecord.DataSourceId);
        Assert.Equal("Test Data Source", historicalRecord.DataSourceName);
    }

    [Fact]
    public async Task GetHistoricalRecord_ReturnsMostCurrentRecord_WhenCurrentIsTrue()
    {
        // Arrange
        var dto = new UpdateRecordRequestDto
        {
            Name = "Updated Test Record",
            Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { UpdatedProp = "UpdatedValue" }))!,
            Uri = "updated://uri",
            OriginalId = "updated-123",
            Description = "Updated Description",
            ClassId = cid
        };

        await _recordBusiness.UpdateRecord(uid, organizationId, pid, rid, dto);


        // Act
        var historicalRecord = await _historicalRecordBusiness.GetHistoricalRecord(rid, organizationId, null);

        // Assert
        Assert.NotNull(historicalRecord);
        Assert.Equal("Updated Test Record", historicalRecord.Name);
    }

    [Fact]
    public async Task GetHistoricalRecord_ReturnsMostRecentRecordByDefault()
    {
        // Arrange
        var dto = new UpdateRecordRequestDto
        {
            Name = "Updated Test Record",
            Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { UpdatedProp = "UpdatedValue" }))!,
            Uri = "updated://uri",
            OriginalId = "updated-123",
            Description = "Updated Description",
            ClassId = cid
        };

        // Act
        await _recordBusiness.UpdateRecord(uid, organizationId, pid, rid, dto);
        var historicalRecord = await _historicalRecordBusiness.GetHistoricalRecord(rid, organizationId, null);

        // Assert
        Assert.NotNull(historicalRecord);
        Assert.Equal("Updated Test Record", historicalRecord.Name);
    }

    [Fact]
    public async Task GetHistoricalRecord_CanIncludeArchived()
    {
        // Arrange
        var dto = new UpdateRecordRequestDto
        {
            Name = "Updated Test Record",
            Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { UpdatedProp = "UpdatedValue" }))!,
            Uri = "updated://uri",
            OriginalId = "updated-123",
            Description = "Updated Description",
            ClassId = cid
        };

        // Act
        await _recordBusiness.UpdateRecord(uid, organizationId, pid, rid, dto);
        await _recordBusiness.ArchiveRecord(uid, organizationId, pid, rid);
        var historicalRecord = await _historicalRecordBusiness.GetHistoricalRecord(rid, organizationId, null, false);

        // Assert
        Assert.NotNull(historicalRecord);
        Assert.Equal("Updated Test Record", historicalRecord.Name);
        Assert.True(historicalRecord.IsArchived);
    }

    // Ask if this should be good behavior
    [Fact]
    public async Task GetHistoricalRecord_ThrowsError_WhenCurrentRecordIsArchived()
    {
        // Arrange
        var dto = new UpdateRecordRequestDto
        {
            Name = "Updated Test Record",
            Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { UpdatedProp = "UpdatedValue" }))!,
            Uri = "updated://uri",
            OriginalId = "updated-123",
            Description = "Updated Description",
            ClassId = cid
        };

        await _recordBusiness.UpdateRecord(uid, organizationId, pid, rid, dto);
        await _recordBusiness.ArchiveRecord(uid, organizationId, pid, rid);

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _historicalRecordBusiness.GetHistoricalRecord(rid, organizationId, null));
        Assert.Contains($"Historical record with id {rid} not found or is archived", exception.Message);
    }


    [Fact]
    public async Task GetHistoricalRecord_FiltersByTime()
    {
        // Arrange
        var pointInTime = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var dto = new UpdateRecordRequestDto
        {
            Name = "Updated Test Record",
            Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { UpdatedProp = "UpdatedValue" }))!,
            Uri = "updated://uri",
            OriginalId = "updated-123",
            Description = "Updated Description",
            ClassId = cid
        };

        // Act
        await _recordBusiness.UpdateRecord(uid, organizationId, pid, rid, dto);
        var historicalRecord = await _historicalRecordBusiness.GetHistoricalRecord(rid, organizationId, pointInTime);

        // Assert
        Assert.NotNull(historicalRecord);
        Assert.Equal("Test Record", historicalRecord.Name);
    }

    [Fact]
    public async Task GetHistoricalRecord_ThrowsError_WhenRecordDoesNotExist()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _historicalRecordBusiness.GetHistoricalRecord(rid + 100000, organizationId, null));
        Assert.Contains($"Historical record with id {rid + 100000} not found", exception.Message);
    }

    #endregion
}