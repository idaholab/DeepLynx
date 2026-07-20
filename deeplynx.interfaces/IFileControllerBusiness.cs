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

    // Update file
    Task<RecordResponseDto> UpdateFile(
        long currentUserId,
        long organizationId,
        long projectId,
        long recordId,
        IFormFile file,
        long? vlmConfigId,
        long? embeddingModelConfigId,
        string? userJwt);

    // Download file
    Task<FileStreamResult> DownloadFile(
        long currentUserId,
        long organizationId,
        long projectId,
        long recordId,
        bool isSysAdmin,
        bool isOrgAdmin,
        bool isProjectAdmin);

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
        long recordId);

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