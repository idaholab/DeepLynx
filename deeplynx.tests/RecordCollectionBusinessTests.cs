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
using Moq;
using Record = deeplynx.datalayer.Models.Record;

namespace deeplynx.tests;

[Collection("Test Suite Collection")]
public class RecordCollectionBusinessTests : IntegrationTestBase
{
    private EventBusiness _eventBusiness = null!;
    private Mock<IHubContext<EventNotificationHub>> _mockHubContext = null!;
    private Mock<ILogger<NotificationBusiness>> _mockNotificationLogger = null!;
    private INotificationBusiness _notificationBusiness = null!;
    private RecordCollectionBusiness _recordCollectionBusiness = null!;
    private SensitivityLabelBusiness _sensitivityLabelBusiness = null!;
    private SensitivityLabelService _sensitivityLabelService = null!;
    private TagBusiness _tagBusiness = null!;
    private BulkCopyUpsertExecutor _bulkCopyUpsertExecutor = null!;

    private long _archivedCollectionId;
    private long _archivedRecordId;
    private long _collectionId;
    private long _labelId;
    private long _labelId2;
    private long _organizationId;
    private long _projectId;
    private long _projectId2;
    private long _recordId1;
    private long _recordId2;
    private long _tagId1;
    private long _tagId2;
    private long _userId;

    public RecordCollectionBusinessTests(TestSuiteFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        _mockHubContext = new Mock<IHubContext<EventNotificationHub>>();
        _mockNotificationLogger = new Mock<ILogger<NotificationBusiness>>();
        _notificationBusiness =
            new NotificationBusiness(Context, _mockNotificationLogger.Object, _mockHubContext.Object);
        _bulkCopyUpsertExecutor = new BulkCopyUpsertExecutor();
        _eventBusiness = new EventBusiness(Context, _notificationBusiness, _bulkCopyUpsertExecutor);
        _tagBusiness = new TagBusiness(Context, _eventBusiness);
        _sensitivityLabelBusiness = new SensitivityLabelBusiness(Context, _eventBusiness);
        _sensitivityLabelService = new SensitivityLabelService(Context);
        _recordCollectionBusiness = new RecordCollectionBusiness(
            Context,
            _eventBusiness,
            _bulkCopyUpsertExecutor,
            _tagBusiness,
            _sensitivityLabelBusiness,
            _sensitivityLabelService);
    }

    protected override async Task SeedTestDataAsync()
    {
        await base.SeedTestDataAsync();

        var user = new User
        {
            Name = "Record Collection User",
            Email = "record.collection@test.com",
            Password = "test_password",
            IsArchived = false
        };
        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        _userId = user.Id;

        var organization = new Organization
        {
            Name = "Record Collection Org",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = _userId,
            RequireSensitivityLabel = false
        };
        Context.Organizations.Add(organization);
        await Context.SaveChangesAsync();
        _organizationId = organization.Id;

        var project = new Project
        {
            Name = "Primary Project",
            OrganizationId = _organizationId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = _userId,
            RequireSensitivityLabel = false
        };
        var project2 = new Project
        {
            Name = "Secondary Project",
            OrganizationId = _organizationId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = _userId,
            RequireSensitivityLabel = false
        };
        Context.Projects.AddRange(project, project2);
        await Context.SaveChangesAsync();
        _projectId = project.Id;
        _projectId2 = project2.Id;

        var dataSource = new DataSource
        {
            Name = "Primary Data Source",
            ProjectId = _projectId,
            OrganizationId = _organizationId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = _userId
        };
        var testClass = new Class
        {
            Name = "Primary Class",
            ProjectId = _projectId,
            OrganizationId = _organizationId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = _userId
        };
        Context.DataSources.Add(dataSource);
        Context.Classes.Add(testClass);
        await Context.SaveChangesAsync();

        var tag1 = new Tag
        {
            Name = "tag-one",
            ProjectId = _projectId,
            OrganizationId = _organizationId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = _userId
        };
        var tag2 = new Tag
        {
            Name = "tag-two",
            ProjectId = _projectId,
            OrganizationId = _organizationId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = _userId
        };
        var label1 = new SensitivityLabel
        {
            Name = "label-one",
            OrganizationId = _organizationId,
            ProjectId = _projectId,
            IsArchived = false
        };
        var label2 = new SensitivityLabel
        {
            Name = "label-two",
            OrganizationId = _organizationId,
            ProjectId = _projectId,
            IsArchived = false
        };
        Context.Tags.AddRange(tag1, tag2);
        Context.SensitivityLabels.AddRange(label1, label2);
        await Context.SaveChangesAsync();
        _tagId1 = tag1.Id;
        _tagId2 = tag2.Id;
        _labelId = label1.Id;
        _labelId2 = label2.Id;

        var record1 = new Record
        {
            Name = "record-one",
            Description = "first record",
            OriginalId = "og-1",
            Properties = JsonSerializer.Serialize(new { order = 1 }),
            ProjectId = _projectId,
            OrganizationId = _organizationId,
            DataSourceId = dataSource.Id,
            ClassId = testClass.Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = _userId,
            IsArchived = false
        };
        var record2 = new Record
        {
            Name = "record-two",
            Description = "second record",
            OriginalId = "og-2",
            Properties = JsonSerializer.Serialize(new { order = 2 }),
            ProjectId = _projectId,
            OrganizationId = _organizationId,
            DataSourceId = dataSource.Id,
            ClassId = testClass.Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = _userId,
            IsArchived = false
        };
        var archivedRecord = new Record
        {
            Name = "record-archived",
            Description = "archived record",
            OriginalId = "og-3",
            Properties = JsonSerializer.Serialize(new { order = 3 }),
            ProjectId = _projectId,
            OrganizationId = _organizationId,
            DataSourceId = dataSource.Id,
            ClassId = testClass.Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = _userId,
            IsArchived = true
        };
        Context.Records.AddRange(record1, record2, archivedRecord);
        await Context.SaveChangesAsync();
        _recordId1 = record1.Id;
        _recordId2 = record2.Id;
        _archivedRecordId = archivedRecord.Id;

        var collection = new RecordCollection
        {
            Name = "Active Collection",
            Description = "active collection",
            Properties = JsonSerializer.Serialize(new { source = "seed" }),
            ProjectId = _projectId,
            OrganizationId = _organizationId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = _userId,
            IsArchived = false,
            Tags = new List<Tag> { tag1 },
            Labels = new List<SensitivityLabel> { label1 },
            Records = new List<Record> { record1, archivedRecord }
        };
        var archivedCollection = new RecordCollection
        {
            Name = "Archived Collection",
            Description = "archived collection",
            Properties = JsonSerializer.Serialize(new { source = "archived" }),
            ProjectId = _projectId,
            OrganizationId = _organizationId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = _userId,
            IsArchived = true,
            Tags = new List<Tag> { tag2 },
            Labels = new List<SensitivityLabel> { label2 }
        };
        Context.RecordCollections.AddRange(collection, archivedCollection);
        await Context.SaveChangesAsync();
        _collectionId = collection.Id;
        _archivedCollectionId = archivedCollection.Id;
    }

    [Fact]
    public async Task GetAllRecordCollections_HideArchived_ReturnsOnlyActiveCollections()
    {
        var result = await _recordCollectionBusiness.GetAllRecordCollections(
            _userId, _organizationId, _projectId, true, isSysAdmin: true);

        var collection = Assert.Single(result);
        Assert.Equal(_collectionId, collection.Id);
        Assert.False(collection.IsArchived);
        Assert.Single(collection.Tags);
        Assert.Equal(_tagId1, collection.Tags.First().Id);
        Assert.Single(collection.Labels);
        Assert.Equal(_labelId, collection.Labels.First().Id);
    }

    [Fact]
    public async Task GetRecordsInRecordCollection_HideArchived_ExcludesArchivedRecords()
    {
        var result = await _recordCollectionBusiness.GetRecordsInRecordCollection(
            _userId, _organizationId, _projectId, _collectionId, true, isSysAdmin: true);

        var record = Assert.Single(result);
        Assert.Equal(_recordId1, record.Id);
        Assert.False(record.IsArchived);
    }

    [Fact]
    public async Task GetRecordsInRecordCollection_ArchivedCollection_ThrowsKeyNotFound()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordCollectionBusiness.GetRecordsInRecordCollection(
                _userId, _organizationId, _projectId, _archivedCollectionId, true, isSysAdmin: true));
    }

    [Fact]
    public async Task GetRecordCollectionsByTags_ReturnsCollectionsContainingAllTags()
    {
        await _recordCollectionBusiness.AttachTag(_userId, _organizationId, _projectId, _collectionId, _tagId2);

        var result = await _recordCollectionBusiness.GetRecordCollectionsByTags(
            _userId, _organizationId, _projectId, new[] { _tagId1, _tagId2 }, true, isSysAdmin: true);

        var collection = Assert.Single(result);
        Assert.Equal(_collectionId, collection.Id);
    }

    [Fact]
    public async Task AddRecordsToRecordCollection_AddsDistinctRecords_AndSkipsExistingOnes()
    {
        var result = await _recordCollectionBusiness.AddRecordsToRecordCollection(
            _userId, _organizationId, _projectId, _collectionId, new List<long> { _recordId1, _recordId2, _recordId2 },
            isSysAdmin: true);

        Assert.True(result);

        var collection = await Context.RecordCollections
            .Include(c => c.Records)
            .FirstAsync(c => c.Id == _collectionId);

        Assert.Contains(collection.Records, r => r.Id == _recordId1);
        Assert.Contains(collection.Records, r => r.Id == _recordId2);
        Assert.Equal(2, collection.Records.Count(r => !r.IsArchived));
    }

    [Fact]
    public async Task RemoveRecordsFromRecordCollection_RemovesRequestedRecords()
    {
        await _recordCollectionBusiness.AddRecordsToRecordCollection(
            _userId, _organizationId, _projectId, _collectionId, new List<long> { _recordId2 }, isSysAdmin: true);

        var result = await _recordCollectionBusiness.RemoveRecordsFromRecordCollection(
            _userId, _organizationId, _projectId, _collectionId, new List<long> { _recordId2, _recordId2 },
            isSysAdmin: true);

        Assert.True(result);

        var collection = await Context.RecordCollections
            .Include(c => c.Records)
            .FirstAsync(c => c.Id == _collectionId);

        Assert.DoesNotContain(collection.Records, r => r.Id == _recordId2);
        Assert.Contains(collection.Records, r => r.Id == _recordId1);
    }

    [Fact]
    public async Task CreateRecordCollection_CreatesCollection_AndDeduplicatesTags()
    {
        var dto = new CreateRecordCollectionRequestDto
        {
            Name = "Created Collection",
            Description = "created for test",
            Properties = new JsonObject { ["status"] = "new" },
            Tags = new List<string> { "alpha", "alpha", "beta" }
        };

        var result = await _recordCollectionBusiness.CreateRecordCollection(
            _userId, _organizationId, _projectId, dto);

        Assert.Equal("Created Collection", result.Name);
        Assert.Equal(2, result.Tags.Count);
        Assert.Contains(result.Tags, t => t.Name == "alpha");
        Assert.Contains(result.Tags, t => t.Name == "beta");

        var persisted = await Context.RecordCollections
            .Include(c => c.Tags)
            .FirstAsync(c => c.Id == result.Id);

        Assert.Equal(2, persisted.Tags.Count);
    }

    [Fact]
    public async Task UpdateRecordCollection_UpdatesMutableFields()
    {
        var dto = new UpdateRecordCollectionRequestDto
        {
            Name = "Updated Collection",
            Description = "updated description",
            Properties = new JsonObject { ["status"] = "updated" }
        };

        var result = await _recordCollectionBusiness.UpdateRecordCollection(
            _userId, _organizationId, _projectId, _collectionId, dto);

        Assert.Equal(_collectionId, result.Id);
        Assert.Equal("Updated Collection", result.Name);
        Assert.Equal("updated description", result.Description);
        Assert.Equal("updated", JsonNode.Parse(result.Properties)?["status"]?.GetValue<string>());
        Assert.Equal(_userId, result.LastUpdatedBy);
    }

    [Fact]
    public async Task AttachTag_AlreadyAttached_ThrowsInvalidOperation()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _recordCollectionBusiness.AttachTag(_userId, _organizationId, _projectId, _collectionId, _tagId1));
    }

    [Fact]
    public async Task UnattachLabel_WhenProjectRequiresLabel_AndOnlyOneLabelExists_ThrowsInvalidOperation()
    {
        var project = await Context.Projects.FirstAsync(p => p.Id == _projectId);
        project.RequireSensitivityLabel = true;
        await Context.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _recordCollectionBusiness.UnattachLabel(_userId, _organizationId, _projectId, _collectionId, _labelId));
    }
}
