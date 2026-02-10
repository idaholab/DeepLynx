using System.Text;
using System.Text.Json.Nodes;
using deeplynx.business;
using deeplynx.datalayer.Models;
using deeplynx.helpers.BigData;
using deeplynx.helpers.Hubs;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;

namespace deeplynx.tests;

[Collection("Test Suite Collection")]
public class FileBusinessAuthTests : IntegrationTestBase
{
    private readonly string _testDirectory = Path.Combine(Path.GetTempPath(), "FileBusinessAuthTests");
    private ClassBusiness _classBusiness = null!;
    private DataSourceBusiness _dataSourceBusiness = null!;
    private Mock<IEdgeBusiness> _edgeBusiness = null!;
    private EventBusiness _eventBusiness = null!;
    private FileBusiness _fileBusiness = null!;
    private Mock<IFileBusinessFactory> _fileBusinessFactory = null!;
    private Mock<IHubContext<EventNotificationHub>> _mockHubContext = null!;
    private Mock<ILogger<NotificationBusiness>> _mockNotificationLogger = null!;
    private INotificationBusiness _notificationBusiness = null!;
    private ObjectStorageBusiness _objectStorageBusiness = null!;
    private RecordBusiness _recordBusiness = null!;
    private Mock<IRelationshipBusiness> _relationshipBusiness = null!;
    private SensitivityLabelBusiness _sensitivityLabelBusiness = null!;
    private TagBusiness _tagBusiness = null!;
    private BulkCopyUpsertExecutor _mockBulkCopyExecutor = null!;
    
    private long organizationId;
    private long os1;
    private long os2;
    private long pid;
    private long uid;
    private long dataSourceId;
    private long defaultLabelId;
    private long defaultLabelId2;
    private long roleId;
    
    public FileBusinessAuthTests(TestSuiteFixture fixture) : base(fixture)
    {
    }
    
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        Directory.CreateDirectory(_testDirectory);
        
        // Set environment variable for chunk size
        Environment.SetEnvironmentVariable("RECOMMENDED_CHUNK_SIZE", "100000000"); // 100MB
        
        // Initialize mocks and real business classes
        _mockHubContext = new Mock<IHubContext<EventNotificationHub>>();
        _edgeBusiness = new Mock<IEdgeBusiness>();
        _relationshipBusiness = new Mock<IRelationshipBusiness>();
        _mockNotificationLogger = new Mock<ILogger<NotificationBusiness>>();
        _notificationBusiness = new NotificationBusiness(Context, _mockNotificationLogger.Object, _mockHubContext.Object);
        _mockBulkCopyExecutor = new BulkCopyUpsertExecutor();
        _eventBusiness = new EventBusiness(Context, _notificationBusiness, _mockBulkCopyExecutor);
        _fileBusinessFactory = new Mock<IFileBusinessFactory>();
        
        _objectStorageBusiness = new ObjectStorageBusiness(Context);
        _tagBusiness = new TagBusiness(Context, _eventBusiness);
        _sensitivityLabelBusiness = new SensitivityLabelBusiness(Context, _eventBusiness);
        _recordBusiness = new RecordBusiness(Context, _eventBusiness, _mockBulkCopyExecutor, _tagBusiness, _sensitivityLabelBusiness);
        _classBusiness = new ClassBusiness(Context, _recordBusiness, _relationshipBusiness.Object, _eventBusiness);
        _dataSourceBusiness = new DataSourceBusiness(Context, _edgeBusiness.Object, _recordBusiness, _eventBusiness);
        
        // Setup real filesystem business
        var realFileFilesystemBusiness = new FileFilesystemBusiness(Context, _objectStorageBusiness, _classBusiness, _recordBusiness);
        
        _fileBusinessFactory
            .Setup(x => x.CreateFileBusiness("filesystem"))
            .Returns(realFileFilesystemBusiness);
        
        // Create the FileBusiness with all dependencies
        _fileBusiness = new FileBusiness(
            Context,
            _fileBusinessFactory.Object,
            _objectStorageBusiness,
            _dataSourceBusiness,
            _classBusiness,
            _recordBusiness);
    }
    
    #region Helper Methods
    
    private IFormFile CreateFormFile(string content, string fileName = "test.txt")
    {
        var ms = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var formFile = new FormFile(ms, 0, ms.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/octet-stream"
        };
        return formFile;
    }
    
    #endregion
    
    #region UploadFile Tests
    
    [Fact]
    public async Task UploadFile_WithValidSensitivityLabel_ShouldSucceed()
    {
        // Arrange
        var content = "Test file content";
        var fileName = "test.txt";
        var formFile = CreateFormFile(content, fileName);

        try
        {
            // Act
            var result = await _fileBusiness.UploadFile(
                uid, 
                organizationId, 
                pid, 
                dataSourceId, 
                os1, 
                formFile,
                new List<long> { defaultLabelId });

            // Assert
            Assert.NotNull(result);
            Assert.Equal(fileName, result.Name);
            Assert.NotNull(result.Uri);
            Assert.True(File.Exists(result.Uri));
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
    }
    
    [Fact]
    public async Task UploadFile_WithMultipleSensitivityLabels_ShouldSucceed()
    {
        // Arrange
        var content = "Test file with multiple labels";
        var fileName = "multi-label.txt";
        var formFile = CreateFormFile(content, fileName);

        try
        {
            // Act
            var result = await _fileBusiness.UploadFile(
                uid, 
                organizationId, 
                pid, 
                dataSourceId, 
                os1, 
                formFile,
                new List<long> { defaultLabelId }); // Multiple authorized labels

            // Assert
            Assert.NotNull(result);
            Assert.Equal(fileName, result.Name);
            Assert.NotNull(result.Uri);
            Assert.True(File.Exists(result.Uri));
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
    }
    
    [Fact]
    public async Task UploadFile_WithUnauthorizedSensitivityLabel_ShouldThrow()
    {
        // Arrange
        var formFile = CreateFormFile("test content", "test.txt");

        try
        {
            // Act & Assert
            // Try to upload with a label the user doesn't have permission for
            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
                await _fileBusiness.UploadFile(
                    uid, 
                    organizationId, 
                    pid, 
                    dataSourceId, 
                    os1, 
                    formFile,
                    new List<long> { defaultLabelId2 })); // User has no permission for this label
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
    }
    
    [Fact]
    public async Task UploadFile_SensitivityLabelsRequired_NoLabel_ShouldFail()
    {
        // Arrange
        var content = "Test file content";
        var fileName = "test.txt";
        var formFile = CreateFormFile(content, fileName);

        var project = await Context.Projects.FirstOrDefaultAsync(p => p.Id == pid);
        project.RequireSensitivityLabel =  true;
        await Context.SaveChangesAsync();

        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await _fileBusiness.UploadFile(
                uid, 
                organizationId, 
                pid, 
                dataSourceId, 
                os1, 
                formFile));
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
    }
    
    #endregion
    
    #region UpdateFile Tests
    
    [Fact]
    public async Task UpdateFile_WithoutPermission_ShouldThrow()
    {
        // Arrange - First create a file with defaultLabelId
        var initialFile = CreateFormFile("initial content", "initial.txt");
        
        try
        {
            var createdRecord = await _fileBusiness.UploadFile(
                uid, organizationId, pid, dataSourceId, os1, 
                initialFile, new List<long> { defaultLabelId });

            Context.ChangeTracker.Clear();

            // Attach a label the user doesn't have update permission for
            await _recordBusiness.AttachLabel(uid, organizationId, pid, createdRecord.Id, defaultLabelId2);

            Context.ChangeTracker.Clear();

            var updateFile = CreateFormFile("updated content", "updated.txt");

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
                await _fileBusiness.UpdateFile(uid, organizationId, pid, createdRecord.Id, updateFile));
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
    }
    
    #endregion
    
    #region DownloadFile Tests
    
    [Fact]
    public async Task DownloadFile_WithoutPermission_ShouldThrow()
    {
        // Arrange - Create a file and attach unauthorized label
        var formFile = CreateFormFile("download test", "download.txt");
        
        try
        {
            var createdRecord = await _fileBusiness.UploadFile(
                uid, organizationId, pid, dataSourceId, os1, 
                formFile, new List<long> { defaultLabelId });

            Context.ChangeTracker.Clear();

            // Attach a label the user doesn't have download permission for
            await _recordBusiness.AttachLabel(uid, organizationId, pid, createdRecord.Id, defaultLabelId2);

            Context.ChangeTracker.Clear();

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
                await _fileBusiness.DownloadFile(uid, organizationId, pid, createdRecord.Id));
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
    }
    
    [Fact]
    public async Task DownloadFile_WithPermission_ShouldSucceed()
    {
        // Arrange
        var content = "downloadable content";
        var formFile = CreateFormFile(content, "authorized.txt");
        
        try
        {
            var createdRecord = await _fileBusiness.UploadFile(
                uid, organizationId, pid, dataSourceId, os1, 
                formFile, new List<long> { defaultLabelId });

            // Act
            var result = await _fileBusiness.DownloadFile(uid, organizationId, pid, createdRecord.Id);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("authorized.txt", result.FileDownloadName);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
    }
    
    #endregion
    
    #region DeleteFile Tests
    
    [Fact]
    public async Task DeleteFile_WithoutPermission_ShouldThrow()
    {
        // Arrange - Create file and attach unauthorized label
        var formFile = CreateFormFile("delete test", "delete.txt");
        
        try
        {
            var createdRecord = await _fileBusiness.UploadFile(
                uid, organizationId, pid, dataSourceId, os1, 
                formFile, new List<long> { defaultLabelId });

            Context.ChangeTracker.Clear();

            // Attach a label the user doesn't have delete permission for
            await _recordBusiness.AttachLabel(uid, organizationId, pid, createdRecord.Id, defaultLabelId2);

            Context.ChangeTracker.Clear();

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
                await _fileBusiness.DeleteFile(uid, organizationId, pid, createdRecord.Id));
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
    }
    
    [Fact]
    public async Task DeleteFile_WithPermission_ShouldSucceed()
    {
        // Arrange
        var formFile = CreateFormFile("content to delete", "deletable.txt");
        
        try
        {
            var createdRecord = await _fileBusiness.UploadFile(
                uid, organizationId, pid, dataSourceId, os1, 
                formFile, new List<long> { defaultLabelId });

            var filePath = createdRecord.Uri;
            Assert.True(File.Exists(filePath));

            // Act
            var result = await _fileBusiness.DeleteFile(uid, organizationId, pid, createdRecord.Id);

            // Assert
            Assert.True(result);
            Assert.False(File.Exists(filePath));
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
    }
    
    #endregion
    
    #region Chunked Upload Tests
    
    [Fact]
    public async Task StartUpload_WithAuthorizedLabel_ShouldSucceed()
    {
        // Arrange
        var request = new FileUploadInitRequestDto
        {
            FileName = "bigfile.bin",
            FileSize = 2L * 1024 * 1024 * 1024
        };

        try
        {
            // Act
            var session = await _fileBusiness.StartUpload(
                uid, organizationId, pid, dataSourceId, os1, request, 
                new List<long> { defaultLabelId });

            // Assert
            Assert.NotNull(session);
            Assert.NotNull(session.UploadId);
            
            var uploadPath = Path.Combine(
                _testDirectory,
                $"org_{organizationId}",
                $"project_{pid}",
                $"datasource_{dataSourceId}",
                "uploads",
                session.UploadId
            );
            Assert.True(Directory.Exists(uploadPath));
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
    }
    
    [Fact]
    public async Task StartUpload_WithUnauthorizedLabel_ShouldThrow()
    {
        // Arrange
        var request = new FileUploadInitRequestDto
        {
            FileName = "test.bin",
            FileSize = 2048
        };

        try
        {
            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            {
                await _fileBusiness.StartUpload(
                    uid, organizationId, pid, dataSourceId, os1, request, 
                    new List<long> { defaultLabelId2 }); // Unauthorized label
            });
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
    }
    
    [Fact]
    public async Task UploadChunk_WithUnauthorizedLabel_ShouldThrow()
    {
        // Arrange
        var request = new FileUploadInitRequestDto
        {
            FileName = "test.bin",
            FileSize = 2048
        };
        
        try
        {
            var session = await _fileBusiness.StartUpload(
                uid, organizationId, pid, dataSourceId, os1, request, 
                new List<long> { defaultLabelId });

            var chunk = CreateFormFile("chunk content");

            // Act & Assert - Try to upload chunk with unauthorized label
            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
                await _fileBusiness.UploadChunk(
                    uid, organizationId, pid, dataSourceId, os1, 
                    chunk, session.UploadId, 0, 
                    new List<long> { defaultLabelId2 })); // Different unauthorized label
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
    }
    
    [Fact]
    public async Task CompleteUpload_WithUnauthorizedLabel_ShouldThrow()
    {
        // Arrange
        var request = new FileUploadInitRequestDto
        {
            FileName = "test.bin",
            FileSize = 2048
        };
        
        try
        {
            var session = await _fileBusiness.StartUpload(
                uid, organizationId, pid, dataSourceId, os1, request, 
                new List<long> { defaultLabelId });

            var chunk0 = CreateFormFile("chunk0");
            var chunk1 = CreateFormFile("chunk1");
            
            await _fileBusiness.UploadChunk(
                uid, organizationId, pid, dataSourceId, os1, 
                chunk0, session.UploadId, 0, new List<long> { defaultLabelId });
                
            await _fileBusiness.UploadChunk(
                uid, organizationId, pid, dataSourceId, os1, 
                chunk1, session.UploadId, 1, new List<long> { defaultLabelId });

            var completeRequest = new FileUploadCompleteRequestDto
            {
                UploadId = session.UploadId,
                FileName = "test.bin",
                TotalChunks = 2
            };

            // Act & Assert - Try to complete with unauthorized label
            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
                await _fileBusiness.CompleteUpload(
                    uid, organizationId, pid, dataSourceId, os1, 
                    completeRequest, new List<long> { defaultLabelId2 })); // Unauthorized label
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
    }
    
    #endregion
    
    protected override async Task SeedTestDataAsync()
    {
        await base.SeedTestDataAsync();
        
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
        organizationId = organization.Id;

        var project = new Project { Name = "Test Project 1", OrganizationId = organizationId };
        Context.Projects.Add(project);
        await Context.SaveChangesAsync();
        pid = project.Id;
        
        // Create a data source
        var dataSource = new DataSource
        {
            Name = "Test DataSource",
            ProjectId = pid,
            OrganizationId = organizationId,
            Default = true,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        Context.DataSources.Add(dataSource);
        await Context.SaveChangesAsync();
        dataSourceId = dataSource.Id;
        
        var defaultLabel = new SensitivityLabel
        {
            Name = "Default Test Label",
            Description = "Default test sensitivity label",
            ProjectId = project.Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = organizationId,
            LastUpdatedBy = uid,
            IsArchived = false
        };
        var defaultLabel2 = new SensitivityLabel
        {
            Name = "Default Test Label 2",
            Description = "Second default test sensitivity label",
            ProjectId = project.Id,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = organizationId,
            LastUpdatedBy = uid,
            IsArchived = false
        };
        Context.SensitivityLabels.Add(defaultLabel);
        Context.SensitivityLabels.Add(defaultLabel2);
        await Context.SaveChangesAsync();
        defaultLabelId = defaultLabel.Id;
        defaultLabelId2 = defaultLabel2.Id;
        
        // Create permissions for defaultLabelId (user will have these)
        var downloadPermission = new Permission
        {
            Name = "download Default Label",
            Description = "download permission for default test label",
            Action = "download file",
            IsDefault = false,
            LabelId = defaultLabelId,
            ProjectId = pid,
            OrganizationId = organizationId,
            LastUpdatedBy = uid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            IsArchived = false
        };

        var uploadPermission = new Permission
        {
            Name = "Upload Default Label",
            Description = "Upload permission for default test label",
            Action = "upload file",
            IsDefault = false,
            LabelId = defaultLabelId,
            ProjectId = pid,
            OrganizationId = organizationId,
            LastUpdatedBy = uid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            IsArchived = false
        };
        
        var updatePermission = new Permission
        {
            Name = "Update Default Label",
            Description = "update permission for default test label",
            Action = "update file",
            IsDefault = false,
            LabelId = defaultLabelId,
            ProjectId = pid,
            OrganizationId = organizationId,
            LastUpdatedBy = uid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            IsArchived = false
        };
        
        var deletePermission = new Permission
        {
            Name = "Delete Default Label",
            Description = "delete permission for default test label",
            Action = "delete file",
            IsDefault = false,
            LabelId = defaultLabelId,
            ProjectId = pid,
            OrganizationId = organizationId,
            LastUpdatedBy = uid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            IsArchived = false
        };
        
        // Write permission is needed to attach labels
        var writePermission = new Permission
        {
            Name = "Write Default Label",
            Description = "write permission for default test label",
            Action = "write record",
            IsDefault = false,
            LabelId = defaultLabelId,
            ProjectId = pid,
            OrganizationId = organizationId,
            LastUpdatedBy = uid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            IsArchived = false
        };
        
        var writePermission2 = new Permission
        {
            Name = "Write Default Label 2",
            Description = "write permission for default test label 2",
            Action = "write record",
            IsDefault = false,
            LabelId = defaultLabelId2,
            ProjectId = pid,
            OrganizationId = organizationId,
            LastUpdatedBy = uid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            IsArchived = false
        };

        Context.Permissions.Add(downloadPermission);
        Context.Permissions.Add(uploadPermission);
        Context.Permissions.Add(updatePermission);
        Context.Permissions.Add(deletePermission);
        Context.Permissions.Add(writePermission);
        Context.Permissions.Add(writePermission2);
        await Context.SaveChangesAsync();

        var os1Config = new ObjectStorageConfigDto { MountPath = _testDirectory };
        var objectStorage = new ObjectStorage
        {
            Name = "Test Object Storage 1",
            ProjectId = pid,
            OrganizationId = organizationId,
            Type = "filesystem",
            Config = JsonConvert.SerializeObject(os1Config),
            Default = true
        };

        var os2Config = new ObjectStorageConfigDto { MountPath = _testDirectory };
        var objectStorage2 = new ObjectStorage
        {
            Name = "Test Object Storage 2",
            Type = "filesystem",
            ProjectId = pid,
            OrganizationId = organizationId,
            Config = JsonConvert.SerializeObject(os2Config)
        };

        Context.ObjectStorages.Add(objectStorage);
        Context.ObjectStorages.Add(objectStorage2);
        await Context.SaveChangesAsync();
        os1 = objectStorage.Id;
        os2 = objectStorage2.Id;
        
        var testRole = new Role
        {
            Name = "Test Role",
            Description = "Test role for unit tests",
            ProjectId = pid,
            OrganizationId = organizationId,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = uid
        };
        Context.Roles.Add(testRole);
        await Context.SaveChangesAsync();

        roleId = testRole.Id;

        var projectMember = new ProjectMember
        {
            ProjectId = pid,
            UserId = uid,
            RoleId = testRole.Id
        };
        Context.ProjectMembers.Add(projectMember);
        await Context.SaveChangesAsync();
        
        // Attach permissions for defaultLabelId only (not defaultLabelId2)
        var role = await Context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role != null)
        {
            role.Permissions.Add(downloadPermission);
            role.Permissions.Add(uploadPermission);
            role.Permissions.Add(updatePermission);
            role.Permissions.Add(deletePermission);
            role.Permissions.Add(writePermission);
            role.Permissions.Add(writePermission2); // Needed to attach the unauthorized label
            await Context.SaveChangesAsync();
        }
        
        // Create File class
        var testClass = new Class
        {
            Name = "File",
            ProjectId = pid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            OrganizationId = organizationId
        };
        Context.Classes.Add(testClass);
        await Context.SaveChangesAsync();
    }
    
    public override Task DisposeAsync()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, true);

        return base.DisposeAsync();
    }
}