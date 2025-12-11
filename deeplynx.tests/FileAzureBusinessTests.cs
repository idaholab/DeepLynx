using Azure.Storage.Blobs;
using deeplynx.business;
using deeplynx.datalayer.Models;
using deeplynx.models;
using Microsoft.AspNetCore.Http;
using Record = deeplynx.datalayer.Models.Record;

namespace deeplynx.tests;

[Collection("Test Suite Collection")]
public class FileAzureBusinessTests : IntegrationTestBase
{
    private FileAzureBusiness _fileAzureBusiness = null!;
    private string _connectionString = null!;
    private string _containerName = "test-container";
    private ObjectStorageConfigDto _objectStorageConfig = null!;

    // Test data IDs
    private long _oid;
    private long _pid;
    private long _dsid;
    private long _uid;
    private long _recordId;

    public FileAzureBusinessTests(TestSuiteFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        _connectionString = _fixture.AzuriteConnectionString;

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
    
    // We don't really need this test since we are generating a new guid every time in our controller
    [Fact]
    public async Task UploadFile_Success_OverwritesExistingFile()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var fileName = "overwrite-test.txt";
        var originalContent = "Original content";
        var newContent = "New content";

        var originalFile = CreateMockFile(fileName, originalContent);
        
        // Upload original file
        var oldResult = await _fileAzureBusiness.UploadFile(
            _oid, _pid, _dsid, _objectStorageConfig, originalFile, guid);

        var newFile = CreateMockFile(fileName, newContent);

        // Act - Upload same file with new content
        var result = await _fileAzureBusiness.UploadFile(
            _oid, _pid, _dsid, _objectStorageConfig, newFile, guid);

        // Assert
        var storedContent = await GetBlobContentAsync(result);
        Assert.Equal(newContent, storedContent);
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
    
    // Don't need this test either
    [Fact]
    public async Task UploadFile_Success_HandlesSpecialCharactersInFileName()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var fileName = "test file with spaces & special!chars.txt";
        var mockFile = CreateMockFile(fileName, "content");

        // Act
        var result = await _fileAzureBusiness.UploadFile(
            _oid, _pid, _dsid, _objectStorageConfig, mockFile, guid);

        // Assert
        Assert.NotNull(result);
        var exists = await BlobExistsAsync(result);
        Assert.True(exists);
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
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _fileAzureBusiness.UpdateFile(recordDto, invalidConfig, mockFile, Guid.NewGuid()));
    }

    [Fact]
    public async Task UpdateFile_Fails_WhenOldFileDoesNotExist()
    {
        // Arrange
        var recordDto = new RecordResponseDto
        {
            Uri = "non-existent-file.txt",
            OrganizationId = _oid,
            ProjectId = _pid,
            DataSourceId = _dsid
        };
        var mockFile = CreateMockFile("test.txt", "content");

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _fileAzureBusiness.UpdateFile(recordDto, _objectStorageConfig, mockFile, Guid.NewGuid()));
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
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _fileAzureBusiness.DownloadFile(recordDto, invalidConfig));
    }

    [Fact]
    public async Task DownloadFile_Fails_WhenFileDoesNotExist()
    {
        // Arrange
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
            _fileAzureBusiness.DownloadFile(recordDto, _objectStorageConfig));
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
}