using System.Text.Json;
using System.Text.Json.Nodes;
using deeplynx.business;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.helpers.BigData;
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
public class HistoricalRecordBusinessTests : IntegrationTestBase
{
    private EventBusiness _eventBusiness;
    private SensitivityLabelBusiness _sensitivityLabelBusiness = null!;
    private HistoricalRecordBusiness _historicalRecordBusiness = null!;
    private UserBusiness _userBusiness;
    private Mock<IHubContext<EventNotificationHub>> _mockHubContext = null!;
    private Mock<ILogger<NotificationBusiness>> _mockNotificationLogger = null!;
    private INotificationBusiness _notificationBusiness = null!;
    private RecordBusiness _recordBusiness = null!;
    private TagBusiness _tagBusiness = null!;
    private IBulkCopyUpsertExecutor _bulkCopyUpsertExecutor = null!;
    private ISensitivityLabelService _sensitivityLabelService = null!;
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
    public long roleId;
    protected long defaultLabelId;
    protected long defaultLabelId2;
    protected long readPermissionId;
    protected long writePermissionId;
    protected long readPermissionId2;
    protected long writePermissionId2;

    public HistoricalRecordBusinessTests(TestSuiteFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        _encryptionHelper = new EncryptionHelper();
        await base.InitializeAsync();
        _sensitivityLabelService = new SensitivityLabelService(Context);
        _historicalRecordBusiness = new HistoricalRecordBusiness(Context, _sensitivityLabelService);
        _mockHubContext = new Mock<IHubContext<EventNotificationHub>>();
        _mockNotificationLogger = new Mock<ILogger<NotificationBusiness>>();
        _notificationBusiness =
            new NotificationBusiness(Context, _mockNotificationLogger.Object, _mockHubContext.Object);
        _bulkCopyUpsertExecutor = new BulkCopyUpsertExecutor();
        _eventBusiness = new EventBusiness(Context, _notificationBusiness, _bulkCopyUpsertExecutor);
        _tagBusiness = new TagBusiness(Context, _eventBusiness);
        _userBusiness = new UserBusiness(Context);
        _sensitivityLabelBusiness = new SensitivityLabelBusiness(Context, _eventBusiness, _userBusiness);
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
        _recordBusiness = new RecordBusiness(Context, _eventBusiness, _bulkCopyUpsertExecutor, _tagBusiness,
            _sensitivityLabelBusiness, _sensitivityLabelService, _fileBusiness);
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
        Context.Tags.Add(testTag);
        Context.Tags.Add(testTag2);
        await Context.SaveChangesAsync();

        var config = new JsonObject();
        var objectStorage = new ObjectStorage
        {
            Name = "Object Storage 1",
            Type = "filesystem",
            ConfigEncrypted = _encryptionHelper.SerializeAndEncrypt(config),
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

        Context.Records.Add(testRecord);
        Context.Records.Add(testRecord2);
        Context.Records.Add(testRecord3);
        Context.Records.Add(testRecord4);
        await Context.SaveChangesAsync();

        rid = testRecord.Id;
        rid2 = testRecord2.Id;

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

        roleId = testRole.Id;

        var projectMember = new ProjectMember
        {
            ProjectId = pid,
            UserId = uid,
            RoleId = testRole.Id
        };
        Context.ProjectMembers.Add(projectMember);
        await Context.SaveChangesAsync();

        var defaultLabel = new SensitivityLabel
        {
            Name = "Default Test Label",
            Description = "Default test sensitivity label",
            ProjectId = pid,
            OrganizationId = organizationId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            IsArchived = false
        };
        Context.SensitivityLabels.Add(defaultLabel);
        await Context.SaveChangesAsync();
        defaultLabelId = defaultLabel.Id;

        // Create read permission for the label
        var readPermission = new Permission
        {
            Name = "Read Default Label",
            Description = "Read permission for default test label",
            Action = "read record",
            IsDefault = false,
            LabelId = defaultLabelId,
            ProjectId = pid,
            OrganizationId = organizationId,
            LastUpdatedBy = uid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            IsArchived = false
        };

        // Create write permission for the label
        var writePermission = new Permission
        {
            Name = "Write Default Label",
            Description = "Write permission for default test label",
            Action = "write record",
            IsDefault = false,
            LabelId = defaultLabelId,
            ProjectId = pid,
            OrganizationId = organizationId,
            LastUpdatedBy = uid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            IsArchived = false
        };

        var updatePermission = new Permission
        {
            Name = "Update Default Label",
            Description = "update permission for default test label",
            Action = "update record",
            IsDefault = false,
            LabelId = defaultLabelId,
            ProjectId = pid,
            OrganizationId = organizationId,
            LastUpdatedBy = uid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            IsArchived = false
        };

        Context.Permissions.Add(readPermission);
        Context.Permissions.Add(writePermission);
        Context.Permissions.Add(updatePermission);
        await Context.SaveChangesAsync();

        readPermissionId = readPermission.Id;
        writePermissionId = writePermission.Id;

        // Create second default sensitivity label
        var defaultLabel2 = new SensitivityLabel
        {
            Name = "Default Test Label 2",
            Description = "Second default test sensitivity label",
            ProjectId = pid,
            OrganizationId = organizationId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            IsArchived = false
        };
        Context.SensitivityLabels.Add(defaultLabel2);
        await Context.SaveChangesAsync();
        defaultLabelId2 = defaultLabel2.Id;

        // Create read permission for the second label
        var readPermission2 = new Permission
        {
            Name = "Read Default Label 2",
            Description = "Read permission for second default test label",
            Action = "read record",
            Resource = "sensitivity_label",
            IsDefault = false,
            LabelId = defaultLabelId2,
            ProjectId = pid,
            OrganizationId = organizationId,
            LastUpdatedBy = uid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            IsArchived = false
        };

        // Create write permission for the second label
        var writePermission2 = new Permission
        {
            Name = "Write Default Label 2",
            Description = "Write permission for second default test label",
            Action = "write record",
            Resource = "sensitivity_label",
            IsDefault = false,
            LabelId = defaultLabelId2,
            ProjectId = pid,
            OrganizationId = organizationId,
            LastUpdatedBy = uid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            IsArchived = false
        };

        var updatePermission2 = new Permission
        {
            Name = "update Default Label 2",
            Description = "Update permission for second default test label",
            Action = "update record",
            Resource = "sensitivity_label",
            IsDefault = false,
            LabelId = defaultLabelId2,
            ProjectId = pid,
            OrganizationId = organizationId,
            LastUpdatedBy = uid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            IsArchived = false
        };

        Context.Permissions.Add(readPermission2);
        Context.Permissions.Add(writePermission2);
        Context.Permissions.Add(updatePermission2);
        await Context.SaveChangesAsync();

        readPermissionId2 = readPermission2.Id;
        writePermissionId2 = writePermission2.Id;

        // Attach all permissions to the test role
        var role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null)
        {
            role.Permissions.Add(readPermission);
            role.Permissions.Add(writePermission);
            role.Permissions.Add(updatePermission);
            role.Permissions.Add(readPermission2);
            role.Permissions.Add(writePermission2);
            role.Permissions.Add(updatePermission2);
            await Context.SaveChangesAsync();
        }
    }

    #region GetHistoricalRecords Tests

    [Fact]
    public async Task GetHistoricalRecords_ReturnsListOfCurrentHistoricalRecordsForProject()
    {
        // Act
        var historicalRecords = await _historicalRecordBusiness.GetAllHistoricalRecords(uid, pid, organizationId);

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
        var historicalRecords = await _historicalRecordBusiness.GetAllHistoricalRecords(uid, pid, organizationId);

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
            await _historicalRecordBusiness.GetAllHistoricalRecords(uid, pid, organizationId, null, null, false);

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
        var historicalRecords = await _historicalRecordBusiness.GetAllHistoricalRecords(uid, pid, organizationId);

        Assert.NotNull(historicalRecords);
        Assert.Equal(2, historicalRecords.Count());

        // Act
        await _recordBusiness.ArchiveRecord(uid, organizationId, pid, rid);
        var arcHistoricalRecords = await _historicalRecordBusiness.GetAllHistoricalRecords(uid, pid, organizationId);

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
        var historicalRecords = await _historicalRecordBusiness.GetAllHistoricalRecords(uid, pid, organizationId);

        // Assert
        Assert.NotNull(historicalRecords);
        Assert.Empty(historicalRecords);
    }

    [Fact]
    public async Task GetHistoricalRecords_FiltersByDataSource()
    {
        // Act
        var historicalRecords =
            await _historicalRecordBusiness.GetAllHistoricalRecords(uid, pid2, organizationId, did2);

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
            await _historicalRecordBusiness.GetAllHistoricalRecords(uid, pid, organizationId, null, pointInTime, false);

        // Assert
        Assert.NotNull(historicalRecords);
        Assert.Equal(2, historicalRecords.Count());
        Assert.Contains(historicalRecords, x => x.Name == "Test Record");
        Assert.Contains(historicalRecords, x => x.Name == "Test Record 2");
    }

    #endregion

    #region GetHistoricalRecords_SensitivityLabelAuthorization Tests

    [Fact]
    public async Task GetHistoricalRecords_FilterOutUnauthorizedRecordsBySensitivityLabels_ReturnsFilteredRecords()
    {
        // Remove read permission for defaultLabelId2 from the role
        Context.ChangeTracker.Clear();

        var role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        var permissionToRemove = role?.Permissions.FirstOrDefault(p => p.Id == readPermissionId2);
        if (permissionToRemove != null)
        {
            role!.Permissions.Remove(permissionToRemove);
            await Context.SaveChangesAsync();
        }

        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid2, defaultLabelId2);

        // Record with sensitivity label should not be returned because user does not have access
        var records = await _historicalRecordBusiness.GetAllHistoricalRecords(
            uid, pid, organizationId, null, null);

        Assert.NotNull(records);
        Assert.DoesNotContain(records, r => r.Id == rid2);
    }

    [Fact]
    public async Task GetHistoricalRecords_UserHasAccessToAllLabels_ReturnsRecords()
    {
        // User already has read and write permissions for defaultLabelId from seed data
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid2, defaultLabelId);

        // Verify the record IS returned
        var records = await _historicalRecordBusiness.GetAllHistoricalRecords(
            uid, pid, organizationId, null, null);

        Assert.NotNull(records);
        Assert.Contains(records, r => r.Id == rid2);
    }

    [Fact]
    public async Task GetHistoricalRecords_MultipleRecordsMixedAccess_ReturnsOnlyAuthorized()
    {
        // Record 1: No labels (should be returned) - using the seeded record
        var record1Id = rid;

        // Remove read permission for defaultLabelId2 from the role
        Context.ChangeTracker.Clear();

        var role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        var permissionToRemove = role?.Permissions.FirstOrDefault(p => p.Id == readPermissionId2);
        if (permissionToRemove != null)
        {
            role!.Permissions.Remove(permissionToRemove);
            await Context.SaveChangesAsync();
        }

        // Attach labels: user has access to defaultLabelId but not defaultLabelId2
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid, defaultLabelId);
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid2, defaultLabelId2);

        // Act
        var records = await _historicalRecordBusiness.GetAllHistoricalRecords(
            uid, pid, organizationId, null, null);

        // Assert
        Assert.NotNull(records);
        Assert.Contains(records, r => r.Id == record1Id); // User has access
        Assert.DoesNotContain(records, r => r.Id == rid2); // User lacks access
    }

    [Fact]
    public async Task GetHistoricalRecords_RecordWithMultipleLabels_UserHasAll_ReturnsRecord()
    {
        // User already has read and write permissions for both defaultLabelId and defaultLabelId2 from seed data

        // Attach both labels to the record
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid2, defaultLabelId);
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid2, defaultLabelId2);

        // Act
        var records = await _historicalRecordBusiness.GetAllHistoricalRecords(
            uid, pid, organizationId, null, null);

        // Assert
        Assert.NotNull(records);
        Assert.Contains(records, r => r.Id == rid2);
    }

    [Fact]
    public async Task GetHistoricalRecords_RecordWithMultipleLabels_UserMissingOne_FiltersRecord()
    {
        // Remove read permission for defaultLabelId2 from the role
        Context.ChangeTracker.Clear();

        var role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        var permissionToRemove = role?.Permissions.FirstOrDefault(p => p.Id == readPermissionId2);
        if (permissionToRemove != null)
        {
            role!.Permissions.Remove(permissionToRemove);
            await Context.SaveChangesAsync();
        }

        // Attach both labels to the record
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid2, defaultLabelId);
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid2, defaultLabelId2);

        // Act
        var records = await _historicalRecordBusiness.GetAllHistoricalRecords(
            uid, pid, organizationId, null, null);

        // Assert - record should NOT be returned because user lacks access to defaultLabelId2
        Assert.NotNull(records);
        Assert.DoesNotContain(records, r => r.Id == rid2);
    }

    [Fact]
    public async Task GetHistoricalRecords_WithDataSourceFilter_AndLabelAuth_ReturnsBothFiltered()
    {
        // Remove read permission for defaultLabelId2 from the role
        Context.ChangeTracker.Clear();

        var role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        var permissionToRemove = role?.Permissions.FirstOrDefault(p => p.Id == readPermissionId2);
        if (permissionToRemove != null)
        {
            role!.Permissions.Remove(permissionToRemove);
            await Context.SaveChangesAsync();
        }

        // Attach labels to records
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid2, defaultLabelId);
        await _recordBusiness.AttachLabel(uid, organizationId, pid, rid, defaultLabelId2);

        // Act - filter by datasource
        var records = await _historicalRecordBusiness.GetAllHistoricalRecords(
            uid, pid, organizationId, did, null);

        // Assert
        Assert.NotNull(records);
        Assert.Contains(records, r => r.Id == rid2); // Correct datasource, user has access
        Assert.DoesNotContain(records, r => r.Id == rid); // Correct datasource, but no label access
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
        var recordHistory = await _historicalRecordBusiness.GetHistoryForRecord(uid, rid, organizationId);


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
            _historicalRecordBusiness.GetHistoryForRecord(uid, rid + 100000, organizationId));
    }

    #endregion

    #region GetHistoricalRecord Tests

    [Fact]
    public async Task GetHistoricalRecord_ReturnsAllCorrectFields()
    {
        Context.ChangeTracker.Clear();

        // Arrange
        // TODO: insert tags after record to avoid race condition
        var record = await Context.Records
            .AsNoTracking()
            .Where(r => r.ProjectId == pid && r.Id == rid)
            .FirstOrDefaultAsync();
        Assert.NotNull(record);

        Context.ChangeTracker.Clear();

        // Act
        var historicalRecord = await _historicalRecordBusiness.GetHistoricalRecord(uid, rid, organizationId, null);

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
        var historicalRecord = await _historicalRecordBusiness.GetHistoricalRecord(uid, rid, organizationId, null);

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

        var archivedRecord = await _recordBusiness.GetRecord(uid, organizationId, pid, rid, false);
        Assert.NotNull(archivedRecord);

        // Act
        var historicalRecord =
            await _historicalRecordBusiness.GetHistoricalRecord(uid, rid, organizationId, null, false);

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
        var historicalRecord = await _historicalRecordBusiness.GetHistoricalRecord(uid, rid, organizationId, null);

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
        var historicalRecord = await _historicalRecordBusiness.GetHistoricalRecord(uid, rid, organizationId, null);

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
        var historicalRecord =
            await _historicalRecordBusiness.GetHistoricalRecord(uid, rid, organizationId, null, false);

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
                _historicalRecordBusiness.GetHistoricalRecord(uid, rid, organizationId, null));
        Assert.Contains($"Historical record with id {rid} not found or is archived", exception.Message);
    }


    [Fact]
    public async Task GetHistoricalRecord_FiltersByTime()
    {
        // Arrange
        var pointInTime = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        // Ensure temporal separation (prevents same-millisecond issues when tests are run in parallel)
        await Task.Delay(10);

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
        var historicalRecord =
            await _historicalRecordBusiness.GetHistoricalRecord(uid, rid, organizationId, pointInTime);

        // Assert
        Assert.NotNull(historicalRecord);
        Assert.Equal("Test Record", historicalRecord.Name);
    }

    [Fact]
    public async Task GetHistoricalRecord_ThrowsError_WhenRecordDoesNotExist()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _historicalRecordBusiness.GetHistoricalRecord(uid, rid + 100000, organizationId, null));
        Assert.Contains($"Historical record with id {rid + 100000} not found", exception.Message);
    }

    #endregion
}