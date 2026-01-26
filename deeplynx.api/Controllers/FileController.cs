using deeplynx.business;
using deeplynx.helpers;
using deeplynx.helpers.Context;
using deeplynx.models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace deeplynx.api.Controllers;

/// <summary>
///     Controller for managing files.
/// </summary>
/// <remarks>
///     This controller provides endpoints to upload, update, download, and delete file information.
/// </remarks>
[ApiController]
[Route("organizations/{organizationId:long}/projects/{projectId:long}/files")]
[Authorize]
public class FileController : ControllerBase
{
    private readonly FileBusiness _fileBusiness;
    private readonly ILogger<FileController> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="FileController" /> class
    /// </summary>
    /// <param name="fileBusiness">The business logic interface for handling file operations.</param>
    /// <param name="logger">Error/Info logging interface for database log table.</param>
    public FileController(FileBusiness fileBusiness, ILogger<FileController> logger)
    {
        _fileBusiness = fileBusiness;
        _logger = logger;
    }

    /// <summary>
    ///     Upload a File
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the file belongs</param>
    /// <param name="dataSourceId">The ID of the data source to which the file belongs</param>
    /// <param name="objectStorageId">The ID of the object storage method</param>
    /// <param name="file">The file to upload</param>
    /// <returns>Record response DTO containing file information</returns>
    [HttpPost(Name = "api_upload_file")]
    [Auth("write", "file")]
    public async Task<ActionResult<RecordResponseDto>> UploadFile(
        long organizationId,
        long projectId,
        [FromQuery] long? dataSourceId,
        [FromQuery] long? objectStorageId,
        IFormFile file)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var fileUploadInfo =
                await _fileBusiness.UploadFile(currentUserId, organizationId, projectId, dataSourceId, objectStorageId,
                    file);
            return Ok(fileUploadInfo);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while uploading file {file.FileName}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Update a File
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the file belongs</param>
    /// <param name="recordId">The ID of the record that contains file information</param>
    /// <param name="file">The file to replace the old one</param>
    /// <returns>Record response DTO containing updated file information</returns>
    [HttpPut("{recordId:long}", Name = "api_update_file")]
    [Auth("write", "file")]
    public async Task<ActionResult<RecordResponseDto>> UpdateFile(
        long organizationId,
        long projectId,
        long recordId,
        IFormFile file)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var updatedFileInfo =
                await _fileBusiness.UpdateFile(currentUserId, organizationId, projectId, recordId, file);
            return Ok(updatedFileInfo);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while updating file in record {recordId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Download a File
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the file belongs</param>
    /// <param name="recordId">The ID of the record that contains file information</param>
    /// <returns>The file stream for download</returns>
    [HttpGet("{recordId:long}", Name = "api_download_file")]
    [Auth("read", "file")]
    public async Task<IActionResult> DownloadFile(
        long organizationId,
        long projectId,
        long recordId)
    {
        try
        {
            var fileStreamResult = await _fileBusiness.DownloadFile(organizationId, projectId, recordId);
            return fileStreamResult;
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while downloading file in record {recordId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Delete a File
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the file belongs</param>
    /// <param name="recordId">The ID of the record that contains file information</param>
    /// <returns>A message stating the file was successfully deleted.</returns>
    [HttpDelete("{recordId:long}", Name = "api_delete_file")]
    [Auth("write", "file")]
    public async Task<IActionResult> DeleteFile(
        long organizationId,
        long projectId,
        long recordId)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            await _fileBusiness.DeleteFile(currentUserId, organizationId, projectId, recordId);
            return Ok(new { message = $"Deleted record {recordId} and its file" });
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while deleting file in record {recordId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Start Chunked File Upload (For large files over 500MB)
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the file belongs</param>
    /// <param name="dataSourceId">The ID of the data source to which the file belongs</param>
    /// <param name="objectStorageId">The ID of the object storage method</param>
    /// <param name="request">File upload initialization request DTO</param>
    /// <returns>{UploadId, ChunkSize}</returns>
    [HttpPost("upload/start", Name = "api_start_file_upload")]
    [Auth("write", "file")]
    public async Task<IActionResult> StartUpload(
        long organizationId,
        long projectId,
        [FromQuery] long? dataSourceId,
        [FromQuery] long? objectStorageId,
        [FromBody] FileUploadInitRequestDto request)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var uploadSession = await _fileBusiness.StartUpload(
                currentUserId, organizationId, projectId, dataSourceId, objectStorageId, request);
            return Ok(uploadSession);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while starting upload for file {request.FileName}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Upload File Chunk
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the file belongs</param>
    /// <param name="dataSourceId">The ID of the data source to which the file belongs</param>
    /// <param name="objectStorageId">The ID of the object storage method</param>
    /// <param name="chunk">File chunk from form</param>
    /// <param name="uploadId">ID of upload session</param>
    /// <param name="chunkNumber">Chunk number (0-indexed)</param>
    /// <returns>{ChunkUploadStatus}</returns>
    [HttpPost("upload/chunk", Name = "api_upload_file_chunk")]
    [Auth("write", "file")]
    [RequestSizeLimit(500_000_000)] // 500MB limit per chunk
    public async Task<IActionResult> UploadChunk(
        long organizationId,
        long projectId,
        [FromQuery] long? dataSourceId,
        [FromQuery] long? objectStorageId,
        IFormFile chunk,
        [FromForm] string uploadId,
        [FromForm] int chunkNumber)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var chunkUploadStatus = await _fileBusiness.UploadChunk(
                currentUserId, organizationId, projectId, dataSourceId, objectStorageId, chunk, uploadId, chunkNumber);
            return Ok(new { ChunkUploadStatus = chunkUploadStatus });
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while uploading chunk {chunkNumber} for upload {uploadId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Complete Chunked File Upload
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the file belongs</param>
    /// <param name="dataSourceId">The ID of the data source to which the file belongs</param>
    /// <param name="objectStorageId">The ID of the object storage method</param>
    /// <param name="request">File upload completion request DTO</param>
    /// <returns>Record response DTO containing file information</returns>
    [HttpPost("upload/complete", Name = "api_complete_file_upload")]
    [Auth("write", "file")]
    public async Task<ActionResult<RecordResponseDto>> CompleteUpload(
        long organizationId,
        long projectId,
        [FromQuery] long? dataSourceId,
        [FromQuery] long? objectStorageId,
        [FromBody] FileUploadCompleteRequestDto request)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var fileRecord = await _fileBusiness.CompleteUpload(
                currentUserId, organizationId, projectId, dataSourceId, objectStorageId, request);
            return Ok(fileRecord);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while completing file upload {request.UploadId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    ///     Cancel Chunked File Upload
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the file belongs</param>
    /// <param name="dataSourceId">The ID of the data source to which the file belongs</param>
    /// <param name="objectStorageId">The ID of the object storage method</param>
    /// <param name="uploadId">ID of upload session to cancel</param>
    /// <returns>A message stating the upload was successfully cancelled</returns>
    [HttpDelete("upload/{uploadId}", Name = "api_cancel_file_upload")]
    [Auth("write", "file")]
    public async Task<IActionResult> CancelUpload(
        long organizationId,
        long projectId,
        [FromQuery] long? dataSourceId,
        [FromQuery] long? objectStorageId,
        string uploadId)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            await _fileBusiness.CancelUpload(currentUserId, organizationId, projectId, dataSourceId, objectStorageId,
                uploadId);
            return Ok(new { message = $"Upload {uploadId} cancelled successfully" });
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while cancelling upload {uploadId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }
}