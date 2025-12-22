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

        try
        {
            // Act
            var updatedPath = await _fileBusiness.UpdateFile(record, fileMock.Object);

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
            var result = await _fileBusiness.DownloadFile(record);

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
            var delete = await _fileBusiness.DeleteFile(record);

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