using deeplynx.api.Controllers;
using deeplynx.interfaces;
using deeplynx.helpers.Context;
using deeplynx.models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace deeplynx.tests.Controllers;

// NOTE: FileBusiness is a concrete class (no IFileBusiness-shaped interface covers
// the controller's dependency). Moq can only intercept members that are virtual
// (or interface members). If FileBusiness's public methods are not declared
// `virtual`, every _mockFileBusiness.Setup(...) call below will fail at runtime
// with a Moq NotSupportedException. If that happens, the fix is either:
//   1. Extract an interface (e.g. IFileBusiness2 / IUploadFileBusiness) that
//      FileController depends on instead of the concrete class, or
//   2. Mark FileBusiness's public methods virtual.
// These tests assume one of those has been (or will be) done.
[Collection("Test Suite Collection")]
public class FileControllerTests : IDisposable
{
    private readonly Mock<IFileControllerBusiness> _mockFileBusiness;
    private readonly Mock<ILogger<FileController>> _mockLogger;
    private readonly FileController _fileController;

    private const long UserId = 10L;
    private const long OrgId = 1L;
    private const long ProjectId = 2L;
    private const long DataSourceId = 20L;
    private const long ObjectStorageId = 30L;
    private const long RecordId = 7L;
    private const string UploadId = "upload-abc-123";
    private const string UserJwt = "test-jwt-token";

    public FileControllerTests()
    {
        _mockLogger = new Mock<ILogger<FileController>>();
        _mockFileBusiness = new Mock<IFileControllerBusiness>();

        _fileController = new FileController(
            _mockFileBusiness.Object,
            _mockLogger.Object
        );

        UserContextStorage.UserId = UserId;
        UserContextStorage.Token = UserJwt;
        UserContextStorage.IsSysAdmin = false;
        UserContextStorage.IsOrgAdmin = false;
        UserContextStorage.IsProjectAdmin = false;
    }

    public void Dispose()
    {
        UserContextStorage.UserId = default;
        UserContextStorage.OrganizationId = default;
        UserContextStorage.IsSysAdmin = default;
        UserContextStorage.IsOrgAdmin = default;
        UserContextStorage.IsProjectAdmin = default;
        UserContextStorage.Token = default;
    }

    private static IFormFile CreateMockFormFile(string fileName = "test.pdf", string content = "file content")
    {
        var mockFile = new Mock<IFormFile>();
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);

        mockFile.Setup(f => f.FileName).Returns(fileName);
        mockFile.Setup(f => f.Length).Returns(bytes.Length);
        mockFile.Setup(f => f.OpenReadStream()).Returns(stream);

        return mockFile.Object;
    }

    // =========================================================================
    // UploadFile Tests
    // =========================================================================

    #region UploadFile Tests

    [Fact]
    public async Task UploadFile_Returns200_WithRecord()
    {
        // Arrange
        var file = CreateMockFormFile();
        var expected = new RecordResponseDto();

        _mockFileBusiness
            .Setup(b => b.UploadFile(
                UserId, OrgId, ProjectId, DataSourceId, ObjectStorageId,
                file, null, null, false, null, null, UserJwt, false, false, false))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _fileController.UploadFile(
            OrgId, ProjectId, DataSourceId, ObjectStorageId, file, null, null);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);

        _mockFileBusiness.Verify(
            b => b.UploadFile(
                UserId, OrgId, ProjectId, DataSourceId, ObjectStorageId,
                file, null, null, false, null, null, UserJwt, false, false, false),
            Times.Once);
    }

    [Fact]
    public async Task UploadFile_Returns500_OnUnexpectedException()
    {
        // Arrange
        var file = CreateMockFormFile();

        _mockFileBusiness
            .Setup(b => b.UploadFile(
                It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long?>(),
                It.IsAny<IFormFile>(), It.IsAny<List<long>?>(), It.IsAny<IFormFile?>(), It.IsAny<bool>(),
                It.IsAny<long?>(), It.IsAny<long?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ThrowsAsync(new Exception("upload error"));

        // Act
        var actionResult = await _fileController.UploadFile(
            OrgId, ProjectId, DataSourceId, ObjectStorageId, file, null, null);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);

        var message = Assert.IsType<string>(result.Value);
        Assert.Contains($"An error occurred while uploading file {file.FileName}", message);
        Assert.Contains("upload error", message);
    }

    [Fact]
    public async Task UploadFile_PassesUserContextAndArgumentsToBusinessLayer()
    {
        // Arrange
        var file = CreateMockFormFile();
        var labelIds = new List<long> { 1L, 2L };
        const bool embed = true;
        const long vlmConfigId = 100L;
        const long embeddingModelConfigId = 200L;

        UserContextStorage.UserId = UserId;
        UserContextStorage.Token = UserJwt;
        UserContextStorage.IsSysAdmin = true;
        UserContextStorage.IsOrgAdmin = true;
        UserContextStorage.IsProjectAdmin = true;

        var expected = new RecordResponseDto();

        _mockFileBusiness
            .Setup(b => b.UploadFile(
                UserId, OrgId, ProjectId, DataSourceId, ObjectStorageId,
                file, labelIds, null, embed, vlmConfigId, embeddingModelConfigId, UserJwt, true, true, true))
            .ReturnsAsync(expected);

        // Act
        await _fileController.UploadFile(
            OrgId, ProjectId, DataSourceId, ObjectStorageId, file, labelIds, null,
            embed, vlmConfigId, embeddingModelConfigId);

        // Assert
        _mockFileBusiness.Verify(
            b => b.UploadFile(
                UserId, OrgId, ProjectId, DataSourceId, ObjectStorageId,
                file, labelIds, null, embed, vlmConfigId, embeddingModelConfigId, UserJwt, true, true, true),
            Times.Once);
    }

    [Fact]
    public void UploadFile_HasHttpPost()
    {
        var method = GetControllerMethod(
            nameof(FileController.UploadFile),
            "organizationId", "projectId", "dataSourceId", "objectStorageId", "file");

        AssertHasHttpAttribute(method, nameof(HttpPostAttribute));
    }

    #endregion

    // =========================================================================
    // UpdateFile Tests
    // =========================================================================

    #region UpdateFile Tests

    [Fact]
    public async Task UpdateFile_Returns200_WithRecord()
    {
        // Arrange
        var file = CreateMockFormFile();
        var expected = new RecordResponseDto();

        _mockFileBusiness
            .Setup(b => b.UpdateFile(UserId, OrgId, ProjectId, RecordId, file, null, null, UserJwt))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _fileController.UpdateFile(OrgId, ProjectId, RecordId, file, null, null);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);

        _mockFileBusiness.Verify(
            b => b.UpdateFile(UserId, OrgId, ProjectId, RecordId, file, null, null, UserJwt),
            Times.Once);
    }

    [Fact]
    public async Task UpdateFile_Returns500_OnUnexpectedException()
    {
        // Arrange
        var file = CreateMockFormFile();

        _mockFileBusiness
            .Setup(b => b.UpdateFile(
                It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                It.IsAny<IFormFile>(), It.IsAny<long?>(), It.IsAny<long?>(), It.IsAny<string?>()))
            .ThrowsAsync(new Exception("update error"));

        // Act
        var actionResult = await _fileController.UpdateFile(OrgId, ProjectId, RecordId, file, null, null);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);

        var message = Assert.IsType<string>(result.Value);
        Assert.Contains($"An error occurred while updating file in record {RecordId}", message);
        Assert.Contains("update error", message);
    }

    [Fact]
    public async Task UpdateFile_PassesArgumentsToBusinessLayer()
    {
        // Arrange
        var file = CreateMockFormFile();
        const long vlmConfigId = 100L;
        const long embeddingModelConfigId = 200L;

        var expected = new RecordResponseDto();

        _mockFileBusiness
            .Setup(b => b.UpdateFile(UserId, OrgId, ProjectId, RecordId, file, vlmConfigId, embeddingModelConfigId, UserJwt))
            .ReturnsAsync(expected);

        // Act
        await _fileController.UpdateFile(OrgId, ProjectId, RecordId, file, vlmConfigId, embeddingModelConfigId);

        // Assert
        _mockFileBusiness.Verify(
            b => b.UpdateFile(UserId, OrgId, ProjectId, RecordId, file, vlmConfigId, embeddingModelConfigId, UserJwt),
            Times.Once);
    }

    [Fact]
    public void UpdateFile_HasHttpPut()
    {
        var method = GetControllerMethod(
            nameof(FileController.UpdateFile),
            "organizationId", "projectId", "recordId", "file");

        AssertHasHttpAttribute(method, nameof(HttpPutAttribute));
    }

    #endregion

    // =========================================================================
    // DownloadAppendedFile Tests
    // =========================================================================

    #region DownloadAppendedFile Tests

    [Fact]
    public async Task DownloadAppendedFile_Returns200_WithFileStreamResult()
    {
        // Arrange
        var expected = new FileStreamResult(new MemoryStream(), "application/octet-stream");
        var cancellationToken = CancellationToken.None;

        _mockFileBusiness
            .Setup(b => b.DownloadAppendedFile(UserId, OrgId, ProjectId, RecordId, false, false, false, cancellationToken))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _fileController.DownloadAppendedFile(OrgId, ProjectId, RecordId, cancellationToken);

        // Assert
        Assert.Same(expected, actionResult);

        _mockFileBusiness.Verify(
            b => b.DownloadAppendedFile(UserId, OrgId, ProjectId, RecordId, false, false, false, cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task DownloadAppendedFile_Returns500_OnUnexpectedException()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;

        _mockFileBusiness
            .Setup(b => b.DownloadAppendedFile(
                It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("download error"));

        // Act
        var actionResult = await _fileController.DownloadAppendedFile(OrgId, ProjectId, RecordId, cancellationToken);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);

        var message = Assert.IsType<string>(result.Value);
        Assert.Contains($"An error occurred while downloading file in record {RecordId}", message);
        Assert.Contains("download error", message);
    }

    [Fact]
    public async Task DownloadAppendedFile_PassesUserContextAndArgumentsToBusinessLayer()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;

        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;
        UserContextStorage.IsOrgAdmin = true;
        UserContextStorage.IsProjectAdmin = true;

        var expected = new FileStreamResult(new MemoryStream(), "application/octet-stream");

        _mockFileBusiness
            .Setup(b => b.DownloadAppendedFile(UserId, OrgId, ProjectId, RecordId, true, true, true, cancellationToken))
            .ReturnsAsync(expected);

        // Act
        await _fileController.DownloadAppendedFile(OrgId, ProjectId, RecordId, cancellationToken);

        // Assert
        _mockFileBusiness.Verify(
            b => b.DownloadAppendedFile(UserId, OrgId, ProjectId, RecordId, true, true, true, cancellationToken),
            Times.Once);
    }

    [Fact]
    public void DownloadAppendedFile_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(FileController.DownloadAppendedFile),
            "organizationId", "projectId", "recordId", "cancellationToken");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
    }

    #endregion

    // =========================================================================
    // DownloadFile Tests
    // =========================================================================

    #region DownloadFile Tests

    [Fact]
    public async Task DownloadFile_Returns200_WithFileStreamResult()
    {
        // Arrange
        var expected = new FileStreamResult(new MemoryStream(), "application/octet-stream");

        _mockFileBusiness
            .Setup(b => b.DownloadFile(UserId, OrgId, ProjectId, RecordId, false, false, false))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _fileController.DownloadFile(OrgId, ProjectId, RecordId);

        // Assert
        Assert.Same(expected, actionResult);

        _mockFileBusiness.Verify(
            b => b.DownloadFile(UserId, OrgId, ProjectId, RecordId, false, false, false),
            Times.Once);
    }

    [Fact]
    public async Task DownloadFile_Returns500_OnUnexpectedException()
    {
        // Arrange
        _mockFileBusiness
            .Setup(b => b.DownloadFile(
                It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ThrowsAsync(new Exception("download error"));

        // Act
        var actionResult = await _fileController.DownloadFile(OrgId, ProjectId, RecordId);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);

        var message = Assert.IsType<string>(result.Value);
        Assert.Contains($"An error occurred while downloading file in record {RecordId}", message);
        Assert.Contains("download error", message);
    }

    [Fact]
    public async Task DownloadFile_PassesUserContextAndArgumentsToBusinessLayer()
    {
        // Arrange
        UserContextStorage.UserId = UserId;
        UserContextStorage.IsSysAdmin = true;
        UserContextStorage.IsOrgAdmin = true;
        UserContextStorage.IsProjectAdmin = true;

        var expected = new FileStreamResult(new MemoryStream(), "application/octet-stream");

        _mockFileBusiness
            .Setup(b => b.DownloadFile(UserId, OrgId, ProjectId, RecordId, true, true, true))
            .ReturnsAsync(expected);

        // Act
        await _fileController.DownloadFile(OrgId, ProjectId, RecordId);

        // Assert
        _mockFileBusiness.Verify(
            b => b.DownloadFile(UserId, OrgId, ProjectId, RecordId, true, true, true),
            Times.Once);
    }

    [Fact]
    public void DownloadFile_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(FileController.DownloadFile),
            "organizationId", "projectId", "recordId");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
    }

    #endregion

    // =========================================================================
    // GenerateDownloadUrl Tests
    // =========================================================================

    #region GenerateDownloadUrl Tests

    [Fact]
    public async Task GenerateDownloadUrl_Returns200_WithUrl()
    {
        // Arrange
        const string expectedUrl = "https://storage.example.com/signed-url";

        _mockFileBusiness
            .Setup(b => b.GenerateDownloadURL(UserId, OrgId, ProjectId, RecordId))
            .ReturnsAsync(expectedUrl);

        // Act
        var actionResult = await _fileController.GenerateDownloadUrl(OrgId, ProjectId, RecordId);

        // Assert
        Assert.Equal(expectedUrl, actionResult.Value);
    }

    [Fact]
    public async Task GenerateDownloadUrl_Returns500_OnUnexpectedException()
    {
        // Arrange
        _mockFileBusiness
            .Setup(b => b.GenerateDownloadURL(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>()))
            .ThrowsAsync(new Exception("url error"));

        // Act
        var actionResult = await _fileController.GenerateDownloadUrl(OrgId, ProjectId, RecordId);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);

        var message = Assert.IsType<string>(result.Value);
        Assert.Contains($"An error occurred while downloading file in record {RecordId}", message);
        Assert.Contains("url error", message);
    }

    [Fact]
    public async Task GenerateDownloadUrl_PassesArgumentsToBusinessLayer()
    {
        // Arrange
        _mockFileBusiness
            .Setup(b => b.GenerateDownloadURL(UserId, OrgId, ProjectId, RecordId))
            .ReturnsAsync("https://storage.example.com/signed-url");

        // Act
        await _fileController.GenerateDownloadUrl(OrgId, ProjectId, RecordId);

        // Assert
        _mockFileBusiness.Verify(
            b => b.GenerateDownloadURL(UserId, OrgId, ProjectId, RecordId),
            Times.Once);
    }

    [Fact]
    public void GenerateDownloadUrl_HasHttpGet()
    {
        var method = GetControllerMethod(
            nameof(FileController.GenerateDownloadUrl),
            "organizationId", "projectId", "recordId");

        AssertHasHttpAttribute(method, nameof(HttpGetAttribute));
    }

    #endregion

    // =========================================================================
    // DeleteFile Tests
    // =========================================================================

    #region DeleteFile Tests

    [Fact]
    public async Task DeleteFile_Returns200_WithMessage()
    {
        // Arrange
        _mockFileBusiness
            .Setup(b => b.DeleteFile(UserId, OrgId, ProjectId, RecordId))
            .ReturnsAsync(true);

        // Act
        var actionResult = await _fileController.DeleteFile(OrgId, ProjectId, RecordId);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(
            $"Deleted record {RecordId} and its file",
            GetMessageFromResultValue(result.Value));
    }

    [Fact]
    public async Task DeleteFile_Returns500_OnUnexpectedException()
    {
        // Arrange
        _mockFileBusiness
            .Setup(b => b.DeleteFile(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>()))
            .ThrowsAsync(new Exception("delete error"));

        // Act
        var actionResult = await _fileController.DeleteFile(OrgId, ProjectId, RecordId);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);

        var message = Assert.IsType<string>(result.Value);
        Assert.Contains($"An error occurred while deleting file in record {RecordId}", message);
        Assert.Contains("delete error", message);
    }

    [Fact]
    public async Task DeleteFile_PassesArgumentsToBusinessLayer()
    {
        // Arrange
        _mockFileBusiness
            .Setup(b => b.DeleteFile(UserId, OrgId, ProjectId, RecordId))
            .ReturnsAsync(true);

        // Act
        await _fileController.DeleteFile(OrgId, ProjectId, RecordId);

        // Assert
        _mockFileBusiness.Verify(
            b => b.DeleteFile(UserId, OrgId, ProjectId, RecordId),
            Times.Once);
    }

    [Fact]
    public void DeleteFile_HasHttpDelete()
    {
        var method = GetControllerMethod(
            nameof(FileController.DeleteFile),
            "organizationId", "projectId", "recordId");

        AssertHasHttpAttribute(method, nameof(HttpDeleteAttribute));
    }

    #endregion

    // =========================================================================
    // StartUpload Tests
    // =========================================================================

    #region StartUpload Tests

    [Fact]
    public async Task StartUpload_Returns200_WithUploadSession()
    {
        // Arrange
        var request = new FileUploadInitRequestDto { FileName = "big.zip", FileSize = 600_000_000 };
        var expected = new FileUploadSessionResponseDto
        {
            UploadId = UploadId,
            ChunkSize = 5_000_000,
            TotalChunks = 120
        };

        _mockFileBusiness
            .Setup(b => b.StartUpload(OrgId, ProjectId, DataSourceId, ObjectStorageId, request, request.Metadata))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _fileController.StartUpload(OrgId, ProjectId, DataSourceId, ObjectStorageId, request);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task StartUpload_Returns500_OnUnexpectedException()
    {
        // Arrange
        var request = new FileUploadInitRequestDto { FileName = "big.zip", FileSize = 600_000_000 };

        _mockFileBusiness
            .Setup(b => b.StartUpload(
                It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long?>(),
                It.IsAny<FileUploadInitRequestDto>(), It.IsAny<CreateRecordFileUploadRequestDto?>()))
            .ThrowsAsync(new Exception("start upload error"));

        // Act
        var actionResult = await _fileController.StartUpload(OrgId, ProjectId, DataSourceId, ObjectStorageId, request);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);

        var message = Assert.IsType<string>(result.Value);
        Assert.Contains($"An error occurred while starting upload for file {request.FileName}", message);
        Assert.Contains("start upload error", message);
    }

    [Fact]
    public async Task StartUpload_PassesArgumentsToBusinessLayer()
    {
        // Arrange
        var request = new FileUploadInitRequestDto { FileName = "big.zip", FileSize = 600_000_000 };
        var expected = new FileUploadSessionResponseDto
        {
            UploadId = UploadId,
            ChunkSize = 5_000_000,
            TotalChunks = 120
        };

        _mockFileBusiness
            .Setup(b => b.StartUpload(OrgId, ProjectId, DataSourceId, ObjectStorageId, request, request.Metadata))
            .ReturnsAsync(expected);

        // Act
        await _fileController.StartUpload(OrgId, ProjectId, DataSourceId, ObjectStorageId, request);

        // Assert
        _mockFileBusiness.Verify(
            b => b.StartUpload(OrgId, ProjectId, DataSourceId, ObjectStorageId, request, request.Metadata),
            Times.Once);
    }

    [Fact]
    public void StartUpload_HasHttpPost()
    {
        var method = GetControllerMethod(
            nameof(FileController.StartUpload),
            "organizationId", "projectId", "dataSourceId", "objectStorageId", "request");

        AssertHasHttpAttribute(method, nameof(HttpPostAttribute));
    }

    #endregion

    // =========================================================================
    // UploadChunk Tests
    // =========================================================================

    #region UploadChunk Tests

    [Fact]
    public async Task UploadChunk_Returns200_WithChunkUploadStatus()
    {
        // Arrange
        var chunk = CreateMockFormFile("chunk0");
        const int chunkNumber = 0;
        const string expectedStatus = "success";

        _mockFileBusiness
            .Setup(b => b.UploadChunk(OrgId, ProjectId, DataSourceId, ObjectStorageId, chunk, UploadId, chunkNumber))
            .ReturnsAsync(expectedStatus);

        // Act
        var actionResult = await _fileController.UploadChunk(
            OrgId, ProjectId, DataSourceId, ObjectStorageId, chunk, UploadId, chunkNumber);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);

        var statusProperty = result.Value!.GetType().GetProperty("ChunkUploadStatus");
        Assert.NotNull(statusProperty);
        Assert.Equal(expectedStatus, statusProperty.GetValue(result.Value));
    }

    [Fact]
    public async Task UploadChunk_Returns500_OnUnexpectedException()
    {
        // Arrange
        var chunk = CreateMockFormFile("chunk0");
        const int chunkNumber = 0;

        _mockFileBusiness
            .Setup(b => b.UploadChunk(
                It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long?>(),
                It.IsAny<IFormFile>(), It.IsAny<string>(), It.IsAny<int>()))
            .ThrowsAsync(new Exception("chunk error"));

        // Act
        var actionResult = await _fileController.UploadChunk(
            OrgId, ProjectId, DataSourceId, ObjectStorageId, chunk, UploadId, chunkNumber);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);

        var message = Assert.IsType<string>(result.Value);
        Assert.Contains($"An error occurred while uploading chunk {chunkNumber} for upload {UploadId}", message);
        Assert.Contains("chunk error", message);
    }

    [Fact]
    public async Task UploadChunk_PassesArgumentsToBusinessLayer()
    {
        // Arrange
        var chunk = CreateMockFormFile("chunk0");
        const int chunkNumber = 3;

        _mockFileBusiness
            .Setup(b => b.UploadChunk(OrgId, ProjectId, DataSourceId, ObjectStorageId, chunk, UploadId, chunkNumber))
            .ReturnsAsync("success");

        // Act
        await _fileController.UploadChunk(OrgId, ProjectId, DataSourceId, ObjectStorageId, chunk, UploadId, chunkNumber);

        // Assert
        _mockFileBusiness.Verify(
            b => b.UploadChunk(OrgId, ProjectId, DataSourceId, ObjectStorageId, chunk, UploadId, chunkNumber),
            Times.Once);
    }

    [Fact]
    public void UploadChunk_HasHttpPost()
    {
        var method = GetControllerMethod(
            nameof(FileController.UploadChunk),
            "organizationId", "projectId", "dataSourceId", "objectStorageId", "chunk", "uploadId", "chunkNumber");

        AssertHasHttpAttribute(method, nameof(HttpPostAttribute));
    }

    [Fact]
    public void UploadChunk_HasRequestSizeLimit()
    {
        var method = GetControllerMethod(
            nameof(FileController.UploadChunk),
            "organizationId", "projectId", "dataSourceId", "objectStorageId", "chunk", "uploadId", "chunkNumber");

        AssertHasHttpAttribute(method, nameof(RequestSizeLimitAttribute));
    }

    #endregion

    // =========================================================================
    // CompleteUpload Tests
    // =========================================================================

    #region CompleteUpload Tests

    [Fact]
    public async Task CompleteUpload_Returns200_WithRecord()
    {
        // Arrange
        var request = new FileUploadCompleteRequestDto { UploadId = UploadId, FileName = "big.zip" };
        var expected = new RecordResponseDto();

        _mockFileBusiness
            .Setup(b => b.CompleteUpload(
                UserId, OrgId, ProjectId, DataSourceId, ObjectStorageId, request, null, request.Metadata, false, null, null))
            .ReturnsAsync(expected);

        // Act
        var actionResult = await _fileController.CompleteUpload(
            OrgId, ProjectId, DataSourceId, ObjectStorageId, request, null);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task CompleteUpload_Returns500_OnUnexpectedException()
    {
        // Arrange
        var request = new FileUploadCompleteRequestDto { UploadId = UploadId, FileName = "big.zip" };

        _mockFileBusiness
            .Setup(b => b.CompleteUpload(
                It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long?>(),
                It.IsAny<FileUploadCompleteRequestDto>(), It.IsAny<List<long>?>(), It.IsAny<CreateRecordFileUploadRequestDto?>(),
                It.IsAny<bool>(), It.IsAny<long?>(), It.IsAny<long?>()))
            .ThrowsAsync(new Exception("complete error"));

        // Act
        var actionResult = await _fileController.CompleteUpload(
            OrgId, ProjectId, DataSourceId, ObjectStorageId, request, null);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult.Result);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);

        var message = Assert.IsType<string>(result.Value);
        Assert.Contains($"An error occurred while completing file upload {request.UploadId}", message);
        Assert.Contains("complete error", message);
    }

    [Fact]
    public async Task CompleteUpload_PassesArgumentsToBusinessLayer()
    {
        // Arrange
        var request = new FileUploadCompleteRequestDto { UploadId = UploadId, FileName = "big.zip" };
        var labelIds = new List<long> { 5L };
        const bool embed = true;
        const long vlmConfigId = 100L;
        const long embeddingModelConfigId = 200L;

        var expected = new RecordResponseDto();

        _mockFileBusiness
            .Setup(b => b.CompleteUpload(
                UserId, OrgId, ProjectId, DataSourceId, ObjectStorageId, request, labelIds, request.Metadata,
                embed, vlmConfigId, embeddingModelConfigId))
            .ReturnsAsync(expected);

        // Act
        await _fileController.CompleteUpload(
            OrgId, ProjectId, DataSourceId, ObjectStorageId, request, labelIds, embed, vlmConfigId, embeddingModelConfigId);

        // Assert
        _mockFileBusiness.Verify(
            b => b.CompleteUpload(
                UserId, OrgId, ProjectId, DataSourceId, ObjectStorageId, request, labelIds, request.Metadata,
                embed, vlmConfigId, embeddingModelConfigId),
            Times.Once);
    }

    [Fact]
    public void CompleteUpload_HasHttpPost()
    {
        var method = GetControllerMethod(
            nameof(FileController.CompleteUpload),
            "organizationId", "projectId", "dataSourceId", "objectStorageId", "request");

        AssertHasHttpAttribute(method, nameof(HttpPostAttribute));
    }

    #endregion

    // =========================================================================
    // CancelUpload Tests
    // =========================================================================

    #region CancelUpload Tests

    [Fact]
    public async Task CancelUpload_Returns200_WithMessage()
    {
        // Arrange
        _mockFileBusiness
            .Setup(b => b.CancelUpload(UserId, OrgId, ProjectId, DataSourceId, ObjectStorageId, UploadId))
            .Returns(Task.CompletedTask);

        // Act
        var actionResult = await _fileController.CancelUpload(OrgId, ProjectId, DataSourceId, ObjectStorageId, UploadId);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(
            $"Upload {UploadId} cancelled successfully",
            GetMessageFromResultValue(result.Value));
    }

    [Fact]
    public async Task CancelUpload_Returns500_OnUnexpectedException()
    {
        // Arrange
        _mockFileBusiness
            .Setup(b => b.CancelUpload(
                It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long?>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("cancel error"));

        // Act
        var actionResult = await _fileController.CancelUpload(OrgId, ProjectId, DataSourceId, ObjectStorageId, UploadId);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);

        var message = Assert.IsType<string>(result.Value);
        Assert.Contains($"An error occurred while cancelling upload {UploadId}", message);
        Assert.Contains("cancel error", message);
    }

    [Fact]
    public async Task CancelUpload_PassesArgumentsToBusinessLayer()
    {
        // Arrange
        _mockFileBusiness
            .Setup(b => b.CancelUpload(UserId, OrgId, ProjectId, DataSourceId, ObjectStorageId, UploadId))
            .Returns(Task.CompletedTask);

        // Act
        await _fileController.CancelUpload(OrgId, ProjectId, DataSourceId, ObjectStorageId, UploadId);

        // Assert
        _mockFileBusiness.Verify(
            b => b.CancelUpload(UserId, OrgId, ProjectId, DataSourceId, ObjectStorageId, UploadId),
            Times.Once);
    }

    [Fact]
    public void CancelUpload_HasHttpDelete()
    {
        var method = GetControllerMethod(
            nameof(FileController.CancelUpload),
            "organizationId", "projectId", "dataSourceId", "objectStorageId", "uploadId");

        AssertHasHttpAttribute(method, nameof(HttpDeleteAttribute));
    }

    #endregion

    // =========================================================================
    // CreateUploadTus Tests
    // =========================================================================

    #region CreateUploadTus Tests

    private static DefaultHttpContext CreateHttpContextWithHeaders(IDictionary<string, string> headers)
    {
        var context = new DefaultHttpContext();
        foreach (var (key, value) in headers)
            context.Request.Headers[key] = value;
        return context;
    }

    private static string EncodeMetadata(string key, string value) =>
        $"{key} {Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value))}";

    [Fact]
    public async Task CreateUploadTus_Returns412_WhenTusResumableHeaderMissing()
    {
        // Arrange
        _fileController.ControllerContext = new ControllerContext
        {
            HttpContext = CreateHttpContextWithHeaders(new Dictionary<string, string>())
        };

        // Act
        var actionResult = await _fileController.CreateUploadTus(OrgId, ProjectId, UserId, DataSourceId, ObjectStorageId);

        // Assert
        var result = Assert.IsType<StatusCodeResult>(actionResult);
        Assert.Equal(412, result.StatusCode);
        Assert.Equal("1.0.0", _fileController.Response.Headers["Tus-Resumable"]);
    }

    [Fact]
    public async Task CreateUploadTus_Returns412_WhenTusResumableHeaderWrongVersion()
    {
        // Arrange
        _fileController.ControllerContext = new ControllerContext
        {
            HttpContext = CreateHttpContextWithHeaders(new Dictionary<string, string>
            {
                ["Tus-Resumable"] = "0.9.0"
            })
        };

        // Act
        var actionResult = await _fileController.CreateUploadTus(OrgId, ProjectId, UserId, DataSourceId, ObjectStorageId);

        // Assert
        var result = Assert.IsType<StatusCodeResult>(actionResult);
        Assert.Equal(412, result.StatusCode);
    }

    [Fact]
    public async Task CreateUploadTus_ReturnsBadRequest_WhenUploadLengthHeaderMissing()
    {
        // Arrange
        _fileController.ControllerContext = new ControllerContext
        {
            HttpContext = CreateHttpContextWithHeaders(new Dictionary<string, string>
            {
                ["Tus-Resumable"] = "1.0.0"
            })
        };

        // Act
        var actionResult = await _fileController.CreateUploadTus(OrgId, ProjectId, UserId, DataSourceId, ObjectStorageId);

        // Assert
        var result = Assert.IsType<BadRequestObjectResult>(actionResult);
        Assert.Equal("Missing or invalid Upload-Length header", result.Value);
    }

    [Fact]
    public async Task CreateUploadTus_ReturnsBadRequest_WhenUploadMetadataHeaderMissing()
    {
        // Arrange
        _fileController.ControllerContext = new ControllerContext
        {
            HttpContext = CreateHttpContextWithHeaders(new Dictionary<string, string>
            {
                ["Tus-Resumable"] = "1.0.0",
                ["Upload-Length"] = "1000"
            })
        };

        // Act
        var actionResult = await _fileController.CreateUploadTus(OrgId, ProjectId, UserId, DataSourceId, ObjectStorageId);

        // Assert
        var result = Assert.IsType<BadRequestObjectResult>(actionResult);
        Assert.Equal("Missing Upload-Metadata header", result.Value);
    }

    [Fact]
    public async Task CreateUploadTus_ReturnsBadRequest_WhenFilenameMissingFromMetadata()
    {
        // Arrange
        _fileController.ControllerContext = new ControllerContext
        {
            HttpContext = CreateHttpContextWithHeaders(new Dictionary<string, string>
            {
                ["Tus-Resumable"] = "1.0.0",
                ["Upload-Length"] = "1000",
                ["Upload-Metadata"] = EncodeMetadata("other", "value")
            })
        };

        // Act
        var actionResult = await _fileController.CreateUploadTus(OrgId, ProjectId, UserId, DataSourceId, ObjectStorageId);

        // Assert
        var result = Assert.IsType<BadRequestObjectResult>(actionResult);
        Assert.Equal("Missing filename in Upload-Metadata header", result.Value);
    }

    [Fact]
    public async Task CreateUploadTus_Returns201_WithLocationHeader_OnSuccess()
    {
        // Arrange
        const string fileName = "myfile.pdf";
        const long uploadLength = 1000L;

        _fileController.ControllerContext = new ControllerContext
        {
            HttpContext = CreateHttpContextWithHeaders(new Dictionary<string, string>
            {
                ["Tus-Resumable"] = "1.0.0",
                ["Upload-Length"] = uploadLength.ToString(),
                ["Upload-Metadata"] = EncodeMetadata("filename", fileName)
            })
        };

        var uploadSession = new TusFileUploadSessionResponseDto { UploadId = UploadId };

        _mockFileBusiness
            .Setup(b => b.CreateUploadTus(
                OrgId, ProjectId, DataSourceId, ObjectStorageId,
                It.Is<FileUploadInitRequestDto>(r => r.FileName == fileName && r.FileSize == uploadLength)))
            .ReturnsAsync(uploadSession);

        // Act
        var actionResult = await _fileController.CreateUploadTus(OrgId, ProjectId, UserId, DataSourceId, ObjectStorageId);

        // Assert
        var result = Assert.IsType<StatusCodeResult>(actionResult);
        Assert.Equal(201, result.StatusCode);
        Assert.Equal("1.0.0", _fileController.Response.Headers["Tus-Resumable"]);
        Assert.Equal(
            $"/api/v1/organizations/{OrgId}/projects/{ProjectId}/files/res-upload/{UploadId}",
            _fileController.Response.Headers["Location"]);
    }

    [Fact]
    public async Task CreateUploadTus_Returns500_OnUnexpectedException()
    {
        // Arrange
        const string fileName = "myfile.pdf";

        _fileController.ControllerContext = new ControllerContext
        {
            HttpContext = CreateHttpContextWithHeaders(new Dictionary<string, string>
            {
                ["Tus-Resumable"] = "1.0.0",
                ["Upload-Length"] = "1000",
                ["Upload-Metadata"] = EncodeMetadata("filename", fileName)
            })
        };

        _mockFileBusiness
            .Setup(b => b.CreateUploadTus(
                It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long?>(),
                It.IsAny<FileUploadInitRequestDto>()))
            .ThrowsAsync(new Exception("tus create error"));

        // Act
        var actionResult = await _fileController.CreateUploadTus(OrgId, ProjectId, UserId, DataSourceId, ObjectStorageId);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);

        var message = Assert.IsType<string>(result.Value);
        Assert.Contains("An error occurred while creating upload", message);
        Assert.Contains("tus create error", message);
    }

    [Fact]
    public void CreateUploadTus_HasHttpPost()
    {
        var method = GetControllerMethod(
            nameof(FileController.CreateUploadTus),
            "organizationId", "projectId", "userId", "dataSourceId", "objectStorageId");

        AssertHasHttpAttribute(method, nameof(HttpPostAttribute));
    }

    #endregion

    // =========================================================================
    // GetUploadOffsetTus Tests
    // =========================================================================

    #region GetUploadOffsetTus Tests

    [Fact]
    public async Task GetUploadOffsetTus_Returns412_WhenTusResumableHeaderMissing()
    {
        // Arrange
        _fileController.ControllerContext = new ControllerContext
        {
            HttpContext = CreateHttpContextWithHeaders(new Dictionary<string, string>())
        };

        // Act
        var actionResult = await _fileController.GetUploadOffsetTus(
            OrgId, ProjectId, UploadId, DataSourceId, ObjectStorageId);

        // Assert
        var result = Assert.IsType<StatusCodeResult>(actionResult);
        Assert.Equal(412, result.StatusCode);
    }

    [Fact]
    public async Task GetUploadOffsetTus_ReturnsNoContent_WithOffsetHeaders_OnSuccess()
    {
        // Arrange
        _fileController.ControllerContext = new ControllerContext
        {
            HttpContext = CreateHttpContextWithHeaders(new Dictionary<string, string>
            {
                ["Tus-Resumable"] = "1.0.0"
            })
        };

        _mockFileBusiness
            .Setup(b => b.GetUploadOffsetTus(OrgId, ProjectId, DataSourceId, ObjectStorageId, UploadId))
            .ReturnsAsync((500L, 1000L));

        // Act
        var actionResult = await _fileController.GetUploadOffsetTus(
            OrgId, ProjectId, UploadId, DataSourceId, ObjectStorageId);

        // Assert
        Assert.IsType<NoContentResult>(actionResult);
        Assert.Equal("1.0.0", _fileController.Response.Headers["Tus-Resumable"]);
        Assert.Equal("500", _fileController.Response.Headers["Upload-Offset"]);
        Assert.Equal("1000", _fileController.Response.Headers["Upload-Length"]);
        Assert.Equal("no-store", _fileController.Response.Headers["Cache-Control"]);
    }

    [Fact]
    public async Task GetUploadOffsetTus_Returns500_OnUnexpectedException()
    {
        // Arrange
        _fileController.ControllerContext = new ControllerContext
        {
            HttpContext = CreateHttpContextWithHeaders(new Dictionary<string, string>
            {
                ["Tus-Resumable"] = "1.0.0"
            })
        };

        _mockFileBusiness
            .Setup(b => b.GetUploadOffsetTus(
                It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long?>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("offset error"));

        // Act
        var actionResult = await _fileController.GetUploadOffsetTus(
            OrgId, ProjectId, UploadId, DataSourceId, ObjectStorageId);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);

        var message = Assert.IsType<string>(result.Value);
        Assert.Contains($"An error occurred while getting offset for upload {UploadId}", message);
        Assert.Contains("offset error", message);
    }

    [Fact]
    public void GetUploadOffsetTus_HasHttpHead()
    {
        var method = GetControllerMethod(
            nameof(FileController.GetUploadOffsetTus),
            "organizationId", "projectId", "uploadId", "dataSourceId", "objectStorageId");

        AssertHasHttpAttribute(method, nameof(HttpHeadAttribute));
    }

    #endregion

    // =========================================================================
    // UploadPartTus Tests
    // =========================================================================

    #region UploadPartTus Tests

    [Fact]
    public async Task UploadPartTus_Returns412_WhenTusResumableHeaderMissing()
    {
        // Arrange
        _fileController.ControllerContext = new ControllerContext
        {
            HttpContext = CreateHttpContextWithHeaders(new Dictionary<string, string>())
        };

        // Act
        var actionResult = await _fileController.UploadPartTus(
            OrgId, ProjectId, UploadId, UserId, DataSourceId, ObjectStorageId);

        // Assert
        var result = Assert.IsType<StatusCodeResult>(actionResult);
        Assert.Equal(412, result.StatusCode);
    }

    [Fact]
    public async Task UploadPartTus_ReturnsBadRequest_WhenUploadOffsetHeaderMissing()
    {
        // Arrange
        _fileController.ControllerContext = new ControllerContext
        {
            HttpContext = CreateHttpContextWithHeaders(new Dictionary<string, string>
            {
                ["Tus-Resumable"] = "1.0.0"
            })
        };

        // Act
        var actionResult = await _fileController.UploadPartTus(
            OrgId, ProjectId, UploadId, UserId, DataSourceId, ObjectStorageId);

        // Assert
        var result = Assert.IsType<BadRequestObjectResult>(actionResult);
        Assert.Equal("Missing or invalid Upload-Offset header", result.Value);
    }

    [Fact]
    public async Task UploadPartTus_Returns415_WhenContentTypeIncorrect()
    {
        // Arrange
        _fileController.ControllerContext = new ControllerContext
        {
            HttpContext = CreateHttpContextWithHeaders(new Dictionary<string, string>
            {
                ["Tus-Resumable"] = "1.0.0",
                ["Upload-Offset"] = "0",
                ["Content-Type"] = "application/json"
            })
        };

        // Act
        var actionResult = await _fileController.UploadPartTus(
            OrgId, ProjectId, UploadId, UserId, DataSourceId, ObjectStorageId);

        // Assert
        var result = Assert.IsType<StatusCodeResult>(actionResult);
        Assert.Equal(415, result.StatusCode);
    }

    [Fact]
    public async Task UploadPartTus_ReturnsNoContent_WithNewOffset_OnSuccess()
    {
        // Arrange
        const long startOffset = 0L;
        const long newOffset = 500L;

        var httpContext = CreateHttpContextWithHeaders(new Dictionary<string, string>
        {
            ["Tus-Resumable"] = "1.0.0",
            ["Upload-Offset"] = startOffset.ToString(),
            ["Content-Type"] = "application/offset+octet-stream"
        });

        _fileController.ControllerContext = new ControllerContext { HttpContext = httpContext };

        _mockFileBusiness
            .Setup(b => b.UploadPartTus(
                OrgId, ProjectId, DataSourceId, ObjectStorageId, UploadId, startOffset, UserId,
                It.IsAny<Stream>(), null, null, false, null, null))
            .ReturnsAsync(newOffset);

        // Act
        var actionResult = await _fileController.UploadPartTus(
            OrgId, ProjectId, UploadId, UserId, DataSourceId, ObjectStorageId);

        // Assert
        Assert.IsType<NoContentResult>(actionResult);
        Assert.Equal("1.0.0", _fileController.Response.Headers["Tus-Resumable"]);
        Assert.Equal(newOffset.ToString(), _fileController.Response.Headers["Upload-Offset"]);
    }

    [Fact]
    public async Task UploadPartTus_Returns500_OnUnexpectedException()
    {
        // Arrange
        _fileController.ControllerContext = new ControllerContext
        {
            HttpContext = CreateHttpContextWithHeaders(new Dictionary<string, string>
            {
                ["Tus-Resumable"] = "1.0.0",
                ["Upload-Offset"] = "0",
                ["Content-Type"] = "application/offset+octet-stream"
            })
        };

        _mockFileBusiness
            .Setup(b => b.UploadPartTus(
                It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long?>(), It.IsAny<string>(),
                It.IsAny<long>(), It.IsAny<long>(), It.IsAny<Stream>(), It.IsAny<List<long>?>(),
                It.IsAny<CreateRecordFileUploadRequestDto?>(), It.IsAny<bool>(), It.IsAny<long?>(), It.IsAny<long?>()))
            .ThrowsAsync(new Exception("part upload error"));

        // Act
        var actionResult = await _fileController.UploadPartTus(
            OrgId, ProjectId, UploadId, UserId, DataSourceId, ObjectStorageId);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);

        var message = Assert.IsType<string>(result.Value);
        Assert.Contains($"An error occurred while uploading chunk for upload {UploadId}", message);
        Assert.Contains("part upload error", message);
    }

    [Fact]
    public void UploadPartTus_HasHttpPatch()
    {
        var method = GetControllerMethod(
            nameof(FileController.UploadPartTus),
            "organizationId", "projectId", "uploadId", "userId", "dataSourceId", "objectStorageId");

        AssertHasHttpAttribute(method, nameof(HttpPatchAttribute));
    }

    #endregion

    // =========================================================================
    // CancelTusUpload Tests
    // =========================================================================

    #region CancelTusUpload Tests

    [Fact]
    public async Task CancelTusUpload_Returns412_WhenTusResumableHeaderMissing()
    {
        // Arrange
        _fileController.ControllerContext = new ControllerContext
        {
            HttpContext = CreateHttpContextWithHeaders(new Dictionary<string, string>())
        };

        // Act
        var actionResult = await _fileController.CancelTusUpload(
            OrgId, ProjectId, UploadId, DataSourceId, ObjectStorageId);

        // Assert
        var result = Assert.IsType<StatusCodeResult>(actionResult);
        Assert.Equal(412, result.StatusCode);
    }

    [Fact]
    public async Task CancelTusUpload_ReturnsNoContent_OnSuccess()
    {
        // Arrange
        _fileController.ControllerContext = new ControllerContext
        {
            HttpContext = CreateHttpContextWithHeaders(new Dictionary<string, string>
            {
                ["Tus-Resumable"] = "1.0.0"
            })
        };

        _mockFileBusiness
            .Setup(b => b.CancelUpload(UserId, OrgId, ProjectId, DataSourceId, ObjectStorageId, UploadId))
            .Returns(Task.CompletedTask);

        // Act
        var actionResult = await _fileController.CancelTusUpload(
            OrgId, ProjectId, UploadId, DataSourceId, ObjectStorageId);

        // Assert
        Assert.IsType<NoContentResult>(actionResult);
        Assert.Equal("1.0.0", _fileController.Response.Headers["Tus-Resumable"]);

        _mockFileBusiness.Verify(
            b => b.CancelUpload(UserId, OrgId, ProjectId, DataSourceId, ObjectStorageId, UploadId),
            Times.Once);
    }

    [Fact]
    public async Task CancelTusUpload_Returns500_OnUnexpectedException()
    {
        // Arrange
        _fileController.ControllerContext = new ControllerContext
        {
            HttpContext = CreateHttpContextWithHeaders(new Dictionary<string, string>
            {
                ["Tus-Resumable"] = "1.0.0"
            })
        };

        _mockFileBusiness
            .Setup(b => b.CancelUpload(
                It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>(), It.IsAny<long?>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("cancel tus error"));

        // Act
        var actionResult = await _fileController.CancelTusUpload(
            OrgId, ProjectId, UploadId, DataSourceId, ObjectStorageId);

        // Assert
        var result = Assert.IsType<ObjectResult>(actionResult);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);

        var message = Assert.IsType<string>(result.Value);
        Assert.Contains($"An error occurred while cancelling upload {UploadId}", message);
        Assert.Contains("cancel tus error", message);
    }

    [Fact]
    public void CancelTusUpload_HasHttpDelete()
    {
        var method = GetControllerMethod(
            nameof(FileController.CancelTusUpload),
            "organizationId", "projectId", "uploadId", "dataSourceId", "objectStorageId");

        AssertHasHttpAttribute(method, nameof(HttpDeleteAttribute));
    }

    #endregion

    // =========================================================================
    // Test Helpers
    // =========================================================================

    private static void AssertHasHttpAttribute(
        System.Reflection.MethodInfo method,
        string expectedAttributeName)
    {
        Assert.Contains(method.GetCustomAttributesData(), attribute =>
            attribute.AttributeType.Name == expectedAttributeName);
    }

    private static System.Reflection.MethodInfo GetControllerMethod(
        string methodName,
        params string[] parameterNames)
    {
        return Assert.Single(typeof(FileController).GetMethods()
            .Where(method => method.Name == methodName)
            .Where(method => parameterNames.All(parameterName =>
                method.GetParameters().Any(parameter => parameter.Name == parameterName))));
    }

    private static string GetMessageFromResultValue(object? value)
    {
        Assert.NotNull(value);

        var messageProperty = value.GetType().GetProperty("message");
        Assert.NotNull(messageProperty);

        var message = messageProperty.GetValue(value) as string;
        Assert.NotNull(message);

        return message;
    }
}