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
    private UserBusiness _userBusiness = null!;
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
        _userBusiness = new UserBusiness(Context);
        _sensitivityLabelBusiness = new SensitivityLabelBusiness(Context, _eventBusiness, _userBusiness);
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

    private async Task<RecordCollection> CreateRecordCollectionAsync(
        string name,
        string description,
        DateTime? lastUpdatedAt = null,
        IEnumerable<long>? tagIds = null,
        IEnumerable<long>? labelIds = null,
        IEnumerable<long>? recordIds = null,
        bool isArchived = false)
    {
        var tags = tagIds?.Any() == true
            ? await Context.Tags.Where(t => tagIds.Contains(t.Id)).ToListAsync()
            : new List<Tag>();
        var labels = labelIds?.Any() == true
            ? await Context.SensitivityLabels.Where(l => labelIds.Contains(l.Id)).ToListAsync()
            : new List<SensitivityLabel>();
        var records = recordIds?.Any() == true
            ? await Context.Records.Where(r => recordIds.Contains(r.Id)).ToListAsync()
            : new List<Record>();

        var collection = new RecordCollection
        {
            Name = name,
            Description = description,
            Properties = JsonSerializer.Serialize(new { source = "test" }),
            ProjectId = _projectId,
            OrganizationId = _organizationId,
            LastUpdatedAt = lastUpdatedAt ?? DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = _userId,
            IsArchived = isArchived,
            Tags = tags,
            Labels = labels,
            Records = records
        };

        Context.RecordCollections.Add(collection);
        await Context.SaveChangesAsync();
        return collection;
    }
    
    #region Get all Tests
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-500)]
    public void GetValidatedPageSize_ReturnsDefault_WhenPageSizeIsNonPositive(int pageSize)
    {
        var dto = new RecordCollectionQueryRequestDto { PageSize = pageSize };

        var result = dto.GetValidatedPageSize();

        Assert.Equal(25, result);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(500)]
    public void GetValidatedPageSize_ReturnsConfiguredValue_WhenWithinBounds(int pageSize)
    {
        var dto = new RecordCollectionQueryRequestDto { PageSize = pageSize };

        var result = dto.GetValidatedPageSize();

        Assert.Equal(pageSize, result);
    }

    [Theory]
    [InlineData(501)]
    [InlineData(1000)]
    public void GetValidatedPageSize_ClampsToMaximum_WhenPageSizeExceedsLimit(int pageSize)
    {
        var dto = new RecordCollectionQueryRequestDto { PageSize = pageSize };

        var result = dto.GetValidatedPageSize();

        Assert.Equal(500, result);
    }

    [Fact]
    public async Task GetAllRecordCollections_HideArchived_ReturnsOnlyActiveCollections()
    {
        var queryDto = new RecordCollectionQueryRequestDto();
        var result = await _recordCollectionBusiness.GetAllRecordCollections(
            _userId, _organizationId, _projectId, queryDto, true, isSysAdmin: true);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(1, result.PageNumber);
        Assert.Equal(queryDto.GetValidatedPageSize(), result.PageSize);
        var collection = Assert.Single(result.Items);
        Assert.Equal(_collectionId, collection.Id);
        Assert.False(collection.IsArchived);
        Assert.Single(collection.Tags);
        Assert.Equal(_tagId1, collection.Tags.First().Id);
        Assert.Single(collection.Labels);
        Assert.Equal(_labelId, collection.Labels.First().Id);
    }
    
    [Fact]
    public async Task GetAllRecordCollections_NonAdmin_FiltersOutUnauthorizedLabeledCollections()
    {
        // Create a collection with no labels that the user should be able to see
        var accessibleCollection = new RecordCollection
        {
            Name = "Accessible Collection",
            Description = "user can see this",
            Properties = JsonSerializer.Serialize(new { source = "accessible" }),
            ProjectId = _projectId,
            OrganizationId = _organizationId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = _userId,
            IsArchived = false,
            Labels = new List<SensitivityLabel>()
        };
        // Create a collection with label2 which the user has no permissions for
        var restrictedCollection = new RecordCollection
        {
            Name = "Restricted Collection",
            Description = "user cannot see this",
            Properties = JsonSerializer.Serialize(new { source = "restricted" }),
            ProjectId = _projectId,
            OrganizationId = _organizationId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = _userId,
            IsArchived = false,
            Labels = new List<SensitivityLabel>
            {
                await Context.SensitivityLabels.FirstAsync(l => l.Id == _labelId2)
            }
        };
        Context.RecordCollections.AddRange(accessibleCollection, restrictedCollection);
        await Context.SaveChangesAsync();

        var queryDto = new RecordCollectionQueryRequestDto();
        var result = await _recordCollectionBusiness.GetAllRecordCollections(
            _userId, _organizationId, _projectId, queryDto, hideArchived: true);

        Assert.NotEmpty(result.Items);
        Assert.Contains(result.Items, c => c.Id == accessibleCollection.Id);
        Assert.DoesNotContain(result.Items, c => c.Id == restrictedCollection.Id);
    }

    [Fact]
    public async Task GetAllRecordCollections_Search_MatchesRelatedTagAndLabelNames()
    {
        var matchedCollection = await CreateRecordCollectionAsync(
            "Tagged Collection",
            "search target",
            tagIds: new[] { _tagId2 },
            labelIds: new[] { _labelId2 });
        await CreateRecordCollectionAsync(
            "Non Matching Collection",
            "control group",
            tagIds: new[] { _tagId1 },
            labelIds: new[] { _labelId });

        var tagQuery = new RecordCollectionQueryRequestDto { Search = "tag-two" };
        var tagResult = await _recordCollectionBusiness.GetAllRecordCollections(
            _userId, _organizationId, _projectId, tagQuery, true, isSysAdmin: true);

        Assert.Contains(tagResult.Items, c => c.Id == matchedCollection.Id);
        Assert.DoesNotContain(tagResult.Items, c => c.Id == _collectionId);

        var labelQuery = new RecordCollectionQueryRequestDto { Search = "label-two" };
        var labelResult = await _recordCollectionBusiness.GetAllRecordCollections(
            _userId, _organizationId, _projectId, labelQuery, true, isSysAdmin: true);

        Assert.Contains(labelResult.Items, c => c.Id == matchedCollection.Id);
        Assert.DoesNotContain(labelResult.Items, c => c.Id == _collectionId);
    }

    [Fact]
    public async Task GetAllRecordCollections_FilterBySensitivityLabelIds_ReturnsCollectionsWithAllSelectedLabels()
    {
        var bothLabelsCollection = await CreateRecordCollectionAsync(
            "Both Labels",
            "matches all labels",
            labelIds: new[] { _labelId, _labelId2 });
        await CreateRecordCollectionAsync(
            "Only Second Label",
            "missing first label",
            labelIds: new[] { _labelId2 });

        var queryDto = new RecordCollectionQueryRequestDto
        {
            SensitivityLabelIds = new[] { _labelId, _labelId2 }
        };

        var result = await _recordCollectionBusiness.GetAllRecordCollections(
            _userId, _organizationId, _projectId, queryDto, true, isSysAdmin: true);

        var collection = Assert.Single(result.Items);
        Assert.Equal(bothLabelsCollection.Id, collection.Id);
    }

    [Fact]
    public async Task GetAllRecordCollections_FilterByTagIds_ReturnsCollectionsWithAllSelectedTags()
    {
        var bothTagsCollection = await CreateRecordCollectionAsync(
            "Both Tags",
            "matches all tags",
            tagIds: new[] { _tagId1, _tagId2 });
        await CreateRecordCollectionAsync(
            "Only Second Tag",
            "missing first tag",
            tagIds: new[] { _tagId2 });

        var queryDto = new RecordCollectionQueryRequestDto
        {
            TagIds = new[] { _tagId1, _tagId2 }
        };

        var result = await _recordCollectionBusiness.GetAllRecordCollections(
            _userId, _organizationId, _projectId, queryDto, true, isSysAdmin: true);

        var collection = Assert.Single(result.Items);
        Assert.Equal(bothTagsCollection.Id, collection.Id);
    }

    [Fact]
    public async Task GetAllRecordCollections_SortAndPaginate_ReturnsRequestedSlice()
    {
        var timestamp = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        await CreateRecordCollectionAsync("alpha-page", "page-set", timestamp.AddMinutes(-3));
        await CreateRecordCollectionAsync("bravo-page", "page-set", timestamp.AddMinutes(-2));
        var charlieCollection = await CreateRecordCollectionAsync(
            "charlie-page",
            "page-set",
            timestamp.AddMinutes(-1));

        var queryDto = new RecordCollectionQueryRequestDto
        {
            Search = "page-set",
            Sort = "alphabeticalAsc",
            PageNumber = 2,
            PageSize = 2
        };

        var result = await _recordCollectionBusiness.GetAllRecordCollections(
            _userId, _organizationId, _projectId, queryDto, true, isSysAdmin: true);

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.PageNumber);
        Assert.Equal(2, result.PageSize);
        var collection = Assert.Single(result.Items);
        Assert.Equal(charlieCollection.Id, collection.Id);
        Assert.Equal("charlie-page", collection.Name);
    }
    
    #endregion
    
    #region Get Records In Collections Tests
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
    
    #endregion

    #region Get Collection By Tags
    [Fact]
    public async Task GetRecordCollectionsByTags_ReturnsCollectionsContainingAllTags()
    {
        await _recordCollectionBusiness.AttachTag(_organizationId, _projectId, _collectionId, _tagId2);

        var result = await _recordCollectionBusiness.GetRecordCollectionsByTags(
            _userId, _organizationId, _projectId, new[] { _tagId1, _tagId2 }, true, isSysAdmin: true);

        var collection = Assert.Single(result);
        Assert.Equal(_collectionId, collection.Id);
    }
    
    #endregion
    
    #region Add Records to Collections

    [Fact]
    public async Task AddRecordsToRecordCollection_AddsDistinctRecords_AndSkipsExistingOnes()
    {
        var result = await _recordCollectionBusiness.AddRecordsToRecordCollection(
            _userId, _organizationId, _projectId, _collectionId, new long[] { _recordId1, _recordId2, _recordId2 },
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
    public async Task AddRecordsToRecordCollection_RecordLabels_AreAddedToCollection()
    {
        // Attach label2 to record2 so it differs from the collection's existing label1
        var record2 = await Context.Records.Include(r => r.Labels).FirstAsync(r => r.Id == _recordId2);
        var label2 = await Context.SensitivityLabels.FirstAsync(l => l.Id == _labelId2);
        record2.Labels.Add(label2);
        await Context.SaveChangesAsync();

        var result = await _recordCollectionBusiness.AddRecordsToRecordCollection(
            _userId, _organizationId, _projectId, _collectionId, new long[] { _recordId2 },
            isSysAdmin: true);

        Assert.True(result);

        var collection = await Context.RecordCollections
            .Include(c => c.Labels)
            .FirstAsync(c => c.Id == _collectionId);

        Assert.Contains(collection.Labels, l => l.Id == _labelId2);
    }

    [Fact]
    public async Task AddRecordsToRecordCollection_DuplicateLabels_AreNotAddedTwice()
    {
        // record2 gets label1 which already exists on the collection
        var record2 = await Context.Records.Include(r => r.Labels).FirstAsync(r => r.Id == _recordId2);
        var label1 = await Context.SensitivityLabels.FirstAsync(l => l.Id == _labelId);
        record2.Labels.Add(label1);
        await Context.SaveChangesAsync();

        await _recordCollectionBusiness.AddRecordsToRecordCollection(
            _userId, _organizationId, _projectId, _collectionId, new long[] { _recordId2 },
            isSysAdmin: true);

        var collection = await Context.RecordCollections
            .Include(c => c.Labels)
            .FirstAsync(c => c.Id == _collectionId);

        Assert.Equal(1, collection.Labels.Count(l => l.Id == _labelId));
    }

    [Fact]
    public async Task AddRecordsToRecordCollection_ArchivedRecord_ThrowsKeyNotFound()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordCollectionBusiness.AddRecordsToRecordCollection(
                _userId, _organizationId, _projectId, _collectionId, new long[] { _archivedRecordId },
                isSysAdmin: true));
    }

    [Fact]
    public async Task AddRecordsToRecordCollection_RecordFromDifferentProject_ThrowsKeyNotFound()
    {
        // record seeded under _projectId2 won't be found when querying under _projectId
        var dataSource = await Context.DataSources.FirstAsync(d => d.ProjectId == _projectId);
        var testClass = await Context.Classes.FirstAsync(c => c.ProjectId == _projectId);
        var otherProjectRecord = new Record
        {
            Name = "other-project-record",
            OriginalId = "og-other",
            Properties = "{}",
            Description = "other-project-record",
            ProjectId = _projectId2,
            OrganizationId = _organizationId,
            DataSourceId = dataSource.Id,
            ClassId = testClass.Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = _userId
        };
        Context.Records.Add(otherProjectRecord);
        await Context.SaveChangesAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordCollectionBusiness.AddRecordsToRecordCollection(
                _userId, _organizationId, _projectId, _collectionId, new long[] { otherProjectRecord.Id },
                isSysAdmin: true));
    }

    [Fact]
    public async Task AddRecordsToRecordCollection_EmptyRecordIds_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recordCollectionBusiness.AddRecordsToRecordCollection(
                _userId, _organizationId, _projectId, _collectionId, Array.Empty<long>(),
                isSysAdmin: true));
    }

    [Fact]
    public async Task AddRecordsToRecordCollection_ArchivedCollection_ThrowsKeyNotFound()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordCollectionBusiness.AddRecordsToRecordCollection(
                _userId, _organizationId, _projectId, _archivedCollectionId, new long[] { _recordId2 },
                isSysAdmin: true));
    }

    [Fact]
    public async Task AddRecordsToRecordCollection_NonAdmin_UnauthorizedRecord_ThrowsUnauthorizedAccess()
    {
        // record2 has label2 but the user has no permissions for label2
        var record2 = await Context.Records.Include(r => r.Labels).FirstAsync(r => r.Id == _recordId2);
        var label2 = await Context.SensitivityLabels.FirstAsync(l => l.Id == _labelId2);
        record2.Labels.Add(label2);
        await Context.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _recordCollectionBusiness.AddRecordsToRecordCollection(
                _userId, _organizationId, _projectId, _collectionId, new long[] { _recordId2 }));
    }

    [Fact]
    public async Task AddRecordsToRecordCollection_NonAdmin_AuthorizedRecord_Succeeds()
    {
        // record2 has no labels so any user can access it
        var result = await _recordCollectionBusiness.AddRecordsToRecordCollection(
            _userId, _organizationId, _projectId, _collectionId, new long[] { _recordId2 });

        Assert.True(result);

        var collection = await Context.RecordCollections
            .Include(c => c.Records)
            .FirstAsync(c => c.Id == _collectionId);

        Assert.Contains(collection.Records, r => r.Id == _recordId2);
    }
    
    #endregion
    
    #region Remove Record
    [Fact]
    public async Task RemoveRecordsFromRecordCollection_RemovesRequestedRecords()
    {
        await _recordCollectionBusiness.AddRecordsToRecordCollection(
            _userId, _organizationId, _projectId, _collectionId, new long[] { _recordId2 }, isSysAdmin: true);

        var result = await _recordCollectionBusiness.RemoveRecordsFromRecordCollection(
            _userId, _organizationId, _projectId, _collectionId, new long[] { _recordId2, _recordId2 },
            isSysAdmin: true);

        Assert.True(result);

        var collection = await Context.RecordCollections
            .Include(c => c.Records)
            .FirstAsync(c => c.Id == _collectionId);

        Assert.DoesNotContain(collection.Records, r => r.Id == _recordId2);
        Assert.Contains(collection.Records, r => r.Id == _recordId1);
    }

    [Fact]
    public async Task RemoveRecordsFromRecordCollection_RecordNotInCollection_ThrowsKeyNotFound()
    {
        // record2 has never been added to the collection
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordCollectionBusiness.RemoveRecordsFromRecordCollection(
                _userId, _organizationId, _projectId, _collectionId, new long[] { _recordId2 },
                isSysAdmin: true));
    }

    [Fact]
    public async Task RemoveRecordsFromRecordCollection_EmptyRecordIds_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _recordCollectionBusiness.RemoveRecordsFromRecordCollection(
                _userId, _organizationId, _projectId, _collectionId, Array.Empty<long>(),
                isSysAdmin: true));
    }

    [Fact]
    public async Task RemoveRecordsFromRecordCollection_ArchivedCollection_ThrowsKeyNotFound()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordCollectionBusiness.RemoveRecordsFromRecordCollection(
                _userId, _organizationId, _projectId, _archivedCollectionId, new long[] { _recordId1 },
                isSysAdmin: true));
    }

    [Fact]
    public async Task RemoveRecordsFromRecordCollection_PartialMatch_ThrowsKeyNotFound()
    {
        // record1 is in the collection but record2 is not — the whole operation should fail
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordCollectionBusiness.RemoveRecordsFromRecordCollection(
                _userId, _organizationId, _projectId, _collectionId, new long[] { _recordId1, _recordId2 },
                isSysAdmin: true));

        // record1 should still be in the collection since the operation failed
        var collection = await Context.RecordCollections
            .Include(c => c.Records)
            .FirstAsync(c => c.Id == _collectionId);

        Assert.Contains(collection.Records, r => r.Id == _recordId1);
    }

    [Fact]
    public async Task RemoveRecordsFromRecordCollection_NonAdmin_UnauthorizedRecord_ThrowsUnauthorizedAccess()
    {
        // give record1 a label the user has no permissions for
        var record1 = await Context.Records.Include(r => r.Labels).FirstAsync(r => r.Id == _recordId1);
        var label2 = await Context.SensitivityLabels.FirstAsync(l => l.Id == _labelId2);
        record1.Labels.Add(label2);
        await Context.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _recordCollectionBusiness.RemoveRecordsFromRecordCollection(
                _userId, _organizationId, _projectId, _collectionId, new long[] { _recordId1 }));
    }

    [Fact]
    public async Task RemoveRecordsFromRecordCollection_NonAdmin_AuthorizedRecord_Succeeds()
    {
        // record1 has no labels so any user can access and remove it
        var result = await _recordCollectionBusiness.RemoveRecordsFromRecordCollection(
            _userId, _organizationId, _projectId, _collectionId, new long[] { _recordId1 });

        Assert.True(result);

        var collection = await Context.RecordCollections
            .Include(c => c.Records)
            .FirstAsync(c => c.Id == _collectionId);

        Assert.DoesNotContain(collection.Records, r => r.Id == _recordId1);
    }
    
    #endregion
    
    #region Create Collection
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

        var result = await _recordCollectionBusiness.CreateRecordCollection(_userId, _organizationId, _projectId, null, dto);

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
    public async Task CreateRecordCollection_WithValidLabels_AttachesLabels()
    {
        var dto = new CreateRecordCollectionRequestDto
        {
            Name = "Labeled Collection",
            Description = "collection with labels",
            Properties = new JsonObject { ["status"] = "new" },
            Tags = null
        };

        var result = await _recordCollectionBusiness.CreateRecordCollection(
            _userId, _organizationId, _projectId, new List<long> { _labelId, _labelId2 }, dto);

        Assert.Equal(2, result.Labels.Count);
        Assert.Contains(result.Labels, l => l.Id == _labelId);
        Assert.Contains(result.Labels, l => l.Id == _labelId2);

        var persisted = await Context.RecordCollections
            .Include(c => c.Labels)
            .FirstAsync(c => c.Id == result.Id);

        Assert.Equal(2, persisted.Labels.Count);
    }

    [Fact]
    public async Task CreateRecordCollection_WithLabelFromDifferentProject_ThrowsKeyNotFound()
    {
        var otherLabel = new SensitivityLabel
        {
            Name = "other-project-label",
            OrganizationId = _organizationId,
            ProjectId = _projectId2,
            IsArchived = false
        };
        Context.SensitivityLabels.Add(otherLabel);
        await Context.SaveChangesAsync();

        var dto = new CreateRecordCollectionRequestDto
        {
            Name = "Bad Label Collection",
            Description = "should fail",
            Properties = new JsonObject { ["status"] = "new" },
            Tags = null
        };

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordCollectionBusiness.CreateRecordCollection(
                _userId, _organizationId, _projectId, new List<long> { otherLabel.Id }, dto));
    }

    [Fact]
    public async Task CreateRecordCollection_PropertiesExceedMaxDepth_ThrowsException()
    {
        var dto = new CreateRecordCollectionRequestDto
        {
            Name = "Deep Collection",
            Description = "too deep",
            Properties = new JsonObject
            {
                ["level1"] = new JsonObject
                {
                    ["level2"] = new JsonObject
                    {
                        ["level3"] = new JsonObject
                        {
                            ["level4"] = "too deep"
                        }
                    }
                }
            },
            Tags = null
        };

        await Assert.ThrowsAsync<Exception>(() =>
            _recordCollectionBusiness.CreateRecordCollection(
                _userId, _organizationId, _projectId, null, dto));
    }

    [Fact]
    public async Task CreateRecordCollection_NullLabels_CreatesWithNoLabels()
    {
        var dto = new CreateRecordCollectionRequestDto
        {
            Name = "No Label Collection",
            Description = "no labels",
            Properties = new JsonObject { ["status"] = "new" },
            Tags = null
        };

        var result = await _recordCollectionBusiness.CreateRecordCollection(
            _userId, _organizationId, _projectId, null, dto);

        Assert.Equal("No Label Collection", result.Name);
        Assert.Empty(result.Labels);
    }
    #endregion
    
    #region Update Collection
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
    
    #endregion
    
    #region Attach/Unattach Tags
    [Fact]
    public async Task AttachTag_ValidTag_AttachesSuccessfully()
    {
        // tag2 is not yet on the collection
        var result = await _recordCollectionBusiness.AttachTag(
            _organizationId, _projectId, _collectionId, _tagId2);

        Assert.True(result);

        var collection = await Context.RecordCollections
            .Include(c => c.Tags)
            .FirstAsync(c => c.Id == _collectionId);

        Assert.Contains(collection.Tags, t => t.Id == _tagId2);
    }

    [Fact]
    public async Task AttachTag_AlreadyAttached_ThrowsInvalidOperation()
    {
        // tag1 is already on the collection from seed data
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _recordCollectionBusiness.AttachTag(
                _organizationId, _projectId, _collectionId, _tagId1));
    }

    [Fact]
    public async Task AttachTag_ArchivedCollection_ThrowsKeyNotFound()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordCollectionBusiness.AttachTag(
                _organizationId, _projectId, _archivedCollectionId, _tagId1));
    }

    [Fact]
    public async Task AttachTag_TagFromDifferentProject_ThrowsKeyNotFound()
    {
        var otherProjectTag = new Tag
        {
            Name = "other-project-tag",
            ProjectId = _projectId2,
            OrganizationId = _organizationId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = _userId
        };
        Context.Tags.Add(otherProjectTag);
        await Context.SaveChangesAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordCollectionBusiness.AttachTag(
                _organizationId, _projectId, _collectionId, otherProjectTag.Id));
    }

    [Fact]
    public async Task AttachTag_NotFound_ThrowsKeyNotFound()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordCollectionBusiness.AttachTag(
                _organizationId, _projectId, _collectionId, long.MaxValue));
    }

    [Fact]
    public async Task UnattachTag_ValidTag_RemovesSuccessfully()
    {
        // tag1 is on the collection from seed data
        var result = await _recordCollectionBusiness.UnattachTag(
            _organizationId, _projectId, _collectionId, _tagId1);

        Assert.True(result);

        var collection = await Context.RecordCollections
            .Include(c => c.Tags)
            .FirstAsync(c => c.Id == _collectionId);

        Assert.DoesNotContain(collection.Tags, t => t.Id == _tagId1);
    }

    [Fact]
    public async Task UnattachTag_TagNotOnCollection_ThrowsKeyNotFound()
    {
        // tag2 was never attached to the collection
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordCollectionBusiness.UnattachTag(
                _organizationId, _projectId, _collectionId, _tagId2));
    }

    [Fact]
    public async Task UnattachTag_ArchivedCollection_ThrowsKeyNotFound()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordCollectionBusiness.UnattachTag(
                _organizationId, _projectId, _archivedCollectionId, _tagId2));
    }

    [Fact]
    public async Task UnattachTag_NotFound_ThrowsKeyNotFound()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordCollectionBusiness.UnattachTag(
                _organizationId, _projectId, _collectionId, long.MaxValue));
    }
    
    #endregion
    
    #region Attach/Unattach Labels

    [Fact]
    public async Task AttachLabel_AlreadyAttached_ThrowsInvalidOperation()
    {
        // label1 is already on the collection from seed data
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _recordCollectionBusiness.AttachLabel(
                _organizationId, _projectId, _collectionId, _labelId));
    }

    [Fact]
    public async Task AttachLabel_LabelFromDifferentProject_ThrowsKeyNotFound()
    {
        var otherLabel = new SensitivityLabel
        {
            Name = "other-project-label",
            OrganizationId = _organizationId,
            ProjectId = _projectId2,
            IsArchived = false
        };
        Context.SensitivityLabels.Add(otherLabel);
        await Context.SaveChangesAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordCollectionBusiness.AttachLabel(
                _organizationId, _projectId, _collectionId, otherLabel.Id));
    }

    [Fact]
    public async Task AttachLabel_ValidLabel_AttachesSuccessfully()
    {
        // label2 is not yet on the collection
        var result = await _recordCollectionBusiness.AttachLabel(
            _organizationId, _projectId, _collectionId, _labelId2);

        Assert.True(result);

        var collection = await Context.RecordCollections
            .Include(c => c.Labels)
            .FirstAsync(c => c.Id == _collectionId);

        Assert.Contains(collection.Labels, l => l.Id == _labelId2);
    }

    [Fact]
    public async Task UnattachLabel_LabelNotOnCollection_ThrowsKeyNotFound()
    {
        // label2 was never attached to the collection
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordCollectionBusiness.UnattachLabel(
                _organizationId, _projectId, _collectionId, _labelId2));
    }

    [Fact]
    public async Task UnattachLabel_ValidLabel_RemovesSuccessfully()
    {
        // attach label2 first so we can remove it
        await _recordCollectionBusiness.AttachLabel(
            _organizationId, _projectId, _collectionId, _labelId2);

        var result = await _recordCollectionBusiness.UnattachLabel(
            _organizationId, _projectId, _collectionId, _labelId2);

        Assert.True(result);

        var collection = await Context.RecordCollections
            .Include(c => c.Labels)
            .FirstAsync(c => c.Id == _collectionId);

        Assert.DoesNotContain(collection.Labels, l => l.Id == _labelId2);
    }

    [Fact]
    public async Task UnattachLabel_ArchivedCollection_ThrowsKeyNotFound()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordCollectionBusiness.UnattachLabel(
                _organizationId, _projectId, _archivedCollectionId, _labelId2));
    }

    [Fact]
    public async Task UnattachLabel_WhenProjectRequiresLabel_AndOnlyOneLabelExists_ThrowsInvalidOperation()
    {
        var project = await Context.Projects.FirstAsync(p => p.Id == _projectId);
        project.RequireSensitivityLabel = true;
        await Context.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _recordCollectionBusiness.UnattachLabel(_organizationId, _projectId, _collectionId, _labelId));
    }
    
    #endregion
    
    #region Archive/Unarchive/Delete Collections
    [Fact]
    public async Task ArchiveRecordCollection_ActiveCollection_ArchivesSuccessfully()
    {
        var result = await _recordCollectionBusiness.ArchiveRecordCollection(
            _userId, _organizationId, _projectId, _collectionId);

        Assert.True(result);

        var collection = await Context.RecordCollections
            .FirstAsync(c => c.Id == _collectionId);

        Assert.True(collection.IsArchived);
    }

    [Fact]
    public async Task ArchiveRecordCollection_AlreadyArchived_ThrowsKeyNotFound()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordCollectionBusiness.ArchiveRecordCollection(
                _userId, _organizationId, _projectId, _archivedCollectionId));
    }

    [Fact]
    public async Task ArchiveRecordCollection_NotFound_ThrowsKeyNotFound()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordCollectionBusiness.ArchiveRecordCollection(
                _userId, _organizationId, _projectId, long.MaxValue));
    }

    [Fact]
    public async Task UnarchiveRecordCollection_ArchivedCollection_UnarchivesSuccessfully()
    {
        var result = await _recordCollectionBusiness.UnarchiveRecordCollection(
            _userId, _organizationId, _projectId, _archivedCollectionId);

        Assert.True(result);

        var collection = await Context.RecordCollections
            .FirstAsync(c => c.Id == _archivedCollectionId);

        Assert.False(collection.IsArchived);
    }

    [Fact]
    public async Task UnarchiveRecordCollection_ActiveCollection_ThrowsKeyNotFound()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordCollectionBusiness.UnarchiveRecordCollection(
                _userId, _organizationId, _projectId, _collectionId));
    }

    [Fact]
    public async Task UnarchiveRecordCollection_NotFound_ThrowsKeyNotFound()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordCollectionBusiness.UnarchiveRecordCollection(
                _userId, _organizationId, _projectId, long.MaxValue));
    }
    
    [Fact]
    public async Task DeleteRecordCollection_NotFound_ThrowsKeyNotFound()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordCollectionBusiness.DeleteRecordCollection(
                _userId, _organizationId, _projectId, long.MaxValue));
    }

    [Fact]
    public async Task DeleteRecordCollection_RemovesFromDatabase()
    {
        var result = await _recordCollectionBusiness.DeleteRecordCollection(
            _userId, _organizationId, _projectId, _collectionId);

        Assert.True(result);

        var exists = await Context.RecordCollections
            .AnyAsync(c => c.Id == _collectionId);

        Assert.False(exists);
    }

    [Fact]
    public async Task DeleteRecordCollection_ArchivedCollection_DeletesSuccessfully()
    {
        // delete should work regardless of archived state
        var result = await _recordCollectionBusiness.DeleteRecordCollection(
            _userId, _organizationId, _projectId, _archivedCollectionId);

        Assert.True(result);

        var exists = await Context.RecordCollections
            .AnyAsync(c => c.Id == _archivedCollectionId);

        Assert.False(exists);
    }
    
    #endregion
    
    #region GetSensitivityLabelsForRecordCollection Tests

    [Fact]
    public async Task GetSensitivityLabelsForRecordCollection_ReturnsLabels()
    {
        // _collectionId is seeded with _labelId in seed data
        var result = await _recordCollectionBusiness.GetSensitivityLabelsForRecordCollection(
            _organizationId, _projectId, _collectionId);

        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Contains(result, l => l.Id == _labelId);
    }

    [Fact]
    public async Task GetSensitivityLabelsForRecordCollection_NoLabels_ReturnsEmptyList()
    {
        // Create a collection with no labels
        var collection = new RecordCollection
        {
            Name = "No Label Collection",
            Description = "Has no labels",
            Properties = "{}",
            ProjectId = _projectId,
            OrganizationId = _organizationId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = _userId,
            IsArchived = false,
            Labels = new List<SensitivityLabel>()
        };
        Context.RecordCollections.Add(collection);
        await Context.SaveChangesAsync();

        var result = await _recordCollectionBusiness.GetSensitivityLabelsForRecordCollection(
            _organizationId, _projectId, collection.Id);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetSensitivityLabelsForRecordCollection_NotFound_ThrowsKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordCollectionBusiness.GetSensitivityLabelsForRecordCollection(
                _organizationId, _projectId, long.MaxValue));
    }

    [Fact]
    public async Task GetSensitivityLabelsForRecordCollection_ArchivedCollection_ThrowsKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordCollectionBusiness.GetSensitivityLabelsForRecordCollection(
                _organizationId, _projectId, _archivedCollectionId));
    }

    [Fact]
    public async Task GetSensitivityLabelsForRecordCollection_WrongProject_ThrowsKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _recordCollectionBusiness.GetSensitivityLabelsForRecordCollection(
                _organizationId, _projectId2, _collectionId));
    }

    #endregion
}
