using System.Text;
using System.Text.Json.Nodes;
using deeplynx.business;
using deeplynx.datalayer.Models;
using deeplynx.helpers;
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
    private EncryptionHelper _encryptionHelper = null!;
    private long organizationId;
    public long os1;
    public long os2;
    public long pid;

    public FileFileSystemBusinessTests(TestSuiteFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        _encryptionHelper = new EncryptionHelper();
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
            ConfigEncrypted = _encryptionHelper.SerializeAndEncrypt(os1Config),
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
            ConfigEncrypted = _encryptionHelper.SerializeAndEncrypt(os2Config),
        };

        Context.ObjectStorages.Add(objectStorage);
        Context.ObjectStorages.Add(objectStorage2);
        await Context.SaveChangesAsync();
        os1 = objectStorage.Id;
        os2 = objectStorage2.Id;
    }

    #region GetFileSize Tests

    [Fact]
    public async Task GetFileSize_ReturnsCorrectSize_ForExistingFile()
    {
        // Arrange
        var config = new ObjectStorageConfigDto { MountPath = _testDirectory };
        var testFilePath = Path.Combine(_testDirectory, "size-test.txt");
        Directory.CreateDirectory(_testDirectory);

        var fileContent = new byte[1024]; // 1KB
        new Random().NextBytes(fileContent);
        await File.WriteAllBytesAsync(testFilePath, fileContent);

        try
        {
            // Act
            var result = await _fileBusiness.GetFileSize(testFilePath, config);

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
    public async Task GetFileSize_ReturnsZero_ForEmptyFile()
    {
        // Arrange
        var config = new ObjectStorageConfigDto { MountPath = _testDirectory };
        var testFilePath = Path.Combine(_testDirectory, "empty.txt");
        Directory.CreateDirectory(_testDirectory);

        await File.WriteAllBytesAsync(testFilePath, Array.Empty<byte>());

        try
        {
            // Act
            var result = await _fileBusiness.GetFileSize(testFilePath, config);

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
    public async Task GetFileSize_ThrowsFileNotFoundException_WhenFileDoesNotExist()
    {
        // Arrange
        var config = new ObjectStorageConfigDto { MountPath = _testDirectory };
        var nonExistentPath = Path.Combine(_testDirectory, "non-existent.txt");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _fileBusiness.GetFileSize(nonExistentPath, config)
        );

        Assert.Contains(nonExistentPath, exception.Message);
    }

    [Fact]
    public async Task GetFileSize_ThrowsException_WhenMountPathIsNull()
    {
        // Arrange
        var config = new ObjectStorageConfigDto { MountPath = null };
        var testFilePath = Path.Combine(_testDirectory, "test.txt");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() =>
            _fileBusiness.GetFileSize(testFilePath, config)
        );

        Assert.Contains("File system mount path not set", exception.Message);
    }

    [Fact]
    public async Task GetFileSize_ReturnsCorrectSize_ForLargeFile()
    {
        // Arrange
        var config = new ObjectStorageConfigDto { MountPath = _testDirectory };
        var testFilePath = Path.Combine(_testDirectory, "large-file.bin");
        Directory.CreateDirectory(_testDirectory);

        // Create a 5MB file
        var largeFileSize = 5 * 1024 * 1024;
        await File.WriteAllBytesAsync(testFilePath, new byte[largeFileSize]);

        try
        {
            // Act
            var result = await _fileBusiness.GetFileSize(testFilePath, config);

            // Assert
            Assert.Equal(largeFileSize, result);
        }
        finally
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public async Task GetFileSize_ReturnsCorrectSize_ForMultipleDifferentFiles()
    {
        // Arrange
        var config = new ObjectStorageConfigDto { MountPath = _testDirectory };
        Directory.CreateDirectory(_testDirectory);

        var file1Path = Path.Combine(_testDirectory, "file1.txt");
        var file2Path = Path.Combine(_testDirectory, "file2.txt");
        var file3Path = Path.Combine(_testDirectory, "file3.txt");

        await File.WriteAllBytesAsync(file1Path, new byte[100]);
        await File.WriteAllBytesAsync(file2Path, new byte[500]);
        await File.WriteAllBytesAsync(file3Path, new byte[1000]);

        try
        {
            // Act
            var size1 = await _fileBusiness.GetFileSize(file1Path, config);
            var size2 = await _fileBusiness.GetFileSize(file2Path, config);
            var size3 = await _fileBusiness.GetFileSize(file3Path, config);

            // Assert
            Assert.Equal(100, size1);
            Assert.Equal(500, size2);
            Assert.Equal(1000, size3);
        }
        finally
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public async Task GetFileSize_ThrowsException_WhenFilePathIsInvalid()
    {
        // Arrange
        var config = new ObjectStorageConfigDto { MountPath = _testDirectory };
        var invalidPath = ""; // Empty path

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _fileBusiness.GetFileSize(invalidPath, config)
        );
    }

    [Fact]
    public async Task GetFileSize_HandlesFileInNestedDirectory()
    {
        // Arrange
        var config = new ObjectStorageConfigDto { MountPath = _testDirectory };
        var nestedPath = Path.Combine(_testDirectory, "level1", "level2", "nested.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(nestedPath)!);

        await File.WriteAllBytesAsync(nestedPath, new byte[2048]);

        try
        {
            // Act
            var result = await _fileBusiness.GetFileSize(nestedPath, config);

            // Assert
            Assert.Equal(2048, result);
        }
        finally
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public async Task GetFileSize_ReturnsCorrectSize_AfterFileModification()
    {
        // Arrange
        var config = new ObjectStorageConfigDto { MountPath = _testDirectory };
        var testFilePath = Path.Combine(_testDirectory, "modified.txt");
        Directory.CreateDirectory(_testDirectory);

        // Create initial file
        await File.WriteAllBytesAsync(testFilePath, new byte[500]);

        try
        {
            // Get initial size
            var initialSize = await _fileBusiness.GetFileSize(testFilePath, config);
            Assert.Equal(500, initialSize);

            // Modify file
            await File.WriteAllBytesAsync(testFilePath, new byte[1500]);

            // Act - Get size after modification
            var newSize = await _fileBusiness.GetFileSize(testFilePath, config);

            // Assert
            Assert.Equal(1500, newSize);
            Assert.NotEqual(initialSize, newSize);
        }
        finally
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public async Task GetFileSize_WorksWithDifferentFileExtensions()
    {
        // Arrange
        var config = new ObjectStorageConfigDto { MountPath = _testDirectory };
        Directory.CreateDirectory(_testDirectory);

        var txtPath = Path.Combine(_testDirectory, "test.txt");
        var binPath = Path.Combine(_testDirectory, "test.bin");
        var csvPath = Path.Combine(_testDirectory, "test.csv");
        var jsonPath = Path.Combine(_testDirectory, "test.json");

        await File.WriteAllBytesAsync(txtPath, new byte[100]);
        await File.WriteAllBytesAsync(binPath, new byte[200]);
        await File.WriteAllBytesAsync(csvPath, new byte[300]);
        await File.WriteAllBytesAsync(jsonPath, new byte[400]);

        try
        {
            // Act & Assert
            Assert.Equal(100, await _fileBusiness.GetFileSize(txtPath, config));
            Assert.Equal(200, await _fileBusiness.GetFileSize(binPath, config));
            Assert.Equal(300, await _fileBusiness.GetFileSize(csvPath, config));
            Assert.Equal(400, await _fileBusiness.GetFileSize(jsonPath, config));
        }
        finally
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
    }

    #endregion

    #region Resumable Upload Tests

    [Fact]
    public async Task CreateUpload_WithValidConfig_CreatesUploadDirectoryAndMetaFile()
    {
        // Arrange
        const long dataSourceId = 1;
        const long uploadLength = 1234;
        var config = new ObjectStorageConfigDto { MountPath = _testDirectory };

        try
        {
            // Act
            var uploadId = await _fileBusiness.CreateUpload(
                organizationId,
                pid,
                dataSourceId,
                config,
                uploadLength);

            var uploadPath = GetResumableUploadPath(organizationId, pid, dataSourceId, uploadId.ToString());
            var metaPath = Path.Combine(uploadPath, "meta.json");

            // Assert
            Assert.NotEqual(Guid.Empty, uploadId);
            Assert.True(Directory.Exists(uploadPath));
            Assert.True(File.Exists(metaPath));

            var metaJson = await File.ReadAllTextAsync(metaPath);
            Assert.Contains($"\"UploadLength\":{uploadLength}", metaJson.Replace(" ", ""));
        }
        finally
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public async Task CreateUpload_WhenMountPathIsNull_ThrowsInvalidOperationException()
    {
        // Arrange
        const long dataSourceId = 1;
        const long uploadLength = 100;
        var config = new ObjectStorageConfigDto { MountPath = null };

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _fileBusiness.CreateUpload(
                organizationId,
                pid,
                dataSourceId,
                config,
                uploadLength));

        // Assert
        Assert.Equal("File system mount path not set in object storage", exception.Message);
    }

    [Fact]
    public async Task GetUploadOffset_WhenDataFileDoesNotExist_ReturnsZero()
    {
        // Arrange
        const long dataSourceId = 1;
        const long uploadLength = 100;
        var config = new ObjectStorageConfigDto { MountPath = _testDirectory };

        try
        {
            var uploadId = await _fileBusiness.CreateUpload(
                organizationId,
                pid,
                dataSourceId,
                config,
                uploadLength);

            // Act
            var result = await _fileBusiness.GetUploadOffset(
                organizationId,
                pid,
                dataSourceId,
                uploadId.ToString(),
                config);

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
    public async Task GetUploadOffset_WhenDataFileExists_ReturnsDataFileLength()
    {
        // Arrange
        const long dataSourceId = 1;
        const long uploadLength = 100;
        var config = new ObjectStorageConfigDto { MountPath = _testDirectory };

        try
        {
            var uploadId = await _fileBusiness.CreateUpload(
                organizationId,
                pid,
                dataSourceId,
                config,
                uploadLength);

            var uploadPath = GetResumableUploadPath(organizationId, pid, dataSourceId, uploadId.ToString());
            var dataPath = Path.Combine(uploadPath, "data");

            await File.WriteAllBytesAsync(dataPath, new byte[37]);

            // Act
            var result = await _fileBusiness.GetUploadOffset(
                organizationId,
                pid,
                dataSourceId,
                uploadId.ToString(),
                config);

            // Assert
            Assert.Equal(37, result);
        }
        finally
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public async Task GetUploadOffset_WhenUploadSessionDoesNotExist_ThrowsInvalidOperationException()
    {
        // Arrange
        const long dataSourceId = 1;
        var config = new ObjectStorageConfigDto { MountPath = _testDirectory };
        var uploadId = Guid.NewGuid().ToString();

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _fileBusiness.GetUploadOffset(
                organizationId,
                pid,
                dataSourceId,
                uploadId,
                config));

        // Assert
        Assert.Equal($"Upload session {uploadId} not found or expired", exception.Message);
    }

    [Fact]
    public async Task GetUploadOffset_WhenMountPathIsNull_ThrowsInvalidOperationException()
    {
        // Arrange
        const long dataSourceId = 1;
        var config = new ObjectStorageConfigDto { MountPath = null };
        var uploadId = Guid.NewGuid().ToString();

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _fileBusiness.GetUploadOffset(
                organizationId,
                pid,
                dataSourceId,
                uploadId,
                config));

        // Assert
        Assert.Equal("File system mount path not set in object storage", exception.Message);
    }

    [Fact]
    public async Task GetUploadLength_WithMetaFile_ReturnsUploadLength()
    {
        // Arrange
        const long dataSourceId = 1;
        const long uploadLength = 9876;
        var config = new ObjectStorageConfigDto { MountPath = _testDirectory };

        try
        {
            var uploadId = await _fileBusiness.CreateUpload(
                organizationId,
                pid,
                dataSourceId,
                config,
                uploadLength);

            // Act
            var result = await _fileBusiness.GetUploadLength(
                organizationId,
                pid,
                dataSourceId,
                uploadId.ToString(),
                config);

            // Assert
            Assert.Equal(uploadLength, result);
        }
        finally
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public async Task GetUploadLength_WhenUploadSessionDoesNotExist_ThrowsInvalidOperationException()
    {
        // Arrange
        const long dataSourceId = 1;
        var config = new ObjectStorageConfigDto { MountPath = _testDirectory };
        var uploadId = Guid.NewGuid().ToString();

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _fileBusiness.GetUploadLength(
                organizationId,
                pid,
                dataSourceId,
                uploadId,
                config));

        // Assert
        Assert.Equal($"Upload session {uploadId} not found or expired", exception.Message);
    }

    [Fact]
    public async Task GetUploadLength_WhenMetadataFileIsMissing_ThrowsInvalidOperationException()
    {
        // Arrange
        const long dataSourceId = 1;
        var config = new ObjectStorageConfigDto { MountPath = _testDirectory };
        var uploadId = Guid.NewGuid().ToString();

        var uploadPath = GetResumableUploadPath(organizationId, pid, dataSourceId, uploadId);
        Directory.CreateDirectory(uploadPath);

        try
        {
            // Act
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _fileBusiness.GetUploadLength(
                    organizationId,
                    pid,
                    dataSourceId,
                    uploadId,
                    config));

            // Assert
            Assert.Equal($"Metadata for upload session {uploadId} not found", exception.Message);
        }
        finally
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public async Task GetUploadLength_WhenMountPathIsNull_ThrowsInvalidOperationException()
    {
        // Arrange
        const long dataSourceId = 1;
        var config = new ObjectStorageConfigDto { MountPath = null };
        var uploadId = Guid.NewGuid().ToString();

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _fileBusiness.GetUploadLength(
                organizationId,
                pid,
                dataSourceId,
                uploadId,
                config));

        // Assert
        Assert.Equal("File system mount path not set in object storage", exception.Message);
    }

    [Fact]
    public async Task UploadPart_ToNewDataFile_ReturnsNewOffsetAndWritesData()
    {
        // Arrange
        const long dataSourceId = 1;
        const long uploadLength = 100;
        const long uploadOffset = 0;
        var config = new ObjectStorageConfigDto { MountPath = _testDirectory };

        using var uploadBody = new MemoryStream(Encoding.UTF8.GetBytes("hello"));

        try
        {
            var uploadId = await _fileBusiness.CreateUpload(
                organizationId,
                pid,
                dataSourceId,
                config,
                uploadLength);

            var uploadPath = GetResumableUploadPath(organizationId, pid, dataSourceId, uploadId.ToString());
            var dataPath = Path.Combine(uploadPath, "data");

            // Act
            var result = await _fileBusiness.UploadPart(
                organizationId,
                pid,
                dataSourceId,
                uploadId.ToString(),
                uploadOffset,
                config,
                uploadBody);

            // Assert
            Assert.Equal(5, result);
            Assert.True(File.Exists(dataPath));
            Assert.Equal("hello", await File.ReadAllTextAsync(dataPath));
        }
        finally
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public async Task UploadPart_WhenAppendingSecondPart_ReturnsCombinedOffsetAndWritesCombinedData()
    {
        // Arrange
        const long dataSourceId = 1;
        const long uploadLength = 100;
        var config = new ObjectStorageConfigDto { MountPath = _testDirectory };

        using var firstBody = new MemoryStream(Encoding.UTF8.GetBytes("hello"));
        using var secondBody = new MemoryStream(Encoding.UTF8.GetBytes(" world"));

        try
        {
            var uploadId = await _fileBusiness.CreateUpload(
                organizationId,
                pid,
                dataSourceId,
                config,
                uploadLength);

            var uploadPath = GetResumableUploadPath(organizationId, pid, dataSourceId, uploadId.ToString());
            var dataPath = Path.Combine(uploadPath, "data");

            var firstOffset = await _fileBusiness.UploadPart(
                organizationId,
                pid,
                dataSourceId,
                uploadId.ToString(),
                0,
                config,
                firstBody);

            // Act
            var secondOffset = await _fileBusiness.UploadPart(
                organizationId,
                pid,
                dataSourceId,
                uploadId.ToString(),
                firstOffset,
                config,
                secondBody);

            // Assert
            Assert.Equal(5, firstOffset);
            Assert.Equal(11, secondOffset);
            Assert.Equal("hello world", await File.ReadAllTextAsync(dataPath));
        }
        finally
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public async Task UploadPart_WhenWritingAtExistingOffset_OverwritesExistingData()
    {
        // Arrange
        const long dataSourceId = 1;
        const long uploadLength = 100;
        var config = new ObjectStorageConfigDto { MountPath = _testDirectory };

        using var firstBody = new MemoryStream(Encoding.UTF8.GetBytes("hello world"));
        using var overwriteBody = new MemoryStream(Encoding.UTF8.GetBytes("there"));

        try
        {
            var uploadId = await _fileBusiness.CreateUpload(
                organizationId,
                pid,
                dataSourceId,
                config,
                uploadLength);

            var uploadPath = GetResumableUploadPath(organizationId, pid, dataSourceId, uploadId.ToString());
            var dataPath = Path.Combine(uploadPath, "data");

            await _fileBusiness.UploadPart(
                organizationId,
                pid,
                dataSourceId,
                uploadId.ToString(),
                0,
                config,
                firstBody);

            // Act
            var result = await _fileBusiness.UploadPart(
                organizationId,
                pid,
                dataSourceId,
                uploadId.ToString(),
                6,
                config,
                overwriteBody);

            // Assert
            Assert.Equal(11, result);
            Assert.Equal("hello there", await File.ReadAllTextAsync(dataPath));
        }
        finally
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public async Task UploadPart_WhenUploadSessionDoesNotExist_ThrowsInvalidOperationException()
    {
        // Arrange
        const long dataSourceId = 1;
        const long uploadOffset = 0;
        var config = new ObjectStorageConfigDto { MountPath = _testDirectory };
        var uploadId = Guid.NewGuid().ToString();

        using var uploadBody = new MemoryStream(Encoding.UTF8.GetBytes("hello"));

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _fileBusiness.UploadPart(
                organizationId,
                pid,
                dataSourceId,
                uploadId,
                uploadOffset,
                config,
                uploadBody));

        // Assert
        Assert.Equal($"Upload session {uploadId} not found or expired", exception.Message);
    }

    [Fact]
    public async Task UploadPart_WhenMountPathIsNull_ThrowsInvalidOperationException()
    {
        // Arrange
        const long dataSourceId = 1;
        const long uploadOffset = 0;
        var config = new ObjectStorageConfigDto { MountPath = null };
        var uploadId = Guid.NewGuid().ToString();

        using var uploadBody = new MemoryStream(Encoding.UTF8.GetBytes("hello"));

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _fileBusiness.UploadPart(
                organizationId,
                pid,
                dataSourceId,
                uploadId,
                uploadOffset,
                config,
                uploadBody));

        // Assert
        Assert.Equal("File system mount path not set in object storage", exception.Message);
    }

    private string GetResumableUploadPath(long orgId, long projectId, long dataSourceId, string uploadId)
    {
        return Path.Combine(
            _testDirectory,
            $"org_{orgId}",
            $"project_{projectId}",
            $"datasource_{dataSourceId}",
            "uploads",
            uploadId);
    }

    #endregion
}