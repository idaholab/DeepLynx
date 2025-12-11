using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using deeplynx.business;
using deeplynx.datalayer.Models;
using deeplynx.helpers.Hubs;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests;

[Collection("Test Suite Collection")]
public class TagBusinessTests : IntegrationTestBase
{
    private DeeplynxContext _context;
    private EventBusiness _eventBusiness;
    private Mock<IHubContext<EventNotificationHub>> _mockHubContext = null!;
    private Mock<ILogger<NotificationBusiness>> _mockNotificationLogger = null!;
    private INotificationBusiness _notificationBusiness = null!;
    private TagBusiness _tagBusiness;

    public long pid; //project IDs
    public long pid2;
    public long pid3;
    public long tid; // tag IDs
    public long tid2;
    public long tid3;
    public long tid4;
    public long uid; // user ID
    public long oid;

    public TagBusinessTests(TestSuiteFixture fixture) : base(fixture)
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

        _tagBusiness = new TagBusiness(
            Context,
            _eventBusiness);
    }

    protected override async Task SeedTestDataAsync()
    {
        await base.SeedTestDataAsync();

        // Add user
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

        // Add organization
        var organization = new Organization { Name = "Test Organization" };
        Context.Organizations.Add(organization);
        await Context.SaveChangesAsync();
        oid = organization.Id;

        // Add projects
        var project = new Project
        {
            Name = "Project 1",
            LastUpdatedBy = uid,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };
        var project2 = new Project
        {
            Name = "Project2",
            LastUpdatedBy = uid,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };
        var project3 = new Project
        {
            Name = "Project 3",
            LastUpdatedBy = uid,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };
        Context.Projects.Add(project);
        Context.Projects.Add(project2);
        Context.Projects.Add(project3);

        await Context.SaveChangesAsync();
        pid = project.Id;
        pid2 = project2.Id;
        pid3 = project3.Id;

        // Add tags
        var tag = new Tag
        {
            Name = "Analytics",
            ProjectId = pid,
            LastUpdatedBy = uid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified).AddMonths(-12),
            IsArchived = false,
            OrganizationId = oid
        };

        var tag2 = new Tag
        {
            Name = "Analytics 2",
            ProjectId = pid,
            LastUpdatedBy = uid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified).AddMonths(-12),
            IsArchived = false,
            OrganizationId = oid
        };
        var tag3 = new Tag
        {
            Name = "Analytics 3",
            ProjectId = pid,
            LastUpdatedBy = uid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified).AddMonths(-12),
            IsArchived = true,
            OrganizationId = oid
        };
        var tag4 = new Tag
        {
            Name = "Analytics 4",
            ProjectId = pid2,
            LastUpdatedBy = uid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified).AddMonths(-12),
            IsArchived = false,
            OrganizationId = oid
        };
        await Context.Tags.AddAsync(tag);
        await Context.Tags.AddAsync(tag2);
        await Context.Tags.AddAsync(tag3);
        await Context.Tags.AddAsync(tag4);
        await Context.SaveChangesAsync();
        tid = tag.Id;
        tid2 = tag2.Id;
        tid3 = tag3.Id;
        tid4 = tag4.Id;
    }

    #region GetAllTags Tests

    [Fact]
    public async Task GetAllTags_ValidProjectId_ReturnsActiveTags()
    {
        // Act
        var result = await _tagBusiness.GetAllTags(oid, [pid], true);
        var tags = result.ToList();

        // Assert
        Assert.Equal(2, tags.Count);
        Assert.All(tags, t => Assert.Equal(pid, t.ProjectId));
        Assert.All(tags, t => Assert.Equal(false, t.IsArchived));
        Assert.Contains(tags, t => t.Id == tid);
        Assert.Contains(tags, t => t.Id == tid2);
        Assert.DoesNotContain(tags, t => t.Id == tid3);
        Assert.DoesNotContain(tags, t => t.Id == tid4);
    }

    [Fact]
    public async Task GetAllTags_ProjectWithNoTags_ReturnsEmptyList()
    {
        // Act
        var result = await _tagBusiness.GetAllTags(oid, [pid3], true);
        var tags = result.ToList();

        // Assert
        Assert.Empty(tags);
    }

    [Fact]
    public async Task GetAllTags_DifferentProject_ReturnsCorrectTags()
    {
        // Act
        var result = await _tagBusiness.GetAllTags(oid, [pid], true);
        var tags = result.ToList();

        // Assert
        Assert.Equal(2, tags.Count);
        Assert.All(tags, ds => Assert.Equal(pid, ds.ProjectId));
        Assert.Equal(pid, tags.First().ProjectId);
    }

    #endregion

    #region GetTag Tests

    [Fact]
    public async Task GetTag_ValidIds_ReturnsTag()
    {
        // Act
        var result = await _tagBusiness.GetTag(oid, pid, tid, false);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(tid, result.Id);
        Assert.Equal("Analytics", result.Name);
        Assert.Equal(uid, result.LastUpdatedBy);
        Assert.False(result.IsArchived);
        Assert.Equal(pid, result.ProjectId);
    }

    [Fact]
    public async Task GetTag_NonExistentTag_ThrowsKeyNotFoundException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _tagBusiness.GetTag(oid, pid, 999, false));

        Assert.Contains("Tag with id 999 not found", exception.Message);
    }

    [Fact]
    public async Task GetTag_WrongProject_ThrowsKeyNotFoundException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _tagBusiness.GetTag(oid, pid, tid4, false)); // Tag 1 belongs to project 1, not 2

        Assert.Contains($"Tag with id {tid4} not found", exception.Message);
    }

    [Fact]
    public async Task GetTag_ArchivedTag_ThrowsKeyNotFoundException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _tagBusiness.GetTag(oid, pid, tid3, true)); // Tag 3 of project 1 is archived

        Assert.Contains($"Tag with id {tid3} is archived", exception.Message);
    }

    [Fact]
    public async Task GetTagsByName_Success_WithNullProjectId()
    {
        // Arrange
        var orgTag = new Tag
        {
            Name = "Org Tag By Name",
            ProjectId = null,
            OrganizationId = oid,
            LastUpdatedBy = uid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            IsArchived = false
        };
        Context.Tags.Add(orgTag);
        await Context.SaveChangesAsync();

        // Act
        var result = await _tagBusiness.GetTagsByName(oid, null, new List<string> { "Org Tag By Name" });

        // Assert
        Assert.Single(result);
        Assert.Null(result[0].ProjectId);
        Assert.Equal(oid, result[0].OrganizationId);
    }

    #endregion

    #region CreateTag Tests

    [Fact]
    public async Task CreateTag_NullDto_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _tagBusiness.CreateTag(oid, uid, pid, null));

        // Ensure event was not logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task CreateTag_ValidDto_CreatesTag()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var dto = new CreateTagRequestDto
        {
            Name = "Tag One"
        };

        // Act
        var result = await _tagBusiness.CreateTag(oid, uid, pid, dto);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal("Tag One", result.Name);
        Assert.Equal(pid, result.ProjectId);
        Assert.True(result.LastUpdatedAt >= now);
        Assert.Equal(uid, result.LastUpdatedBy);
        Assert.False(result.IsArchived);


        // Verify it was actually saved to database
        var savedTag = await Context.Tags.FindAsync(result.Id);
        Assert.NotNull(savedTag);
        Assert.Equal("Tag One", savedTag.Name);

        // Ensure that the Tag create event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];
        Assert.Equal(result.ProjectId, actualEvent.ProjectId);
        Assert.Equal("create", actualEvent.Operation);
        Assert.Equal("tag", actualEvent.EntityType);
        Assert.Equal(result.Id, actualEvent.EntityId);
    }

    [Fact]
    public async Task CreateTag_SetsCreatedAtAndCreatedBy()
    {
        // Arrange
        var dto = new CreateTagRequestDto
        {
            Name = "Tag Timestamp Test"
        };

        // Act
        var result = await _tagBusiness.CreateTag(oid, uid, pid, dto);

        // Assert
        Assert.True(result.LastUpdatedAt <= DateTime.UtcNow);

        // Ensure that the Tag create event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];
        Assert.Equal(result.ProjectId, actualEvent.ProjectId);
        Assert.Equal("create", actualEvent.Operation);
        Assert.Equal("tag", actualEvent.EntityType);
        Assert.Equal(result.Id, actualEvent.EntityId);
    }

    [Fact]
    public async Task CreateTag_Success_OnBulkCreate()
    {
        var tags = new List<CreateTagRequestDto>
        {
            new()
            {
                Name = "Test Tag 1"
            },
            new()
            {
                Name = "Test Tag 2"
            }
        };

        var result = await _tagBusiness.BulkCreateTags(oid, uid, pid, tags);
        Assert.Equal(2, result.Count);
        Assert.Equal("Test Tag 1", result.First().Name);
        Assert.Equal("Test Tag 2", result.Last().Name);

        // Ensure that create event was logged for each created tag
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var firstEvent = eventList[0];
        Assert.Equal("create", firstEvent.Operation);
        Assert.Equal("tag", firstEvent.EntityType);
        Assert.Equal(result[0].ProjectId, firstEvent.ProjectId);
    }

    [Fact]
    public async Task CreateTagRequest_Fails_IfNoName()
    {
        // Arrange
        var dto = new CreateTagRequestDto { Name = null };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => _tagBusiness.CreateTag(oid, uid, pid, dto));

        // Ensure that no tag create event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task CreateTagNullProjId_Success_CreatesOrgTag()
    {
        // Arrange
        var dto = new CreateTagRequestDto
        {
            Name = "Organization Tag"
        };

        // Act
        var result = await _tagBusiness.CreateTag(oid, uid, null, dto);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.ProjectId);
        Assert.Equal(oid, result.OrganizationId);
        Assert.Equal("Organization Tag", result.Name);
    }

    // [Fact]
    // public async Task BulkCreateTags_Success_WithNullProjectId()
    // {
    //     // Arrange
    //     var tags = new List<CreateTagRequestDto>
    //     {
    //         new() { Name = "Org Tag 1" },
    //         new() { Name = "Org Tag 2" }
    //     };

    //     // Act
    //     var result = await _tagBusiness.BulkCreateTags(oid, uid, null, tags);

    //     // Assert
    //     Assert.Equal(2, result.Count);
    //     Assert.All(result, t => Assert.Null(t.ProjectId));
    //     Assert.All(result, t => Assert.Equal(oid, t.OrganizationId));
    // }

    #endregion

    #region UpdateTag Tests

    [Fact]
    public async Task UpdateTag_ValidUpdate_UpdatesTag()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var dto = new UpdateTagRequestDto
        {
            Name = "Updated Test Tag"
        };

        // Act
        var result = await _tagBusiness.UpdateTag(oid, uid, pid, tid, dto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(tid, result.Id);
        Assert.Equal("Updated Test Tag", result.Name);
        Assert.False(result.IsArchived);
        Assert.Equal(pid, result.ProjectId);
        Assert.True(result.LastUpdatedAt >= now);
        Assert.Equal(uid, result.LastUpdatedBy);

        // Verify it was actually updated in database
        var updatedTag = await Context.Tags.FindAsync(tid);
        Assert.Equal("Updated Test Tag", updatedTag?.Name);
        Assert.NotNull(updatedTag?.LastUpdatedAt);

        // Ensure that the tag update event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];
        Assert.Equal(result.ProjectId, actualEvent.ProjectId);
        Assert.Equal("update", actualEvent.Operation);
        Assert.Equal("tag", actualEvent.EntityType);
        Assert.Equal(result.Id, actualEvent.EntityId);
    }

    [Fact]
    public async Task UpdateTag_PartialUpdate_UpdatesTag()
    {
        // Arrange
        var updateDto = new UpdateTagRequestDto
        {
            Name = "Updated Tag"
        };

        var originalTag = await Context.Tags.FindAsync(tid);
        Assert.Equal("Analytics", originalTag.Name);


        // Act
        var result = await _tagBusiness.UpdateTag(oid, uid, pid, tid, updateDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(originalTag.Id, result.Id);
        Assert.Equal("Updated Tag", result.Name);
        Assert.Equal(originalTag.LastUpdatedBy, result.LastUpdatedBy);

        // Verify it was actually updated in database
        var updatedTag = await Context.Tags.FindAsync(originalTag.Id);
        Assert.NotNull(updatedTag);
        Assert.Equal("Updated Tag", updatedTag.Name);
        Assert.True(result.LastUpdatedAt > DateTime.MinValue);

        // Ensure that the tag update event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];
        Assert.Equal(result.ProjectId, actualEvent.ProjectId);
        Assert.Equal("update", actualEvent.Operation);
        Assert.Equal("tag", actualEvent.EntityType);
        Assert.Equal(result.Id, actualEvent.EntityId);
    }

    [Fact]
    public async Task UpdateTag_NonExistentTag_ThrowsKeyNotFoundException()
    {
        // Arrange
        var dto = new UpdateTagRequestDto
        {
            Name = "Update Test Tag"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _tagBusiness.UpdateTag(oid, uid, pid, 999, dto));

        Assert.Contains("Tag with id 999 not found", exception.Message);

        // Ensure that the update tag event was not logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task UpdateTag_WrongProject_ThrowsKeyNotFoundException()
    {
        // Arrange
        var dto = new UpdateTagRequestDto
        {
            Name = "Update Test"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _tagBusiness.UpdateTag(oid, uid, pid2, tid, dto)); // Tag 1 belongs to project 2

        Assert.Contains($"Tag with id {tid} not found", exception.Message);

        // Ensure that the update tag event was not logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task UpdateTag_ArchivedTag_ThrowsKeyNotFoundException()
    {
        // Arrange
        var dto = new UpdateTagRequestDto
        {
            Name = "Update Test"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _tagBusiness.UpdateTag(oid, uid, pid, tid3, dto)); // Tag 3 is archived

        Assert.Contains($"Tag with id {tid3} not found", exception.Message);

        // Ensure that the update tag event was not logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    #endregion

    #region DeleteTag Tests

    [Fact]
    public async Task DeleteTag_ValidTag_DeletesSuccessfully()
    {
        // Act
        var result = await _tagBusiness.DeleteTag(oid, pid, tid);

        // Assert
        Assert.True(result);

        // Verify it was actually deleted from database
        var deletedTag = await Context.Tags.FindAsync(tid);
        Assert.Null(deletedTag);
    }

    [Fact]
    public async Task DeleteTag_NonExistentTag_ThrowsKeyNotFoundException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _tagBusiness.DeleteTag(oid, pid, 999));

        Assert.Contains("Tag with id 999 not found", exception.Message);
    }

    [Fact]
    public async Task DeleteTag_WrongProject_ThrowsKeyNotFoundException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _tagBusiness.DeleteTag(oid, pid2, tid)); // Tag 1 belongs to project 1

        Assert.Contains($"Tag with id {tid} not found", exception.Message);
    }

    #endregion

    #region ArchiveTag Tests

    [Fact]
    public async Task ArchiveTag_ValidTag_ArchivesSuccessfully()
    {
        // Arrange
        var now = DateTime.UtcNow;
        // Act
        var result = await _tagBusiness.ArchiveTag(oid, uid, pid, tid);

        // Assert
        Assert.True(result);

        // Verify it was actually archived in database
        var archivedTag = await Context.Tags.FindAsync(tid);
        Assert.NotNull(archivedTag);
        Assert.True(archivedTag.IsArchived);
        Assert.Equal(tid, archivedTag.Id);
        Assert.Equal("Analytics", archivedTag.Name);
        Assert.Equal(pid, archivedTag.ProjectId);
        Assert.True(archivedTag.LastUpdatedAt >= now);
        Assert.Equal(uid, archivedTag.LastUpdatedBy);

        // Ensure that the tag delete event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];
        Assert.Equal(pid, actualEvent.ProjectId);
        Assert.Equal("delete", actualEvent.Operation);
        Assert.Equal("tag", actualEvent.EntityType);
    }

    [Fact]
    public async Task ArchiveTag_NonExistentTag_ThrowsKeyNotFoundException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _tagBusiness.ArchiveTag(oid, uid, pid, 999));

        Assert.Contains("Tag with id 999 not found", exception.Message);

        // Ensure that no tag delete event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task ArchiveTag_WrongProject_ThrowsKeyNotFoundException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _tagBusiness.ArchiveTag(oid, uid, pid2, tid));

        Assert.Contains($"Tag with id {tid} not found", exception.Message);

        // Ensure that no tag delete event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task ArchiveTag_AlreadyArchivedTag_ThrowsKeyNotFoundException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _tagBusiness.ArchiveTag(oid, uid, pid, tid3)); // Tag 3 is already archived

        Assert.Contains($"Tag with id {tid3} not found", exception.Message);

        // Ensure that no tag delete event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task ArchiveTag_ArchivedTagNotReturnedInGetAll()
    {
        // Arrange
        var initialCount = (await _tagBusiness.GetAllTags(oid, [pid], true)).Count;

        // Act
        await _tagBusiness.ArchiveTag(oid, uid, pid, tid);
        var finalCount = (await _tagBusiness.GetAllTags(oid, [pid], true)).Count;

        // Assert
        Assert.Equal(initialCount - 1, finalCount);
    }

    #endregion

    #region Edge Cases and Integration Tests

    [Fact]
    public async Task TagOperations_ConcurrentModification_HandlesCorrectly()
    {
        // This test simulates concurrent operations on the same data source
        // In a real scenario, you might want to test with actual concurrent tasks

        // Arrange
        var dto1 = new UpdateTagRequestDto
        {
            Name = "Concurrent Tag Update 1"
        };

        var dto2 = new UpdateTagRequestDto
        {
            Name = "Concurrent Tag Update 2"
        };

        // Act
        var task1 = await _tagBusiness.UpdateTag(oid, uid, pid, tid, dto1);
        var task2 = await _tagBusiness.UpdateTag(oid, uid, pid, tid2, dto2);

        // Assert
        var result1 = task1;
        var result2 = task2;

        Assert.Equal("Concurrent Tag Update 1", result1.Name);
        Assert.Equal("Concurrent Tag Update 2", result2.Name);
    }

    [Fact]
    public async Task TagOperations_SpecialCharactersInFields_HandlesCorrectly()
    {
        // Arrange
        var dto = new CreateTagRequestDto
        {
            Name = "Test with émojis 🚀 and ñ special chars 中文"
        };

        // Act
        var result = await _tagBusiness.CreateTag(oid, uid, pid, dto);

        // Assert
        Assert.Equal("Test with émojis 🚀 and ñ special chars 中文", result.Name);
    }

    [Fact]
    public void TagRequestDto_AllProperties_CanBeSetAndRetrieved()
    {
        // Arrange & Act
        var dto = new CreateTagRequestDto
        {
            Name = "Tag One"
        };

        // Assert
        Assert.Equal("Tag One", dto.Name);
    }

    [Fact]
    public void TagResponseDto_AllProperties_CanBeSetAndRetrieved()
    {
        // Arrange & Act
        var now = DateTime.UtcNow;

        var dto = new TagResponseDto
        {
            Id = 1,
            Name = "Tag One",
            LastUpdatedBy = uid,
            ProjectId = 1,
            LastUpdatedAt = now,
            IsArchived = false
        };

        // Assert
        Assert.Equal(1, dto.Id);
        Assert.Equal("Tag One", dto.Name);
        Assert.Equal(uid, dto.LastUpdatedBy);
        Assert.Equal(1, dto.ProjectId);
        Assert.Equal(uid, dto.LastUpdatedBy);
        Assert.Equal(now, dto.LastUpdatedAt);
        Assert.False(dto.IsArchived);
    }

    #endregion

    #region UnarchiveTag Tests

    [Fact]
    public async Task UnarchiveTag_ValidArchivedTag_UnarchivesSuccessfully()
    {
        // Arrange
        var now = DateTime.UtcNow;

        // Act
        var result = await _tagBusiness.UnarchiveTag(oid, uid, pid, tid3);

        Assert.True(result);

        var refreshed = await Context.Tags.FindAsync(tid3);
        Assert.NotNull(refreshed);
        Assert.False(refreshed.IsArchived);
        Assert.Equal(tid3, refreshed.Id);
        Assert.Equal("Analytics 3", refreshed.Name);
        Assert.Equal(pid, refreshed.ProjectId);
        Assert.True(refreshed.LastUpdatedAt >= now);
        Assert.Equal(uid, refreshed.LastUpdatedBy);
    }

    [Fact]
    public async Task UnarchiveTag_NonExistentTag_ThrowsKeyNotFoundException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _tagBusiness.UnarchiveTag(oid, uid, pid, 99999));

        Assert.Contains("Tag with id 99999 not found", exception.Message);
    }

    [Fact]
    public async Task UnarchiveTag_WrongProject_ThrowsKeyNotFoundException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _tagBusiness.UnarchiveTag(oid, uid, pid, tid4)); // Calling with pid (wrong project)

        Assert.Contains($"Tag with id {tid4} not found", exception.Message);
    }

    [Fact]
    public async Task UnarchiveTag_AlreadyActive_ThrowsKeyNotFoundException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _tagBusiness.UnarchiveTag(oid, uid, pid, tid));

        Assert.Contains($"Tag with id {tid} not found", exception.Message);
    }

    #endregion

    #region LastUpdatedBy Tests

    [Fact]
    public async Task CreateTag_Success_StoresLastUpdatedByUserId()
    {
        // Arrange
        var testTag = new Tag
        {
            Name = "Test Tag LastUpdatedBy",
            ProjectId = pid,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };

        // Act
        Context.Tags.Add(testTag);
        await Context.SaveChangesAsync();

        // Assert
        var savedTag = await Context.Tags.FindAsync(testTag.Id);
        Assert.NotNull(savedTag);
        Assert.Equal(uid, savedTag.LastUpdatedBy);
    }

    [Fact]
    public async Task CreateTag_Success_NavigationPropertyLoadsUser()
    {
        // Arrange
        var testTag = new Tag
        {
            Name = "Test Tag Navigation",
            ProjectId = pid,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };

        Context.Tags.Add(testTag);
        await Context.SaveChangesAsync();

        // Act
        var tagWithUser = await Context.Tags
            .Include(t => t.LastUpdatedByUser)
            .FirstAsync(t => t.Id == testTag.Id);

        // Assert
        Assert.NotNull(tagWithUser.LastUpdatedByUser);
        Assert.Equal("Test User", tagWithUser.LastUpdatedByUser.Name);
        Assert.Equal("test.user@test.com", tagWithUser.LastUpdatedByUser.Email);
        Assert.Equal(uid, tagWithUser.LastUpdatedBy);
    }

    [Fact]
    public async Task CreateTag_Success_WithNullLastUpdatedBy()
    {
        // Arrange
        var testTag = new Tag
        {
            Name = "Test Tag Null",
            ProjectId = pid,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = null,
        };

        // Act
        Context.Tags.Add(testTag);
        await Context.SaveChangesAsync();

        // Assert
        var savedTag = await Context.Tags.FindAsync(testTag.Id);
        Assert.NotNull(savedTag);
        Assert.Null(savedTag.LastUpdatedBy);

        var tagWithUser = await Context.Tags
            .Include(t => t.LastUpdatedByUser)
            .FirstAsync(t => t.Id == testTag.Id);

        Assert.Null(tagWithUser.LastUpdatedByUser);
    }

    [Fact]
    public async Task UpdateTag_Success_UpdatesLastUpdatedByUserId()
    {
        // Arrange
        var testTag = new Tag
        {
            Name = "Test Tag Update",
            ProjectId = pid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = null,
            OrganizationId = oid
        };
        Context.Tags.Add(testTag);
        await Context.SaveChangesAsync();

        // Act
        testTag.LastUpdatedBy = uid;
        testTag.Name = "Updated Tag Name";
        testTag.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await Context.SaveChangesAsync();

        // Assert
        var updatedTag = await Context.Tags
            .Include(t => t.LastUpdatedByUser)
            .FirstAsync(t => t.Id == testTag.Id);

        Assert.Equal(uid, updatedTag.LastUpdatedBy);
        Assert.NotNull(updatedTag.LastUpdatedByUser);
        Assert.Equal("Test User", updatedTag.LastUpdatedByUser.Name);
        Assert.Equal("Updated Tag Name", updatedTag.Name);
    }

    #endregion
}