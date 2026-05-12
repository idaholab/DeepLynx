using Azure.Storage.Blobs;
using deeplynx.business;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.interfaces;
using deeplynx.models;
using Moq;
using DlRecord = deeplynx.datalayer.Models.Record;
using Testcontainers.Azurite;
using Record = deeplynx.datalayer.Models.Record;

namespace deeplynx.tests;
 
// Fixture for Azurite container
public class MetricsAzuriteFixture : IAsyncLifetime
{
    private AzuriteContainer _azuriteContainer = null!;
 
    public string AzuriteConnectionString { get; private set; } = null!;
 
    public async Task InitializeAsync()
    {
        _azuriteContainer = new AzuriteBuilder()
            .WithImage("mcr.microsoft.com/azure-storage/azurite:latest")
            .Build();
 
        await _azuriteContainer.StartAsync();
        AzuriteConnectionString = _azuriteContainer.GetConnectionString();
    }
 
    public async Task DisposeAsync()
    {
        await _azuriteContainer.DisposeAsync();
    }
}
 
[Collection("Test Suite Collection")]
public class MetricsBusinessTests : IntegrationTestBase, IClassFixture<MetricsAzuriteFixture>
{
    private readonly MetricsAzuriteFixture _azuriteFixture;
    private readonly string _filesystemBasePath = Path.Combine(Path.GetTempPath(), "MetricsBusinessTests");
    
    private MetricsBusiness _metricsBusiness = null!;
    private IFileBusinessFactory _fileBusinessFactory = null!;
    private IObjectStorageBusiness _objectStorageBusiness = null!;
    private EncryptionHelper _encryptionHelper = null!;
 
    // Organization IDs
    private long _org1Id;
    private long _org2Id;
 
    // Project IDs
    private long _org1Proj1Id; // Org1, Project1
    private long _org1Proj2Id; // Org1, Project2
    private long _org2Proj1Id; // Org2, Project1
    private long _org2Proj2Id; // Org2, Project2
 
    // Filesystem Object Storage IDs
    private long _fsOrg1Proj1StorageId;
    private long _fsOrg2Proj1StorageId;
    private long _fsOrg2Proj2StorageId;
 
    // Azure Object Storage IDs
    private long _azureOrg1Proj1StorageId;
    private long _azureOrg2Proj1StorageId;
    private long _azureOrg2Proj2StorageId;
 
    // User ID
    private long _userId;
    
    private long _dsOrg1Proj1Id;
    private long _dsOrg2Proj1Id;
    private long _dsOrg2Proj2Id;
 
    public MetricsBusinessTests(TestSuiteFixture fixture, MetricsAzuriteFixture azuriteFixture) : base(fixture)
    {
        _azuriteFixture = azuriteFixture;
    }
 
    public override async Task InitializeAsync()
    {
        // Set up encryption keys
        Environment.SetEnvironmentVariable("ENCRYPTION_KEY", "SU5TRUNVUkVfREVWX0tFWV8zMl9CWVRFU19MT05HISE=");
        Environment.SetEnvironmentVariable("ENCRYPTION_IV", "SU5TRUNVUkVfREVWX0lWIQ==");
        
        _encryptionHelper = new EncryptionHelper();
        
        await base.InitializeAsync();
 
        // Create filesystem directory
        Directory.CreateDirectory(_filesystemBasePath);
        
        // Ensure all seeded data is committed before initializing business layer
        await Context.SaveChangesAsync();
 
        // Initialize business layer
        _objectStorageBusiness = new ObjectStorageBusiness(Context, _encryptionHelper);
        
        var fileBusinessFactory = new Mock<IFileBusinessFactory>();
        var filesystemBusiness = new FileFilesystemBusiness(Context, _objectStorageBusiness, null!, null!);
        var azureBusiness = new FileAzureBusiness();
        
        fileBusinessFactory.Setup(x => x.CreateFileBusiness("filesystem")).Returns(filesystemBusiness);
        fileBusinessFactory.Setup(x => x.CreateFileBusiness("azure_object")).Returns(azureBusiness);
        
        _fileBusinessFactory = fileBusinessFactory.Object;
        
        _metricsBusiness = new MetricsBusiness(Context, _fileBusinessFactory, _objectStorageBusiness);
    }
 
    public override async Task DisposeAsync()
    {
        // Clean up filesystem
        if (Directory.Exists(_filesystemBasePath))
        {
            try
            {
                Directory.Delete(_filesystemBasePath, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
 
        // Clean up Azure containers
        var blobServiceClient = new BlobServiceClient(_azuriteFixture.AzuriteConnectionString);
        await foreach (var containerItem in blobServiceClient.GetBlobContainersAsync())
        {
            var container = blobServiceClient.GetBlobContainerClient(containerItem.Name);
            await container.DeleteIfExistsAsync();
        }
 
        await base.DisposeAsync();
    }
 
    protected override async Task SeedTestDataAsync()
    {
        await base.SeedTestDataAsync();

        // Create user
        var user = new User
        {
            Name = "Test User",
            Email = "metrics-test@test.com",
            Password = "test_password",
            IsArchived = false
        };
        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        _userId = user.Id;

        // Create Organization 1
        var org1 = new Organization
        {
            Name = "Organization 1",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = _userId
        };
        Context.Organizations.Add(org1);
        await Context.SaveChangesAsync();
        _org1Id = org1.Id;

        // Create Organization 2
        var org2 = new Organization
        {
            Name = "Organization 2",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = _userId
        };
        Context.Organizations.Add(org2);
        await Context.SaveChangesAsync();
        _org2Id = org2.Id;

        // Create Project 1 for Org 1
        var org1Proj1 = new Project
        {
            Name = "Org1 Project1",
            OrganizationId = _org1Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = _userId
        };
        Context.Projects.Add(org1Proj1);
        await Context.SaveChangesAsync();
        _org1Proj1Id = org1Proj1.Id;

        // Create Project 2 for Org 1 (sibling project used by data-modality tests)
        var org1Proj2 = new Project
        {
            Name = "Org1 Project2",
            OrganizationId = _org1Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = _userId
        };
        Context.Projects.Add(org1Proj2);
        await Context.SaveChangesAsync();
        _org1Proj2Id = org1Proj2.Id;

        // Create Project 1 for Org 2
        var org2Proj1 = new Project
        {
            Name = "Org2 Project1",
            OrganizationId = _org2Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = _userId
        };
        Context.Projects.Add(org2Proj1);
        await Context.SaveChangesAsync();
        _org2Proj1Id = org2Proj1.Id;

        // Create Project 2 for Org 2
        var org2Proj2 = new Project
        {
            Name = "Org2 Project2",
            OrganizationId = _org2Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = _userId
        };
        Context.Projects.Add(org2Proj2);
        await Context.SaveChangesAsync();
        _org2Proj2Id = org2Proj2.Id;

        // ========== FILESYSTEM OBJECT STORAGES ==========

        // Filesystem storage for Org1 Project1
        var fsOrg1Proj1Config = new ObjectStorageConfigDto
        {
            MountPath = _filesystemBasePath
        };
        var fsOrg1Proj1Storage = new ObjectStorage
        {
            Name = "FS Org1 Proj1 Storage",
            ProjectId = _org1Proj1Id,
            OrganizationId = _org1Id,
            Type = "filesystem",
            ConfigEncrypted = _encryptionHelper.SerializeAndEncrypt(fsOrg1Proj1Config),
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = _userId
        };
        Context.ObjectStorages.Add(fsOrg1Proj1Storage);
        await Context.SaveChangesAsync();
        _fsOrg1Proj1StorageId = fsOrg1Proj1Storage.Id;

        // Filesystem storage for Org2 Project1
        var fsOrg2Proj1Config = new ObjectStorageConfigDto
        {
            MountPath = _filesystemBasePath
        };
        var fsOrg2Proj1Storage = new ObjectStorage
        {
            Name = "FS Org2 Proj1 Storage",
            ProjectId = _org2Proj1Id,
            OrganizationId = _org2Id,
            Type = "filesystem",
            ConfigEncrypted = _encryptionHelper.SerializeAndEncrypt(fsOrg2Proj1Config),
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = _userId
        };
        Context.ObjectStorages.Add(fsOrg2Proj1Storage);
        await Context.SaveChangesAsync();
        _fsOrg2Proj1StorageId = fsOrg2Proj1Storage.Id;

        // Filesystem storage for Org2 Project2
        var fsOrg2Proj2Config = new ObjectStorageConfigDto
        {
            MountPath = _filesystemBasePath
        };
        var fsOrg2Proj2Storage = new ObjectStorage
        {
            Name = "FS Org2 Proj2 Storage",
            ProjectId = _org2Proj2Id,
            OrganizationId = _org2Id,
            Type = "filesystem",
            ConfigEncrypted = _encryptionHelper.SerializeAndEncrypt(fsOrg2Proj2Config),
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = _userId
        };
        Context.ObjectStorages.Add(fsOrg2Proj2Storage);
        await Context.SaveChangesAsync();
        _fsOrg2Proj2StorageId = fsOrg2Proj2Storage.Id;

        // ========== AZURE OBJECT STORAGES ==========

        // Azure storage for Org1 Project1
        var azureOrg1Proj1Config = new ObjectStorageConfigDto
        {
            AzureObjectConfig = new AzureObjectConfigDto
            {
                AzureConnectionString = _azuriteFixture.AzuriteConnectionString,
                AzureContainerName = "org1-proj1-container"
            }
        };
        var azureOrg1Proj1Storage = new ObjectStorage
        {
            Name = "Azure Org1 Proj1 Storage",
            ProjectId = _org1Proj1Id,
            OrganizationId = _org1Id,
            Type = "azure_object",
            ConfigEncrypted = _encryptionHelper.SerializeAndEncrypt(azureOrg1Proj1Config),
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = _userId
        };
        Context.ObjectStorages.Add(azureOrg1Proj1Storage);
        await Context.SaveChangesAsync();
        _azureOrg1Proj1StorageId = azureOrg1Proj1Storage.Id;

        // Azure storage for Org2 Project1
        var azureOrg2Proj1Config = new ObjectStorageConfigDto
        {
            AzureObjectConfig = new AzureObjectConfigDto
            {
                AzureConnectionString = _azuriteFixture.AzuriteConnectionString,
                AzureContainerName = "org2-proj1-container"
            }
        };
        var azureOrg2Proj1Storage = new ObjectStorage
        {
            Name = "Azure Org2 Proj1 Storage",
            ProjectId = _org2Proj1Id,
            OrganizationId = _org2Id,
            Type = "azure_object",
            ConfigEncrypted = _encryptionHelper.SerializeAndEncrypt(azureOrg2Proj1Config),
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = _userId
        };
        Context.ObjectStorages.Add(azureOrg2Proj1Storage);
        await Context.SaveChangesAsync();
        _azureOrg2Proj1StorageId = azureOrg2Proj1Storage.Id;

        // Azure storage for Org2 Project2
        var azureOrg2Proj2Config = new ObjectStorageConfigDto
        {
            AzureObjectConfig = new AzureObjectConfigDto
            {
                AzureConnectionString = _azuriteFixture.AzuriteConnectionString,
                AzureContainerName = "org2-proj2-container"
            }
        };
        var azureOrg2Proj2Storage = new ObjectStorage
        {
            Name = "Azure Org2 Proj2 Storage",
            ProjectId = _org2Proj2Id,
            OrganizationId = _org2Id,
            Type = "azure_object",
            ConfigEncrypted = _encryptionHelper.SerializeAndEncrypt(azureOrg2Proj2Config),
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = _userId
        };
        Context.ObjectStorages.Add(azureOrg2Proj2Storage);
        await Context.SaveChangesAsync();
        _azureOrg2Proj2StorageId = azureOrg2Proj2Storage.Id;

        // ========== CREATE FILESYSTEM DIRECTORY STRUCTURES WITH FILES ==========

        // Org1 Project1 files (3KB total)
        var org1Proj1Path = Path.Combine(_filesystemBasePath, $"org_{_org1Id}", $"project_{_org1Proj1Id}");
        Directory.CreateDirectory(org1Proj1Path);
        await File.WriteAllBytesAsync(Path.Combine(org1Proj1Path, "file1.txt"), new byte[1024]); // 1KB
        await File.WriteAllBytesAsync(Path.Combine(org1Proj1Path, "file2.txt"), new byte[2048]); // 2KB

        // Org2 Project1 files (5KB total)
        var org2Proj1Path = Path.Combine(_filesystemBasePath, $"org_{_org2Id}", $"project_{_org2Proj1Id}");
        Directory.CreateDirectory(org2Proj1Path);
        await File.WriteAllBytesAsync(Path.Combine(org2Proj1Path, "file1.txt"), new byte[2048]); // 2KB
        await File.WriteAllBytesAsync(Path.Combine(org2Proj1Path, "file2.txt"), new byte[3072]); // 3KB

        // Org2 Project2 files (7KB total)
        var org2Proj2Path = Path.Combine(_filesystemBasePath, $"org_{_org2Id}", $"project_{_org2Proj2Id}");
        Directory.CreateDirectory(org2Proj2Path);
        await File.WriteAllBytesAsync(Path.Combine(org2Proj2Path, "file1.txt"), new byte[3072]); // 3KB
        await File.WriteAllBytesAsync(Path.Combine(org2Proj2Path, "file2.txt"), new byte[4096]); // 4KB

        // ========== CREATE AZURE BLOBS ==========

        // Org1 Project1 blobs (4KB total)
        var org1Proj1Container = new BlobContainerClient(
            _azuriteFixture.AzuriteConnectionString, 
            "org1-proj1-container");
        await org1Proj1Container.CreateIfNotExistsAsync();
        await UploadBlobAsync(org1Proj1Container, $"organization_{_org1Id}/project_{_org1Proj1Id}/file1.txt", 2048); // 2KB
        await UploadBlobAsync(org1Proj1Container, $"organization_{_org1Id}/project_{_org1Proj1Id}/file2.txt", 2048); // 2KB

        // Org2 Project1 blobs (6KB total)
        var org2Proj1Container = new BlobContainerClient(
            _azuriteFixture.AzuriteConnectionString, 
            "org2-proj1-container");
        await org2Proj1Container.CreateIfNotExistsAsync();
        await UploadBlobAsync(org2Proj1Container, $"organization_{_org2Id}/project_{_org2Proj1Id}/file1.txt", 3072); // 3KB
        await UploadBlobAsync(org2Proj1Container, $"organization_{_org2Id}/project_{_org2Proj1Id}/file2.txt", 3072); // 3KB

        // Org2 Project2 blobs (9KB total)
        var org2Proj2Container = new BlobContainerClient(
            _azuriteFixture.AzuriteConnectionString, 
            "org2-proj2-container");
        await org2Proj2Container.CreateIfNotExistsAsync();
        await UploadBlobAsync(org2Proj2Container, $"organization_{_org2Id}/project_{_org2Proj2Id}/file1.txt", 4096); // 4KB
        await UploadBlobAsync(org2Proj2Container, $"organization_{_org2Id}/project_{_org2Proj2Id}/file2.txt", 5120); // 5KB

        // ========== DATA SOURCES ==========

        // Data source for Org1 Project1
        var dsOrg1Proj1 = new DataSource
        {
            Name = "DS Org1 Project1",
            OrganizationId = _org1Id,
            ProjectId = _org1Proj1Id,
            IsArchived = false
        };
        Context.DataSources.Add(dsOrg1Proj1);

        // Data source for Org2 Project1
        var dsOrg2Proj1 = new DataSource
        {
            Name = "DS Org2 Project1",
            OrganizationId = _org2Id,
            ProjectId = _org2Proj1Id,
            IsArchived = false
        };
        Context.DataSources.Add(dsOrg2Proj1);
        
        // Archived data source for Org2 Project1
        var arcOrg2Proj1Id = new DataSource
        {
            Name = "Archived Org2 Project1",
            OrganizationId = _org2Id,
            ProjectId = _org2Proj1Id,
            IsArchived = true
        };
        Context.DataSources.Add(arcOrg2Proj1Id);

        // Data source for Org2 Project2
        var dsOrg2Proj2 = new DataSource
        {
            Name = "DS Org2 Project2",
            OrganizationId = _org2Id,
            ProjectId = _org2Proj2Id,
            IsArchived = false
        };
        Context.DataSources.Add(dsOrg2Proj2);

        // Org-level data source for Org1
        var dsOrg1OrgLevel = new DataSource
        {
            Name = "DS Org1 OrgLevel",
            OrganizationId = _org1Id,
            ProjectId = null,
            IsArchived = false
        };
        Context.DataSources.Add(dsOrg1OrgLevel);

        await Context.SaveChangesAsync();
        
        _dsOrg1Proj1Id = dsOrg1Proj1.Id;
        _dsOrg2Proj1Id = dsOrg2Proj1.Id;
        _dsOrg2Proj2Id = dsOrg2Proj2.Id;
        
        // ========== RECORDS ==========
        var records = new List<Record>
        {
            // Org1 Project1: 2 active with URI (files), 1 active without URI, 1 archived with URI
            new Record
            {
                Name = "Org1 Proj1 Record 1 (file)",
                Description = "Active record with file",
                OriginalId = Guid.NewGuid().ToString(),
                Properties = "{}",
                ProjectId = _org1Proj1Id,
                DataSourceId = _dsOrg1Proj1Id,
                OrganizationId = _org1Id,
                IsArchived = false,
                Uri = "localhost:8090/file1.pdf",
                LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                LastUpdatedBy = _userId
            },
            new Record
            {
                Name = "Org1 Proj1 Record 2 (file)",
                Description = "Active record with file",
                OriginalId = Guid.NewGuid().ToString(),
                Properties = "{}",
                ProjectId = _org1Proj1Id,
                DataSourceId = _dsOrg1Proj1Id,
                OrganizationId = _org1Id,
                IsArchived = false,
                Uri = "localhost:8090/file2.pdf",
                LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                LastUpdatedBy = _userId
            },
            new Record
            {
                Name = "Org1 Proj1 Record 3 (no file)",
                Description = "Active record without file",
                OriginalId = Guid.NewGuid().ToString(),
                Properties = "{}",
                ProjectId = _org1Proj1Id,
                DataSourceId = _dsOrg1Proj1Id,
                OrganizationId = _org1Id,
                IsArchived = false,
                Uri = null,
                LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                LastUpdatedBy = _userId
            },
            new Record
            {
                Name = "Org1 Proj1 Record 4 (archived file)",
                Description = "Archived record with file",
                OriginalId = Guid.NewGuid().ToString(),
                Properties = "{}",
                ProjectId = _org1Proj1Id,
                DataSourceId = _dsOrg1Proj1Id,
                OrganizationId = _org1Id,
                IsArchived = true,
                Uri = "localhost:8090/file4.pdf",
                LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                LastUpdatedBy = _userId
            },
            // Org2 Project1: 3 active with URI, 1 active without URI, 1 archived with URI
            new Record
            {
                Name = "Org2 Proj1 Record 1 (file)",
                Description = "Active record with file",
                OriginalId = Guid.NewGuid().ToString(),
                Properties = "{}",
                ProjectId = _org2Proj1Id,
                DataSourceId = _dsOrg2Proj1Id,
                OrganizationId = _org2Id,
                IsArchived = false,
                Uri = "localhost:8090/file1.pdf",
                LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                LastUpdatedBy = _userId
            },
            new Record
            {
                Name = "Org2 Proj1 Record 2 (file)",
                Description = "Active record with file",
                OriginalId = Guid.NewGuid().ToString(),
                Properties = "{}",
                ProjectId = _org2Proj1Id,
                DataSourceId = _dsOrg2Proj1Id,
                OrganizationId = _org2Id,
                IsArchived = false,
                Uri = "localhost:8090/file2.pdf",
                LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                LastUpdatedBy = _userId
            },
            new Record
            {
                Name = "Org2 Proj1 Record 3 (file)",
                Description = "Active record with file",
                OriginalId = Guid.NewGuid().ToString(),
                Properties = "{}",
                ProjectId = _org2Proj1Id,
                DataSourceId = _dsOrg2Proj1Id,
                OrganizationId = _org2Id,
                IsArchived = false,
                Uri = "localhost:8090/file3.pdf",
                LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                LastUpdatedBy = _userId
            },
            new Record
            {
                Name = "Org2 Proj1 Record 4 (no file)",
                Description = "Active record without file",
                OriginalId = Guid.NewGuid().ToString(),
                Properties = "{}",
                ProjectId = _org2Proj1Id,
                DataSourceId = _dsOrg2Proj1Id,
                OrganizationId = _org2Id,
                IsArchived = false,
                Uri = null,
                LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                LastUpdatedBy = _userId
            },
            new Record
            {
                Name = "Org2 Proj1 Record 5 (archived file)",
                Description = "Archived record with file",
                OriginalId = Guid.NewGuid().ToString(),
                Properties = "{}",
                ProjectId = _org2Proj1Id,
                DataSourceId = _dsOrg2Proj1Id,
                OrganizationId = _org2Id,
                IsArchived = true,
                Uri = "localhost:8090/file5.pdf",
                LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                LastUpdatedBy = _userId
            },
            // Org2 Project2: 2 active with URI, 0 archived
            new Record
            {
                Name = "Org2 Proj2 Record 1 (file)",
                Description = "Active record with file",
                OriginalId = Guid.NewGuid().ToString(),
                Properties = "{}",
                ProjectId = _org2Proj2Id,
                DataSourceId = _dsOrg2Proj2Id,
                OrganizationId = _org2Id,
                IsArchived = false,
                Uri = "localhost:8090/file1.pdf",
                LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                LastUpdatedBy = _userId
            },
            new Record
            {
                Name = "Org2 Proj2 Record 2 (file)",
                Description = "Active record with file",
                OriginalId = Guid.NewGuid().ToString(),
                Properties = "{}",
                ProjectId = _org2Proj2Id,
                DataSourceId = _dsOrg2Proj2Id,
                OrganizationId = _org2Id,
                IsArchived = false,
                Uri = "localhost:8090/file2.pdf",
                LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                LastUpdatedBy = _userId
            }
        };
        Context.Records.AddRange(records);
        await Context.SaveChangesAsync();
    }
 
    #region Helper Methods
 
    private async Task UploadBlobAsync(BlobContainerClient container, string blobName, long sizeInBytes)
    {
        var blob = container.GetBlobClient(blobName);
        var content = new byte[sizeInBytes];
        new Random().NextBytes(content);
        using var stream = new MemoryStream(content);
        await blob.UploadAsync(stream, overwrite: true);
    }
 
    #endregion
 
    #region Get_StorageSize Tests
 
    [Fact]
    public async Task GetProjectStorageSize_AggregatesAllStorageTypes()
    {
        // Arrange
        // Org2 Project1 has:
        // - Filesystem: 5KB (2KB + 3KB)
        // - Azure: 6KB (3KB + 3KB)
        // Total: 11KB
 
        // Act
        var result = await _metricsBusiness.GetProjectStorageSize(_org2Id, _org2Proj1Id);
 
        // Assert
        Assert.NotNull(result);
        Assert.Equal(11264, result.Bytes); // Filesystem: 5120 + Azure: 6144 = 11264 bytes
    }
    
    [Fact]
    public async Task GetOrganizationStorageSize_ReturnsAllStorages_InOrganization()
    {
        // Arrange
        // Org2 has:
        // - Project1 Filesystem: 5KB (2KB + 3KB)
        // - Project1 Azure: 6KB (3KB + 3KB)
        // - Project2 Filesystem: 7KB (3KB + 4KB)
        // - Project2 Azure: 9KB (4KB + 5KB)
        // Total: 27KB

        // Act
        var result = await _metricsBusiness.GetOrganizationStorageSize(_org2Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(27648, result.Bytes); // (5120 + 6144) + (7168 + 9216) = 27648 bytes
    }

    [Fact]
    public async Task GetSystemStorageSize_ReturnsAllStorages_SystemWide()
    {
        // Arrange
        // System-wide storage across all organizations:
        // Org1:
        // - Project1 Filesystem: 3KB (1KB + 2KB)
        // - Project1 Azure: 4KB (2KB + 2KB)
        // Org2:
        // - Project1 Filesystem: 5KB (2KB + 3KB)
        // - Project1 Azure: 6KB (3KB + 3KB)
        // - Project2 Filesystem: 7KB (3KB + 4KB)
        // - Project2 Azure: 9KB (4KB + 5KB)
        // Total: 34KB

        // Act
        var result = await _metricsBusiness.GetSystemStorageSize();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(34816, result.Bytes); // Org1: (3072 + 4096) + Org2: (5120 + 6144 + 7168 + 9216) = 34816 bytes
    }
 
    #endregion
    
    #region GetSystemDataSourceCount Tests

    [Fact]
    public async Task GetSystemDataSourceCount_ReturnsZero_WhenNoDataSources()
    {
        // Arrange - delete existing data sources
        var dataSources = Context.DataSources.ToList();
        Context.DataSources.RemoveRange(dataSources);
        await Context.SaveChangesAsync();
        
        // Act
        var count = await _metricsBusiness.GetSystemDataSourceCount();

        // Assert
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task GetSystemDataSourceCount_ReturnsAllNonArchived_SystemWide()
    {
        // Act
        var count = await _metricsBusiness.GetSystemDataSourceCount();

        // Assert - should include all non-archived across all orgs
        Assert.Equal(4, count);
    }

    [Fact]
    public async Task GetSystemDataSourceCount_HideArchivedFalse_IncludesArchived()
    {
        // Act
        var countWithArchived = await _metricsBusiness.GetSystemDataSourceCount(hideArchived: false);
        var countWithoutArchived = await _metricsBusiness.GetSystemDataSourceCount(hideArchived: true);

        // Assert
        Assert.Equal(5, countWithArchived);
        Assert.Equal(4, countWithoutArchived);
    }

     #endregion

    #region GetProjectDataSourceCount Tests

    [Fact]
    public async Task GetProjectDataSourceCount_ReturnsCount_ForProject()
    { 
        // Act
        var count = await _metricsBusiness.GetProjectDataSourceCount(_org2Proj1Id);

        // Assert - only one non-archived data source for org 2 project 1
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task GetProjectDataSourceCount_IncludesArchived_WhenHideArchivedFalse()
    {
        // Act
        var count = await _metricsBusiness.GetProjectDataSourceCount(_org2Proj1Id, hideArchived: false);

        // Assert - two data sources counting the archived one
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task GetProjectDataSourceCount_ReturnsZero_WhenNoDataSources()
    {
        // Arrange - delete existing data sources for o2p2 to test this
        var org2Proj2DataSources = Context.DataSources
            .Where(ds => ds.ProjectId == _org2Proj2Id)
            .ToList();
        Context.DataSources.RemoveRange(org2Proj2DataSources);
        await Context.SaveChangesAsync();
        
        // Act - project 2 has no data sources
        var count = await _metricsBusiness.GetProjectDataSourceCount(_org2Proj2Id);

        // Assert
        Assert.Equal(0, count);
    }

    #endregion

    #region GetOrganizationDataSourceCount Tests

    [Fact]
    public async Task GetOrganizationDataSourceCount_NoProjectIds_ReturnsAllForOrganization()
    {
        // Act - no projectIds filter, returns all in org
        var count = await _metricsBusiness.GetOrganizationDataSourceCount(_org2Id, null);

        // Assert - only data sources belonging to org 2
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task GetOrganizationDataSourceCount_WithProjectIds_ReturnsProjectAndOrgLevel()
    {
        // Act - filter to project 1 only
        var count = await _metricsBusiness.GetOrganizationDataSourceCount(_org1Id, new[] { _org1Proj1Id });

        // Assert - project 1 data source + inherited org-level data source
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task GetOrganizationDataSourceCount_WithProjectIds_ExcludesOtherProjects()
    {
        // Act - filter to project 2 only
        var count = await _metricsBusiness.GetOrganizationDataSourceCount(_org2Id, new[] { _org2Proj2Id });

        // Assert - only project 2 data source (no org-level exists)
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task GetOrganizationDataSourceCount_ExcludesArchived_WhenHideArchivedTrue()
    {
        // Act
        var countWithArchived = await _metricsBusiness.GetOrganizationDataSourceCount(_org2Id, null, hideArchived: false);
        var countWithoutArchived = await _metricsBusiness.GetOrganizationDataSourceCount(_org2Id, null, hideArchived: true);

        // Assert
        Assert.Equal(3, countWithArchived);
        Assert.Equal(2, countWithoutArchived);
    }

    [Fact]
    public async Task GetOrganizationDataSourceCount_ReturnsZero_WhenNoDataSources()
    {
        // Arrange - delete sources from org 2
        var org2DataSources = Context.DataSources
            .Where(ds => ds.OrganizationId == _org2Id)
            .ToList();
        Context.DataSources.RemoveRange(org2DataSources);
        await Context.SaveChangesAsync();
        
        // Act - org 2 has no data sources
        var count = await _metricsBusiness.GetOrganizationDataSourceCount(_org2Id, null);

        // Assert
        Assert.Equal(0, count);
    }

    #endregion
    
    #region GetRecordCount Tests

    // ── Single projectId overload ─────────────────────────────────────────────

    [Fact]
    public async Task GetRecordCount_SingleProject_ReturnsActiveOnly_ByDefault()
    {
        // Org1 Project1 has 3 active records (archived one excluded)
        var count = await _metricsBusiness.GetRecordCount(_org1Id, (long?)_org1Proj1Id, hideArchived: true);

        Assert.Equal(3, count);
    }

    [Fact]
    public async Task GetRecordCount_SingleProject_IncludesArchived_WhenHideArchivedFalse()
    {
        // Org1 Project1: 3 active + 1 archived = 4
        var countActive = await _metricsBusiness.GetRecordCount(_org1Id, (long?)_org1Proj1Id, hideArchived: true);
        var countAll    = await _metricsBusiness.GetRecordCount(_org1Id, (long?)_org1Proj1Id, hideArchived: false);

        Assert.Equal(3, countActive);
        Assert.Equal(4, countAll);
    }

    [Fact]
    public async Task GetRecordCount_SingleProject_ReturnsZero_WhenProjectHasNoRecords()
    {
        // Arrange - remove all records for Org2 Project2
        var records = Context.Records.Where(r => r.ProjectId == _org2Proj2Id).ToList();
        Context.Records.RemoveRange(records);
        await Context.SaveChangesAsync();

        var count = await _metricsBusiness.GetRecordCount(_org2Id, (long?)_org2Proj2Id, hideArchived: true);
        Assert.Equal(0, count);
    }
    #endregion
    #region GetOrganizationDataModalityCount Tests

    [Fact]
    public async Task GetOrganizationDataModalityCount_ReturnsZero_WhenNoRecords()
    {
        // Act
        var count = await _metricsBusiness.GetOrganizationDataModalityCount(_org1Id, null);

        // Assert
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task GetRecordCount_SingleProject_NullProjectId_ReturnsAllActiveForOrg()
    {
        // Null projectId with a valid orgId returns all active records in that org.
        // Org2: Proj1 (4 active) + Proj2 (2 active) = 6
        var count = await _metricsBusiness.GetRecordCount(_org2Id, (long?)null, hideArchived: true);

        Assert.Equal(6, count);
    }

    // ── projectIds[] overload ─────────────────────────────────────────────────

    [Fact]
    public async Task GetRecordCount_MultipleProjectIds_AggregatesAcrossProjects()
    {
        // Org2 Proj1 (4 active) + Org2 Proj2 (2 active) = 6
        var count = await _metricsBusiness.GetRecordCount(
            _org2Id,
            new[] { _org2Proj1Id, _org2Proj2Id },
            hideArchived: true);

        Assert.Equal(6, count);
    }

    [Fact]
    public async Task GetRecordCount_MultipleProjectIds_IncludesArchived_WhenHideArchivedFalse()
    {
        // Org2 Proj1: 4 active + 1 archived; Org2 Proj2: 2 active + 0 archived
        var countActive = await _metricsBusiness.GetRecordCount(
            _org2Id, new[] { _org2Proj1Id, _org2Proj2Id }, hideArchived: true);
        var countAll = await _metricsBusiness.GetRecordCount(
            _org2Id, new[] { _org2Proj1Id, _org2Proj2Id }, hideArchived: false);

        Assert.Equal(6, countActive);
        Assert.Equal(7, countAll);
    }

    [Fact]
    public async Task GetRecordCount_MultipleProjectIds_SingleEntry_MatchesSingleProjectOverload()
    {
        // Array overload with one project ID must agree with the single-ID overload
        var countArray  = await _metricsBusiness.GetRecordCount(_org2Id, new[] { _org2Proj1Id }, hideArchived: true);
        var countSingle = await _metricsBusiness.GetRecordCount(_org2Id, (long?)_org2Proj1Id, hideArchived: true);

        Assert.Equal(countSingle, countArray);
    }

    [Fact]
    public async Task GetRecordCount_MultipleProjectIds_EmptyArray_ReturnsAllActiveForOrg()
    {
        // An empty array (Length == 0) skips the projectIds filter, returning the whole org.
        // Org1: 3 active records
        var count = await _metricsBusiness.GetRecordCount(_org1Id, Array.Empty<long>(), hideArchived: true);

        Assert.Equal(3, count);
    }

    // ── Null / system-wide scoping ────────────────────────────────────────────

    [Fact]
    public async Task GetRecordCount_NullOrgAndNullProjectIds_ReturnsSystemWideActiveCount()
    {
        // No org or project filter = all active records system-wide.
        // Org1 Proj1: 3 + Org2 Proj1: 4 + Org2 Proj2: 2 = 9
        var count = await _metricsBusiness.GetRecordCount(
            (long?)null, (long[]?)null, hideArchived: true);

        Assert.Equal(9, count);
    }

    [Fact]
    public async Task GetRecordCount_NullOrgAndNullProjectIds_IncludesArchived_WhenHideArchivedFalse()
    {
        // Active (9) + archived (Org1 Proj1: 1, Org2 Proj1: 1) = 11
        var countActive = await _metricsBusiness.GetRecordCount((long?)null, (long[]?)null, hideArchived: true);
        var countAll    = await _metricsBusiness.GetRecordCount((long?)null, (long[]?)null, hideArchived: false);

        Assert.Equal(9, countActive);
        Assert.Equal(11, countAll);
    }

    [Fact]
    public async Task GetRecordCount_NullOrgAndNullProjectIds_ReturnsZero_WhenNoRecordsExist()
    {
        Context.Records.RemoveRange(Context.Records.ToList());
        await Context.SaveChangesAsync();

        var count = await _metricsBusiness.GetRecordCount((long?)null, (long[]?)null, hideArchived: true);

        Assert.Equal(0, count);
    }

    // ── Cross-org isolation ───────────────────────────────────────────────────

    [Fact]
    public async Task GetRecordCount_OrgScoped_DoesNotLeakAcrossOrganizations()
    {
        // Each org query must return only its own records
        var org1Count = await _metricsBusiness.GetRecordCount(_org1Id, (long[]?)null, hideArchived: true);
        var org2Count = await _metricsBusiness.GetRecordCount(_org2Id, (long[]?)null, hideArchived: true);

        Assert.Equal(3, org1Count); // only Org1 Proj1's 2 active records
        Assert.Equal(6, org2Count); // Org2 Proj1 (3) + Org2 Proj2 (2)
        Assert.NotEqual(org1Count, org2Count);
    }

    #endregion
    
    #region GetFileCount Tests

    // ── Single project ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetFileCount_SingleProject_ReturnsOnlyRecordsWithUri()
    {
        // Org1 Proj1 has 3 active records but only 2 have a URI
        var count = await _metricsBusiness.GetFileCount(_org1Id, new[] { _org1Proj1Id }, hideArchived: true);
        Assert.Equal(2, count);
    }

    public async Task GetOrganizationDataModalityCount_ReturnsDistinctFileTypeCount_NoProjectFilter()
    {
        // Arrange
        var ds = new DataSource { Name = "DS", OrganizationId = _org1Id, ProjectId = _org1Proj1Id, IsArchived = false };
        Context.DataSources.Add(ds);
        await Context.SaveChangesAsync();

        Context.Records.AddRange(
            new DlRecord { Name ="R1", OriginalId = "1", Properties = "{}", Description = "", OrganizationId = _org1Id, ProjectId = _org1Proj1Id, DataSourceId = ds.Id, LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified), FileType = "image/png" },
            new DlRecord { Name ="R2", OriginalId = "2", Properties = "{}", Description = "", OrganizationId = _org1Id, ProjectId = _org1Proj1Id, DataSourceId = ds.Id, LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified), FileType = "image/png" },
            new DlRecord { Name ="R3", OriginalId = "3", Properties = "{}", Description = "", OrganizationId = _org1Id, ProjectId = _org1Proj1Id, DataSourceId = ds.Id, LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified), FileType = "text/csv" },
            new DlRecord { Name ="R4", OriginalId = "4", Properties = "{}", Description = "", OrganizationId = _org1Id, ProjectId = _org1Proj2Id, DataSourceId = ds.Id, LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified), FileType = "application/json" }
        );
        await Context.SaveChangesAsync();

        // Act
        var count = await _metricsBusiness.GetOrganizationDataModalityCount(_org1Id, null);

        // Assert - 3 distinct file types across org (png, csv, json)
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task GetOrganizationDataModalityCount_WithProjectFilter_ReturnsCountForThatProject()
    {
        // Arrange
        var ds = new DataSource { Name = "DS", OrganizationId = _org1Id, ProjectId = _org1Proj1Id, IsArchived = false };
        Context.DataSources.Add(ds);
        await Context.SaveChangesAsync();

        Context.Records.AddRange(
            new DlRecord { Name ="R1", OriginalId = "1", Properties = "{}", Description = "", OrganizationId = _org1Id, ProjectId = _org1Proj1Id, DataSourceId = ds.Id, LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified), FileType = "image/png" },
            new DlRecord { Name ="R2", OriginalId = "2", Properties = "{}", Description = "", OrganizationId = _org1Id, ProjectId = _org1Proj1Id, DataSourceId = ds.Id, LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified), FileType = "text/csv" },
            new DlRecord { Name ="R3", OriginalId = "3", Properties = "{}", Description = "", OrganizationId = _org1Id, ProjectId = _org1Proj2Id, DataSourceId = ds.Id, LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified), FileType = "application/json" }
        );
        await Context.SaveChangesAsync();

        // Act
        var count = await _metricsBusiness.GetOrganizationDataModalityCount(_org1Id, _org1Proj1Id);

        // Assert - only records in project 1: png and csv
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task GetFileCount_SingleProject_IncludesArchived_WhenHideArchivedFalse()
    {
        // Org1 Proj1: 2 active files + 1 archived file = 3
        var countActive = await _metricsBusiness.GetFileCount(_org1Id, new[] { _org1Proj1Id }, hideArchived: true);
        var countAll    = await _metricsBusiness.GetFileCount(_org1Id, new[] { _org1Proj1Id }, hideArchived: false);

        Assert.Equal(2, countActive);
        Assert.Equal(3, countAll);
    }

    [Fact]
    public async Task GetFileCount_SingleProject_NeverCountsRecordsWithNullUri()
    {
        // Even with hideArchived: false, records without a URI must never appear
        var fileCount   = await _metricsBusiness.GetFileCount(_org2Id, new[] { _org2Proj1Id }, hideArchived: false);
        var recordCount = await _metricsBusiness.GetRecordCount(_org2Id, new[] { _org2Proj1Id }, hideArchived: false);

        // file count must be strictly less than record count because Org2 Proj1 has null-URI records
        Assert.True(fileCount < recordCount);
    }

    [Fact]
    public async Task GetFileCount_SingleProject_ReturnsZero_WhenNoFilesExist()
    {
        // Arrange - clear all URIs for Org2 Proj2
        var proj2Records = Context.Records.Where(r => r.ProjectId == _org2Proj2Id).ToList();
        foreach (var r in proj2Records) r.Uri = null;
        await Context.SaveChangesAsync();

        var count = await _metricsBusiness.GetFileCount(_org2Id, new[] { _org2Proj2Id }, hideArchived: true);

        Assert.Equal(0, count);
    }

    // ── Multiple projects ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetFileCount_MultipleProjects_AggregatesAcrossProjects()
    {
        // Org2 Proj1 (3 active files) + Org2 Proj2 (2 active files) = 5
        var count = await _metricsBusiness.GetFileCount(
            _org2Id, new[] { _org2Proj1Id, _org2Proj2Id }, hideArchived: true);

        Assert.Equal(5, count);
    }

    [Fact]
    public async Task GetFileCount_MultipleProjects_IncludesArchived_WhenHideArchivedFalse()
    {
        // Org2 Proj1: 3 active + 1 archived; Org2 Proj2: 2 active + 0 archived = 6 total files
        var countActive = await _metricsBusiness.GetFileCount(
            _org2Id, new[] { _org2Proj1Id, _org2Proj2Id }, hideArchived: true);
        var countAll = await _metricsBusiness.GetFileCount(
            _org2Id, new[] { _org2Proj1Id, _org2Proj2Id }, hideArchived: false);

        Assert.Equal(5, countActive);
        Assert.Equal(6, countAll);
    }

    [Fact]
    public async Task GetFileCount_MultipleProjects_EmptyArray_ReturnsAllActiveFilesForOrg()
    {
        // Empty projectIds array falls through filter — returns all files in the org.
        // Org2: Proj1 (3) + Proj2 (2) = 5 active files
        var count = await _metricsBusiness.GetFileCount(_org2Id, Array.Empty<long>(), hideArchived: true);

        Assert.Equal(5, count);
    }

    // ── Null / system-wide scoping ────────────────────────────────────────────

    [Fact]
    public async Task GetFileCount_NullOrgAndNullProjectIds_ReturnsSystemWideActiveFileCount()
    {
        // Org1 Proj1 (2) + Org2 Proj1 (3) + Org2 Proj2 (2) = 7 active files
        var count = await _metricsBusiness.GetFileCount((long?)null, (long[]?)null, hideArchived: true);

        Assert.Equal(7, count);
    }

    [Fact]
    public async Task GetFileCount_NullOrgAndNullProjectIds_IncludesArchived_WhenHideArchivedFalse()
    {
        // Active (7) + archived files (Org1 Proj1: 1, Org2 Proj1: 1) = 9
        var countActive = await _metricsBusiness.GetFileCount((long?)null, (long[]?)null, hideArchived: true);
        var countAll    = await _metricsBusiness.GetFileCount((long?)null, (long[]?)null, hideArchived: false);

        Assert.Equal(7, countActive);
        Assert.Equal(9, countAll);
    }

    [Fact]
    public async Task GetFileCount_NullOrgAndNullProjectIds_ReturnsZero_WhenNoFilesExist()
    {
        // Strip all URIs system-wide
        var allRecords = Context.Records.ToList();
        foreach (var r in allRecords) r.Uri = null;
        await Context.SaveChangesAsync();

        var count = await _metricsBusiness.GetFileCount((long?)null, (long[]?)null, hideArchived: true);

        Assert.Equal(0, count);
    }

    // ── Cross-org isolation ───────────────────────────────────────────────────

    [Fact]
    public async Task GetFileCount_OrgScoped_DoesNotLeakAcrossOrganizations()
    {
        var org1Count = await _metricsBusiness.GetFileCount(_org1Id, (long[]?)null, hideArchived: true);
        var org2Count = await _metricsBusiness.GetFileCount(_org2Id, (long[]?)null, hideArchived: true);

        Assert.Equal(2, org1Count); // only Org1 Proj1's 2 active files
        Assert.Equal(5, org2Count); // Org2 Proj1 (3) + Org2 Proj2 (2)
        Assert.NotEqual(org1Count, org2Count);
    }

    // ── File count vs record count ────────────────────────────────────────────

    [Fact]
    public async Task GetFileCount_IsAlwaysLessThanOrEqualToRecordCount()
    {
        // File count can never exceed record count since it is a strict subset
        var fileCount   = await _metricsBusiness.GetFileCount((long?)null, (long[]?)null, hideArchived: true);
        var recordCount = await _metricsBusiness.GetRecordCount((long?)null, (long[]?)null, hideArchived: true);

        Assert.True(fileCount <= recordCount);
    }

    [Fact]
    public async Task GetFileCount_EqualsRecordCount_WhenAllRecordsHaveUri()
    {
        // Force every record to have a URI, then file count must equal record count
        var allRecords = Context.Records.Where(r => !r.IsArchived).ToList();
        foreach (var r in allRecords) r.Uri ??= "localhost:8090/backfilled.pdf";
        await Context.SaveChangesAsync();

        var fileCount   = await _metricsBusiness.GetFileCount((long?)null, (long[]?)null, hideArchived: true);
        var recordCount = await _metricsBusiness.GetRecordCount((long?)null, (long[]?)null, hideArchived: true);

        Assert.Equal(recordCount, fileCount);
    }

    [Fact]
    public async Task GetOrganizationDataModalityCount_ExcludesRecordsWithNullFileType()
    {
        // Arrange
        var ds = new DataSource { Name = "DS", OrganizationId = _org1Id, ProjectId = _org1Proj1Id, IsArchived = false };
        Context.DataSources.Add(ds);
        await Context.SaveChangesAsync();

        Context.Records.AddRange(
            new DlRecord { Name ="R1", OriginalId = "1", Properties = "{}", Description = "", OrganizationId = _org1Id, ProjectId = _org1Proj1Id, DataSourceId = ds.Id, LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified), FileType = "image/png" },
            new DlRecord { Name ="R2", OriginalId = "2", Properties = "{}", Description = "", OrganizationId = _org1Id, ProjectId = _org1Proj1Id, DataSourceId = ds.Id, LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified), FileType = null }
        );
        await Context.SaveChangesAsync();

        // Act
        var count = await _metricsBusiness.GetOrganizationDataModalityCount(_org1Id, null);

        // Assert - null FileType record excluded
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task GetOrganizationDataModalityCount_ReturnsZero_WhenAllFileTypesAreNull()
    {
        // Arrange
        var ds = new DataSource { Name = "DS", OrganizationId = _org1Id, ProjectId = _org1Proj1Id, IsArchived = false };
        Context.DataSources.Add(ds);
        await Context.SaveChangesAsync();

        Context.Records.Add(new DlRecord { Name ="R1", OriginalId = "1", Properties = "{}", Description = "", OrganizationId = _org1Id, ProjectId = _org1Proj1Id, DataSourceId = ds.Id, LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified), FileType = null });
        await Context.SaveChangesAsync();

        // Act
        var count = await _metricsBusiness.GetOrganizationDataModalityCount(_org1Id, null);

        // Assert
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task GetOrganizationDataModalityCount_ExcludesOtherOrganizations()
    {
        // Arrange
        var ds1 = new DataSource { Name = "DS Org1", OrganizationId = _org1Id, ProjectId = _org1Proj1Id, IsArchived = false };
        var ds2 = new DataSource { Name = "DS Org2", OrganizationId = _org2Id, ProjectId = _org1Proj2Id, IsArchived = false };
        Context.DataSources.AddRange(ds1, ds2);
        await Context.SaveChangesAsync();

        // Project for org2
        var proj2 = new Project { Name = "Org2 Project", OrganizationId = _org2Id, LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified), LastUpdatedBy = _userId };
        Context.Projects.Add(proj2);
        await Context.SaveChangesAsync();

        Context.Records.AddRange(
            new DlRecord { Name ="R1", OriginalId = "1", Properties = "{}", Description = "", OrganizationId = _org1Id, ProjectId = _org1Proj1Id, DataSourceId = ds1.Id, LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified), FileType = "image/png" },
            new DlRecord { Name ="R2", OriginalId = "2", Properties = "{}", Description = "", OrganizationId = _org2Id, ProjectId = proj2.Id, DataSourceId = ds2.Id, LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified), FileType = "text/csv" },
            new DlRecord { Name ="R3", OriginalId = "3", Properties = "{}", Description = "", OrganizationId = _org2Id, ProjectId = proj2.Id, DataSourceId = ds2.Id, LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified), FileType = "application/json" }
        );
        await Context.SaveChangesAsync();

        // Act
        var count = await _metricsBusiness.GetOrganizationDataModalityCount(_org1Id, null);

        // Assert - only org 1's file type (png)
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task GetOrganizationDataModalityCount_CountsDistinct_NotTotal()
    {
        // Arrange
        var ds = new DataSource { Name = "DS", OrganizationId = _org1Id, ProjectId = _org1Proj1Id, IsArchived = false };
        Context.DataSources.Add(ds);
        await Context.SaveChangesAsync();

        // 5 records but only 2 distinct file types
        Context.Records.AddRange(
            new DlRecord { Name ="R1", OriginalId = "1", Properties = "{}", Description = "", OrganizationId = _org1Id, ProjectId = _org1Proj1Id, DataSourceId = ds.Id, LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified), FileType = "image/png" },
            new DlRecord { Name ="R2", OriginalId = "2", Properties = "{}", Description = "", OrganizationId = _org1Id, ProjectId = _org1Proj1Id, DataSourceId = ds.Id, LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified), FileType = "image/png" },
            new DlRecord { Name ="R3", OriginalId = "3", Properties = "{}", Description = "", OrganizationId = _org1Id, ProjectId = _org1Proj1Id, DataSourceId = ds.Id, LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified), FileType = "image/png" },
            new DlRecord { Name ="R4", OriginalId = "4", Properties = "{}", Description = "", OrganizationId = _org1Id, ProjectId = _org1Proj1Id, DataSourceId = ds.Id, LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified), FileType = "text/csv" },
            new DlRecord { Name ="R5", OriginalId = "5", Properties = "{}", Description = "", OrganizationId = _org1Id, ProjectId = _org1Proj1Id, DataSourceId = ds.Id, LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified), FileType = "text/csv" }
        );
        await Context.SaveChangesAsync();

        // Act
        var count = await _metricsBusiness.GetOrganizationDataModalityCount(_org1Id, null);

        // Assert
        Assert.Equal(2, count);
    }

    #endregion
}