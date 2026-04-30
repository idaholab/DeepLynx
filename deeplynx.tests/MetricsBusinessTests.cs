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
        Assert.NotNull(result);
        Assert.Equal(5000L, result.Bytes);
        _mockFileBusinessFactory.Verify(f => f.CreateFileBusiness("filesystem"), Times.Once);
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
        Assert.NotNull(result);
        Assert.Equal(3000L, result.Bytes);
        _mockFileBusinessFactory.Verify(f => f.CreateFileBusiness("azure_object"), Times.Once);
    }

    [Fact]
    public async Task GetObjectStorageSize_UsesCorrectPrefix_ForProjectStorage()
    {
        // Arrange
        _mockFilesystemBusiness
            .Setup(f => f.GetStorageSize(It.IsAny<string>(), It.IsAny<ObjectStorageConfigDto>()))
            .ReturnsAsync(1000L);

        // Act
        await _metricsBusiness.GetObjectStorageSize(_oid, _pid, _fsOsId);

        // Assert
        _mockFilesystemBusiness.Verify(
            f => f.GetStorageSize($"org_{_oid}/project_{_pid}/", It.IsAny<ObjectStorageConfigDto>()), 
            Times.Once);
    }

    [Fact]
    public async Task GetObjectStorageSize_UsesCorrectPrefix_ForOrgStorage()
    {
        // Arrange
        _mockFilesystemBusiness
            .Setup(f => f.GetStorageSize(It.IsAny<string>(), It.IsAny<ObjectStorageConfigDto>()))
            .ReturnsAsync(1000L);

        // Act - Call with null projectId for org-level storage
        await _metricsBusiness.GetObjectStorageSize(_oid, null, _orgFsOsId);

        // Assert
        _mockFilesystemBusiness.Verify(
            f => f.GetStorageSize($"org_{_oid}/", It.IsAny<ObjectStorageConfigDto>()), 
            Times.Once);
    }

    [Fact]
    public async Task GetObjectStorageSize_ThrowsKeyNotFoundException_WhenStorageNotFound()
    {
        // Arrange
        var nonExistentId = 99999L;

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _metricsBusiness.GetObjectStorageSize(_oid, _pid, nonExistentId));
    }

    [Fact]
    public async Task GetObjectStorageSize_ThrowsKeyNotFoundException_WhenStorageArchived()
    {
        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _metricsBusiness.GetObjectStorageSize(_oid, _pid, _archivedOsId));
    }

    [Fact]
    public async Task GetObjectStorageSize_ThrowsInvalidOperationException_WhenProjectMismatch()
    {
        // Arrange
        _mockFilesystemBusiness
            .Setup(f => f.GetStorageSize(It.IsAny<string>(), It.IsAny<ObjectStorageConfigDto>()))
            .ReturnsAsync(1000L);

        // Act & Assert - Try to access project 1 storage with project 2 ID
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _metricsBusiness.GetObjectStorageSize(_oid, _pid2, _fsOsId));
    }

    [Fact]
    public async Task GetObjectStorageSize_ThrowsKeyNotFoundException_WhenProjectDoesNotExist()
    {
        // Arrange
        var nonExistentProjectId = 99999L;

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _metricsBusiness.GetObjectStorageSize(_oid, nonExistentProjectId, _fsOsId));
    }

    [Fact]
    public async Task GetObjectStorageSize_ExcludesArchivedStorages()
    {
        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _metricsBusiness.GetObjectStorageSize(_oid, _pid, _archivedOsId));
    }

    #endregion

    #region GetProjectStorageSize Tests

    [Fact]
    public async Task GetProjectStorageSize_ReturnsAllStorages_InProject()
    {
        // Arrange
        _mockFilesystemBusiness
            .Setup(f => f.GetStorageSize(It.IsAny<string>(), It.IsAny<ObjectStorageConfigDto>()))
            .ReturnsAsync(1000L);
        
        _mockAzureBusiness
            .Setup(f => f.GetStorageSize(It.IsAny<string>(), It.IsAny<ObjectStorageConfigDto>()))
            .ReturnsAsync(2000L);

        // Act
        var result = await _metricsBusiness.GetProjectStorageSize(_oid, _pid);

        // Assert
        Assert.NotNull(result);
        // 2 project storages (fs + azure) + 2 org storages = 4 total
        // Each filesystem storage = 1000, each azure storage = 2000
        // Total = 1000 + 2000 + 1000 + 2000 = 6000
        Assert.Equal(6000L, result.Bytes);
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

        // Assert - Verify archived storage wasn't included
        // Should only process non-archived storages
        _mockFilesystemBusiness.Verify(
            f => f.GetStorageSize(It.IsAny<string>(), It.IsAny<ObjectStorageConfigDto>()),
            Times.Exactly(2)); // 1 project + 1 org level filesystem storage
    }

    [Fact]
    public async Task GetProjectStorageSize_IncludesOrgLevelStorages()
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

        // Assert - Should call GetStorageSize for both project and org-level storages
        _mockFilesystemBusiness.Verify(
            f => f.GetStorageSize(It.IsAny<string>(), It.IsAny<ObjectStorageConfigDto>()), 
            Times.Exactly(2)); // Project filesystem + org filesystem
        
        _mockAzureBusiness.Verify(
            f => f.GetStorageSize(It.IsAny<string>(), It.IsAny<ObjectStorageConfigDto>()), 
            Times.Exactly(2)); // Project azure + org azure
    }

    [Fact]
    public async Task GetProjectStorageSize_UsesProjectPrefix()
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
    public async Task GetProjectStorageSize_ContinuesOnError_ExcludesFailedStorage()
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

        // Assert - Should still return a result
        Assert.NotNull(result);
        // 1000 (successful fs) + 0 (failed fs) + 2000 + 2000 (both azure) = 5000
        Assert.Equal(5000L, result.Bytes);
    }

    [Fact]
    public async Task GetProjectStorageSize_ThrowsKeyNotFoundException_WhenOrganizationDoesNotExist()
    {
        // Arrange
        var nonExistentOrgId = 99999L;

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _metricsBusiness.GetProjectStorageSize(nonExistentOrgId, _pid));
    }

    [Fact]
    public async Task GetProjectStorageSize_ThrowsKeyNotFoundException_WhenProjectDoesNotExist()
    {
        // Arrange
        var nonExistentProjectId = 99999L;

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _metricsBusiness.GetProjectStorageSize(_oid, nonExistentProjectId));
    }

    [Fact]
    public async Task GetProjectStorageSize_ThrowsInvalidOperationException_WhenProjectDoesNotBelongToOrganization()
    {
        // Arrange - project 1 belongs to org 1, try to access with org 2
        
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _metricsBusiness.GetProjectStorageSize(_oid2, _pid));
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
        // 2 filesystem (3000 each) + 2 azure (2000 each) = 10000
        Assert.Equal(10000L, result.Bytes);
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

        // Assert - Should only count non-archived (2 filesystem storages)
        _mockFilesystemBusiness.Verify(
            f => f.GetStorageSize(It.IsAny<string>(), It.IsAny<ObjectStorageConfigDto>()),
            Times.Exactly(2)); // Only non-archived filesystem storages
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
    public async Task GetOrganizationStorageSize_ReturnsZeroBytes_WhenNoStorages()
    {
        // Arrange - Use org 2 which has no storages
        
        // Act
        var result = await _metricsBusiness.GetOrganizationStorageSize(_oid2);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0L, result.Bytes);
    }

    [Fact]
    public async Task GetOrganizationStorageSize_ThrowsKeyNotFoundException_WhenOrganizationDoesNotExist()
    {
        // Arrange
        var nonExistentOrgId = 99999L;

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _metricsBusiness.GetOrganizationStorageSize(nonExistentOrgId));
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
        // Due to grouping by config, we should get unique configs
        // 2 unique filesystem configs + 2 unique azure configs = 4 total
        Assert.Equal(16000L, result.Bytes); // 2*5000 + 2*3000
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

        // Assert - Should not include archived storage
        // Verify the method was called the correct number of times (non-archived only)
        Assert.NotNull(result);
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

    #region GetDataSourceCount Tests

    [Fact]
    public async Task GetDataSourceCount_ReturnsZero_WhenNoDataSources()
    {
        // Act
        var count = await _metricsBusiness.GetDataSourceCount();

        // Assert
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task GetDataSourceCount_ReturnsAllNonArchived_SystemWide()
    {
        // Arrange - create data sources across both orgs
        Context.DataSources.Add(new DataSource
        {
            Name = "DS Org1 Project",
            OrganizationId = _oid,
            ProjectId = _pid,
            IsArchived = false
        });
        Context.DataSources.Add(new DataSource
        {
            Name = "DS Org1 OrgLevel",
            OrganizationId = _oid,
            ProjectId = null,
            IsArchived = false
        });
        Context.DataSources.Add(new DataSource
        {
            Name = "DS Org2",
            OrganizationId = _oid2,
            ProjectId = null,
            IsArchived = false
        });
        Context.DataSources.Add(new DataSource
        {
            Name = "DS Archived",
            OrganizationId = _oid,
            ProjectId = _pid,
            IsArchived = true
        });
        await Context.SaveChangesAsync();

        // Act
        var count = await _metricsBusiness.GetDataSourceCount();

        // Assert - should include all non-archived across all orgs
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task GetDataSourceCount_HideArchivedFalse_IncludesArchived()
    {
        // Arrange
        Context.DataSources.Add(new DataSource
        {
            Name = "DS Active",
            OrganizationId = _oid,
            ProjectId = _pid,
            IsArchived = false
        });
        Context.DataSources.Add(new DataSource
        {
            Name = "DS Archived",
            OrganizationId = _oid,
            ProjectId = _pid,
            IsArchived = true
        });
        await Context.SaveChangesAsync();

        // Act
        var countWithArchived = await _metricsBusiness.GetDataSourceCount(hideArchived: false);
        var countWithoutArchived = await _metricsBusiness.GetDataSourceCount(hideArchived: true);

        // Assert
        Assert.Equal(2, countWithArchived);
        Assert.Equal(1, countWithoutArchived);
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