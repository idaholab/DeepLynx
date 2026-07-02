using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using deeplynx.business;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.helpers.BigData;
using deeplynx.helpers.exceptions;
using deeplynx.helpers.Hubs;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Record = deeplynx.datalayer.Models.Record;

namespace deeplynx.tests;

[Collection("Test Suite Collection")]
public class RecordBusinessTests : IntegrationTestBase
{
    private EventBusiness _eventBusiness;
    private SensitivityLabelBusiness _sensitivityLabelBusiness;
    private Mock<IHubContext<EventNotificationHub>> _mockHubContext = null!;
    private Mock<ILogger<NotificationBusiness>> _mockNotificationLogger = null!;
    private INotificationBusiness _notificationBusiness = null!;
    private RecordBusiness _recordBusiness;
    private TagBusiness _tagBusiness = null!;
    private UserBusiness _userBusiness = null!;
    private BulkCopyUpsertExecutor _mockBulkCopyUpsertExecutor = null!;
    private SensitivityLabelService _sensitivityLabelService = null!;
    private EncryptionHelper _encryptionHelper = null!;
    private FileBusiness _fileBusiness = null!;
    private Mock<IFileBusinessFactory> _fileBusinessFactory = null!;
    private DataSourceBusiness _dataSourceBusiness = null!;
    private Mock<IEdgeBusiness> _edgeBusiness = null!;
    private ClassBusiness _classBusiness = null!;
    private Mock<IRelationshipBusiness> _relationshipBusiness = null!;
    private Mock<IInsightBusiness> _insightBusiness = null!;
    private OlapBusiness _olapBusiness = null!;
    private IObjectStorageBusiness _objectStorageBusiness = null!;
    private Mock<ILogger<OlapBusiness>> _mockTimeseriesLogger = null!;
    public long cid; // class ID
    public long did; // datasource ID
    public long did2;
    private long organizationId;
    public long osid; // object storage ID
    public long pid; // project ID
    public long pid2;
    public string rdesc;
    public string rfiletype;
    public long rid; // record ID
    public long rid2;
    public long rid3;
    public long rid4;
    public string rogid;
    public string rprop; // additional record props
    public string ruri;
    public long tid; // tag ID
    public long lid; // sensitivity label ID
    public long uid;
    public long roleId;

    public RecordBusinessTests(TestSuiteFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        _encryptionHelper = new EncryptionHelper();
        await base.InitializeAsync();
        _mockHubContext = new Mock<IHubContext<EventNotificationHub>>();
        _mockNotificationLogger = new Mock<ILogger<NotificationBusiness>>();
        _sensitivityLabelService = new SensitivityLabelService(Context);
        _notificationBusiness =
            new NotificationBusiness(Context, _mockNotificationLogger.Object, _mockHubContext.Object);
        _mockBulkCopyUpsertExecutor = new BulkCopyUpsertExecutor();
        _eventBusiness = new EventBusiness(Context, _notificationBusiness, _mockBulkCopyUpsertExecutor);
        _userBusiness = new UserBusiness(Context);
        _sensitivityLabelBusiness = new SensitivityLabelBusiness(Context, _eventBusiness, _userBusiness);
        _tagBusiness = new TagBusiness(Context, _eventBusiness);
        _fileBusinessFactory = new Mock<IFileBusinessFactory>();
        _edgeBusiness = new Mock<IEdgeBusiness>();
        _dataSourceBusiness =
            new DataSourceBusiness(Context, _edgeBusiness.Object, _recordBusiness, _eventBusiness);
        _relationshipBusiness = new Mock<IRelationshipBusiness>();
        _classBusiness = new ClassBusiness(Context, _recordBusiness, _relationshipBusiness.Object, _eventBusiness);
        _insightBusiness = new Mock<IInsightBusiness>();
        _objectStorageBusiness = new ObjectStorageBusiness(Context, _encryptionHelper);
        _mockTimeseriesLogger = new Mock<ILogger<OlapBusiness>>();
        _olapBusiness = new OlapBusiness(Context, _recordBusiness, _objectStorageBusiness, _mockTimeseriesLogger.Object);
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
        _recordBusiness = new RecordBusiness(Context, _eventBusiness, _mockBulkCopyUpsertExecutor, _tagBusiness,
            _sensitivityLabelBusiness, _sensitivityLabelService, _fileBusiness);
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
        var dataSources = new List<DataSource>
        {
            new DataSource{
                Name = "Test Data Source",
                Description = "Test data source for unit tests",
                ProjectId = pid,
                LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                LastUpdatedBy = uid,
                OrganizationId = organizationId
            },
            new DataSource
            {
                Name = "Test Data Source 2",
                Description = "Second data source for filtering tests",
                ProjectId = pid,
                LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                LastUpdatedBy = uid,
                OrganizationId = organizationId
            }
        };

        Context.DataSources.AddRange(dataSources);
        await Context.SaveChangesAsync();
        did = dataSources[0].Id;
        did2 = dataSources[1].Id;


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
            ConfigEncrypted = _encryptionHelper.SerializeAndEncrypt(config),
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

        var testLabel = new SensitivityLabel
        {
            Name = "Test Label",
            OrganizationId = organizationId,
            ProjectId = pid,
        };

        var testRecords = new List<Record>
        {
            new Record
            {
                Name = "Test Record",
                Description = "Test record for unit tests",
                OriginalId = "og_id",
                Properties = JsonSerializer.Serialize(new { TestProperty = "TestValue" }),
                ProjectId = pid,
                DataSourceId = did,
                ClassId = testClass.Id,
                LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                LastUpdatedBy = uid,
                Uri = "localhost:8090",
                FileType = "pdf",
                OrganizationId = organizationId
            },
            new Record
            {
                Name = "Test Record 2",
                Description = "Test record for unit tests",
                OriginalId = Guid.NewGuid().ToString(),
                Properties = JsonSerializer.Serialize(new { TestProperty = "TestValue" }),
                ProjectId = pid,
                DataSourceId = did2,
                ClassId = testClass.Id,
                LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                LastUpdatedBy = uid,
                Uri = "localhost:8090",
                FileType = "pdf",
                OrganizationId = organizationId
            },
            new Record
            {
                Name = "Test Record 3" + Guid.NewGuid(),
                Description = "Test record 3 for unit tests",
                OriginalId = Guid.NewGuid().ToString(),
                Properties = JsonSerializer.Serialize(new { TestProperty = "TestValue" }),
                ProjectId = pid,
                DataSourceId = did,
                ClassId = cid,
                LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                LastUpdatedBy = uid,
                Uri = "localhost:8090",
                FileType = "csv",
                OrganizationId = organizationId
            },
            new Record
            {
                Name = "Test Record 4",
                Description = "Test Record 4 for unit tests",
                OriginalId = Guid.NewGuid().ToString(),
                Properties = "{}",
                ProjectId = pid,
                DataSourceId = did,
                ClassId = cid,
                FileType = "pdf",
                LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                LastUpdatedBy = uid,
                OrganizationId = organizationId
            }
        };

        Context.Records.AddRange(testRecords);
        Context.Tags.Add(testTag);
        Context.SensitivityLabels.Add(testLabel);
        await Context.SaveChangesAsync();

        var testRole = new Role
        {
            Name = "Test Role",
            Description = "Test role for unit tests",
            ProjectId = pid,
            OrganizationId = organizationId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        Context.Roles.Add(testRole);
        await Context.SaveChangesAsync();

        var projectMember = new ProjectMember
        {
            ProjectId = pid,
            UserId = uid,
            RoleId = testRole.Id
        };
        Context.ProjectMembers.Add(projectMember);
        await Context.SaveChangesAsync();

        roleId = testRole.Id;
        rid = testRecords[0].Id;
        rid2 = testRecords[1].Id;
        rid3 = testRecords[2].Id;
        rid4 = testRecords[3].Id;
        tid = testTag.Id;
        lid = testLabel.Id;
        rprop = testRecords[0].Properties;
        rogid = testRecords[0].OriginalId;
        rdesc = testRecords[0].Description;
        ruri = testRecords[0].Uri;
        rfiletype = testRecords[0].FileType;
    }

    #region GetRecordsCountByDataSource Tests

    [Fact]
    public async Task GetRecordsCountByDataSource_ValidDataSource_ReturnsCount()
    {
        // Act
        var result = await _recordBusiness.GetRecordsCountByDataSource(
            organizationId, pid, did2, true);

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
        Assert.Equal(2, result);
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
        Assert.Equal(3, result);
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

        // Act - Get count for first data source (should only have 1 record)
        var result1 = await _recordBusiness.GetRecordsCountByDataSource(
            organizationId, pid, did, true);

        // Act - Get count for second data source (should have 2 records)
        var result2 = await _recordBusiness.GetRecordsCountByDataSource(
            organizationId, pid, did2, true);

        // Assert
        Assert.Equal(3, result1); // Original data source has 1 record
        Assert.Equal(1, result2); // New data source has 2 records
    }

    #endregion

    #region GetAllRecords Tests

    [Fact]
    public async Task GetAllRecords_ValidProjectId_ReturnsRecords()
    {
        // Act
        var result = await _recordBusiness.GetAllRecords(uid, organizationId, pid, null, true);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(4, result.Count);
        Assert.Equal("Test Record", result.First().Name);
    }

    [Fact]
    public async Task GetAllRecords_ReturnsTags()
    {
        await _recordBusiness.AttachTag(uid, organizationId, pid, rid, tid);
        // Act
        var result = await _recordBusiness.GetAllRecords(uid, organizationId, pid, null, true);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.First().Tags);
        Assert.Equal("Test Tag", result.First().Tags.First().Name);
        Assert.Equal("Test Record", result.First().Name);
    }

    [Fact]
    public async Task GetAllRecords_WithDataSourceId_ReturnsFilteredRecords()
    {
        // Act
        var result = await _recordBusiness.GetAllRecords(uid, organizationId, pid, did2, true);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(did2, result.First().DataSourceId);
    }

    [Fact]
    public async Task GetAllRecords_WithFileType_ReturnsFilteredRecords()
    {
        // Arrange - Make sure incorrect fileType filter results in no results (we only have 1 record seeded and its of pdf type)
        var incorrectFileTypeResponse = await _recordBusiness.GetAllRecords(uid, organizationId, pid, did, true, "png");
        Assert.Empty(incorrectFileTypeResponse);

        // Act
        var correctFileTypeResponse = await _recordBusiness.GetAllRecords(uid, organizationId, pid, did, true, "pdf");

        // Assert
        Assert.NotNull(correctFileTypeResponse);
        Assert.Equal("pdf", correctFileTypeResponse.First().FileType);
    }

    [Fact]
    public async Task GetAllRecordsPaginated_ReturnsRequestedPageAndTotalCount()
    {
        var page1 = await _recordBusiness.GetAllRecordsPaginated(
            uid,
            organizationId,
            pid,
            null,
            true,
            null,
            new PaginatedRequestDto { PageNumber = 1, PageSize = 2 });
        var page2 = await _recordBusiness.GetAllRecordsPaginated(
            uid,
            organizationId,
            pid,
            null,
            true,
            null,
            new PaginatedRequestDto { PageNumber = 2, PageSize = 2 });

        Assert.Equal(4, page1.TotalCount);
        Assert.Equal(1, page1.PageNumber);
        Assert.Equal(2, page1.PageSize);
        Assert.Equal(2, page1.Items.Count);
        Assert.Equal(2, page2.Items.Count);
        Assert.Empty(page1.Items.Select(r => r.Id).Intersect(page2.Items.Select(r => r.Id)));
    }

    #endregion

    #region GetRecordsByTags Tests

    [Fact]
    public async Task GetRecordsByTags_ValidProjectIdWithSingleTag_ReturnsMatchingRecords()
    {
        await _recordBusiness.AttachTag(uid, organizationId, pid, rid, tid);
        // Act
        var result = await _recordBusiness.GetRecordsByTags(uid, organizationId, pid, [tid], true);

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
        await _recordBusiness.AttachTag(uid, organizationId, pid, rid, tid);
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
            FileType = "pdf",
            OrganizationId = organizationId
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
            FileType = "pdf",
            OrganizationId = organizationId
        };

        Context.Records.AddRange(recordWithAllTags, recordWithSomeTags);
        await Context.SaveChangesAsync();

        // Act - Query for records with both testTag AND tag2
        var result = await _recordBusiness.GetRecordsByTags(uid, organizationId, pid, [tid, tag2.Id], true);

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
            FileType = "pdf",
            OrganizationId = organizationId
        };

        Context.Records.Add(recordWithAllTags);
        await Context.SaveChangesAsync();

        // Act - Query for records with both tags but in different valid project (pid2)
        var result = await _recordBusiness.GetRecordsByTags(uid, organizationId, pid2, [tid, tag2.Id], true);

        // Assert - Should return empty because records exist in pid, not pid2
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetRecordsByTags_EmptyTagArray_ReturnsAllNonArchivedRecords()
    {
        // Act
        var result = await _recordBusiness.GetRecordsByTags(uid, organizationId, pid, [], true);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(4, result.Count);
        Assert.Equal("Test Record", result.First().Name);
    }

    [Fact]
    public async Task GetRecordsByTags_HideArchivedTrue_ExcludesArchivedRecords()
    {
        await _recordBusiness.AttachTag(uid, organizationId, pid, rid, tid);
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
        var result = await _recordBusiness.GetRecordsByTags(uid, organizationId, pid, [tid], true);

        // Assert - Should only get the non-archived seeded record
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("Test Record", result.First().Name);
        Assert.False(result.First().IsArchived);
    }

    [Fact]
    public async Task GetRecordsByTags_HideArchivedFalse_IncludesArchivedRecords()
    {

        await _recordBusiness.AttachTag(uid, organizationId, pid, rid, tid);
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
        var result = await _recordBusiness.GetRecordsByTags(uid, organizationId, pid, [tid], false);

        // Assert - Should get both archived and non-archived records
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.Name == "Test Record" && !r.IsArchived);
        Assert.Contains(result, r => r.Name == "Archived Record" && r.IsArchived);
    }

    [Fact]
    public async Task GetRecordsByTags_NonExistentTag_ReturnsEmpty()
    {
        await _recordBusiness.AttachTag(uid, organizationId, pid, rid, tid);
        // Arrange - Make sure non-existent tag results in no results
        var nonExistentTagResult = await _recordBusiness.GetRecordsByTags(uid, organizationId, pid, [99999], true);
        Assert.Empty(nonExistentTagResult);

        // Act - Verify correct tag returns results
        var correctTagResult = await _recordBusiness.GetRecordsByTags(uid, organizationId, pid, [tid], true);

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
        var result = await _recordBusiness.GetRecord(uid, organizationId, pid, rid, true);

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
            _recordBusiness.GetRecord(uid, organizationId, 999L, rid, true));

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
        // Assert.Equal(3, recordCount);

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

        Assert.Contains($"DataSource with id 1 not found in project with id 999", exception.Message);

        // Ensure that no record create event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task BulkCreateRecords_ValidData_LongNames()
    {
        // Arrange
        var records = new List<CreateRecordRequestDto>
        {
            new()
            {
                Name = "A name that is five hundred characters long a name that is five hundred characters long a name that is five hundred characters long a name that is five hundred characters long a name that is five hundred characters long a name that is five hundred characters long a name that is five hundred characters long a name that is five hundred characters long a name that is five hundred characters long a name that is five hundred characters long a name that is five hundred characters long a name that is f",
                Description = "Long name 1 Description",
                ObjectStorageId = osid,
                OriginalId = "br1",
                Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { TestProp = "Value1" }))!
            },
            new()
            {
                Name = "A name that is just over one hundred characters long a name that is just over one hundred characters long",
                Description = "Long name 2 Description",
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
        Assert.Contains(result, r => r.Name == "A name that is five hundred characters long a name that is five hundred characters long a name that is five hundred characters long a name that is five hundred characters long a name that is five hundred characters long a name that is five hundred characters long a name that is five hundred characters long a name that is five hundred characters long a name that is five hundred characters long a name that is five hundred characters long a name that is five hundred characters long a name that is f");
        Assert.Contains(result, r => r.Name == "A name that is just over one hundred characters long a name that is just over one hundred characters long");

        // Ensure that a record create event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList); // One event is logged with the total bulk count in the properties
    }

    [Fact]
    public async Task BulkCreateRecords_WithLabelsAndTags_CreatesMultipleRecordsWithLabelsAndTags()
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
                Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { TestProp = "Value1" }))!,
                Tags = new List<string>{"UNIQUE TAG", "GREAT"}
            },
            new()
            {
                Name = "Bulk Record 2",
                Description = "Bulk Record 2 Description",
                ObjectStorageId = osid,
                OriginalId = "br2",
                Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { TestProp = "Value2" }))!,
                Tags = new List<string>{"AWESOME TAG", "NEW"}
            }
        };

        var label1 = new CreateSensitivityLabelRequestDto
        {
            Name = "secret sauce",
            Description = "secret sauce description",
        };
        var label2 = new CreateSensitivityLabelRequestDto
        {
            Name = "Very Sensitive Label",
            Description = "Very Sensitive Label Description",
        };

        var label1response = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, label1, pid, organizationId);
        var label2response = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, label2, pid, organizationId);

        var role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstAsync(r => r.Id == roleId);

        var label1WritePermission = Context.Permissions
            .FirstOrDefault(p => p.LabelId == label1response.Id && p.Action == "write record");

        var label2WritePermission = Context.Permissions
            .FirstOrDefault(p => p.LabelId == label2response.Id && p.Action == "write record");

        role.Permissions.Add(label1WritePermission);
        role.Permissions.Add(label2WritePermission);

        await Context.SaveChangesAsync();

        // Act
        var result = await _recordBusiness.BulkCreateRecords(
            uid, organizationId, pid, did, records, new List<long> { label1response.Id, label2response.Id });

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count());
        Assert.True(result.All(r =>
            r.LastUpdatedBy == uid && !r.IsArchived && r.DataSourceId == did && r.ProjectId == pid));
        Assert.Contains(result, r => r.Name == "Bulk Record 1");
        Assert.Contains(result, r => r.Name == "Bulk Record 2");

        // Assert tags are attached to records in response
        var record1 = result.First(r => r.Name == "Bulk Record 1");
        Assert.NotNull(record1.Tags);
        Assert.Equal(2, record1.Tags.Count);
        Assert.Contains(record1.Tags, t => t.Name == "UNIQUE TAG");
        Assert.Contains(record1.Tags, t => t.Name == "GREAT");

        var record2 = result.First(r => r.Name == "Bulk Record 2");
        Assert.NotNull(record2.Tags);
        Assert.Equal(2, record2.Tags.Count);
        Assert.Contains(record2.Tags, t => t.Name == "AWESOME TAG");
        Assert.Contains(record2.Tags, t => t.Name == "NEW");

        // Assert labels are attached to records in response
        Assert.NotNull(record1.Labels);
        Assert.Equal(2, record1.Labels.Count);
        Assert.Contains(record1.Labels, l => l.Name == "secret sauce");
        Assert.Contains(record1.Labels, l => l.Name == "Very Sensitive Label");

        Assert.NotNull(record2.Labels);
        Assert.Equal(2, record2.Labels.Count);
        Assert.Contains(record2.Labels, l => l.Name == "secret sauce");
        Assert.Contains(record2.Labels, l => l.Name == "Very Sensitive Label");

        // Verify records were actually created in database with tags and labels
        var dbRecord1 = await Context.Records
            .Include(r => r.Tags)
            .Include(r => r.Labels)
            .FirstAsync(r => r.Id == record1.Id);

        Assert.Equal(2, dbRecord1.Tags.Count);
        Assert.Contains(dbRecord1.Tags, t => t.Name == "UNIQUE TAG");
        Assert.Contains(dbRecord1.Tags, t => t.Name == "GREAT");
        Assert.Equal(2, dbRecord1.Labels.Count);
        Assert.Contains(dbRecord1.Labels, l => l.Name == "secret sauce");
        Assert.Contains(dbRecord1.Labels, l => l.Name == "Very Sensitive Label");

        var dbRecord2 = await Context.Records
            .Include(r => r.Tags)
            .Include(r => r.Labels)
            .FirstAsync(r => r.Id == record2.Id);

        Assert.Equal(2, dbRecord2.Tags.Count);
        Assert.Contains(dbRecord2.Tags, t => t.Name == "AWESOME TAG");
        Assert.Contains(dbRecord2.Tags, t => t.Name == "NEW");
        Assert.Equal(2, dbRecord2.Labels.Count);
        Assert.Contains(dbRecord2.Labels, l => l.Name == "secret sauce");
        Assert.Contains(dbRecord2.Labels, l => l.Name == "Very Sensitive Label");
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
        var getResult = await _recordBusiness.GetRecord(uid, organizationId, pid, rid, true);
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
        var getResult = await _recordBusiness.GetRecord(uid, organizationId, pid, rid, true);
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

        Assert.Contains("Record with id 999 is archived or not found", exception.Message);
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

    #region ArchiveRecord Collection Removal Tests

    [Fact]
    public async Task ArchiveRecord_RemovesRecordFromAllCollections()
    {
        // Arrange - create a collection containing the record
        var record = await Context.Records.FirstAsync(r => r.Id == rid);

        var collection = new RecordCollection
        {
            Name = "Collection To Remove From",
            Description = "Record should be removed on archive",
            Properties = "{}",
            ProjectId = pid,
            OrganizationId = organizationId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            IsArchived = false,
            Records = new List<Record> { record }
        };
        Context.RecordCollections.Add(collection);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        // Act
        await _recordBusiness.ArchiveRecord(uid, organizationId, pid, rid);
        Context.ChangeTracker.Clear();

        // Assert - record should be archived
        var archivedRecord = await Context.Records.FindAsync(rid);
        Assert.NotNull(archivedRecord);
        Assert.True(archivedRecord.IsArchived);

        // Assert - record should no longer be in the collection
        var updatedCollection = await Context.RecordCollections
            .Include(c => c.Records)
            .FirstAsync(c => c.Id == collection.Id);

        Assert.DoesNotContain(updatedCollection.Records, r => r.Id == rid);
        Assert.Equal(uid, updatedCollection.LastUpdatedBy);
        Assert.NotNull(updatedCollection.LastUpdatedAt);
    }

    [Fact]
    public async Task ArchiveRecord_RemovesRecordFromMultipleCollections()
    {
        // Arrange - create two collections both containing the record
        var record = await Context.Records.FirstAsync(r => r.Id == rid);

        var collection1 = new RecordCollection
        {
            Name = "First Collection",
            Description = "First collection containing the record",
            Properties = "{}",
            ProjectId = pid,
            OrganizationId = organizationId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            IsArchived = false,
            Records = new List<Record> { record }
        };
        var collection2 = new RecordCollection
        {
            Name = "Second Collection",
            Description = "Second collection containing the record",
            Properties = "{}",
            ProjectId = pid,
            OrganizationId = organizationId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            IsArchived = false,
            Records = new List<Record> { record }
        };
        Context.RecordCollections.AddRange(collection1, collection2);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        // Act
        await _recordBusiness.ArchiveRecord(uid, organizationId, pid, rid);
        Context.ChangeTracker.Clear();

        // Assert - record removed from both collections
        var updatedCollection1 = await Context.RecordCollections
            .Include(c => c.Records)
            .FirstAsync(c => c.Id == collection1.Id);

        var updatedCollection2 = await Context.RecordCollections
            .Include(c => c.Records)
            .FirstAsync(c => c.Id == collection2.Id);

        Assert.DoesNotContain(updatedCollection1.Records, r => r.Id == rid);
        Assert.Equal(uid, updatedCollection1.LastUpdatedBy);

        Assert.DoesNotContain(updatedCollection2.Records, r => r.Id == rid);
        Assert.Equal(uid, updatedCollection2.LastUpdatedBy);
    }

    [Fact]
    public async Task ArchiveRecord_DoesNotAffectOtherRecordsInCollection()
    {
        // Arrange - create a collection with two records, only archive one
        var record1 = await Context.Records.FirstAsync(r => r.Id == rid);
        var record2 = await Context.Records.FirstAsync(r => r.Id == rid2);

        var collection = new RecordCollection
        {
            Name = "Collection With Multiple Records",
            Description = "Only one record will be archived",
            Properties = "{}",
            ProjectId = pid,
            OrganizationId = organizationId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            IsArchived = false,
            Records = new List<Record> { record1, record2 }
        };
        Context.RecordCollections.Add(collection);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        // Act - only archive rid
        await _recordBusiness.ArchiveRecord(uid, organizationId, pid, rid);
        Context.ChangeTracker.Clear();

        // Assert - rid is removed but rid2 remains
        var updatedCollection = await Context.RecordCollections
            .Include(c => c.Records)
            .FirstAsync(c => c.Id == collection.Id);

        Assert.DoesNotContain(updatedCollection.Records, r => r.Id == rid);
        Assert.Contains(updatedCollection.Records, r => r.Id == rid2);
        Assert.Equal(1, updatedCollection.Records.Count);
    }

    [Fact]
    public async Task ArchiveRecord_UpdatesCollectionLastUpdatedFields()
    {
        // Arrange
        var record = await Context.Records.FirstAsync(r => r.Id == rid);
        var beforeArchive = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        var collection = new RecordCollection
        {
            Name = "Timestamp Check Collection",
            Description = "Check that timestamps are updated",
            Properties = "{}",
            ProjectId = pid,
            OrganizationId = organizationId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow.AddMinutes(-5), DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            IsArchived = false,
            Records = new List<Record> { record }
        };
        Context.RecordCollections.Add(collection);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        // Act
        await _recordBusiness.ArchiveRecord(uid, organizationId, pid, rid);
        Context.ChangeTracker.Clear();

        // Assert
        var updatedCollection = await Context.RecordCollections
            .FirstAsync(c => c.Id == collection.Id);

        Assert.True(updatedCollection.LastUpdatedAt >= beforeArchive);
        Assert.Equal(uid, updatedCollection.LastUpdatedBy);
    }

    [Fact]
    public async Task ArchiveRecord_NoCollections_ArchivesSuccessfully()
    {
        // Arrange - rid2 is not in any collection in seed data
        Context.ChangeTracker.Clear();

        // Act & Assert - should not throw even when record is in no collections
        var result = await _recordBusiness.ArchiveRecord(uid, organizationId, pid, rid2);
        Assert.True(result);

        Context.ChangeTracker.Clear();

        var archivedRecord = await Context.Records.FindAsync(rid2);
        Assert.NotNull(archivedRecord);
        Assert.True(archivedRecord.IsArchived);
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
        var result = await _recordBusiness.AttachTag(uid, organizationId, pid, record.Id, newTag.Id);

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
        var result = await _recordBusiness.AttachTag(uid, organizationId, pid, record.Id, newTag.Id);

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
            _recordBusiness.AttachTag(uid, organizationId, pid, 9999L, tid));

        Assert.Contains("Record with id 9999 not found", exception.Message);
    }

    [Fact]
    public async Task AttachTag_TagNotFound_ThrowsKeyNotFound()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordBusiness.AttachTag(uid, organizationId, pid, rid, 9999L));

        Assert.Contains("Tag with id 9999 not found, is archived, or does not belong to this organization/project.",
            exception.Message);
    }

    [Fact]
    public async Task AttachTag_AlreadyAttached_ThrowsException()
    {
        await _recordBusiness.AttachTag(uid, organizationId, pid, rid, tid);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _recordBusiness.AttachTag(uid, organizationId, pid, rid, tid));

        Assert.Contains($"Tag with id {tid} is already attached to record {rid}", exception.Message);
    }

    [Fact]
    public async Task UnattachTag_SuccessfullyDetachesTagFromRecord()
    {
        await _recordBusiness.AttachTag(uid, organizationId, pid, rid, tid);
        // Arrange
        var record = await Context.Records.Include(r => r.Tags).FirstAsync(r => r.Id == rid);
        Assert.Contains(record.Tags, t => t.Id == tid);

        //ensures that the record tags are not in the record context
        Context.ChangeTracker.Clear();

        // Act
        var result = await _recordBusiness.UnattachTag(uid, organizationId, pid, record.Id, tid);

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
            _recordBusiness.UnattachTag(uid, organizationId, pid, 9999L, tid));

        Assert.Contains("Record with id 9999 not found or is archived.", exception.Message);
    }

    [Fact]
    public async Task UnattachTag_TagNotFound_ThrowsKeyNotFound()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordBusiness.UnattachTag(uid, organizationId, pid, rid, 9999L));

        Assert.Contains("Tag with id 9999 is not attached to record", exception.Message);
    }

    #endregion

    #region Attach/Unattach Labels Tests

    [Fact]
    public async Task AttachLabel_SuccessfullyAttachesLabelToRecord()
    {
        // Arrange
        // Create label using business method - this automatically creates permissions
        var newLabel = new CreateSensitivityLabelRequestDto
        {
            Name = "New Label",
            Description = "New Label"
        };

        var newLabelResponse = await _sensitivityLabelBusiness.CreateSensitivityLabel(
            uid, newLabel, pid, organizationId);

        var record = await Context.Records.Include(r => r.Labels).FirstAsync(r => r.Id == rid);
        record.Labels.Clear(); // ensure label not already attached
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        // Act
        var result = await _recordBusiness.AttachLabel(uid, organizationId, pid, record.Id, newLabelResponse.Id);

        // Assert
        Assert.True(result);
        var updatedRecord = await Context.Records.Include(r => r.Labels).FirstAsync(r => r.Id == record.Id);
        Assert.Contains(updatedRecord.Labels, l => l.Id == newLabelResponse.Id);
    }

    [Fact]
    public async Task AttachLabel_SuccessfullyAttachesOrgLabelToRecord()
    {
        // Arrange
        // Create org-level label using business method - this automatically creates permissions
        var newLabel = new CreateSensitivityLabelRequestDto
        {
            Name = "New Label",
            Description = "New Label"
        };

        var newLabelResponse = await _sensitivityLabelBusiness.CreateSensitivityLabel(
            uid, newLabel, pid, organizationId);

        var record = await Context.Records.Include(r => r.Labels).FirstAsync(r => r.Id == rid);
        record.Labels.Clear(); // ensure label not already attached
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        await Context.SaveChangesAsync();

        // Act
        var result = await _recordBusiness.AttachLabel(uid, organizationId, pid, record.Id, newLabelResponse.Id);

        // Assert
        Assert.True(result);
        var updatedRecord = await Context.Records.Include(r => r.Labels).FirstAsync(r => r.Id == record.Id);
        Assert.Contains(updatedRecord.Labels, l => l.Id == newLabelResponse.Id);
    }

    [Fact]
    public async Task BulkAttachLabelsToRecords_SuccessfullyAttachesLabelsToRecord()
    {
        // create two new labels that will be attached to record 1 and record 2
        var newLabel1 = new CreateSensitivityLabelRequestDto
        {
            Name = "Very Top Secret",
            Description = "Very Top Secret Description"
        };

        var newLabel2 = new CreateSensitivityLabelRequestDto
        {
            Name = "Confidential",
            Description = "Very Confidential Description"
        };

        var newLabel1Response = await _sensitivityLabelBusiness.CreateSensitivityLabel(
            uid, newLabel1, null, organizationId);

        var newLabel2Response = await _sensitivityLabelBusiness.CreateSensitivityLabel(
            uid, newLabel2, null, organizationId);

        Context.ChangeTracker.Clear();

        var record1 = await Context.Records.Include(r => r.Labels).FirstAsync(r => r.Id == rid);
        record1.Labels.Clear();

        var record2 = await Context.Records.Include(r => r.Labels).FirstAsync(r => r.Id == rid2);
        record2.Labels.Clear();

        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        // perform the action
        await _recordBusiness.BulkAttachLabels(uid, organizationId, pid,
            new List<long> { rid, rid2 },
            new List<long> { newLabel1Response.Id, newLabel2Response.Id });

        // ensure that the records have those labels
        var record1Updated = await Context.Records
            .Include(r => r.Labels)
            .FirstAsync(r => r.Id == rid);

        Assert.Equal(2, record1Updated.Labels.Count);
        Assert.Contains(record1Updated.Labels, l => l.Name == newLabel1.Name);
        Assert.Contains(record1Updated.Labels, l => l.Name == newLabel2.Name);

        var record2Updated = await Context.Records
            .Include(r => r.Labels)
            .FirstAsync(r => r.Id == rid2);

        Assert.Equal(2, record2Updated.Labels.Count);
        Assert.Contains(record2Updated.Labels, l => l.Name == newLabel1.Name);
        Assert.Contains(record2Updated.Labels, l => l.Name == newLabel2.Name);
    }

    #endregion

    #region AttachLabel Collection Propagation Tests

    [Fact]
    public async Task AttachLabel_PropagatesLabelToCollectionsContainingRecord()
    {
        // Arrange - create a collection containing rid
        var newLabel = new CreateSensitivityLabelRequestDto
        {
            Name = "Propagation Label",
            Description = "Label that should propagate to collections"
        };
        var newLabelResponse = await _sensitivityLabelBusiness.CreateSensitivityLabel(
            uid, newLabel, pid, organizationId);

        var collection = new RecordCollection
        {
            Name = "Collection With Record",
            Description = "Contains the record we will attach a label to",
            Properties = "{}",
            ProjectId = pid,
            OrganizationId = organizationId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            IsArchived = false,
            Records = new List<Record> { await Context.Records.FirstAsync(r => r.Id == rid) },
            Labels = new List<SensitivityLabel>()
        };
        Context.RecordCollections.Add(collection);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        // Act
        var result = await _recordBusiness.AttachLabel(uid, organizationId, pid, rid, newLabelResponse.Id);

        // Assert
        Assert.True(result);

        var updatedCollection = await Context.RecordCollections
            .Include(c => c.Labels)
            .FirstAsync(c => c.Id == collection.Id);

        Assert.NotEmpty(updatedCollection.Labels);
        Assert.Contains(updatedCollection.Labels, l => l.Id == newLabelResponse.Id);
        Assert.Equal(1, updatedCollection.Labels.Count);
    }

    [Fact]
    public async Task AttachLabel_DoesNotDuplicateLabelOnCollection_IfAlreadyPresent()
    {
        // Arrange - create a label and a collection that already has it
        var newLabel = new CreateSensitivityLabelRequestDto
        {
            Name = "Existing Collection Label",
            Description = "Already on the collection"
        };
        var newLabelResponse = await _sensitivityLabelBusiness.CreateSensitivityLabel(
            uid, newLabel, pid, organizationId);

        var label = await Context.SensitivityLabels.FirstAsync(l => l.Id == newLabelResponse.Id);
        var record = await Context.Records.FirstAsync(r => r.Id == rid);

        var collection = new RecordCollection
        {
            Name = "Collection With Existing Label",
            Description = "Already has the label",
            Properties = "{}",
            ProjectId = pid,
            OrganizationId = organizationId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            IsArchived = false,
            Records = new List<Record> { record },
            Labels = new List<SensitivityLabel> { label }
        };
        Context.RecordCollections.Add(collection);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        // Act
        var result = await _recordBusiness.AttachLabel(uid, organizationId, pid, rid, newLabelResponse.Id);

        // Assert
        Assert.True(result);

        var updatedCollection = await Context.RecordCollections
            .Include(c => c.Labels)
            .FirstAsync(c => c.Id == collection.Id);

        Assert.Equal(1, updatedCollection.Labels.Count(l => l.Id == newLabelResponse.Id));
    }

    [Fact]
    public async Task AttachLabel_OnlyPropagatesToCollectionsContainingRecord()
    {
        // Arrange - create two collections, only one contains the record
        var newLabel = new CreateSensitivityLabelRequestDto
        {
            Name = "Selective Propagation Label",
            Description = "Should only go to collections with the record"
        };
        var newLabelResponse = await _sensitivityLabelBusiness.CreateSensitivityLabel(
            uid, newLabel, pid, organizationId);

        var record = await Context.Records.FirstAsync(r => r.Id == rid);

        var collectionWithRecord = new RecordCollection
        {
            Name = "Collection With Record",
            Description = "Has the record",
            Properties = "{}",
            ProjectId = pid,
            OrganizationId = organizationId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            IsArchived = false,
            Records = new List<Record> { record },
            Labels = new List<SensitivityLabel>()
        };
        var collectionWithoutRecord = new RecordCollection
        {
            Name = "Collection Without Record",
            Description = "Does not have the record",
            Properties = "{}",
            ProjectId = pid,
            OrganizationId = organizationId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            IsArchived = false,
            Records = new List<Record>(),
            Labels = new List<SensitivityLabel>()
        };
        Context.RecordCollections.AddRange(collectionWithRecord, collectionWithoutRecord);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        // Act
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid, newLabelResponse.Id);

        // Assert
        var updatedWithRecord = await Context.RecordCollections
            .Include(c => c.Labels)
            .FirstAsync(c => c.Id == collectionWithRecord.Id);

        var updatedWithoutRecord = await Context.RecordCollections
            .Include(c => c.Labels)
            .FirstAsync(c => c.Id == collectionWithoutRecord.Id);

        Assert.Contains(updatedWithRecord.Labels, l => l.Id == newLabelResponse.Id);
        Assert.DoesNotContain(updatedWithoutRecord.Labels, l => l.Id == newLabelResponse.Id);
    }

    [Fact]
    public async Task AttachLabel_PropagatesLabelToMultipleCollectionsContainingRecord()
    {
        // Arrange - create two collections that both contain the record
        var newLabel = new CreateSensitivityLabelRequestDto
        {
            Name = "Multi Collection Label",
            Description = "Should propagate to all collections with the record"
        };
        var newLabelResponse = await _sensitivityLabelBusiness.CreateSensitivityLabel(
            uid, newLabel, pid, organizationId);

        var record = await Context.Records.FirstAsync(r => r.Id == rid);

        var collection1 = new RecordCollection
        {
            Name = "First Collection",
            Description = "First collection with the record",
            Properties = "{}",
            ProjectId = pid,
            OrganizationId = organizationId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            IsArchived = false,
            Records = new List<Record> { record },
            Labels = new List<SensitivityLabel>()
        };
        var collection2 = new RecordCollection
        {
            Name = "Second Collection",
            Description = "Second collection with the record",
            Properties = "{}",
            ProjectId = pid,
            OrganizationId = organizationId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            IsArchived = false,
            Records = new List<Record> { record },
            Labels = new List<SensitivityLabel>()
        };
        Context.RecordCollections.AddRange(collection1, collection2);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        // Act
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid, newLabelResponse.Id);

        // Assert
        var updatedCollection1 = await Context.RecordCollections
            .Include(c => c.Labels)
            .FirstAsync(c => c.Id == collection1.Id);

        var updatedCollection2 = await Context.RecordCollections
            .Include(c => c.Labels)
            .FirstAsync(c => c.Id == collection2.Id);

        Assert.Contains(updatedCollection1.Labels, l => l.Id == newLabelResponse.Id);
        Assert.Equal(1, updatedCollection1.Labels.Count);

        Assert.Contains(updatedCollection2.Labels, l => l.Id == newLabelResponse.Id);
        Assert.Equal(1, updatedCollection2.Labels.Count);
    }

    #endregion

    #region BulkAttachLabels Collection Propagation Tests

    [Fact]
    public async Task BulkAttachLabels_PropagatesLabelsToCollectionsContainingRecords()
    {
        // Arrange - create two labels and a collection containing rid and rid2
        var newLabel1 = new CreateSensitivityLabelRequestDto
        {
            Name = "Bulk Label 1",
            Description = "First bulk label"
        };
        var newLabel2 = new CreateSensitivityLabelRequestDto
        {
            Name = "Bulk Label 2",
            Description = "Second bulk label"
        };

        var label1Response = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, newLabel1, pid, organizationId);
        var label2Response = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, newLabel2, pid, organizationId);

        var record1 = await Context.Records.FirstAsync(r => r.Id == rid);
        var record2 = await Context.Records.FirstAsync(r => r.Id == rid2);

        var collection = new RecordCollection
        {
            Name = "Bulk Label Collection",
            Description = "Collection containing both records",
            Properties = "{}",
            ProjectId = pid,
            OrganizationId = organizationId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            IsArchived = false,
            Records = new List<Record> { record1, record2 },
            Labels = new List<SensitivityLabel>()
        };
        Context.RecordCollections.Add(collection);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        // Act
        var result = await _recordBusiness.BulkAttachLabels(
            uid, organizationId, pid,
            new List<long> { rid, rid2 },
            new List<long> { label1Response.Id, label2Response.Id });

        // Assert
        Assert.True(result);

        var updatedCollection = await Context.RecordCollections
            .Include(c => c.Labels)
            .FirstAsync(c => c.Id == collection.Id);

        Assert.Equal(2, updatedCollection.Labels.Count);
        Assert.Contains(updatedCollection.Labels, l => l.Id == label1Response.Id);
        Assert.Contains(updatedCollection.Labels, l => l.Id == label2Response.Id);
        Assert.Equal(uid, updatedCollection.LastUpdatedBy);
    }

    [Fact]
    public async Task BulkAttachLabels_DoesNotPropagateLabelToCollectionNotContainingRecord()
    {
        // Arrange - create a collection that does NOT contain any of the records
        var newLabel = new CreateSensitivityLabelRequestDto
        {
            Name = "Non Propagation Label",
            Description = "Should not appear on unrelated collection"
        };
        var labelResponse = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, newLabel, pid, organizationId);

        var unrelatedCollection = new RecordCollection
        {
            Name = "Unrelated Collection",
            Description = "Contains none of the records being labeled",
            Properties = "{}",
            ProjectId = pid,
            OrganizationId = organizationId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            IsArchived = false,
            Records = new List<Record>(),
            Labels = new List<SensitivityLabel>()
        };
        Context.RecordCollections.Add(unrelatedCollection);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        // Act
        await _recordBusiness.BulkAttachLabels(
            uid, organizationId, pid,
            new List<long> { rid },
            new List<long> { labelResponse.Id });

        // Assert
        var updatedCollection = await Context.RecordCollections
            .Include(c => c.Labels)
            .FirstAsync(c => c.Id == unrelatedCollection.Id);

        Assert.Empty(updatedCollection.Labels);
        Assert.DoesNotContain(updatedCollection.Labels, l => l.Id == labelResponse.Id);
    }

    [Fact]
    public async Task BulkAttachLabels_DoesNotDuplicateLabelsAlreadyOnCollection()
    {
        // Arrange - create a collection that already has the label
        var newLabel = new CreateSensitivityLabelRequestDto
        {
            Name = "Pre-existing Label",
            Description = "Already on the collection"
        };
        var labelResponse = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, newLabel, pid, organizationId);
        var label = await Context.SensitivityLabels.FirstAsync(l => l.Id == labelResponse.Id);
        var record = await Context.Records.FirstAsync(r => r.Id == rid);

        var collection = new RecordCollection
        {
            Name = "Collection With Existing Label",
            Description = "Already has the label before bulk attach",
            Properties = "{}",
            ProjectId = pid,
            OrganizationId = organizationId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            IsArchived = false,
            Records = new List<Record> { record },
            Labels = new List<SensitivityLabel> { label }
        };
        Context.RecordCollections.Add(collection);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        // Act
        await _recordBusiness.BulkAttachLabels(
            uid, organizationId, pid,
            new List<long> { rid },
            new List<long> { labelResponse.Id });

        // Assert
        var updatedCollection = await Context.RecordCollections
            .Include(c => c.Labels)
            .FirstAsync(c => c.Id == collection.Id);

        Assert.Equal(1, updatedCollection.Labels.Count(l => l.Id == labelResponse.Id));
    }

    [Fact]
    public async Task BulkAttachLabels_PropagatesLabelsAcrossMultipleCollections()
    {
        // Arrange - two collections both containing rid
        var newLabel = new CreateSensitivityLabelRequestDto
        {
            Name = "Multi Collection Bulk Label",
            Description = "Should appear on all collections with the record"
        };
        var labelResponse = await _sensitivityLabelBusiness.CreateSensitivityLabel(uid, newLabel, pid, organizationId);
        var record = await Context.Records.FirstAsync(r => r.Id == rid);

        var collection1 = new RecordCollection
        {
            Name = "First Bulk Collection",
            Description = "First collection with the record",
            Properties = "{}",
            ProjectId = pid,
            OrganizationId = organizationId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            IsArchived = false,
            Records = new List<Record> { record },
            Labels = new List<SensitivityLabel>()
        };
        var collection2 = new RecordCollection
        {
            Name = "Second Bulk Collection",
            Description = "Second collection with the record",
            Properties = "{}",
            ProjectId = pid,
            OrganizationId = organizationId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            IsArchived = false,
            Records = new List<Record> { record },
            Labels = new List<SensitivityLabel>()
        };
        Context.RecordCollections.AddRange(collection1, collection2);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        // Act
        await _recordBusiness.BulkAttachLabels(
            uid, organizationId, pid,
            new List<long> { rid },
            new List<long> { labelResponse.Id });

        // Assert
        var updatedCollection1 = await Context.RecordCollections
            .Include(c => c.Labels)
            .FirstAsync(c => c.Id == collection1.Id);

        var updatedCollection2 = await Context.RecordCollections
            .Include(c => c.Labels)
            .FirstAsync(c => c.Id == collection2.Id);

        Assert.Contains(updatedCollection1.Labels, l => l.Id == labelResponse.Id);
        Assert.Equal(1, updatedCollection1.Labels.Count);
        Assert.Equal(uid, updatedCollection1.LastUpdatedBy);

        Assert.Contains(updatedCollection2.Labels, l => l.Id == labelResponse.Id);
        Assert.Equal(1, updatedCollection2.Labels.Count);
        Assert.Equal(uid, updatedCollection2.LastUpdatedBy);
    }

    #endregion

    #region GetRecordsByOriginalId Tests

    [Fact]
    public async Task GetRecordsByOriginalId_ValidOriginalIds_ReturnsMatchingRecords()
    {
        // Act
        var result = await _recordBusiness.GetRecordsByOriginalId(uid, organizationId, pid, did, ["og_id"], true);

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
                _recordBusiness.GetRecordsByOriginalId(uid, organizationId, pid, did, ["non-existent-id"], true));

        Assert.Contains("Records not found or access is unauthorized with original IDs", exception.Message);
    }

    [Fact]
    public async Task GetRecordsByOriginalId_NullOriginalIds_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recordBusiness.GetRecordsByOriginalId(uid, organizationId, pid, did, null, true));
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
                _recordBusiness.GetRecordsByOriginalId(uid, organizationId, pid, did, ["og_id"], true));

        Assert.Contains("og_id", exception.Message);
    }

    [Fact]
    public async Task GetRecordsByOriginalId_IncludesArchivedRecords_WhenHideArchivedFalse()
    {
        // Arrange
        var record = await Context.Records.FindAsync(rid);
        record.IsArchived = true;
        await Context.SaveChangesAsync();

        // Act
        var result = await _recordBusiness.GetRecordsByOriginalId(uid, organizationId, pid, did, ["og_id"], false);

        // Assert
        Assert.Single(result);
        Assert.Equal("og_id", result.First().OriginalId);
        Assert.True(result.First().IsArchived);
    }

    [Fact]
    public async Task GetRecordsByOriginalId_InvalidProjectId_ThrowsKeyNotFoundException()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _recordBusiness.GetRecordsByOriginalId(uid, organizationId, 999L, did, ["og-id"], true));

        Assert.Contains("No data source with Id", exception.Message);
    }

    [Fact]
    public async Task GetRecordsByOriginalId_InvalidDataSourceId_ThrowsKeyNotFoundException()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _recordBusiness.GetRecordsByOriginalId(uid, organizationId, pid, 999999L, ["og-id"], true));

        Assert.Contains("No data source with Id", exception.Message);
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
            FileType = "txt",
            OrganizationId = organizationId
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
            FileType = "txt",
            OrganizationId = organizationId
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
            FileType = "txt",
            OrganizationId = organizationId
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

    #region FileSize Tests

    [Fact]
    public async Task CreateRecord_WithFileSize_StoresFileSize()
    {
        // Arrange
        var fileSize = 1024000L; // 1MB
        var dto = new CreateRecordRequestDto
        {
            Name = "Record with File Size",
            Description = "Test record with file size",
            Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { TestProp = "TestValue" }))!,
            OriginalId = "filesize-record-1",
            ClassId = cid,
            FileType = "pdf",
            FileSize = fileSize
        };

        // Act
        var result = await _recordBusiness.CreateRecord(uid, organizationId, pid, did, dto);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.FileSize);
        Assert.Equal(fileSize, result.FileSize);

        // Verify in database
        var dbRecord = await Context.Records.FindAsync(result.Id);
        Assert.NotNull(dbRecord);
        Assert.Equal(fileSize, dbRecord.FileSize);
    }

    [Fact]
    public async Task CreateRecord_WithoutFileSize_AllowsNull()
    {
        // Arrange
        var dto = new CreateRecordRequestDto
        {
            Name = "Record without File Size",
            Description = "Test record without file size",
            Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { TestProp = "TestValue" }))!,
            OriginalId = "no-filesize-record",
            ClassId = cid,
            FileSize = null // Explicitly null
        };

        // Act
        var result = await _recordBusiness.CreateRecord(uid, organizationId, pid, did, dto);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.FileSize);

        // Verify in database
        var dbRecord = await Context.Records.FindAsync(result.Id);
        Assert.Null(dbRecord.FileSize);
    }

    [Fact]
    public async Task UpdateRecord_UpdatesFileSize()
    {
        // Arrange - Create initial record with file size
        var initialSize = 500000L;
        var createDto = new CreateRecordRequestDto
        {
            Name = "Initial Record",
            Description = "Initial description",
            Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { TestProp = "TestValue" }))!,
            OriginalId = "update-filesize-test",
            ClassId = cid,
            FileSize = initialSize
        };
        var createdRecord = await _recordBusiness.CreateRecord(uid, organizationId, pid, did, createDto);

        // Act - Update with new file size
        var newSize = 750000L;
        var updateDto = new UpdateRecordRequestDto
        {
            FileSize = newSize
        };
        var result = await _recordBusiness.UpdateRecord(uid, organizationId, pid, createdRecord.Id, updateDto);

        // Assert
        Assert.NotNull(result.FileSize);
        Assert.Equal(newSize, result.FileSize);
        Assert.NotEqual(initialSize, result.FileSize);

        // Verify in database
        var dbRecord = await Context.Records.FindAsync(result.Id);
        Assert.Equal(newSize, dbRecord.FileSize);
    }

    [Fact]
    public async Task UpdateRecord_PartialUpdate_PreservesFileSize()
    {
        // Arrange - Create record with file size
        var originalSize = 1000000L;
        var createDto = new CreateRecordRequestDto
        {
            Name = "Preserve Size Record",
            Description = "Original description",
            Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { TestProp = "TestValue" }))!,
            OriginalId = "preserve-size-test",
            ClassId = cid,
            FileSize = originalSize
        };
        var createdRecord = await _recordBusiness.CreateRecord(uid, organizationId, pid, did, createDto);

        // Act - Update only name, not file size
        var updateDto = new UpdateRecordRequestDto
        {
            Name = "Updated Name Only"
        };
        var result = await _recordBusiness.UpdateRecord(uid, organizationId, pid, createdRecord.Id, updateDto);

        // Assert - File size should be preserved
        Assert.Equal(originalSize, result.FileSize);

        // Verify in database
        var dbRecord = await Context.Records.FindAsync(result.Id);
        Assert.Equal(originalSize, dbRecord.FileSize);
    }

    [Fact]
    public async Task GetRecord_ReturnsFileSize()
    {
        // Arrange - Create record with file size
        var fileSize = 2048000L;
        var createDto = new CreateRecordRequestDto
        {
            Name = "Get File Size Record",
            Description = "Test getting file size",
            Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { TestProp = "TestValue" }))!,
            OriginalId = "get-filesize-test",
            ClassId = cid,
            FileSize = fileSize
        };
        var createdRecord = await _recordBusiness.CreateRecord(uid, organizationId, pid, did, createDto);

        // Act
        var result = await _recordBusiness.GetRecord(uid, organizationId, pid, createdRecord.Id, true);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(fileSize, result.FileSize);
    }

    [Fact]
    public async Task GetAllRecords_ReturnsFileSizes()
    {
        // Arrange - Create multiple records with different file sizes
        var sizes = new[] { 100000L, 500000L, 1000000L };
        var recordIds = new List<long>();

        for (int i = 0; i < sizes.Length; i++)
        {
            var dto = new CreateRecordRequestDto
            {
                Name = $"Record {i}",
                Description = $"Record with size {sizes[i]}",
                Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { Index = i }))!,
                OriginalId = $"multi-size-{i}",
                ClassId = cid,
                FileSize = sizes[i]
            };
            var record = await _recordBusiness.CreateRecord(uid, organizationId, pid, did, dto);
            recordIds.Add(record.Id);
        }

        // Act
        var allRecords = await _recordBusiness.GetAllRecords(uid, organizationId, pid, did, true);

        // Assert
        var createdRecords = allRecords.Where(r => recordIds.Contains(r.Id)).OrderBy(r => r.FileSize).ToList();
        Assert.Equal(3, createdRecords.Count);

        for (int i = 0; i < sizes.Length; i++)
        {
            Assert.Equal(sizes[i], createdRecords[i].FileSize);
        }
    }

    [Fact]
    public async Task GetRecordsByTags_ReturnsFileSizes()
    {
        // Arrange - Create records with tags and file sizes
        var tag = await Context.Tags.FirstAsync(t => t.Id == tid);

        var dto1 = new CreateRecordRequestDto
        {
            Name = "Tagged Record 1",
            Description = "Record with tag and size",
            Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { Test = "Value" }))!,
            OriginalId = "tagged-size-1",
            ClassId = cid,
            FileSize = 250000L,
            Tags = new List<string> { tag.Name }
        };

        var dto2 = new CreateRecordRequestDto
        {
            Name = "Tagged Record 2",
            Description = "Record with tag and size",
            Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { Test = "Value" }))!,
            OriginalId = "tagged-size-2",
            ClassId = cid,
            FileSize = 750000L,
            Tags = new List<string> { tag.Name }
        };

        await _recordBusiness.CreateRecord(uid, organizationId, pid, did, dto1);
        await _recordBusiness.CreateRecord(uid, organizationId, pid, did, dto2);

        // Act
        var results = await _recordBusiness.GetRecordsByTags(uid, organizationId, pid, new[] { tid }, true);

        // Assert
        var taggedRecords = results.Where(r => r.Name.StartsWith("Tagged Record")).ToList();
        Assert.Equal(2, taggedRecords.Count);
        Assert.All(taggedRecords, r => Assert.NotNull(r.FileSize));
        Assert.Contains(taggedRecords, r => r.FileSize == 250000L);
        Assert.Contains(taggedRecords, r => r.FileSize == 750000L);
    }

    [Fact]
    public async Task GetRecordsByOriginalId_ReturnsFileSizes()
    {
        // Arrange - Create records with file sizes
        var originalIds = new List<string> { "orig-size-1", "orig-size-2" };
        var sizes = new[] { 300000L, 600000L };

        for (int i = 0; i < originalIds.Count; i++)
        {
            var dto = new CreateRecordRequestDto
            {
                Name = $"Original ID Record {i}",
                Description = "Test record",
                Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { Index = i }))!,
                OriginalId = originalIds[i],
                ClassId = cid,
                FileSize = sizes[i]
            };
            await _recordBusiness.CreateRecord(uid, organizationId, pid, did, dto);
        }

        // Act
        var results = await _recordBusiness.GetRecordsByOriginalId(uid, organizationId, pid, did, originalIds, true);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.NotNull(r.FileSize));

        var record1 = results.First(r => r.OriginalId == "orig-size-1");
        var record2 = results.First(r => r.OriginalId == "orig-size-2");

        Assert.Equal(300000L, record1.FileSize);
        Assert.Equal(600000L, record2.FileSize);
    }

    [Fact]
    public async Task BulkCreateRecords_WithFileSizes_CreatesAllCorrectly()
    {
        // Arrange
        var records = new List<CreateRecordRequestDto>
        {
            new()
            {
                Name = "Bulk Record 1",
                Description = "Bulk record with size",
                OriginalId = "bulk-size-1",
                Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { Index = 1 }))!,
                ClassId = cid,
                FileSize = 100000L
            },
            new()
            {
                Name = "Bulk Record 2",
                Description = "Bulk record with size",
                OriginalId = "bulk-size-2",
                Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { Index = 2 }))!,
                ClassId = cid,
                FileSize = 200000L
            },
            new()
            {
                Name = "Bulk Record 3",
                Description = "Bulk record with size",
                OriginalId = "bulk-size-3",
                Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { Index = 3 }))!,
                ClassId = cid,
                FileSize = 300000L
            }
        };

        // Act
        var results = await _recordBusiness.BulkCreateRecords(uid, organizationId, pid, did, records);

        // Assert
        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.NotNull(r.FileSize));

        Assert.Contains(results, r => r.FileSize == 100000L);
        Assert.Contains(results, r => r.FileSize == 200000L);
        Assert.Contains(results, r => r.FileSize == 300000L);

        // Verify in database
        foreach (var result in results)
        {
            var dbRecord = await Context.Records.FindAsync(result.Id);
            Assert.NotNull(dbRecord);
            Assert.Equal(result.FileSize, dbRecord.FileSize);
        }
    }

    [Fact]
    public async Task BulkCreateRecords_MixedFileSizes_HandlesNullsCorrectly()
    {
        // Arrange - Some records with file sizes, some without
        var records = new List<CreateRecordRequestDto>
        {
            new()
            {
                Name = "With Size",
                Description = "Has file size",
                OriginalId = "mixed-1",
                Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { Test = "Value" }))!,
                ClassId = cid,
                FileSize = 500000L
            },
            new()
            {
                Name = "Without Size",
                Description = "No file size",
                OriginalId = "mixed-2",
                Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { Test = "Value" }))!,
                ClassId = cid,
                FileSize = null
            }
        };

        // Act
        var results = await _recordBusiness.BulkCreateRecords(uid, organizationId, pid, did, records);

        // Assert
        Assert.Equal(2, results.Count);

        var withSize = results.First(r => r.Name == "With Size");
        var withoutSize = results.First(r => r.Name == "Without Size");

        Assert.Equal(500000L, withSize.FileSize);
        Assert.Null(withoutSize.FileSize);
    }

    [Fact]
    public async Task ArchiveRecord_PreservesFileSize()
    {
        // Arrange - Create record with file size
        var fileSize = 1500000L;
        var dto = new CreateRecordRequestDto
        {
            Name = "Archive Size Test",
            Description = "Test file size after archive",
            Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { Test = "Value" }))!,
            OriginalId = "archive-size-test",
            ClassId = cid,
            FileSize = fileSize
        };
        var createdRecord = await _recordBusiness.CreateRecord(uid, organizationId, pid, did, dto);

        // Act - Archive the record
        await _recordBusiness.ArchiveRecord(uid, organizationId, pid, createdRecord.Id);

        // Assert - File size should be preserved even when archived
        Context.ChangeTracker.Clear();
        var archivedRecord = await Context.Records.FindAsync(createdRecord.Id);
        Assert.NotNull(archivedRecord);
        Assert.True(archivedRecord.IsArchived);
        Assert.Equal(fileSize, archivedRecord.FileSize);
    }

    [Fact]
    public async Task UnarchiveRecord_PreservesFileSize()
    {
        // Arrange - Create and archive record with file size
        var fileSize = 2000000L;
        var dto = new CreateRecordRequestDto
        {
            Name = "Unarchive Size Test",
            Description = "Test file size after unarchive",
            Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { Test = "Value" }))!,
            OriginalId = "unarchive-size-test",
            ClassId = cid,
            FileSize = fileSize
        };
        var createdRecord = await _recordBusiness.CreateRecord(uid, organizationId, pid, did, dto);
        await _recordBusiness.ArchiveRecord(uid, organizationId, pid, createdRecord.Id);

        // Act - Unarchive the record
        await _recordBusiness.UnarchiveRecord(uid, organizationId, pid, createdRecord.Id);

        // Assert - File size should still be preserved
        Context.ChangeTracker.Clear();
        var unarchivedRecord = await Context.Records.FindAsync(createdRecord.Id);
        Assert.NotNull(unarchivedRecord);
        Assert.False(unarchivedRecord.IsArchived);
        Assert.Equal(fileSize, unarchivedRecord.FileSize);
    }

    [Fact]
    public async Task GetRecordsCountByDataSource_IncludesRecordsWithAndWithoutFileSize()
    {
        // Arrange - Create records with and without file sizes
        var dto1 = new CreateRecordRequestDto
        {
            Name = "With Size Count",
            Description = "Has size",
            Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { Test = "Value" }))!,
            OriginalId = "count-with-size",
            ClassId = cid,
            FileSize = 100000L
        };

        var dto2 = new CreateRecordRequestDto
        {
            Name = "Without Size Count",
            Description = "No size",
            Properties = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new { Test = "Value" }))!,
            OriginalId = "count-without-size",
            ClassId = cid,
            FileSize = null
        };

        await _recordBusiness.CreateRecord(uid, organizationId, pid, did, dto1);
        await _recordBusiness.CreateRecord(uid, organizationId, pid, did, dto2);

        // Act
        var count = await _recordBusiness.GetRecordsCountByDataSource(organizationId, pid, did, true);

        // Assert - Both records should be counted regardless of file size
        Assert.True(count >= 2);
    }

    [Fact]
    public async Task GetRecord_ReturnsUriOnlyWhenUserCanDownload()
    {
        // Arrange
        var adminUser = new User
        {
            Name = "Admin",
            Email = $"admin-{Guid.NewGuid()}@test.com",
            IsSysAdmin = true,
            IsActive = true
        };

        var restrictedUser = new User
        {
            Name = "Restricted",
            Email = $"restricted-{Guid.NewGuid()}@test.com",
            IsSysAdmin = false,
            IsActive = true
        };

        Context.Users.Add(adminUser);
        Context.Users.Add(restrictedUser);
        await Context.SaveChangesAsync();

        var label = new SensitivityLabel
        {
            Name = $"Test Label {Guid.NewGuid()}",
            Description = "Test label for download permission",
            OrganizationId = organizationId,
            ProjectId = pid,
            LastUpdatedBy = adminUser.Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified),
            IsArchived = false
        };

        Context.SensitivityLabels.Add(label);
        await Context.SaveChangesAsync();

        var permission = new Permission
        {
            Name = $"Download File Permission {Guid.NewGuid()}",
            Description = "Allows file download for this label",
            Action = "download file",
            LabelId = label.Id,
            OrganizationId = organizationId,
            ProjectId = pid,
            LastUpdatedBy = adminUser.Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified),
            IsArchived = false,
            IsDefault = false
        };

        var role = new Role
        {
            Name = $"Download Role {Guid.NewGuid()}",
            Description = "Role with download file permission",
            OrganizationId = organizationId,
            ProjectId = pid,
            LastUpdatedBy = adminUser.Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified),
            IsArchived = false,
            Permissions = new List<Permission> { permission }
        };

        Context.Roles.Add(role);
        await Context.SaveChangesAsync();

        var projectMember = new ProjectMember
        {
            UserId = adminUser.Id,
            ProjectId = pid,
            RoleId = role.Id
        };

        Context.ProjectMembers.Add(projectMember);
        await Context.SaveChangesAsync();

        var expectedUri = $"../data/test/{Guid.NewGuid()}_protected-file.txt";

        var record = new Record
        {
            OrganizationId = organizationId,
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            Name = "Protected file record",
            Description = "Protected file record",
            Uri = expectedUri,
            Properties = "{}",
            OriginalId = Guid.NewGuid().ToString(),
            LastUpdatedBy = adminUser.Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified),
            IsArchived = false,
            FileType = "txt",
            FileSize = 1,
            Labels = new List<SensitivityLabel> { label }
        };

        Context.Records.Add(record);
        await Context.SaveChangesAsync();

        // Act
        var adminResult = await _recordBusiness.GetRecord(
            adminUser.Id,
            organizationId,
            pid,
            record.Id,
            hideArchived: true);

        var restrictedUserResult = await _recordBusiness.GetRecord(
            restrictedUser.Id,
            organizationId,
            pid,
            record.Id,
            hideArchived: true);

        // Assert
        Assert.Equal(expectedUri, adminResult.Uri);
        Assert.Null(restrictedUserResult.Uri);
    }

    [Fact]
    public async Task UpdateRecord_OnlyAllowsUriUpdateWhenUserCanUploadForLabel()
    {
        // Arrange
        var adminUser = new User
        {
            Name = "Admin",
            Email = $"admin-{Guid.NewGuid()}@test.com",
            IsSysAdmin = true,
            IsActive = true
        };

        var restrictedUser = new User
        {
            Name = "Restricted",
            Email = $"restricted-{Guid.NewGuid()}@test.com",
            IsSysAdmin = false,
            IsActive = true
        };

        Context.Users.Add(adminUser);
        Context.Users.Add(restrictedUser);
        await Context.SaveChangesAsync();

        var label = new SensitivityLabel
        {
            Name = $"Test Label {Guid.NewGuid()}",
            Description = "Test label for upload permission",
            OrganizationId = organizationId,
            ProjectId = pid,
            LastUpdatedBy = adminUser.Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified),
            IsArchived = false
        };

        Context.SensitivityLabels.Add(label);
        await Context.SaveChangesAsync();

        var uploadPermission = new Permission
        {
            Name = $"Upload File Permission {Guid.NewGuid()}",
            Description = "Allows file upload for this label",
            Action = "update file",
            LabelId = label.Id,
            OrganizationId = organizationId,
            ProjectId = pid,
            LastUpdatedBy = adminUser.Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified),
            IsArchived = false,
            IsDefault = false
        };

        var downloadPermission = new Permission
        {
            Name = $"Download File Permission {Guid.NewGuid()}",
            Description = "Allows file download for this label",
            Action = "download file",
            LabelId = label.Id,
            OrganizationId = organizationId,
            ProjectId = pid,
            LastUpdatedBy = adminUser.Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified),
            IsArchived = false,
            IsDefault = false
        };

        var role = new Role
        {
            Name = $"Upload Download Role {Guid.NewGuid()}",
            Description = "Role with upload and download file permission",
            OrganizationId = organizationId,
            ProjectId = pid,
            LastUpdatedBy = adminUser.Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified),
            IsArchived = false,
            Permissions = new List<Permission> { uploadPermission, downloadPermission }
        };

        Context.Roles.Add(role);
        await Context.SaveChangesAsync();

        Context.ProjectMembers.Add(new ProjectMember
        {
            UserId = adminUser.Id,
            ProjectId = pid,
            RoleId = role.Id
        });

        await Context.SaveChangesAsync();

        var originalUri = $"../data/test/{Guid.NewGuid()}_original-file.txt";

        var record = new Record
        {
            OrganizationId = organizationId,
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            Name = "Protected file record",
            Description = "Protected file record",
            Uri = originalUri,
            Properties = "{}",
            OriginalId = Guid.NewGuid().ToString(),
            LastUpdatedBy = adminUser.Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified),
            IsArchived = false,
            FileType = "txt",
            FileSize = 1,
            Labels = new List<SensitivityLabel> { label }
        };

        Context.Records.Add(record);
        await Context.SaveChangesAsync();

        var restrictedUpdateDto = new UpdateRecordRequestDto
        {
            Uri = $"../data/test/{Guid.NewGuid()}_restricted-update.txt",
            Properties = new JsonObject()
        };

        // Act / Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _recordBusiness.UpdateRecord(
                restrictedUser.Id,
                organizationId,
                pid,
                record.Id,
                restrictedUpdateDto));

        var adminUpdateUri = $"../data/test/{Guid.NewGuid()}_admin-update.txt";

        var adminUpdateDto = new UpdateRecordRequestDto
        {
            Uri = adminUpdateUri,
            Properties = new JsonObject()
        };

        var adminResult = await _recordBusiness.UpdateRecord(
            adminUser.Id,
            organizationId,
            pid,
            record.Id,
            adminUpdateDto);

        Assert.Equal(adminUpdateUri, adminResult.Uri);
    }

    [Fact]
    public async Task GetAllRecords_ReturnsUriOnlyWhenUserCanDownload()
    {
        var (adminUser, restrictedUser, label) =
            await SeedUriSecurityUsersAndLabel("download file");

        var record = await SeedLabeledRecord(adminUser, label);

        var adminResults = await _recordBusiness.GetAllRecords(
            adminUser.Id, organizationId, pid, did, hideArchived: true);

        var restrictedResults = await _recordBusiness.GetAllRecords(
            restrictedUser.Id, organizationId, pid, did, hideArchived: true);

        Assert.Equal(record.Uri, adminResults.Single(r => r.Id == record.Id).Uri);
        Assert.Null(restrictedResults.Single(r => r.Id == record.Id).Uri);
    }

    [Fact]
    public async Task GetRecordsByTags_ReturnsUriOnlyWhenUserCanDownload()
    {
        var (adminUser, restrictedUser, label) =
            await SeedUriSecurityUsersAndLabel("download file");

        var tagName = $"uri-test-tag-{Guid.NewGuid()}";
        var record = await SeedLabeledRecord(adminUser, label, tagName);
        var tagId = record.Tags.Single().Id;

        var adminResults = await _recordBusiness.GetRecordsByTags(
            adminUser.Id, organizationId, pid, new[] { tagId }, hideArchived: true);

        var restrictedResults = await _recordBusiness.GetRecordsByTags(
            restrictedUser.Id, organizationId, pid, new[] { tagId }, hideArchived: true);

        Assert.Equal(record.Uri, adminResults.Single(r => r.Id == record.Id).Uri);
        Assert.Null(restrictedResults.Single(r => r.Id == record.Id).Uri);
    }

    [Fact]
    public async Task GetRecordsByOriginalId_ReturnsUriOnlyWhenUserCanDownload()
    {
        var (adminUser, restrictedUser, label) =
            await SeedUriSecurityUsersAndLabel("download file");

        var record = await SeedLabeledRecord(adminUser, label);

        var adminResults = await _recordBusiness.GetRecordsByOriginalId(
            adminUser.Id, organizationId, pid, did, new List<string> { record.OriginalId }, hideArchived: true);

        var restrictedResults = await _recordBusiness.GetRecordsByOriginalId(
            restrictedUser.Id, organizationId, pid, did, new List<string> { record.OriginalId }, hideArchived: true);

        Assert.Equal(record.Uri, adminResults.Single().Uri);
        Assert.Null(restrictedResults.Single().Uri);
    }

    [Fact]
    public async Task CreateRecord_OnlyAllowsUriCreateWhenUserCanUploadForLabel()
    {
        var (adminUser, restrictedUser, label) =
            await SeedUriSecurityUsersAndLabel("upload file", "download file");

        var restrictedDto = new CreateRecordRequestDto
        {
            Name = "Restricted create URI test",
            Description = "Restricted create URI test",
            Uri = $"../data/test/{Guid.NewGuid()}_restricted-create.txt",
            Properties = new JsonObject(),
            OriginalId = Guid.NewGuid().ToString(),
            ClassId = cid,
            FileType = "txt",
            FileSize = 1
        };

        await Assert.ThrowsAsync<DependencyDeletionException>(() =>
            _recordBusiness.CreateRecord(
                restrictedUser.Id,
                organizationId,
                pid,
                did,
                restrictedDto,
                new List<long> { label.Id }));

        var adminDto = new CreateRecordRequestDto
        {
            Name = "Admin create URI test",
            Description = "Admin create URI test",
            Uri = $"../data/test/{Guid.NewGuid()}_admin-create.txt",
            Properties = new JsonObject(),
            OriginalId = Guid.NewGuid().ToString(),
            ClassId = cid,
            FileType = "txt",
            FileSize = 1
        };

        var result = await _recordBusiness.CreateRecord(
            adminUser.Id,
            organizationId,
            pid,
            did,
            adminDto,
            new List<long> { label.Id });

        Assert.Equal(adminDto.Uri, result.Uri);
    }

    [Fact]
    public async Task BulkCreateRecords_OnlyAllowsUriCreateWhenUserCanUploadForLabel()
    {
        var (adminUser, restrictedUser, label) =
            await SeedUriSecurityUsersAndLabel("upload file", "download file");

        var restrictedRecords = new List<CreateRecordRequestDto>
        {
            new()
            {
                Name = "Restricted bulk create URI test",
                Description = "Restricted bulk create URI test",
                Uri = $"../data/test/{Guid.NewGuid()}_restricted-bulk-create.txt",
                Properties = new JsonObject(),
                OriginalId = Guid.NewGuid().ToString(),
                ClassId = cid,
                FileType = "txt",
                FileSize = 1
            }
        };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _recordBusiness.BulkCreateRecords(
                restrictedUser.Id,
                organizationId,
                pid,
                did,
                restrictedRecords,
                new List<long> { label.Id }));

        var adminUri = $"../data/test/{Guid.NewGuid()}_admin-bulk-create.txt";

        var adminRecords = new List<CreateRecordRequestDto>
        {
            new()
            {
                Name = "Admin bulk create URI test",
                Description = "Admin bulk create URI test",
                Uri = adminUri,
                Properties = new JsonObject(),
                OriginalId = Guid.NewGuid().ToString(),
                ClassId = cid,
                FileType = "txt",
                FileSize = 1
            }
        };

        var results = await _recordBusiness.BulkCreateRecords(
            adminUser.Id,
            organizationId,
            pid,
            did,
            adminRecords,
            new List<long> { label.Id });

        Assert.Equal(adminUri, results.Single().Uri);
    }

    [Fact]
    public async Task CreateRecord_AllowsSysAdminToCreateUriWithoutUploadFilePermission()
    {
        var adminUser = new User
        {
            Name = "Admin",
            Email = $"admin-{Guid.NewGuid()}@test.com",
            IsSysAdmin = true,
            IsActive = true
        };

        Context.Users.Add(adminUser);
        await Context.SaveChangesAsync();

        var label = new SensitivityLabel
        {
            Name = $"Test Label {Guid.NewGuid()}",
            Description = "Test label",
            OrganizationId = organizationId,
            ProjectId = pid,
            LastUpdatedBy = adminUser.Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified),
            IsArchived = false
        };

        Context.SensitivityLabels.Add(label);
        await Context.SaveChangesAsync();

        var uri = $"../data/test/{Guid.NewGuid()}_admin-create.txt";

        var dto = new CreateRecordRequestDto
        {
            Name = "Admin create URI test",
            Description = "Admin create URI test",
            Uri = uri,
            Properties = new JsonObject(),
            OriginalId = Guid.NewGuid().ToString(),
            ClassId = cid,
            FileType = "txt",
            FileSize = 1
        };

        var result = await _recordBusiness.CreateRecord(
            adminUser.Id,
            organizationId,
            pid,
            did,
            dto,
            new List<long> { label.Id },
            embedded: false,
            isSysAdmin: true);

        Assert.Equal(uri, result.Uri);
    }

    [Fact]
    public async Task BulkCreateRecords_AllowsSysAdminToCreateUriWithoutUploadFilePermission()
    {
        var adminUser = new User
        {
            Name = "Admin",
            Email = $"admin-{Guid.NewGuid()}@test.com",
            IsSysAdmin = true,
            IsActive = true
        };

        Context.Users.Add(adminUser);
        await Context.SaveChangesAsync();

        var label = new SensitivityLabel
        {
            Name = $"Test Label {Guid.NewGuid()}",
            Description = "Test label",
            OrganizationId = organizationId,
            ProjectId = pid,
            LastUpdatedBy = adminUser.Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified),
            IsArchived = false
        };

        Context.SensitivityLabels.Add(label);
        await Context.SaveChangesAsync();

        var uri = $"../data/test/{Guid.NewGuid()}_admin-bulk-create.txt";

        var records = new List<CreateRecordRequestDto>
        {
            new()
            {
                Name = "Admin bulk create URI test",
                Description = "Admin bulk create URI test",
                Uri = uri,
                Properties = new JsonObject(),
                OriginalId = Guid.NewGuid().ToString(),
                ClassId = cid,
                FileType = "txt",
                FileSize = 1
            }
        };

        var results = await _recordBusiness.BulkCreateRecords(
            adminUser.Id,
            organizationId,
            pid,
            did,
            records,
            new List<long> { label.Id },
            isSysAdmin: true);

        Assert.Single(results);
        Assert.Equal(uri, results.Single().Uri);
    }

    [Fact]
    public async Task UpdateRecord_AllowsSysAdminToUpdateUriWithoutUpdateFilePermission()
    {
        var adminUser = new User
        {
            Name = "Admin",
            Email = $"admin-{Guid.NewGuid()}@test.com",
            IsSysAdmin = true,
            IsActive = true
        };

        Context.Users.Add(adminUser);
        await Context.SaveChangesAsync();

        var label = new SensitivityLabel
        {
            Name = $"Test Label {Guid.NewGuid()}",
            Description = "Test label",
            OrganizationId = organizationId,
            ProjectId = pid,
            LastUpdatedBy = adminUser.Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified),
            IsArchived = false
        };

        Context.SensitivityLabels.Add(label);
        await Context.SaveChangesAsync();

        var originalUri = $"../data/test/{Guid.NewGuid()}_original.txt";

        var record = new Record
        {
            OrganizationId = organizationId,
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            Name = "Admin update URI test",
            Description = "Admin update URI test",
            Uri = originalUri,
            Properties = "{}",
            OriginalId = Guid.NewGuid().ToString(),
            LastUpdatedBy = adminUser.Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified),
            IsArchived = false,
            FileType = "txt",
            FileSize = 1,
            Labels = new List<SensitivityLabel> { label }
        };

        Context.Records.Add(record);
        await Context.SaveChangesAsync();

        var updatedUri = $"../data/test/{Guid.NewGuid()}_updated.txt";

        var dto = new UpdateRecordRequestDto
        {
            Uri = updatedUri,
            Properties = new JsonObject()
        };

        var result = await _recordBusiness.UpdateRecord(
            adminUser.Id,
            organizationId,
            pid,
            record.Id,
            dto,
            isSysAdmin: true);

        Assert.Equal(updatedUri, result.Uri);
    }

    private async Task<(User adminUser, User restrictedUser, SensitivityLabel label)>
        SeedUriSecurityUsersAndLabel(params string[] permissionActions)
    {
        var adminUser = new User
        {
            Name = "Admin",
            Email = $"admin-{Guid.NewGuid()}@test.com",
            IsSysAdmin = true,
            IsActive = true
        };

        var restrictedUser = new User
        {
            Name = "Restricted",
            Email = $"restricted-{Guid.NewGuid()}@test.com",
            IsSysAdmin = false,
            IsActive = true
        };

        Context.Users.AddRange(adminUser, restrictedUser);
        await Context.SaveChangesAsync();

        var label = new SensitivityLabel
        {
            Name = $"Test Label {Guid.NewGuid()}",
            Description = "Test label for URI permission",
            OrganizationId = organizationId,
            ProjectId = pid,
            LastUpdatedBy = adminUser.Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified),
            IsArchived = false
        };

        Context.SensitivityLabels.Add(label);
        await Context.SaveChangesAsync();

        var readPermission = new Permission
        {
            Name = $"Read Record Permission {Guid.NewGuid()}",
            Description = "Allows record read for this label",
            Action = "read record",
            LabelId = label.Id,
            OrganizationId = organizationId,
            ProjectId = pid,
            LastUpdatedBy = adminUser.Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified),
            IsArchived = false,
            IsDefault = false
        };

        var adminPermissions = permissionActions.Select(action => new Permission
        {
            Name = $"{action} Permission {Guid.NewGuid()}",
            Description = $"Allows {action} for this label",
            Action = action,
            LabelId = label.Id,
            OrganizationId = organizationId,
            ProjectId = pid,
            LastUpdatedBy = adminUser.Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified),
            IsArchived = false,
            IsDefault = false
        }).ToList();

        var adminRole = new Role
        {
            Name = $"Admin URI Role {Guid.NewGuid()}",
            Description = "Role with URI permission",
            OrganizationId = organizationId,
            ProjectId = pid,
            LastUpdatedBy = adminUser.Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified),
            IsArchived = false,
            Permissions = new List<Permission> { readPermission }.Concat(adminPermissions).ToList()
        };

        var restrictedRole = new Role
        {
            Name = $"Restricted URI Role {Guid.NewGuid()}",
            Description = "Role with read permission only",
            OrganizationId = organizationId,
            ProjectId = pid,
            LastUpdatedBy = adminUser.Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified),
            IsArchived = false,
            Permissions = new List<Permission> { readPermission }
        };

        Context.Roles.AddRange(adminRole, restrictedRole);
        await Context.SaveChangesAsync();

        Context.ProjectMembers.AddRange(
            new ProjectMember { UserId = adminUser.Id, ProjectId = pid, RoleId = adminRole.Id },
            new ProjectMember { UserId = restrictedUser.Id, ProjectId = pid, RoleId = restrictedRole.Id });

        await Context.SaveChangesAsync();

        return (adminUser, restrictedUser, label);
    }

    private async Task<Record> SeedLabeledRecord(User user, SensitivityLabel label, string? tagName = null)
    {
        var record = new Record
        {
            OrganizationId = organizationId,
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            Name = $"Protected file record {Guid.NewGuid()}",
            Description = "Protected file record",
            Uri = $"../data/test/{Guid.NewGuid()}_protected-file.txt",
            Properties = "{}",
            OriginalId = Guid.NewGuid().ToString(),
            LastUpdatedBy = user.Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified),
            IsArchived = false,
            FileType = "txt",
            FileSize = 1,
            Labels = new List<SensitivityLabel> { label }
        };

        if (tagName != null)
        {
            record.Tags = new List<Tag>
            {
                new()
                {
                    Name = tagName,
                    OrganizationId = organizationId,
                    ProjectId = pid,
                    LastUpdatedBy = user.Id,
                    LastUpdatedAt = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified),
                    IsArchived = false
                }
            };
        }

        Context.Records.Add(record);
        await Context.SaveChangesAsync();

        return record;
    }

    #endregion

    #region GetInsightEligibleRecords Tests

    [Fact]
    public async Task GetInsightEligibleRecords_EligibleViaFileType_IsReturned()
    {
        // Arrange
        var eligible = new Record
        {
            Name = "Eligible PDF",
            Description = "Eligible record via FileType",
            OriginalId = Guid.NewGuid().ToString(),
            Properties = "{}",
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            Uri = "localhost:8090/file",
            FileType = "pdf",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            OrganizationId = organizationId
        };

        Context.Records.Add(eligible);
        await Context.SaveChangesAsync();

        // Act
        var result = await _recordBusiness.GetAllRecords(uid, organizationId, pid, null, false, isInsightEligible: true);

        // Assert
        Assert.Contains(result, r => r.Id == eligible.Id);
    }

    [Fact]
    public async Task GetInsightEligibleRecords_NoUri_IsNotReturned()
    {
        // Arrange - FileType is supported but URI is missing; URI is required for eligibility
        var noUri = new Record
        {
            Name = "No URI Record",
            Description = "Missing URI so not insight eligible",
            OriginalId = Guid.NewGuid().ToString(),
            Properties = "{}",
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            Uri = null,
            FileType = "pdf",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            OrganizationId = organizationId
        };

        Context.Records.Add(noUri);
        await Context.SaveChangesAsync();

        // Act
        var result = await _recordBusiness.GetAllRecords(uid, organizationId, pid, null, false, isInsightEligible: true);

        // Assert
        Assert.DoesNotContain(result, r => r.Id == noUri.Id);
    }

    [Fact]
    public async Task GetInsightEligibleRecords_UnsupportedFileType_IsNotReturned()
    {
        // Arrange - FileType is unsupported; URI/Name extensions are NOT checked when FileType is set
        var unsupported = new Record
        {
            Name = "report.pdf",                        // name has a supported extension
            Description = "Unsupported file type",
            OriginalId = Guid.NewGuid().ToString(),
            Properties = "{}",
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            Uri = "localhost:8090/file.pdf",            // URI also has a supported extension
            FileType = "csv",                           // FileType is set and unsupported — this wins
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            OrganizationId = organizationId
        };

        Context.Records.Add(unsupported);
        await Context.SaveChangesAsync();

        // Act
        var result = await _recordBusiness.GetAllRecords(uid, organizationId, pid, null, false, isInsightEligible: true);

        // Assert - FileType is authoritative when set; URI/Name extensions are ignored
        Assert.DoesNotContain(result, r => r.Id == unsupported.Id);
    }

    [Fact]
    public async Task GetInsightEligibleRecords_NullFileType_FallsBackToUriExtension()
    {
        // Arrange
        var eligibleViaUri = new Record
        {
            Name = "No FileType But Good URI",
            Description = "Eligible via URI extension",
            OriginalId = Guid.NewGuid().ToString(),
            Properties = "{}",
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            Uri = "localhost:8090/myfile.txt",
            FileType = null,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            OrganizationId = organizationId
        };

        Context.Records.Add(eligibleViaUri);
        await Context.SaveChangesAsync();

        // Act
        var result = await _recordBusiness.GetAllRecords(uid, organizationId, pid, null, false, isInsightEligible: true);

        // Assert
        Assert.Contains(result, r => r.Id == eligibleViaUri.Id);
    }

    [Fact]
    public async Task GetInsightEligibleRecords_NullFileType_UnsupportedUriExtension_IsNotReturned()
    {
        // Arrange - no FileType, URI extension is unsupported, Name has no extension
        var notEligible = new Record
        {
            Name = "No FileType Bad URI",
            Description = "Not eligible, URI extension unsupported",
            OriginalId = Guid.NewGuid().ToString(),
            Properties = "{}",
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            Uri = "localhost:8090/myfile.csv",
            FileType = null,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            OrganizationId = organizationId
        };

        Context.Records.Add(notEligible);
        await Context.SaveChangesAsync();

        // Act
        var result = await _recordBusiness.GetAllRecords(uid, organizationId, pid, null, false, isInsightEligible: true);

        // Assert
        Assert.DoesNotContain(result, r => r.Id == notEligible.Id);
    }

    [Fact]
    public async Task GetInsightEligibleRecords_NullFileType_FallsBackToNameExtension()
    {
        // Arrange - no FileType, URI has no supported extension, but Name ends with a supported extension
        var eligibleViaName = new Record
        {
            Name = "document.html",
            Description = "Eligible via Name extension",
            OriginalId = Guid.NewGuid().ToString(),
            Properties = "{}",
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            Uri = "localhost:8090/blob/abc123",  // no extension
            FileType = null,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            OrganizationId = organizationId
        };

        Context.Records.Add(eligibleViaName);
        await Context.SaveChangesAsync();

        // Act
        var result = await _recordBusiness.GetAllRecords(uid, organizationId, pid, null, false, isInsightEligible: true);

        // Assert
        Assert.Contains(result, r => r.Id == eligibleViaName.Id);
    }

    [Fact]
    public async Task GetInsightEligibleRecords_NullFileType_UnsupportedNameExtension_IsNotReturned()
    {
        // Arrange - no FileType, URI has no extension, Name extension is unsupported
        var notEligible = new Record
        {
            Name = "report.xlsx",
            Description = "Not eligible via Name extension",
            OriginalId = Guid.NewGuid().ToString(),
            Properties = "{}",
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            Uri = "localhost:8090/blob/def456",
            FileType = null,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            OrganizationId = organizationId
        };

        Context.Records.Add(notEligible);
        await Context.SaveChangesAsync();

        // Act
        var result = await _recordBusiness.GetAllRecords(uid, organizationId, pid, null, false, isInsightEligible: true);

        // Assert
        Assert.DoesNotContain(result, r => r.Id == notEligible.Id);
    }

    [Fact]
    public async Task GetInsightEligibleRecords_ExcludesRecordsFromOtherProjects()
    {
        // Arrange
        var otherProjectRecord = new Record
        {
            Name = "Other Project PDF",
            Description = "Belongs to pid2",
            OriginalId = Guid.NewGuid().ToString(),
            Properties = "{}",
            ProjectId = pid2,
            DataSourceId = did,
            ClassId = cid,
            Uri = "localhost:8090/other.pdf",
            FileType = "pdf",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            OrganizationId = organizationId
        };

        Context.Records.Add(otherProjectRecord);
        await Context.SaveChangesAsync();

        // Act
        var result = await _recordBusiness.GetAllRecords(uid, organizationId, pid, null, false, isInsightEligible: true);

        // Assert
        Assert.DoesNotContain(result, r => r.Id == otherProjectRecord.Id);
    }

    [Fact]
    public async Task GetInsightEligibleRecords_ReturnsTags()
    {
        // Arrange
        var record = new Record
        {
            Name = "Tagged Eligible Record",
            Description = "Has a tag and is insight eligible",
            OriginalId = Guid.NewGuid().ToString(),
            Properties = "{}",
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            Uri = "localhost:8090/tagged.pdf",
            FileType = "pdf",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            OrganizationId = organizationId
        };

        Context.Records.Add(record);
        await Context.SaveChangesAsync();

        await _recordBusiness.AttachTag(uid, organizationId, pid, record.Id, tid);

        // Act
        var result = await _recordBusiness.GetAllRecords(uid, organizationId, pid, null, false, isInsightEligible: true);

        // Assert
        var match = result.First(r => r.Id == record.Id);
        Assert.NotNull(match.Tags);
        Assert.Contains(match.Tags, t => t.Name == "Test Tag");
    }

    [Fact]
    public async Task GetInsightEligibleRecords_NonAdminUser_ExcludesUnauthorizedLabeledRecords()
    {
        // Arrange - create a label with no permissions granted to the non-admin user
        var restrictedLabel = new SensitivityLabel
        {
            Name = $"Restricted Label {Guid.NewGuid()}",
            Description = "No read permission for non-admin",
            OrganizationId = organizationId,
            ProjectId = pid,
            LastUpdatedBy = uid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            IsArchived = false
        };

        var restrictedRecord = new Record
        {
            Name = "Restricted Eligible Record",
            Description = "Eligible file type but unauthorized label",
            OriginalId = Guid.NewGuid().ToString(),
            Properties = "{}",
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            Uri = "localhost:8090/restricted.pdf",
            FileType = "pdf",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            OrganizationId = organizationId,
            Labels = new List<SensitivityLabel> { restrictedLabel }
        };

        var otherUser = new User
        {
            Name = "Other User",
            Email = $"other-{Guid.NewGuid()}@test.com",
            IsActive = true
        };

        Context.Users.Add(otherUser);
        Context.Records.Add(restrictedRecord);
        await Context.SaveChangesAsync();

        // Act
        var result = await _recordBusiness.GetAllRecords(
            otherUser.Id, organizationId, pid, null, false,
            isSysAdmin: false, isOrgAdmin: false, isProjectAdmin: false, isInsightEligible: true);

        // Assert
        Assert.DoesNotContain(result, r => r.Id == restrictedRecord.Id);
    }

    [Fact]
    public async Task GetInsightEligibleRecords_SysAdmin_IncludesAllLabeledRecords()
    {
        // Arrange - record with a label that has no explicit permissions
        var label = new SensitivityLabel
        {
            Name = $"Admin-Only Label {Guid.NewGuid()}",
            Description = "No read permission for regular users",
            OrganizationId = organizationId,
            ProjectId = pid,
            LastUpdatedBy = uid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            IsArchived = false
        };

        var labeledRecord = new Record
        {
            Name = "Admin Only Eligible Record",
            Description = "Eligible file but restricted label",
            OriginalId = Guid.NewGuid().ToString(),
            Properties = "{}",
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            Uri = "localhost:8090/admin-only.jpg",
            FileType = "jpg",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            OrganizationId = organizationId,
            Labels = new List<SensitivityLabel> { label }
        };

        Context.Records.Add(labeledRecord);
        await Context.SaveChangesAsync();

        // Act
        var result = await _recordBusiness.GetAllRecords(uid, organizationId, pid, null, false, isSysAdmin: true, isInsightEligible: true);

        // Assert
        Assert.Contains(result, r => r.Id == labeledRecord.Id);
    }

    [Fact]
    public async Task GetInsightEligibleRecords_AllSupportedFileTypes_AreEligible()
    {
        // Arrange - one record per supported file type
        var supportedTypes = new[] { "pdf", "txt", "html", "htm", "png", "jpg", "jpeg", "webp" };

        var records = supportedTypes.Select(ext => new Record
        {
            Name = $"Record {ext}",
            Description = $"Eligible record with FileType {ext}",
            OriginalId = Guid.NewGuid().ToString(),
            Properties = "{}",
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            Uri = "localhost:8090/file",
            FileType = ext,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            OrganizationId = organizationId
        }).ToList();

        Context.Records.AddRange(records);
        await Context.SaveChangesAsync();

        // Act
        var result = await _recordBusiness.GetAllRecords(uid, organizationId, pid, null, false, isInsightEligible: true);

        // Assert
        foreach (var r in records)
            Assert.Contains(result, res => res.Id == r.Id);
    }

    [Fact]
    public async Task GetInsightEligibleRecords_FileTypeCheck_IsCaseInsensitive()
    {
        // Arrange
        var upperCaseFileType = new Record
        {
            Name = "Uppercase FileType",
            Description = "FileType in uppercase",
            OriginalId = Guid.NewGuid().ToString(),
            Properties = "{}",
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            Uri = "localhost:8090/file",
            FileType = "PDF",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            OrganizationId = organizationId
        };

        Context.Records.Add(upperCaseFileType);
        await Context.SaveChangesAsync();

        // Act
        var result = await _recordBusiness.GetAllRecords(uid, organizationId, pid, null, false, isInsightEligible: true);

        // Assert
        Assert.Contains(result, r => r.Id == upperCaseFileType.Id);
    }

    [Fact]
    public async Task GetInsightEligibleRecords_UriAndNameExtensionChecks_AreCaseInsensitive()
    {
        // Arrange - no FileType on either record, so URI/Name extension fallback applies
        var upperCaseUriExt = new Record
        {
            Name = "No FileType Mixed URI",
            Description = "URI extension in uppercase",
            OriginalId = Guid.NewGuid().ToString(),
            Properties = "{}",
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            Uri = "localhost:8090/file.TXT",
            FileType = null,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            OrganizationId = organizationId
        };

        var upperCaseNameExt = new Record
        {
            Name = "Document.PNG",
            Description = "Name extension in uppercase",
            OriginalId = Guid.NewGuid().ToString(),
            Properties = "{}",
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            Uri = "localhost:8090/blob/xyz",     // no extension
            FileType = null,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            OrganizationId = organizationId
        };

        Context.Records.AddRange(upperCaseUriExt, upperCaseNameExt);
        await Context.SaveChangesAsync();

        // Act
        var result = await _recordBusiness.GetAllRecords(uid, organizationId, pid, null, false, isInsightEligible: true);

        // Assert
        Assert.Contains(result, r => r.Id == upperCaseUriExt.Id);
        Assert.Contains(result, r => r.Id == upperCaseNameExt.Id);
    }

    #endregion

    #region SearchPaginated Tests

    private static PaginatedRequestDto DefaultPagination() => new() { PageNumber = 1, PageSize = 50 };

    private static RecordSearchRequestDto DefaultSearch() => new()
    {
        UserQuery = null,
        ClassIds = [],
        TagIds = [],
        Embedding = "any",
        HideArchived = false,
        IsInsightEligible = false
    };

    [Fact]
    public async Task SearchPaginated_NoFilters_ReturnsAllRecords()
    {
        // Act
        var result = await _recordBusiness.SearchPaginated(
            uid, organizationId, pid, DefaultSearch(), DefaultPagination(), isSysAdmin: true);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(4, result.TotalCount);
    }

    [Fact]
    public async Task SearchPaginated_NoFilters_ReturnsRecordsWithinProject()
    {
        // Act
        var result = await _recordBusiness.SearchPaginated(
            uid, organizationId, 99999L, DefaultSearch(), DefaultPagination(), isSysAdmin: true);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task SearchPaginated_HideArchivedTrue_ExcludesArchivedRecords()
    {
        // Arrange
        var record = await Context.Records.FindAsync(rid);
        record!.IsArchived = true;
        await Context.SaveChangesAsync();

        var search = DefaultSearch();
        search.HideArchived = true;

        // Act
        var result = await _recordBusiness.SearchPaginated(
            uid, organizationId, pid, search, DefaultPagination(), isSysAdmin: true);

        // Assert
        Assert.Equal(3, result.TotalCount);
        Assert.DoesNotContain(result.Items, r => r.Id == rid);
    }

    [Fact]
    public async Task SearchPaginated_HideArchivedFalse_IncludesArchivedRecords()
    {
        // Arrange
        var record = await Context.Records.FindAsync(rid);
        record!.IsArchived = true;
        await Context.SaveChangesAsync();

        var search = DefaultSearch();
        search.HideArchived = false;

        // Act
        var result = await _recordBusiness.SearchPaginated(
            uid, organizationId, pid, search, DefaultPagination(), isSysAdmin: true);

        // Assert
        Assert.Equal(4, result.TotalCount);
        Assert.Contains(result.Items, r => r.Id == rid);
    }

    [Fact]
    public async Task SearchPaginated_ClassIdFilter_ReturnsOnlyMatchingRecords()
    {
        // Arrange - rid and rid2 have cid, rid3 and rid4 also have cid per seed data
        var search = DefaultSearch();
        search.ClassIds = [cid];

        // Act
        var result = await _recordBusiness.SearchPaginated(
            uid, organizationId, pid, search, DefaultPagination(), isSysAdmin: true);

        // Assert
        Assert.True(result.TotalCount > 0);
        Assert.All(result.Items, r => Assert.Equal(cid, r.ClassId));
    }

    [Fact]
    public async Task SearchPaginated_NonExistentClassId_ReturnsEmpty()
    {
        // Arrange
        var search = DefaultSearch();
        search.ClassIds = [999999L];

        // Act
        var result = await _recordBusiness.SearchPaginated(
            uid, organizationId, pid, search, DefaultPagination(), isSysAdmin: true);

        // Assert
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task SearchPaginated_TagFilter_ReturnsOnlyRecordsWithAllTags()
    {
        // Arrange
        await _recordBusiness.AttachTag(uid, organizationId, pid, rid, tid);

        var search = DefaultSearch();
        search.TagIds = [tid];

        // Act
        var result = await _recordBusiness.SearchPaginated(
            uid, organizationId, pid, search, DefaultPagination(), isSysAdmin: true);

        // Assert
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(rid, result.Items.Single().Id);
    }

    [Fact]
    public async Task SearchPaginated_MultipleTagFilter_ReturnsOnlyRecordsWithAllTags()
    {
        // Arrange
        var tag2 = new Tag
        {
            Name = "Search Tag 2",
            ProjectId = pid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = organizationId
        };
        Context.Tags.Add(tag2);
        await Context.SaveChangesAsync();

        await _recordBusiness.AttachTag(uid, organizationId, pid, rid, tid);
        await _recordBusiness.AttachTag(uid, organizationId, pid, rid, tag2.Id);
        await _recordBusiness.AttachTag(uid, organizationId, pid, rid2, tid);

        var search = DefaultSearch();
        search.TagIds = [tid, tag2.Id];

        // Act
        var result = await _recordBusiness.SearchPaginated(
            uid, organizationId, pid, search, DefaultPagination(), isSysAdmin: true);

        // Assert - only rid has both tags
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(rid, result.Items.Single().Id);
    }

    [Fact]
    public async Task SearchPaginated_EmbeddedFilter_ReturnsOnlyEmbeddedRecords()
    {
        // Arrange - mark rid as embedded, leave the rest as not embedded
        var record = await Context.Records.FindAsync(rid);
        record!.Embedded = true;
        await Context.SaveChangesAsync();

        var search = DefaultSearch();
        search.Embedding = "embedded";

        // Act
        var result = await _recordBusiness.SearchPaginated(
            uid, organizationId, pid, search, DefaultPagination(), isSysAdmin: true);

        // Assert - only the record we marked should appear
        Assert.Equal(1, result.TotalCount);
        Assert.Contains(result.Items, r => r.Id == rid);

        // Verify via DB that all returned IDs are actually embedded
        var returnedIds = result.Items.Select(r => r.Id).ToList();
        var dbRecords = await Context.Records
            .Where(r => returnedIds.Contains(r.Id))
            .ToListAsync();
        Assert.All(dbRecords, r => Assert.True(r.Embedded));
    }

    [Fact]
    public async Task SearchPaginated_NotEmbeddedFilter_ReturnsOnlyNotEmbeddedRecords()
    {
        // Arrange - mark rid as embedded so we can confirm it is excluded
        var record = await Context.Records.FindAsync(rid);
        record!.Embedded = true;
        await Context.SaveChangesAsync();

        var search = DefaultSearch();
        search.Embedding = "pending";

        // Act
        var result = await _recordBusiness.SearchPaginated(
            uid, organizationId, pid, search, DefaultPagination(), isSysAdmin: true);

        // Assert - the embedded record must be absent; all others must not be embedded
        Assert.DoesNotContain(result.Items, r => r.Id == rid);

        var returnedIds = result.Items.Select(r => r.Id).ToList();
        var dbRecords = await Context.Records
            .Where(r => returnedIds.Contains(r.Id))
            .ToListAsync();
        Assert.All(dbRecords, r => Assert.False(r.Embedded));
    }

    [Fact]
    public async Task SearchPaginated_UserQuery_MatchesRecordName()
    {
        // Act
        var search = DefaultSearch();
        search.UserQuery = "Test Record";

        var result = await _recordBusiness.SearchPaginated(
            uid, organizationId, pid, search, DefaultPagination(), isSysAdmin: true);

        // Assert
        Assert.True(result.TotalCount > 0);
        Assert.All(result.Items, r =>
            Assert.True(
                r.Name.Contains("Test Record", StringComparison.OrdinalIgnoreCase) ||
                r.Description?.Contains("Test Record", StringComparison.OrdinalIgnoreCase) == true));
    }

    [Fact]
    public async Task SearchPaginated_UserQuery_MatchesOriginalId()
    {
        // Act
        var search = DefaultSearch();
        search.UserQuery = "og_id";

        var result = await _recordBusiness.SearchPaginated(
            uid, organizationId, pid, search, DefaultPagination(), isSysAdmin: true);

        // Assert
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(rid, result.Items.Single().Id);
    }

    [Fact]
    public async Task SearchPaginated_UserQuery_NoMatch_ReturnsEmpty()
    {
        // Arrange
        var search = DefaultSearch();
        search.UserQuery = "zzz_absolutely_no_match_xyz";

        // Act
        var result = await _recordBusiness.SearchPaginated(
            uid, organizationId, pid, search, DefaultPagination(), isSysAdmin: true);

        // Assert
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task SearchPaginated_Pagination_RespectsPageSize()
    {
        // Arrange
        var paginated = new PaginatedRequestDto { PageNumber = 1, PageSize = 2 };

        // Act
        var result = await _recordBusiness.SearchPaginated(
            uid, organizationId, pid, DefaultSearch(), paginated, isSysAdmin: true);

        // Assert
        Assert.Equal(4, result.TotalCount);
        Assert.Equal(2, result.Items.Count());
    }

    [Fact]
    public async Task SearchPaginated_Pagination_SecondPage_ReturnsRemainingRecords()
    {
        // Arrange
        var page1 = new PaginatedRequestDto { PageNumber = 1, PageSize = 2 };
        var page2 = new PaginatedRequestDto { PageNumber = 2, PageSize = 2 };

        // Act
        var result1 = await _recordBusiness.SearchPaginated(
            uid, organizationId, pid, DefaultSearch(), page1, isSysAdmin: true);
        var result2 = await _recordBusiness.SearchPaginated(
            uid, organizationId, pid, DefaultSearch(), page2, isSysAdmin: true);

        // Assert
        Assert.Equal(2, result1.Items.Count());
        Assert.Equal(2, result2.Items.Count());
        Assert.Empty(result1.Items.Select(r => r.Id).Intersect(result2.Items.Select(r => r.Id)));
    }

    [Fact]
    public async Task SearchPaginated_NonAdmin_ExcludesUnauthorizedLabeledRecords()
    {
        // Arrange - create a label with no read permission for the test user
        var restrictedLabel = new SensitivityLabel
        {
            Name = $"Restricted {Guid.NewGuid()}",
            Description = "No read permission",
            OrganizationId = organizationId,
            ProjectId = pid,
            LastUpdatedBy = uid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            IsArchived = false
        };

        var restrictedRecord = new Record
        {
            Name = "Restricted Record",
            Description = "Should not appear for non-admin",
            OriginalId = Guid.NewGuid().ToString(),
            Properties = "{}",
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            OrganizationId = organizationId,
            FileType = "pdf",
            Labels = new List<SensitivityLabel> { restrictedLabel }
        };

        var otherUser = new User
        {
            Name = "Other User",
            Email = $"other-search-{Guid.NewGuid()}@test.com",
            IsActive = true
        };

        Context.Users.Add(otherUser);
        Context.Records.Add(restrictedRecord);
        await Context.SaveChangesAsync();

        // Act
        var result = await _recordBusiness.SearchPaginated(
            otherUser.Id, organizationId, pid, DefaultSearch(), DefaultPagination(),
            isSysAdmin: false, isOrgAdmin: false, isProjectAdmin: false);

        // Assert
        Assert.DoesNotContain(result.Items, r => r.Id == restrictedRecord.Id);
    }

    [Fact]
    public async Task SearchPaginated_SysAdmin_IncludesAllLabeledRecords()
    {
        // Arrange
        var restrictedLabel = new SensitivityLabel
        {
            Name = $"SysAdmin Label {Guid.NewGuid()}",
            Description = "Admin only",
            OrganizationId = organizationId,
            ProjectId = pid,
            LastUpdatedBy = uid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            IsArchived = false
        };

        var restrictedRecord = new Record
        {
            Name = "Admin Only Record",
            Description = "Visible to sysadmin",
            OriginalId = Guid.NewGuid().ToString(),
            Properties = "{}",
            ProjectId = pid,
            DataSourceId = did,
            ClassId = cid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            OrganizationId = organizationId,
            FileType = "pdf",
            Labels = new List<SensitivityLabel> { restrictedLabel }
        };

        Context.Records.Add(restrictedRecord);
        await Context.SaveChangesAsync();

        // Act
        var result = await _recordBusiness.SearchPaginated(
            uid, organizationId, pid, DefaultSearch(), DefaultPagination(), isSysAdmin: true);

        // Assert
        Assert.Contains(result.Items, r => r.Id == restrictedRecord.Id);
    }

    [Fact]
    public async Task SearchPaginated_IsInsightEligible_ReturnsOnlyEligibleRecords()
    {
        // Arrange
        var search = DefaultSearch();
        search.IsInsightEligible = true;
        search.HideArchived = false;

        // Act
        var result = await _recordBusiness.SearchPaginated(
            uid, organizationId, pid, search, DefaultPagination(), isSysAdmin: true);

        // Assert - seeded records with pdf file type and a URI should appear; csv-only records should not
        Assert.All(result.Items, r =>
        {
            var fileType = r.FileType?.ToLowerInvariant();
            var supportedTypes = new[] { "pdf", "txt", "html", "htm", "png", "jpg", "jpeg", "webp" };
            Assert.Contains(fileType, supportedTypes);
        });
    }

    [Fact]
    public async Task SearchPaginated_CombinedFilters_ClassAndTag_ReturnsIntersection()
    {
        // Arrange
        await _recordBusiness.AttachTag(uid, organizationId, pid, rid, tid);

        var search = DefaultSearch();
        search.ClassIds = [cid];
        search.TagIds = [tid];

        // Act
        var result = await _recordBusiness.SearchPaginated(
            uid, organizationId, pid, search, DefaultPagination(), isSysAdmin: true);

        // Assert - must satisfy both class and tag filter
        Assert.True(result.TotalCount > 0);
        Assert.All(result.Items, r =>
        {
            Assert.Equal(cid, r.ClassId);
            Assert.Contains(r.Tags, t => t.Id == tid);
        });
    }

    [Fact]
    public async Task SearchPaginated_CombinedFilters_QueryAndHideArchived_ReturnsCorrectResults()
    {
        // Arrange
        var record = await Context.Records.FindAsync(rid);
        record!.IsArchived = true;
        await Context.SaveChangesAsync();

        var search = DefaultSearch();
        search.UserQuery = "Test Record";
        search.HideArchived = true;

        // Act
        var result = await _recordBusiness.SearchPaginated(
            uid, organizationId, pid, search, DefaultPagination(), isSysAdmin: true);

        // Assert
        Assert.DoesNotContain(result.Items, r => r.Id == rid);
        Assert.All(result.Items, r => Assert.False(r.IsArchived));
    }

    [Fact]
    public async Task SearchPaginated_ReturnsTags()
    {
        // Arrange
        await _recordBusiness.AttachTag(uid, organizationId, pid, rid, tid);

        // Act
        var result = await _recordBusiness.SearchPaginated(
            uid, organizationId, pid, DefaultSearch(), DefaultPagination(), isSysAdmin: true);

        // Assert
        var match = result.Items.First(r => r.Id == rid);
        Assert.NotNull(match.Tags);
        Assert.Contains(match.Tags, t => t.Id == tid && t.Name == "Test Tag");
    }

    [Fact]
    public async Task SearchPaginated_ReturnsUriOnlyWhenUserCanDownload()
    {
        // Arrange
        var (adminUser, restrictedUser, label) =
            await SeedUriSecurityUsersAndLabel("download file");

        var record = await SeedLabeledRecord(adminUser, label);

        // Act
        var adminResult = await _recordBusiness.SearchPaginated(
            adminUser.Id, organizationId, pid, DefaultSearch(), DefaultPagination(), isSysAdmin: true);

        var restrictedResult = await _recordBusiness.SearchPaginated(
            restrictedUser.Id, organizationId, pid, DefaultSearch(), DefaultPagination());

        // Assert
        Assert.Equal(record.Uri, adminResult.Items.Single(r => r.Id == record.Id).Uri);
        Assert.Null(restrictedResult.Items.Single(r => r.Id == record.Id).Uri);
    }

    #endregion
}