using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using deeplynx.business;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
using deeplynx.helpers.BigData;
using deeplynx.helpers.Hubs;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests;

[Collection("Test Suite Collection")]
public class FileBusinessTests : IntegrationTestBase
{
    private readonly Mock<IFileBusiness> _innerFileBusiness = null!;
    private readonly string _orgDefaultDirectory = Path.Combine(Path.GetTempPath(), "OrgDefaultStorage");
    private readonly string _testDirectory = Path.Combine(Path.GetTempPath(), "FileBusinessChunkedTests");
    private ClassBusiness _classBusiness = null!;
    private DataSourceBusiness _dataSourceBusiness = null!;
    private Mock<IEdgeBusiness> _edgeBusiness = null!;
    private EventBusiness _eventBusiness = null!;
    private FileBusiness _fileBusiness = null!;
    private Mock<IFileBusinessFactory> _fileBusinessFactory = null!;
    private BulkCopyUpsertExecutor _mockBulkCopyExecutor = null!;
    private Mock<IHubContext<EventNotificationHub>> _mockHubContext = null!;
    private Mock<ILogger<NotificationBusiness>> _mockNotificationLogger = null!;
    private INotificationBusiness _notificationBusiness = null!;
    private ObjectStorageBusiness _objectStorageBusiness = null!;
    private RecordBusiness _recordBusiness = null!;
    private Mock<IRelationshipBusiness> _relationshipBusiness = null!;
    private SensitivityLabelBusiness _sensitivityLabelBusiness = null!;
    private ISensitivityLabelService _sensitivityLabelService = null!;
    private TagBusiness _tagBusiness = null!;

    public long did; // datasource ID
    public long oid; // organization ID
    public long osid; // object storage ID
    public long pid; // project ID
    public long uid; // user ID

    public FileBusinessTests(TestSuiteFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        Directory.CreateDirectory(_testDirectory);
        Directory.CreateDirectory(_orgDefaultDirectory);

        _mockHubContext = new Mock<IHubContext<EventNotificationHub>>();
        _edgeBusiness = new Mock<IEdgeBusiness>();
        _relationshipBusiness = new Mock<IRelationshipBusiness>();
        _mockNotificationLogger = new Mock<ILogger<NotificationBusiness>>();
        _notificationBusiness =
            new NotificationBusiness(Context, _mockNotificationLogger.Object, _mockHubContext.Object);
        _mockBulkCopyExecutor = new BulkCopyUpsertExecutor();
        _eventBusiness = new EventBusiness(Context, _notificationBusiness, _mockBulkCopyExecutor);


        _fileBusinessFactory = new Mock<IFileBusinessFactory>();

        _dataSourceBusiness =
            new DataSourceBusiness(Context, _edgeBusiness.Object, _recordBusiness, _eventBusiness);
        _objectStorageBusiness = new ObjectStorageBusiness(Context);

        _tagBusiness = new TagBusiness(Context, _eventBusiness);
        _sensitivityLabelBusiness = new SensitivityLabelBusiness(Context, _eventBusiness);
        _sensitivityLabelService = new SensitivityLabelService(Context);
        _recordBusiness = new RecordBusiness(Context, _eventBusiness, _mockBulkCopyExecutor, _tagBusiness,
            _sensitivityLabelBusiness, _sensitivityLabelService);
        _classBusiness = new ClassBusiness(Context, _recordBusiness, _relationshipBusiness.Object, _eventBusiness);

        var realFileFilesystemBusiness =
            new FileFilesystemBusiness(Context, _objectStorageBusiness, _classBusiness, _recordBusiness);

        // Object storage should also determine this implicitly - but we can also add this failsafe for now
        _fileBusinessFactory
            .Setup(x => x.CreateFileBusiness("filesystem"))
            .Returns(realFileFilesystemBusiness);

        _fileBusiness = new FileBusiness(
            Context,
            _fileBusinessFactory.Object,
            _dataSourceBusiness,
            _classBusiness,
            _recordBusiness
        );
    }

    protected override async Task SeedTestDataAsync()
    {
        await base.SeedTestDataAsync();

        var user = new User
        {
            Name = "Test User",
            Email = "test_chunked@example.com",
            Password = "test_password",
            IsArchived = false
        };
        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        uid = user.Id;

        var organization = new Organization { Name = "Test Organization" };
        Context.Organizations.Add(organization);
        await Context.SaveChangesAsync();
        oid = organization.Id;

        var project = new Project { Name = "Test Project", OrganizationId = oid };
        Context.Projects.Add(project);
        await Context.SaveChangesAsync();
        pid = project.Id;

        var dataSource = new DataSource
        {
            Name = "Test Data Source",
            Description = "Test data source for chunked upload tests",
            ProjectId = pid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid,
            OrganizationId = oid,
            Default = true
        };
        Context.DataSources.Add(dataSource);
        await Context.SaveChangesAsync();
        did = dataSource.Id;

        var osConfig = new JsonObject
        {
            ["mountPath"] = _testDirectory
        };

        var objectStorage = new ObjectStorage
        {
            Name = "Test Object Storage",
            ProjectId = pid,
            OrganizationId = oid,
            Type = "filesystem",
            Config = osConfig.ToString(),
            Default = true
        };

        Context.ObjectStorages.Add(objectStorage);
        await Context.SaveChangesAsync();
        osid = objectStorage.Id;

        var testClass = new Class
        {
            Name = "File",
            ProjectId = pid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = oid
        };
        Context.Classes.Add(testClass);
        await Context.SaveChangesAsync();
    }


    public override Task DisposeAsync()
    {
        if (Directory.Exists(_testDirectory)) Directory.Delete(_testDirectory, true);
        if (Directory.Exists(_orgDefaultDirectory)) Directory.Delete(_orgDefaultDirectory, true);

        return base.DisposeAsync();
    }

    #region Helpers

    private IFormFile CreateFormFile(string content)
    {
        var ms = new MemoryStream(Encoding.UTF8.GetBytes(content));
        return new FormFile(ms, 0, ms.Length, "chunk", "chunk.part")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/octet-stream"
        };
    }

    private IFormFile CreateFormFileFromBytes(byte[] data)
    {
        var ms = new MemoryStream(data);
        return new FormFile(ms, 0, data.Length, "chunk", "chunk.part")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/octet-stream"
        };
    }

    #endregion

    #region UploadFile Tests

    [Fact]
    public async Task UploadFile_WithSpecificObjectStorageId_UsesSpecifiedStorage()
    {
        // Arrange
        var content = "Test file with specific storage";
        var ms = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var file = new FormFile(ms, 0, ms.Length, "file", "specific-storage.txt")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };

        // Act: Upload with explicit objectStorageId
        var result = await _fileBusiness.UploadFile(
            uid,
            oid,
            pid,
            did,
            osid, // Explicitly specify project storage
            file
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal("specific-storage.txt", result.Name);
        Assert.Equal(osid, result.ObjectStorageId); // Should use specified storage
        Assert.True(File.Exists(result.Uri));
        Assert.True(result.Uri.Contains(_testDirectory), "File should be in project directory");
    }

    [Fact]
    public async Task UploadFile_WithOrgLevelObjectStorageId_UsesOrgStorage()
    {
        // Arrange: Create org-level storage
        var orgOsConfig = new JsonObject
        {
            ["mountPath"] = _orgDefaultDirectory
        };

        var orgObjectStorage = new ObjectStorage
        {
            Name = "Org Level Storage",
            ProjectId = null, // Org-level storage
            OrganizationId = oid,
            Type = "filesystem",
            Config = orgOsConfig.ToString(),
            Default = true
        };

        Context.ObjectStorages.Add(orgObjectStorage);
        await Context.SaveChangesAsync();
        var orgOsId = orgObjectStorage.Id;

        var content = "Test file in org storage";
        var ms = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var file = new FormFile(ms, 0, ms.Length, "file", "org-storage-file.txt")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };

        // Act: Explicitly specify org-level storage
        var result = await _fileBusiness.UploadFile(
            uid,
            oid,
            pid,
            did,
            orgOsId, // Explicitly use org storage
            file
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal("org-storage-file.txt", result.Name);
        Assert.Equal(orgOsId, result.ObjectStorageId);
        Assert.True(File.Exists(result.Uri));
        Assert.True(result.Uri.Contains(_orgDefaultDirectory),
            $"File should be in org directory. Actual: {result.Uri}");
        Assert.False(result.Uri.Contains(_testDirectory),
            $"File should NOT be in project directory. Actual: {result.Uri}");
    }

    [Fact]
    public async Task UploadFile_WithProjectStorageId_IgnoresOrgDefault()
    {
        // Arrange: Create org default
        var orgOsConfig = new JsonObject
        {
            ["mountPath"] = _orgDefaultDirectory
        };

        var orgObjectStorage = new ObjectStorage
        {
            Name = "Org Default Storage",
            ProjectId = null,
            OrganizationId = oid,
            Type = "filesystem",
            Config = orgOsConfig.ToString(),
            Default = true
        };

        Context.ObjectStorages.Add(orgObjectStorage);
        await Context.SaveChangesAsync();

        var content = "Explicit project storage";
        var ms = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var file = new FormFile(ms, 0, ms.Length, "file", "explicit-project.txt")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };

        // Act: Explicitly specify project storage even though org default exists
        var result = await _fileBusiness.UploadFile(
            uid,
            oid,
            pid,
            did,
            osid, // Explicitly use project storage
            file
        );

        // Assert: Should use project storage, not org default
        Assert.NotNull(result);
        Assert.Equal(osid, result.ObjectStorageId);
        Assert.True(result.Uri.Contains(_testDirectory),
            "Should use specified project storage, not org default");
        Assert.False(result.Uri.Contains(_orgDefaultDirectory));
    }

    [Fact]
    public async Task UploadFile_WithNonDefaultStorageId_UsesSpecifiedStorage()
    {
        // Arrange: Create a second project-level storage (not default)
        var secondaryOsConfig = new JsonObject
        {
            ["mountPath"] = Path.Combine(Path.GetTempPath(), "SecondaryStorage")
        };

        var secondaryObjectStorage = new ObjectStorage
        {
            Name = "Secondary Project Storage",
            ProjectId = pid,
            OrganizationId = oid,
            Type = "filesystem",
            Config = secondaryOsConfig.ToString(),
            Default = false // Not the default
        };

        Context.ObjectStorages.Add(secondaryObjectStorage);
        await Context.SaveChangesAsync();
        var secondaryOsId = secondaryObjectStorage.Id;

        // Create the directory
        var secondaryDir = Path.Combine(Path.GetTempPath(), "SecondaryStorage");
        Directory.CreateDirectory(secondaryDir);

        try
        {
            var content = "Non-default storage file";
            var ms = new MemoryStream(Encoding.UTF8.GetBytes(content));
            var file = new FormFile(ms, 0, ms.Length, "file", "secondary-storage.txt")
            {
                Headers = new HeaderDictionary(),
                ContentType = "text/plain"
            };

            // Act: Explicitly use non-default storage
            var result = await _fileBusiness.UploadFile(
                uid,
                oid,
                pid,
                did,
                secondaryOsId, // Use non-default storage
                file
            );

            // Assert
            Assert.NotNull(result);
            Assert.Equal(secondaryOsId, result.ObjectStorageId);
            Assert.True(result.Uri.Contains("SecondaryStorage"),
                $"Should use secondary storage. Actual: {result.Uri}");
            Assert.False(result.Uri.Contains(_testDirectory));
            Assert.False(result.Uri.Contains(_orgDefaultDirectory));
        }
        finally
        {
            // Cleanup secondary directory
            if (Directory.Exists(secondaryDir)) Directory.Delete(secondaryDir, true);
        }
    }

    [Fact]
    public async Task UploadFile_WithInvalidObjectStorageId_ThrowsException()
    {
        // Arrange
        var content = "Test file";
        var ms = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var file = new FormFile(ms, 0, ms.Length, "file", "invalid-storage.txt")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };

        var invalidStorageId = 99999L; // Non-existent ID

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _fileBusiness.UploadFile(uid, oid, pid, did, invalidStorageId, file)
        );

        Assert.Contains("No object storage found", exception.Message);
    }

    [Fact]
    public async Task UploadFile_WithStorageFromDifferentOrg_ThrowsException()
    {
        // Arrange: Create storage for a different organization
        var otherOrg = new Organization { Name = "Other Organization" };
        Context.Organizations.Add(otherOrg);
        await Context.SaveChangesAsync();

        var otherOrgOsConfig = new JsonObject
        {
            ["mountPath"] = _testDirectory
        };

        var otherOrgStorage = new ObjectStorage
        {
            Name = "Other Org Storage",
            ProjectId = null,
            OrganizationId = otherOrg.Id, // Different org
            Type = "filesystem",
            Config = otherOrgOsConfig.ToString(),
            Default = true
        };

        Context.ObjectStorages.Add(otherOrgStorage);
        await Context.SaveChangesAsync();

        var content = "Test file";
        var ms = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var file = new FormFile(ms, 0, ms.Length, "file", "wrong-org.txt")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };

        // Act & Assert: Should not be able to use storage from different org
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _fileBusiness.UploadFile(uid, oid, pid, did, otherOrgStorage.Id, file)
        );

        Assert.Contains("No object storage found", exception.Message);
    }

    [Fact]
    public async Task UploadFile_MultipleFilesWithDifferentStorages_WorksCorrectly()
    {
        // Arrange: Create org storage
        var orgOsConfig = new JsonObject
        {
            ["mountPath"] = _orgDefaultDirectory
        };

        var orgObjectStorage = new ObjectStorage
        {
            Name = "Org Storage",
            ProjectId = null,
            OrganizationId = oid,
            Type = "filesystem",
            Config = orgOsConfig.ToString(),
            Default = false
        };

        Context.ObjectStorages.Add(orgObjectStorage);
        await Context.SaveChangesAsync();
        var orgOsId = orgObjectStorage.Id;

        // Upload file 1 to project storage
        var content1 = "File in project storage";
        var ms1 = new MemoryStream(Encoding.UTF8.GetBytes(content1));
        var file1 = new FormFile(ms1, 0, ms1.Length, "file", "file1.txt")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };

        var result1 = await _fileBusiness.UploadFile(uid, oid, pid, did, osid, file1);

        // Upload file 2 to org storage
        var content2 = "File in org storage";
        var ms2 = new MemoryStream(Encoding.UTF8.GetBytes(content2));
        var file2 = new FormFile(ms2, 0, ms2.Length, "file", "file2.txt")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };

        var result2 = await _fileBusiness.UploadFile(uid, oid, pid, did, orgOsId, file2);

        // Assert: Both files should be in their respective storages
        Assert.NotNull(result1);
        Assert.Equal(osid, result1.ObjectStorageId);
        Assert.True(result1.Uri.Contains(_testDirectory));
        Assert.True(File.Exists(result1.Uri));

        Assert.NotNull(result2);
        Assert.Equal(orgOsId, result2.ObjectStorageId);
        Assert.True(result2.Uri.Contains(_orgDefaultDirectory));
        Assert.True(File.Exists(result2.Uri));
    }

    [Fact]
    public async Task UploadFile_CustomMetadata_WorksCorrectly()
    {
        // Arrange: Create org storage
        var orgOsConfig = new JsonObject
        {
            ["mountPath"] = _orgDefaultDirectory
        };

        var orgObjectStorage = new ObjectStorage
        {
            Name = "Org Storage",
            ProjectId = null,
            OrganizationId = oid,
            Type = "filesystem",
            Config = orgOsConfig.ToString(),
            Default = false
        };

        Context.ObjectStorages.Add(orgObjectStorage);
        await Context.SaveChangesAsync();

        // Upload file to project storage
        var content1 = "File in project storage";
        var ms1 = new MemoryStream(Encoding.UTF8.GetBytes(content1));
        var file1 = new FormFile(ms1, 0, ms1.Length, "file", "file1.txt")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };

        var metadata = new CreateRecordFileUploadRequestDto
        {
            Name = "Metadata",
            Description = "Description",
            Properties = new JsonObject { ["Name"] = "Name" },
            OriginalId = "OriginalId"
        };

        var metadataJson = JsonSerializer.Serialize(metadata);
        var metadataBytes = Encoding.UTF8.GetBytes(metadataJson);
        var metadataStream = new MemoryStream(metadataBytes);
        var metadataFile = new FormFile(metadataStream, 0, metadataStream.Length, "metadataFile", "metadata.json")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/json"
        };

        var result1 = await _fileBusiness.UploadFile(uid, oid, pid, did, osid, file1, null, metadataFile);

        // Assert
        Assert.NotNull(result1);
        Assert.Equal(osid, result1.ObjectStorageId);
        Assert.True(result1.Uri.Contains(_testDirectory));
        Assert.True(File.Exists(result1.Uri));
        Assert.Equal(metadata.Name, result1.Name);
        Assert.Equal(metadata.Description, result1.Description);
        Assert.Equal(metadata.OriginalId, result1.OriginalId);
    }

    [Fact]
    public async Task UploadFile_MetadataFileMissingRequiredFields_ThrowsValidationException()
    {
        // Arrange
        var content = "File in project storage";
        var ms = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var file = new FormFile(ms, 0, ms.Length, "file", "file1.txt")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };

        // Metadata missing required fields: Description, Properties, OriginalId
        var incompleteMetadata = new
        {
            Name = "Only Name Provided"
        };

        var metadataJson = JsonSerializer.Serialize(incompleteMetadata);
        var metadataBytes = Encoding.UTF8.GetBytes(metadataJson);
        var metadataStream = new MemoryStream(metadataBytes);
        var metadataFile = new FormFile(metadataStream, 0, metadataStream.Length, "metadataFile", "metadata.json")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/json"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() =>
            _fileBusiness.UploadFile(uid, oid, pid, did, osid, file, null, metadataFile));
    }

    #endregion

    #region UpdateFile Tests

    [Fact]
    public async Task UpdateFile_UsesRecordObjectStorage()
    {
        // Arrange: First upload a file
        var content = "Original content";
        var ms = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var file = new FormFile(ms, 0, ms.Length, "file", "update-test.txt")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };

        var originalRecord = await _fileBusiness.UploadFile(uid, oid, pid, did, osid, file);

        // Now update it
        var newContent = "Updated content";
        var newMs = new MemoryStream(Encoding.UTF8.GetBytes(newContent));
        var newFile = new FormFile(newMs, 0, newMs.Length, "file", "updated.txt")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };

        // Act: UpdateFile should use the record's stored objectStorageId
        var updatedRecord = await _fileBusiness.UpdateFile(uid, oid, pid, originalRecord.Id, newFile);

        // Assert
        Assert.NotNull(updatedRecord);
        Assert.Equal("updated.txt", updatedRecord.Name);
        Assert.Equal(osid, updatedRecord.ObjectStorageId);
        Assert.True(File.Exists(updatedRecord.Uri));

        var savedContent = await File.ReadAllTextAsync(updatedRecord.Uri);
        Assert.Equal(newContent, savedContent);
    }

    [Fact]
    public async Task UpdateFile_WithProjectDefault_WorksCorrectly()
    {
        // Arrange: Upload using project default
        var content = "Original content";
        var ms = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var file = new FormFile(ms, 0, ms.Length, "file", "update-default.txt")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };

        var originalRecord = await _fileBusiness.UploadFile(uid, oid, pid, did, null, file);

        // Update the file
        var newContent = "Updated with default";
        var newMs = new MemoryStream(Encoding.UTF8.GetBytes(newContent));
        var newFile = new FormFile(newMs, 0, newMs.Length, "file", "updated-default.txt")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };

        // Act
        var updatedRecord = await _fileBusiness.UpdateFile(uid, oid, pid, originalRecord.Id, newFile);

        // Assert
        Assert.NotNull(updatedRecord);
        Assert.Equal("updated-default.txt", updatedRecord.Name);
        Assert.Equal(osid, updatedRecord.ObjectStorageId);
        Assert.True(updatedRecord.Uri.Contains(_testDirectory));
    }

    [Fact]
    public async Task UpdateFile_WithOrgDefault_WorksCorrectly()
    {
        // Arrange: Create org default, disable project default
        var orgOsConfig = new JsonObject
        {
            ["mountPath"] = _orgDefaultDirectory
        };

        var orgObjectStorage = new ObjectStorage
        {
            Name = "Org Default Storage",
            ProjectId = null,
            OrganizationId = oid,
            Type = "filesystem",
            Config = orgOsConfig.ToString(),
            Default = true
        };

        Context.ObjectStorages.Add(orgObjectStorage);
        await Context.SaveChangesAsync();
        var orgOsId = orgObjectStorage.Id;

        var projectStorage = Context.ObjectStorages.First(os => os.Id == osid);
        projectStorage.Default = false;
        await Context.SaveChangesAsync();

        // Upload using org default
        var content = "Original content";
        var ms = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var file = new FormFile(ms, 0, ms.Length, "file", "org-update.txt")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };

        var originalRecord = await _fileBusiness.UploadFile(uid, oid, pid, did, null, file);

        // Update the file
        var newContent = "Updated in org storage";
        var newMs = new MemoryStream(Encoding.UTF8.GetBytes(newContent));
        var newFile = new FormFile(newMs, 0, newMs.Length, "file", "org-updated.txt")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };

        // Act
        var updatedRecord = await _fileBusiness.UpdateFile(uid, oid, pid, originalRecord.Id, newFile);

        // Assert
        Assert.NotNull(updatedRecord);
        Assert.Equal("org-updated.txt", updatedRecord.Name);
        Assert.Equal(orgOsId, updatedRecord.ObjectStorageId);
        Assert.True(updatedRecord.Uri.Contains(_orgDefaultDirectory));
        Assert.False(updatedRecord.Uri.Contains(_testDirectory));
    }

    #endregion

    #region DownloadFile Tests

    [Fact]
    public async Task DownloadFile_WorksWithProjectDefault()
    {
        // Arrange: Upload a file using project default
        var content = "Download test content";
        var ms = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var file = new FormFile(ms, 0, ms.Length, "file", "download-test.txt")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };

        var record = await _fileBusiness.UploadFile(uid, oid, pid, did, null, file);

        // Act: Download the file
        var result = await _fileBusiness.DownloadFile(uid, oid, pid, record.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("download-test.txt", result.FileDownloadName);

        using var reader = new StreamReader(result.FileStream);
        var downloadedContent = await reader.ReadToEndAsync();
        Assert.Equal(content, downloadedContent);
    }

    [Fact]
    public async Task DownloadFile_WorksWithOrgDefault()
    {
        // Arrange: Create org default, disable project default
        var orgOsConfig = new JsonObject
        {
            ["mountPath"] = _orgDefaultDirectory
        };

        var orgObjectStorage = new ObjectStorage
        {
            Name = "Org Default Storage",
            ProjectId = null,
            OrganizationId = oid,
            Type = "filesystem",
            Config = orgOsConfig.ToString(),
            Default = true
        };

        Context.ObjectStorages.Add(orgObjectStorage);
        await Context.SaveChangesAsync();

        var projectStorage = Context.ObjectStorages.First(os => os.Id == osid);
        projectStorage.Default = false;
        await Context.SaveChangesAsync();

        // Upload file using org default
        var content = "Org download test";
        var ms = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var file = new FormFile(ms, 0, ms.Length, "file", "org-download.txt")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };

        var record = await _fileBusiness.UploadFile(uid, oid, pid, did, null, file);

        // Act
        var result = await _fileBusiness.DownloadFile(uid, oid, pid, record.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("org-download.txt", result.FileDownloadName);

        using var reader = new StreamReader(result.FileStream);
        var downloadedContent = await reader.ReadToEndAsync();
        Assert.Equal(content, downloadedContent);
    }

    [Fact]
    public async Task DownloadFile_WithSpecificObjectStorage_WorksCorrectly()
    {
        // Arrange: Upload with specific object storage
        var content = "Specific storage download";
        var ms = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var file = new FormFile(ms, 0, ms.Length, "file", "specific-download.txt")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };

        var record = await _fileBusiness.UploadFile(uid, oid, pid, did, osid, file);

        // Act
        var result = await _fileBusiness.DownloadFile(uid, oid, pid, record.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("specific-download.txt", result.FileDownloadName);

        using var reader = new StreamReader(result.FileStream);
        var downloadedContent = await reader.ReadToEndAsync();
        Assert.Equal(content, downloadedContent);
    }

    #endregion

    #region DeleteFile Tests

    [Fact]
    public async Task DeleteFile_WorksWithProjectDefault()
    {
        // Arrange: Upload file using project default
        var content = "Delete test";
        var ms = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var file = new FormFile(ms, 0, ms.Length, "file", "delete-test.txt")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };

        var record = await _fileBusiness.UploadFile(uid, oid, pid, did, null, file);
        var filePath = record.Uri;

        Assert.True(File.Exists(filePath));
        Assert.True(filePath.Contains(_testDirectory));

        // Act: Delete the file
        var result = await _fileBusiness.DeleteFile(uid, oid, pid, record.Id);

        // Assert
        Assert.True(result);
        Assert.False(File.Exists(filePath)); // File should be deleted
    }

    [Fact]
    public async Task DeleteFile_WorksWithOrgDefault()
    {
        // Arrange: Create org default, disable project default
        var orgOsConfig = new JsonObject
        {
            ["mountPath"] = _orgDefaultDirectory
        };

        var orgObjectStorage = new ObjectStorage
        {
            Name = "Org Default Storage",
            ProjectId = null,
            OrganizationId = oid,
            Type = "filesystem",
            Config = orgOsConfig.ToString(),
            Default = true
        };

        Context.ObjectStorages.Add(orgObjectStorage);
        await Context.SaveChangesAsync();

        var projectStorage = Context.ObjectStorages.First(os => os.Id == osid);
        projectStorage.Default = false;
        await Context.SaveChangesAsync();

        var content = "Delete test";
        var ms = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var file = new FormFile(ms, 0, ms.Length, "file", "delete-org-test.txt")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };

        var record = await _fileBusiness.UploadFile(uid, oid, pid, did, null, file);
        var filePath = record.Uri;

        // Verify file is in ORG directory
        Assert.True(filePath.Contains(_orgDefaultDirectory),
            $"File should be in org directory. Actual: {filePath}");
        Assert.True(File.Exists(filePath));

        // Act
        var result = await _fileBusiness.DeleteFile(uid, oid, pid, record.Id);

        // Assert
        Assert.True(result);
        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public async Task DeleteFile_WithSpecificObjectStorage_WorksCorrectly()
    {
        // Arrange: Upload with specific object storage
        var content = "Specific delete test";
        var ms = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var file = new FormFile(ms, 0, ms.Length, "file", "specific-delete.txt")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };

        var record = await _fileBusiness.UploadFile(uid, oid, pid, did, osid, file);
        var filePath = record.Uri;

        Assert.True(File.Exists(filePath));

        // Act
        var result = await _fileBusiness.DeleteFile(uid, oid, pid, record.Id);

        // Assert
        Assert.True(result);
        Assert.False(File.Exists(filePath));
    }

    #endregion

    #region CancelUpload Tests

    [Fact]
    public async Task CancelUpload_NoObjectStorageId_UsesProjectDefault()
    {
        // Arrange: Start upload without objectStorageId
        var session = await _fileBusiness.StartUpload(
            oid, pid, did, null,
            new FileUploadInitRequestDto { FileName = "cancel-test.txt", FileSize = 2048 }
        );

        await _fileBusiness.UploadChunk(oid, pid, did, null, CreateFormFile("chunk0"), session.UploadId, 0);

        var uploadPath = Path.Combine(
            _testDirectory,
            $"org_{oid}",
            $"project_{pid}",
            $"datasource_{did}",
            "uploads",
            session.UploadId
        );

        Assert.True(Directory.Exists(uploadPath));
        Assert.True(File.Exists(Path.Combine(uploadPath, "0.part")));

        // Act: Cancel the upload
        await _fileBusiness.CancelUpload(uid, oid, pid, did, null, session.UploadId);

        // Assert: Upload directory should be cleaned up
        Assert.False(Directory.Exists(uploadPath));
    }

    [Fact]
    public async Task CancelUpload_NoObjectStorageId_FallsBackToOrgDefault()
    {
        // Arrange: Org default with different directory
        var orgOsConfig = new JsonObject
        {
            ["mountPath"] = _orgDefaultDirectory
        };

        var orgObjectStorage = new ObjectStorage
        {
            Name = "Org Default Storage",
            ProjectId = null,
            OrganizationId = oid,
            Type = "filesystem",
            Config = orgOsConfig.ToString(),
            Default = true
        };

        Context.ObjectStorages.Add(orgObjectStorage);
        await Context.SaveChangesAsync();

        var projectStorage = Context.ObjectStorages.First(os => os.Id == osid);
        projectStorage.Default = false;
        await Context.SaveChangesAsync();

        var session = await _fileBusiness.StartUpload(
            oid, pid, did, null,
            new FileUploadInitRequestDto { FileName = "cancel-org.txt", FileSize = 2048 }
        );

        await _fileBusiness.UploadChunk(oid, pid, did, null, CreateFormFile("chunk"), session.UploadId, 0);

        var uploadPath = Path.Combine(
            _orgDefaultDirectory, // ← ORG directory
            $"org_{oid}",
            $"project_{pid}",
            $"datasource_{did}",
            "uploads",
            session.UploadId
        );

        Assert.True(Directory.Exists(uploadPath), "Upload should be in org directory");

        // Act
        await _fileBusiness.CancelUpload(uid, oid, pid, did, null, session.UploadId);

        // Assert
        Assert.False(Directory.Exists(uploadPath), "Upload directory should be cleaned up");
    }

    [Fact]
    public async Task CancelUpload_WithSpecificObjectStorage_CleansUpCorrectly()
    {
        // Arrange
        var session = await _fileBusiness.StartUpload(
            oid, pid, did, osid,
            new FileUploadInitRequestDto { FileName = "cancel-specific.txt", FileSize = 2048 }
        );

        await _fileBusiness.UploadChunk(oid, pid, did, osid, CreateFormFile("chunk0"), session.UploadId, 0);
        await _fileBusiness.UploadChunk(oid, pid, did, osid, CreateFormFile("chunk1"), session.UploadId, 1);

        var uploadPath = Path.Combine(
            _testDirectory,
            $"org_{oid}",
            $"project_{pid}",
            $"datasource_{did}",
            "uploads",
            session.UploadId
        );

        Assert.True(Directory.Exists(uploadPath));
        Assert.True(File.Exists(Path.Combine(uploadPath, "0.part")));
        Assert.True(File.Exists(Path.Combine(uploadPath, "1.part")));

        // Act
        await _fileBusiness.CancelUpload(uid, oid, pid, did, osid, session.UploadId);

        // Assert: All chunks and directory should be cleaned up
        Assert.False(Directory.Exists(uploadPath));
    }

    [Fact]
    public async Task CancelUpload_MultipleChunks_CleansUpAll()
    {
        // Arrange: Upload multiple chunks
        var session = await _fileBusiness.StartUpload(
            oid, pid, did, null,
            new FileUploadInitRequestDto { FileName = "cancel-multi.txt", FileSize = 4096 }
        );

        await _fileBusiness.UploadChunk(oid, pid, did, null, CreateFormFile("chunk0"), session.UploadId, 0);
        await _fileBusiness.UploadChunk(oid, pid, did, null, CreateFormFile("chunk1"), session.UploadId, 1);
        await _fileBusiness.UploadChunk(oid, pid, did, null, CreateFormFile("chunk2"), session.UploadId, 2);
        await _fileBusiness.UploadChunk(oid, pid, did, null, CreateFormFile("chunk3"), session.UploadId, 3);

        var uploadPath = Path.Combine(
            _testDirectory,
            $"org_{oid}",
            $"project_{pid}",
            $"datasource_{did}",
            "uploads",
            session.UploadId
        );

        // Verify all chunks exist
        Assert.True(File.Exists(Path.Combine(uploadPath, "0.part")));
        Assert.True(File.Exists(Path.Combine(uploadPath, "1.part")));
        Assert.True(File.Exists(Path.Combine(uploadPath, "2.part")));
        Assert.True(File.Exists(Path.Combine(uploadPath, "3.part")));

        // Act
        await _fileBusiness.CancelUpload(uid, oid, pid, did, null, session.UploadId);

        // Assert: Everything should be cleaned up
        Assert.False(Directory.Exists(uploadPath));
    }

    #endregion

    #region StartUpload Tests

    [Fact]
    public async Task StartUpload_CreatesUploadDirectory_AndReturnsSessionInfo()
    {
        // Arrange
        var request = new FileUploadInitRequestDto
        {
            FileName = "bigfile.bin",
            FileSize = 2L * 1024 * 1024 * 1024 // 2GB
        };

        // Act
        var session = await _fileBusiness.StartUpload(
            oid,
            pid,
            did,
            osid,
            request
        );

        // Assert
        Assert.NotNull(session);
        Assert.False(string.IsNullOrWhiteSpace(session.UploadId));
        Assert.Equal(100_000_000, session.ChunkSize);
        Assert.Equal(22, session.TotalChunks); // 2GB / 100MB = 22 chunks

        var uploadPath = Path.Combine(
            _testDirectory,
            $"org_{oid}",
            $"project_{pid}",
            $"datasource_{did}",
            "uploads",
            session.UploadId
        );

        Assert.True(Directory.Exists(uploadPath));
    }

    [Fact]
    public async Task FileExactly501MB_UsesChunking()
    {
        // Arrange: File just over threshold
        var initRequest = new FileUploadInitRequestDto
        {
            FileName = "501mb.bin",
            FileSize = 501L * 1024 * 1024 // 501MB
        };

        // Act: Start upload (should create chunked upload session)
        var session = await _fileBusiness.StartUpload(oid, pid, did, osid, initRequest);

        // Assert: Should use chunking
        Assert.NotNull(session);
        Assert.NotNull(session.UploadId);
        Assert.True(session.TotalChunks > 1); // More than 1 chunk

        // Verify upload directory was created (sign of chunking)
        var uploadPath = Path.Combine(
            _testDirectory,
            $"org_{oid}",
            $"project_{pid}",
            $"datasource_{did}",
            "uploads",
            session.UploadId
        );
        Assert.True(Directory.Exists(uploadPath));
    }

    #endregion

    #region UploadChunk Tests

    [Fact]
    public async Task UploadChunk_WritesChunkFile_WhenSessionExists()
    {
        // Arrange: create an upload session first
        var initRequest = new FileUploadInitRequestDto
        {
            FileName = "file.txt",
            FileSize = 1024
        };

        var session = await _fileBusiness.StartUpload(oid, pid, did, osid, initRequest);

        var content = "CHUNK-0-CONTENT";
        var formFile = CreateFormFile(content);

        var expectedChunkPath = Path.Combine(
            _testDirectory,
            $"org_{oid}",
            $"project_{pid}",
            $"datasource_{did}",
            "uploads",
            session.UploadId,
            "0.part"
        );

        // Act
        var result = await _fileBusiness.UploadChunk(
            oid,
            pid,
            did,
            osid,
            formFile,
            session.UploadId,
            0
        );

        // Assert
        Assert.Equal("success", result);
        Assert.True(File.Exists(expectedChunkPath));

        var saved = await File.ReadAllTextAsync(expectedChunkPath);
        Assert.Equal(content, saved);
    }

    [Fact]
    public async Task UploadChunk_OutOfOrder_StillMergesCorrectly()
    {
        // Arrange: create an upload session
        var fileName = "out-of-order.txt";
        var initRequest = new FileUploadInitRequestDto
        {
            FileName = fileName,
            FileSize = 3072 // 3 chunks worth
        };

        var session = await _fileBusiness.StartUpload(oid, pid, did, osid, initRequest);

        // Act: Upload chunks OUT OF ORDER (2, 0, 1)
        await _fileBusiness.UploadChunk(oid, pid, did, osid, CreateFormFile("CHUNK-2"), session.UploadId, 2);
        await _fileBusiness.UploadChunk(oid, pid, did, osid, CreateFormFile("CHUNK-0"), session.UploadId, 0);
        await _fileBusiness.UploadChunk(oid, pid, did, osid, CreateFormFile("CHUNK-1"), session.UploadId, 1);

        var completeRequest = new FileUploadCompleteRequestDto
        {
            UploadId = session.UploadId,
            FileName = fileName,
            TotalChunks = 3
        };

        var result = await _fileBusiness.CompleteUpload(uid, oid, pid, did, osid, completeRequest);

        // Assert: Chunks should be merged in CORRECT ORDER (0, 1, 2)
        Assert.NotNull(result);
        Assert.Equal(fileName, result.Name);

        // Verify file content is in correct order
        var finalFilePath = result.Uri;
        Assert.True(File.Exists(finalFilePath));

        var mergedContent = await File.ReadAllTextAsync(finalFilePath);
        Assert.Equal("CHUNK-0CHUNK-1CHUNK-2", mergedContent);
    }

    [Fact]
    public async Task UploadChunk_DuplicateChunk_LastWriteWins()
    {
        // Arrange
        var session = await _fileBusiness.StartUpload(
            oid, pid, did, osid,
            new FileUploadInitRequestDto { FileName = "duplicate.txt", FileSize = 2048 }
        );

        // Act: Upload chunk 0 twice with different content
        await _fileBusiness.UploadChunk(oid, pid, did, osid, CreateFormFile("FIRST"), session.UploadId, 0);
        await _fileBusiness.UploadChunk(oid, pid, did, osid, CreateFormFile("SECOND"), session.UploadId,
            0); // Overwrites
        await _fileBusiness.UploadChunk(oid, pid, did, osid, CreateFormFile("CHUNK-1"), session.UploadId, 1);

        var completeRequest = new FileUploadCompleteRequestDto
        {
            UploadId = session.UploadId,
            FileName = "duplicate.txt",
            TotalChunks = 2
        };

        var result = await _fileBusiness.CompleteUpload(uid, oid, pid, did, osid, completeRequest);
        
        

        // Assert: Should use SECOND upload (last write wins)
        var finalFilePath = result.Uri;
        var mergedContent = await File.ReadAllTextAsync(finalFilePath);
        Assert.Equal("SECONDCHUNK-1", mergedContent);
    }


    [Fact]
    public async Task UploadChunk_EmptyChunk_ThrowsException()
    {
        // Arrange
        var session = await _fileBusiness.StartUpload(
            oid, pid, did, osid,
            new FileUploadInitRequestDto { FileName = "empty-chunk.txt", FileSize = 2048 }
        );

        // Act & Assert: Empty chunk should throw ArgumentException
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _fileBusiness.UploadChunk(
                oid, pid, did, osid,
                CreateFormFile(""), // Empty chunk
                session.UploadId,
                0
            )
        );
    }

    [Fact]
    public async Task UploadChunk_NullChunk_ThrowsException()
    {
        // Arrange
        var session = await _fileBusiness.StartUpload(
            oid, pid, did, osid,
            new FileUploadInitRequestDto { FileName = "null-chunk.txt", FileSize = 2048 }
        );

        // Act & Assert: Null chunk should throw ArgumentException
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _fileBusiness.UploadChunk(
                oid, pid, did, osid,
                null!, // Null chunk
                session.UploadId,
                0
            )
        );
    }

    [Fact]
    public async Task UploadChunk_InvalidUploadId_ThrowsException()
    {
        // Arrange: Don't start an upload session

        // Act & Assert: Upload chunk with non-existent uploadId
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _fileBusiness.UploadChunk(
                oid, pid, did, osid,
                CreateFormFile("chunk"),
                "non-existent-upload-id",
                0
            )
        );
    }

    #endregion

    #region CompleteUpload Tests

    [Fact]
    public async Task CompleteUpload_MergesChunks_AndCreatesRecord()
    {
        // Arrange: create an upload session
        var fileName = "final.txt";
        var initRequest = new FileUploadInitRequestDto
        {
            FileName = fileName,
            FileSize = 2048
        };

        var session = await _fileBusiness.StartUpload(oid, pid, did, osid, initRequest);

        // Upload two chunks
        var chunk0 = CreateFormFile("first-");
        await _fileBusiness.UploadChunk(oid, pid, did, osid, chunk0, session.UploadId, 0);

        var chunk1 = CreateFormFile("second");
        await _fileBusiness.UploadChunk(oid, pid, did, osid, chunk1, session.UploadId, 1);

        var completeRequest = new FileUploadCompleteRequestDto
        {
            UploadId = session.UploadId,
            FileName = fileName,
            TotalChunks = 2
        };

        var uploadPath = Path.Combine(
            _testDirectory,
            $"project_{pid}",
            $"datasource_{did}",
            "uploads",
            session.UploadId
        );

        // Act
        var result = await _fileBusiness.CompleteUpload(uid, oid, pid, did, osid, completeRequest);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(fileName, result.Name);
        Assert.NotNull(result.Uri);
        Assert.Equal(osid, result.ObjectStorageId);

        // Upload directory should be cleaned up
        Assert.False(Directory.Exists(uploadPath));

        // Verify final file exists in object storage location
        var finalFilePath = result.Uri; // This is the path returned by FileFilesystemBusiness
        Assert.True(File.Exists(finalFilePath));

        // Verify merged content
        var mergedContent = await File.ReadAllTextAsync(finalFilePath);
        Assert.Equal("first-second", mergedContent);
    }

    [Fact]
    public async Task CompleteUpload_MissingChunk_ThrowsException()
    {
        // Arrange
        var session = await _fileBusiness.StartUpload(
            oid, pid, did, osid,
            new FileUploadInitRequestDto { FileName = "test.txt", FileSize = 2048 }
        );

        // Upload only chunk 0, skip chunk 1
        await _fileBusiness.UploadChunk(
            oid, pid, did, osid,
            CreateFormFile("chunk0"),
            session.UploadId,
            0
        );

        var completeRequest = new FileUploadCompleteRequestDto
        {
            UploadId = session.UploadId,
            FileName = "test.txt",
            TotalChunks = 2
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _fileBusiness.CompleteUpload(uid, oid, pid, did, osid, completeRequest)
        );

        // Verify cleanup happened
        var uploadPath = Path.Combine(
            _testDirectory,
            $"project_{pid}",
            $"datasource_{did}",
            "uploads",
            session.UploadId
        );
        Assert.False(Directory.Exists(uploadPath));
    }

    [Fact]
    public async Task CompleteUpload_VerifyMergedFileIntegrity()
    {
        // Arrange: Create known content chunks
        var session = await _fileBusiness.StartUpload(
            oid, pid, did, osid,
            new FileUploadInitRequestDto { FileName = "integrity-test.txt", FileSize = 3072 }
        );

        var chunk0Content = "AAAA";
        var chunk1Content = "BBBB";
        var chunk2Content = "CCCC";

        // Act: Upload chunks with known content
        await _fileBusiness.UploadChunk(oid, pid, did, osid, CreateFormFile(chunk0Content), session.UploadId, 0);
        await _fileBusiness.UploadChunk(oid, pid, did, osid, CreateFormFile(chunk1Content), session.UploadId, 1);
        await _fileBusiness.UploadChunk(oid, pid, did, osid, CreateFormFile(chunk2Content), session.UploadId, 2);

        var completeRequest = new FileUploadCompleteRequestDto
        {
            UploadId = session.UploadId,
            FileName = "integrity-test.txt",
            TotalChunks = 3
        };

        var result = await _fileBusiness.CompleteUpload(uid, oid, pid, did, osid, completeRequest);

        // Assert: Verify merged file has exact expected content
        var finalFilePath = result.Uri;
        Assert.True(File.Exists(finalFilePath));

        var mergedContent = await File.ReadAllTextAsync(finalFilePath);
        var expectedContent = chunk0Content + chunk1Content + chunk2Content;

        Assert.Equal(expectedContent, mergedContent);
        Assert.Equal(expectedContent.Length, mergedContent.Length);
    }

    [Fact]
    public async Task CompleteUpload_LargerChunks_MergesCorrectly()
    {
        // Arrange: Simulate larger chunks with binary data
        var session = await _fileBusiness.StartUpload(
            oid, pid, did, osid,
            new FileUploadInitRequestDto { FileName = "binary-test.bin", FileSize = 1024 * 1024 }
        );

        // Create chunks with binary data (different patterns)
        var chunk0Data = new byte[256];
        var chunk1Data = new byte[256];
        for (var i = 0; i < 256; i++)
        {
            chunk0Data[i] = (byte)i; // 0-255
            chunk1Data[i] = (byte)(255 - i); // 255-0
        }

        await _fileBusiness.UploadChunk(oid, pid, did, osid, CreateFormFileFromBytes(chunk0Data), session.UploadId,
            0);
        await _fileBusiness.UploadChunk(oid, pid, did, osid, CreateFormFileFromBytes(chunk1Data), session.UploadId,
            1);

        var completeRequest = new FileUploadCompleteRequestDto
        {
            UploadId = session.UploadId,
            FileName = "binary-test.bin",
            TotalChunks = 2
        };

        var result = await _fileBusiness.CompleteUpload(uid, oid, pid, did, osid, completeRequest);

        // Assert: Verify binary data integrity
        var finalFilePath = result.Uri;
        var mergedBytes = await File.ReadAllBytesAsync(finalFilePath);

        Assert.Equal(512, mergedBytes.Length); // 256 + 256

        // Verify first chunk
        for (var i = 0; i < 256; i++) Assert.Equal((byte)i, mergedBytes[i]);

        // Verify second chunk
        for (var i = 0; i < 256; i++) Assert.Equal((byte)(255 - i), mergedBytes[256 + i]);
    }

    [Fact]
    public async Task CompleteUpload_NoChunksUploaded_ThrowsException()
    {
        // Arrange: Start upload but don't upload any chunks
        var session = await _fileBusiness.StartUpload(
            oid, pid, did, osid,
            new FileUploadInitRequestDto { FileName = "no-chunks.txt", FileSize = 2048 }
        );

        var completeRequest = new FileUploadCompleteRequestDto
        {
            UploadId = session.UploadId,
            FileName = "no-chunks.txt",
            TotalChunks = 2
        };

        // Act & Assert: Should throw because chunks are missing
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _fileBusiness.CompleteUpload(uid, oid, pid, did, osid, completeRequest)
        );
    }


    [Fact]
    public async Task CompleteUpload_CleansUpUploadDirectory()
    {
        // Arrange
        var session = await _fileBusiness.StartUpload(
            oid, pid, did, osid,
            new FileUploadInitRequestDto { FileName = "cleanup-test.txt", FileSize = 2048 }
        );

        var uploadPath = Path.Combine(
            _testDirectory,
            $"org_{oid}",
            $"project_{pid}",
            $"datasource_{did}",
            "uploads",
            session.UploadId
        );

        await _fileBusiness.UploadChunk(oid, pid, did, osid, CreateFormFile("chunk0"), session.UploadId, 0);
        await _fileBusiness.UploadChunk(oid, pid, did, osid, CreateFormFile("chunk1"), session.UploadId, 1);

        // Verify upload directory exists before complete
        Assert.True(Directory.Exists(uploadPath));
        Assert.True(File.Exists(Path.Combine(uploadPath, "0.part")));
        Assert.True(File.Exists(Path.Combine(uploadPath, "1.part")));

        var completeRequest = new FileUploadCompleteRequestDto
        {
            UploadId = session.UploadId,
            FileName = "cleanup-test.txt",
            TotalChunks = 2
        };

        // Act
        await _fileBusiness.CompleteUpload(uid, oid, pid, did, osid, completeRequest);

        // Assert: Upload directory should be deleted
        Assert.False(Directory.Exists(uploadPath));
    }

    #endregion

    #region Default Object Storage Tests

    [Fact]
    public async Task StartUpload_NoObjectStorageId_UsesProjectDefault()
    {
        // Arrange
        var request = new FileUploadInitRequestDto
        {
            FileName = "test.txt",
            FileSize = 2048
        };

        // Act: Call without specifying objectStorageId (null)
        var session = await _fileBusiness.StartUpload(
            oid,
            pid,
            did,
            null, // objectStorageId is null
            request
        );

        // Assert: Should use the project default (osid)
        Assert.NotNull(session);
        Assert.NotNull(session.UploadId);

        // Verify the upload directory was created with correct project path
        var uploadPath = Path.Combine(
            _testDirectory,
            $"org_{oid}",
            $"project_{pid}",
            $"datasource_{did}",
            "uploads",
            session.UploadId
        );
        Assert.True(Directory.Exists(uploadPath));
    }

    [Fact]
    public async Task StartUpload_NoObjectStorageId_FallsBackToOrgDefault()
    {
        // Arrange: Create org-level default storage with different directory
        var orgOsConfig = new JsonObject
        {
            ["mountPath"] = _orgDefaultDirectory // Use org-specific directory
        };

        var orgObjectStorage = new ObjectStorage
        {
            Name = "Org Default Storage",
            ProjectId = null, // Organization-level (no project)
            OrganizationId = oid,
            Type = "filesystem",
            Config = orgOsConfig.ToString(),
            Default = true
        };

        Context.ObjectStorages.Add(orgObjectStorage);
        await Context.SaveChangesAsync();

        // Remove project-level default
        var projectStorage = Context.ObjectStorages.First(os => os.Id == osid);
        projectStorage.Default = false;
        await Context.SaveChangesAsync();

        var request = new FileUploadInitRequestDto
        {
            FileName = "org-default-test.txt",
            FileSize = 2048
        };

        // Act: Call without specifying objectStorageId
        var session = await _fileBusiness.StartUpload(
            oid,
            pid,
            did,
            null, // objectStorageId is null
            request
        );

        // Assert: Should use org default in org directory
        Assert.NotNull(session);
        Assert.NotNull(session.UploadId);

        var orgUploadPath = Path.Combine(
            _orgDefaultDirectory, // ← ORG directory
            $"org_{oid}",
            $"project_{pid}",
            $"datasource_{did}",
            "uploads",
            session.UploadId
        );
        Assert.True(Directory.Exists(orgUploadPath), "Upload should be in org default directory");

        // Verify it's NOT in project directory
        var projectUploadPath = Path.Combine(
            _testDirectory, // ← PROJECT directory
            $"org_{oid}",
            $"project_{pid}",
            $"datasource_{did}",
            "uploads",
            session.UploadId
        );
        Assert.False(Directory.Exists(projectUploadPath), "Upload should NOT be in project directory");
    }

    [Fact]
    public async Task StartUpload_NoObjectStorageId_PrioritizesProjectOverOrg()
    {
        // Arrange: Create both project and org defaults with different directories
        var orgOsConfig = new JsonObject
        {
            ["mountPath"] = _orgDefaultDirectory // Org-specific directory
        };

        var orgObjectStorage = new ObjectStorage
        {
            Name = "Org Default Storage",
            ProjectId = null, // Organization-level
            OrganizationId = oid,
            Type = "filesystem",
            Config = orgOsConfig.ToString(),
            Default = true
        };

        Context.ObjectStorages.Add(orgObjectStorage);
        await Context.SaveChangesAsync();

        // Project default already exists (osid) with _testDirectory

        var request = new FileUploadInitRequestDto
        {
            FileName = "priority-test.txt",
            FileSize = 2048
        };

        // Act
        var session = await _fileBusiness.StartUpload(
            oid,
            pid,
            did,
            null, // objectStorageId is null
            request
        );

        // Assert: Should use PROJECT default (_testDirectory), not org default
        var projectUploadPath = Path.Combine(
            _testDirectory, // ← PROJECT directory (should be used)
            $"org_{oid}",
            $"project_{pid}",
            $"datasource_{did}",
            "uploads",
            session.UploadId
        );
        Assert.True(Directory.Exists(projectUploadPath), "Should use PROJECT default storage");

        // Verify org path was NOT used
        var orgUploadPath = Path.Combine(
            _orgDefaultDirectory, // ← ORG directory (should NOT be used)
            $"org_{oid}",
            $"project_{pid}",
            $"datasource_{did}",
            "uploads",
            session.UploadId
        );
        Assert.False(Directory.Exists(orgUploadPath), "Should NOT use org default when project default exists");
    }

    [Fact]
    public async Task CompleteUpload_NoObjectStorageId_UsesProjectDefault()
    {
        // Arrange
        var session = await _fileBusiness.StartUpload(
            oid, pid, did, null, // No objectStorageId
            new FileUploadInitRequestDto { FileName = "complete-default.txt", FileSize = 2048 }
        );

        await _fileBusiness.UploadChunk(oid, pid, did, null, CreateFormFile("chunk0"), session.UploadId, 0);
        await _fileBusiness.UploadChunk(oid, pid, did, null, CreateFormFile("chunk1"), session.UploadId, 1);

        var completeRequest = new FileUploadCompleteRequestDto
        {
            UploadId = session.UploadId,
            FileName = "complete-default.txt",
            TotalChunks = 2
        };

        // Act
        var result = await _fileBusiness.CompleteUpload(uid, oid, pid, did, null, completeRequest);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(osid, result.ObjectStorageId); // Should use project default
        Assert.True(File.Exists(result.Uri));
        Assert.True(result.Uri.Contains(_testDirectory), "File should be in project directory");
    }

    [Fact]
    public async Task CompleteUpload_CustomMetadata_WorksCorrectly()
    {
        // Arrange
        var session = await _fileBusiness.StartUpload(
            oid, pid, did, null,
            new FileUploadInitRequestDto { FileName = "complete-default.txt", FileSize = 2048 }
        );

        await _fileBusiness.UploadChunk(oid, pid, did, null, CreateFormFile("chunk0"), session.UploadId, 0);
        await _fileBusiness.UploadChunk(oid, pid, did, null, CreateFormFile("chunk1"), session.UploadId, 1);

        var completeRequest = new FileUploadCompleteRequestDto
        {
            UploadId = session.UploadId,
            FileName = "complete-default.txt",
            TotalChunks = 2
        };

        var metadata = new CreateRecordFileUploadRequestDto
        {
            Name = "Metadata",
            Description = "Description",
            Properties = new JsonObject
            {
                ["Name"] = "Name"
            },
            OriginalId = "OriginalId"
        };

        // Act
        var result1 = await _fileBusiness.CompleteUpload(uid, oid, pid, did, null, completeRequest, null, metadata);

        // Assert: Both files should be in their respective storages
        Assert.NotNull(result1);
        Assert.True(File.Exists(result1.Uri));
        Assert.Equal(result1.Name, metadata.Name);
        Assert.Equal(result1.Description, metadata.Description);
        Assert.Equal(result1.OriginalId, metadata.OriginalId);
    }

    [Fact]
    public async Task CompleteUpload_NoObjectStorageId_UsesOrgDefault()
    {
        // Arrange: Create org default, disable project default
        var orgOsConfig = new JsonObject
        {
            ["mountPath"] = _orgDefaultDirectory
        };

        var orgObjectStorage = new ObjectStorage
        {
            Name = "Org Default Storage",
            ProjectId = null,
            OrganizationId = oid,
            Type = "filesystem",
            Config = orgOsConfig.ToString(),
            Default = true
        };

        Context.ObjectStorages.Add(orgObjectStorage);
        await Context.SaveChangesAsync();
        var orgOsId = orgObjectStorage.Id;

        var projectStorage = Context.ObjectStorages.First(os => os.Id == osid);
        projectStorage.Default = false;
        await Context.SaveChangesAsync();

        var session = await _fileBusiness.StartUpload(
            oid, pid, did, null,
            new FileUploadInitRequestDto { FileName = "complete-org.txt", FileSize = 2048 }
        );

        await _fileBusiness.UploadChunk(oid, pid, did, null, CreateFormFile("chunk0"), session.UploadId, 0);
        await _fileBusiness.UploadChunk(oid, pid, did, null, CreateFormFile("chunk1"), session.UploadId, 1);

        var completeRequest = new FileUploadCompleteRequestDto
        {
            UploadId = session.UploadId,
            FileName = "complete-org.txt",
            TotalChunks = 2
        };

        // Act
        var result = await _fileBusiness.CompleteUpload(uid, oid, pid, did, null, completeRequest);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(orgOsId, result.ObjectStorageId); // Should use org default
        Assert.True(File.Exists(result.Uri));
        Assert.True(result.Uri.Contains(_orgDefaultDirectory),
            $"File should be in org directory. Actual: {result.Uri}");
        Assert.False(result.Uri.Contains(_testDirectory),
            $"File should NOT be in project directory. Actual: {result.Uri}");
    }

    [Fact]
    public async Task UploadFile_NoObjectStorageId_UsesProjectDefault()
    {
        // Arrange
        var content = "Test file content";
        var ms = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var file = new FormFile(ms, 0, ms.Length, "file", "test.txt")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };

        // Act: Upload without specifying objectStorageId
        var result = await _fileBusiness.UploadFile(
            uid,
            oid,
            pid,
            did,
            null, // objectStorageId is null
            file
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test.txt", result.Name);
        Assert.Equal(osid, result.ObjectStorageId); // Should use project default
        Assert.True(File.Exists(result.Uri));
        Assert.True(result.Uri.Contains(_testDirectory), "File should be in project directory");
    }

    [Fact]
    public async Task UploadFile_NoObjectStorageId_FallsBackToOrgDefault()
    {
        // Arrange: Create org-level default with different directory
        var orgOsConfig = new JsonObject
        {
            ["mountPath"] = _orgDefaultDirectory
        };

        var orgObjectStorage = new ObjectStorage
        {
            Name = "Org Default Storage",
            ProjectId = null,
            OrganizationId = oid,
            Type = "filesystem",
            Config = orgOsConfig.ToString(),
            Default = true
        };

        Context.ObjectStorages.Add(orgObjectStorage);
        await Context.SaveChangesAsync();
        var orgOsId = orgObjectStorage.Id;

        // Disable project default
        var projectStorage = Context.ObjectStorages.First(os => os.Id == osid);
        projectStorage.Default = false;
        await Context.SaveChangesAsync();

        var content = "Test file content";
        var ms = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var file = new FormFile(ms, 0, ms.Length, "file", "org-default.txt")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };

        // Act
        var result = await _fileBusiness.UploadFile(
            uid,
            oid,
            pid,
            did,
            null, // objectStorageId is null
            file
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal(orgOsId, result.ObjectStorageId); // Should use org default
        Assert.True(File.Exists(result.Uri));
        Assert.True(result.Uri.Contains(_orgDefaultDirectory),
            $"File should be in org directory. Actual: {result.Uri}");
        Assert.False(result.Uri.Contains(_testDirectory),
            $"File should NOT be in project directory. Actual: {result.Uri}");
    }

    [Fact]
    public async Task StartUpload_NoDefaultFound_ThrowsException()
    {
        // Arrange: Remove all default flags
        var allStorages = Context.ObjectStorages.Where(os => os.OrganizationId == oid);
        foreach (var storage in allStorages) storage.Default = false;
        await Context.SaveChangesAsync();

        var request = new FileUploadInitRequestDto
        {
            FileName = "no-default.txt",
            FileSize = 2048
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _fileBusiness.StartUpload(oid, pid, did, null, request)
        );

        Assert.Contains("No default object storage found", exception.Message);
    }

    #endregion
    
    #region FileSize Tests

    [Fact]
    public async Task UploadFile_CapturesFileSize()
    {
        // Arrange
        var content = "Test file content with some length";
        var expectedSize = System.Text.Encoding.UTF8.GetBytes(content).Length;
        var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        var file = new FormFile(ms, 0, ms.Length, "file", "filesize-test.txt")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };

        // Act
        var result = await _fileBusiness.UploadFile(
            uid, oid, pid, did, osid, file
        );

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.FileSize);
        Assert.Equal(expectedSize, result.FileSize);
        
        // Verify file size persisted in database
        var dbRecord = await Context.Records.FindAsync(result.Id);
        Assert.NotNull(dbRecord);
        Assert.Equal(expectedSize, dbRecord.FileSize);
    }

    [Fact]
    public async Task UploadFile_LargeFile_CapturesCorrectFileSize()
    {
        // Arrange - Create a 1MB file
        var content = new string('A', 1024 * 1024); // 1MB of 'A' characters
        var expectedSize = System.Text.Encoding.UTF8.GetBytes(content).Length;
        var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        var file = new FormFile(ms, 0, ms.Length, "file", "large-file.txt")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };

        // Act
        var result = await _fileBusiness.UploadFile(
            uid, oid, pid, did, osid, file
        );

        // Assert
        Assert.NotNull(result.FileSize);
        Assert.Equal(expectedSize, result.FileSize);
        Assert.True(result.FileSize > 1000000); // Over 1MB
    }

    [Fact]
    public async Task UpdateFile_UpdatesFileSize()
    {
        // Arrange - Upload initial file
        var initialContent = "Initial content";
        var ms1 = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(initialContent));
        var file1 = new FormFile(ms1, 0, ms1.Length, "file", "update-size-test.txt")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };
        var initialRecord = await _fileBusiness.UploadFile(uid, oid, pid, did, osid, file1);
        var initialSize = initialRecord.FileSize;

        // Act - Update with larger file
        var newContent = "This is much longer content for the updated file";
        var expectedNewSize = System.Text.Encoding.UTF8.GetBytes(newContent).Length;
        var ms2 = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(newContent));
        var file2 = new FormFile(ms2, 0, ms2.Length, "file", "updated-size.txt")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };
        var updatedRecord = await _fileBusiness.UpdateFile(uid, oid, pid, initialRecord.Id, file2);

        // Assert
        Assert.NotNull(updatedRecord.FileSize);
        Assert.Equal(expectedNewSize, updatedRecord.FileSize);
        Assert.NotEqual(initialSize, updatedRecord.FileSize);
        Assert.True(updatedRecord.FileSize > initialSize);
        
        // Verify in database
        var dbRecord = await Context.Records.FindAsync(updatedRecord.Id);
        Assert.Equal(expectedNewSize, dbRecord.FileSize);
    }

    [Fact]
    public async Task GetRecord_ReturnsFileSize()
    {
        // Arrange
        var content = "Content for get test";
        var expectedSize = System.Text.Encoding.UTF8.GetBytes(content).Length;
        var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        var file = new FormFile(ms, 0, ms.Length, "file", "get-size-test.txt")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };
        var uploadedRecord = await _fileBusiness.UploadFile(uid, oid, pid, did, osid, file);

        // Act
        var retrievedRecord = await _recordBusiness.GetRecord(uid, oid, pid, uploadedRecord.Id, true);

        // Assert
        Assert.NotNull(retrievedRecord);
        Assert.NotNull(retrievedRecord.FileSize);
        Assert.Equal(expectedSize, retrievedRecord.FileSize);
    }

    [Fact]
    public async Task GetAllRecords_ReturnsFileSizes()
    {
        // Arrange - Upload multiple files with different sizes
        var file1Content = "Small file";
        var file1Size = System.Text.Encoding.UTF8.GetBytes(file1Content).Length;
        var ms1 = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(file1Content));
        var file1 = new FormFile(ms1, 0, ms1.Length, "file", "small.txt")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };
        
        var file2Content = new string('B', 5000); // Larger file
        var file2Size = System.Text.Encoding.UTF8.GetBytes(file2Content).Length;
        var ms2 = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(file2Content));
        var file2 = new FormFile(ms2, 0, ms2.Length, "file", "large.txt")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };

        await _fileBusiness.UploadFile(uid, oid, pid, did, osid, file1);
        await _fileBusiness.UploadFile(uid, oid, pid, did, osid, file2);

        // Act
        var allRecords = await _recordBusiness.GetAllRecords(uid, oid, pid, did, true);

        // Assert
        var uploadedRecords = allRecords.Where(r => r.Name == "small.txt" || r.Name == "large.txt").ToList();
        Assert.Equal(2, uploadedRecords.Count);
        Assert.All(uploadedRecords, r => Assert.NotNull(r.FileSize));
        
        var smallFile = uploadedRecords.First(r => r.Name == "small.txt");
        var largeFile = uploadedRecords.First(r => r.Name == "large.txt");
        
        Assert.Equal(file1Size, smallFile.FileSize);
        Assert.Equal(file2Size, largeFile.FileSize);
        Assert.True(largeFile.FileSize > smallFile.FileSize);
    }

    [Fact]
    public async Task CompleteUpload_ChunkedUpload_CapturesCorrectFileSize()
    {
        // Arrange
        var content = "Chunked upload content with multiple parts that will be merged";
        var expectedSize = System.Text.Encoding.UTF8.GetBytes(content).Length;
        var chunks = new[] { "Chunked upload ", "content with ", "multiple parts ", "that will be merged" };
        
        var session = await _fileBusiness.StartUpload(
            oid, pid, did, osid,
            new FileUploadInitRequestDto { FileName = "chunked-size.txt", FileSize = expectedSize }
        );

        // Act - Upload chunks
        for (int i = 0; i < chunks.Length; i++)
        {
            await _fileBusiness.UploadChunk(oid, pid, did, osid, CreateFormFile(chunks[i]), session.UploadId, i);
        }

        var completeRequest = new FileUploadCompleteRequestDto
        {
            UploadId = session.UploadId,
            FileName = "chunked-size.txt",
            TotalChunks = chunks.Length
        };

        var result = await _fileBusiness.CompleteUpload(uid, oid, pid, did, osid, completeRequest);

        // Assert
        Assert.NotNull(result.FileSize);
        Assert.Equal(expectedSize, result.FileSize);
        
        // Verify actual file size matches
        var filePath = result.Uri;
        var actualFileSize = new FileInfo(filePath).Length;
        Assert.Equal(expectedSize, actualFileSize);
    }

    [Fact]
    public async Task UploadFile_EmptyFile_CapturesZeroSize()
    {
        // Arrange
        var ms = new MemoryStream(Array.Empty<byte>());
        var file = new FormFile(ms, 0, 0, "file", "empty.txt")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };

        // Act & Assert - Should throw because empty files aren't allowed
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _fileBusiness.UploadFile(uid, oid, pid, did, osid, file)
        );
    }

    #endregion
}