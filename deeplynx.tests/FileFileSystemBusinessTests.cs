using System.Text;
using System.Text.Json.Nodes;
using deeplynx.business;
using deeplynx.datalayer.Models;
using deeplynx.interfaces;
using deeplynx.models;
using Microsoft.AspNetCore.Http;
using Moq;

namespace deeplynx.tests;

[Collection("Test Suite Collection")]
public class FileFileSystemBusinessTests : IntegrationTestBase
{
    private readonly string _testDirectory = Path.Combine(Path.GetTempPath(), "FileBusinessTests");
    private Mock<IClassBusiness> _classBusiness = null!;
    private FileFilesystemBusiness _fileBusiness;
    private Mock<IObjectStorageBusiness> _objectStorageBusiness = null!;
    private Mock<IRecordBusiness> _recordBusiness = null!;
    private long organizationId;
    public long os1;
    public long os2;
    public long pid;

    public FileFileSystemBusinessTests(TestSuiteFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _recordBusiness = new Mock<IRecordBusiness>();
        _objectStorageBusiness = new Mock<IObjectStorageBusiness>();
        _classBusiness = new Mock<IClassBusiness>();
        _fileBusiness = new FileFilesystemBusiness(Context, _objectStorageBusiness.Object, _classBusiness.Object,
            _recordBusiness.Object);
    }

    [Fact]
    public async Task UploadFile_ShouldSaveFileAndReturnPath()
    {
        // Arrange
        var config = new ObjectStorageConfigDto { MountPath = _testDirectory };
        var fileMock = new Mock<IFormFile>();
        var content = "Test file content";
        var fileName = "test.txt";
        var ms = new MemoryStream(Encoding.UTF8.GetBytes(content));
        fileMock.Setup(f => f.FileName).Returns(fileName);
        fileMock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), default))
            .Returns((Stream stream, CancellationToken token) => ms.CopyToAsync(stream));

        var guid = Guid.NewGuid();

        // need the try finally for if the test fails, still want to do cleanup
        try
        {
            // Act
            var result = await _fileBusiness.UploadFile(organizationId, pid, 1, config, fileMock.Object, guid);

            // Assert
            Assert.Contains(guid.ToString(), result);
            Assert.True(File.Exists(result));
            Assert.True(Directory.Exists(_testDirectory));
        }
        finally
        {
            // delete the entire test directory
            if (Directory.Exists(_testDirectory)) Directory.Delete(_testDirectory, true);
            Assert.False(Directory.Exists(_testDirectory));
        }
    }


    [Fact]
    public async Task UpdateFile_ShouldReplaceExistingFile()
    {
        // Arrange
        Directory.CreateDirectory(_testDirectory);
        var config = new ObjectStorageConfigDto { MountPath = _testDirectory };
        var originalFilePath = Path.Combine(_testDirectory, "original.txt");
        await File.WriteAllTextAsync(originalFilePath, "Old content");

        var record = new RecordResponseDto
        {
            Uri = originalFilePath,
            OriginalId = Guid.NewGuid().ToString()
        };

        var newContent = "New content";
        var fileMock = new Mock<IFormFile>();
        var ms = new MemoryStream(Encoding.UTF8.GetBytes(newContent));
        fileMock.Setup(f => f.FileName).Returns("new.txt");
        fileMock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), default))
            .Returns((Stream stream, CancellationToken token) => ms.CopyToAsync(stream));
        
        var guid = Guid.NewGuid();

        try
        {
            // Act
            var updatedPath = await _fileBusiness.UpdateFile(record, config, fileMock.Object, guid);

            // Assert
            Assert.True(File.Exists(updatedPath));
            var updatedContent = await File.ReadAllTextAsync(updatedPath);
            Assert.Equal(newContent, updatedContent);
        }
        finally
        {
            // delete the entire test directory
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
    }


    [Fact]
    public async Task DownloadFile_ShouldReturnFileStreamResult()
    {
        // Arrange
        Directory.CreateDirectory(_testDirectory);
        var config = new ObjectStorageConfigDto { MountPath = _testDirectory };
        var filePath = Path.Combine(_testDirectory, "download.txt");
        var content = "Downloadable content";
        await File.WriteAllTextAsync(filePath, content);

        var record = new RecordResponseDto
        {
            Uri = filePath,
            Name = "download.txt"
        };

        try
        {
            // Act
            var result = await _fileBusiness.DownloadFile(record, config);

            // Assert
            Assert.NotNull(result);
            using var reader = new StreamReader(result.FileStream);
            var resultContent = await reader.ReadToEndAsync();
            Assert.Equal(content, resultContent);
        }
        finally
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public async Task DeleteFile_ShouldDeleteFileAndEmptyDirectoriesCreated()
    {
        // Arrange
        var config = new ObjectStorageConfigDto { MountPath = _testDirectory };
        var fileMock = new Mock<IFormFile>();
        var content = "Test file content";
        var fileName = "test.txt";
        var ms = new MemoryStream(Encoding.UTF8.GetBytes(content));
        fileMock.Setup(f => f.FileName).Returns(fileName);
        fileMock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), default))
            .Returns((Stream stream, CancellationToken token) => ms.CopyToAsync(stream));
 
        var guid = Guid.NewGuid();
 
        // need the try finally for if the test fails, still want to do cleanup
        try
        {
            var result = await _fileBusiness.UploadFile(organizationId, pid, 1, config, fileMock.Object, guid);
 
            Assert.Contains(guid.ToString(), result);
            Assert.True(File.Exists(result));
            Assert.True(Directory.Exists(_testDirectory));
            
            var record = new RecordResponseDto
            {
                Uri = result,
                Name = "test.txt",
                ObjectStorageId = os1,
                ProjectId = pid
            };
 
            // Act
            var delete = await _fileBusiness.DeleteFile(record, config);
 
            // Assert
            Assert.True(delete);
            Assert.False(File.Exists(result));
            Assert.False(Directory.Exists(result));
            Assert.True(Directory.Exists(_testDirectory));
            Assert.True(Directory.GetFileSystemEntries(_testDirectory).Length == 0);
        }
        finally
        {
            // delete the entire test directory
            if (Directory.Exists(_testDirectory)) Directory.Delete(_testDirectory, true);
            Assert.False(Directory.Exists(_testDirectory));
        }
    }
 
    #region GetStorageSize Tests
 
    [Fact]
    public async Task GetStorageSize_ReturnsZero_WhenMountPathIsNull()
    {
        // Arrange
        var config = new ObjectStorageConfigDto { MountPath = null };
        var prefix = "org_1/project_1/";
 
        // Act
        var result = await _fileBusiness.GetStorageSize(prefix, config);
 
        // Assert
        Assert.Equal(0, result);
    }
 
    [Fact]
    public async Task GetStorageSize_ReturnsZero_WhenDirectoryDoesNotExist()
    {
        // Arrange
        var config = new ObjectStorageConfigDto { MountPath = _testDirectory };
        var prefix = "org_999/project_999/";
 
        // Act
        var result = await _fileBusiness.GetStorageSize(prefix, config);
 
        // Assert
        Assert.Equal(0, result);
    }
 
    [Fact]
    public async Task GetStorageSize_ReturnsZero_WhenDirectoryIsEmpty()
    {
        // Arrange
        var config = new ObjectStorageConfigDto { MountPath = _testDirectory };
        var prefix = "org_1/project_1/";
        var fullPath = Path.Combine(_testDirectory, "org_1", "project_1");
        Directory.CreateDirectory(fullPath);
 
        try
        {
            // Act
            var result = await _fileBusiness.GetStorageSize(prefix, config);
 
            // Assert
            Assert.Equal(0, result);
        }
        finally
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
    }
 
    [Fact]
    public async Task GetStorageSize_ReturnsCorrectSize_ForSingleFile()
    {
        // Arrange
        var config = new ObjectStorageConfigDto { MountPath = _testDirectory };
        var prefix = "org_1/project_1/";
        var fullPath = Path.Combine(_testDirectory, "org_1", "project_1");
        Directory.CreateDirectory(fullPath);
        
        var fileContent = new byte[1024]; // 1KB
        new Random().NextBytes(fileContent);
        var filePath = Path.Combine(fullPath, "test-file.txt");
        await File.WriteAllBytesAsync(filePath, fileContent);
 
        try
        {
            // Act
            var result = await _fileBusiness.GetStorageSize(prefix, config);
 
            // Assert
            Assert.Equal(1024, result);
        }
        finally
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
    }
 
    [Fact]
    public async Task GetStorageSize_ReturnsCorrectSize_ForMultipleFiles()
    {
        // Arrange
        var config = new ObjectStorageConfigDto { MountPath = _testDirectory };
        var prefix = "org_1/project_1/";
        var fullPath = Path.Combine(_testDirectory, "org_1", "project_1");
        Directory.CreateDirectory(fullPath);
        
        // Create 3 files of different sizes
        await File.WriteAllBytesAsync(Path.Combine(fullPath, "file1.txt"), new byte[500]);
        await File.WriteAllBytesAsync(Path.Combine(fullPath, "file2.txt"), new byte[1000]);
        await File.WriteAllBytesAsync(Path.Combine(fullPath, "file3.txt"), new byte[1500]);
 
        try
        {
            // Act
            var result = await _fileBusiness.GetStorageSize(prefix, config);
 
            // Assert
            Assert.Equal(3000, result); // 500 + 1000 + 1500
        }
        finally
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
    }
 
    [Fact]
    public async Task GetStorageSize_ReturnsCorrectSize_ForNestedDirectories()
    {
        // Arrange
        var config = new ObjectStorageConfigDto { MountPath = _testDirectory };
        var prefix = "org_1/project_1/";
        var basePath = Path.Combine(_testDirectory, "org_1", "project_1");
        
        // Create nested directory structure
        var datasource1Path = Path.Combine(basePath, "datasource_1");
        var datasource2Path = Path.Combine(basePath, "datasource_2");
        var subfolderPath = Path.Combine(datasource1Path, "subfolder");
        
        Directory.CreateDirectory(datasource1Path);
        Directory.CreateDirectory(datasource2Path);
        Directory.CreateDirectory(subfolderPath);
        
        // Create files at different levels
        await File.WriteAllBytesAsync(Path.Combine(basePath, "root-file.txt"), new byte[100]);
        await File.WriteAllBytesAsync(Path.Combine(datasource1Path, "ds1-file.txt"), new byte[200]);
        await File.WriteAllBytesAsync(Path.Combine(datasource2Path, "ds2-file.txt"), new byte[300]);
        await File.WriteAllBytesAsync(Path.Combine(subfolderPath, "nested-file.txt"), new byte[400]);
 
        try
        {
            // Act
            var result = await _fileBusiness.GetStorageSize(prefix, config);
 
            // Assert
            Assert.Equal(1000, result); // 100 + 200 + 300 + 400
        }
        finally
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
    }
 
    [Fact]
    public async Task GetStorageSize_ReturnsCorrectSize_WithEmptyPrefix()
    {
        // Arrange
        var config = new ObjectStorageConfigDto { MountPath = _testDirectory };
        var prefix = "";
        
        // Create files in multiple organizations and projects
        var org1Proj1Path = Path.Combine(_testDirectory, "org_1", "project_1");
        var org1Proj2Path = Path.Combine(_testDirectory, "org_1", "project_2");
        var org2Proj1Path = Path.Combine(_testDirectory, "org_2", "project_1");
        
        Directory.CreateDirectory(org1Proj1Path);
        Directory.CreateDirectory(org1Proj2Path);
        Directory.CreateDirectory(org2Proj1Path);
        
        await File.WriteAllBytesAsync(Path.Combine(org1Proj1Path, "file1.txt"), new byte[1000]);
        await File.WriteAllBytesAsync(Path.Combine(org1Proj2Path, "file2.txt"), new byte[2000]);
        await File.WriteAllBytesAsync(Path.Combine(org2Proj1Path, "file3.txt"), new byte[3000]);
 
        try
        {
            // Act - Empty prefix should count everything
            var result = await _fileBusiness.GetStorageSize(prefix, config);
 
            // Assert
            Assert.Equal(6000, result); // All files counted
        }
        finally
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
    }
 
    [Fact]
    public async Task GetStorageSize_OnlyCountsFilesInSpecificPrefix()
    {
        // Arrange
        var config = new ObjectStorageConfigDto { MountPath = _testDirectory };
        
        // Create files in different projects
        var proj1Path = Path.Combine(_testDirectory, "org_1", "project_1");
        var proj2Path = Path.Combine(_testDirectory, "org_1", "project_2");
        
        Directory.CreateDirectory(proj1Path);
        Directory.CreateDirectory(proj2Path);
        
        await File.WriteAllBytesAsync(Path.Combine(proj1Path, "file1.txt"), new byte[1000]);
        await File.WriteAllBytesAsync(Path.Combine(proj2Path, "file2.txt"), new byte[2000]);
 
        try
        {
            // Act - Only count project 1
            var result = await _fileBusiness.GetStorageSize("org_1/project_1/", config);
 
            // Assert
            Assert.Equal(1000, result); // Only project 1 file
        }
        finally
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
    }
 
    [Fact]
    public async Task GetStorageSize_HandlesWindowsPathSeparators()
    {
        // Arrange
        var config = new ObjectStorageConfigDto { MountPath = _testDirectory };
        var prefix = "org_1/project_1/"; // Unix-style separators
        var fullPath = Path.Combine(_testDirectory, "org_1", "project_1");
        Directory.CreateDirectory(fullPath);
        
        await File.WriteAllBytesAsync(Path.Combine(fullPath, "test.txt"), new byte[500]);
 
        try
        {
            // Act - Method should convert / to platform-specific separator
            var result = await _fileBusiness.GetStorageSize(prefix, config);
 
            // Assert
            Assert.Equal(500, result);
        }
        finally
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
    }
 
    [Fact]
    public async Task GetStorageSize_ContinuesOnFileAccessError()
    {
        // Arrange
        var config = new ObjectStorageConfigDto { MountPath = _testDirectory };
        var prefix = "org_1/project_1/";
        var fullPath = Path.Combine(_testDirectory, "org_1", "project_1");
        Directory.CreateDirectory(fullPath);
        
        // Create accessible files
        await File.WriteAllBytesAsync(Path.Combine(fullPath, "file1.txt"), new byte[1000]);
        await File.WriteAllBytesAsync(Path.Combine(fullPath, "file2.txt"), new byte[2000]);
 
        try
        {
            // Act - Even if one file fails, should count others
            var result = await _fileBusiness.GetStorageSize(prefix, config);
 
            // Assert - Should count accessible files
            Assert.Equal(3000, result);
        }
        finally
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
    }
 
    [Fact]
    public async Task GetStorageSize_ReturnsCorrectSize_ForLargeFile()
    {
        // Arrange
        var config = new ObjectStorageConfigDto { MountPath = _testDirectory };
        var prefix = "org_1/project_1/";
        var fullPath = Path.Combine(_testDirectory, "org_1", "project_1");
        Directory.CreateDirectory(fullPath);
        
        // Create a 5MB file
        var largeFileSize = 5 * 1024 * 1024;
        await File.WriteAllBytesAsync(Path.Combine(fullPath, "large-file.bin"), new byte[largeFileSize]);
 
        try
        {
            // Act
            var result = await _fileBusiness.GetStorageSize(prefix, config);
 
            // Assert
            Assert.Equal(largeFileSize, result);
        }
        finally
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
    }
 
    #endregion
 
    #region BuildPrefix Tests
 
    [Fact]
    public void BuildPrefix_ReturnsCorrectFormat_WithProjectId()
    {
        // Arrange
        long orgId = 123;
        long? projectId = 456;
 
        // Act
        var result = _fileBusiness.BuildPrefix(orgId, projectId);
 
        // Assert
        Assert.Equal("org_123/project_456/", result);
    }
 
    [Fact]
    public void BuildPrefix_ReturnsCorrectFormat_WithoutProjectId()
    {
        // Arrange
        long orgId = 789;
        long? projectId = null;
 
        // Act
        var result = _fileBusiness.BuildPrefix(orgId, projectId);
 
        // Assert
        Assert.Equal("org_789/", result);
    }
 
    [Fact]
    public void BuildPrefix_UsesCorrectNamingConvention()
    {
        // Arrange & Act
        var withProject = _fileBusiness.BuildPrefix(1, 2);
        var withoutProject = _fileBusiness.BuildPrefix(1, null);
 
        // Assert
        // Filesystem uses "org_" prefix (not "organization_" like Azure)
        Assert.StartsWith("org_", withProject);
        Assert.StartsWith("org_", withoutProject);
        
        // Should use "project_" for project
        Assert.Contains("project_", withProject);
        Assert.DoesNotContain("project_", withoutProject);
    }
 
    [Fact]
    public void BuildPrefix_EndsWithSlash()
    {
        // Arrange & Act
        var withProject = _fileBusiness.BuildPrefix(1, 2);
        var withoutProject = _fileBusiness.BuildPrefix(1, null);
 
        // Assert
        Assert.EndsWith("/", withProject);
        Assert.EndsWith("/", withoutProject);
    }
 
    [Fact]
    public void BuildPrefix_MultipleCallsWithSameInput_ReturnsSameResult()
    {
        // Arrange
        long orgId = 100;
        long? projectId = 200;
 
        // Act
        var result1 = _fileBusiness.BuildPrefix(orgId, projectId);
        var result2 = _fileBusiness.BuildPrefix(orgId, projectId);
        var result3 = _fileBusiness.BuildPrefix(orgId, projectId);
 
        // Assert
        Assert.Equal(result1, result2);
        Assert.Equal(result2, result3);
    }
 
    [Fact]
    public async Task GetStorageSize_WithBuildPrefix_WorksCorrectly()
    {
        // Arrange
        var config = new ObjectStorageConfigDto { MountPath = _testDirectory };
        long orgId = 5;
        long projectId = 10;
        
        // Use BuildPrefix to get the prefix
        var prefix = _fileBusiness.BuildPrefix(orgId, projectId);
        
        // Create directory matching the prefix
        var fullPath = Path.Combine(_testDirectory, "org_5", "project_10");
        Directory.CreateDirectory(fullPath);
        
        await File.WriteAllBytesAsync(Path.Combine(fullPath, "file.txt"), new byte[2500]);
 
        try
        {
            // Act
            var result = await _fileBusiness.GetStorageSize(prefix, config);
 
            // Assert
            Assert.Equal(2500, result);
        }
        finally
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
    }
    
    #endregion

    protected override async Task SeedTestDataAsync()
    {
        await base.SeedTestDataAsync();

        var organization = new Organization { Name = "Test Organization" };
        Context.Organizations.Add(organization);
        await Context.SaveChangesAsync();
        organizationId = organization.Id;

        var project = new Project { Name = "Test Project 1", OrganizationId = organizationId };
        Context.Projects.Add(project);
        await Context.SaveChangesAsync();
        pid = project.Id;

        var os1Config = new JsonObject();
        os1Config["mountPath"] = _testDirectory;
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
        os2Config["mountPath"] = _testDirectory;
        var objectStorage2 = new ObjectStorage
        {
            Name = "Test Object Storage 2",
            Type = "filesystem",
            ProjectId = pid,
            OrganizationId = organizationId,
            Config = os2Config.ToString()
        };

        Context.ObjectStorages.Add(objectStorage);
        Context.ObjectStorages.Add(objectStorage2);
        await Context.SaveChangesAsync();
        os1 = objectStorage.Id;
        os2 = objectStorage2.Id;
    }
}