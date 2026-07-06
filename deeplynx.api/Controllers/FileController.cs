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
    /// <param name="sensitivityLabelIds">The IDs of the Sensitivity Labels that will be attached to the record</param>
    /// <param name="metadata">Optional metadata that will be appended to the created record</param>
    /// <param name="embed">Boolean value that determines if the file will be embedded by Insight</param>
    /// <param name="vlmConfigId">Optional ID of the VLM model that will be used by Insight if embed is set to true</param>
    /// <param name="embeddingModelConfigId">Optional ID of the Embedding model that will be used by Insight if embed is set to true</param>
    /// <returns>Record response DTO containing file information</returns>
    [HttpPost(Name = "api_upload_file")]
    [Auth("write", "file")]
    [Auth("write", "record")]
    [Sensitivity("upload file")]
    public async Task<ActionResult<RecordResponseDto>> UploadFile(
        long organizationId,
        long projectId,
        [FromQuery] long? dataSourceId,
        [FromQuery] long? objectStorageId,
        IFormFile file,
        [FromQuery] List<long>? sensitivityLabelIds,
        IFormFile? metadata,
        [FromQuery] bool embed = false,
        [FromQuery] long? vlmConfigId = null,
        [FromQuery] long? embeddingModelConfigId = null)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var userJwt = UserContextStorage.Token;
            var isSysAdmin = UserContextStorage.IsSysAdmin;
            var isOrgAdmin = UserContextStorage.IsOrgAdmin;
            var isProjectAdmin = UserContextStorage.IsProjectAdmin;
            var fileUploadInfo =
                await _fileBusiness.UploadFile(currentUserId, organizationId, projectId, dataSourceId, objectStorageId,
                    file, sensitivityLabelIds, metadata, embed, vlmConfigId, embeddingModelConfigId, userJwt, isSysAdmin, isOrgAdmin, isProjectAdmin);
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
    /// <param name="vlmConfigId">Optional ID of the VLM model that will be used by Insight if embed is set to true</param>
    /// <param name="embeddingModelConfigId">Optional ID of the Embedding model that will be used by Insight if embed is set to true</param>
    /// <returns>Record response DTO containing updated file information</returns>
    [HttpPut("{recordId:long}", Name = "api_update_file")]
    [Auth("update", "file")]
    [Auth("update", "record")]
    [Sensitivity("update file")]
    public async Task<ActionResult<RecordResponseDto>> UpdateFile(
        long organizationId,
        long projectId,
        long recordId,
        IFormFile file,
        [FromQuery] long? vlmConfigId = null,
        [FromQuery] long? embeddingModelConfigId = null)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var userJwt = UserContextStorage.Token;
            var updatedFileInfo =
                await _fileBusiness.UpdateFile(currentUserId, organizationId, projectId, recordId, file, vlmConfigId, embeddingModelConfigId, userJwt);
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
    [Sensitivity("download file")]
    public async Task<IActionResult> DownloadFile(
        long organizationId,
        long projectId,
        long recordId)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var isSysAdmin = UserContextStorage.IsSysAdmin;
            var isOrgAdmin = UserContextStorage.IsOrgAdmin;
            var isProjectAdmin = UserContextStorage.IsProjectAdmin;
            var fileStreamResult = await _fileBusiness.DownloadFile(currentUserId, organizationId, projectId, recordId, isSysAdmin, isOrgAdmin, isProjectAdmin);
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
    ///     Generate Download URL
    /// </summary>
    /// <param name="organizationId">The ID of the organization to which the project belongs</param>
    /// <param name="projectId">The ID of the project to which the file belongs</param>
    /// <param name="recordId">The ID of the record that contains file information</param>
    /// <returns>The file stream for download</returns>
    [HttpGet("{recordId:long}/url", Name = "api_download_url")]
    [Auth("read", "file")]
    [Sensitivity("download file")]
    public async Task<ActionResult<string>> GenerateDownloadUrl(
        long organizationId,
        long projectId,
        long recordId)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var fileStreamResult = await _fileBusiness.GenerateDownloadURL(currentUserId, organizationId, projectId, recordId);
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
    [Sensitivity("delete file")]
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
            var uploadSession = await _fileBusiness.StartUpload(
                organizationId, projectId, dataSourceId, objectStorageId, request);
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
            var chunkUploadStatus = await _fileBusiness.UploadChunk(
                organizationId, projectId, dataSourceId, objectStorageId, chunk, uploadId, chunkNumber);
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
    /// <param name="request">File upload completion request DTO with optional metadata DTO</param>
    /// <param name="sensitivityLabelIds">Optional List of ID's for the Sensitivity Labels to be associated with this file/record</param>
    /// <param name="embed">Optional boolean that determines if the file will be embedded by Insight</param>
    /// <param name="vlmConfigId">Optional ID of the VLM model that will be used by Insight if embed is set to true</param>
    /// <param name="embeddingModelConfigId">Optional ID of the Embedding model that will be used by Insight if embed is set to true</param>
    /// <returns>Record response DTO containing file information</returns>
    [HttpPost("upload/complete", Name = "api_complete_file_upload")]
    [Auth("write", "file")]
    [Sensitivity("upload file")]
    public async Task<ActionResult<RecordResponseDto>> CompleteUpload(
        long organizationId,
        long projectId,
        [FromQuery] long? dataSourceId,
        [FromQuery] long? objectStorageId,
        [FromBody] FileUploadCompleteRequestDto request,
        [FromQuery] List<long>? sensitivityLabelIds,
        [FromQuery] bool embed = false,
        [FromQuery] long? vlmConfigId = null,
        [FromQuery] long? embeddingModelConfigId = null)
    {
        try
        {
            var currentUserId = UserContextStorage.UserId;
            var fileRecord = await _fileBusiness.CompleteUpload(
                currentUserId, organizationId, projectId, dataSourceId, objectStorageId, request, sensitivityLabelIds,
                request.Metadata, embed, vlmConfigId, embeddingModelConfigId);
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

    /// <summary>
    /// Create Resumable Upload
    /// </summary>
    /// <remarks>Creates a reumable upload according to the tus protocol.</remarks>
    /// <param name="organizationId"></param>
    /// <param name="projectId"></param>
    /// <param name="dataSourceId"></param>
    /// <param name="objectStorageId"></param>
    /// <returns></returns>
    [HttpPost("res-upload", Name = "api_create_resumable_file_upload")]
    [Auth("write", "file")]
    public async Task<IActionResult> CreateUploadTus(
        long organizationId,
        long projectId,
        long userId,
        [FromQuery] long? dataSourceId,
        [FromQuery] long? objectStorageId)
    {
        try
        {
            if (!Request.Headers.TryGetValue("Tus-Resumable", out var tusResumable) || tusResumable != "1.0.0")
            {
                Response.Headers["Tus-Resumable"] = "1.0.0";
                return StatusCode(412);
            }

            if (!Request.Headers.TryGetValue("Upload-Length", out var uploadLengthHeader) ||
                !long.TryParse(uploadLengthHeader, out var uploadLength))
                return BadRequest("Missing or invalid Upload-Length header");

            if (!Request.Headers.TryGetValue("Upload-Metadata", out var uploadMetadata))
                return BadRequest("Missing Upload-Metadata header");

            var fileName = ParseMetadataValue(uploadMetadata, "filename");
            if (string.IsNullOrEmpty(fileName))
                return BadRequest("Missing filename in Upload-Metadata header");

            var request = new FileUploadInitRequestDto
            {
                FileName = fileName,
                FileSize = uploadLength
            };

            var uploadSession = await _fileBusiness.CreateUploadTus(
                organizationId, projectId, dataSourceId, objectStorageId, request);

            Response.Headers["Tus-Resumable"] = "1.0.0";
            Response.Headers["Location"] = $"/api/v1/organizations/{organizationId}/projects/{projectId}/files/res-upload/{uploadSession.UploadId}";

            return StatusCode(201);
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while creating upload: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    /// Get Resumable Offset
    /// </summary>
    /// <remarks>Gets the offset/upload-progress according to the tus protocol.</remarks>
    /// <param name="organizationId"></param>
    /// <param name="projectId"></param>
    /// <param name="uploadId"></param>
    /// <param name="dataSourceId"></param>
    /// <param name="objectStorageId"></param>
    /// <returns></returns>
    [HttpHead("res-upload/{uploadId}", Name = "api_get_resumable_upload_offset")]
    [Auth("write", "file")]
    public async Task<IActionResult> GetUploadOffsetTus(
        long organizationId,
        long projectId,
        string uploadId,
        [FromQuery] long? dataSourceId,
        [FromQuery] long? objectStorageId)
    {
        try
        {
            if (!Request.Headers.TryGetValue("Tus-Resumable", out var tusResumable) || tusResumable != "1.0.0")
            {
                Response.Headers["Tus-Resumable"] = "1.0.0";
                return StatusCode(412);
            }

            var (offset, uploadLength) = await _fileBusiness.GetUploadOffsetTus(organizationId, projectId, dataSourceId, objectStorageId, uploadId);

            Response.Headers["Tus-Resumable"] = "1.0.0";
            Response.Headers["Upload-Offset"] = offset.ToString();
            Response.Headers["Upload-Length"] = uploadLength.ToString();
            Response.Headers["Cache-Control"] = "no-store";
            return NoContent();
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while getting offset for upload {uploadId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    /// Upload Resumable Part
    /// </summary>
    /// <remarks>Uploads a reumable part of a file at the desired offset according to the tus protocol.</remarks>
    /// <param name="organizationId"></param>
    /// <param name="projectId"></param>
    /// <param name="uploadId"></param>
    /// <param name="dataSourceId"></param>
    /// <param name="objectStorageId"></param>
    /// <returns></returns>
    [HttpPatch("res-upload/{uploadId}", Name = "api_patch_resumable_file_upload")]
    [Auth("write", "file")]
    public async Task<IActionResult> UploadPartTus(
        long organizationId,
        long projectId,
        string uploadId,
        long userId,
        [FromQuery] long? dataSourceId,
        [FromQuery] long? objectStorageId)
    {
        try
        {
            if (!Request.Headers.TryGetValue("Tus-Resumable", out var tusResumable) || tusResumable != "1.0.0")
            {
                Response.Headers["Tus-Resumable"] = "1.0.0";
                return StatusCode(412);
            }

            if (!Request.Headers.TryGetValue("Upload-Offset", out var offsetHeader) ||
                !long.TryParse(offsetHeader, out var uploadOffset))
                return BadRequest("Missing or invalid Upload-Offset header");

            if (!Request.Headers.TryGetValue("Content-Type", out var contentType) ||
                contentType != "application/offset+octet-stream")
                return StatusCode(415);

            var newOffset = await _fileBusiness.UploadPartTus(
                organizationId, projectId, dataSourceId, objectStorageId, uploadId, uploadOffset, userId, Request.Body, null, null, false, null, null);

            Response.Headers["Tus-Resumable"] = "1.0.0";
            Response.Headers["Upload-Offset"] = newOffset.ToString();
            return NoContent();
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while uploading chunk for upload {uploadId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }

    /// <summary>
    /// Cancel Resumable Upload
    /// </summary>
    /// <remarks>Cancels a resumable upload with tus protocol reponse.</remarks>
    /// <param name="organizationId"></param>
    /// <param name="projectId"></param>
    /// <param name="uploadId"></param>
    /// <param name="dataSourceId"></param>
    /// <param name="objectStorageId"></param>
    /// <returns></returns>
    [HttpDelete("res-upload/{uploadId}", Name = "api_cancel_resumable_file_upload")]
    [Auth("write", "file")]
    public async Task<IActionResult> CancelTusUpload(
        long organizationId,
        long projectId,
        string uploadId,
        [FromQuery] long? dataSourceId,
        [FromQuery] long? objectStorageId)
    {
        try
        {
            if (!Request.Headers.TryGetValue("Tus-Resumable", out var tusResumable) || tusResumable != "1.0.0")
            {
                Response.Headers["Tus-Resumable"] = "1.0.0";
                return StatusCode(412);
            }

            var currentUserId = UserContextStorage.UserId;
            await _fileBusiness.CancelUpload(currentUserId, organizationId, projectId, dataSourceId, objectStorageId, uploadId);

            Response.Headers["Tus-Resumable"] = "1.0.0";
            return NoContent();
        }
        catch (Exception exc)
        {
            var message = $"An error occurred while cancelling upload {uploadId}: {exc}";
            _logger.LogError(message);
            return StatusCode(StatusCodes.Status500InternalServerError, message);
        }
    }
    //Private helper
    private string ParseMetadataValue(string uploadMetadata, string key)
    {
        foreach (var pair in uploadMetadata.Split(','))
        {
            var parts = pair.Trim().Split(' ');
            if (parts.Length == 2 && parts[0] == key)
                return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(parts[1]));
        }
        return null;
    }
}
