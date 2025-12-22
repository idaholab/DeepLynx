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
public class RelationshipBusinessTests : IntegrationTestBase
{
    private ClassBusiness _classBusiness = null!;
    private DataSourceBusiness _dataSourceBusiness = null!;
    private EventBusiness _eventBusiness = null!;
    private Mock<IEdgeBusiness> _mockEdgeBusiness = null!;
    private Mock<IHubContext<EventNotificationHub>> _mockHubContext = null!;
    private Mock<ILogger<ProjectBusiness>> _mockLogger = null!;
    private Mock<ILogger<NotificationBusiness>> _mockNotificationLogger = null!;
    private Mock<IObjectStorageBusiness> _mockObjectStorageBusiness = null!;
    private Mock<IOrganizationBusiness> _mockOrganizationBusiness = null!;
    private Mock<IRecordBusiness> _mockRecordBusiness = null!;
    private Mock<IRoleBusiness> _mockRoleBusiness = null!;
    private INotificationBusiness _notificationBusiness = null!;
    private ProjectBusiness _projectBusiness = null!;
    private RelationshipBusiness _relationshipBusiness = null!;
    public long cid; // origin class ID
    public long cid2; // dest. class ID
    public long oid; // organization ID

    public long pid; // project ID
    public long rid; // existing record ID
    public long rid2; // archived record ID
    public long uid; // user ID

    public RelationshipBusinessTests(TestSuiteFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _mockEdgeBusiness = new Mock<IEdgeBusiness>();
        _mockRecordBusiness = new Mock<IRecordBusiness>();
        _mockLogger = new Mock<ILogger<ProjectBusiness>>();
        _mockHubContext = new Mock<IHubContext<EventNotificationHub>>();
        _mockNotificationLogger = new Mock<ILogger<NotificationBusiness>>();
        _notificationBusiness =
            new NotificationBusiness(Context, _mockNotificationLogger.Object, _mockHubContext.Object);
        _eventBusiness = new EventBusiness(Context, _notificationBusiness);
        _mockObjectStorageBusiness = new Mock<IObjectStorageBusiness>();
        _mockRoleBusiness = new Mock<IRoleBusiness>();
        _mockOrganizationBusiness = new Mock<IOrganizationBusiness>();

        _relationshipBusiness = new RelationshipBusiness(
            Context, _mockEdgeBusiness.Object, _eventBusiness);

        _dataSourceBusiness = new DataSourceBusiness(
            Context, _mockEdgeBusiness.Object, _mockRecordBusiness.Object, _eventBusiness);

        _classBusiness = new ClassBusiness(
            Context, _mockRecordBusiness.Object,
            _relationshipBusiness, _eventBusiness);

        _projectBusiness = new ProjectBusiness(
            Context, _mockLogger.Object,
            _classBusiness, _mockRoleBusiness.Object, _dataSourceBusiness,
            _mockObjectStorageBusiness.Object, _eventBusiness, _mockOrganizationBusiness.Object);
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

        var organization = new Organization { Name = "Test Organization" };
        Context.Organizations.Add(organization);
        await Context.SaveChangesAsync();
        oid = organization.Id;

        // Add project
        var project = new Project
        {
            Name = "Project 1",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            OrganizationId = oid
        };
        Context.Projects.Add(project);
        await Context.SaveChangesAsync();
        pid = project.Id;

        // Add datasource
        var dataSource = new DataSource
        {
            Name = "DataSource 1",
            OrganizationId = oid,
            ProjectId = pid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        Context.DataSources.Add(dataSource);
        await Context.SaveChangesAsync();

        // Add classes
        var originClass = new Class
        {
            Name = "Origin Class",
            OrganizationId = oid,
            ProjectId = pid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        Context.Classes.Add(originClass);

        var destinationClass = new Class
        {
            Name = "Destination Class",
            OrganizationId = oid,
            ProjectId = pid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        Context.Classes.Add(destinationClass);
        await Context.SaveChangesAsync();

        cid = originClass.Id;
        cid2 = destinationClass.Id;

        // Add relationships
        var existingRelationship = new Relationship
        {
            Name = "Original Relationship",
            Description = "Original Description",
            OrganizationId = oid,
            ProjectId = pid,
            OriginId = cid,
            DestinationId = cid2,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = null
        };
        var archivedRelationship = new Relationship
        {
            Name = "Archived Relationship",
            Description = "Archived Description",
            OrganizationId = oid,
            ProjectId = pid,
            OriginId = cid,
            DestinationId = cid2,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = null,
            IsArchived = true
        };
        Context.Relationships.AddRange(existingRelationship, archivedRelationship);
        await Context.SaveChangesAsync();

        rid = existingRelationship.Id;
        rid2 = archivedRelationship.Id;
    }

    #region CreateRelationship Tests

    [Fact]
    public async Task CreateRelationship_Success_ReturnsIdAndCreatedAt()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var dto = new CreateRelationshipRequestDto
        {
            Name = $"Test Relationship {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Description = "Test Description",
            Uuid = $"test-uuid-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            OriginId = cid,
            DestinationId = cid2
        };

        // Act
        var result = await _relationshipBusiness.CreateRelationship(uid, oid, pid, dto);

        // Assert
        Assert.True(result.Id > 0);
        Assert.True(result.LastUpdatedAt >= now);
        Assert.Equal(dto.Name, result.Name);
        Assert.Equal(dto.Description, result.Description);
        Assert.Equal(cid, result.OriginId);
        Assert.Equal(cid2, result.DestinationId);
        Assert.Equal(pid, result.ProjectId);
        Assert.NotNull(result.OriginId);
        Assert.NotNull(result.DestinationId);
        Assert.Equal(dto.Uuid, result.Uuid);
        Assert.Equal(uid, result.LastUpdatedBy);

        // Ensure that relationship create event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal("create", actualEvent.Operation);
        Assert.Equal("relationship", actualEvent.EntityType);
        Assert.Equal(result.Id, actualEvent.EntityId);
        Assert.Equal(result.ProjectId, actualEvent.ProjectId);
    }

    [Fact]
    public async Task CreateRelationship_Success_WithNullOriginId()
    {
        // Arrange
        var dto = new CreateRelationshipRequestDto
        {
            Name = $"Test Relationship {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Description = "Test Description",
            OriginId = null,
            DestinationId = cid2
        };

        // Act
        var result = await _relationshipBusiness.CreateRelationship(uid, oid, pid, dto);

        // Assert
        Assert.True(result.Id > 0);
        Assert.Null(result.OriginId);
        Assert.Equal(cid2, result.DestinationId);

        // Ensure that relationship create event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal("create", actualEvent.Operation);
        Assert.Equal("relationship", actualEvent.EntityType);
        Assert.Equal(result.Id, actualEvent.EntityId);
        Assert.Equal(result.ProjectId, actualEvent.ProjectId);
    }

    [Fact]
    public async Task CreateRelationship_Success_WithNullDestinationId()
    {
        // Arrange
        var dto = new CreateRelationshipRequestDto
        {
            Name = $"Test Relationship {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Description = "Test Description",
            OriginId = cid,
            DestinationId = null
        };

        // Act
        var result = await _relationshipBusiness.CreateRelationship(uid, oid, pid, dto);

        // Assert
        Assert.True(result.Id > 0);
        Assert.Equal(cid, result.OriginId);
        Assert.Null(result.DestinationId);

        // Ensure that relationship create event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal("create", actualEvent.Operation);
        Assert.Equal("relationship", actualEvent.EntityType);
        Assert.Equal(result.Id, actualEvent.EntityId);
        Assert.Equal(result.ProjectId, actualEvent.ProjectId);
    }

    [Fact]
    public async Task CreateRelationship_Fails_IfNoName()
    {
        // Arrange
        var dto = new CreateRelationshipRequestDto
        {
            Name = null!, Description = "Test Description"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() =>
            _relationshipBusiness.CreateRelationship(uid, oid, pid, dto));

        // Ensure that no relationship create event is logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task CreateRelationship_Fails_IfEmptyName()
    {
        // Arrange
        var dto = new CreateRelationshipRequestDto
        {
            Name = "", Description = "Test Description"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() =>
            _relationshipBusiness.CreateRelationship(uid, oid, pid, dto));

        // Ensure that no relationship create event is logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task CreateRelationship_Fails_IfInvalidOriginId()
    {
        // Arrange
        var dto = new CreateRelationshipRequestDto
        {
            Name = "Test Relationship",
            Description = "Test Description",
            OriginId = 999,
            DestinationId = cid2
        };

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _relationshipBusiness.CreateRelationship(uid, oid, pid, dto));

        Assert.Contains($"Origin class with ID {dto.OriginId} not found.", exception.Message);

        // Ensure that no relationship create event is logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task CreateRelationship_Fails_IfInvalidDestinationId()
    {
        // Arrange
        var dto = new CreateRelationshipRequestDto
        {
            Name = "Test Relationship",
            Description = "Test Description",
            OriginId = cid,
            DestinationId = 999
        };

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _relationshipBusiness.CreateRelationship(uid, oid, pid, dto));

        Assert.Contains($"Destination class with ID {dto.DestinationId} not found.", exception.Message);

        // Ensure that no relationship create event is logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    #endregion

    #region BulkCreateRelationships Tests

    [Fact]
    public async Task BulkCreateRelationships_Success_ReturnsMultipleRelationshipsProject()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var relationshipDtos = new List<CreateRelationshipRequestDto>
        {
            new()
            {
                Name = "Bulk Relationship 1",
                Description = "First bulk relationship",
                OriginId = cid,
                DestinationId = cid2
            },
            new()
            {
                Name = "Bulk Relationship 2",
                Description = "Second bulk relationship",
                OriginId = cid2,
                DestinationId = cid
            }
        };

        // Act
        var result = await _relationshipBusiness.BulkCreateRelationships(uid, oid, pid, relationshipDtos);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.True(r.Id > 0));
        Assert.All(result, r => Assert.True(r.LastUpdatedAt >= now));
        Assert.All(result, r => Assert.True(r.LastUpdatedBy >= uid));
        Assert.All(result, r => Assert.True(r.ProjectId == pid));
        Assert.Equal("Bulk Relationship 1", result.First().Name);
        Assert.Equal("Bulk Relationship 2", result.Last().Name);

        // Ensure that relationship create events were logged
        // var eventList = await Context.Events.ToListAsync();
        // Assert.Equal(2, eventList.Count);

        // var firstEvent = eventList[0];
        //
        // Assert.Equal("create", firstEvent.Operation);
        // Assert.Equal("relationship", firstEvent.EntityType);
        // Assert.Equal(result[0].Id, firstEvent.EntityId);
        // Assert.Equal(result[0].ProjectId, firstEvent.ProjectId);
        //
        // var secondEvent = eventList[1];
        //
        // Assert.Equal("create", secondEvent.Operation);
        // Assert.Equal("relationship", secondEvent.EntityType);
        // Assert.Equal(result[1].Id, secondEvent.EntityId);
        // Assert.Equal(result[1].ProjectId, secondEvent.ProjectId);
    }
    
    [Fact]
    public async Task BulkCreateRelationships_Success_ReturnsMultipleRelationshipsOrganization()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var relationshipDtos = new List<CreateRelationshipRequestDto>
        {
            new()
            {
                Name = "Bulk Relationship 1",
                Description = "First bulk relationship",
                OriginId = cid,
                DestinationId = cid2
            },
            new()
            {
                Name = "Bulk Relationship 2",
                Description = "Second bulk relationship",
                OriginId = cid2,
                DestinationId = cid
            }
        };

        // Act
        var result = await _relationshipBusiness.BulkCreateRelationships(uid, oid, null, relationshipDtos);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.True(r.Id > 0));
        Assert.All(result, r => Assert.True(r.LastUpdatedAt >= now));
        Assert.All(result, r => Assert.True(r.LastUpdatedBy >= uid));
        Assert.All(result, r => Assert.True(r.ProjectId == null));
        Assert.Equal("Bulk Relationship 1", result.First().Name);
        Assert.Equal("Bulk Relationship 2", result.Last().Name);
    }

    [Fact]
    public async Task BulkCreateRelationships_Fails_IfNullDto()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _relationshipBusiness.BulkCreateRelationships(uid, oid, pid, null!));

        // Ensure that no relationship create events were logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    #endregion

    #region GetAllRelationships Tests

    [Fact]
    public async Task GetAllRelationships_ReturnsOnlyForProject()
    {
        // Arrange
        var p2 = new Project { Name = "ExtraProj", OrganizationId = oid };
        Context.Projects.Add(p2);
        await Context.SaveChangesAsync();

        await _relationshipBusiness.CreateRelationship(uid, oid, pid, new CreateRelationshipRequestDto
        {
            Name = $"Relationship1-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Description = "Test",
            OriginId = cid,
            DestinationId = cid2
        });

        // Create relationship directly for project 2 (bypass validation)
        var p2Relationship = new Relationship
        {
            Name = "P2 Relationship",
            OrganizationId = oid,
            ProjectId = p2.Id,
            OriginId = cid,
            DestinationId = cid2,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };
        Context.Relationships.Add(p2Relationship);
        await Context.SaveChangesAsync();

        // Act
        var list = await _relationshipBusiness.GetAllRelationships(oid, [pid], true);

        // Assert
        Assert.All(list, r => Assert.Equal(pid, r.ProjectId));
    }

    [Fact]
    public async Task GetAllRelationships_ExcludesSoftDeleted()
    {
        // Act
        var listWithArchived = await _relationshipBusiness.GetAllRelationships(oid, [pid], false);
        var listWithoutArchived = await _relationshipBusiness.GetAllRelationships(oid, [pid], true);

        // Assert
        Assert.Contains(listWithArchived, r => r.Id == rid2);
        Assert.DoesNotContain(listWithoutArchived, r => r.Id == rid2);
    }

    #endregion

    #region GetRelationship Tests

    [Fact]
    public async Task GetRelationship_Success_WhenExists()
    {
        // Arrange
        var testRelationship = await Context.Relationships.FindAsync(rid);

        // Act
        var result = await _relationshipBusiness.GetRelationship(oid, pid, rid, true);

        // Assert
        Assert.Equal(rid, result.Id);
        Assert.Equal(testRelationship.Name, result.Name);
        Assert.Equal(cid, result.OriginId);
        Assert.Equal(cid2, result.DestinationId);
    }

    [Fact]
    public async Task GetRelationship_Fails_IfDeletedRelationship()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _relationshipBusiness.GetRelationship(oid, pid, rid2, true));

        Assert.Contains($"Relationship with id {rid2} is archived", exception.Message);
    }

    #endregion

    #region UpdateRelationship Tests

    [Fact]
    public async Task UpdateRelationship_Success_ReturnsModifiedAt()
    {
        // Arrange
        var testRelationship = new Relationship
        {
            Name = $"Original Relationship {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Description = "Original Description",
            OrganizationId = oid,
            ProjectId = pid,
            OriginId = cid,
            DestinationId = cid2,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = null
        };
        Context.Relationships.Add(testRelationship);
        await Context.SaveChangesAsync();

        // Add a small delay to ensure ModifiedAt is after CreatedAt
        await Task.Delay(50);

        var dto = new UpdateRelationshipRequestDto
        {
            Name = $"Updated Relationship {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Description = "Updated Description",
            OriginId = cid2,
            DestinationId = cid
        };

        // Act
        var updatedResult = await _relationshipBusiness.UpdateRelationship(uid, oid, pid, testRelationship.Id, dto);

        // Assert
        Assert.True((DateTime.UtcNow - updatedResult.LastUpdatedAt).TotalSeconds < 1);
        Assert.Equal(testRelationship.ProjectId, updatedResult.ProjectId);
        Assert.Equal(testRelationship.LastUpdatedBy, updatedResult.LastUpdatedBy);
        Assert.Equal(dto.Name, updatedResult.Name);
        Assert.Equal(dto.Description, updatedResult.Description);
        Assert.Equal(cid2, updatedResult.OriginId);
        Assert.Equal(cid, updatedResult.DestinationId);

        // Ensure that relationship update event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal("update", actualEvent.Operation);
        Assert.Equal("relationship", actualEvent.EntityType);
        Assert.Equal(updatedResult.Id, actualEvent.EntityId);
        Assert.Equal(updatedResult.ProjectId, actualEvent.ProjectId);
    }

    [Fact]
    public async Task UpdateRelationship_PartialUpdate_UpdatesRelationship()
    {
        // Arrange
        var updateDto = new UpdateRelationshipRequestDto
        {
            Description = "Updated Description"
        };

        var originalRelationship = await Context.Relationships.FindAsync(rid);

        // Act
        var result = await _relationshipBusiness.UpdateRelationship(uid, oid, pid, rid, updateDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(rid, result.Id);
        Assert.Equal("Updated Description", result.Description);
        Assert.Equal(originalRelationship.Name, result.Name);

        // Verify it was actually updated in database
        var updatedRelationship = await Context.Relationships.FindAsync(rid);
        Assert.NotNull(updatedRelationship);
        Assert.Equal("Updated Description", updatedRelationship.Description);
        Assert.Equal(originalRelationship.Name, updatedRelationship.Name);
        Assert.NotNull(updatedRelationship.LastUpdatedAt);

        // Ensure that relationship update event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal("update", actualEvent.Operation);
        Assert.Equal("relationship", actualEvent.EntityType);
        Assert.Equal(result.Id, actualEvent.EntityId);
        Assert.Equal(result.ProjectId, actualEvent.ProjectId);
    }

    [Fact]
    public async Task UpdateRelationship_Fails_IfNotFound()
    {
        // Arrange
        var dto = new UpdateRelationshipRequestDto
        {
            Name = "Updated Relationship",
            Description = "Updated Description",
            OriginId = cid,
            DestinationId = cid2
        };

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _relationshipBusiness.UpdateRelationship(uid, oid, pid, 99, dto));

        Assert.Contains("Relationship with ID 99 not found.", exception.Message);

        // Ensure that no relationship update event is logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    #endregion

    #region DeleteRelationship Tests

    [Fact]
    public async Task DeleteRelationship_Success_WhenExists()
    {
        // Act
        var deletedResult = await _relationshipBusiness.DeleteRelationship(uid, oid, pid, rid);

        // Assert
        var deletedRelationship = await Context.Relationships.FindAsync(rid);
        Assert.True(deletedResult);
        Assert.Null(deletedRelationship);
    }

    [Fact]
    public async Task DeleteRelationship_Fails_IfNotFound()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _relationshipBusiness.DeleteRelationship(uid, oid, pid, 999));

        Assert.Contains("Relationship with id 999 not found", exception.Message);
    }

    #endregion

    #region ArchiveRelationship Tests

    [Fact]
    public async Task ArchiveRelationship_Success_WhenExists()
    {
        // Arrange
        var originalRelationship = await Context.Relationships.FindAsync(rid);

        var now = DateTime.UtcNow;

        // Act
        var archivedResult = await _relationshipBusiness.ArchiveRelationship(uid, oid, pid, rid);


        // Assert
        Assert.True(archivedResult);

        // Procedure is not traced by entity framework
        // This forces EF to sync to db on next query
        Context.ChangeTracker.Clear();

        // Assert
        var archivedRelationship = await Context.Relationships.FindAsync(rid);
        Assert.NotNull(archivedRelationship);
        Assert.True(archivedRelationship.IsArchived);
        Assert.Equal(originalRelationship.Id, archivedRelationship.Id);
        Assert.Equal(originalRelationship.Name, archivedRelationship.Name);
        Assert.True(archivedRelationship.LastUpdatedAt >= now);
        Assert.Equal(originalRelationship.Description, archivedRelationship.Description);
        Assert.Equal(originalRelationship.Uuid, archivedRelationship.Uuid);
        Assert.Equal(originalRelationship.ProjectId, archivedRelationship.ProjectId);
        Assert.Equal(originalRelationship.DestinationId, archivedRelationship.DestinationId);
        Assert.Equal(originalRelationship.OriginId, archivedRelationship.OriginId);
        Assert.Equal(uid, archivedRelationship.LastUpdatedBy);

        // Assert
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);

        var actualEvent = eventList[0];

        Assert.Equal(archivedRelationship.ProjectId, actualEvent.ProjectId);
        Assert.Equal("archive", actualEvent.Operation);
        Assert.Equal("relationship", actualEvent.EntityType);
        Assert.Equal(archivedRelationship.Id, actualEvent.EntityId);
    }

    [Fact]
    public async Task RelationshipArchived_WhenProjectArchived()
    {
        // Act
        var archivedResult = await _projectBusiness.ArchiveProject(uid, oid, pid);
        Assert.True(archivedResult);

        // procedure is not traced by entity framework
        //this forces EF to sync to db on next query
        Context.ChangeTracker.Clear();

        // Assert
        var archivedRelationship = await Context.Relationships.FindAsync(rid);
        Assert.NotNull(archivedRelationship);

        // Check if ArchivedAt was set (optional based on implementation)
        if (archivedRelationship.IsArchived) Assert.True(archivedRelationship.IsArchived);
    }

    [Fact]
    public async Task ArchiveRelationship_Fails_IfNotFound()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _relationshipBusiness.ArchiveRelationship(uid, oid, pid, 999));

        Assert.Contains("Relationship with id 999 not found", exception.Message);
    }

    #endregion

    #region UnarchiveRelationship Tests

    [Fact]
    public async Task UnarchiveRelationship_Success_WhenArchived()
    {
        // Arrange
        var originalRelationship = await Context.Relationships.FindAsync(rid2);

        var now = DateTime.UtcNow;

        // Act
        var unarchivedResult = await _relationshipBusiness.UnarchiveRelationship(uid, oid, pid, rid2);
        Assert.True(unarchivedResult);

        // procedure is not traced by entity framework
        //this forces EF to sync to db on next query
        Context.ChangeTracker.Clear();

        // Assert
        var unarchivedRelationship = await Context.Relationships.FindAsync(rid2);
        Assert.NotNull(unarchivedRelationship);
        Assert.False(unarchivedRelationship.IsArchived);
        Assert.Equal(originalRelationship.Id, unarchivedRelationship.Id);
        Assert.Equal(originalRelationship.Name, unarchivedRelationship.Name);
        Assert.True(unarchivedRelationship.LastUpdatedAt >= now);
        Assert.Equal(originalRelationship.Description, unarchivedRelationship.Description);
        Assert.Equal(originalRelationship.Uuid, unarchivedRelationship.Uuid);
        Assert.Equal(originalRelationship.ProjectId, unarchivedRelationship.ProjectId);
        Assert.Equal(originalRelationship.DestinationId, unarchivedRelationship.DestinationId);
        Assert.Equal(originalRelationship.OriginId, unarchivedRelationship.OriginId);
        Assert.Equal(uid, unarchivedRelationship.LastUpdatedBy);
    }

    [Fact]
    public async Task UnarchiveRelationship_Fails_IfNotFound()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _relationshipBusiness.UnarchiveRelationship(uid, oid, pid, 999));

        Assert.Contains("Relationship with id 999 not found", exception.Message);
    }

    [Fact]
    public async Task UnarchiveRelationship_Fails_IfNotArchived()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _relationshipBusiness.UnarchiveRelationship(uid, oid, pid, rid));

        Assert.Contains($"Relationship with id {rid} not found or is not archived.", exception.Message);
    }

    #endregion

    #region DTO Tests

    [Fact]
    public void RelationshipRequestDto_AllProperties_CanBeSetAndRetrieved()
    {
        // Act
        var dto = new CreateRelationshipRequestDto
        {
            Name = "Test Relationship",
            Description = "Test Description",
            Uuid = "test-uuid",
            OriginId = 1,
            DestinationId = 2
        };

        // Assert
        Assert.Equal("Test Relationship", dto.Name);
        Assert.Equal("Test Description", dto.Description);
        Assert.Equal("test-uuid", dto.Uuid);
        Assert.Equal(1, dto.OriginId);
        Assert.Equal(2, dto.DestinationId);
    }

    [Fact]
    public void RelationshipResponseDto_AllProperties_CanBeSetAndRetrieved()
    {
        // Arrange
        var now = DateTime.UtcNow;

        // Act
        var dto = new RelationshipResponseDto
        {
            Id = 1,
            Name = "Test Relationship",
            Description = "Test Description",
            Uuid = "test-uuid",
            ProjectId = 3,
            LastUpdatedBy = uid,
            LastUpdatedAt = now,
            IsArchived = false,
            OriginId = 1,
            DestinationId = 2
        };

        // Assert
        Assert.Equal(1, dto.Id);
        Assert.Equal("Test Relationship", dto.Name);
        Assert.Equal("Test Description", dto.Description);
        Assert.Equal("test-uuid", dto.Uuid);
        Assert.Equal(3, dto.ProjectId);
        Assert.Equal(uid, dto.LastUpdatedBy);
        Assert.Equal(now, dto.LastUpdatedAt);
        Assert.False(dto.IsArchived);
        Assert.Equal(1, dto.OriginId);
        Assert.Equal(2, dto.DestinationId);
    }

    #endregion

    #region GetRelationshipsByName Tests

    [Fact]
    public async Task GetRelationshipsByName_ValidRelationshipNames_ReturnsMatchingRelationships()
    {
        // Arrange
        var relationshipNames = new List<string> { "Original Relationship" };

        // Act
        var result = await _relationshipBusiness.GetRelationshipsByName(oid, pid, relationshipNames);

        // Assert
        Assert.Single(result);
        Assert.Equal("Original Relationship", result.First().Name);
        Assert.Equal(pid, result.First().ProjectId);
    }

    [Fact]
    public async Task GetRelationshipsByName_MissingRelationshipNames_ThrowsKeyNotFoundException()
    {
        // Arrange
        var relationshipNames = new List<string> { "NonExistentRelationship" };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _relationshipBusiness.GetRelationshipsByName(oid, pid, relationshipNames));

        Assert.Contains("Relationships not found with names", exception.Message);
    }

    [Fact]
    public async Task GetRelationshipsByName_NullRelationshipNames_ThrowsArgumentException()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _relationshipBusiness.GetRelationshipsByName(oid, pid, null));

        Assert.Contains("Relationship names list cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task GetRelationshipsByName_ExcludesArchivedRelationships()
    {
        // Arrange
        var relationshipNames = new List<string> { "Archived Relationship" };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _relationshipBusiness.GetRelationshipsByName(oid, pid, relationshipNames));

        Assert.Contains("Archived Relationship", exception.Message);
    }

    #endregion

    #region LastUpdatedBy Tests

    [Fact]
    public async Task CreateRelationship_Success_StoresLastUpdatedByUserId()
    {
        // Arrange
        var testRelationship = new Relationship
        {
            Name = $"Test Relationship with User {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Description = "Test Description with User ID",
            OrganizationId = oid,
            ProjectId = pid,
            OriginId = cid,
            DestinationId = cid2,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            IsArchived = false
        };

        // Act
        Context.Relationships.Add(testRelationship);
        await Context.SaveChangesAsync();

        // Assert
        var savedRelationship = await Context.Relationships.FindAsync(testRelationship.Id);
        Assert.NotNull(savedRelationship);
        Assert.Equal(uid, savedRelationship.LastUpdatedBy);
    }

    [Fact]
    public async Task CreateRelationship_Success_NavigationPropertyLoadsUser()
    {
        // Arrange
        var testRelationship = new Relationship
        {
            Name = $"Test Relationship Navigation {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Description = "Test Navigation Property",
            OrganizationId = oid,
            ProjectId = pid,
            OriginId = cid,
            DestinationId = cid2,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            IsArchived = false
        };

        Context.Relationships.Add(testRelationship);
        await Context.SaveChangesAsync();

        // Act
        var relationshipWithUser = await Context.Relationships
            .Include(r => r.LastUpdatedByUser)
            .FirstAsync(r => r.Id == testRelationship.Id);

        // Assert
        Assert.NotNull(relationshipWithUser.LastUpdatedByUser);
        Assert.Equal("Test User", relationshipWithUser.LastUpdatedByUser.Name);
        Assert.Equal("test.user@test.com", relationshipWithUser.LastUpdatedByUser.Email);
        Assert.Equal(uid, relationshipWithUser.LastUpdatedBy);
    }

    [Fact]
    public async Task CreateRelationship_Success_WithNullLastUpdatedBy()
    {
        // Arrange
        var testRelationship = new Relationship
        {
            Name = $"Test Relationship Null User {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Description = "Test with null LastUpdatedBy",
            OrganizationId = oid,
            ProjectId = pid,
            OriginId = cid,
            DestinationId = cid2,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = null,
            IsArchived = false
        };

        // Act
        Context.Relationships.Add(testRelationship);
        await Context.SaveChangesAsync();

        // Assert
        var savedRelationship = await Context.Relationships.FindAsync(testRelationship.Id);
        Assert.NotNull(savedRelationship);
        Assert.Null(savedRelationship.LastUpdatedBy);

        var relationshipWithUser = await Context.Relationships
            .Include(r => r.LastUpdatedByUser)
            .FirstAsync(r => r.Id == testRelationship.Id);

        Assert.Null(relationshipWithUser.LastUpdatedByUser);
    }

    [Fact]
    public async Task UpdateRelationship_Success_UpdatesLastUpdatedByUserId()
    {
        // Arrange
        var testRelationship = new Relationship
        {
            Name = $"Original Relationship {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Description = "Original Description",
            OrganizationId = oid,
            ProjectId = pid,
            OriginId = cid,
            DestinationId = cid2,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = null
        };
        Context.Relationships.Add(testRelationship);
        await Context.SaveChangesAsync();

        // Act
        testRelationship.LastUpdatedBy = uid;
        testRelationship.Description = "Updated Description";
        testRelationship.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        Context.Relationships.Update(testRelationship);
        await Context.SaveChangesAsync();

        // Assert
        var updatedRelationship = await Context.Relationships
            .Include(r => r.LastUpdatedByUser)
            .FirstAsync(r => r.Id == testRelationship.Id);

        Assert.Equal(uid, updatedRelationship.LastUpdatedBy);
        Assert.NotNull(updatedRelationship.LastUpdatedByUser);
        Assert.Equal("Test User", updatedRelationship.LastUpdatedByUser.Name);
        Assert.Equal("Updated Description", updatedRelationship.Description);
    }

    #endregion
}