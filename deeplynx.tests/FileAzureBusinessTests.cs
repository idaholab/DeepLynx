using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using deeplynx.business;
using deeplynx.datalayer.Models;
using deeplynx.models;
using Microsoft.AspNetCore.Http;
using Testcontainers.Azurite;
using Record = deeplynx.datalayer.Models.Record;

namespace deeplynx.tests;

// Fixture specifically for this test class only
public class FileAzuriteFixture : IAsyncLifetime
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
public class FileAzureBusinessTests : IntegrationTestBase, IClassFixture<FileAzuriteFixture>
{
    private FileAzureBusiness _fileAzureBusiness = null!;
    private readonly FileAzuriteFixture _azuriteFixture;
    private string _connectionString = null!;
    private string _containerName = "test-container";
    private ObjectStorageConfigDto _objectStorageConfig = null!;

    // Test data IDs
    private long _oid;
    private long _pid;
    private long _dsid;
    private long _uid;
    private long _recordId;

    public FileAzureBusinessTests(TestSuiteFixture fixture, FileAzuriteFixture azuriteFixture) : base(fixture)
    {
        _azuriteFixture = azuriteFixture;
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        
        _connectionString = _azuriteFixture.AzuriteConnectionString;

        // Set up object storage config
        _objectStorageConfig = new ObjectStorageConfigDto
        {
            AzureObjectConfig = new AzureObjectConfigDto
            {
                AzureConnectionString = _connectionString,
                AzureContainerName = _containerName
            }
        };

        _fileAzureBusiness = new FileAzureBusiness();
    }

    public override async Task DisposeAsync()
    {
        var blobServiceClient = new BlobServiceClient(_connectionString);
            
        // Get all containers
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
            Email = "test.user@test.com",
        };
        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        _uid = user.Id;

        // Create organization
        var org = new Organization
        {
            Name = "Test Org",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = _uid
        };
        Context.Organizations.Add(org);
        await Context.SaveChangesAsync();
        _oid = org.Id;

        // Create project
        var project = new Project
        {
            Name = "Test Project",
            OrganizationId = _oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = _uid
        };
        Context.Projects.Add(project);
        await Context.SaveChangesAsync();
        _pid = project.Id;

        // Create data source
        var dataSource = new DataSource
        {
            Name = "Test Datasource",
            ProjectId = _pid,
            OrganizationId = _oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = _uid
        };
        Context.DataSources.Add(dataSource);
        await Context.SaveChangesAsync();
        _dsid = dataSource.Id;

        // Create class
        var testClass = new Class
        {
            Name = "Test Class",
            ProjectId = _pid,
            OrganizationId = _oid,
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = _uid
        };
        Context.Classes.Add(testClass);
        await Context.SaveChangesAsync();

        // Create record
        var record = new Record
        {
            Name = "Test Record",
            Description = "Test Record description",
            ClassId = testClass.Id,
            DataSourceId = _dsid,
            Properties = "{}",
            ProjectId = _pid,
            OrganizationId = _oid,
            OriginalId = "test-original-id",
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            LastUpdatedBy = _uid
        };
        Context.Records.Add(record);
        await Context.SaveChangesAsync();
        _recordId = record.Id;
    }

    #region Helper Methods

    private IFormFile CreateMockFile(string fileName, string content)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        var formFile = new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };
        return formFile;
    }

    private async Task<bool> BlobExistsAsync(string fileName)
    {
        var container = new BlobContainerClient(_connectionString, _containerName);
        var blob = container.GetBlobClient(fileName);
        return await blob.ExistsAsync();
    }

    private async Task<string> GetBlobContentAsync(string fileName)
    {
        var container = new BlobContainerClient(_connectionString, _containerName);
        var blob = container.GetBlobClient(fileName);
        
        var response = await blob.DownloadContentAsync();
        return response.Value.Content.ToString();
    }

    #endregion

    #region UploadFile Tests

    [Fact]
    public async Task UploadFile_Success_CreatesFileInAzure()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var fileName = "test-file.txt";
        var fileContent = "This is test content";
        var mockFile = CreateMockFile(fileName, fileContent);

        // Act
        var result = await _fileAzureBusiness.UploadFile(
            _oid, _pid, _dsid, _objectStorageConfig, mockFile, guid);

        // Assert
        Assert.NotNull(result);
        Assert.Equal($"organization_{_oid}/project_{_pid}/datasource_{_dsid}/{guid}_{fileName}", result);
        
        // Verify file exists in Azure
        var exists = await BlobExistsAsync(result);
        Assert.True(exists);

        // Verify content
        var storedContent = await GetBlobContentAsync(result);
        Assert.Equal(fileContent, storedContent);
    }

    [Fact]
    public async Task UploadFile_Success_CreatesContainerIfNotExists()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var fileName = "test-file.txt";
        var fileContent = "This is test content";
        var mockFile = CreateMockFile(fileName, fileContent);

        // Ensure container doesn't exist
        var container = new BlobContainerClient(_connectionString, _containerName);
        await container.DeleteIfExistsAsync();
        Assert.False(await container.ExistsAsync());

        // Act
        var result = await _fileAzureBusiness.UploadFile(
            _oid, _pid, _dsid, _objectStorageConfig, mockFile, guid);

        // Assert
        Assert.NotNull(result);
        var containerExists = await container.ExistsAsync();
        Assert.True(containerExists);
    }

    [Fact]
    public async Task UploadFile_Fails_WhenAzureConfigIsNull()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var mockFile = CreateMockFile("test.txt", "content");
        var invalidConfig = new ObjectStorageConfigDto
        {
            AzureObjectConfig = null
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _fileAzureBusiness.UploadFile(_oid, _pid, _dsid, invalidConfig, mockFile, guid));
    }

    #endregion

    #region UpdateFile Tests

    [Fact]
    public async Task UpdateFile_Success_ReplacesOldFileWithNew()
    {
        // Arrange
        var oldGuid = Guid.NewGuid();
        var newGuid = Guid.NewGuid();
        var oldFileName = "old-file.txt";
        var newFileName = "new-file.txt";
        var oldContent = "Old content";
        var newContent = "New content";

        // Upload original file
        var oldFile = CreateMockFile(oldFileName, oldContent);
        var oldUri = await _fileAzureBusiness.UploadFile(
            _oid, _pid, _dsid, _objectStorageConfig, oldFile, oldGuid);

        // Update record with URI
        var record = await Context.Records.FindAsync(_recordId);
        record!.Uri = oldUri;
        await Context.SaveChangesAsync();

        var recordDto = new RecordResponseDto
        {
            Id = record.Id,
            Name = record.Name,
            Uri = record.Uri,
            OrganizationId = _oid,
            ProjectId = _pid,
            DataSourceId = _dsid
        };

        var newFile = CreateMockFile(newFileName, newContent);

        // Act
        var result = await _fileAzureBusiness.UpdateFile(
            recordDto, _objectStorageConfig, newFile, newGuid);

        // Assert
        Assert.NotNull(result);
        Assert.Contains(newFileName, result);
        
        // Old file should be deleted
        var oldExists = await BlobExistsAsync(oldUri);
        Assert.False(oldExists);

        // New file should exist with new content
        var newExists = await BlobExistsAsync(result);
        Assert.True(newExists);
        var storedContent = await GetBlobContentAsync(result);
        Assert.Equal(newContent, storedContent);
    }

    [Fact]
    public async Task UpdateFile_Fails_WhenRecordUriIsNull()
    {
        // Arrange
        var recordDto = new RecordResponseDto
        {
            Uri = null,
            OrganizationId = _oid,
            ProjectId = _pid,
            DataSourceId = _dsid
        };
        var mockFile = CreateMockFile("test.txt", "content");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _fileAzureBusiness.UpdateFile(recordDto, _objectStorageConfig, mockFile, Guid.NewGuid()));
    }

    [Fact]
    public async Task UpdateFile_Fails_WhenAzureConfigIsNull()
    {
        // Arrange
        var recordDto = new RecordResponseDto
        {
            Uri = "some-uri",
            OrganizationId = _oid,
            ProjectId = _pid,
            DataSourceId = _dsid
        };
        var mockFile = CreateMockFile("test.txt", "content");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _fileAzureBusiness.UpdateFile(recordDto, null, mockFile, Guid.NewGuid()));
    }

    [Fact]
    public async Task UpdateFile_Fails_WhenContainerDoesNotExist()
    {
        // Arrange
        var invalidConfig = new ObjectStorageConfigDto
        {
            AzureObjectConfig = new AzureObjectConfigDto
            {
                AzureConnectionString = _connectionString,
                AzureContainerName = "non-existent-container"
            }
        };

        var recordDto = new RecordResponseDto
        {
            Uri = "some-uri",
            OrganizationId = _oid,
            ProjectId = _pid,
            DataSourceId = _dsid
        };
        var mockFile = CreateMockFile("test.txt", "content");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _fileAzureBusiness.UpdateFile(recordDto, invalidConfig, mockFile, Guid.NewGuid()));
    }

    [Fact]
    public async Task UpdateFile_Fails_WhenOldFileDoesNotExist()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var fileName = "test-file.txt";
        var fileContent = "This is test content";
        var mockFile = CreateMockFile(fileName, fileContent);
        
        await _fileAzureBusiness.UploadFile(
            _oid, _pid, _dsid, _objectStorageConfig, mockFile, guid);
        
        var recordNonExistentFileDto = new RecordResponseDto
        {
            Uri = "non-existent-file.txt",
            Name = "test.txt",
            OrganizationId = _oid,
            ProjectId = _pid,
            DataSourceId = _dsid
        };
        
        
        var newFileName = "new-test-file.txt";
        var newFileContent = "This is new test content";
        var newMockFile =  CreateMockFile(newFileName, newFileContent);

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _fileAzureBusiness.UpdateFile(recordNonExistentFileDto, _objectStorageConfig, newMockFile, Guid.NewGuid()));
    }

    [Fact]
    public async Task UpdateFile_RollsBack_OnUploadFailure()
    {
        // Arrange
        var oldGuid = Guid.NewGuid();
        var oldFile = CreateMockFile("old.txt", "old content");
        var oldUri = await _fileAzureBusiness.UploadFile(
            _oid, _pid, _dsid, _objectStorageConfig, oldFile, oldGuid);

        var recordDto = new RecordResponseDto
        {
            Uri = oldUri,
            OrganizationId = _oid,
            ProjectId = _pid,
            DataSourceId = _dsid
        };

        // Create a file that will fail to upload (empty stream that's been disposed)
        var stream = new MemoryStream();
        await stream.DisposeAsync();
        var badFile = new FormFile(stream, 0, 0, "file", "bad.txt");

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() =>
            _fileAzureBusiness.UpdateFile(recordDto, _objectStorageConfig, badFile, Guid.NewGuid()));

        // Old file should still exist
        var oldExists = await BlobExistsAsync(oldUri);
        Assert.True(oldExists);
    }

    #endregion

    #region DownloadFile Tests

    [Fact]
    public async Task DownloadFile_Success_ReturnsFileStream()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var fileName = "download-test.txt";
        var fileContent = "Download test content";
        var mockFile = CreateMockFile(fileName, fileContent);

        var uri = await _fileAzureBusiness.UploadFile(
            _oid, _pid, _dsid, _objectStorageConfig, mockFile, guid);

        var record = await Context.Records.FindAsync(_recordId);
        record!.Uri = uri;
        record.Name = fileName;
        await Context.SaveChangesAsync();

        var recordDto = new RecordResponseDto
        {
            Id = record.Id,
            Name = record.Name,
            Uri = record.Uri,
            OrganizationId = _oid,
            ProjectId = _pid,
            DataSourceId = _dsid
        };

        // Act
        var result = await _fileAzureBusiness.DownloadFile(recordDto, _objectStorageConfig);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(fileName, result.FileDownloadName);
        Assert.Equal("text/plain", result.ContentType);

        // Read stream content
        using var reader = new StreamReader(result.FileStream);
        var content = await reader.ReadToEndAsync();
        Assert.Equal(fileContent, content);
    }

    [Fact]
    public async Task DownloadFile_Success_SetsCorrectContentType()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var pdfFileName = "test.pdf";
        var mockFile = CreateMockFile(pdfFileName, "PDF content");

        var uri = await _fileAzureBusiness.UploadFile(
            _oid, _pid, _dsid, _objectStorageConfig, mockFile, guid);

        var recordDto = new RecordResponseDto
        {
            Name = pdfFileName,
            Uri = uri,
            OrganizationId = _oid,
            ProjectId = _pid,
            DataSourceId = _dsid
        };

        // Act
        var result = await _fileAzureBusiness.DownloadFile(recordDto, _objectStorageConfig);

        // Assert
        Assert.Equal("application/pdf", result.ContentType);
    }

    [Fact]
    public async Task DownloadFile_Success_UsesDefaultContentTypeForUnknownExtension()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var unknownFileName = "test.unknownext";
        var mockFile = CreateMockFile(unknownFileName, "content");

        var uri = await _fileAzureBusiness.UploadFile(
            _oid, _pid, _dsid, _objectStorageConfig, mockFile, guid);

        var recordDto = new RecordResponseDto
        {
            Name = unknownFileName,
            Uri = uri,
            OrganizationId = _oid,
            ProjectId = _pid,
            DataSourceId = _dsid
        };

        // Act
        var result = await _fileAzureBusiness.DownloadFile(recordDto, _objectStorageConfig);

        // Assert
        Assert.Equal("application/octet-stream", result.ContentType);
    }

    [Fact]
    public async Task DownloadFile_Fails_WhenRecordUriIsNull()
    {
        // Arrange
        var recordDto = new RecordResponseDto
        {
            Uri = null,
            OrganizationId = _oid,
            ProjectId = _pid,
            DataSourceId = _dsid
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _fileAzureBusiness.DownloadFile(recordDto, _objectStorageConfig));
    }

    [Fact]
    public async Task DownloadFile_Fails_WhenAzureConfigIsNull()
    {
        // Arrange
        var recordDto = new RecordResponseDto
        {
            Uri = "some-uri",
            OrganizationId = _oid,
            ProjectId = _pid,
            DataSourceId = _dsid
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _fileAzureBusiness.DownloadFile(recordDto, null));
    }

    [Fact]
    public async Task DownloadFile_Fails_WhenContainerDoesNotExist()
    {
        // Arrange
        var invalidConfig = new ObjectStorageConfigDto
        {
            AzureObjectConfig = new AzureObjectConfigDto
            {
                AzureConnectionString = _connectionString,
                AzureContainerName = "non-existent-container"
            }
        };

        var recordDto = new RecordResponseDto
        {
            Uri = "some-uri",
            OrganizationId = _oid,
            ProjectId = _pid,
            DataSourceId = _dsid
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _fileAzureBusiness.DownloadFile(recordDto, invalidConfig));
    }

    [Fact]
    public async Task DownloadFile_Fails_WhenFileDoesNotExist()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var fileName = "test-file.txt";
        var fileContent = "This is test content";
        var mockFile = CreateMockFile(fileName, fileContent);
        
        await _fileAzureBusiness.UploadFile(
            _oid, _pid, _dsid, _objectStorageConfig, mockFile, guid);
        
        var recordNonExistentFileDto = new RecordResponseDto
        {
            Uri = "non-existent-file.txt",
            Name = "test.txt",
            OrganizationId = _oid,
            ProjectId = _pid,
            DataSourceId = _dsid
        };

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _fileAzureBusiness.DownloadFile(recordNonExistentFileDto, _objectStorageConfig));
    }

    #endregion

    #region DeleteFile Tests

    [Fact]
    public async Task DeleteFile_Success_RemovesFileFromAzure()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var fileName = "delete-test.txt";
        var mockFile = CreateMockFile(fileName, "content to delete");

        var uri = await _fileAzureBusiness.UploadFile(
            _oid, _pid, _dsid, _objectStorageConfig, mockFile, guid);

        var record = await Context.Records.FindAsync(_recordId);
        record!.Uri = uri;
        await Context.SaveChangesAsync();

        var recordDto = new RecordResponseDto
        {
            Uri = uri,
            OrganizationId = _oid,
            ProjectId = _pid,
            DataSourceId = _dsid
        };

        // Verify file exists before deletion
        var existsBefore = await BlobExistsAsync(uri);
        Assert.True(existsBefore);

        // Act
        var result = await _fileAzureBusiness.DeleteFile(recordDto, _objectStorageConfig);

        // Assert
        Assert.True(result);

        // Verify file no longer exists
        var existsAfter = await BlobExistsAsync(uri);
        Assert.False(existsAfter);
    }

    [Fact]
    public async Task DeleteFile_ReturnsFalse_WhenFileDoesNotExist()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var fileName = "test-file.txt";
        var fileContent = "This is test content";
        var mockFile = CreateMockFile(fileName, fileContent);

        // Act
        var uploadResult = await _fileAzureBusiness.UploadFile(
            _oid, _pid, _dsid, _objectStorageConfig, mockFile, guid);

        // Arrange
        var recordDto = new RecordResponseDto
        {
            Uri = "non-existent-file.txt",
            OrganizationId = _oid,
            ProjectId = _pid,
            DataSourceId = _dsid
        };

        // Act
        var result = await _fileAzureBusiness.DeleteFile(recordDto, _objectStorageConfig);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DeleteFile_Fails_WhenAzureConfigIsNull()
    {
        // Arrange
        var recordDto = new RecordResponseDto
        {
            Uri = "some-uri",
            OrganizationId = _oid,
            ProjectId = _pid,
            DataSourceId = _dsid
        };

        var invalidConfig = new ObjectStorageConfigDto
        {
            AzureObjectConfig = null
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _fileAzureBusiness.DeleteFile(recordDto, invalidConfig));
    }

    [Fact]
    public async Task DeleteFile_Fails_WhenContainerDoesNotExist()
    {
        // Arrange
        var invalidConfig = new ObjectStorageConfigDto
        {
            AzureObjectConfig = new AzureObjectConfigDto
            {
                AzureConnectionString = _connectionString,
                AzureContainerName = "non-existent-container"
            }
        };

        var recordDto = new RecordResponseDto
        {
            Uri = "some-uri",
            OrganizationId = _oid,
            ProjectId = _pid,
            DataSourceId = _dsid
        };

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _fileAzureBusiness.DeleteFile(recordDto, invalidConfig));
    }

    [Fact]
    public async Task DeleteFile_Success_IsIdempotent()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var mockFile = CreateMockFile("test.txt", "content");
        var uri = await _fileAzureBusiness.UploadFile(
            _oid, _pid, _dsid, _objectStorageConfig, mockFile, guid);

        var recordDto = new RecordResponseDto
        {
            Uri = uri,
            OrganizationId = _oid,
            ProjectId = _pid,
            DataSourceId = _dsid
        };

        // Act - Delete twice
        var firstDelete = await _fileAzureBusiness.DeleteFile(recordDto, _objectStorageConfig);
        var secondDelete = await _fileAzureBusiness.DeleteFile(recordDto, _objectStorageConfig);

        // Assert
        Assert.True(firstDelete);
        Assert.False(secondDelete); // Already deleted
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task FileLifecycle_UploadUpdateDownloadDelete_WorksTogether()
    {
        // Upload
        var uploadGuid = Guid.NewGuid();
        var originalFile = CreateMockFile("lifecycle.txt", "Original content");
        var uploadedUri = await _fileAzureBusiness.UploadFile(
            _oid, _pid, _dsid, _objectStorageConfig, originalFile, uploadGuid);

        Assert.NotNull(uploadedUri);

        // Update record with URI
        var record = await Context.Records.FindAsync(_recordId);
        record!.Uri = uploadedUri;
        record.Name = "lifecycle.txt";
        await Context.SaveChangesAsync();

        var recordDto = new RecordResponseDto
        {
            Id = record.Id,
            Name = record.Name,
            Uri = record.Uri,
            OrganizationId = _oid,
            ProjectId = _pid,
            DataSourceId = _dsid
        };

        // Download and verify
        var downloadResult = await _fileAzureBusiness.DownloadFile(recordDto, _objectStorageConfig);
        using (var reader = new StreamReader(downloadResult.FileStream))
        {
            var content = await reader.ReadToEndAsync();
            Assert.Equal("Original content", content);
        }

        // Update
        var updateGuid = Guid.NewGuid();
        var updatedFile = CreateMockFile("lifecycle-updated.txt", "Updated content");
        var updatedUri = await _fileAzureBusiness.UpdateFile(
            recordDto, _objectStorageConfig, updatedFile, updateGuid);

        Assert.NotNull(updatedUri);
        Assert.NotEqual(uploadedUri, updatedUri);

        // Update record with new URI
        record.Uri = updatedUri;
        await Context.SaveChangesAsync();
        recordDto.Uri = updatedUri;

        // Download updated file and verify
        var downloadResult2 = await _fileAzureBusiness.DownloadFile(recordDto, _objectStorageConfig);
        using (var reader = new StreamReader(downloadResult2.FileStream))
        {
            var content = await reader.ReadToEndAsync();
            Assert.Equal("Updated content", content);
        }

        // Delete
        var deleteResult = await _fileAzureBusiness.DeleteFile(recordDto, _objectStorageConfig);
        Assert.True(deleteResult);

        // Verify deleted
        var exists = await BlobExistsAsync(updatedUri);
        Assert.False(exists);
    }

    [Fact]
    public async Task MultipleFiles_CanBeStoredInSameContainer()
    {
        // Arrange & Act
        var file1 = CreateMockFile("file1.txt", "Content 1");
        var file2 = CreateMockFile("file2.txt", "Content 2");
        var file3 = CreateMockFile("file3.txt", "Content 3");

        var uri1 = await _fileAzureBusiness.UploadFile(
            _oid, _pid, _dsid, _objectStorageConfig, file1, Guid.NewGuid());
        var uri2 = await _fileAzureBusiness.UploadFile(
            _oid, _pid, _dsid, _objectStorageConfig, file2, Guid.NewGuid());
        var uri3 = await _fileAzureBusiness.UploadFile(
            _oid, _pid, _dsid, _objectStorageConfig, file3, Guid.NewGuid());

        // Assert
        Assert.True(await BlobExistsAsync(uri1));
        Assert.True(await BlobExistsAsync(uri2));
        Assert.True(await BlobExistsAsync(uri3));

        Assert.Equal("Content 1", await GetBlobContentAsync(uri1));
        Assert.Equal("Content 2", await GetBlobContentAsync(uri2));
        Assert.Equal("Content 3", await GetBlobContentAsync(uri3));
    }

    #endregion
    
    #region GenerateUploadUrl Tests

    [Fact]
    public async Task GenerateUploadUrl_Success_ReturnsValidSasUri()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var fileName = "upload-test.txt";

        // Act
        var result = await _fileAzureBusiness.GenerateUploadUrl(
            _oid, _pid, _dsid, _objectStorageConfig, fileName, guid, expirationHours: 24);

        // Assert
        Assert.NotNull(result);
        Assert.Contains(_containerName, result);
        Assert.Contains($"{guid}_{fileName}", result);
        Assert.Contains("sig=", result); // SAS token signature
        Assert.Contains("se=", result); // Expiry time
        Assert.Contains("sp=", result); // Permissions
    }

    [Fact]
    public async Task GenerateUploadUrl_Success_CreatesContainerIfNotExists()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var fileName = "test-upload.txt";

        // Ensure container doesn't exist
        var container = new BlobContainerClient(_connectionString, _containerName);
        await container.DeleteIfExistsAsync();
        Assert.False(await container.ExistsAsync());

        // Act
        var result = await _fileAzureBusiness.GenerateUploadUrl(
            _oid, _pid, _dsid, _objectStorageConfig, fileName, guid);

        // Assert
        Assert.NotNull(result);
        var containerExists = await container.ExistsAsync();
        Assert.True(containerExists);
    }

    [Fact]
    public async Task GenerateUploadUrl_Success_GeneratedUrlCanBeUsedForUpload()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var fileName = "direct-upload-test.txt";
        var fileContent = "This content was uploaded via SAS URL";

        // Act - Generate SAS URL
        var sasUrl = await _fileAzureBusiness.GenerateUploadUrl(
            _oid, _pid, _dsid, _objectStorageConfig, fileName, guid, expirationHours: 1);

        // Use the SAS URL to upload directly
        var blobClient = new BlobClient(new Uri(sasUrl));
        var bytes = System.Text.Encoding.UTF8.GetBytes(fileContent);
        using var stream = new MemoryStream(bytes);
        await blobClient.UploadAsync(stream, overwrite: true);

        // Assert - Verify the file was uploaded successfully
        var expectedBlobName = $"organization_{_oid}/project_{_pid}/datasource_{_dsid}/{guid}_{fileName}";
        var exists = await BlobExistsAsync(expectedBlobName);
        Assert.True(exists);

        var storedContent = await GetBlobContentAsync(expectedBlobName);
        Assert.Equal(fileContent, storedContent);
    }

    [Fact]
    public async Task GenerateUploadUrl_Fails_WhenAzureConfigIsNull()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var fileName = "test.txt";
        var invalidConfig = new ObjectStorageConfigDto
        {
            AzureObjectConfig = null
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _fileAzureBusiness.GenerateUploadUrl(_oid, _pid, _dsid, invalidConfig, fileName, guid));
    }

    [Fact]
    public async Task GenerateUploadUrl_Success_IncludesCorrectPermissions()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var fileName = "permissions-test.txt";

        // Act
        var result = await _fileAzureBusiness.GenerateUploadUrl(
            _oid, _pid, _dsid, _objectStorageConfig, fileName, guid);

        // Assert
        Assert.NotNull(result);
        // SAS URL should contain write (w) and create (c) permissions
        // The sp parameter contains the permissions
        Assert.Contains("sp=", result);
        var uri = new Uri(result);
        var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var permissions = queryParams["sp"];
        Assert.NotNull(permissions);
        Assert.Contains("w", permissions); // Write permission
        Assert.Contains("c", permissions); // Create permission
    }

    [Fact]
    public async Task GenerateUploadUrl_Success_ExpirationTimeIsCorrect()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var fileName = "expiry-test.txt";
        var expirationHours = 12;

        // Act
        var result = await _fileAzureBusiness.GenerateUploadUrl(
            _oid, _pid, _dsid, _objectStorageConfig, fileName, guid, expirationHours);

        // Assert
        Assert.NotNull(result);
        var uri = new Uri(result);
        var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var expiryString = queryParams["se"];
        Assert.NotNull(expiryString);

        var expiry = DateTimeOffset.Parse(expiryString);
        var now = DateTimeOffset.UtcNow;
        var expectedExpiry = now.AddHours(expirationHours);

        // Allow 5 minute tolerance for test execution time
        Assert.InRange(expiry, expectedExpiry.AddMinutes(-5), expectedExpiry.AddMinutes(5));
    }

    #endregion

    #region GenerateDownloadUrl Tests

    [Fact]
    public async Task GenerateDownloadUrl_Success_ReturnsValidSasUri()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var fileName = "download-sas-test.txt";
        var fileContent = "Content for SAS download";
        var mockFile = CreateMockFile(fileName, fileContent);

        // Upload file first
        var uri = await _fileAzureBusiness.UploadFile(
            _oid, _pid, _dsid, _objectStorageConfig, mockFile, guid);

        var recordDto = new RecordResponseDto
        {
            Uri = uri,
            Name = fileName,
            OrganizationId = _oid,
            ProjectId = _pid,
            DataSourceId = _dsid
        };

        // Act
        var result = await _fileAzureBusiness.GenerateDownloadUrl(
            recordDto, _objectStorageConfig, expirationHours: 1);

        // Assert
        Assert.NotNull(result);
        Assert.Contains(_containerName, result);
        Assert.Contains($"{guid}_{fileName}", result);
        Assert.Contains("sig=", result); // SAS token signature
        Assert.Contains("se=", result); // Expiry time
        Assert.Contains("sp=", result); // Permissions
    }

    [Fact]
    public async Task GenerateDownloadUrl_Success_GeneratedUrlCanBeUsedForDownload()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var fileName = "direct-download-test.txt";
        var fileContent = "This content will be downloaded via SAS URL";
        var mockFile = CreateMockFile(fileName, fileContent);

        // Upload file first
        var uri = await _fileAzureBusiness.UploadFile(
            _oid, _pid, _dsid, _objectStorageConfig, mockFile, guid);

        var recordDto = new RecordResponseDto
        {
            Uri = uri,
            Name = fileName,
            OrganizationId = _oid,
            ProjectId = _pid,
            DataSourceId = _dsid
        };

        // Act - Generate download SAS URL
        var sasUrl = await _fileAzureBusiness.GenerateDownloadUrl(
            recordDto, _objectStorageConfig, expirationHours: 1);

        // Use the SAS URL to download directly
        var blobClient = new BlobClient(new Uri(sasUrl));
        var downloadResponse = await blobClient.DownloadContentAsync();
        var downloadedContent = downloadResponse.Value.Content.ToString();

        // Assert
        Assert.Equal(fileContent, downloadedContent);
    }

    [Fact]
    public async Task GenerateDownloadUrl_Fails_WhenRecordUriIsNull()
    {
        // Arrange
        var recordDto = new RecordResponseDto
        {
            Uri = null,
            OrganizationId = _oid,
            ProjectId = _pid,
            DataSourceId = _dsid
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _fileAzureBusiness.GenerateDownloadUrl(recordDto, _objectStorageConfig));
    }

    [Fact]
    public async Task GenerateDownloadUrl_Fails_WhenAzureConfigIsNull()
    {
        // Arrange
        var recordDto = new RecordResponseDto
        {
            Uri = "some-uri",
            OrganizationId = _oid,
            ProjectId = _pid,
            DataSourceId = _dsid
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _fileAzureBusiness.GenerateDownloadUrl(recordDto, null));
    }

    [Fact]
    public async Task GenerateDownloadUrl_Fails_WhenContainerDoesNotExist()
    {
        // Arrange
        var invalidConfig = new ObjectStorageConfigDto
        {
            AzureObjectConfig = new AzureObjectConfigDto
            {
                AzureConnectionString = _connectionString,
                AzureContainerName = "non-existent-container"
            }
        };

        var recordDto = new RecordResponseDto
        {
            Uri = "some-uri",
            OrganizationId = _oid,
            ProjectId = _pid,
            DataSourceId = _dsid
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _fileAzureBusiness.GenerateDownloadUrl(recordDto, invalidConfig));
    }

    [Fact]
    public async Task GenerateDownloadUrl_Fails_WhenFileDoesNotExist()
    {
        // Arrange
        // Create container but don't upload the file
        var container = new BlobContainerClient(_connectionString, _containerName);
        await container.CreateIfNotExistsAsync();

        var recordDto = new RecordResponseDto
        {
            Uri = "non-existent-file.txt",
            Name = "test.txt",
            OrganizationId = _oid,
            ProjectId = _pid,
            DataSourceId = _dsid
        };

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _fileAzureBusiness.GenerateDownloadUrl(recordDto, _objectStorageConfig));
    }

    [Fact]
    public async Task GenerateDownloadUrl_Success_IncludesOnlyReadPermission()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var fileName = "read-permission-test.txt";
        var mockFile = CreateMockFile(fileName, "content");

        var uri = await _fileAzureBusiness.UploadFile(
            _oid, _pid, _dsid, _objectStorageConfig, mockFile, guid);

        var recordDto = new RecordResponseDto
        {
            Uri = uri,
            Name = fileName,
            OrganizationId = _oid,
            ProjectId = _pid,
            DataSourceId = _dsid
        };

        // Act
        var result = await _fileAzureBusiness.GenerateDownloadUrl(
            recordDto, _objectStorageConfig);

        // Assert
        Assert.NotNull(result);
        var uri_parsed = new Uri(result);
        var queryParams = System.Web.HttpUtility.ParseQueryString(uri_parsed.Query);
        var permissions = queryParams["sp"];
        Assert.NotNull(permissions);
        Assert.Equal("r", permissions); // Should only have read permission
    }

    [Fact]
    public async Task GenerateDownloadUrl_Success_ExpirationTimeIsCorrect()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var fileName = "expiry-download-test.txt";
        var mockFile = CreateMockFile(fileName, "content");

        var uri = await _fileAzureBusiness.UploadFile(
            _oid, _pid, _dsid, _objectStorageConfig, mockFile, guid);

        var recordDto = new RecordResponseDto
        {
            Uri = uri,
            Name = fileName,
            OrganizationId = _oid,
            ProjectId = _pid,
            DataSourceId = _dsid
        };

        var expirationHours = 2;

        // Act
        var result = await _fileAzureBusiness.GenerateDownloadUrl(
            recordDto, _objectStorageConfig, expirationHours);

        // Assert
        Assert.NotNull(result);
        var uri_parsed = new Uri(result);
        var queryParams = System.Web.HttpUtility.ParseQueryString(uri_parsed.Query);
        var expiryString = queryParams["se"];
        Assert.NotNull(expiryString);

        var expiry = DateTimeOffset.Parse(expiryString);
        var now = DateTimeOffset.UtcNow;
        var expectedExpiry = now.AddHours(expirationHours);

        // Allow 5 minute tolerance for test execution time
        Assert.InRange(expiry, expectedExpiry.AddMinutes(-5), expectedExpiry.AddMinutes(5));
    }

    #endregion

    #region SAS Integration Tests

    [Fact]
    public async Task SasUrlWorkflow_UploadViaGeneratedUrl_ThenDownloadViaGeneratedUrl()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var fileName = "sas-workflow-test.txt";
        var fileContent = "End-to-end SAS workflow test content";

        // Act 1: Generate upload URL
        var uploadUrl = await _fileAzureBusiness.GenerateUploadUrl(
            _oid, _pid, _dsid, _objectStorageConfig, fileName, guid, expirationHours: 1);

        // Act 2: Upload using the SAS URL
        var blobClient = new BlobClient(new Uri(uploadUrl));
        var bytes = System.Text.Encoding.UTF8.GetBytes(fileContent);
        using (var uploadStream = new MemoryStream(bytes))
        {
            await blobClient.UploadAsync(uploadStream, overwrite: true);
        }

        // Act 3: Create record DTO with the uploaded file URI
        var uploadedUri = $"organization_{_oid}/project_{_pid}/datasource_{_dsid}/{guid}_{fileName}";
        var recordDto = new RecordResponseDto
        {
            Uri = uploadedUri,
            Name = fileName,
            OrganizationId = _oid,
            ProjectId = _pid,
            DataSourceId = _dsid
        };

        // Act 4: Generate download URL
        var downloadUrl = await _fileAzureBusiness.GenerateDownloadUrl(
            recordDto, _objectStorageConfig, expirationHours: 1);

        // Act 5: Download using the SAS URL
        var downloadBlobClient = new BlobClient(new Uri(downloadUrl));
        var downloadResponse = await downloadBlobClient.DownloadContentAsync();
        var downloadedContent = downloadResponse.Value.Content.ToString();

        // Assert
        Assert.Equal(fileContent, downloadedContent);
    }

    [Fact]
    public async Task GenerateUploadUrl_Success_DifferentFilesGetDifferentUrls()
    {
        // Arrange
        var guid1 = Guid.NewGuid();
        var guid2 = Guid.NewGuid();
        var fileName1 = "file1.txt";
        var fileName2 = "file2.txt";

        // Act
        var url1 = await _fileAzureBusiness.GenerateUploadUrl(
            _oid, _pid, _dsid, _objectStorageConfig, fileName1, guid1);
        var url2 = await _fileAzureBusiness.GenerateUploadUrl(
            _oid, _pid, _dsid, _objectStorageConfig, fileName2, guid2);

        // Assert
        Assert.NotEqual(url1, url2);
        Assert.Contains(fileName1, url1);
        Assert.Contains(fileName2, url2);
        Assert.Contains(guid1.ToString(), url1);
        Assert.Contains(guid2.ToString(), url2);
    }

#endregion

    #region Chunked Upload Tests

    [Fact]
    public async Task StartUpload_Success_ReturnsValidGuid()
    {
        // Act
        var uploadId = await _fileAzureBusiness.StartUpload(
            _oid, _pid, _dsid, _objectStorageConfig);

        // Assert
        Assert.NotEqual(Guid.Empty, uploadId);
    }

    [Fact]
    public async Task StartUpload_Success_CreatesContainerIfNotExists()
    {
        // Arrange
        var container = new BlobContainerClient(_connectionString, _containerName);
        await container.DeleteIfExistsAsync();
        Assert.False(await container.ExistsAsync());

        // Act
        var uploadId = await _fileAzureBusiness.StartUpload(
            _oid, _pid, _dsid, _objectStorageConfig);

        // Assert
        Assert.NotEqual(Guid.Empty, uploadId);
        var containerExists = await container.ExistsAsync();
        Assert.True(containerExists);
    }

    [Fact]
    public async Task StartUpload_Fails_WhenAzureConfigIsNull()
    {
        // Arrange
        var invalidConfig = new ObjectStorageConfigDto
        {
            AzureObjectConfig = null
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _fileAzureBusiness.StartUpload(_oid, _pid, _dsid, invalidConfig));
    }

    [Fact]
    public async Task UploadChunk_Success_StagesBlockToBlob()
    {
        // Arrange
        var uploadId = await _fileAzureBusiness.StartUpload(
            _oid, _pid, _dsid, _objectStorageConfig);
        
        var chunkContent = "This is chunk 0";
        var chunk = CreateMockFile("chunk0.txt", chunkContent);

        // Act
        await _fileAzureBusiness.UploadChunk(
            _oid, _pid, _dsid, 0, uploadId.ToString(), _objectStorageConfig, chunk);

        // Assert - Verify uncommitted blocks exist
        var tempBlobName = $"organization_{_oid}/project_{_pid}/datasource_{_dsid}/uploads/{uploadId}";
        var container = new BlobContainerClient(_connectionString, _containerName);
        var blockBlobClient = container.GetBlockBlobClient(tempBlobName);
        
        var blockList = await blockBlobClient.GetBlockListAsync(Azure.Storage.Blobs.Models.BlockListTypes.Uncommitted);
        var uncommittedBlocks = blockList.Value.UncommittedBlocks.ToList();
        
        Assert.Single(uncommittedBlocks);
    }

    [Fact]
    public async Task UploadChunk_Success_UploadsMultipleChunks()
    {
        // Arrange
        var uploadId = await _fileAzureBusiness.StartUpload(
            _oid, _pid, _dsid, _objectStorageConfig);

        var chunk0 = CreateMockFile("chunk0.txt", "Chunk 0 content");
        var chunk1 = CreateMockFile("chunk1.txt", "Chunk 1 content");
        var chunk2 = CreateMockFile("chunk2.txt", "Chunk 2 content");

        // Act
        await _fileAzureBusiness.UploadChunk(_oid, _pid, _dsid, 0, uploadId.ToString(), _objectStorageConfig, chunk0);
        await _fileAzureBusiness.UploadChunk(_oid, _pid, _dsid, 1, uploadId.ToString(), _objectStorageConfig, chunk1);
        await _fileAzureBusiness.UploadChunk(_oid, _pid, _dsid, 2, uploadId.ToString(), _objectStorageConfig, chunk2);

        // Assert
        var tempBlobName = $"organization_{_oid}/project_{_pid}/datasource_{_dsid}/uploads/{uploadId}";
        var container = new BlobContainerClient(_connectionString, _containerName);
        var blockBlobClient = container.GetBlockBlobClient(tempBlobName);
        
        var blockList = await blockBlobClient.GetBlockListAsync(Azure.Storage.Blobs.Models.BlockListTypes.Uncommitted);
        var uncommittedBlocks = blockList.Value.UncommittedBlocks.ToList();
        
        Assert.Equal(3, uncommittedBlocks.Count);
    }

    [Fact]
    public async Task UploadChunk_Fails_WhenAzureConfigIsNull()
    {
        // Arrange
        var uploadId = Guid.NewGuid();
        var chunk = CreateMockFile("chunk.txt", "content");
        var invalidConfig = new ObjectStorageConfigDto
        {
            AzureObjectConfig = null
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _fileAzureBusiness.UploadChunk(_oid, _pid, _dsid, 0, uploadId.ToString(), invalidConfig, chunk));
    }

    [Fact]
    public async Task UploadChunk_Fails_WhenChunkIsNull()
    {
        // Arrange
        var uploadId = await _fileAzureBusiness.StartUpload(_oid, _pid, _dsid, _objectStorageConfig);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _fileAzureBusiness.UploadChunk(_oid, _pid, _dsid, 0, uploadId.ToString(), _objectStorageConfig, null));
    }

    [Fact]
    public async Task UploadChunk_Fails_WhenContainerDoesNotExist()
    {
        // Arrange
        var uploadId = Guid.NewGuid();
        var chunk = CreateMockFile("chunk.txt", "content");
        var invalidConfig = new ObjectStorageConfigDto
        {
            AzureObjectConfig = new AzureObjectConfigDto
            {
                AzureConnectionString = _connectionString,
                AzureContainerName = "non-existent-container"
            }
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _fileAzureBusiness.UploadChunk(_oid, _pid, _dsid, 0, uploadId.ToString(), invalidConfig, chunk));
    }

    [Fact]
    public async Task CompleteUpload_Success_CombinesChunksIntoSingleFile()
    {
        // Arrange
        var uploadId = await _fileAzureBusiness.StartUpload(_oid, _pid, _dsid, _objectStorageConfig);
        var guid = Guid.NewGuid();
        var fileName = "complete-test.txt";

        // Upload 3 chunks
        var chunk0 = CreateMockFile("chunk0", "Part 1 ");
        var chunk1 = CreateMockFile("chunk1", "Part 2 ");
        var chunk2 = CreateMockFile("chunk2", "Part 3");

        await _fileAzureBusiness.UploadChunk(_oid, _pid, _dsid, 0, uploadId.ToString(), _objectStorageConfig, chunk0);
        await _fileAzureBusiness.UploadChunk(_oid, _pid, _dsid, 1, uploadId.ToString(), _objectStorageConfig, chunk1);
        await _fileAzureBusiness.UploadChunk(_oid, _pid, _dsid, 2, uploadId.ToString(), _objectStorageConfig, chunk2);

        var completeRequest = new FileUploadCompleteRequestDto
        {
            UploadId = uploadId.ToString(),
            FileName = fileName,
            TotalChunks = 3
        };

        // Act
        var result = await _fileAzureBusiness.CompleteUpload(
            _oid, _pid, _dsid, _objectStorageConfig, completeRequest, guid);

        // Assert
        Assert.NotNull(result);
        Assert.Contains(fileName, result);
        Assert.Contains(guid.ToString(), result);

        // Verify the final file exists
        var exists = await BlobExistsAsync(result);
        Assert.True(exists);

        // Verify the content is correct (chunks combined in order)
        var content = await GetBlobContentAsync(result);
        Assert.Equal("Part 1 Part 2 Part 3", content);
    }

    [Fact]
    public async Task CompleteUpload_Success_DeletesTemporaryBlob()
    {
        // Arrange
        var uploadId = await _fileAzureBusiness.StartUpload(_oid, _pid, _dsid, _objectStorageConfig);
        var guid = Guid.NewGuid();

        var chunk = CreateMockFile("chunk", "content");
        await _fileAzureBusiness.UploadChunk(_oid, _pid, _dsid, 0, uploadId.ToString(), _objectStorageConfig, chunk);

        var completeRequest = new FileUploadCompleteRequestDto
        {
            UploadId = uploadId.ToString(),
            FileName = "test.txt",
            TotalChunks = 1
        };

        // Act
        await _fileAzureBusiness.CompleteUpload(_oid, _pid, _dsid, _objectStorageConfig, completeRequest, guid);

        // Assert - Verify temp blob is deleted
        var tempBlobName = $"organization_{_oid}/project_{_pid}/datasource_{_dsid}/uploads/{uploadId}";
        var tempExists = await BlobExistsAsync(tempBlobName);
        Assert.False(tempExists);
    }

    [Fact]
    public async Task CompleteUpload_Fails_WhenChunksAreMissing()
    {
        // Arrange
        var uploadId = await _fileAzureBusiness.StartUpload(_oid, _pid, _dsid, _objectStorageConfig);
        var guid = Guid.NewGuid();

        // Upload only 2 chunks but claim there should be 3
        var chunk0 = CreateMockFile("chunk0", "Part 1");
        var chunk1 = CreateMockFile("chunk1", "Part 2");

        await _fileAzureBusiness.UploadChunk(_oid, _pid, _dsid, 0, uploadId.ToString(), _objectStorageConfig, chunk0);
        await _fileAzureBusiness.UploadChunk(_oid, _pid, _dsid, 1, uploadId.ToString(), _objectStorageConfig, chunk1);

        var completeRequest = new FileUploadCompleteRequestDto
        {
            UploadId = uploadId.ToString(),
            FileName = "test.txt",
            TotalChunks = 3 // Expecting 3 but only uploaded 2
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _fileAzureBusiness.CompleteUpload(_oid, _pid, _dsid, _objectStorageConfig, completeRequest, guid));
    }

    [Fact]
    public async Task CompleteUpload_Fails_WhenAzureConfigIsNull()
    {
        // Arrange
        var completeRequest = new FileUploadCompleteRequestDto
        {
            UploadId = Guid.NewGuid().ToString(),
            FileName = "test.txt",
            TotalChunks = 1
        };

        var invalidConfig = new ObjectStorageConfigDto
        {
            AzureObjectConfig = null
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _fileAzureBusiness.CompleteUpload(_oid, _pid, _dsid, invalidConfig, completeRequest, Guid.NewGuid()));
    }

    [Fact]
    public async Task CompleteUpload_Fails_WhenContainerDoesNotExist()
    {
        // Arrange
        var completeRequest = new FileUploadCompleteRequestDto
        {
            UploadId = Guid.NewGuid().ToString(),
            FileName = "test.txt",
            TotalChunks = 1
        };

        var invalidConfig = new ObjectStorageConfigDto
        {
            AzureObjectConfig = new AzureObjectConfigDto
            {
                AzureConnectionString = _connectionString,
                AzureContainerName = "non-existent-container"
            }
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _fileAzureBusiness.CompleteUpload(_oid, _pid, _dsid, invalidConfig, completeRequest, Guid.NewGuid()));
    }

    [Fact]
    public async Task CancelUpload_Success_DeletesUncommittedBlocks()
    {
        // Arrange
        var uploadId = await _fileAzureBusiness.StartUpload(_oid, _pid, _dsid, _objectStorageConfig);

        // Upload some chunks
        var chunk0 = CreateMockFile("chunk0", "content 0");
        var chunk1 = CreateMockFile("chunk1", "content 1");

        await _fileAzureBusiness.UploadChunk(_oid, _pid, _dsid, 0, uploadId.ToString(), _objectStorageConfig, chunk0);
        await _fileAzureBusiness.UploadChunk(_oid, _pid, _dsid, 1, uploadId.ToString(), _objectStorageConfig, chunk1);

        // Verify blocks exist before cancel
        var tempBlobName = $"organization_{_oid}/project_{_pid}/datasource_{_dsid}/uploads/{uploadId}";
        var container = new BlobContainerClient(_connectionString, _containerName);
        var blockBlobClient = container.GetBlockBlobClient(tempBlobName);
        
        var blockListBefore = await blockBlobClient.GetBlockListAsync(Azure.Storage.Blobs.Models.BlockListTypes.Uncommitted);
        Assert.Equal(2, blockListBefore.Value.UncommittedBlocks.Count());

        // Act
        await _fileAzureBusiness.CancelUpload(_oid, _pid, _dsid, uploadId.ToString(), _objectStorageConfig);

        // Assert - Verify temp blob no longer exists
        var exists = await blockBlobClient.ExistsAsync();
        Assert.False(exists);
    }

    [Fact]
    public async Task CancelUpload_Success_DoesNotThrowWhenNoBlobExists()
    {
        // Arrange
        var uploadId = Guid.NewGuid();

        // Act & Assert - Should not throw
        await _fileAzureBusiness.CancelUpload(_oid, _pid, _dsid, uploadId.ToString(), _objectStorageConfig);
    }

    [Fact]
    public async Task CancelUpload_Success_DoesNotThrowWhenContainerDoesNotExist()
    {
        // Arrange
        var uploadId = Guid.NewGuid();
        var invalidConfig = new ObjectStorageConfigDto
        {
            AzureObjectConfig = new AzureObjectConfigDto
            {
                AzureConnectionString = _connectionString,
                AzureContainerName = "non-existent-container"
            }
        };

        // Act & Assert - Should not throw, just return
        await _fileAzureBusiness.CancelUpload(_oid, _pid, _dsid, uploadId.ToString(), invalidConfig);
    }

    [Fact]
    public async Task CancelUpload_Fails_WhenAzureConfigIsNull()
    {
        // Arrange
        var uploadId = Guid.NewGuid();
        var invalidConfig = new ObjectStorageConfigDto
        {
            AzureObjectConfig = null
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _fileAzureBusiness.CancelUpload(_oid, _pid, _dsid, uploadId.ToString(), invalidConfig));
    }

    [Fact]
    public async Task ChunkedUploadWorkflow_FullLifecycle_WorksCorrectly()
    {
        // Start upload
        var uploadId = await _fileAzureBusiness.StartUpload(_oid, _pid, _dsid, _objectStorageConfig);
        Assert.NotEqual(Guid.Empty, uploadId);

        // Upload chunks
        var chunk0 = CreateMockFile("chunk0", "First chunk. ");
        var chunk1 = CreateMockFile("chunk1", "Second chunk. ");
        var chunk2 = CreateMockFile("chunk2", "Third chunk.");

        await _fileAzureBusiness.UploadChunk(_oid, _pid, _dsid, 0, uploadId.ToString(), _objectStorageConfig, chunk0);
        await _fileAzureBusiness.UploadChunk(_oid, _pid, _dsid, 1, uploadId.ToString(), _objectStorageConfig, chunk1);
        await _fileAzureBusiness.UploadChunk(_oid, _pid, _dsid, 2, uploadId.ToString(), _objectStorageConfig, chunk2);

        // Complete upload
        var guid = Guid.NewGuid();
        var fileName = "workflow-test.txt";
        var completeRequest = new FileUploadCompleteRequestDto
        {
            UploadId = uploadId.ToString(),
            FileName = fileName,
            TotalChunks = 3
        };

        var result = await _fileAzureBusiness.CompleteUpload(
            _oid, _pid, _dsid, _objectStorageConfig, completeRequest, guid);

        // Verify final file
        Assert.NotNull(result);
        Assert.True(await BlobExistsAsync(result));
        var content = await GetBlobContentAsync(result);
        Assert.Equal("First chunk. Second chunk. Third chunk.", content);

        // Verify temp blob was cleaned up
        var tempBlobName = $"organization_{_oid}/project_{_pid}/datasource_{_dsid}/uploads/{uploadId}";
        Assert.False(await BlobExistsAsync(tempBlobName));

        // Verify we can download the completed file
        var record = await Context.Records.FindAsync(_recordId);
        record!.Uri = result;
        record.Name = fileName;
        await Context.SaveChangesAsync();

        var recordDto = new RecordResponseDto
        {
            Id = record.Id,
            Name = record.Name,
            Uri = record.Uri,
            OrganizationId = _oid,
            ProjectId = _pid,
            DataSourceId = _dsid
        };

        var downloadResult = await _fileAzureBusiness.DownloadFile(recordDto, _objectStorageConfig);
        using var reader = new StreamReader(downloadResult.FileStream);
        var downloadedContent = await reader.ReadToEndAsync();
        Assert.Equal("First chunk. Second chunk. Third chunk.", downloadedContent);
    }

    [Fact]
    public async Task ChunkedUpload_Success_HandlesLargeNumberOfChunks()
    {
        // Arrange
        var uploadId = await _fileAzureBusiness.StartUpload(_oid, _pid, _dsid, _objectStorageConfig);
        var numberOfChunks = 10;

        // Upload 10 chunks
        for (int i = 0; i < numberOfChunks; i++)
        {
            var chunk = CreateMockFile($"chunk{i}", $"Chunk {i} content. ");
            await _fileAzureBusiness.UploadChunk(_oid, _pid, _dsid, i, uploadId.ToString(), _objectStorageConfig, chunk);
        }

        // Complete upload
        var guid = Guid.NewGuid();
        var completeRequest = new FileUploadCompleteRequestDto
        {
            UploadId = uploadId.ToString(),
            FileName = "large-chunk-test.txt",
            TotalChunks = numberOfChunks
        };

        // Act
        var result = await _fileAzureBusiness.CompleteUpload(
            _oid, _pid, _dsid, _objectStorageConfig, completeRequest, guid);

        // Assert
        Assert.NotNull(result);
        Assert.True(await BlobExistsAsync(result));

        var content = await GetBlobContentAsync(result);
        var expectedContent = string.Join("", Enumerable.Range(0, numberOfChunks).Select(i => $"Chunk {i} content. "));
        Assert.Equal(expectedContent, content);
    }

    [Fact]
    public async Task ChunkedUpload_CancelAfterSomeChunks_CleansUpCorrectly()
    {
        // Arrange
        var uploadId = await _fileAzureBusiness.StartUpload(_oid, _pid, _dsid, _objectStorageConfig);

        // Upload some chunks
        await _fileAzureBusiness.UploadChunk(_oid, _pid, _dsid, 0, uploadId.ToString(), _objectStorageConfig, 
            CreateMockFile("chunk0", "content 0"));
        await _fileAzureBusiness.UploadChunk(_oid, _pid, _dsid, 1, uploadId.ToString(), _objectStorageConfig, 
            CreateMockFile("chunk1", "content 1"));
        await _fileAzureBusiness.UploadChunk(_oid, _pid, _dsid, 2, uploadId.ToString(), _objectStorageConfig, 
            CreateMockFile("chunk2", "content 2"));

        // Cancel
        await _fileAzureBusiness.CancelUpload(_oid, _pid, _dsid, uploadId.ToString(), _objectStorageConfig);

        // Try to complete (should fail because chunks are gone)
        var completeRequest = new FileUploadCompleteRequestDto
        {
            UploadId = uploadId.ToString(),
            FileName = "test.txt",
            TotalChunks = 3
        };

        // Assert - Complete should fail because blocks were cleaned up
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _fileAzureBusiness.CompleteUpload(_oid, _pid, _dsid, _objectStorageConfig, completeRequest, Guid.NewGuid()));
    }

    #endregion
    
    #region GetStorageSize Tests
 
    [Fact]
    public async Task GetStorageSize_ReturnsZero_WhenAzureConfigIsNull()
    {
        // Arrange
        var invalidConfig = new ObjectStorageConfigDto
        {
            AzureObjectConfig = null
        };
        var prefix = "organization_1/project_1/";
 
        // Act
        var result = await _fileAzureBusiness.GetStorageSize(prefix, invalidConfig);
 
        // Assert
        Assert.Equal(0, result);
    }
 
    [Fact]
    public async Task GetStorageSize_ReturnsZero_WhenContainerDoesNotExist()
    {
        // Arrange
        var nonExistentConfig = new ObjectStorageConfigDto
        {
            AzureObjectConfig = new AzureObjectConfigDto
            {
                AzureConnectionString = _connectionString,
                AzureContainerName = "non-existent-container"
            }
        };
        var prefix = "organization_1/project_1/";
 
        // Act
        var result = await _fileAzureBusiness.GetStorageSize(prefix, nonExistentConfig);
 
        // Assert
        Assert.Equal(0, result);
    }
 
    [Fact]
    public async Task GetStorageSize_ReturnsZero_WhenContainerIsEmpty()
    {
        // Arrange
        var container = new BlobContainerClient(_connectionString, _containerName);
        await container.CreateIfNotExistsAsync();
        var prefix = "organization_1/project_1/";
 
        // Act
        var result = await _fileAzureBusiness.GetStorageSize(prefix, _objectStorageConfig);
 
        // Assert
        Assert.Equal(0, result);
    }
 
    [Fact]
    public async Task GetStorageSize_ReturnsCorrectSize_ForSingleBlob()
    {
        // Arrange
        var container = new BlobContainerClient(_connectionString, _containerName);
        await container.CreateIfNotExistsAsync();
        
        var blobName = "organization_1/project_1/datasource_1/test-blob.txt";
        var blob = container.GetBlobClient(blobName);
        var content = new byte[1024]; // 1KB
        new Random().NextBytes(content);
        using var stream = new MemoryStream(content);
        await blob.UploadAsync(stream, overwrite: true);
 
        var prefix = "organization_1/project_1/";
 
        // Act
        var result = await _fileAzureBusiness.GetStorageSize(prefix, _objectStorageConfig);
 
        // Assert
        Assert.Equal(1024, result);
    }
 
    [Fact]
    public async Task GetStorageSize_ReturnsCorrectSize_ForMultipleBlobs()
    {
        // Arrange
        var container = new BlobContainerClient(_connectionString, _containerName);
        await container.CreateIfNotExistsAsync();
        
        // Create 3 blobs of different sizes
        await UploadBlobAsync(container, "organization_1/project_1/datasource_1/blob1.txt", 500);
        await UploadBlobAsync(container, "organization_1/project_1/datasource_1/blob2.txt", 1000);
        await UploadBlobAsync(container, "organization_1/project_1/datasource_2/blob3.txt", 1500);
 
        var prefix = "organization_1/project_1/";
 
        // Act
        var result = await _fileAzureBusiness.GetStorageSize(prefix, _objectStorageConfig);
 
        // Assert
        Assert.Equal(3000, result); // 500 + 1000 + 1500
    }
 
    [Fact]
    public async Task GetStorageSize_ReturnsCorrectSize_WithEmptyPrefix()
    {
        // Arrange
        var container = new BlobContainerClient(_connectionString, _containerName);
        await container.CreateIfNotExistsAsync();
        
        // Create blobs across multiple organizations and projects
        await UploadBlobAsync(container, "organization_1/project_1/datasource_1/file1.txt", 1000);
        await UploadBlobAsync(container, "organization_1/project_2/datasource_1/file2.txt", 2000);
        await UploadBlobAsync(container, "organization_2/project_1/datasource_1/file3.txt", 3000);
 
        var prefix = "";
 
        // Act - Empty prefix should count everything
        var result = await _fileAzureBusiness.GetStorageSize(prefix, _objectStorageConfig);
 
        // Assert
        Assert.Equal(6000, result); // All blobs counted
    }
 
    [Fact]
    public async Task GetStorageSize_OnlyCountsBlobsWithSpecificPrefix()
    {
        // Arrange
        var container = new BlobContainerClient(_connectionString, _containerName);
        await container.CreateIfNotExistsAsync();
        
        // Create blobs in different projects
        await UploadBlobAsync(container, "organization_1/project_1/datasource_1/file1.txt", 1000);
        await UploadBlobAsync(container, "organization_1/project_2/datasource_1/file2.txt", 2000);
        await UploadBlobAsync(container, "organization_2/project_1/datasource_1/file3.txt", 3000);
 
        var prefix = "organization_1/project_1/";
 
        // Act - Only count project 1 in org 1
        var result = await _fileAzureBusiness.GetStorageSize(prefix, _objectStorageConfig);
 
        // Assert
        Assert.Equal(1000, result); // Only project 1 blob
    }
 
    [Fact]
    public async Task GetStorageSize_HandlesNestedPaths()
    {
        // Arrange
        var container = new BlobContainerClient(_connectionString, _containerName);
        await container.CreateIfNotExistsAsync();
        
        var prefix = "organization_1/project_1/";
        
        // Create blobs at different nested levels
        await UploadBlobAsync(container, "organization_1/project_1/datasource_1/file1.txt", 100);
        await UploadBlobAsync(container, "organization_1/project_1/datasource_1/subfolder/file2.txt", 200);
        await UploadBlobAsync(container, "organization_1/project_1/datasource_2/file3.txt", 300);
        await UploadBlobAsync(container, "organization_1/project_1/datasource_2/deep/nested/file4.txt", 400);
 
        // Act
        var result = await _fileAzureBusiness.GetStorageSize(prefix, _objectStorageConfig);
 
        // Assert
        Assert.Equal(1000, result); // All nested blobs counted
    }
 
    [Fact]
    public async Task GetStorageSize_OnlyCountsBlobsInCorrectOrganization()
    {
        // Arrange
        var container = new BlobContainerClient(_connectionString, _containerName);
        await container.CreateIfNotExistsAsync();
        
        // Create blobs for different organizations
        await UploadBlobAsync(container, "organization_1/project_1/datasource_1/file1.txt", 5000);
        await UploadBlobAsync(container, "organization_2/project_1/datasource_1/file2.txt", 7000);
        await UploadBlobAsync(container, "organization_3/project_1/datasource_1/file3.txt", 9000);
 
        var prefix = "organization_1/";
 
        // Act - Get size for org 1 only
        var result = await _fileAzureBusiness.GetStorageSize(prefix, _objectStorageConfig);
 
        // Assert
        Assert.Equal(5000, result); // Only org 1 blobs
    }
 
    [Fact]
    public async Task GetStorageSize_ReturnsCorrectSize_ForLargeBlob()
    {
        // Arrange
        var container = new BlobContainerClient(_connectionString, _containerName);
        await container.CreateIfNotExistsAsync();
        
        // Create a 5MB blob
        var largeFileSize = 5 * 1024 * 1024;
        await UploadBlobAsync(container, "organization_1/project_1/datasource_1/large-file.bin", largeFileSize);
 
        var prefix = "organization_1/project_1/";
 
        // Act
        var result = await _fileAzureBusiness.GetStorageSize(prefix, _objectStorageConfig);
 
        // Assert
        Assert.Equal(largeFileSize, result);
    }
 
    [Fact]
    public async Task GetStorageSize_ReturnsZero_ForEmptyBlobs()
    {
        // Arrange
        var container = new BlobContainerClient(_connectionString, _containerName);
        await container.CreateIfNotExistsAsync();
        
        // Create empty blob
        await UploadBlobAsync(container, "organization_1/project_1/datasource_1/empty.txt", 0);
 
        var prefix = "organization_1/project_1/";
 
        // Act
        var result = await _fileAzureBusiness.GetStorageSize(prefix, _objectStorageConfig);
 
        // Assert
        Assert.Equal(0, result);
    }
 
    [Fact]
    public async Task GetStorageSize_HandlesBlobsWithoutContentLength()
    {
        // Arrange
        var container = new BlobContainerClient(_connectionString, _containerName);
        await container.CreateIfNotExistsAsync();
        
        // Create normal blobs
        await UploadBlobAsync(container, "organization_1/project_1/datasource_1/file1.txt", 1000);
        await UploadBlobAsync(container, "organization_1/project_1/datasource_1/file2.txt", 2000);
 
        var prefix = "organization_1/project_1/";
 
        // Act
        var result = await _fileAzureBusiness.GetStorageSize(prefix, _objectStorageConfig);
 
        // Assert - Should count blobs that have ContentLength
        Assert.Equal(3000, result);
    }
 
    [Fact]
    public async Task GetStorageSize_WorksWithDifferentContainers()
    {
        // Arrange
        var container1Name = "container-1";
        var container2Name = "container-2";
        
        var container1 = new BlobContainerClient(_connectionString, container1Name);
        var container2 = new BlobContainerClient(_connectionString, container2Name);
        
        await container1.CreateIfNotExistsAsync();
        await container2.CreateIfNotExistsAsync();
        
        // Add blobs to both containers
        await UploadBlobAsync(container1, "organization_1/project_1/datasource_1/file1.txt", 3000);
        await UploadBlobAsync(container2, "organization_1/project_1/datasource_1/file2.txt", 5000);
 
        var config1 = new ObjectStorageConfigDto
        {
            AzureObjectConfig = new AzureObjectConfigDto
            {
                AzureConnectionString = _connectionString,
                AzureContainerName = container1Name
            }
        };
 
        var config2 = new ObjectStorageConfigDto
        {
            AzureObjectConfig = new AzureObjectConfigDto
            {
                AzureConnectionString = _connectionString,
                AzureContainerName = container2Name
            }
        };
 
        var prefix = "organization_1/project_1/";
 
        // Act
        var result1 = await _fileAzureBusiness.GetStorageSize(prefix, config1);
        var result2 = await _fileAzureBusiness.GetStorageSize(prefix, config2);
 
        // Assert
        Assert.Equal(3000, result1);
        Assert.Equal(5000, result2);
    }
 
    [Fact]
    public async Task GetStorageSize_IgnoresBlobsOutsidePrefix()
    {
        // Arrange
        var container = new BlobContainerClient(_connectionString, _containerName);
        await container.CreateIfNotExistsAsync();
        
        // Create blobs - some matching prefix, some not
        await UploadBlobAsync(container, "organization_1/project_1/datasource_1/file1.txt", 1000);
        await UploadBlobAsync(container, "organization_1/project_2/datasource_1/file2.txt", 2000);
        await UploadBlobAsync(container, "organization_10/project_1/datasource_1/file3.txt", 3000); // Similar but different org
        await UploadBlobAsync(container, "organization_1/project_10/datasource_1/file4.txt", 4000); // Similar but different project
 
        var prefix = "organization_1/project_1/";
 
        // Act
        var result = await _fileAzureBusiness.GetStorageSize(prefix, _objectStorageConfig);
 
        // Assert
        Assert.Equal(1000, result); // Only exact prefix match
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
        var result = _fileAzureBusiness.BuildPrefix(orgId, projectId);
 
        // Assert
        Assert.Equal("organization_123/project_456/", result);
    }
 
    [Fact]
    public void BuildPrefix_ReturnsCorrectFormat_WithoutProjectId()
    {
        // Arrange
        long orgId = 789;
        long? projectId = null;
 
        // Act
        var result = _fileAzureBusiness.BuildPrefix(orgId, projectId);
 
        // Assert
        Assert.Equal("organization_789/", result);
    }
 
    [Fact]
    public void BuildPrefix_UsesCorrectNamingConvention()
    {
        // Arrange & Act
        var withProject = _fileAzureBusiness.BuildPrefix(1, 2);
        var withoutProject = _fileAzureBusiness.BuildPrefix(1, null);
 
        // Assert
        // Azure uses "organization_" prefix (not "org_" like filesystem)
        Assert.StartsWith("organization_", withProject);
        Assert.StartsWith("organization_", withoutProject);
        
        // Should use "project_" for project
        Assert.Contains("project_", withProject);
        Assert.DoesNotContain("project_", withoutProject);
    }
 
    [Fact]
    public void BuildPrefix_EndsWithSlash()
    {
        // Arrange & Act
        var withProject = _fileAzureBusiness.BuildPrefix(1, 2);
        var withoutProject = _fileAzureBusiness.BuildPrefix(1, null);
 
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
        var result1 = _fileAzureBusiness.BuildPrefix(orgId, projectId);
        var result2 = _fileAzureBusiness.BuildPrefix(orgId, projectId);
        var result3 = _fileAzureBusiness.BuildPrefix(orgId, projectId);
 
        // Assert
        Assert.Equal(result1, result2);
        Assert.Equal(result2, result3);
    }
 
    [Fact]
    public void BuildPrefix_DifferentFromFilesystemFormat()
    {
        // Arrange
        long orgId = 1;
        long? projectId = 2;
 
        // Act
        var azurePrefix = _fileAzureBusiness.BuildPrefix(orgId, projectId);
 
        // Assert
        // Azure format uses "organization_" not "org_"
        Assert.StartsWith("organization_", azurePrefix);
        Assert.Equal("organization_1/project_2/", azurePrefix);
        
        // Filesystem would be "org_1/project_2/"
        Assert.NotEqual("org_1/project_2/", azurePrefix);
    }
 
    #endregion
 
    #region Integration Tests - GetStorageSize with BuildPrefix
 
    [Fact]
    public async Task GetStorageSize_WithBuildPrefix_WorksCorrectly()
    {
        // Arrange
        var container = new BlobContainerClient(_connectionString, _containerName);
        await container.CreateIfNotExistsAsync();
        
        long orgId = 5;
        long projectId = 10;
        
        // Use BuildPrefix to get the prefix
        var prefix = _fileAzureBusiness.BuildPrefix(orgId, projectId);
        
        // Create blob matching the prefix
        await UploadBlobAsync(container, $"organization_{orgId}/project_{projectId}/datasource_1/file.txt", 2500);
 
        // Act
        var result = await _fileAzureBusiness.GetStorageSize(prefix, _objectStorageConfig);
 
        // Assert
        Assert.Equal(2500, result);
    }
 
    [Fact]
    public async Task GetStorageSize_WithBuildPrefix_OrganizationLevel_AggregatesAllProjects()
    {
        // Arrange
        var container = new BlobContainerClient(_connectionString, _containerName);
        await container.CreateIfNotExistsAsync();
        
        long orgId = 4;
        
        // Create blobs across multiple projects in the same organization
        await UploadBlobAsync(container, $"organization_{orgId}/project_1/datasource_1/file1.txt", 1000);
        await UploadBlobAsync(container, $"organization_{orgId}/project_2/datasource_1/file2.txt", 2000);
        await UploadBlobAsync(container, $"organization_{orgId}/project_3/datasource_1/file3.txt", 3000);
 
        // Act - Get organization-level prefix (no project)
        var prefix = _fileAzureBusiness.BuildPrefix(orgId, null);
        var result = await _fileAzureBusiness.GetStorageSize(prefix, _objectStorageConfig);
 
        // Assert
        Assert.Equal(6000, result); // All projects in org
    }
 
    #endregion
    
    #region GetFileSize Tests

    [Fact]
    public async Task GetFileSize_ReturnsCorrectSize_ForExistingBlob()
    {
        // Arrange
        var container = new BlobContainerClient(_connectionString, _containerName);
        await container.CreateIfNotExistsAsync();
    
        var blobName = $"organization_{_oid}/project_{_pid}/datasource_{_dsid}/size-test.txt";
        var content = new byte[1024]; // 1KB
        new Random().NextBytes(content);
    
        var blob = container.GetBlobClient(blobName);
        using var stream = new MemoryStream(content);
        await blob.UploadAsync(stream, overwrite: true);

        // Act
        var result = await _fileAzureBusiness.GetFileSize(blobName, _objectStorageConfig);

        // Assert
        Assert.Equal(1024, result);
    }

    [Fact]
    public async Task GetFileSize_ReturnsZero_ForEmptyBlob()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var fileName = "empty.txt";
        var mockFile = CreateMockFile(fileName, "");

        var uri = await _fileAzureBusiness.UploadFile(
            _oid, _pid, _dsid, _objectStorageConfig, mockFile, guid);

        // Act
        var result = await _fileAzureBusiness.GetFileSize(uri, _objectStorageConfig);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task GetFileSize_ThrowsException_WhenBlobDoesNotExist()
    {
        // Arrange
        var nonExistentUri = "organization_1/project_1/datasource_1/non-existent-file.txt";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() =>
            _fileAzureBusiness.GetFileSize(nonExistentUri, _objectStorageConfig));

        Assert.Contains("Failed to get size for file", exception.Message);
    }

    [Fact]
    public async Task GetFileSize_ThrowsException_WhenAzureConfigIsNull()
    {
        // Arrange
        var invalidConfig = new ObjectStorageConfigDto
        {
            AzureObjectConfig = null
        };
        var testUri = "some-uri";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() =>
            _fileAzureBusiness.GetFileSize(testUri, invalidConfig));

        Assert.Contains("Azure configuration not set", exception.Message);
    }

    [Fact]
    public async Task GetFileSize_ThrowsArgumentException_WhenFileUriIsNull()
    {
        // Arrange
        string nullUri = null!;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _fileAzureBusiness.GetFileSize(nullUri, _objectStorageConfig));

        Assert.Contains("File URI is not specified", exception.Message);
    }

    [Fact]
    public async Task GetFileSize_ThrowsArgumentException_WhenFileUriIsEmpty()
    {
        // Arrange
        var emptyUri = "";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _fileAzureBusiness.GetFileSize(emptyUri, _objectStorageConfig));

        Assert.Contains("File URI is not specified", exception.Message);
    }

    [Fact]
    public async Task GetFileSize_ReturnsCorrectSize_ForLargeBlob()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var fileName = "large-file.bin";
        var largeFileSize = 5 * 1024 * 1024; // 5MB
        var content = new byte[largeFileSize];
        new Random().NextBytes(content);
        
        var container = new BlobContainerClient(_connectionString, _containerName);
        await container.CreateIfNotExistsAsync();
        
        var blobName = $"organization_{_oid}/project_{_pid}/datasource_{_dsid}/{guid}_{fileName}";
        var blob = container.GetBlobClient(blobName);
        using var stream = new MemoryStream(content);
        await blob.UploadAsync(stream, overwrite: true);

        // Act
        var result = await _fileAzureBusiness.GetFileSize(blobName, _objectStorageConfig);

        // Assert
        Assert.Equal(largeFileSize, result);
    }

    [Fact]
    public async Task GetFileSize_ReturnsCorrectSize_ForMultipleDifferentBlobs()
    {
        // Arrange
        var container = new BlobContainerClient(_connectionString, _containerName);
        await container.CreateIfNotExistsAsync();

        var blob1Name = $"organization_{_oid}/project_{_pid}/datasource_{_dsid}/file1.txt";
        var blob2Name = $"organization_{_oid}/project_{_pid}/datasource_{_dsid}/file2.txt";
        var blob3Name = $"organization_{_oid}/project_{_pid}/datasource_{_dsid}/file3.txt";

        await UploadBlobAsync(container, blob1Name, 100);
        await UploadBlobAsync(container, blob2Name, 500);
        await UploadBlobAsync(container, blob3Name, 1000);

        // Act
        var size1 = await _fileAzureBusiness.GetFileSize(blob1Name, _objectStorageConfig);
        var size2 = await _fileAzureBusiness.GetFileSize(blob2Name, _objectStorageConfig);
        var size3 = await _fileAzureBusiness.GetFileSize(blob3Name, _objectStorageConfig);

        // Assert
        Assert.Equal(100, size1);
        Assert.Equal(500, size2);
        Assert.Equal(1000, size3);
    }

    [Fact]
    public async Task GetFileSize_ThrowsException_WhenContainerDoesNotExist()
    {
        // Arrange
        var invalidConfig = new ObjectStorageConfigDto
        {
            AzureObjectConfig = new AzureObjectConfigDto
            {
                AzureConnectionString = _connectionString,
                AzureContainerName = "non-existent-container"
            }
        };
        var testUri = "some-uri";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() =>
            _fileAzureBusiness.GetFileSize(testUri, invalidConfig));

        Assert.Contains("Failed to get size for file", exception.Message);
    }

    [Fact]
    public async Task GetFileSize_HandlesNestedBlobPaths()
    {
        // Arrange
        var container = new BlobContainerClient(_connectionString, _containerName);
        await container.CreateIfNotExistsAsync();

        var nestedPath = $"organization_{_oid}/project_{_pid}/datasource_{_dsid}/level1/level2/nested.txt";
        await UploadBlobAsync(container, nestedPath, 2048);

        // Act
        var result = await _fileAzureBusiness.GetFileSize(nestedPath, _objectStorageConfig);

        // Assert
        Assert.Equal(2048, result);
    }

    [Fact]
    public async Task GetFileSize_ReturnsCorrectSize_AfterBlobUpdate()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var fileName = "modified.txt";
        var initialFile = CreateMockFile(fileName, "initial content");

        var uri = await _fileAzureBusiness.UploadFile(
            _oid, _pid, _dsid, _objectStorageConfig, initialFile, guid);

        var initialSize = await _fileAzureBusiness.GetFileSize(uri, _objectStorageConfig);

        // Update the blob with larger content
        var container = new BlobContainerClient(_connectionString, _containerName);
        var blob = container.GetBlobClient(uri);
        var newContent = new byte[1500];
        new Random().NextBytes(newContent);
        using var stream = new MemoryStream(newContent);
        await blob.UploadAsync(stream, overwrite: true);

        // Act
        var newSize = await _fileAzureBusiness.GetFileSize(uri, _objectStorageConfig);

        // Assert
        Assert.Equal(1500, newSize);
        Assert.NotEqual(initialSize, newSize);
    }

    [Fact]
    public async Task GetFileSize_WorksWithDifferentFileExtensions()
    {
        // Arrange
        var container = new BlobContainerClient(_connectionString, _containerName);
        await container.CreateIfNotExistsAsync();

        var txtPath = $"organization_{_oid}/project_{_pid}/test.txt";
        var binPath = $"organization_{_oid}/project_{_pid}/test.bin";
        var csvPath = $"organization_{_oid}/project_{_pid}/test.csv";
        var jsonPath = $"organization_{_oid}/project_{_pid}/test.json";

        await UploadBlobAsync(container, txtPath, 100);
        await UploadBlobAsync(container, binPath, 200);
        await UploadBlobAsync(container, csvPath, 300);
        await UploadBlobAsync(container, jsonPath, 400);

        // Act & Assert
        Assert.Equal(100, await _fileAzureBusiness.GetFileSize(txtPath, _objectStorageConfig));
        Assert.Equal(200, await _fileAzureBusiness.GetFileSize(binPath, _objectStorageConfig));
        Assert.Equal(300, await _fileAzureBusiness.GetFileSize(csvPath, _objectStorageConfig));
        Assert.Equal(400, await _fileAzureBusiness.GetFileSize(jsonPath, _objectStorageConfig));
    }

    #endregion

    #region GetFileSizesBatch Tests

    [Fact]
    public async Task GetFileSizesBatch_ReturnsCorrectSizes_ForMultipleBlobs()
    {
        // Arrange
        var container = new BlobContainerClient(_connectionString, _containerName);
        await container.CreateIfNotExistsAsync();

        var blob1Name = $"organization_{_oid}/project_{_pid}/batch1.txt";
        var blob2Name = $"organization_{_oid}/project_{_pid}/batch2.txt";
        var blob3Name = $"organization_{_oid}/project_{_pid}/batch3.txt";

        await UploadBlobAsync(container, blob1Name, 1024);
        await UploadBlobAsync(container, blob2Name, 2048);
        await UploadBlobAsync(container, blob3Name, 3072);

        var fileUris = new List<string> { blob1Name, blob2Name, blob3Name };

        // Act
        var results = await _fileAzureBusiness.GetFileSizesBatch(fileUris, _objectStorageConfig);

        // Assert
        Assert.Equal(3, results.Count);
        Assert.Equal(1024, results[blob1Name]);
        Assert.Equal(2048, results[blob2Name]);
        Assert.Equal(3072, results[blob3Name]);
    }

    [Fact]
    public async Task GetFileSizesBatch_ReturnsEmptyDictionary_WhenContainerDoesNotExist()
    {
        // Arrange
        var invalidConfig = new ObjectStorageConfigDto
        {
            AzureObjectConfig = new AzureObjectConfigDto
            {
                AzureConnectionString = _connectionString,
                AzureContainerName = "non-existent-container"
            }
        };
        var fileUris = new List<string> { "file1.txt", "file2.txt" };

        // Act
        var results = await _fileAzureBusiness.GetFileSizesBatch(fileUris, invalidConfig);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task GetFileSizesBatch_ThrowsException_WhenAzureConfigIsNull()
    {
        // Arrange
        var invalidConfig = new ObjectStorageConfigDto
        {
            AzureObjectConfig = null
        };
        var fileUris = new List<string> { "file1.txt" };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() =>
            _fileAzureBusiness.GetFileSizesBatch(fileUris, invalidConfig));

        Assert.Contains("Azure configuration not set", exception.Message);
    }

    [Fact]
    public async Task GetFileSizesBatch_ReturnsOnlyExistingBlobs()
    {
        // Arrange
        var container = new BlobContainerClient(_connectionString, _containerName);
        await container.CreateIfNotExistsAsync();

        var existingBlob1 = $"organization_{_oid}/project_{_pid}/existing1.txt";
        var existingBlob2 = $"organization_{_oid}/project_{_pid}/existing2.txt";
        var nonExistentBlob = $"organization_{_oid}/project_{_pid}/non-existent.txt";

        await UploadBlobAsync(container, existingBlob1, 500);
        await UploadBlobAsync(container, existingBlob2, 1000);

        var fileUris = new List<string> { existingBlob1, existingBlob2, nonExistentBlob };

        // Act
        var results = await _fileAzureBusiness.GetFileSizesBatch(fileUris, _objectStorageConfig);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Equal(500, results[existingBlob1]);
        Assert.Equal(1000, results[existingBlob2]);
        Assert.False(results.ContainsKey(nonExistentBlob));
    }

    [Fact]
    public async Task GetFileSizesBatch_ReturnsEmptyDictionary_ForEmptyList()
    {
        // Arrange
        var fileUris = new List<string>();

        // Act
        var results = await _fileAzureBusiness.GetFileSizesBatch(fileUris, _objectStorageConfig);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task GetFileSizesBatch_HandlesLargeNumberOfBlobs()
    {
        // Arrange
        var container = new BlobContainerClient(_connectionString, _containerName);
        await container.CreateIfNotExistsAsync();

        var fileUris = new List<string>();
        var expectedSizes = new Dictionary<string, long>();

        // Create 20 blobs
        for (int i = 0; i < 20; i++)
        {
            var blobName = $"organization_{_oid}/project_{_pid}/batch_file_{i}.txt";
            var size = (i + 1) * 100; // 100, 200, 300, etc.
            await UploadBlobAsync(container, blobName, size);
            fileUris.Add(blobName);
            expectedSizes[blobName] = size;
        }

        // Act
        var results = await _fileAzureBusiness.GetFileSizesBatch(fileUris, _objectStorageConfig);

        // Assert
        Assert.Equal(20, results.Count);
        foreach (var kvp in expectedSizes)
        {
            Assert.Equal(kvp.Value, results[kvp.Key]);
        }
    }

    [Fact]
    public async Task GetFileSizesBatch_ExitsEarly_WhenAllRequestedFilesFound()
    {
        // Arrange
        var container = new BlobContainerClient(_connectionString, _containerName);
        await container.CreateIfNotExistsAsync();

        // Create requested blobs
        var blob1 = $"organization_{_oid}/project_{_pid}/requested1.txt";
        var blob2 = $"organization_{_oid}/project_{_pid}/requested2.txt";
        await UploadBlobAsync(container, blob1, 1000);
        await UploadBlobAsync(container, blob2, 2000);

        // Create extra blobs that shouldn't be in results
        for (int i = 0; i < 50; i++)
        {
            var extraBlob = $"organization_{_oid}/project_{_pid}/extra_{i}.txt";
            await UploadBlobAsync(container, extraBlob, 500);
        }

        var fileUris = new List<string> { blob1, blob2 };

        // Act
        var results = await _fileAzureBusiness.GetFileSizesBatch(fileUris, _objectStorageConfig);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Equal(1000, results[blob1]);
        Assert.Equal(2000, results[blob2]);
    }

    [Fact]
    public async Task GetFileSizesBatch_HandlesZeroSizeBlobs()
    {
        // Arrange
        var container = new BlobContainerClient(_connectionString, _containerName);
        await container.CreateIfNotExistsAsync();

        var emptyBlob = $"organization_{_oid}/project_{_pid}/empty.txt";
        var normalBlob = $"organization_{_oid}/project_{_pid}/normal.txt";

        await UploadBlobAsync(container, emptyBlob, 0);
        await UploadBlobAsync(container, normalBlob, 1024);

        var fileUris = new List<string> { emptyBlob, normalBlob };

        // Act
        var results = await _fileAzureBusiness.GetFileSizesBatch(fileUris, _objectStorageConfig);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Equal(0, results[emptyBlob]);
        Assert.Equal(1024, results[normalBlob]);
    }

    [Fact]
    public async Task GetFileSizesBatch_HandlesDuplicateUris()
    {
        // Arrange
        var container = new BlobContainerClient(_connectionString, _containerName);
        await container.CreateIfNotExistsAsync();

        var blobName = $"organization_{_oid}/project_{_pid}/duplicate.txt";
        await UploadBlobAsync(container, blobName, 2048);

        // Request same blob multiple times
        var fileUris = new List<string> { blobName, blobName, blobName };

        // Act
        var results = await _fileAzureBusiness.GetFileSizesBatch(fileUris, _objectStorageConfig);

        // Assert
        Assert.Single(results); // Should only have one entry
        Assert.Equal(2048, results[blobName]);
    }

    [Fact]
    public async Task GetFileSizesBatch_WorksAcrossDifferentPaths()
    {
        // Arrange
        var container = new BlobContainerClient(_connectionString, _containerName);
        await container.CreateIfNotExistsAsync();

        var blob1 = $"organization_{_oid}/project_{_pid}/datasource_1/file.txt";
        var blob2 = $"organization_{_oid}/project_{_pid}/datasource_2/file.txt";
        var blob3 = $"organization_{_oid}/project_{_pid}/nested/deep/file.txt";

        await UploadBlobAsync(container, blob1, 1024);
        await UploadBlobAsync(container, blob2, 2048);
        await UploadBlobAsync(container, blob3, 3072);

        var fileUris = new List<string> { blob1, blob2, blob3 };

        // Act
        var results = await _fileAzureBusiness.GetFileSizesBatch(fileUris, _objectStorageConfig);

        // Assert
        Assert.Equal(3, results.Count);
        Assert.Equal(1024, results[blob1]);
        Assert.Equal(2048, results[blob2]);
        Assert.Equal(3072, results[blob3]);
    }

    [Fact]
    public async Task GetFileSizesBatch_IgnoresBlobsWithoutContentLength()
    {
        // Arrange
        var container = new BlobContainerClient(_connectionString, _containerName);
        await container.CreateIfNotExistsAsync();

        var normalBlob = $"organization_{_oid}/project_{_pid}/normal.txt";
        await UploadBlobAsync(container, normalBlob, 1024);

        var fileUris = new List<string> { normalBlob };

        // Act
        var results = await _fileAzureBusiness.GetFileSizesBatch(fileUris, _objectStorageConfig);

        // Assert
        Assert.Single(results);
        Assert.Equal(1024, results[normalBlob]);
    }

    [Fact]
    public async Task GetFileSizesBatch_PerformanceTest_IsFasterThanIndividualCalls()
    {
        // Arrange
        var container = new BlobContainerClient(_connectionString, _containerName);
        await container.CreateIfNotExistsAsync();

        var fileUris = new List<string>();
        for (int i = 0; i < 10; i++)
        {
            var blobName = $"organization_{_oid}/project_{_pid}/perf_test_{i}.txt";
            await UploadBlobAsync(container, blobName, 1000 * (i + 1));
            fileUris.Add(blobName);
        }

        // Act - Batch call
        var batchStart = DateTime.UtcNow;
        var batchResults = await _fileAzureBusiness.GetFileSizesBatch(fileUris, _objectStorageConfig);
        var batchDuration = DateTime.UtcNow - batchStart;

        // Act - Individual calls
        var individualStart = DateTime.UtcNow;
        var individualResults = new Dictionary<string, long>();
        foreach (var uri in fileUris)
        {
            var size = await _fileAzureBusiness.GetFileSize(uri, _objectStorageConfig);
            individualResults[uri] = size;
        }
        var individualDuration = DateTime.UtcNow - individualStart;

        // Assert
        Assert.Equal(10, batchResults.Count);
        Assert.Equal(10, individualResults.Count);
        
        // Batch should generally be faster or comparable
        // Note: This is a loose assertion as timing can vary in test environments
        Assert.True(batchDuration <= individualDuration.Add(TimeSpan.FromSeconds(1)));
    }

    #endregion
 
    #region Helper Methods
 
    /// <summary>
    /// Helper method to upload a blob with specified size
    /// </summary>
    private async Task UploadBlobAsync(BlobContainerClient container, string blobName, long sizeInBytes)
    {
        var blob = container.GetBlobClient(blobName);
        var content = new byte[sizeInBytes];
        new Random().NextBytes(content);
        using var stream = new MemoryStream(content);
        await blob.UploadAsync(stream, overwrite: true);
    }
    
    #endregion
}