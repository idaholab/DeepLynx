using System.Text.Json.Nodes;
using deeplynx.business;
using deeplynx.datalayer.Models;
using deeplynx.interfaces;
using deeplynx.models;
using Moq;

namespace deeplynx.tests;

[Collection("Test Suite Collection")]
public class MetricsBusinessTests : IntegrationTestBase
{
    private MetricsBusiness _metricsBusiness = null!;
    private Mock<IFileBusinessFactory> _mockFileBusinessFactory = null!;
    private Mock<IFileBusiness> _mockFilesystemBusiness = null!;
    private Mock<IFileBusiness> _mockAzureBusiness = null!;
    
    // Test data IDs
    private long _oid;
    private long _oid2;
    private long _pid;
    private long _pid2;
    private long _uid;
    
    // Object storage IDs
    private long _fsOsId; // Filesystem project storage
    private long _azureOsId; // Azure project storage
    private long _orgFsOsId; // Filesystem org storage
    private long _orgAzureOsId; // Azure org storage
    private long _archivedOsId; // Archived storage

    public MetricsBusinessTests(TestSuiteFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        
        // Setup mocks
        _mockFileBusinessFactory = new Mock<IFileBusinessFactory>();
        _mockFilesystemBusiness = new Mock<IFileBusiness>();
        _mockAzureBusiness = new Mock<IFileBusiness>();
        
        // Configure factory to return appropriate mock based on storage type
        _mockFileBusinessFactory
            .Setup(f => f.CreateFileBusiness("filesystem"))
            .Returns(_mockFilesystemBusiness.Object);
        
        _mockFileBusinessFactory
            .Setup(f => f.CreateFileBusiness("azure_object"))
            .Returns(_mockAzureBusiness.Object);
        
        // Setup BuildPrefix for filesystem (uses "org_" prefix)
        _mockFilesystemBusiness
            .Setup(f => f.BuildPrefix(It.IsAny<long>(), It.IsAny<long?>()))
            .Returns<long, long?>((orgId, projId) => 
                projId.HasValue ? $"org_{orgId}/project_{projId.Value}/" : $"org_{orgId}/");
        
        // Setup BuildPrefix for Azure (uses "organization_" prefix)
        _mockAzureBusiness
            .Setup(f => f.BuildPrefix(It.IsAny<long>(), It.IsAny<long?>()))
            .Returns<long, long?>((orgId, projId) => 
                projId.HasValue ? $"organization_{orgId}/project_{projId.Value}/" : $"organization_{orgId}/");
        
        _metricsBusiness = new MetricsBusiness(Context, _mockFileBusinessFactory.Object);
    }

    protected override async Task SeedTestDataAsync()
    {
        await base.SeedTestDataAsync();

        // Create user
        var user = new User
        {
            Name = "Test User",
            Email = "metrics.test@test.com"
        };
        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        _uid = user.Id;

        // Create organization 1
        var org1 = new Organization
        {
            Name = "Test Org 1",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = _uid
        };
        Context.Organizations.Add(org1);
        await Context.SaveChangesAsync();
        _oid = org1.Id;

        // Create organization 2
        var org2 = new Organization
        {
            Name = "Test Org 2",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = _uid
        };
        Context.Organizations.Add(org2);
        await Context.SaveChangesAsync();
        _oid2 = org2.Id;

        // Create project 1 in org 1
        var project1 = new Project
        {
            Name = "Test Project 1",
            OrganizationId = _oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = _uid
        };
        Context.Projects.Add(project1);
        await Context.SaveChangesAsync();
        _pid = project1.Id;

        // Create project 2 in org 1
        var project2 = new Project
        {
            Name = "Test Project 2",
            OrganizationId = _oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = _uid
        };
        Context.Projects.Add(project2);
        await Context.SaveChangesAsync();
        _pid2 = project2.Id;

        // Create filesystem storage for project 1
        var fsConfig = new JsonObject
        {
            ["mountPath"] = "/test/path"
        };
        var fsStorage = new ObjectStorage
        {
            Name = "Filesystem Project Storage",
            ProjectId = _pid,
            OrganizationId = _oid,
            Type = "filesystem",
            Config = fsConfig.ToString(),
            Default = true
        };
        Context.ObjectStorages.Add(fsStorage);
        await Context.SaveChangesAsync();
        _fsOsId = fsStorage.Id;

        // Create Azure storage for project 1
        var azureConfig = new JsonObject
        {
            ["azureObjectConfig"] = new JsonObject
            {
                ["azureConnectionString"] = "test-connection-string",
                ["azureContainerName"] = "test-container"
            }
        };
        var azureStorage = new ObjectStorage
        {
            Name = "Azure Project Storage",
            ProjectId = _pid,
            OrganizationId = _oid,
            Type = "azure_object",
            Config = azureConfig.ToString(),
            Default = false
        };
        Context.ObjectStorages.Add(azureStorage);
        await Context.SaveChangesAsync();
        _azureOsId = azureStorage.Id;

        // Create filesystem org-level storage
        var orgFsConfig = new JsonObject
        {
            ["mountPath"] = "/org/test/path"
        };
        var orgFsStorage = new ObjectStorage
        {
            Name = "Filesystem Org Storage",
            ProjectId = null,
            OrganizationId = _oid,
            Type = "filesystem",
            Config = orgFsConfig.ToString(),
            Default = false
        };
        Context.ObjectStorages.Add(orgFsStorage);
        await Context.SaveChangesAsync();
        _orgFsOsId = orgFsStorage.Id;

        // Create Azure org-level storage
        var orgAzureConfig = new JsonObject
        {
            ["azureObjectConfig"] = new JsonObject
            {
                ["azureConnectionString"] = "test-connection-string",
                ["azureContainerName"] = "org-container"
            }
        };
        var orgAzureStorage = new ObjectStorage
        {
            Name = "Azure Org Storage",
            ProjectId = null,
            OrganizationId = _oid,
            Type = "azure_object",
            Config = orgAzureConfig.ToString(),
            Default = false
        };
        Context.ObjectStorages.Add(orgAzureStorage);
        await Context.SaveChangesAsync();
        _orgAzureOsId = orgAzureStorage.Id;

        // Create archived storage
        var archivedConfig = new JsonObject
        {
            ["mountPath"] = "/archived/path"
        };
        var archivedStorage = new ObjectStorage
        {
            Name = "Archived Storage",
            ProjectId = _pid,
            OrganizationId = _oid,
            Type = "filesystem",
            Config = archivedConfig.ToString(),
            Default = false,
            IsArchived = true
        };
        Context.ObjectStorages.Add(archivedStorage);
        await Context.SaveChangesAsync();
        _archivedOsId = archivedStorage.Id;
    }

    #region GetObjectStorageSize Tests

    [Fact]
    public async Task GetObjectStorageSize_CallsCorrectFileBusiness_ForFilesystem()
    {
        // Arrange
        _mockFilesystemBusiness
            .Setup(f => f.GetStorageSize(It.IsAny<string>(), It.IsAny<ObjectStorageConfigDto>()))
            .ReturnsAsync(5000L);

        // Act
        var result = await _metricsBusiness.GetObjectStorageSize(_oid, _pid, _fsOsId);

        // Assert
        Assert.Equal(5000L, result);
        _mockFileBusinessFactory.Verify(f => f.CreateFileBusiness("filesystem"), Times.Once);
        _mockFilesystemBusiness.Verify(f => f.BuildPrefix(_oid, _pid), Times.Once);
        _mockFilesystemBusiness.Verify(
            f => f.GetStorageSize($"org_{_oid}/project_{_pid}/", It.IsAny<ObjectStorageConfigDto>()), 
            Times.Once);
    }

    [Fact]
    public async Task GetObjectStorageSize_CallsCorrectFileBusiness_ForAzure()
    {
        // Arrange
        _mockAzureBusiness
            .Setup(f => f.GetStorageSize(It.IsAny<string>(), It.IsAny<ObjectStorageConfigDto>()))
            .ReturnsAsync(3000L);

        // Act
        var result = await _metricsBusiness.GetObjectStorageSize(_oid, _pid, _azureOsId);

        // Assert
        Assert.Equal(3000L, result);
        _mockFileBusinessFactory.Verify(f => f.CreateFileBusiness("azure_object"), Times.Once);
        _mockAzureBusiness.Verify(f => f.BuildPrefix(_oid, _pid), Times.Once);
        _mockAzureBusiness.Verify(
            f => f.GetStorageSize($"organization_{_oid}/project_{_pid}/", It.IsAny<ObjectStorageConfigDto>()), 
            Times.Once);
    }

    [Fact]
    public async Task GetObjectStorageSize_UsesProjectScope_WhenProjectIdProvided()
    {
        // Arrange
        _mockFilesystemBusiness
            .Setup(f => f.GetStorageSize(It.IsAny<string>(), It.IsAny<ObjectStorageConfigDto>()))
            .ReturnsAsync(1000L);

        // Act
        await _metricsBusiness.GetObjectStorageSize(_oid, _pid, _fsOsId);

        // Assert
        _mockFilesystemBusiness.Verify(f => f.BuildPrefix(_oid, _pid), Times.Once);
    }

    [Fact]
    public async Task GetObjectStorageSize_UsesStorageProjectScope_WhenNoProjectIdProvided()
    {
        // Arrange
        _mockFilesystemBusiness
            .Setup(f => f.GetStorageSize(It.IsAny<string>(), It.IsAny<ObjectStorageConfigDto>()))
            .ReturnsAsync(2000L);

        // Act - No projectId provided, should use storage's projectId
        await _metricsBusiness.GetObjectStorageSize(_oid, null, _fsOsId);

        // Assert - Should use storage's project ID (_pid)
        _mockFilesystemBusiness.Verify(f => f.BuildPrefix(_oid, _pid), Times.Once);
    }

    [Fact]
    public async Task GetObjectStorageSize_UsesOrgScope_ForOrgLevelStorage()
    {
        // Arrange
        _mockFilesystemBusiness
            .Setup(f => f.GetStorageSize(It.IsAny<string>(), It.IsAny<ObjectStorageConfigDto>()))
            .ReturnsAsync(4000L);

        // Act
        await _metricsBusiness.GetObjectStorageSize(_oid, null, _orgFsOsId);

        // Assert - Should use null for project (org-level)
        _mockFilesystemBusiness.Verify(f => f.BuildPrefix(_oid, null), Times.Once);
    }

    [Fact]
    public async Task GetObjectStorageSize_UsesProjectScope_WhenProjectIdProvidedForOrgStorage()
    {
        // Arrange
        _mockFilesystemBusiness
            .Setup(f => f.GetStorageSize(It.IsAny<string>(), It.IsAny<ObjectStorageConfigDto>()))
            .ReturnsAsync(1500L);

        // Act - Provide project ID for org-level storage
        await _metricsBusiness.GetObjectStorageSize(_oid, _pid, _orgFsOsId);

        // Assert - Should use provided project ID
        _mockFilesystemBusiness.Verify(f => f.BuildPrefix(_oid, _pid), Times.Once);
    }

    [Fact]
    public async Task GetObjectStorageSize_ThrowsException_WhenStorageNotFound()
    {
        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _metricsBusiness.GetObjectStorageSize(_oid, _pid, 99999L));
    }

    [Fact]
    public async Task GetObjectStorageSize_ThrowsException_WhenStorageIsArchived()
    {
        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _metricsBusiness.GetObjectStorageSize(_oid, _pid, _archivedOsId));
    }

    [Fact]
    public async Task GetObjectStorageSize_ThrowsException_WhenProjectMismatch()
    {
        // Act & Assert - Try to access project 1 storage with project 2 scope
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _metricsBusiness.GetObjectStorageSize(_oid, _pid2, _fsOsId));
    }

    #endregion

    #region GetProjectStorageSize Tests

    [Fact]
    public async Task GetProjectStorageSize_ReturnsAllStorages_InProject()
    {
        // Arrange
        _mockFilesystemBusiness
            .Setup(f => f.GetStorageSize(It.IsAny<string>(), It.IsAny<ObjectStorageConfigDto>()))
            .ReturnsAsync(5000L);
        
        _mockAzureBusiness
            .Setup(f => f.GetStorageSize(It.IsAny<string>(), It.IsAny<ObjectStorageConfigDto>()))
            .ReturnsAsync(3000L);

        // Act
        var result = await _metricsBusiness.GetProjectStorageSize(_oid, _pid);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(4, result.Count); // 2 project-level + 2 org-level storages
        Assert.Equal(5000L, result[_fsOsId]);
        Assert.Equal(3000L, result[_azureOsId]);
        Assert.Equal(5000L, result[_orgFsOsId]); // Org storages also return values
        Assert.Equal(3000L, result[_orgAzureOsId]);
    }

    [Fact]
    public async Task GetProjectStorageSize_IncludesOrgLevelStorages()
    {
        // Arrange
        _mockFilesystemBusiness
            .Setup(f => f.GetStorageSize(It.IsAny<string>(), It.IsAny<ObjectStorageConfigDto>()))
            .ReturnsAsync(2000L);
        
        _mockAzureBusiness
            .Setup(f => f.GetStorageSize(It.IsAny<string>(), It.IsAny<ObjectStorageConfigDto>()))
            .ReturnsAsync(1500L);

        // Act
        var result = await _metricsBusiness.GetProjectStorageSize(_oid, _pid);

        // Assert
        Assert.Equal(4, result.Count); // 2 project + 2 org storages
        Assert.Contains(_orgFsOsId, result.Keys);
        Assert.Contains(_orgAzureOsId, result.Keys);
    }

    [Fact]
    public async Task GetProjectStorageSize_ExcludesArchivedStorages()
    {
        // Arrange
        _mockFilesystemBusiness
            .Setup(f => f.GetStorageSize(It.IsAny<string>(), It.IsAny<ObjectStorageConfigDto>()))
            .ReturnsAsync(1000L);

        // Act
        var result = await _metricsBusiness.GetProjectStorageSize(_oid, _pid);

        // Assert
        Assert.DoesNotContain(_archivedOsId, result.Keys);
    }

    [Fact]
    public async Task GetProjectStorageSize_UsesCorrectPrefix_ForEachStorageType()
    {
        // Arrange
        _mockFilesystemBusiness
            .Setup(f => f.GetStorageSize(It.IsAny<string>(), It.IsAny<ObjectStorageConfigDto>()))
            .ReturnsAsync(1000L);
        
        _mockAzureBusiness
            .Setup(f => f.GetStorageSize(It.IsAny<string>(), It.IsAny<ObjectStorageConfigDto>()))
            .ReturnsAsync(2000L);

        // Act
        await _metricsBusiness.GetProjectStorageSize(_oid, _pid);

        // Assert
        _mockFilesystemBusiness.Verify(
            f => f.GetStorageSize($"org_{_oid}/project_{_pid}/", It.IsAny<ObjectStorageConfigDto>()), 
            Times.Exactly(2)); // Called for both filesystem storages
        
        _mockAzureBusiness.Verify(
            f => f.GetStorageSize($"organization_{_oid}/project_{_pid}/", It.IsAny<ObjectStorageConfigDto>()), 
            Times.Exactly(2)); // Called for both Azure storages
    }

    [Fact]
    public async Task GetProjectStorageSize_ContinuesOnError_ReturnsZeroForFailedStorage()
    {
        // Arrange
        _mockFilesystemBusiness
            .SetupSequence(f => f.GetStorageSize(It.IsAny<string>(), It.IsAny<ObjectStorageConfigDto>()))
            .ReturnsAsync(1000L) // First call succeeds
            .ThrowsAsync(new Exception("Storage error")); // Second call fails
        
        _mockAzureBusiness
            .Setup(f => f.GetStorageSize(It.IsAny<string>(), It.IsAny<ObjectStorageConfigDto>()))
            .ReturnsAsync(2000L);

        // Act
        var result = await _metricsBusiness.GetProjectStorageSize(_oid, _pid);

        // Assert - Should have results for all storages
        Assert.NotNull(result);
        Assert.Equal(4, result.Count);
        // One of the filesystem storages should be 0 due to error
        Assert.Contains(result, kvp => kvp.Value == 0);
    }

    #endregion

    #region GetOrganizationStorageSize Tests

    [Fact]
    public async Task GetOrganizationStorageSize_ReturnsAllStorages_InOrganization()
    {
        // Arrange
        _mockFilesystemBusiness
            .Setup(f => f.GetStorageSize(It.IsAny<string>(), It.IsAny<ObjectStorageConfigDto>()))
            .ReturnsAsync(3000L);
        
        _mockAzureBusiness
            .Setup(f => f.GetStorageSize(It.IsAny<string>(), It.IsAny<ObjectStorageConfigDto>()))
            .ReturnsAsync(2000L);

        // Act
        var result = await _metricsBusiness.GetOrganizationStorageSize(_oid);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(4, result.Count); // All non-archived storages in org
    }

    [Fact]
    public async Task GetOrganizationStorageSize_ExcludesArchivedStorages()
    {
        // Arrange
        _mockFilesystemBusiness
            .Setup(f => f.GetStorageSize(It.IsAny<string>(), It.IsAny<ObjectStorageConfigDto>()))
            .ReturnsAsync(1000L);

        // Act
        var result = await _metricsBusiness.GetOrganizationStorageSize(_oid);

        // Assert
        Assert.DoesNotContain(_archivedOsId, result.Keys);
    }

    [Fact]
    public async Task GetOrganizationStorageSize_UsesOrgLevelPrefix()
    {
        // Arrange
        _mockFilesystemBusiness
            .Setup(f => f.GetStorageSize(It.IsAny<string>(), It.IsAny<ObjectStorageConfigDto>()))
            .ReturnsAsync(1000L);
        
        _mockAzureBusiness
            .Setup(f => f.GetStorageSize(It.IsAny<string>(), It.IsAny<ObjectStorageConfigDto>()))
            .ReturnsAsync(2000L);

        // Act
        await _metricsBusiness.GetOrganizationStorageSize(_oid);

        // Assert - Should use org-level prefix (no project)
        _mockFilesystemBusiness.Verify(f => f.BuildPrefix(_oid, null), Times.Exactly(2));
        _mockAzureBusiness.Verify(f => f.BuildPrefix(_oid, null), Times.Exactly(2));
    }

    [Fact]
    public async Task GetOrganizationStorageSize_ReturnsEmpty_WhenNoStorages()
    {
        // Arrange - Use org 2 which has no storages
        
        // Act
        var result = await _metricsBusiness.GetOrganizationStorageSize(_oid2);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    #endregion

    #region GetSystemStorageSize Tests

    [Fact]
    public async Task GetSystemStorageSize_ReturnsAllStorages_SystemWide()
    {
        // Arrange
        _mockFilesystemBusiness
            .Setup(f => f.GetStorageSize(It.IsAny<string>(), It.IsAny<ObjectStorageConfigDto>()))
            .ReturnsAsync(5000L);
        
        _mockAzureBusiness
            .Setup(f => f.GetStorageSize(It.IsAny<string>(), It.IsAny<ObjectStorageConfigDto>()))
            .ReturnsAsync(3000L);

        // Act
        var result = await _metricsBusiness.GetSystemStorageSize();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(4, result.Count); // All non-archived storages
    }

    [Fact]
    public async Task GetSystemStorageSize_ExcludesArchivedStorages()
    {
        // Arrange
        _mockFilesystemBusiness
            .Setup(f => f.GetStorageSize(It.IsAny<string>(), It.IsAny<ObjectStorageConfigDto>()))
            .ReturnsAsync(1000L);

        // Act
        var result = await _metricsBusiness.GetSystemStorageSize();

        // Assert
        Assert.DoesNotContain(_archivedOsId, result.Keys);
    }

    [Fact]
    public async Task GetSystemStorageSize_UsesEmptyPrefix()
    {
        // Arrange
        _mockFilesystemBusiness
            .Setup(f => f.GetStorageSize(It.IsAny<string>(), It.IsAny<ObjectStorageConfigDto>()))
            .ReturnsAsync(1000L);
        
        _mockAzureBusiness
            .Setup(f => f.GetStorageSize(It.IsAny<string>(), It.IsAny<ObjectStorageConfigDto>()))
            .ReturnsAsync(2000L);

        // Act
        await _metricsBusiness.GetSystemStorageSize();

        // Assert - Should use empty prefix for system-wide
        _mockFilesystemBusiness.Verify(
            f => f.GetStorageSize("", It.IsAny<ObjectStorageConfigDto>()), 
            Times.Exactly(2));
        _mockAzureBusiness.Verify(
            f => f.GetStorageSize("", It.IsAny<ObjectStorageConfigDto>()), 
            Times.Exactly(2));
    }

    [Fact]
    public async Task GetSystemStorageSize_DoesNotCallBuildPrefix()
    {
        // Arrange
        _mockFilesystemBusiness
            .Setup(f => f.GetStorageSize(It.IsAny<string>(), It.IsAny<ObjectStorageConfigDto>()))
            .ReturnsAsync(1000L);

        // Act
        await _metricsBusiness.GetSystemStorageSize();

        // Assert - BuildPrefix should not be called for system-wide (empty prefix)
        _mockFilesystemBusiness.Verify(f => f.BuildPrefix(It.IsAny<long>(), It.IsAny<long?>()), Times.Never);
        _mockAzureBusiness.Verify(f => f.BuildPrefix(It.IsAny<long>(), It.IsAny<long?>()), Times.Never);
    }

    #endregion

    #region Factory Integration Tests

    [Fact]
    public async Task MetricsBusiness_UsesFactory_ToGetCorrectImplementation()
    {
        // Arrange
        _mockFilesystemBusiness
            .Setup(f => f.GetStorageSize(It.IsAny<string>(), It.IsAny<ObjectStorageConfigDto>()))
            .ReturnsAsync(1000L);
        
        _mockAzureBusiness
            .Setup(f => f.GetStorageSize(It.IsAny<string>(), It.IsAny<ObjectStorageConfigDto>()))
            .ReturnsAsync(2000L);

        // Act
        await _metricsBusiness.GetObjectStorageSize(_oid, _pid, _fsOsId);
        await _metricsBusiness.GetObjectStorageSize(_oid, _pid, _azureOsId);

        // Assert
        _mockFileBusinessFactory.Verify(f => f.CreateFileBusiness("filesystem"), Times.Once);
        _mockFileBusinessFactory.Verify(f => f.CreateFileBusiness("azure_object"), Times.Once);
    }

    [Fact]
    public async Task MetricsBusiness_CallsGetStorageSize_WithCorrectConfig()
    {
        // Arrange
        ObjectStorageConfigDto? capturedConfig = null;
        
        _mockFilesystemBusiness
            .Setup(f => f.GetStorageSize(It.IsAny<string>(), It.IsAny<ObjectStorageConfigDto>()))
            .Callback<string, ObjectStorageConfigDto>((prefix, config) => capturedConfig = config)
            .ReturnsAsync(1000L);

        // Act
        await _metricsBusiness.GetObjectStorageSize(_oid, _pid, _fsOsId);

        // Assert
        Assert.NotNull(capturedConfig);
        Assert.NotNull(capturedConfig.MountPath);
        Assert.Equal("/test/path", capturedConfig.MountPath);
    }

    #endregion
}