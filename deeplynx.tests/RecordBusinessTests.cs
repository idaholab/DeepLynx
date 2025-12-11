using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using deeplynx.business;
using deeplynx.datalayer.Models;
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
public class RecordBusinessTests : IntegrationTestBase
{
    private EventBusiness _eventBusiness;
    private Mock<IHubContext<EventNotificationHub>> _mockHubContext = null!;
    private Mock<ILogger<NotificationBusiness>> _mockNotificationLogger = null!;
    private INotificationBusiness _notificationBusiness = null!;
    private RecordBusiness _recordBusiness;
    private TagBusiness _tagBusiness = null!;
    public long cid; // class ID
    public long did; // datasource ID
    private long organizationId;
    public long osid; // object storage ID
    public long pid; // project ID
    public long pid2;
    public string rdesc;
    public string rfiletype;
    public long rid; // record ID
    public string rogid;
    public string rprop; // additional record props
    public string ruri;
    public long tid; // tag ID
    public long uid;

    public RecordBusinessTests(TestSuiteFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _mockHubContext = new Mock<IHubContext<EventNotificationHub>>();
        _mockNotificationLogger = new Mock<ILogger<NotificationBusiness>>();
        _notificationBusiness =
            new NotificationBusiness(Context, _mockNotificationLogger.Object, _mockHubContext.Object);
        _eventBusiness = new EventBusiness(Context, _notificationBusiness);
        _tagBusiness = new TagBusiness(Context, _eventBusiness);
        _recordBusiness = new RecordBusiness(Context, _eventBusiness, _tagBusiness);
    }

    #region RecordResponseDto Tests

    [Fact]
    public void RecordResponseDto_AllProperties_CanBeSetAndRetrieved()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var tags = new List<RecordTagDto>
        {
            new() { Id = 1, Name = "Test Tag" }
        };

        var dto = new RecordResponseDto
        {
            Id = 1,
            Name = "Test Record",
            Description = "Test Description",
            Uri = "test://uri",
            Properties = "{\"test\":\"value\"}",
            ObjectStorageId = 100,
            OriginalId = "original-123",
            ClassId = 200,
            DataSourceId = 300,
            ProjectId = 400,
            LastUpdatedAt = now,
            LastUpdatedBy = uid,
            IsArchived = false,
            FileType = "pdf",
            Tags = tags
        };

        // Assert
        Assert.Equal(1, dto.Id);
        Assert.Equal("Test Record", dto.Name);
        Assert.Equal("Test Description", dto.Description);
        Assert.Equal("test://uri", dto.Uri);
        Assert.Equal("{\"test\":\"value\"}", dto.Properties);
        Assert.Equal(100, dto.ObjectStorageId);
        Assert.Equal("original-123", dto.OriginalId);
        Assert.Equal(200, dto.ClassId);
        Assert.Equal(300, dto.DataSourceId);
        Assert.Equal(400, dto.ProjectId);
        Assert.Equal(now, dto.LastUpdatedAt);
        Assert.Equal(uid, dto.LastUpdatedBy);
        Assert.False(dto.IsArchived);
        Assert.Equal("pdf", dto.FileType);
        Assert.Single(dto.Tags);
        Assert.Equal("Test Tag", dto.Tags.First().Name);
    }

    #endregion

    protected override async Task SeedTestDataAsync()
    {
        await base.SeedTestDataAsync();

        var user = new User
        {
            Name = "Test User",
            Email = "test_record@example.com",
            Password = "test_password",
            IsArchived = false
        };
        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        uid = user.Id;

        var organization = new Organization { Name = "Test Organization" };
        Context.Organizations.Add(organization);
        await Context.SaveChangesAsync();
        organizationId = organization.Id;

        // Add projects
        var project = new Project
        {
            Name = "Test Project",
            Description = "Test project for unit tests",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            OrganizationId = organizationId
        };
        var project2 = new Project
        {
            Name = "Test Project 2",
            Description = "Test project 2 for unit tests",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            OrganizationId = organizationId
        };
        Context.Projects.Add(project);
        Context.Projects.Add(project2);
        await Context.SaveChangesAsync();
        pid = project.Id;
        pid2 = project2.Id;

        // Add datasource
        var dataSource = new DataSource
        {
            Name = "Test Data Source",
            Description = "Test data source for unit tests",
            ProjectId = pid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            OrganizationId = organizationId
        };
        Context.DataSources.Add(dataSource);
        await Context.SaveChangesAsync();
        did = dataSource.Id;


        // Add class
        var testClass = new Class
        {
            Name = "Test Class",
            Description = "Test class for unit tests",
            ProjectId = pid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            OrganizationId = organizationId
        };
        Context.Classes.Add(testClass);
        await Context.SaveChangesAsync();
        cid = testClass.Id;

        // Add object storage
        var config = new JsonObject();
        var objectStorage = new ObjectStorage
        {
            Name = "Object Storage 1",
            Type = "filesystem",
            Config = config.ToString(),
            ProjectId = pid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            OrganizationId = organizationId
        };
        Context.ObjectStorages.Add(objectStorage);
        await Context.SaveChangesAsync();
        osid = objectStorage.Id;

        // Add tag
        var testTag = new Tag
        {
            Name = "Test Tag",
            ProjectId = pid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            OrganizationId = organizationId
        };

        // Add record
        var testRecord = new Record
        {
            Name = "Test Record",
            Description = "Test record for unit tests",
            OriginalId = "og_id",
            Properties = JsonSerializer.Serialize(new { TestProperty = "TestValue" }),
            ProjectId = pid,
            DataSourceId = dataSource.Id,
            ClassId = testClass.Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            Tags = new List<Tag> { testTag },
            Uri = "localhost:8090",
            FileType = "pdf",
            OrganizationId = organizationId
        };

        Context.Records.Add(testRecord);
        Context.Tags.Add(testTag);
        await Context.SaveChangesAsync();

        rid = testRecord.Id;
        tid = testTag.Id;
        rprop = testRecord.Properties;
        rogid = testRecord.OriginalId;
        rdesc = testRecord.Description;
        ruri = testRecord.Uri;
        rfiletype = testRecord.FileType;
    }

    #region GetRecordsCountByDataSource Tests

    [Fact]
    public async Task GetRecordsCountByDataSource_ValidDataSource_ReturnsCount()
    {
        // Act
        var result = await _recordBusiness.GetRecordsCountByDataSource(
            organizationId, pid, did, true);

        // Assert
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task GetRecordsCountByDataSource_NonExistentDataSource_ThrowsKeyNotFoundException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordBusiness.GetRecordsCountByDataSource(organizationId, pid, 999L, true));

        Assert.Contains("DataSource with id 999 not found", exception.Message);
    }

    [Fact]
    public async Task GetRecordsCountByDataSource_WrongProject_ThrowsKeyNotFoundException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordBusiness.GetRecordsCountByDataSource(organizationId, pid2, did, true));

        Assert.Contains($"DataSource with id {did} not found", exception.Message);
    }

    [Fact]
    public async Task GetRecordsCountByDataSource_WithArchivedRecords_HideArchivedTrue_ExcludesArchived()
    {
        // Arrange - Archive the existing record
        var record = await Context.Records.FindAsync(rid);
        record!.IsArchived = true;
        await Context.SaveChangesAsync();

        // Act
        var result = await _recordBusiness.GetRecordsCountByDataSource(
            organizationId, pid, did, true);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task GetRecordsCountByDataSource_WithArchivedRecords_HideArchivedFalse_IncludesArchived()
    {
        // Arrange - Archive the existing record
        var record = await Context.Records.FindAsync(rid);
        record!.IsArchived = true;
        await Context.SaveChangesAsync();

        // Act
        var result = await _recordBusiness.GetRecordsCountByDataSource(
            organizationId, pid, did, false);

        // Assert
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task GetRecordsCountByDataSource_NoRecords_ReturnsZero()
    {
        // Arrange - Create a new data source with no records
        var emptyDataSource = new DataSource
        {
            Name = "Empty Data Source",
            Description = "Data source with no records",
            ProjectId = pid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            OrganizationId = organizationId
        };
        Context.DataSources.Add(emptyDataSource);
        await Context.SaveChangesAsync();

        // Act
        var result = await _recordBusiness.GetRecordsCountByDataSource(
            organizationId, pid, emptyDataSource.Id, true);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task GetRecordsCountByDataSource_MultipleDataSources_OnlyCountsSpecificDataSource()
    {
        // Arrange - Create a second data source in the same project
        var dataSource2 = new DataSource
        {
            Name = "Test Data Source 2",
            Description = "Second data source for unit tests",
            ProjectId = pid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            OrganizationId = organizationId
        };
        Context.DataSources.Add(dataSource2);
        await Context.SaveChangesAsync();

        // Create additional records for the second data source
        var record2 = new Record
        {
            Name = "Test Record 2",
            Description = "Second test record",
            OriginalId = "og_id_2",
            Properties = JsonSerializer.Serialize(new { TestProperty = "TestValue2" }),
            ProjectId = pid,
            DataSourceId = dataSource2.Id,
            ClassId = cid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            OrganizationId = organizationId
        };

        var record3 = new Record
        {
            Name = "Test Record 3",
            Description = "Third test record",
            OriginalId = "og_id_3",
            Properties = JsonSerializer.Serialize(new { TestProperty = "TestValue3" }),
            ProjectId = pid,
            DataSourceId = dataSource2.Id,
            ClassId = cid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            OrganizationId = organizationId
        };

        Context.Records.Add(record2);
        Context.Records.Add(record3);
        await Context.SaveChangesAsync();

        // Act - Get count for first data source (should only have 1 record)
        var result1 = await _recordBusiness.GetRecordsCountByDataSource(
            organizationId, pid, did, true);

        // Act - Get count for second data source (should have 2 records)
        var result2 = await _recordBusiness.GetRecordsCountByDataSource(
            organizationId, pid, dataSource2.Id, true);

        // Assert
        Assert.Equal(1, result1); // Original data source has 1 record
        Assert.Equal(2, result2); // New data source has 2 records
    }

    #endregion

    #region GetAllRecords Tests

    [Fact]
    public async Task GetAllRecords_ValidProjectId_ReturnsRecords()
    {
        // Act
        var result = await _recordBusiness.GetAllRecords(organizationId, pid, null, true);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("Test Record", result.First().Name);
    }

    [Fact]
    public async Task GetAllRecords_ReturnsTags()
    {
        // Act
        var result = await _recordBusiness.GetAllRecords(organizationId, pid, null, true);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.First().Tags);
        Assert.Equal("Test Tag", result.First().Tags.First().Name);
        Assert.Single(result);
        Assert.Equal("Test Record", result.First().Name);
    }

    [Fact]
    public async Task GetAllRecords_WithDataSourceId_ReturnsFilteredRecords()
    {
        // Act
        var result = await _recordBusiness.GetAllRecords(organizationId, pid, did, true);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(did, result.First().DataSourceId);
    }

    [Fact]
    public async Task GetAllRecords_WithFileType_ReturnsFilteredRecords()
    {
        // Arrange - Make sure incorrect fileType filter results in no results (we only have 1 record seeded and its of pdf type)
        var incorrectFileTypeResponse = await _recordBusiness.GetAllRecords(organizationId, pid, did, true, "png");
        Assert.Empty(incorrectFileTypeResponse);

        // Act
        var correctFileTypeResponse = await _recordBusiness.GetAllRecords(organizationId, pid, did, true, "pdf");

        // Assert
        Assert.NotNull(correctFileTypeResponse);
        Assert.Single(correctFileTypeResponse);
        Assert.Equal("pdf", correctFileTypeResponse.First().FileType);
    }

    #endregion

    #region GetRecordsByTags Tests

    [Fact]
    public async Task GetRecordsByTags_ValidProjectIdWithSingleTag_ReturnsMatchingRecords()
    {
        // Act
        var result = await _recordBusiness.GetRecordsByTags(organizationId, pid, [tid], true);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("Test Record", result.First().Name);
        Assert.Single(result.First().Tags);
        Assert.Equal("Test Tag", result.First().Tags.First().Name);
    }

    [Fact]
    public async Task GetRecordsByTags_WithMultipleTags_ReturnsOnlyRecordsWithAllTags()
    {
        // Arrange - Add additional tag
        var tag2 = new Tag
        {
            Name = "Tag2",
            ProjectId = pid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = organizationId
        };
        Context.Tags.Add(tag2);
        await Context.SaveChangesAsync();

        var testTag = await Context.Tags.FindAsync(tid);

        var recordWithAllTags = new Record
        {
            Name = "Record With All Tags",
            Description = "Has testTag and tag2",
            OriginalId = "multi_tag_record",
            Properties = "{}",
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            Tags = new List<Tag> { testTag, tag2 },
            Uri = "localhost:8090",
            FileType = "pdf", OrganizationId = organizationId
        };

        var recordWithSomeTags = new Record
        {
            Name = "Record With Some Tags",
            Description = "Has only testTag",
            OriginalId = "partial_tag_record",
            Properties = "{}",
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            Tags = new List<Tag> { testTag },
            Uri = "localhost:8090",
            FileType = "pdf", OrganizationId = organizationId
        };

        Context.Records.AddRange(recordWithAllTags, recordWithSomeTags);
        await Context.SaveChangesAsync();

        // Act - Query for records with both testTag AND tag2
        var result = await _recordBusiness.GetRecordsByTags(organizationId, pid, [tid, tag2.Id], true);

        // Assert - Should only get the record with ALL tags
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("Record With All Tags", result.First().Name);
        Assert.Equal(2, result.First().Tags.Count);
    }

    [Fact]
    public async Task GetRecordsByTags_WithMultipleTags_DifferentProject_ReturnsEmpty()
    {
        // Arrange - Add additional tag
        var tag2 = new Tag
        {
            Name = "Tag2",
            ProjectId = pid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = organizationId
        };
        Context.Tags.Add(tag2);
        await Context.SaveChangesAsync();

        var testTag = await Context.Tags.FindAsync(tid);

        var recordWithAllTags = new Record
        {
            Name = "Record With All Tags",
            Description = "Has testTag and tag2",
            OriginalId = "multi_tag_different_project",
            Properties = "{}",
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            Tags = new List<Tag> { testTag, tag2 },
            Uri = "localhost:8090",
            FileType = "pdf", OrganizationId = organizationId
        };

        Context.Records.Add(recordWithAllTags);
        await Context.SaveChangesAsync();

        // Act - Query for records with both tags but in different valid project (pid2)
        var result = await _recordBusiness.GetRecordsByTags(organizationId, pid2, [tid, tag2.Id], true);

        // Assert - Should return empty because records exist in pid, not pid2
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetRecordsByTags_EmptyTagArray_ReturnsAllNonArchivedRecords()
    {
        // Act
        var result = await _recordBusiness.GetRecordsByTags(organizationId, pid, [], true);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result); // Only the seeded record
        Assert.Equal("Test Record", result.First().Name);
    }

    [Fact]
    public async Task GetRecordsByTags_HideArchivedTrue_ExcludesArchivedRecords()
    {
        // Arrange - Add an archived record with the same tag
        var testTag = await Context.Tags.FindAsync(tid);

        var archivedRecord = new Record
        {
            Name = "Archived Record",
            Description = "Archived",
            OriginalId = "archived_record",
            Properties = "{}",
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            IsArchived = true,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            Tags = new List<Tag> { testTag },
            Uri = "localhost:8090",
            FileType = "pdf",
            OrganizationId = organizationId
        };

        Context.Records.Add(archivedRecord);
        await Context.SaveChangesAsync();

        // Act
        var result = await _recordBusiness.GetRecordsByTags(organizationId, pid, [tid], true);

        // Assert - Should only get the non-archived seeded record
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("Test Record", result.First().Name);
        Assert.False(result.First().IsArchived);
    }

    [Fact]
    public async Task GetRecordsByTags_HideArchivedFalse_IncludesArchivedRecords()
    {
        // Arrange - Add an archived record with the same tag
        var testTag = await Context.Tags.FindAsync(tid);

        var archivedRecord = new Record
        {
            Name = "Archived Record",
            Description = "Archived",
            OriginalId = "archived_record_2",
            Properties = "{}",
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            IsArchived = true,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            Tags = new List<Tag> { testTag },
            Uri = "localhost:8090",
            FileType = "pdf",
            OrganizationId = organizationId
        };

        Context.Records.Add(archivedRecord);
        await Context.SaveChangesAsync();

        // Act
        var result = await _recordBusiness.GetRecordsByTags(organizationId, pid, [tid], false);

        // Assert - Should get both archived and non-archived records
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.Name == "Test Record" && !r.IsArchived);
        Assert.Contains(result, r => r.Name == "Archived Record" && r.IsArchived);
    }

    [Fact]
    public async Task GetRecordsByTags_NonExistentTag_ReturnsEmpty()
    {
        // Arrange - Make sure non-existent tag results in no results
        var nonExistentTagResult = await _recordBusiness.GetRecordsByTags(organizationId, pid, [99999], true);
        Assert.Empty(nonExistentTagResult);

        // Act - Verify correct tag returns results
        var correctTagResult = await _recordBusiness.GetRecordsByTags(organizationId, pid, [tid], true);

        // Assert
        Assert.NotNull(correctTagResult);
        Assert.Single(correctTagResult);
        Assert.Equal("Test Record", correctTagResult.First().Name);
    }

    #endregion

    #region GetRecord Tests

    [Fact]
    public async Task GetRecord_ValidIds_ReturnsRecord()
    {
        // Act
        var result = await _recordBusiness.GetRecord(organizationId, pid, rid, true);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(rid, result.Id);
        Assert.Equal("Test Record", result.Name);
    }

    [Fact]
    public async Task GetRecord_WrongProject_ThrowsKeyNotFoundException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordBusiness.GetRecord(organizationId, 999L, rid, true));

        Assert.Contains($"Record with id {rid} not found", exception.Message);
    }

    #endregion

    #region CreateRecord Tests

    [Fact]
    public async Task CreateRecord_ValidData_CreatesRecord()
    {
        // Arrange

        var now = DateTime.UtcNow;
        var dto = new CreateRecordRequestDto
        {
            Name = "New Test Record",
            Description = "Test Record Description",
            Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { TestProp = "TestValue" }))!,
            Uri = "test://uri",
            OriginalId = "original-123",
            ClassId = cid,
            FileType = "png"
        };

        // Act
        var result = await _recordBusiness.CreateRecord(uid, organizationId, pid, did, dto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("New Test Record", result.Name);
        Assert.Equal("Test Record Description", result.Description);
        Assert.Equal(pid, result.ProjectId);
        Assert.Equal(did, result.DataSourceId);
        Assert.Equal("test://uri", result.Uri);
        Assert.Equal("original-123", result.OriginalId);
        Assert.Equal(cid, result.ClassId);
        Assert.Equal("png", result.FileType);
        Assert.True(result.LastUpdatedAt >= now);
        Assert.Equal(uid, result.LastUpdatedBy);

        // Verify record was actually created in database
        var createdRecord = await Context.Records.FindAsync(result.Id);
        Assert.NotNull(createdRecord);
        Assert.Equal("New Test Record", createdRecord.Name);

        // Ensure that record create event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal(createdRecord.ProjectId, actualEvent.ProjectId);
        Assert.Equal("create", actualEvent.Operation);
        Assert.Equal("record", actualEvent.EntityType);
        Assert.Equal(createdRecord.Id, actualEvent.EntityId);
    }

    [Fact]
    public async Task CreateRecord_InvalidProjectId_ThrowsKeyNotFoundException()
    {
        // Arrange
        var dto = new CreateRecordRequestDto
        {
            Name = "Test Record",
            Description = "Test Record Description",
            OriginalId = "original-123",
            Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { TestProp = "TestValue" }))!
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordBusiness.CreateRecord(uid, organizationId, 1000999L, did, dto));

        Assert.Contains($"DataSource with id {did} not found in project", exception.Message);

        // Ensure that no record create event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task CreateRecord_InvalidDataSourceId_ThrowsKeyNotFoundException()
    {
        // Arrange
        var dto = new CreateRecordRequestDto
        {
            Name = "Test Record",
            Description = "Test Record Description",
            OriginalId = "original-123",
            Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { TestProp = "TestValue" }))!
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordBusiness.CreateRecord(uid, organizationId, pid, 999L, dto));

        Assert.Contains($"DataSource with id 999 not found in project with id {pid}", exception.Message);

        // Ensure that no record create event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task CreateRecord_TooDeepJson_ThrowsException()
    {
        // Arrange
        var deepJson = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new
        {
            Level1 = new
            {
                Level2 = new
                {
                    Level3 = new
                    {
                        Level4 = new
                        {
                            Value = "Too deep"
                        }
                    }
                }
            }
        }))!;

        var dto = new CreateRecordRequestDto
        {
            Name = "Deep JSON Record",
            Description = "Deep JSON Record Description",
            OriginalId = "original-123",
            Properties = deepJson
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() =>
            _recordBusiness.CreateRecord(uid, organizationId, pid, did, dto));
        Assert.Contains("depth of the JSON structure exceeds", exception.Message);

        // Ensure that no record create event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task CreateRecord_WithInvalidDataSource_ThrowsException()
    {
        var dataSourceInWrongProject = new DataSource
        {
            Name = "Test Data Source",
            Description = "Test data source for unit tests",
            ProjectId = pid2,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = organizationId
        };
        Context.DataSources.Add(dataSourceInWrongProject);
        await Context.SaveChangesAsync();

        var dto = new CreateRecordRequestDto
        {
            Name = "Invalid Record",
            Description = "Invalid Record Description",
            OriginalId = "original-12334532",
            Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { TestProp = "TestValue" }))!
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordBusiness.CreateRecord(uid, organizationId, pid, dataSourceInWrongProject.Id, dto));

        Assert.Contains($"DataSource with id {dataSourceInWrongProject.Id} not found in project with id {pid}",
            exception.Message);

        // Ensure that no record create event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task CreateRecord_WithExistingTags_AttachesTagsToRecord()
    {
        // Arrange
        var dto = new CreateRecordRequestDto
        {
            Name = "Record With Existing Tag",
            Description = "Test Record with existing tag",
            Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { TestProp = "TestValue" }))!,
            OriginalId = "original-with-tag",
            Tags = new List<string> { "Test Tag" } // This tag already exists in seed data
        };

        // Act
        var result = await _recordBusiness.CreateRecord(uid, organizationId, pid, did, dto);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Tags);
        Assert.Single(result.Tags);
        Assert.Equal("Test Tag", result.Tags.First().Name);
        Assert.Equal(tid, result.Tags.First().Id); // Should be the existing tag ID

        // Verify in database
        var createdRecord = await Context.Records
            .Include(r => r.Tags)
            .FirstOrDefaultAsync(r => r.Id == result.Id);
        Assert.NotNull(createdRecord);
        Assert.Single(createdRecord.Tags);
        Assert.Equal(tid, createdRecord.Tags.First().Id);
    }

    [Fact]
    public async Task CreateRecord_WithNewTags_CreatesAndAttachesNewTags()
    {
        // Arrange
        var dto = new CreateRecordRequestDto
        {
            Name = "Record With New Tags",
            Description = "Test Record with new tags",
            Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { TestProp = "TestValue" }))!,
            OriginalId = "original-new-tags",
            Tags = new List<string> { "New Tag 1", "New Tag 2" }
        };

        // Act
        var result = await _recordBusiness.CreateRecord(uid, organizationId, pid, did, dto);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Tags);
        Assert.Equal(2, result.Tags.Count);

        var tagNames = result.Tags.Select(t => t.Name).OrderBy(n => n).ToList();
        Assert.Equal("New Tag 1", tagNames[0]);
        Assert.Equal("New Tag 2", tagNames[1]);

        // Verify tags were created in database
        var newTag1 = await Context.Tags.FirstOrDefaultAsync(t => t.Name == "New Tag 1" && t.ProjectId == pid);
        var newTag2 = await Context.Tags.FirstOrDefaultAsync(t => t.Name == "New Tag 2" && t.ProjectId == pid);
        Assert.NotNull(newTag1);
        Assert.NotNull(newTag2);

        // Verify record-tag associations
        var createdRecord = await Context.Records
            .Include(r => r.Tags)
            .FirstOrDefaultAsync(r => r.Id == result.Id);
        Assert.NotNull(createdRecord);
        Assert.Equal(2, createdRecord.Tags.Count);
    }

    [Fact]
    public async Task CreateRecord_WithMixedExistingAndNewTags_HandlesCorrectly()
    {
        // Arrange
        var dto = new CreateRecordRequestDto
        {
            Name = "Record With Mixed Tags",
            Description = "Test Record with mixed tags",
            Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { TestProp = "TestValue" }))!,
            OriginalId = "original-mixed-tags",
            Tags = new List<string> { "Test Tag", "Brand New Tag", "Another New Tag" }
        };

        // Act
        var result = await _recordBusiness.CreateRecord(uid, organizationId, pid, did, dto);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Tags);
        Assert.Equal(3, result.Tags.Count);

        var existingTag = result.Tags.FirstOrDefault(t => t.Name == "Test Tag");
        Assert.NotNull(existingTag);
        Assert.Equal(tid, existingTag.Id); // Should reuse existing tag

        var newTag1 = result.Tags.FirstOrDefault(t => t.Name == "Brand New Tag");
        var newTag2 = result.Tags.FirstOrDefault(t => t.Name == "Another New Tag");
        Assert.NotNull(newTag1);
        Assert.NotNull(newTag2);

        // Verify in database
        var createdRecord = await Context.Records
            .Include(r => r.Tags)
            .FirstOrDefaultAsync(r => r.Id == result.Id);
        Assert.NotNull(createdRecord);
        Assert.Equal(3, createdRecord.Tags.Count);
    }

    [Fact]
    public async Task CreateRecord_WithoutTags_CreatesRecordSuccessfully()
    {
        // Arrange
        var dto = new CreateRecordRequestDto
        {
            Name = "Record Without Tags",
            Description = "Test Record without tags",
            Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { TestProp = "TestValue" }))!,
            OriginalId = "original-no-tags"
        };

        // Act
        var result = await _recordBusiness.CreateRecord(uid, organizationId, pid, did, dto);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Tags);
        Assert.Empty(result.Tags);

        // Verify in database
        var createdRecord = await Context.Records
            .Include(r => r.Tags)
            .FirstOrDefaultAsync(r => r.Id == result.Id);
        Assert.NotNull(createdRecord);
        Assert.Empty(createdRecord.Tags);
    }

    [Fact]
    public async Task CreateRecord_WithEmptyTagsList_CreatesRecordSuccessfully()
    {
        // Arrange
        var dto = new CreateRecordRequestDto
        {
            Name = "Record With Empty Tags List",
            Description = "Test Record with empty tags list",
            Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { TestProp = "TestValue" }))!,
            OriginalId = "original-empty-tags",
            Tags = new List<string>()
        };

        // Act
        var result = await _recordBusiness.CreateRecord(uid, organizationId, pid, did, dto);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Tags);
        Assert.Empty(result.Tags);
    }

    [Fact]
    public async Task CreateRecord_WithDuplicateTags_DeduplicatesCorrectly()
    {
        // Arrange
        var dto = new CreateRecordRequestDto
        {
            Name = "Record With Duplicate Tags",
            Description = "Test Record with duplicate tag names",
            Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { TestProp = "TestValue" }))!,
            OriginalId = "original-duplicate-tags",
            Tags = new List<string> { "Duplicate Tag", "Duplicate Tag", "Unique Tag" }
        };

        // Act
        var result = await _recordBusiness.CreateRecord(uid, organizationId, pid, did, dto);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Tags);

        // Verify tags were created without duplicates
        var tagCount = await Context.Tags
            .Where(t => t.Name == "Duplicate Tag" && t.ProjectId == pid)
            .CountAsync();
        Assert.Equal(1, tagCount); // Should only create one tag with this name

        // Verify record associations
        var createdRecord = await Context.Records
            .Include(r => r.Tags)
            .FirstOrDefaultAsync(r => r.Id == result.Id);
        Assert.NotNull(createdRecord);

        var duplicateTagsOnRecord = createdRecord.Tags.Count(t => t.Name == "Duplicate Tag");
        Assert.Equal(1, duplicateTagsOnRecord); // Should only be attached once
    }

    #endregion

    #region BulkCreateRecords Tests

    [Fact]
    public async Task BulkCreateRecords_ValidData_CreatesMultipleRecords()
    {
        // Arrange
        var records = new List<CreateRecordRequestDto>
        {
            new()
            {
                Name = "Bulk Record 1",
                Description = "Bulk Record 1 Description",
                ObjectStorageId = osid,
                OriginalId = "br1",
                Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { TestProp = "Value1" }))!
            },
            new()
            {
                Name = "Bulk Record 2",
                Description = "Bulk Record 2 Description",
                ObjectStorageId = osid,
                OriginalId = "br2",
                Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { TestProp = "Value2" }))!
            }
        };

        // Act
        var result = await _recordBusiness.BulkCreateRecords(uid, organizationId, pid, did, records);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count());
        Assert.True(result.All(r =>
            r.LastUpdatedBy == uid && !r.IsArchived && r.DataSourceId == did && r.ProjectId == pid));
        Assert.Contains(result, r => r.Name == "Bulk Record 1");
        Assert.Contains(result, r => r.Name == "Bulk Record 2");

        // Verify records were actually created in database
        var recordCount = await Context.Records.CountAsync(r => r.ProjectId == pid);
        Assert.Equal(3, recordCount); // 1 from seed + 2 new

        // Ensure that a record create event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList); // One event is logged with the total bulk count in the properties
    }

    [Fact]
    public async Task BulkCreateRecords_EmptyList_ThrowsException()
    {
        // Arrange
        var records = new List<CreateRecordRequestDto>();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() =>
            _recordBusiness.BulkCreateRecords(uid, organizationId, pid, did, records));

        Assert.Contains("Unable to bulk create records: no records selected for creation", exception.Message);

        // Ensure that no record create event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task BulkCreateRecords_InvalidProjectId_ThrowsKeyNotFoundException()
    {
        // Arrange
        var records = new List<CreateRecordRequestDto>
        {
            new()
            {
                Name = "Test Record",
                Description = "Test Record Description",
                OriginalId = "test",
                Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { TestProp = "TestValue" }))!
            }
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordBusiness.BulkCreateRecords(uid, organizationId, 999L, 1L, records));

        Assert.Contains("Project with id 999 not found.", exception.Message);

        // Ensure that no record create event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    #endregion

    #region UpdateRecord Tests

    [Fact]
    public async Task UpdateRecord_ValidData_UpdatesRecord()
    {
        // Arrange
        var dto = new UpdateRecordRequestDto
        {
            Name = "Updated Test Record",
            Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { UpdatedProp = "UpdatedValue" }))!,
            Uri = "updated://uri",
            OriginalId = "updated-123",
            Description = "Updated Description",
            ClassId = cid,
            FileType = "png"
        };

        // Act
        var result = await _recordBusiness.UpdateRecord(uid, organizationId, pid, rid, dto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Updated Test Record", result.Name);
        Assert.Equal("updated://uri", result.Uri);
        Assert.Equal("updated-123", result.OriginalId);
        Assert.Equal("Updated Description", result.Description);
        Assert.Equal("png", result.FileType);

        // Verify record was actually updated in database
        var updatedRecord = await Context.Records.FindAsync(rid);
        Assert.NotNull(updatedRecord);
        Assert.Equal("Updated Test Record", updatedRecord.Name);

        // Verify that get function gets updated version
        var getResult = await _recordBusiness.GetRecord(organizationId, pid, rid, true);
        Assert.NotNull(getResult);
        Assert.Equal("Updated Test Record", getResult.Name);
        Assert.Equal("Updated Description", getResult.Description);
        Assert.Equal("png", getResult.FileType);
        Assert.NotNull(getResult.LastUpdatedAt);

        // Ensure that a record update event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal(pid, actualEvent.ProjectId);
        Assert.Equal(result.Id, actualEvent.EntityId);
        Assert.Equal("record", actualEvent.EntityType);
        Assert.Equal("update", actualEvent.Operation);
        Assert.Equal(result.DataSourceId, actualEvent.DataSourceId);
    }

    [Fact]
    public async Task UpdateRecord_PartialUpdate_UpdatesRecord()
    {
        // Arrange
        var dto = new UpdateRecordRequestDto
        {
            Name = "New-ish Test Record"
        };

        // Act
        var result = await _recordBusiness.UpdateRecord(uid, organizationId, pid, rid, dto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("New-ish Test Record", result.Name);
        Assert.Equal(ruri, result.Uri);
        Assert.Equal(rogid, result.OriginalId);
        Assert.Equal(rdesc, result.Description);
        Assert.Equal(rprop, result.Properties);
        Assert.Equal(rfiletype, result.FileType);

        // Verify record was actually updated in database
        var updatedRecord = await Context.Records.FindAsync(rid);
        Assert.NotNull(updatedRecord);
        Assert.Equal("New-ish Test Record", updatedRecord.Name);
        Assert.Equal(rdesc, updatedRecord.Description);

        // Verify that get function gets updated version
        var getResult = await _recordBusiness.GetRecord(organizationId, pid, rid, true);
        Assert.NotNull(getResult);
        Assert.Equal("New-ish Test Record", getResult.Name);
        Assert.Equal(rdesc, getResult.Description);
        Assert.NotNull(getResult.LastUpdatedAt);

        // Ensure that a record update event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];
        Assert.Equal(pid, actualEvent.ProjectId);
        Assert.Equal(result.Id, actualEvent.EntityId);
        Assert.Equal("record", actualEvent.EntityType);
        Assert.Equal("update", actualEvent.Operation);
        Assert.Equal(result.DataSourceId, actualEvent.DataSourceId);
    }

    [Fact]
    public async Task UpdateRecord_InvalidRecordId_ThrowsKeyNotFoundException()
    {
        // Arrange
        var dto = new UpdateRecordRequestDto
        {
            Name = "Updated Record",
            Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { TestProp = "TestValue" }))!
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordBusiness.UpdateRecord(uid, organizationId, pid, 999L, dto));

        Assert.Contains("Record with id 999 not found", exception.Message);

        // Ensure that no record create event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task UpdateRecord_RecordFromDifferentProject_ThrowsKeyNotFoundException()
    {
        // Arrange
        var dto = new UpdateRecordRequestDto
        {
            Name = "Updated Record",
            Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { TestProp = "TestValue" }))!
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordBusiness.UpdateRecord(uid, organizationId, 999L, rid, dto));

        Assert.Contains($"Record with id {rid} not found", exception.Message);

        // Ensure that no record create event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task UpdateRecord_TooDeepJson_ThrowsException()
    {
        // Arrange
        var deepJson = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new
        {
            Level1 = new
            {
                Level2 = new
                {
                    Level3 = new
                    {
                        Level4 = new
                        {
                            Value = "Too deep"
                        }
                    }
                }
            }
        }))!;

        var dto = new UpdateRecordRequestDto
        {
            Name = "Deep JSON Record",
            Properties = deepJson
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() =>
            _recordBusiness.UpdateRecord(uid, organizationId, pid, rid, dto));
        Assert.Contains("depth of the JSON structure exceeds", exception.Message);

        // Ensure that no record create event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    #endregion

    #region DeleteRecord Tests

    [Fact]
    public async Task DeleteRecord_ValidData_DeletesRecord()
    {
        // Arrange - Verify record exists before deletion
        var recordExists = await Context.Records.AnyAsync(r => r.Id == rid);
        Assert.True(recordExists);

        // Act
        var result = await _recordBusiness.DeleteRecord(uid, organizationId, pid, rid);

        // Assert
        Assert.True(result);

        // Verify record was actually deleted from database
        var deletedRecord = await Context.Records.FindAsync(rid);
        Assert.Null(deletedRecord);
    }

    [Fact]
    public async Task DeleteRecord_InvalidRecordId_ThrowsKeyNotFoundException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordBusiness.DeleteRecord(uid, organizationId, pid, 999L));

        Assert.Contains("Record with id 999 not found", exception.Message);
    }

    #endregion

    #region ArchiveRecord Tests

    [Fact]
    public async Task ArchiveRecord_Success_RecordIsArchived()
    {
        //Arrange
        var originalRecord = await Context.Records.FindAsync(rid);
        // Act
        var archived = await _recordBusiness.ArchiveRecord(uid, organizationId, pid, rid);
        Assert.True(archived);

        // force EF to update context with db
        Context.ChangeTracker.Clear();

        // Assert
        var archivedRecord = await Context.Records.FindAsync(rid);
        Assert.NotNull(archivedRecord);
        Assert.Equal(originalRecord?.Id, archivedRecord.Id);
        Assert.True(archivedRecord.IsArchived);
        Assert.Equal(originalRecord?.ProjectId, archivedRecord.ProjectId);
        Assert.True(originalRecord?.LastUpdatedAt < archivedRecord.LastUpdatedAt);
        Assert.Equal(originalRecord.Name, archivedRecord.Name);
        Assert.Equal(originalRecord.Description, archivedRecord.Description);
        Assert.Equal(originalRecord.DataSourceId, archivedRecord.DataSourceId);
        Assert.Equal(originalRecord.FileType, archivedRecord.FileType);
        Assert.Equal(originalRecord.Uri, archivedRecord.Uri);
        Assert.Equal(uid, archivedRecord.LastUpdatedBy);
    }

    [Fact]
    public async Task ArchiveRecord_InvalidRecordId_ThrowsKeyNotFoundException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordBusiness.ArchiveRecord(uid, organizationId, pid, 999L));

        Assert.Contains("Record with id 999 not found", exception.Message);

        // Ensure that no record soft delete event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }


    [Fact]
    public async Task ArchiveRecord_AlreadyArchivedRecord_ThrowsKeyNotFoundException()
    {
        // Arrange - First archive the record
        var record = await Context.Records.FindAsync(rid);
        record.IsArchived = true;
        await Context.SaveChangesAsync();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordBusiness.ArchiveRecord(uid, organizationId, pid, rid));

        Assert.Contains($"Record with id {rid} not found", exception.Message);

        // Ensure that no record soft delete event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    #endregion

    #region Edge Cases and Integration Tests

    [Fact]
    public async Task CreateRecord_ValidJsonDepthThree_Success()
    {
        // Arrange
        var validDepthJson = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new
        {
            Level1 = new
            {
                Level2 = new
                {
                    Level3 = "Valid depth"
                }
            }
        }))!;

        var dto = new CreateRecordRequestDto
        {
            Name = "Valid Depth Record",
            Description = "Valid Depth Description",
            ObjectStorageId = osid,
            OriginalId = "VDR1",
            Properties = validDepthJson
        };

        // Act
        var result = await _recordBusiness.CreateRecord(uid, organizationId, pid, did, dto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Valid Depth Record", result.Name);

        // Ensure that record create event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal(result.ProjectId, actualEvent.ProjectId);
        Assert.Equal("create", actualEvent.Operation);
        Assert.Equal("record", actualEvent.EntityType);
        Assert.Equal(result.Id, actualEvent.EntityId);
    }

    [Fact]
    public async Task CreateRecord_NullProperties_ThrowsException()
    {
        // Arrange
        var dto = new CreateRecordRequestDto
        {
            Name = "No Properties Record",
            Description = "No Properties Description",
            OriginalId = "NoProps",
            Properties = null
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() =>
            _recordBusiness.CreateRecord(uid, organizationId, pid, did, dto));

        // Ensure that no record create event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task CreateRecord_NoName_ThrowsException()
    {
        // Arrange
        var dto = new CreateRecordRequestDto
        {
            Name = null,
            Description = "No Name Description",
            OriginalId = "NoName",
            Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { TestProp = "TestValue" }))!
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() =>
            _recordBusiness.CreateRecord(uid, organizationId, pid, did, dto));

        // Ensure that no record create event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task CreateRecord_NoDescription_ThrowsException()
    {
        // Arrange
        var dto = new CreateRecordRequestDto
        {
            Name = "No Description Record",
            Description = null,
            OriginalId = "NoDesc",
            Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { TestProp = "TestValue" }))!
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() =>
            _recordBusiness.CreateRecord(uid, organizationId, pid, did, dto));

        // Ensure that no record create event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task CreateRecord_NoOriginalId_ThrowsException()
    {
        // Arrange
        var dto = new CreateRecordRequestDto
        {
            Name = "No Original ID Record",
            Description = "No Original ID Description",
            OriginalId = null,
            Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { TestProp = "TestValue" }))!
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() =>
            _recordBusiness.CreateRecord(uid, organizationId, pid, did, dto));

        // Ensure that no record create event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    #endregion

    #region UnarchiveRecord Tests

    [Fact]
    public async Task UnarchiveRecord_ValidArchivedRecord_UnarchivesSuccessfully()
    {
        // Arrange
        var record = await Context.Records.FindAsync(rid);
        record.IsArchived = true;
        await Context.SaveChangesAsync();

        // Act
        var result = await _recordBusiness.UnarchiveRecord(uid, organizationId, pid, rid);

        //this forces EF to sync to db on next query
        Context.ChangeTracker.Clear();

        // Assert
        var unarchivedRecord = await Context.Records.FindAsync(rid);
        Assert.NotNull(unarchivedRecord);
        Assert.Equal(record?.Id, unarchivedRecord.Id);
        Assert.False(unarchivedRecord.IsArchived);
        Assert.Equal(record?.ProjectId, unarchivedRecord.ProjectId);
        Assert.True(record?.LastUpdatedAt < unarchivedRecord.LastUpdatedAt);
        Assert.Equal(record.Name, unarchivedRecord.Name);
        Assert.Equal(record.Description, unarchivedRecord.Description);
        Assert.Equal(record.DataSourceId, unarchivedRecord.DataSourceId);
        Assert.Equal(record.FileType, unarchivedRecord.FileType);
        Assert.Equal(record.Uri, unarchivedRecord.Uri);
        Assert.Equal(uid, unarchivedRecord.LastUpdatedBy);

        // Ensure that the record unarchive event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal("unarchive", actualEvent.Operation);
        Assert.Equal("record", actualEvent.EntityType);
        Assert.Equal(rid, actualEvent.EntityId);
    }

    [Fact]
    public async Task UnarchiveRecord_InvalidRecordId_ThrowsKeyNotFoundException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordBusiness.UnarchiveRecord(uid, organizationId, pid, 999L));

        Assert.Contains("Record with id 999 not found", exception.Message);

        // Ensure that no event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task UnarchiveRecord_RecordFromDifferentProject_ThrowsKeyNotFoundException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordBusiness.UnarchiveRecord(uid, organizationId, 999L, rid));

        Assert.Contains($"Record with id {rid} not found or is not archived.", exception.Message);

        // Ensure that no event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task UnarchiveRecord_AlreadyUnarchived_ThrowsKeyNotFoundException()
    {
        // Arrange - Confirm record is not archived
        var existing = await Context.Records.FindAsync(rid);
        existing.IsArchived = false;
        await Context.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordBusiness.UnarchiveRecord(uid, organizationId, pid, rid));

        // Ensure that no event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    #endregion

    #region Attach/Unattach Tag Tests

    [Fact]
    public async Task AttachTag_SuccessfullyAttachesTagToRecord()
    {
        // Arrange
        var newTag = new Tag
        {
            Name = "Tag to Attach",
            ProjectId = pid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = organizationId
        };
        Context.Tags.Add(newTag);

        var record = await Context.Records.Include(r => r.Tags).FirstAsync(r => r.Id == rid);
        record.Tags.Clear(); // ensure tag not already attached
        await Context.SaveChangesAsync();

        // Act
        var result = await _recordBusiness.AttachTag(organizationId, pid, record.Id, newTag.Id);

        // Assert
        Assert.True(result);
        var updatedRecord = await Context.Records.Include(r => r.Tags).FirstAsync(r => r.Id == record.Id);
        Assert.Contains(updatedRecord.Tags, t => t.Id == newTag.Id);
    }

    [Fact]
    public async Task AttachTag_SuccessfullyAttachesOrgTagToRecord()
    {
        // Arrange
        var newTag = new Tag
        {
            Name = "Tag to Attach",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = organizationId
        };
        Context.Tags.Add(newTag);

        var record = await Context.Records.Include(r => r.Tags).FirstAsync(r => r.Id == rid);
        record.Tags.Clear(); // ensure tag not already attached
        await Context.SaveChangesAsync();

        // Act
        var result = await _recordBusiness.AttachTag(organizationId, pid, record.Id, newTag.Id);

        // Assert
        Assert.True(result);
        var updatedRecord = await Context.Records.Include(r => r.Tags).FirstAsync(r => r.Id == record.Id);
        Assert.Contains(updatedRecord.Tags, t => t.Id == newTag.Id);
    }

    [Fact]
    public async Task AttachTag_RecordNotFound_ThrowsKeyNotFound()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordBusiness.AttachTag(organizationId, pid, 9999L, tid));

        Assert.Contains("Record with id 9999 not found", exception.Message);
    }

    [Fact]
    public async Task AttachTag_TagNotFound_ThrowsKeyNotFound()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordBusiness.AttachTag(organizationId, pid, rid, 9999L));

        Assert.Contains("Tag with id 9999 not found or is archived.", exception.Message);
    }

    [Fact]
    public async Task AttachTag_AlreadyAttached_ThrowsException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _recordBusiness.AttachTag(organizationId, pid, rid, tid));

        Assert.Contains($"Tag with id {tid} is already attached to record {rid}", exception.Message);
    }

    [Fact]
    public async Task UnattachTag_SuccessfullyDetachesTagFromRecord()
    {
        // Arrange
        var record = await Context.Records.Include(r => r.Tags).FirstAsync(r => r.Id == rid);
        Assert.Contains(record.Tags, t => t.Id == tid);

        //ensures that the record tags are not in the record context
        Context.ChangeTracker.Clear();

        // Act
        var result = await _recordBusiness.UnattachTag(organizationId, pid, record.Id, tid);

        // Assert
        Assert.True(result);
        var refreshed = await Context.Records.Include(r => r.Tags).FirstAsync(r => r.Id == record.Id);
        Assert.DoesNotContain(refreshed.Tags, t => t.Id == tid);
    }

    [Fact]
    public async Task UnattachTag_RecordNotFound_ThrowsKeyNotFound()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordBusiness.UnattachTag(organizationId, pid, 9999L, tid));

        Assert.Contains("Record with id 9999 not found or is archived.", exception.Message);
    }

    [Fact]
    public async Task UnattachTag_TagNotFound_ThrowsKeyNotFound()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordBusiness.UnattachTag(organizationId, pid, rid, 9999L));

        Assert.Contains("Tag with id 9999 not found or is archived.", exception.Message);
    }

    #endregion

    #region GetRecordsByOriginalId Tests

    [Fact]
    public async Task GetRecordsByOriginalId_ValidOriginalIds_ReturnsMatchingRecords()
    {
        // Act
        var result = await _recordBusiness.GetRecordsByOriginalId(organizationId, pid, ["og_id"]);

        // Assert
        Assert.Equal(1, result.Count);
        Assert.Equal("og_id", result.First().OriginalId);
        Assert.Equal(pid, result.First().ProjectId);
    }

    [Fact]
    public async Task GetRecordsByOriginalId_MissingOriginalIds_ThrowsKeyNotFoundException()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _recordBusiness.GetRecordsByOriginalId(organizationId, pid, ["non-existent-id"]));

        Assert.Contains("Records not found with original IDs", exception.Message);
    }

    [Fact]
    public async Task GetRecordsByOriginalId_NullOriginalIds_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recordBusiness.GetRecordsByOriginalId(organizationId, pid, null));
    }

    [Fact]
    public async Task GetRecordsByOriginalId_ExcludesArchivedRecords()
    {
        // Arrange
        var record = await Context.Records.FindAsync(rid);
        record.IsArchived = true;
        await Context.SaveChangesAsync();

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _recordBusiness.GetRecordsByOriginalId(organizationId, pid, ["og_id"]));

        Assert.Contains("og_id", exception.Message);
    }

    [Fact]
    public async Task GetRecordsByOriginalId_InvalidProjectId_ThrowsKeyNotFoundException()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _recordBusiness.GetRecordsByOriginalId(organizationId, 999L, ["some-id"]));

        Assert.Contains("Records not found with original IDs", exception.Message);
    }

    #endregion

    #region LastUpdatedBy Tests

    [Fact]
    public async Task CreateRecord_Success_StoresLastUpdatedByUserId()
    {
        // Arrange
        var testRecord = new Record
        {
            Name = "Test Record LastUpdatedBy",
            Description = "Test description",
            OriginalId = "test-original-id",
            Properties = JsonSerializer.Serialize(new { TestProperty = "TestValue" }),
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            Uri = "test://uri",
            FileType = "txt", OrganizationId = organizationId
        };

        // Act
        Context.Records.Add(testRecord);
        await Context.SaveChangesAsync();

        // Assert
        var savedRecord = await Context.Records.FindAsync(testRecord.Id);
        Assert.NotNull(savedRecord);
        Assert.Equal(uid, savedRecord.LastUpdatedBy);
    }

    [Fact]
    public async Task CreateRecord_Success_NavigationPropertyLoadsUser()
    {
        // Arrange
        var testRecord = new Record
        {
            Name = "Test Record Navigation",
            Description = "Test description 2",
            OriginalId = "test-original-id-2",
            Properties = JsonSerializer.Serialize(new { TestProperty = "TestValue2" }),
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            Uri = "test://uri2",
            FileType = "txt", OrganizationId = organizationId
        };

        Context.Records.Add(testRecord);
        await Context.SaveChangesAsync();

        // Act
        var recordWithUser = await Context.Records
            .Include(r => r.LastUpdatedByUser)
            .FirstAsync(r => r.Id == testRecord.Id);

        // Assert
        Assert.NotNull(recordWithUser.LastUpdatedByUser);
        Assert.Equal("Test User", recordWithUser.LastUpdatedByUser.Name);
        Assert.Equal("test_record@example.com", recordWithUser.LastUpdatedByUser.Email);
        Assert.Equal(uid, recordWithUser.LastUpdatedBy);
    }

    [Fact]
    public async Task CreateRecord_Success_WithNullLastUpdatedBy()
    {
        // Arrange
        var testRecord = new Record
        {
            Name = "Test Record Null",
            Description = "Test description 3",
            OriginalId = "test-original-id-3",
            Properties = JsonSerializer.Serialize(new { TestProperty = "TestValue3" }),
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = null,
            Uri = "test://uri3",
            FileType = "txt", OrganizationId = organizationId
        };

        // Act
        Context.Records.Add(testRecord);
        await Context.SaveChangesAsync();

        // Assert
        var savedRecord = await Context.Records.FindAsync(testRecord.Id);
        Assert.NotNull(savedRecord);
        Assert.Null(savedRecord.LastUpdatedBy);

        var recordWithUser = await Context.Records
            .Include(r => r.LastUpdatedByUser)
            .FirstAsync(r => r.Id == testRecord.Id);

        Assert.Null(recordWithUser.LastUpdatedByUser);
    }

    [Fact]
    public async Task UpdateRecord_Success_UpdatesLastUpdatedByUserId()
    {
        // Arrange
        var testRecord = new Record
        {
            Name = "Test Record Update",
            Description = "Test description 4",
            OriginalId = "test-original-id-4",
            Properties = JsonSerializer.Serialize(new { TestProperty = "TestValue4" }),
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = null,
            Uri = "test://uri4",
            FileType = "txt",
            OrganizationId = organizationId
        };
        Context.Records.Add(testRecord);
        await Context.SaveChangesAsync();

        // Act
        testRecord.LastUpdatedBy = uid;
        testRecord.Name = "Updated Record Name";
        testRecord.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        Context.Records.Update(testRecord);
        await Context.SaveChangesAsync();

        // Assert
        var updatedRecord = await Context.Records
            .Include(r => r.LastUpdatedByUser)
            .FirstAsync(r => r.Id == testRecord.Id);

        Assert.Equal(uid, updatedRecord.LastUpdatedBy);
        Assert.NotNull(updatedRecord.LastUpdatedByUser);
        Assert.Equal("Test User", updatedRecord.LastUpdatedByUser.Name);
        Assert.Equal("Updated Record Name", updatedRecord.Name);
    }

    #endregion
}