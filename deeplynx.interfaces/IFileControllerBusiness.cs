using deeplynx.models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IO;

namespace deeplynx.interfaces;

public interface IFileControllerBusiness
{
    // Upload file
    Task<RecordResponseDto> UploadFile(
        long currentUserId,
        long organizationId,
        long projectId,
        long? dataSourceId,
        long? objectStorageId,
        IFormFile file,
        List<long>? sensitivityLabelIds,
        IFormFile? metadataFile,
        bool embed,
        long? vlmConfigId,
        long? embeddingModelConfigId,
        string? userJwt,
        bool isSysAdmin,
        bool isOrgAdmin,
        bool isProjectAdmin);

    Task<RecordResponseDtoV2> UploadFileV2(
        long currentUserId,
        long organizationId,
        long projectId,
        long? dataSourceId,
        long? objectStorageId,
        IFormFile file,
        List<long>? sensitivityLabelIds,
        IFormFile? metadataFile,
        bool embed,
        long? vlmConfigId,
        long? embeddingModelConfigId,
        string? userJwt,
        bool isSysAdmin,
        bool isOrgAdmin,
        bool isProjectAdmin);

    // Update file
    Task<RecordResponseDto> UpdateFile(
        long currentUserId,
        long organizationId,
        long projectId,
        long recordId,
        IFormFile file,
        long? vlmConfigId,
        long? embeddingModelConfigId,
        string? userJwt,
        IFormFile? metadataFile = null);

    Task<RecordResponseDtoV2> UpdateFileV2(
        long currentUserId,
        long organizationId,
        long projectId,
        long recordId,
        IFormFile file,
        long? vlmConfigId,
        long? embeddingModelConfigId,
        string? userJwt,
        IFormFile? metadataFile = null);

    Task<RecordResponseDto> UpdateFileContentHash(
        long currentUserId,
        long organizationId,
        long projectId,
        long recordId,
        UpdateFileContentHashRequestDto dto);

    Task<RecordResponseDtoV2> UpdateFileContentHashV2(
        long currentUserId,
        long organizationId,
        long projectId,
        long recordId,
        UpdateFileContentHashRequestDto dto);

    // Download file
    Task<FileStreamResult> DownloadFile(
        long currentUserId,
        long organizationId,
        long projectId,
        long recordId,
        bool isSysAdmin,
        bool isOrgAdmin,
        bool isProjectAdmin);

    Task<FileStreamResult> DownloadFileDirect(
        long organizationId,
        long projectId,
        long recordId,
        string token);

    // Download appended file
    Task<FileStreamResult> DownloadAppendedFile(
        long currentUserId,
        long organizationId,
        long projectId,
        long recordId,
        bool isSysAdmin,
        bool isOrgAdmin,
        bool isProjectAdmin,
        CancellationToken cancellationToken);

    // Generate download URL
    Task<string> GenerateDownloadURL(
        long currentUserId,
        long organizationId,
        long projectId,
        long recordId,
        string? directUrl = null);

    // Delete file
    Task<bool> DeleteFile(
        long currentUserId,
        long organizationId,
        long projectId,
        long recordId);

    // Start chunk upload
    Task<FileUploadSessionResponseDto> StartUpload(
        long organizationId,
        long projectId,
        long? dataSourceId,
        long? objectStorageId,
        FileUploadInitRequestDto request,
        CreateRecordFileUploadRequestDto? metadata);

    Task<FileUploadSessionResponseDto> StartUpdateUpload(
        long currentUserId,
        long organizationId,
        long projectId,
        long recordId,
        FileUploadInitRequestDto request);

    // Upload chunk
    Task<string> UploadChunk(
        long organizationId,
        long projectId,
        long? dataSourceId,
        long? objectStorageId,
        IFormFile chunk,
        string uploadId,
        int chunkNumber);

    // Complete upload
    Task<RecordResponseDto> CompleteUpload(
        long currentUserId,
        long organizationId,
        long projectId,
        long? dataSourceId,
        long? objectStorageId,
        FileUploadCompleteRequestDto request,
        List<long>? sensitivityLabelIds,
        CreateRecordFileUploadRequestDto? metadata,
        bool embed,
        long? vlmConfigId,
        long? embeddingModelConfigId);

    Task<RecordResponseDtoV2> CompleteUploadV2(
        long currentUserId,
        long organizationId,
        long projectId,
        long? dataSourceId,
        long? objectStorageId,
        FileUploadCompleteRequestDto request,
        List<long>? sensitivityLabelIds,
        CreateRecordFileUploadRequestDto? metadata,
        bool embed,
        long? vlmConfigId,
        long? embeddingModelConfigId);

    Task<RecordResponseDto> CompleteUpdateUpload(
        long currentUserId,
        long organizationId,
        long projectId,
        long recordId,
        FileUploadCompleteRequestDto request,
        long? vlmConfigId,
        long? embeddingModelConfigId,
        string? userJwt,
        CreateRecordFileUploadRequestDto? metadata = null);

    Task<RecordResponseDtoV2> CompleteUpdateUploadV2(
        long currentUserId,
        long organizationId,
        long projectId,
        long recordId,
        FileUploadCompleteRequestDto request,
        long? vlmConfigId,
        long? embeddingModelConfigId,
        string? userJwt,
        CreateRecordFileUploadRequestDto? metadata = null);

    Task CancelUpdateUpload(
        long currentUserId,
        long organizationId,
        long projectId,
        long recordId,
        string uploadId);

    // Cancel upload
    Task CancelUpload(
        long currentUserId,
        long organizationId,
        long projectId,
        long? dataSourceId,
        long? objectStorageId,
        string uploadId);

    // TUS upload creation
    Task<TusFileUploadSessionResponseDto> CreateUploadTus(
        long organizationId,
        long projectId,
        long? dataSourceId,
        long? objectStorageId,
        FileUploadInitRequestDto request);

    // Get TUS upload offset
    Task<(long, long)> GetUploadOffsetTus(
        long organizationId,
        long projectId,
        long? dataSourceId,
        long? objectStorageId,
        string uploadId);

    // Upload TUS part
    Task<long> UploadPartTus(
        long organizationId,
        long projectId,
        long? dataSourceId,
        long? objectStorageId,
        string uploadId,
        long uploadOffset,
        long currentUserId,
        Stream uploadBody,
        List<long>? sensitivityLabelIds,
        CreateRecordFileUploadRequestDto? metadata,
        bool embed,
        long? vlmConfigId,
        long? embeddingModelConfigId);
}
