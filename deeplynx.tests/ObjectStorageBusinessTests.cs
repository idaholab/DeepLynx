using System.Text.Json.Nodes;
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
public class ObjectStorageBusinessTests : IntegrationTestBase
{
    private EventBusiness _eventBusiness = null!;
    private Mock<IClassBusiness> _mockClassBusiness = null!;
    private Mock<IDataSourceBusiness> _mockDataSourceBusiness = null!;
    private Mock<IHubContext<EventNotificationHub>> _mockHubContext = null!;
    private Mock<ILogger<ProjectBusiness>> _mockLogger = null!;
    private Mock<ILogger<NotificationBusiness>> _mockNotificationLogger = null!;
    private Mock<IRoleBusiness> _mockRoleBusiness = null!;
    private INotificationBusiness _notificationBusiness = null!;
    private ObjectStorageBusiness _objectStorageBusiness;
    private Mock<IOrganizationBusiness> _organizationBusiness = null!;
    private ProjectBusiness _projectBusiness;
    public long archivedOs;
    private long organizationId;
    public long os1;
    public long os2;
    public long os4;
    public long os6;
    public long os7;
    public long os8;

    public long pid;
    public long pid2;
    public long uid;

    public ObjectStorageBusinessTests(TestSuiteFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _organizationBusiness = new Mock<IOrganizationBusiness>();
        _objectStorageBusiness = new ObjectStorageBusiness(Context);
        _mockHubContext = new Mock<IHubContext<EventNotificationHub>>();
        _mockNotificationLogger = new Mock<ILogger<NotificationBusiness>>();
        _notificationBusiness =
            new NotificationBusiness(Context, _mockNotificationLogger.Object, _mockHubContext.Object);
        _eventBusiness = new EventBusiness(Context, _notificationBusiness);
        _mockLogger = new Mock<ILogger<ProjectBusiness>>();
        _mockClassBusiness = new Mock<IClassBusiness>();
        _mockDataSourceBusiness = new Mock<IDataSourceBusiness>();
        _mockRoleBusiness = new Mock<IRoleBusiness>();
        _projectBusiness = new ProjectBusiness(
            Context,
            _mockLogger.Object,
            _mockClassBusiness.Object,
            _mockRoleBusiness.Object,
            _mockDataSourceBusiness.Object,
            _objectStorageBusiness,
            _eventBusiness, _organizationBusiness.Object);
    }

    #region ObjectStorageResponseDto Tests

    [Fact]
    public void ObjectStorageResponseDto_AllProperties_CanBeSetAndRetrieved()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var dto = new ObjectStorageResponseDto
        {
            Id = 1,
            Name = "Test Storage",
            Type = "filesystem",
            ProjectId = 100,
            OrganizationId = 1000,
            Default = true,
            LastUpdatedAt = now,
            LastUpdatedBy = uid,
            IsArchived = false
        };

        // Assert
        Assert.Equal(1, dto.Id);
        Assert.Equal("Test Storage", dto.Name);
        Assert.Equal("filesystem", dto.Type);
        Assert.Equal(100, dto.ProjectId);
        Assert.Equal(1000, dto.OrganizationId);
        Assert.True(dto.Default);
        Assert.Equal(now, dto.LastUpdatedAt);
        Assert.Equal(uid, dto.LastUpdatedBy);
        Assert.False(dto.IsArchived);
    }

    #endregion

    protected override async Task SeedTestDataAsync()
    {
        await base.SeedTestDataAsync();
        var user = new User
        {
            Name = "Test User",
            Email = "test_user@example.com",
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

        var project = new Project { Name = "Test Project 1", OrganizationId = organizationId };
        var project2 = new Project { Name = "Test Project 2", OrganizationId = organizationId };
        Context.Projects.Add(project);
        Context.Projects.Add(project2);
        await Context.SaveChangesAsync();
        pid = project.Id;
        pid2 = project2.Id;

        var os1Config = new JsonObject();
        os1Config["mountPath"] = "Test Project 1";
        var objectStorage = new ObjectStorage
        {
            Name = "Test Object Storage 1",
            ProjectId = pid,
            OrganizationId = organizationId,
            Type = "filesystem",
            Config = os1Config.ToString(),
            Default = true
        };

        var os2Config = new JsonObject();
        os2Config["mountPath"] = "Test Project 2";
        var objectStorage2 = new ObjectStorage
        {
            Name = "Test Object Storage 2",
            Type = "filesystem",
            ProjectId = pid,
            OrganizationId = organizationId,
            Config = os2Config.ToString()
        };

        var os3Config = new JsonObject();
        os3Config["mountPath"] = "Test Project 3";
        var objectStorage3 = new ObjectStorage
        {
            Name = "Test Object Storage 3",
            ProjectId = pid2,
            OrganizationId = organizationId,
            Type = "filesystem",
            Config = os3Config.ToString()
        };

        var os4Config = new JsonObject();
        os4Config["mountPath"] = "Test Project 4";
        var objectStorage4 = new ObjectStorage
        {
            Name = "Test Object Storage 4",
            Type = "filesystem",
            ProjectId = pid2,
            OrganizationId = organizationId,
            Config = os4Config.ToString()
        };

        var os5Config = new JsonObject();
        os5Config["mountPath"] = "Test Project 5";
        var objectStorage5 = new ObjectStorage
        {
            Name = "Test Object Storage 5",
            Type = "filesystem",
            ProjectId = pid,
            OrganizationId = organizationId,
            Config = os5Config.ToString(),
            IsArchived = true
        };

        var os6Config = new JsonObject();
        os6Config["mountPath"] = "Test 6";
        var objectStorage6 = new ObjectStorage
        {
            Name = "Test Object Storage 6",
            Type = "filesystem",
            OrganizationId = organizationId,
            Config = os6Config.ToString(),
            Default = true,
            IsArchived = false
        };

        var os7Config = new JsonObject();
        os7Config["mountPath"] = "Test 7";
        var objectStorage7 = new ObjectStorage
        {
            Name = "Test Object Storage 7",
            Type = "filesystem",
            OrganizationId = organizationId,
            Config = os7Config.ToString(),
            IsArchived = false
        };

        var os8Config = new JsonObject();
        os8Config["mountPath"] = "Test 8";
        var objectStorage8 = new ObjectStorage
        {
            Name = "Test Object Storage 8",
            Type = "filesystem",
            OrganizationId = organizationId,
            Config = os7Config.ToString(),
            IsArchived = true
        };

        Context.ObjectStorages.Add(objectStorage);
        Context.ObjectStorages.Add(objectStorage2);
        Context.ObjectStorages.Add(objectStorage3);
        Context.ObjectStorages.Add(objectStorage4);
        Context.ObjectStorages.Add(objectStorage5);
        Context.ObjectStorages.Add(objectStorage6);
        Context.ObjectStorages.Add(objectStorage7);
        Context.ObjectStorages.Add(objectStorage8);

        await Context.SaveChangesAsync();
        os1 = objectStorage.Id;
        os2 = objectStorage2.Id;
        os4 = objectStorage4.Id;
        os6 = objectStorage6.Id;
        os7 = objectStorage7.Id;
        os8 = objectStorage8.Id;
        archivedOs = objectStorage5.Id;
    }

    #region Edge Cases

    [Fact]
    public async Task ObjectStoragesArchived_WhenProjectArchived()
    {
        // Act
        var result = await _projectBusiness.ArchiveProject(uid, organizationId, pid);

        // Assert
        Assert.True(result);

        // need to clear change tracker for stored procedure
        Context.ChangeTracker.Clear();

        var archivedObjectStorages = await Context.ObjectStorages.Where(os => os.ProjectId == pid).ToListAsync();
        Assert.NotNull(archivedObjectStorages);
        Assert.All(archivedObjectStorages, os => Assert.True(os.IsArchived));
    }

    #endregion

    #region GetAllObjectStorages Tests

    [Fact]
    public async Task GetAllObjectStorages_Success_ReturnsAllObjectStoragesInProject()
    {
        // Act
        var objectStorages = await _objectStorageBusiness.GetAllObjectStorages(
            organizationId, pid, true);

        // Assert
        Assert.Equal(2, objectStorages.Count);
        Assert.All(objectStorages, os => Assert.Equal(pid, os.ProjectId));
        Assert.All(objectStorages, os => Assert.Equal(organizationId, os.OrganizationId));
        Assert.Equal(os1, objectStorages.First().Id);
        Assert.Equal(os2, objectStorages.Last().Id);
    }

    [Fact]
    public async Task GetAllObjectStorages_Success_ReturnsAllObjectStoragesInOrganization()
    {
        // Act
        var objectStorages = await _objectStorageBusiness.GetAllObjectStorages(
            organizationId, null, true);

        var actual = Context.ObjectStorages.ToList();
        // Assert
        Assert.Equal(6, objectStorages.Count);
        Assert.All(objectStorages, os => Assert.Equal(organizationId, os.OrganizationId));
    }

    [Fact]
    public async Task GetAllObjectStorages_Success_CanFilterOutArchived()
    {
        // Act
        var objectStorages = await _objectStorageBusiness.GetAllObjectStorages(
            organizationId, pid, true);

        // Assert
        Assert.Equal(2, objectStorages.Count);
        Assert.DoesNotContain(objectStorages, os => os.Id == archivedOs);
        Assert.All(objectStorages, os => Assert.False(os.IsArchived));
    }

    [Fact]
    public async Task GetAllObjectStorages_Success_CanContainArchived()
    {
        // Act
        var objectStorages = await _objectStorageBusiness.GetAllObjectStorages(
            organizationId, pid, false);

        // Assert
        Assert.Equal(3, objectStorages.Count);
        Assert.Contains(objectStorages, os => os.Id == archivedOs && os.IsArchived);
        Assert.All(objectStorages, os => Assert.Equal(pid, os.ProjectId));
    }

    [Fact]
    public async Task GetAllObjectStorages_Fails_WhenProjectDoesNotExist()
    {
        // Act
        var objectStorages = await _objectStorageBusiness.GetAllObjectStorages(
            organizationId, pid2 + 1000, true);

        // Assert - should return empty list, not throw
        Assert.Empty(objectStorages);
    }

    #endregion

    #region GetObjectStorage Tests

    [Fact]
    public async Task GetObjectStorage_Success_ReturnsNameAndTypeInProject()
    {
        // Act
        var objectStorage = await _objectStorageBusiness.GetObjectStorage(
            organizationId, pid, os1, true);

        // Assert
        Assert.NotNull(objectStorage);
        Assert.Equal(os1, objectStorage.Id);
        Assert.Equal("Test Object Storage 1", objectStorage.Name);
    }

    [Fact]
    public async Task GetObjectStorage_Success_ReturnsNameAndTypeInOrganization()
    {
        // Act
        var objectStorage = await _objectStorageBusiness.GetObjectStorage(
            organizationId, null, os6, true);

        // Assert
        Assert.NotNull(objectStorage);
        Assert.Equal(os6, objectStorage.Id);
        Assert.Equal("Test Object Storage 6", objectStorage.Name);
    }

    [Fact]
    public async Task GetObjectStorage_Success_CanIncludeArchived()
    {
        // Act
        var objectStorage = await _objectStorageBusiness.GetObjectStorage(
            organizationId, pid, archivedOs, false);

        // Assert
        Assert.NotNull(objectStorage);
        Assert.Equal(archivedOs, objectStorage.Id);
        Assert.True(objectStorage.IsArchived);
    }

    [Fact]
    public async Task GetObjectStorage_Fails_WhenObjectStorageIsArchived()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _objectStorageBusiness.GetObjectStorage(organizationId, pid, archivedOs, true));
        Assert.Contains($"Object storage with id {archivedOs} is archived", exception.Message);
    }

    #endregion

    #region CreateObjectStorage Tests

    [Fact]
    public async Task Create_Success_ReturnsNameAndType()
    {
        // Arrange
        var config = new JsonObject();
        config["mountPath"] = "./storage/";
        var dto = new CreateObjectStorageRequestDto
        {
            Name = "Test",
            Config = config
        };

        // Act
        var objectStorageResponse = await _objectStorageBusiness.CreateObjectStorage(
            uid, organizationId, pid, dto);

        // Assert
        Assert.Equal(dto.Name, objectStorageResponse.Name);
        Assert.Equal(pid, objectStorageResponse.ProjectId);
        Assert.Equal(organizationId, objectStorageResponse.OrganizationId);
        Assert.Equal("filesystem", objectStorageResponse.Type);
    }

    [Fact]
    public async Task Create_Success_ReturnsCorrectValues()
    {
        // Arrange
        var config = new JsonObject();
        config["mountPath"] = "./storage/";
        var dto = new CreateObjectStorageRequestDto
        {
            Name = "Test",
            Config = config
        };

        var now = DateTime.UtcNow;

        // Act
        var objectStorageResponse = await _objectStorageBusiness.CreateObjectStorage(
            uid, organizationId, null, dto);

        // Assert
        Assert.NotNull(objectStorageResponse);
        Assert.Equal(dto.Name, objectStorageResponse.Name);
        Assert.Equal("filesystem", objectStorageResponse.Type);
        Assert.Null(objectStorageResponse.ProjectId);
        Assert.Equal(organizationId, objectStorageResponse.OrganizationId);
        Assert.False(objectStorageResponse.Default);
        Assert.False(objectStorageResponse.IsArchived);
        Assert.True(objectStorageResponse.LastUpdatedAt >= now);
        Assert.Equal(uid, objectStorageResponse.LastUpdatedBy);

        // check to see if config is in db
        var objectStorage = await Context.ObjectStorages.FindAsync(objectStorageResponse.Id);
        Assert.NotNull(objectStorage);
        Assert.NotNull(objectStorage.Config);
    }

    [Fact]
    public async Task Create_Success_ReturnsNameAndCorrectType()
    {
        // Arrange
        var config = new JsonObject();
        config["azureConnectionString"] = "example-connection-string";
        var dto = new CreateObjectStorageRequestDto
        {
            Name = "Test",
            Config = config
        };

        var config2 = new JsonObject();
        config2["awsConnectionString"] = "example-connection-string";
        var dto2 = new CreateObjectStorageRequestDto
        {
            Name = "Test 2",
            Config = config2
        };

        var config3 = new JsonObject();
        config3["mountPath"] = "./storage/";
        var dto3 = new CreateObjectStorageRequestDto
        {
            Name = "Test 3",
            Config = config3
        };

        // Act
        var objectStorageResponse =
            await _objectStorageBusiness.CreateObjectStorage(uid, organizationId, pid, dto);
        var objectStorageResponse2 = await _objectStorageBusiness.CreateObjectStorage(
            uid, organizationId, pid, dto2);
        var objectStorageResponse3 = await _objectStorageBusiness.CreateObjectStorage(
            uid, organizationId, pid, dto3);


        // Assert
        Assert.Equal(dto.Name, objectStorageResponse.Name);
        Assert.Equal(dto2.Name, objectStorageResponse2.Name);
        Assert.Equal(dto3.Name, objectStorageResponse3.Name);
        Assert.Equal("azure_object", objectStorageResponse.Type);
        Assert.Equal("aws_s3", objectStorageResponse2.Type);
        Assert.Equal("filesystem", objectStorageResponse3.Type);
    }

    [Fact]
    public async Task Create_Fails_WhenConfigIsEmpty()
    {
        // Arrange
        var config = new JsonObject();
        var dto = new CreateObjectStorageRequestDto
        {
            Name = "Test",
            Config = config
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _objectStorageBusiness.CreateObjectStorage(uid, organizationId, pid, dto));
        Assert.Contains("Request does not contain recognized config", exception.Message);
    }

    #endregion

    #region UpdateObjectStorage Tests

    [Fact]
    public async Task Update_Success_ReturnsUpdatedNameInProject()
    {
        // Arrange
        var updateDto = new UpdateObjectStorageRequestDto
        {
            Name = "Updated Name"
        };
        // Act
        var updatedObjectStorage = await _objectStorageBusiness.UpdateObjectStorage(
            uid, organizationId, pid, os1, updateDto);

        // Assert
        Assert.Equal(updateDto.Name, updatedObjectStorage.Name);
    }

    [Fact]
    public async Task Update_Success_ReturnsUpdatedNameInOrganization()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var updateDto = new UpdateObjectStorageRequestDto
        {
            Name = "Updated Name"
        };
        // Act
        var updatedObjectStorage =
            await _objectStorageBusiness.UpdateObjectStorage(uid, organizationId, null, os7, updateDto);

        // Assert
        Assert.Equal(os7, updatedObjectStorage.Id);
        Assert.Equal(updateDto.Name, updatedObjectStorage.Name);
        Assert.Equal("filesystem", updatedObjectStorage.Type);
        Assert.Null(updatedObjectStorage.ProjectId);
        Assert.False(updatedObjectStorage.Default);
        Assert.False(updatedObjectStorage.IsArchived);
        Assert.True(updatedObjectStorage.LastUpdatedAt >= now);
        Assert.Equal(uid, updatedObjectStorage.LastUpdatedBy);
    }

    [Fact]
    public async Task Update_Fails_WhenObjectStorageDoesNotExist()
    {
        // Arrange
        var updateDto = new UpdateObjectStorageRequestDto
        {
            Name = "Updated Name"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _objectStorageBusiness.UpdateObjectStorage(uid, organizationId, pid, os1 + 1000, updateDto));
        Assert.Contains($"Object storage with id {os1 + 1000} not found", exception.Message);
    }

    [Fact]
    public async Task Update_Fails_WhenObjectStorageIsArchived()
    {
        // Arrange
        var updateDto = new UpdateObjectStorageRequestDto
        {
            Name = "Updated Name"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _objectStorageBusiness.UpdateObjectStorage(uid, organizationId, pid, archivedOs, updateDto));
        Assert.Contains($"Object storage with id {archivedOs} not found", exception.Message);
    }

    #endregion

    #region DeleteObjectStorage Tests

    [Fact]
    public async Task Delete_Success_RemovesObjectStorage()
    {
        // Act
        var deleted = await _objectStorageBusiness.DeleteObjectStorage(
            uid, organizationId, pid, os2);

        // Assert
        Assert.True(deleted);
        var deletedObjectStorage = await Context.ObjectStorages.Where(os => os.ProjectId == pid && os.Id == os2)
            .FirstOrDefaultAsync();
        Assert.Null(deletedObjectStorage);
    }

    [Fact]
    public async Task Delete_Success_RemovesObjectStorageInOrganization()
    {
        // Act
        var deleted = await _objectStorageBusiness.DeleteObjectStorage(
            uid, organizationId, null, os7);

        // Assert
        Assert.True(deleted);
        var deletedObjectStorage = await Context.ObjectStorages
            .Where(os => os.OrganizationId == organizationId && os.Id == os7).FirstOrDefaultAsync();
        Assert.Null(deletedObjectStorage);
    }

    [Fact]
    public async Task Delete_Fails_WhenObjectStorageDoesNotExist()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _objectStorageBusiness.DeleteObjectStorage(uid, organizationId, pid, os1 + 1000));
        Assert.Contains($"Object storage with id {os1 + 1000} not found", exception.Message);
    }

    [Fact]
    public async Task Delete_Fails_WhenObjectStorageIsDefault()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _objectStorageBusiness.DeleteObjectStorage(uid, organizationId, pid, os1));
        Assert.Contains("Default object storage cannot be deleted. Please assign new default storage before deleting.",
            exception.Message);
    }

    #endregion

    #region ArchiveObjectStorage Tests

    [Fact]
    public async Task Archive_Success_ArchivesObjectStorage()
    {
        // Arrange
        var now = DateTime.UtcNow;

        // Act
        var archived = await _objectStorageBusiness.ArchiveObjectStorage(
            uid, organizationId, pid, os2);

        // Assert
        Assert.True(archived);
        var archivedObjectStorage = await Context.ObjectStorages.Where(os => os.Id == os2 && os.ProjectId == pid)
            .FirstOrDefaultAsync();
        Assert.NotNull(archivedObjectStorage);
        Assert.True(archivedObjectStorage.IsArchived);
        Assert.Equal(os2, archivedObjectStorage.Id);
        Assert.Equal("Test Object Storage 2", archivedObjectStorage.Name);
        Assert.Equal("filesystem", archivedObjectStorage.Type);
        Assert.Equal(pid, archivedObjectStorage.ProjectId);
        Assert.False(archivedObjectStorage.Default);
        Assert.NotNull(archivedObjectStorage.Config);
        Assert.True(archivedObjectStorage.LastUpdatedAt >= now);
        Assert.Equal(uid, archivedObjectStorage.LastUpdatedBy);
    }

    [Fact]
    public async Task Archive_Success_ArchivesObjectStorageInOrganization()
    {
        // Act
        var archived = await _objectStorageBusiness.ArchiveObjectStorage(uid, organizationId, null, os7);

        // Assert
        Assert.True(archived);
        var archivedObjectStorage = await Context.ObjectStorages
            .Where(os => os.Id == os7 && os.OrganizationId == organizationId).FirstOrDefaultAsync();
        Assert.NotNull(archivedObjectStorage);
        Assert.Equal(os7, archivedObjectStorage.Id);
        Assert.True(archivedObjectStorage.IsArchived);
    }

    [Fact]
    public async Task Archive_Fails_IfObjectStorageDoesNotExist()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _objectStorageBusiness.ArchiveObjectStorage(uid, organizationId, pid, os1 + 1000));
        Assert.Contains($"Object storage with id {os1 + 1000} not found", exception.Message);
    }

    [Fact]
    public async Task Archive_Fails_IfObjectStorageIsDefault()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _objectStorageBusiness.ArchiveObjectStorage(uid, organizationId, pid, os1));
        Assert.Contains(
            "Default object storage cannot be archived. Please assign new default storage before archiving.",
            exception.Message);
    }

    [Fact]
    public async Task Archive_Fails_IfObjectStorageIsAlreadyArchived()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _objectStorageBusiness.ArchiveObjectStorage(uid, organizationId, pid, archivedOs));
        Assert.Contains($"Object storage with id {archivedOs} is already archived", exception.Message);
    }

    #endregion

    #region UnarchiveObjectStorage Tests

    [Fact]
    public async Task Unarchive_Success_UnarchivesObjectStorage()
    {
        // Arrange
        var now = DateTime.UtcNow;

        // Act
        var unarchived = await _objectStorageBusiness.UnarchiveObjectStorage(
            uid, organizationId, pid, archivedOs);

        // Assert
        Assert.True(unarchived);
        var unarchivedObjectStorage = await Context.ObjectStorages
            .Where(os => os.Id == archivedOs && os.ProjectId == pid).FirstOrDefaultAsync();
        Assert.NotNull(unarchivedObjectStorage);
        Assert.False(unarchivedObjectStorage.IsArchived);
        Assert.Equal(archivedOs, unarchivedObjectStorage.Id);
        Assert.Equal("Test Object Storage 5", unarchivedObjectStorage.Name);
        Assert.Equal("filesystem", unarchivedObjectStorage.Type);
        Assert.Equal(pid, unarchivedObjectStorage.ProjectId);
        Assert.Equal(organizationId, unarchivedObjectStorage.OrganizationId);
        Assert.False(unarchivedObjectStorage.Default);
        Assert.True(unarchivedObjectStorage.LastUpdatedAt >= now);
        Assert.Equal(uid, unarchivedObjectStorage.LastUpdatedBy);
    }

    [Fact]
    public async Task Unarchive_Success_UnarchivesObjectStorageInOrganization()
    {
        // Act
        var unarchived = await _objectStorageBusiness.UnarchiveObjectStorage(
            uid, organizationId, null, os8);

        // Assert
        Assert.True(unarchived);
        var unarchivedObjectStorage = await Context.ObjectStorages
            .Where(os => os.Id == os8 && os.OrganizationId == organizationId)
            .FirstOrDefaultAsync();
        Assert.NotNull(unarchivedObjectStorage);
        Assert.Equal(os8, unarchivedObjectStorage.Id);
        Assert.False(unarchivedObjectStorage.IsArchived);
    }

    [Fact]
    public async Task Unarchive_Fails_WhenObjectStorageDoesNotExist()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _objectStorageBusiness.UnarchiveObjectStorage(uid, organizationId, pid, archivedOs + 1000));
        Assert.Contains($"Object storage with id {archivedOs + 1000} not found", exception.Message);
    }

    [Fact]
    public async Task Unarchive_Fails_WhenObjectIsAlreadyUnarchived()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _objectStorageBusiness.UnarchiveObjectStorage(uid, organizationId, pid, os1));
        Assert.Contains($"Object storage with id {os1} is not archived", exception.Message);
    }

    #endregion

    #region GetDefaultObjectStorage Tests

    [Fact]
    public async Task GetDefaultObjectStorage_Success_ReturnsDefaultObject()
    {
        // Act
        var defaultObjectStorage = await _objectStorageBusiness.GetDefaultObjectStorage(
            organizationId, pid);

        // Assert
        Assert.NotNull(defaultObjectStorage);
        Assert.Equal(os1, defaultObjectStorage.Id);
    }

    [Fact]
    public async Task GetDefaultObjectStorage_Success_ReturnsDefaultObjectInOrganization()
    {
        // Act
        var defaultObjectStorage = await _objectStorageBusiness.GetDefaultObjectStorage(
            organizationId, null);

        // Assert
        Assert.NotNull(defaultObjectStorage);
        Assert.Equal(os6, defaultObjectStorage.Id);
    }

    [Fact]
    public async Task GetDefaultObjectStorage_Fails_WhenNoDefault()
    {
        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _objectStorageBusiness.GetDefaultObjectStorage(organizationId, pid2));
        Assert.Contains("Default object storage not found", exception.Message);
    }

    #endregion

    #region SetDefaultObjectStorage Tests

    [Fact]
    public async Task SetDefault_InProject_SetsDefault()
    {
        // Act
        var newDefaultObjectStorage = await _objectStorageBusiness.SetDefaultObjectStorage(
            uid, organizationId, pid, os2);

        // Assert
        Assert.NotNull(newDefaultObjectStorage);
        Assert.Equal(os2, newDefaultObjectStorage.Id);
        Assert.True(newDefaultObjectStorage.Default);
        
        // Clear the change tracker to force a fresh query from the database
        Context.ChangeTracker.Clear();

        var oldDefaultObjectStorage = await Context.ObjectStorages
            .Where(os => os.Id == os1 && os.ProjectId == pid)
            .FirstOrDefaultAsync();
        Assert.NotNull(oldDefaultObjectStorage);
        Assert.Equal(os1, oldDefaultObjectStorage.Id);
        Assert.False(oldDefaultObjectStorage.Default);
    }

    [Fact]
    public async Task SetDefault_InOrganization_SetsDefault()
    {
        // Act
        var newDefaultObjectStorage =
            await _objectStorageBusiness.SetDefaultObjectStorage(
                uid, organizationId, null, os7);

        // Assert
        Assert.NotNull(newDefaultObjectStorage);
        Assert.Equal(os7, newDefaultObjectStorage.Id);
        Assert.True(newDefaultObjectStorage.Default);
        
        // Clear the change tracker to force a fresh query from the database
        Context.ChangeTracker.Clear();

        var oldDefaultObjectStorage = await Context.ObjectStorages
            .Where(os => os.Id == os6 && os.OrganizationId == organizationId).FirstOrDefaultAsync();
        Assert.NotNull(oldDefaultObjectStorage);
        Assert.Equal(os6, oldDefaultObjectStorage.Id);
        Assert.False(oldDefaultObjectStorage.Default);
    }

    [Fact]
    public async Task ChangeDefault_Fails_WhenObjectStorageDoesNotExist()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _objectStorageBusiness.SetDefaultObjectStorage(uid, organizationId, pid, os1 + 1000));
        Assert.Contains($"Object storage with id {os1 + 1000} not found", exception.Message);
    }

    #endregion

    #region LastUpdatedBy Tests

    [Fact]
    public async Task CreateObjectStorage_Success_StoresLastUpdatedByUserId()
    {
        // Arrange
        var config = new JsonObject();
        config["mountPath"] = "./test/storage/";
        var testObjectStorage = new ObjectStorage
        {
            Name = "Test Object Storage",
            ProjectId = pid,
            OrganizationId = organizationId,
            Type = "filesystem",
            Config = config.ToString(),
            Default = false,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };

        // Act
        Context.ObjectStorages.Add(testObjectStorage);
        await Context.SaveChangesAsync();

        // Assert
        var savedObjectStorage = await Context.ObjectStorages.FindAsync(testObjectStorage.Id);
        Assert.NotNull(savedObjectStorage);
        Assert.Equal(uid, savedObjectStorage.LastUpdatedBy);
    }

    [Fact]
    public async Task CreateObjectStorage_Success_NavigationPropertyLoadsUser()
    {
        // Arrange
        var config = new JsonObject();
        config["mountPath"] = "./test/storage2/";
        var testObjectStorage = new ObjectStorage
        {
            Name = "Test Object Storage 12345",
            ProjectId = pid,
            OrganizationId = organizationId,
            Type = "filesystem",
            Config = config.ToString(),
            Default = false,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };

        Context.ObjectStorages.Add(testObjectStorage);
        await Context.SaveChangesAsync();

        // Act
        var objectStorageWithUser = await Context.ObjectStorages
            .Include(os => os.LastUpdatedByUser)
            .FirstAsync(os => os.Id == testObjectStorage.Id);

        // Assert
        Assert.NotNull(objectStorageWithUser.LastUpdatedByUser);
        Assert.Equal("Test User", objectStorageWithUser.LastUpdatedByUser.Name);
        Assert.Equal("test_user@example.com", objectStorageWithUser.LastUpdatedByUser.Email);
        Assert.Equal(uid, objectStorageWithUser.LastUpdatedBy);
    }

    [Fact]
    public async Task CreateObjectStorage_Success_WithNullLastUpdatedBy()
    {
        // Arrange
        var config = new JsonObject();
        config["mountPath"] = "./test/storage3/";
        var testObjectStorage = new ObjectStorage
        {
            Name = "Test Object Storage 3",
            ProjectId = pid,
            OrganizationId = organizationId,
            Type = "filesystem",
            Config = config.ToString(),
            Default = false,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = null
        };

        // Act
        Context.ObjectStorages.Add(testObjectStorage);
        await Context.SaveChangesAsync();

        // Assert
        var savedObjectStorage = await Context.ObjectStorages.FindAsync(testObjectStorage.Id);
        Assert.NotNull(savedObjectStorage);
        Assert.Null(savedObjectStorage.LastUpdatedBy);

        var objectStorageWithUser = await Context.ObjectStorages
            .Include(os => os.LastUpdatedByUser)
            .FirstAsync(os => os.Id == testObjectStorage.Id);

        Assert.Null(objectStorageWithUser.LastUpdatedByUser);
    }

    [Fact]
    public async Task UpdateObjectStorage_Success_UpdatesLastUpdatedByUserId()
    {
        // Arrange
        var config = new JsonObject();
        config["mountPath"] = "./test/storage4/";
        var testObjectStorage = new ObjectStorage
        {
            Name = "Test Object Storage 4",
            ProjectId = pid,
            OrganizationId = organizationId,
            Type = "filesystem",
            Config = config.ToString(),
            Default = false,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = null
        };
        Context.ObjectStorages.Add(testObjectStorage);
        await Context.SaveChangesAsync();

        // Act
        testObjectStorage.LastUpdatedBy = uid;
        testObjectStorage.Name = "Updated Name";
        testObjectStorage.LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        Context.ObjectStorages.Update(testObjectStorage);
        await Context.SaveChangesAsync();

        // Assert
        var updatedObjectStorage = await Context.ObjectStorages
            .Include(os => os.LastUpdatedByUser)
            .FirstAsync(os => os.Id == testObjectStorage.Id);

        Assert.Equal(uid, updatedObjectStorage.LastUpdatedBy);
        Assert.NotNull(updatedObjectStorage.LastUpdatedByUser);
        Assert.Equal("Test User", updatedObjectStorage.LastUpdatedByUser.Name);
        Assert.Equal("Updated Name", updatedObjectStorage.Name);
    }

    #endregion
}