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
using Record = deeplynx.datalayer.Models.Record;

namespace deeplynx.tests;

[Collection("Test Suite Collection")]
public class ClassBusinessTests : IntegrationTestBase
{
    private ClassBusiness _classBusiness = null!;
    private Mock<IDataSourceBusiness> _dataSourceBusiness = null!;
    private EventBusiness _eventBusiness = null!;
    private Mock<IHubContext<EventNotificationHub>> _mockHubContext = null!;
    private Mock<ILogger<ProjectBusiness>> _mockLogger = null!;
    private Mock<ILogger<NotificationBusiness>> _mockNotificationLogger = null!;
    private INotificationBusiness _notificationBusiness = null!;
    private Mock<IObjectStorageBusiness> _objectStorageBusiness = null!;
    private Mock<IOrganizationBusiness> _organizationBusiness = null!;
    private ProjectBusiness _projectBusiness = null!;
    private Mock<IRecordBusiness> _recordBusiness = null!;
    private Mock<IRelationshipBusiness> _relationshipBusiness = null!;
    private Mock<IRoleBusiness> _roleBusiness = null!;

    public long cid1; // class IDs
    public long cid2;
    public long cid3;
    public long cid4;
    public long cid5;
    public long dsid; // data source ID
    public long oid; // organization IDs
    public long oid2;
    public long pid; // project ID
    public long pid2; // project 2 ID
    public long relid1; // relationship IDs
    public long relid2;
    public long relid3;
    public long relid4;
    public long relid5;
    public long rid1; // record IDs
    public long rid2;
    public long uid; // user ID

    public ClassBusinessTests(TestSuiteFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _recordBusiness = new Mock<IRecordBusiness>();
        _relationshipBusiness = new Mock<IRelationshipBusiness>();
        _dataSourceBusiness = new Mock<IDataSourceBusiness>();
        _mockLogger = new Mock<ILogger<ProjectBusiness>>();
        _mockHubContext = new Mock<IHubContext<EventNotificationHub>>();
        _mockNotificationLogger = new Mock<ILogger<NotificationBusiness>>();
        _notificationBusiness =
            new NotificationBusiness(Context, _mockNotificationLogger.Object, _mockHubContext.Object);
        _eventBusiness = new EventBusiness(Context, _notificationBusiness);
        _objectStorageBusiness = new Mock<IObjectStorageBusiness>();
        _roleBusiness = new Mock<IRoleBusiness>();
        _organizationBusiness = new Mock<IOrganizationBusiness>();

        _classBusiness = new ClassBusiness(
            Context, _recordBusiness.Object,
            _relationshipBusiness.Object, _eventBusiness);

        _projectBusiness = new ProjectBusiness(
            Context, _mockLogger.Object,
            _classBusiness, _roleBusiness.Object, _dataSourceBusiness.Object,
            _objectStorageBusiness.Object, _eventBusiness, _organizationBusiness.Object);
    }

    protected override async Task SeedTestDataAsync()
    {
        await base.SeedTestDataAsync();

        // Create user
        var user = new User
        {
            Name = "Test User",
            Email = "test.user@test.com",
            Password = "test_password",
            IsArchived = false
        };
        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        uid = user.Id;

        // Create organization
        var org = new Organization
        {
            Name = "Test Org",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        var org2 = new Organization
        {
            Name = "Deleted Org",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        Context.Organizations.AddRange(org, org2);
        await Context.SaveChangesAsync();
        oid = org.Id;
        oid2 = org2.Id;

        // Create projects
        var project1 = new Project
        {
            Name = "Project 1",
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        var project2 = new Project
        {
            Name = "Project 2",
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        Context.Projects.AddRange(project1, project2);
        await Context.SaveChangesAsync();
        pid = project1.Id;
        pid2 = project2.Id;

        // Create data source
        var dataSource = new DataSource
        {
            Name = "Test Datasource",
            ProjectId = pid,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        Context.DataSources.Add(dataSource);
        await Context.SaveChangesAsync();
        dsid = dataSource.Id;

        // Create classes
        var class1 = new Class
        {
            Name = "Class 1",
            Description = "Description 1",
            Uuid = "uuid-1",
            ProjectId = pid,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            IsArchived = false
        };
        var class2 = new Class
        {
            Name = "Class 2",
            Description = "Description 2",
            ProjectId = pid,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            IsArchived = true // Archived
        };
        var class3 = new Class
        {
            Name = "Class 3",
            ProjectId = pid,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            IsArchived = false
        };
        var class4 = new Class
        {
            Name = "Class 4",
            ProjectId = pid2,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            IsArchived = false
        };
        var class5 = new Class
        {
            Name = "Class 5",
            ProjectId = pid,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            IsArchived = false
        };
        Context.Classes.AddRange(class1, class2, class3, class4, class5);
        await Context.SaveChangesAsync();
        cid1 = class1.Id;
        cid2 = class2.Id;
        cid3 = class3.Id;
        cid4 = class4.Id;
        cid5 = class5.Id;

        // Delete class3 (hard delete for testing)
        Context.Classes.Remove(class3);
        await Context.SaveChangesAsync();

        // Create records
        var record1 = new Record
        {
            Name = "Record 1",
            ClassId = cid5,
            DataSourceId = dsid,
            ProjectId = pid,
            OrganizationId = oid,
            OriginalId = "og1",
            Description = "Test Description 1",
            Properties = "{\"test\": \"value1\"}",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        var record2 = new Record
        {
            Name = "Record 2",
            ClassId = cid5,
            DataSourceId = dsid,
            ProjectId = pid,
            OriginalId = "og2",
            OrganizationId = oid,
            Description = "Test Description 2",
            Properties = "{\"test\": \"value2\"}",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        Context.Records.AddRange(record1, record2);
        await Context.SaveChangesAsync();
        rid1 = record1.Id;
        rid2 = record2.Id;

        // Create relationships
        var rel1 = new Relationship
        {
            Name = "Relationship 1",
            OriginId = cid1,
            DestinationId = cid4,
            ProjectId = pid,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        var rel2 = new Relationship
        {
            Name = "Relationship 2",
            OriginId = cid4,
            DestinationId = cid1,
            ProjectId = pid,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        var rel3 = new Relationship
        {
            Name = "Relationship 3",
            OriginId = cid1,
            DestinationId = null,
            ProjectId = pid,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        var rel4 = new Relationship
        {
            Name = "Relationship 4",
            OriginId = null,
            DestinationId = cid1,
            ProjectId = pid,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        var rel5 = new Relationship
        {
            Name = "Relationship 5",
            OriginId = null,
            DestinationId = null,
            ProjectId = pid,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        Context.Relationships.AddRange(rel1, rel2, rel3, rel4, rel5);
        await Context.SaveChangesAsync();
        relid1 = rel1.Id;
        relid2 = rel2.Id;
        relid3 = rel3.Id;
        relid4 = rel4.Id;
        relid5 = rel5.Id;
    }

    #region CreateClass Tests

    [Fact]
    public async Task CreateClass_Success_ReturnsCorrectValues()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var dto = new CreateClassRequestDto
        {
            Name = $"New Class {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Description = "New Description",
            Uuid = $"new-uuid-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}"
        };

        // Act
        var result = await _classBusiness.CreateClass(uid, oid, pid, dto);

        // Assert
        Assert.True(result.Id > 0);
        Assert.True(result.LastUpdatedAt >= now);
        Assert.Equal(dto.Name, result.Name);
        Assert.Equal(dto.Description, result.Description);
        Assert.Equal(dto.Uuid, result.Uuid);
        Assert.Equal(pid, result.ProjectId);
        Assert.Equal(oid, result.OrganizationId);
        Assert.Equal(uid, result.LastUpdatedBy);
        Assert.False(result.IsArchived);

        // Ensure the create event is logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);
        Assert.Equal("create", eventList[0].Operation);
        Assert.Equal("class", eventList[0].EntityType);
        Assert.Equal(result.Id, eventList[0].EntityId);
    }

    [Fact]
    public async Task CreateClasses_Success_OnBulkCreate()
    {
        // Arrange
        var bulkDto = new List<CreateClassRequestDto>
        {
            new()
            {
                Name = "Bulk Class 1",
                Description = "Bulk Description 1",
                Uuid = $"bulk-uuid-1-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}"
            },
            new()
            {
                Name = "Bulk Class 2",
                Description = "Bulk Description 2",
                Uuid = $"bulk-uuid-2-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}"
            }
        };

        // Act
        var result = await _classBusiness.BulkCreateClasses(uid, oid, pid, bulkDto);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("Bulk Class 1", result[0].Name);
        Assert.Equal("Bulk Class 2", result[1].Name);
        Assert.All(result, r => Assert.Equal(uid, r.LastUpdatedBy));
        Assert.All(result, r => Assert.Equal(oid, r.OrganizationId));
        Assert.All(result, r => Assert.Equal(pid, r.ProjectId));
    }

    [Fact]
    public async Task BulkCreateClasses_Success_OnNameCollision_UpdatesDescription()
    {
        // Arrange - Use seeded class1
        var bulkDto = new List<CreateClassRequestDto>
        {
            new()
            {
                Name = "Class 1", // Matches seeded class1
                Description = "Updated Description"
            }
        };
    }

    [Fact]
    public async Task CreateClass_Fails_IfNoName()
    {
        // Arrange
        var dto = new CreateClassRequestDto { Name = null, Description = "Test Description" };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => _classBusiness.CreateClass(uid, oid, pid, dto));

        // Ensure that no events were created on failed class creation
        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task CreateClass_Fails_IfEmptyName()
    {
        // Arrange
        var dto = new CreateClassRequestDto { Name = "", Description = "Test Description" };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => _classBusiness.CreateClass(uid, oid, pid, dto));

        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task CreateClass_Fails_IfNoProjectId()
    {
        // Arrange
        var dto = new CreateClassRequestDto { Name = "Test Class", Description = "Test Description" };

        // Act & Assert
        await Assert.ThrowsAsync<DbUpdateException>(() => _classBusiness.CreateClass(
            uid, oid, 99999, dto));

        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task CreateClass_Fails_IfDeletedProjectId_AndDeletedOrgId()
    {
        // Arrange
        var project = await Context.Projects.FindAsync(pid);
        Context.Projects.Remove(project);
        var org = await Context.Organizations.FindAsync(oid);
        Context.Organizations.Remove(org);
        await Context.SaveChangesAsync();

        var dto = new CreateClassRequestDto { Name = "Test Class", Description = "Test Description" };

        // Act & Assert
        await Assert.ThrowsAsync<DbUpdateException>(() =>
            _classBusiness.CreateClass(uid, oid, pid, dto));

        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task CreateClass_Fails_IfDuplicateName()
    {
        // Arrange - Try to create duplicate of seeded class1
        var dto = new CreateClassRequestDto { Name = "Class 1", Description = "Duplicate" };

        // Act & Assert
        await Assert.ThrowsAsync<DbUpdateException>(() => _classBusiness.CreateClass(uid, oid, pid, dto));

        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    #endregion

    #region GetAllClasses Tests

    [Fact]
    public async Task GetAllClasses_ReturnsOnlyForProjects()
    {
        // Act - Get classes for pid only
        var list = await _classBusiness.GetAllClasses(oid, new[] { pid }, true);

        // Assert - Should get class1 and class5 (not class2 which is archived, not class4 which is in pid2)
        Assert.Equal(2, list.Count);
        Assert.Contains(list, c => c.Id == cid1);
        Assert.Contains(list, c => c.Id == cid5);
        Assert.DoesNotContain(list, c => c.Id == cid2);
        Assert.DoesNotContain(list, c => c.Id == cid4);
    }

    [Fact]
    public async Task GetAllClasses_ExcludesSoftDeleted()
    {
        // Act
        var list = await _classBusiness.GetAllClasses(oid, new[] { pid }, true);

        // Assert - class2 is archived, should not be returned
        Assert.DoesNotContain(list, c => c.Id == cid2);
        Assert.All(list, c => Assert.False(c.IsArchived));
    }

    [Fact]
    public async Task GetAllClasses_ValidProjectIds_ReturnsClassesFromAllProjects()
    {
        // Act
        var result = await _classBusiness.GetAllClasses(oid, new[] { pid, pid2 }, true);

        // Assert - Should get class1, class4, class5 (not class2 which is archived)
        Assert.Equal(3, result.Count);
        Assert.Contains(result, c => c.Id == cid1 && c.ProjectId == pid);
        Assert.Contains(result, c => c.Id == cid4 && c.ProjectId == pid2);
        Assert.Contains(result, c => c.Id == cid5 && c.ProjectId == pid);
    }

    [Fact]
    public async Task GetAllClasses_NonExistentProjectIds_ReturnsEmptyList()
    {
        // Act
        var result = await _classBusiness.GetAllClasses(oid, new long[] { 999, 998 }, true);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllClasses_HideArchivedFalse_ReturnsArchivedClasses()
    {
        // Act
        var result = await _classBusiness.GetAllClasses(oid, new[] { pid }, false);

        // Assert - Should include archived class2
        Assert.Contains(result, c => c.Id == cid2 && c.IsArchived);
    }

    [Fact]
    public async Task GetAllClasses_HideArchivedTrue_ExcludesArchivedClasses()
    {
        // Act
        var result = await _classBusiness.GetAllClasses(oid, new[] { pid }, true);

        // Assert
        Assert.DoesNotContain(result, c => c.Id == cid2);
        Assert.All(result, c => Assert.False(c.IsArchived));
    }

    [Fact]
    public async Task GetAllClasses_ReturnsAllProperties_Correctly()
    {
        // Act
        var result = await _classBusiness.GetAllClasses(oid, new[] { pid }, false);
        var class1Dto = result.First(c => c.Id == cid1);

        // Assert
        Assert.Equal(cid1, class1Dto.Id);
        Assert.Equal("Class 1", class1Dto.Name);
        Assert.Equal("Description 1", class1Dto.Description);
        Assert.Equal("uuid-1", class1Dto.Uuid);
        Assert.Equal(pid, class1Dto.ProjectId);
        Assert.Equal(oid, class1Dto.OrganizationId);
        Assert.Equal(uid, class1Dto.LastUpdatedBy);
        Assert.False(class1Dto.IsArchived);
    }

    #endregion

    #region GetClass Tests

    [Fact]
    public async Task GetClass_Success_WhenExists()
    {
        // Act
        var result = await _classBusiness.GetClass(oid, pid, cid1, true);

        // Assert
        Assert.Equal(cid1, result.Id);
        Assert.Equal("Class 1", result.Name);
        Assert.False(result.IsArchived);
    }

    [Fact]
    public async Task GetClass_Fails_IfNoProjectID()
    {
        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _classBusiness.GetClass(oid, 99999, cid1, true));
    }

    [Fact]
    public async Task GetClass_Fails_IfDeletedClass()
    {
        // Act & Assert - class3 was hard deleted
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _classBusiness.GetClass(oid, pid, cid3, true));
    }

    [Fact]
    public async Task GetClass_Fails_IfArchivedClass_AndHideArchivedTrue()
    {
        // Act & Assert - class2 is archived
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _classBusiness.GetClass(oid, pid, cid2, true));
    }

    [Fact]
    public async Task GetClass_Success_IfArchivedClass_AndHideArchivedFalse()
    {
        // Act
        var result = await _classBusiness.GetClass(oid, pid, cid2, false);

        // Assert
        Assert.Equal(cid2, result.Id);
        Assert.True(result.IsArchived);
    }

    [Fact]
    public async Task GetClass_Fails_IfWrongProject()
    {
        // Act & Assert - class4 belongs to pid2, not pid
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _classBusiness.GetClass(oid, pid, cid4, true));
    }

    #endregion

    #region UpdateClass Tests

    [Fact]
    public async Task UpdateClass_Success_ReturnsCorrectValues()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var dto = new UpdateClassRequestDto
        {
            Name = "Updated Class 1",
            Description = "Updated Description"
        };

        // Act
        var result = await _classBusiness.UpdateClass(uid, oid, pid, cid1, dto);

        // Assert
        Assert.Equal(cid1, result.Id);
        Assert.True(result.LastUpdatedAt >= now);
        Assert.Equal("Updated Class 1", result.Name);
        Assert.Equal("Updated Description", result.Description);
        Assert.Equal(pid, result.ProjectId);
        Assert.Equal(oid, result.OrganizationId);
        Assert.False(result.IsArchived);

        // Verify event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);
        Assert.Equal("update", eventList[0].Operation);
        Assert.Equal("class", eventList[0].EntityType);
        Assert.Equal(cid1, eventList[0].EntityId);
    }

    [Fact]
    public async Task UpdateClass_PartialUpdate_UpdatesClass()
    {
        // Arrange
        var dto = new UpdateClassRequestDto
        {
            Description = "Only Description Updated"
        };

        // Act
        var result = await _classBusiness.UpdateClass(uid, oid, pid, cid1, dto);

        // Assert
        Assert.Equal(cid1, result.Id);
        Assert.Equal("Class 1", result.Name); // Name unchanged
        Assert.Equal("Only Description Updated", result.Description);

        // Verify in database
        var updatedClass = await Context.Classes.FindAsync(cid1);
        Assert.Equal("Class 1", updatedClass.Name);
        Assert.Equal("Only Description Updated", updatedClass.Description);
    }

    [Fact]
    public async Task UpdateClass_Fails_IfNotFound()
    {
        // Arrange
        var dto = new UpdateClassRequestDto { Name = "Updated" };

        // Act & Assert - class3 was deleted
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _classBusiness.UpdateClass(uid, oid, pid, cid3, dto));

        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task UpdateClass_Fails_IfArchived()
    {
        // Arrange
        var dto = new UpdateClassRequestDto { Name = "Updated" };

        // Act & Assert - class2 is archived
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _classBusiness.UpdateClass(uid, oid, pid, cid2, dto));

        var eventList = await Context.Events.ToListAsync();
        Assert.Empty(eventList);
    }

    [Fact]
    public async Task UpdateClass_Fails_IfWrongProject()
    {
        // Arrange
        var dto = new UpdateClassRequestDto { Name = "Updated" };

        // Act & Assert - class4 belongs to pid2
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _classBusiness.UpdateClass(uid, oid, pid, cid4, dto));
    }

    #endregion

    #region ArchiveClass Tests

    [Fact]
    public async Task ArchiveClass_Success_WhenExists()
    {
        // Arrange
        var now = DateTime.UtcNow;

        // Act
        var result = await _classBusiness.ArchiveClass(uid, oid, pid, cid1);

        // Assert
        Assert.True(result);

        // Force EF to sync with database
        Context.ChangeTracker.Clear();

        var archivedClass = await Context.Classes.FindAsync(cid1);
        Assert.NotNull(archivedClass);
        Assert.True(archivedClass.IsArchived);
        Assert.True(archivedClass.LastUpdatedAt >= now);
        Assert.Equal(uid, archivedClass.LastUpdatedBy);

        // Verify event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);
        Assert.Equal("archive", eventList[0].Operation);
        Assert.Equal("class", eventList[0].EntityType);
        Assert.Equal(cid1, eventList[0].EntityId);
    }

    [Fact]
    public async Task ArchiveClass_Fails_IfNotFound()
    {
        // Act & Assert - class3 was deleted
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _classBusiness.ArchiveClass(uid, oid, pid, cid3));
    }

    [Fact]
    public async Task ArchiveClass_Fails_IfAlreadyArchived()
    {
        // Act & Assert - class2 is already archived
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _classBusiness.ArchiveClass(uid, oid, pid, cid2));
    }

    [Fact]
    public async Task ArchiveClass_Fails_IfWrongProject()
    {
        // Act & Assert - class4 belongs to pid2
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _classBusiness.ArchiveClass(uid, oid, pid, cid4));
    }

    [Fact]
    public async Task ClassArchived_WhenProjectArchived()
    {
        // Arrange
        var beforeArchive = DateTime.UtcNow;

        // Act
        var result = await _projectBusiness.ArchiveProject(uid, oid, pid);
        Assert.True(result);

        // Force EF to sync with database
        Context.ChangeTracker.Clear();

        // Assert - class1 should be archived
        var archivedClass = await Context.Classes.FindAsync(cid1);
        Assert.NotNull(archivedClass);
        Assert.True(archivedClass.IsArchived);
        Assert.True(archivedClass.LastUpdatedAt >= beforeArchive);
    }

    #endregion

    #region DeleteClass Tests

    [Fact]
    public async Task DeleteClass_Success_WhenExists()
    {
        // Act
        var result = await _classBusiness.DeleteClass(uid, oid, pid, cid1);

        // Assert
        Assert.True(result);

        var deletedClass = await Context.Classes.FindAsync(cid1);
        Assert.Null(deletedClass);

        // Verify event was logged
        var eventList = await Context.Events.ToListAsync();
        Assert.Single(eventList);
        Assert.Equal("delete", eventList[0].Operation);
        Assert.Equal("class", eventList[0].EntityType);
        Assert.Equal(cid1, eventList[0].EntityId);
    }

    [Fact]
    public async Task DeleteClass_Fails_IfNotFound()
    {
        // Act & Assert - class3 was already deleted
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _classBusiness.DeleteClass(uid, oid, pid, cid3));
    }

    [Fact]
    public async Task DeleteClass_Fails_IfWrongProject()
    {
        // Act & Assert - class4 belongs to pid2
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _classBusiness.DeleteClass(uid, oid, pid, cid4));
    }

    [Fact]
    public async Task DeleteClass_DeletesRelationshipsWithNullClass()
    {
        // Arrange - relid3 has cid1 as origin, null destination
        //          relid4 has null origin, cid1 as destination
        //          relid5 has null origin and destination

        // Act
        var result = await _classBusiness.DeleteClass(uid, oid, pid, cid1);
        Assert.True(result);

        // Assert
        var deletedClass = await Context.Classes.FindAsync(cid1);
        var deletedRel3 = await Context.Relationships.FindAsync(relid3);
        var deletedRel4 = await Context.Relationships.FindAsync(relid4);
        var intactRel5 = await Context.Relationships.FindAsync(relid5);

        Assert.Null(deletedClass);
        Assert.Null(deletedRel3);
        Assert.Null(deletedRel4);
        Assert.NotNull(intactRel5);
    }

    [Fact]
    public async Task DeleteClass_DeletesDownstreamRelationships()
    {
        // Arrange - relid1 has cid1 as origin, cid4 as destination
        //          relid2 has cid4 as origin, cid1 as destination

        // Act
        var result = await _classBusiness.DeleteClass(uid, oid, pid, cid1);
        Assert.True(result);

        // Assert
        var deletedRel1 = await Context.Relationships.FindAsync(relid1);
        var deletedRel2 = await Context.Relationships.FindAsync(relid2);

        Assert.Null(deletedRel1);
        Assert.Null(deletedRel2);
    }

    [Fact]
    public async Task DeleteClass_DeletesDownstreamRecords()
    {
        // Arrange - rid1 and rid2 belong to cid5
        var existingRecords = await Context.Records
            .Where(r => r.ClassId == cid5)
            .ToListAsync();
        Assert.Equal(2, existingRecords.Count);

        // Act
        var result = await _classBusiness.DeleteClass(uid, oid, pid, cid5);

        // Assert
        Assert.True(result);

        var remainingRecords = await Context.Records
            .Where(r => r.ClassId == cid5)
            .ToListAsync();
        Assert.Empty(remainingRecords);
    }

    #endregion

    #region UnarchiveClass Tests

    [Fact]
    public async Task UnarchiveClass_Success_WhenArchived()
    {
        // Arrange
        var now = DateTime.UtcNow;

        // Act
        var result = await _classBusiness.UnarchiveClass(uid, oid, pid, cid2);

        // Assert
        Assert.True(result);

        // Force EF to sync with database
        Context.ChangeTracker.Clear();

        var unarchivedClass = await Context.Classes.FindAsync(cid2);
        Assert.NotNull(unarchivedClass);
        Assert.False(unarchivedClass.IsArchived);
        Assert.True(unarchivedClass.LastUpdatedAt >= now);
        Assert.Equal(uid, unarchivedClass.LastUpdatedBy);
    }

    [Fact]
    public async Task UnarchiveClass_Fails_IfNotFound()
    {
        // Act & Assert - class3 was deleted
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _classBusiness.UnarchiveClass(uid, oid, pid, cid3));
    }

    [Fact]
    public async Task UnarchiveClass_Fails_IfNotArchived()
    {
        // Act & Assert - class1 is not archived
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _classBusiness.UnarchiveClass(uid, oid, pid, cid1));
    }

    [Fact]
    public async Task UnarchiveClass_Fails_IfWrongProject()
    {
        // Act & Assert - class4 belongs to pid2
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _classBusiness.UnarchiveClass(uid, oid, pid, cid4));
    }

    #endregion

    #region LastUpdatedBy Tests

    [Fact]
    public async Task CreateClass_Success_StoresLastUpdatedByUserId()
    {
        // Arrange
        var testClass = new Class
        {
            Name = $"Test Class with User {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Description = "Test Description with User ID",
            ProjectId = pid,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            IsArchived = false
        };

        // Act
        Context.Classes.Add(testClass);
        await Context.SaveChangesAsync();

        // Assert
        var savedClass = await Context.Classes.FindAsync(testClass.Id);
        Assert.NotNull(savedClass);
        Assert.Equal(uid, savedClass.LastUpdatedBy);
    }

    [Fact]
    public async Task CreateClass_Success_NavigationPropertyLoadsUser()
    {
        // Arrange
        var testClass = new Class
        {
            Name = $"Test Class Navigation {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Description = "Test Navigation Property",
            ProjectId = pid,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            IsArchived = false
        };

        Context.Classes.Add(testClass);
        await Context.SaveChangesAsync();

        // Act
        var classWithUser = await Context.Classes
            .Include(c => c.LastUpdatedByUser)
            .FirstAsync(c => c.Id == testClass.Id);

        // Assert
        Assert.NotNull(classWithUser.LastUpdatedByUser);
        Assert.Equal("Test User", classWithUser.LastUpdatedByUser.Name);
        Assert.Equal("test.user@test.com", classWithUser.LastUpdatedByUser.Email);
        Assert.Equal(uid, classWithUser.LastUpdatedBy);
    }

    [Fact]
    public async Task CreateClass_Success_WithNullLastUpdatedBy()
    {
        // Arrange
        var testClass = new Class
        {
            Name = $"Test Class Null User {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Description = "Test with null LastUpdatedBy",
            ProjectId = pid,
            OrganizationId = oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = null,
            IsArchived = false
        };

        // Act
        Context.Classes.Add(testClass);
        await Context.SaveChangesAsync();

        // Assert
        var savedClass = await Context.Classes.FindAsync(testClass.Id);
        Assert.NotNull(savedClass);
        Assert.Null(savedClass.LastUpdatedBy);

        var classWithUser = await Context.Classes
            .Include(c => c.LastUpdatedByUser)
            .FirstAsync(c => c.Id == testClass.Id);

        Assert.Null(classWithUser.LastUpdatedByUser);
    }

    [Fact]
    public async Task UpdateClass_Success_UpdatesLastUpdatedByUserId()
    {
        // Arrange - Use seeded class with null LastUpdatedBy
        var testClass = await Context.Classes.FindAsync(cid1);
        testClass.LastUpdatedBy = null;
        await Context.SaveChangesAsync();

        // Act
        testClass.LastUpdatedBy = uid;
        testClass.Description = "Updated Description";
        testClass.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        Context.Classes.Update(testClass);
        await Context.SaveChangesAsync();

        // Assert
        var updatedClass = await Context.Classes
            .Include(c => c.LastUpdatedByUser)
            .FirstAsync(c => c.Id == cid1);

        Assert.Equal(uid, updatedClass.LastUpdatedBy);
        Assert.NotNull(updatedClass.LastUpdatedByUser);
        Assert.Equal("Test User", updatedClass.LastUpdatedByUser.Name);
        Assert.Equal("Updated Description", updatedClass.Description);
    }

    #endregion
}