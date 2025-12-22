using System.ComponentModel.DataAnnotations;
using deeplynx.business;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.helpers.Hubs;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests;

[Collection("Test Suite Collection")]
public class GroupBusinessTests : IntegrationTestBase
{
    private EventBusiness _eventBusiness;
    private GroupBusiness _groupBusiness;
    private Mock<IHubContext<EventNotificationHub>> _mockHubContext = null!;
    private Mock<ILogger<NotificationBusiness>> _mockNotificationLogger = null!;
    private INotificationBusiness _notificationBusiness = null!;
    public long gid; // group ID
    public long gid2; // archived group ID

    public long oid; // organization ID
    public long uid; // user ID
    public long uid2; // second user ID (not in group)

    public GroupBusinessTests(TestSuiteFixture fixture) : base(fixture)
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
        _groupBusiness = new GroupBusiness(Context, _eventBusiness);
    }

    protected override async Task SeedTestDataAsync()
    {
        await base.SeedTestDataAsync();

        // create test organization
        var testOrg = new Organization
        {
            Name = "Test Organization",
            Description = "Test org for unit tests",
            IsArchived = false
        };
        Context.Organizations.Add(testOrg);
        await Context.SaveChangesAsync();
        oid = testOrg.Id;

        // create test users
        var testUser = new User
        {
            Name = "Test User",
            Email = "test@test.com",
            IsArchived = false
        };
        var testUser2 = new User
        {
            Name = "Test User 2",
            Email = "test2@test.com",
            IsArchived = false
        };
        Context.Users.AddRange(testUser, testUser2);
        await Context.SaveChangesAsync();
        uid = testUser.Id;
        uid2 = testUser2.Id;

        // create test groups
        var testGroup = new Group
        {
            Name = "Test Group",
            Description = "Test group for unit tests",
            OrganizationId = oid,
            IsArchived = false
        };
        var archivedGroup = new Group
        {
            Name = "Archived Group",
            Description = "Archived group for tests",
            OrganizationId = oid,
            IsArchived = true
        };
        Context.Groups.AddRange(testGroup, archivedGroup);
        await Context.SaveChangesAsync();
        gid = testGroup.Id;
        gid2 = archivedGroup.Id;

        // add test user to test group
        testGroup.Users.Add(testUser);
        await Context.SaveChangesAsync();
    }

    #region GetAllGroups Tests

    [Fact]
    public async Task GetAllGroups_ExcludesArchived()
    {
        // Act
        var result = await _groupBusiness.GetAllGroups(oid);
        var groups = result.ToList();

        // Assert
        Assert.All(groups, g => Assert.False(g.IsArchived));
        Assert.Contains(groups, g => g.Id == gid);
        Assert.DoesNotContain(groups, g => g.Id == gid2); // archived group
    }

    [Fact]
    public async Task GetAllGroups_WithHideArchivedFalse_IncludesArchived()
    {
        // Act
        var result = await _groupBusiness.GetAllGroups(oid, false);
        var groups = result.ToList();

        // Assert
        Assert.Contains(groups, g => g.IsArchived);
        Assert.Contains(groups, g => g.Id == gid);
        Assert.Contains(groups, g => g.Id == gid2); // archived group
    }

    [Fact]
    public async Task GetAllGroups_FiltersOnOrganizationId()
    {
        // Act
        var result = await _groupBusiness.GetAllGroups(oid);
        var groups = result.ToList();

        // Assert
        Assert.All(groups, g => Assert.Equal(oid, g.OrganizationId));
        Assert.Contains(groups, g => g.Id == gid);
    }

    #endregion

    #region GetGroup Tests

    [Fact]
    public async Task GetGroup_Succeeds_WhenExists()
    {
        // Act
        var result = await _groupBusiness.GetGroup(oid, gid);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(gid, result.Id);
        Assert.Equal("Test Group", result.Name);
        Assert.Equal("Test group for unit tests", result.Description);
        Assert.False(result.IsArchived);
    }

    [Fact]
    public async Task GetGroup_Fails_IfNotFound()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() => _groupBusiness.GetGroup(oid, 99999));

        Assert.Contains("Group with id 99999 does not exist", exception.Message);
    }

    [Fact]
    public async Task GetGroup_Fails_IfDeletedGroup()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() => _groupBusiness.GetGroup(oid, gid2)); // archived group

        Assert.Contains($"Group with id {gid2} is archived", exception.Message);
    }

    #endregion

    #region CreateGroup Tests

    [Fact]
    public async Task CreateGroup_Success_ReturnsGroup()
    {
        // Arrange
        var dto = new CreateGroupRequestDto
        {
            Name = "New Test Group",
            Description = "New test group description"
        };

        // Act
        var result = await _groupBusiness.CreateGroup(uid, oid, dto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(dto.Name, result.Name);
        Assert.Equal(dto.Description, result.Description);
        Assert.Equal(oid, result.OrganizationId);
        Assert.False(result.IsArchived);
        Assert.Equal(uid, result.LastUpdatedBy);

        // verify group was actually created in database
        var createdGroup = await Context.Groups.FindAsync(result.Id);
        Assert.NotNull(createdGroup);
        Assert.Equal(dto.Name, createdGroup.Name);

        // Ensure that the Group create event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal("create", actualEvent.Operation);
        Assert.Equal("group", actualEvent.EntityType);
        Assert.Equal(result.Id, actualEvent.EntityId);
    }

    [Fact]
    public async Task CreateGroup_Success_CreatesEvent()
    {
        // Arrange
        var dto = new CreateGroupRequestDto
        {
            Name = "Event Test Group",
            Description = "A test group for event logging"
        };

        // Act
        var result = await _groupBusiness.CreateGroup(uid, oid, dto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Event Test Group", result.Name);

        // Ensure that the Group create event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal("create", actualEvent.Operation);
        Assert.Equal("group", actualEvent.EntityType);
        Assert.Equal(dto.Name, actualEvent.EntityName);
        Assert.Equal(result.Id, actualEvent.EntityId);
    }

    [Fact]
    public async Task CreateGroup_Fails_IfNoName()
    {
        // Arrange
        var dto = new CreateGroupRequestDto
        {
            Description = "Group without name"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => _groupBusiness.CreateGroup(uid, oid, dto));

        // Ensure that no event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task CreateGroup_Fails_IfEmptyName()
    {
        // Arrange
        var dto = new CreateGroupRequestDto
        {
            Name = "",
            Description = "Group with empty name"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => _groupBusiness.CreateGroup(uid, oid, dto));

        // Ensure that no event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    #endregion

    #region UpdateGroup Tests

    [Fact]
    public async Task UpdateGroup_Success_ReturnsGroup()
    {
        // Arrange
        var dto = new UpdateGroupRequestDto
        {
            Name = "Updated Group",
            Description = "Updated description"
        };

        // Act
        var result = await _groupBusiness.UpdateGroup(uid, oid, gid, dto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(gid, result.Id);
        Assert.Equal(dto.Name, result.Name);
        Assert.Equal(dto.Description, result.Description);
        Assert.Equal(oid, result.OrganizationId);
        Assert.Equal(uid, result.LastUpdatedBy);
        Assert.False(result.IsArchived);


        // Verify it was actually saved to DB
        var savedGroup = await Context.Groups.FindAsync(gid);
        Assert.NotNull(savedGroup);
        Assert.Equal("Updated Group", savedGroup.Name);
        Assert.Equal("Updated description", savedGroup.Description);

        // Ensure that the Group update event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal("update", actualEvent.Operation);
        Assert.Equal("group", actualEvent.EntityType);
        Assert.Equal(result.Id, actualEvent.EntityId);
    }

    [Fact]
    public async Task UpdateGroup_Success_CreatesEvent()
    {
        // Arrange
        var dto = new UpdateGroupRequestDto
        {
            Name = "Event Updated Group"
        };

        // Act
        var result = await _groupBusiness.UpdateGroup(uid, oid, gid, dto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Event Updated Group", result.Name);

        // Ensure that the Group update event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal("update", actualEvent.Operation);
        Assert.Equal("group", actualEvent.EntityType);
        Assert.Equal(result.Id, actualEvent.EntityId);
    }

    [Fact]
    public async Task UpdateGroup_Fails_IfNotFound()
    {
        // Arrange
        var dto = new UpdateGroupRequestDto
        {
            Name = "Updated Group"
        };

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() => _groupBusiness.UpdateGroup(uid, oid, 99999, dto));

        Assert.Contains("Group with id 99999 not found", exception.Message);

        // Ensure that no event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task UpdateGroup_Fails_IfArchived()
    {
        // Arrange
        var dto = new UpdateGroupRequestDto
        {
            Name = "Updated Archived Group"
        };

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _groupBusiness.UpdateGroup(uid, oid, gid2, dto)); // archived group

        Assert.Contains($"Group with id {gid2} not found", exception.Message);

        // Ensure that no event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    #endregion

    #region ArchiveGroup Tests

    [Fact]
    public async Task ArchiveGroup_Succeeds_IfNotArchived()
    {
        // Act
        var result = await _groupBusiness.ArchiveGroup(uid, oid, gid);

        // Assert
        Assert.True(result);

        // Verify it was actually saved to DB
        var savedGroup = await Context.Groups.FindAsync(gid);
        Assert.NotNull(savedGroup);
        Assert.Equal(gid, savedGroup.Id);
        Assert.True(savedGroup.IsArchived);
        Assert.Equal("Test Group", savedGroup.Name);
        Assert.Equal("Test group for unit tests", savedGroup.Description);
        Assert.Equal(oid, savedGroup.OrganizationId);

        // Ensure that the Group archive event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal("archive", actualEvent.Operation);
        Assert.Equal("group", actualEvent.EntityType);
        Assert.Equal(gid, actualEvent.EntityId);
    }

    [Fact]
    public async Task ArchiveGroup_Fails_IfArchived()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _groupBusiness.ArchiveGroup(uid, oid, gid2)); // already archived

        Assert.Contains($"Group with id {gid2} not found or is archived", exception.Message);

        // Ensure that no event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task ArchiveGroup_Fails_IfNotFound()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() => _groupBusiness.ArchiveGroup(uid, oid, 99999));

        Assert.Contains("Group with id 99999 not found or is archived", exception.Message);

        // Ensure that no event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    #endregion

    #region UnarchiveGroup Tests

    [Fact]
    public async Task UnarchiveGroup_Succeeds_IfArchived()
    {
        // Act
        var result = await _groupBusiness.UnarchiveGroup(uid, oid, gid2);

        // Assert
        Assert.True(result);

        // Verify it was actually saved to DB
        var savedGroup = await Context.Groups.FindAsync(gid2);
        Assert.NotNull(savedGroup);
        Assert.False(savedGroup.IsArchived);
        Assert.Equal(gid2, savedGroup.Id);
        Assert.Equal("Archived Group", savedGroup.Name);
        Assert.Equal("Archived group for tests", savedGroup.Description);
        Assert.Equal(oid, savedGroup.OrganizationId);

        // Ensure that the Group unarchive event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal("unarchive", actualEvent.Operation);
        Assert.Equal("group", actualEvent.EntityType);
        Assert.Equal(gid2, actualEvent.EntityId);
    }

    [Fact]
    public async Task UnarchiveGroup_Fails_IfNotArchived()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _groupBusiness.UnarchiveGroup(uid, oid, gid)); // not archived

        Assert.Contains($"Group with id {gid} not found or is not archived", exception.Message);

        // Ensure that no event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task UnarchiveGroup_Fails_IfNotFound()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() => _groupBusiness.UnarchiveGroup(uid, oid, 99999));

        Assert.Contains("Group with id 99999 not found or is not archived", exception.Message);

        // Ensure that no event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    #endregion

    #region DeleteGroup Tests

    [Fact]
    public async Task DeleteGroup_Succeeds_WhenExists()
    {
        // Act
        var result = await _groupBusiness.DeleteGroup(uid, oid, gid);

        // Assert
        Assert.True(result);

        // Verify it was actually deleted from DB
        var deletedGroup = await Context.Groups.FindAsync(gid);
        Assert.Null(deletedGroup);

        // Ensure that the Group delete event was logged
        var eventList = await Context.Events.Where(e => e.Operation == "delete").ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal("delete", actualEvent.Operation);
        Assert.Equal("group", actualEvent.EntityType);
        Assert.Equal(gid, actualEvent.EntityId);
    }

    [Fact]
    public async Task DeleteGroup_Fails_IfNotFound()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() => _groupBusiness.DeleteGroup(uid, oid, 99999));

        Assert.Contains("Group with id 99999 not found", exception.Message);

        // Ensure that no event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task DeleteGroup_Fails_IfArchived()
    {
        // Act & Assert - trying to delete archived group
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _groupBusiness.DeleteGroup(uid, oid, gid2)); // archived

        Assert.Contains($"Group with id {gid2} not found", exception.Message);

        // Ensure that no event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    #endregion

    #region AddUser Tests

    [Fact]
    public async Task AddUser_Succeeds_IfGroupAndUserExists()
    {
        // Arrange
        var newGroup = new Group { Name = "Test Group", OrganizationId = oid };
        Context.Groups.Add(newGroup);
        await Context.SaveChangesAsync();
        var newGroupId = newGroup.Id;

        // Act
        var added = await _groupBusiness.AddUserToGroup(uid, oid, newGroupId);

        // Assert
        Assert.True(added);
        var group = await Context.Groups.FirstOrDefaultAsync(g => g.Id == newGroupId);
        Assert.NotNull(group);
        Assert.Single(group.Users);
        var user = group.Users.FirstOrDefault(u => u.Id == uid);
        Assert.NotNull(user);
    }

    [Fact]
    public async Task AddUser_Fails_IfGroupNotFound()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() => _groupBusiness.AddUserToGroup(uid, oid, 99999));

        Assert.Contains("Group with id 99999 not found", exception.Message);
    }

    [Fact]
    public async Task AddUser_Fails_IfUserNotFound()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() => _groupBusiness.AddUserToGroup(99999, oid, gid));

        Assert.Contains("User with id 99999 not found", exception.Message);
    }

    #endregion

    #region RemoveUser Tests

    [Fact]
    public async Task RemoveUser_Succeeds_IfGroupUserExists()
    {
        // Act
        var result = await _groupBusiness.RemoveUserFromGroup(uid, oid, gid);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task RemoveUser_ReturnsFalse_IfUserNotInGroup()
    {
        // Act
        var result = await _groupBusiness.RemoveUserFromGroup(uid2, oid, gid);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task RemoveUser_Fails_IfGroupNotFound()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() => _groupBusiness.RemoveUserFromGroup(uid, oid, 99999));

        Assert.Contains("Group with id 99999 not found", exception.Message);
    }

    [Fact]
    public async Task RemoveUser_Fails_IfUserNotFound()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() => _groupBusiness.RemoveUserFromGroup(99999, oid, gid));

        Assert.Contains("User with id 99999 does not exist", exception.Message);
    }

    #endregion

    #region LastUpdatedBy Tests

    [Fact]
    public async Task CreateGroup_Success_StoresLastUpdatedByUserId()
    {
        // Arrange
        var testGroup = new Group
        {
            Name = $"Test Group with User {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Description = "Test Description with User ID",
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            IsArchived = false
        };

        // Act
        Context.Groups.Add(testGroup);
        await Context.SaveChangesAsync();

        // Assert
        var savedGroup = await Context.Groups.FindAsync(testGroup.Id);
        Assert.NotNull(savedGroup);
        Assert.Equal(uid, savedGroup.LastUpdatedBy);
    }

    [Fact]
    public async Task CreateGroup_Success_NavigationPropertyLoadsUser()
    {
        // Arrange
        var testGroup = new Group
        {
            Name = $"Test Group Navigation {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Description = "Test Navigation Property",
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            IsArchived = false
        };

        Context.Groups.Add(testGroup);
        await Context.SaveChangesAsync();

        // Act
        var groupWithUser = await Context.Groups
            .Include(g => g.LastUpdatedByUser)
            .FirstAsync(g => g.Id == testGroup.Id);

        // Assert
        Assert.NotNull(groupWithUser.LastUpdatedByUser);
        Assert.Equal("Test User", groupWithUser.LastUpdatedByUser.Name);
        Assert.Equal("test@test.com", groupWithUser.LastUpdatedByUser.Email);
        Assert.Equal(uid, groupWithUser.LastUpdatedBy);
    }

    [Fact]
    public async Task CreateGroup_Success_WithNullLastUpdatedBy()
    {
        // Arrange
        var testGroup = new Group
        {
            Name = $"Test Group Null User {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Description = "Test with null LastUpdatedBy",
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = null,
            IsArchived = false
        };

        // Act
        Context.Groups.Add(testGroup);
        await Context.SaveChangesAsync();

        // Assert
        var savedGroup = await Context.Groups.FindAsync(testGroup.Id);
        Assert.NotNull(savedGroup);
        Assert.Null(savedGroup.LastUpdatedBy);

        var groupWithUser = await Context.Groups
            .Include(g => g.LastUpdatedByUser)
            .FirstAsync(g => g.Id == testGroup.Id);

        Assert.Null(groupWithUser.LastUpdatedByUser);
    }

    [Fact]
    public async Task UpdateGroup_Success_UpdatesLastUpdatedByUserId()
    {
        // Arrange
        var testGroup = new Group
        {
            Name = $"Original Group {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Description = "Original Description",
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = null
        };
        Context.Groups.Add(testGroup);
        await Context.SaveChangesAsync();

        // Act
        testGroup.LastUpdatedBy = uid2;
        testGroup.Description = "Updated Description";
        testGroup.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        Context.Groups.Update(testGroup);
        await Context.SaveChangesAsync();

        // Assert
        var updatedGroup = await Context.Groups
            .Include(g => g.LastUpdatedByUser)
            .FirstAsync(g => g.Id == testGroup.Id);

        Assert.Equal(uid2, updatedGroup.LastUpdatedBy);
        Assert.NotNull(updatedGroup.LastUpdatedByUser);
        Assert.Equal("Test User 2", updatedGroup.LastUpdatedByUser.Name);
        Assert.Equal("Updated Description", updatedGroup.Description);
    }

    #endregion
}